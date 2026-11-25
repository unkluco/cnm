using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Runes
{
    public enum RuneRecognitionStatus { Accepted, TooShort, BelowThreshold, Ambiguous, Unsupported, Busy, Unavailable }
    public enum RuneBackend { Auto, Cpu, Gpu }

    public static class RuneRecognitionDefaults
    {
        public const float FlexibleThreshold=.80f,StrictThreshold=.90f,Margin=.05f;
        public const bool DefaultFlexible=true;
        public const float GameplayThreshold=FlexibleThreshold;
    }

    public readonly struct RuneMatch
    {
        public readonly RuneRecognitionStatus Status;
        public readonly RuneGlyph Glyph;
        public readonly float Score, Gap, Milliseconds;
        public bool Accepted=>Status==RuneRecognitionStatus.Accepted;
        public RuneMatch(RuneRecognitionStatus status,RuneGlyph glyph=default,float score=0,float gap=0,float milliseconds=0)
        {Status=status;Glyph=glyph;Score=score;Gap=gap;Milliseconds=milliseconds;}
    }

    [Serializable]
    public sealed class RuneModelInfo
    {
        public int formatVersion,inputSize,embeddingSize,referenceCount,modelBytes;
        public string modelName,mergedSha256,onnxSha256,referenceSha256;
        public string[] labels;
        public float gameplayThreshold=RuneRecognitionDefaults.GameplayThreshold,gameplayMargin=RuneRecognitionDefaults.Margin,
            calibratedThreshold=RuneRecognitionDefaults.FlexibleThreshold,calibratedMargin=RuneRecognitionDefaults.Margin;
    }

    /// <summary>Bank contains only normalized reference vectors, no player drawing coordinates.</summary>
    public sealed class RuneReferenceBank
    {
        public const int Dimension=192,AllMask=(1<<RuneGlyphs.Count)-1;
        private readonly float[] vectors;
        public readonly int PerGlyph;

        public RuneReferenceBank(byte[] bytes)
        {
            using var stream=new MemoryStream(bytes,false);
            using var reader=new BinaryReader(stream);
            if(new string(reader.ReadChars(4))!="LSRU"||reader.ReadInt32()!=1||reader.ReadInt32()!=RuneGlyphs.Count)
                throw new InvalidDataException("Invalid rune reference header");
            PerGlyph=reader.ReadInt32();int dimension=reader.ReadInt32();
            if(PerGlyph!=10||dimension!=Dimension||stream.Length!=20L+RuneGlyphs.Count*PerGlyph*Dimension*4L)
                throw new InvalidDataException("Invalid rune reference dimensions");
            vectors=new float[RuneGlyphs.Count*PerGlyph*Dimension];
            for(int i=0;i<vectors.Length;i++)vectors[i]=reader.ReadSingle();
            for(int i=0;i<vectors.Length;i+=Dimension)
            {
                double norm=0;for(int j=0;j<Dimension;j++){float v=vectors[i+j];if(float.IsNaN(v)||float.IsInfinity(v))throw new InvalidDataException("Non-finite vector");norm+=v*v;}
                if(Math.Abs(norm-1)>.005)throw new InvalidDataException("Unnormalized rune reference vector");
            }
        }

        public static int Mask(IReadOnlyList<RuneSkill> kit)
        {
            if(kit==null)return AllMask;
            int mask=0;foreach(var skill in kit)mask|=1<<(int)skill.Glyph;return mask;
        }

        public RuneMatch Match(float[] embedding,int allowedMask,float threshold,float margin,float milliseconds=0)
        {
            if(embedding==null||embedding.Length!=Dimension)return new RuneMatch(RuneRecognitionStatus.Unavailable);
            double sum=0;
            foreach(float x in embedding){if(float.IsNaN(x)||float.IsInfinity(x))return new RuneMatch(RuneRecognitionStatus.Unavailable);sum+=x*x;}
            if(sum<=1e-12)return new RuneMatch(RuneRecognitionStatus.Unavailable);
            float inverse=(float)(1/Math.Sqrt(sum)),best=-2,second=-2;int index=-1;
            // Calculate the GLOBAL winner, then gate it against the equipped
            // runes. Drawing a valid but unequipped rune must not cast its nearest
            // available alternative, even if that alternative scores highly.
            for(int glyph=0;glyph<RuneGlyphs.Count;glyph++)
            {
                float high=-2;
                for(int refIndex=0;refIndex<PerGlyph;refIndex++)
                {
                    int start=(glyph*PerGlyph+refIndex)*Dimension;float dot=0;
                    for(int d=0;d<Dimension;d++)dot+=embedding[d]*vectors[start+d];
                    high=Mathf.Max(high,Mathf.Clamp(dot*inverse,-1,1));
                }
                if(high>best){second=best;best=high;index=glyph;}
                else if(high>second)second=high;
            }
            float gap=best-second;
            RuneRecognitionStatus status=best<threshold?RuneRecognitionStatus.BelowThreshold:
                gap<margin?RuneRecognitionStatus.Ambiguous:
                (allowedMask&(1<<index))==0?RuneRecognitionStatus.Unsupported:RuneRecognitionStatus.Accepted;
            return new RuneMatch(status,(RuneGlyph)index,best,gap,milliseconds);
        }
    }

    /// <summary>
    /// Uses the actual exported LoRA-merged ONNX. No heuristic/fallback gesture
    /// classifier. One request at a time; async readback allows gameplay to
    /// continue while the model executes. CPU fallback only changes backend.
    /// </summary>
    public sealed class RuneClassifier:IDisposable
    {
        private readonly RuneRasterizer raster=new RuneRasterizer();
        private readonly Model model;
        private Worker worker;
        private bool busy,disposeRequested;
        public RuneReferenceBank References{get;}
        public RuneModelInfo Info{get;}
        public bool Busy=>busy;
        public RuneBackend Backend{get;private set;}

        public RuneClassifier(RuneBackend backend=RuneBackend.Auto)
        {
            var asset=Resources.Load<ModelAsset>("RuneCasting/ViTTinyRune");
            var bank=Resources.Load<TextAsset>("RuneCasting/RuneReferences");
            var info=Resources.Load<TextAsset>("RuneCasting/RuneModelInfo");
            if(asset==null||bank==null||info==null)throw new InvalidDataException("Rune model assets are missing");
            Info=JsonUtility.FromJson<RuneModelInfo>(info.text);
            if(Info==null||Info.formatVersion!=1||Info.inputSize!=224||Info.embeddingSize!=192||Info.referenceCount!=120||
                Info.labels==null||Info.labels.Length!=RuneGlyphs.Count)throw new InvalidDataException("Rune model metadata mismatch");
            for(int i=0;i<RuneGlyphs.Count;i++)if(Info.labels[i]!=RuneGlyphs.Id((RuneGlyph)i))throw new InvalidDataException("Rune label order mismatch");
            References=new RuneReferenceBank(bank.bytes);
            model=ModelLoader.Load(asset);
            bool canGpu=SystemInfo.supportsComputeShaders&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null;
            // Burst CPU has lower warmed latency and much less shader warm-up
            // on our desktop Unity runner. Mobile uses compute where available;
            // explicit backends remain available for device validation.
            Backend=backend==RuneBackend.Auto?(canGpu&&Application.isMobilePlatform?RuneBackend.Gpu:RuneBackend.Cpu):backend;
            if(Backend==RuneBackend.Gpu&&!canGpu)Backend=RuneBackend.Cpu;
            worker=new Worker(model,Backend==RuneBackend.Gpu?BackendType.GPUCompute:BackendType.CPU);
        }

        public RuneMatch RecognizeSync(IReadOnlyList<Vector2> points,int mask=RuneReferenceBank.AllMask,
            float threshold=RuneRecognitionDefaults.GameplayThreshold,float margin=RuneRecognitionDefaults.Margin)
        {
            if(busy||disposeRequested||worker==null)return new RuneMatch(RuneRecognitionStatus.Busy);
            if(!raster.TryRender(points,out var input))return new RuneMatch(RuneRecognitionStatus.TooShort);
            var watch=Stopwatch.StartNew();
            return References.Match(EncodeSync(input),mask,threshold,margin,(float)watch.Elapsed.TotalMilliseconds);
        }

        public float[] EncodeSync(float[] normalizedInput)
        {
            using var input=new Tensor<float>(new TensorShape(1,3,224,224),normalizedInput);
            worker.Schedule(input);
            var output=worker.PeekOutput() as Tensor<float>;
            if(output==null)throw new InvalidDataException("Rune model returned no float embedding");
            using var cpu=output.ReadbackAndClone();
            return cpu.DownloadToArray();
        }

        public async Awaitable<RuneMatch> RecognizeAsync(IReadOnlyList<Vector2> points,int mask,float threshold,float margin)
        {
            if(busy||disposeRequested||worker==null)return new RuneMatch(RuneRecognitionStatus.Busy);
            if(!raster.TryRender(points,out var normalizedInput))return new RuneMatch(RuneRecognitionStatus.TooShort);
            busy=true;var watch=Stopwatch.StartNew();
            try
            {
                try{return await EncodeAndMatch(normalizedInput,mask,threshold,margin,watch);}
                catch(Exception ex) when(Backend==RuneBackend.Gpu&&!disposeRequested)
                {
                    UnityEngine.Debug.LogWarning("Rune GPU backend unavailable; switching to CPU: "+ex.Message);
                    worker.Dispose();Backend=RuneBackend.Cpu;worker=new Worker(model,BackendType.CPU);
                    return await EncodeAndMatch(normalizedInput,mask,threshold,margin,watch);
                }
            }
            catch(Exception ex){UnityEngine.Debug.LogWarning("Rune inference failed: "+ex.Message);return new RuneMatch(RuneRecognitionStatus.Unavailable);}
            finally
            {
                busy=false;
                if(disposeRequested){worker?.Dispose();worker=null;}
            }
        }

        private async Awaitable<RuneMatch> EncodeAndMatch(float[] normalizedInput,int mask,float threshold,float margin,Stopwatch watch)
        {
            using var input=new Tensor<float>(new TensorShape(1,3,224,224),normalizedInput);
            worker.Schedule(input);
            var output=worker.PeekOutput() as Tensor<float>;
            if(output==null)throw new InvalidDataException("Rune model returned no embedding");
            using var cpu=await output.ReadbackAndCloneAsync();
            return References.Match(cpu.DownloadToArray(),mask,threshold,margin,(float)watch.Elapsed.TotalMilliseconds);
        }

        public void Dispose()
        {
            disposeRequested=true;
            if(!busy){worker?.Dispose();worker=null;}
        }
    }
}
