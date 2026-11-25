using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Runes
{
    /// <summary>
    /// Offline, allocation-bounded replica of Rune Lab's 224px input: fit the
    /// original aspect/orientation, rasterize a fixed black stroke on white
    /// at 3x resolution, then separable Lanczos downsample and ImageNet RGB
    /// normalization. World/UI colours are never fed into the network.
    /// </summary>
    public sealed class RuneRasterizer
    {
        public const int Size=224, Channels=3, MaxPoints=4096;
        private const int Scale=3, High=Size*Scale;
        private const double Extent=176.0, Radius=13.5;
        private static readonly float[] Mean={.485f,.456f,.406f}, Std={.229f,.224f,.225f};
        private static readonly Kernel[] Kernels=CreateKernels();
        private readonly byte[] high=new byte[High*High], horizontal=new byte[High*Size], ink=new byte[Size*Size];
        private readonly float[] normalized=new float[Channels*Size*Size];

        private sealed class Kernel
        {
            public int Start;
            public float[] Weights;
        }
        private static double Sinc(double x)=>Math.Abs(x)<1e-9?1:Math.Sin(Math.PI*x)/(Math.PI*x);
        private static Kernel[] CreateKernels()
        {
            var result=new Kernel[Size];
            for(int dst=0;dst<Size;dst++)
            {
                double center=(dst+.5)*Scale-.5;
                int start=Math.Max(0,(int)Math.Ceiling(center-3*Scale));
                int end=Math.Min(High-1,(int)Math.Floor(center+3*Scale));
                var weights=new float[end-start+1];double sum=0;
                for(int src=start;src<=end;src++)
                {
                    double delta=(src-center)/Scale;
                    double w=Math.Abs(delta)<3?Sinc(delta)*Sinc(delta/3):0;
                    weights[src-start]=(float)w;sum+=w;
                }
                for(int i=0;i<weights.Length;i++)weights[i]/=(float)sum;
                result[dst]=new Kernel{Start=start,Weights=weights};
            }
            return result;
        }

        public static bool IsValid(IReadOnlyList<Vector2> points)
        {
            if(points==null||points.Count<2||points.Count>MaxPoints)return false;
            Vector2 min=points[0],max=points[0];double length=0;
            for(int i=0;i<points.Count;i++)
            {
                var p=points[i];
                if(float.IsNaN(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.x)||float.IsInfinity(p.y)||p.x<0||p.x>1||p.y<0||p.y>1)return false;
                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
                if(i>0)length+=Vector2.Distance(points[i-1],p);
            }
            return Mathf.Max(max.x-min.x,max.y-min.y)>=.075f&&length>=.09;
        }

        public bool TryRender(IReadOnlyList<Vector2> points,out float[] input)
        {
            input=null;
            if(!IsValid(points))return false;
            Vector2 min=points[0],max=points[0];
            foreach(var p in points){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            double centerX=((double)min.x+max.x)*.5,centerY=((double)min.y+max.y)*.5;
            double fit=Extent/Math.Max((double)max.x-min.x,(double)max.y-min.y);
            Array.Fill(high,(byte)255);
            Vector2Int last=Fit(points[0],centerX,centerY,fit);
            for(int i=1;i<points.Count;i++)
            {
                Vector2Int next=Fit(points[i],centerX,centerY,fit);
                Draw(last,next);last=next;
            }
            for(int y=0;y<High;y++)
            {
                int row=y*High,outRow=y*Size;
                for(int x=0;x<Size;x++)
                {
                    var k=Kernels[x];float value=0;
                    for(int i=0;i<k.Weights.Length;i++)value+=high[row+k.Start+i]*k.Weights[i];
                    horizontal[outRow+x]=Quantize(value);
                }
            }
            for(int y=0;y<Size;y++)
            {
                var k=Kernels[y];
                for(int x=0;x<Size;x++)
                {
                    float value=0;
                    for(int i=0;i<k.Weights.Length;i++)value+=horizontal[(k.Start+i)*Size+x]*k.Weights[i];
                    int index=y*Size+x;ink[index]=Quantize(value);
                    float unit=ink[index]*(1f/255f);
                    for(int c=0;c<Channels;c++)normalized[c*Size*Size+index]=(unit-Mean[c])/Std[c];
                }
            }
            input=normalized;return true;
        }

        // Returned copy is primarily for diagnostic tests, never a screenshot.
        public byte[] CopyInk()=>(byte[])ink.Clone();
        private static byte Quantize(float value)=>(byte)Mathf.Clamp(Mathf.FloorToInt(value+.5f),0,255);
        private static Vector2Int Fit(Vector2 p,double cx,double cy,double fit)
        {
            int Convert(double value,double center)=>(int)Math.Floor(Math.Round((value-center)*fit+(Size-1)*.5,8)*Scale+.5);
            return new Vector2Int(Convert(p.x,cx),Convert(p.y,cy));
        }
        private void Draw(Vector2Int a,Vector2Int b)
        {
            int minX=Math.Max(0,Mathf.FloorToInt((float)(Math.Min(a.x,b.x)-Radius)));
            int maxX=Math.Min(High-1,Mathf.CeilToInt((float)(Math.Max(a.x,b.x)+Radius)));
            int minY=Math.Max(0,Mathf.FloorToInt((float)(Math.Min(a.y,b.y)-Radius)));
            int maxY=Math.Min(High-1,Mathf.CeilToInt((float)(Math.Max(a.y,b.y)+Radius)));
            float dx=b.x-a.x,dy=b.y-a.y,len=dx*dx+dy*dy;
            float inverse=len>0?1f/len:0;
            const float limit=(float)(Radius*Radius);
            for(int y=minY;y<=maxY;y++)
            {
                float oy=y-a.y;int row=y*High;
                for(int x=minX;x<=maxX;x++)
                {
                    float ox=x-a.x,t=Mathf.Clamp01((ox*dx+oy*dy)*inverse);
                    float px=ox-t*dx,py=oy-t*dy;
                    if(px*px+py*py<=limit)high[row+x]=0;
                }
            }
        }
    }
}
