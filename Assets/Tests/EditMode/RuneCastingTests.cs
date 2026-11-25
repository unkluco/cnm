using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LightSouls.Combat.Items;
using LightSouls.Combat.Runes;
using LightSouls.Combat.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class RuneCastingTests
    {
        [Test]
        public void AllFortyFiveRegisteredWeaponsHaveFourUniqueRealSkillsPlusSharedDodge()
        {
            var db=AssetDatabase.LoadAssetAtPath<ItemDatabaseSO>("Assets/ScriptableObjects/Combat/Items/ItemDatabase.asset");
            var weapons=db.AllItems.OfType<WeaponDataSO>().ToArray();
            Assert.AreEqual(45,weapons.Length);Assert.AreEqual(45,RuneSkillCatalog.WeaponCount);
            Assert.AreEqual(180,RuneSkillCatalog.AuthoredWeaponSkillCount);Assert.AreEqual(225,RuneSkillCatalog.SkillCount);
            var names=new HashSet<string>();var forms=new HashSet<RuneForm>();var effects=new HashSet<RuneEffect>();
            foreach(var weapon in weapons)
            {
                var kit=RuneSkillCatalog.For(weapon);Assert.AreEqual(5,kit.Count,weapon.ItemId);
                Assert.AreEqual(5,kit.Select(s=>s.Glyph).Distinct().Count(),weapon.ItemId);
                Assert.AreEqual(1,kit.Count(s=>s.Basic),weapon.ItemId);
                Assert.AreSame(RuneSkillCatalog.DashSkill,kit[4],"Dodge must not change rune when swapping weapons");
                foreach(var skill in kit)
                {
                    if(skill.Form!=RuneForm.Dash)Assert.IsTrue(names.Add(skill.Name),"Duplicate skill name: "+skill.Name);
                    Assert.AreSame(skill,RuneSkillCatalog.Find(weapon,skill.Glyph));
                    Assert.Greater(skill.BaseCooldown(weapon),0,skill.Name);
                    Assert.IsTrue(skill.Basic||skill.Form==RuneForm.Dash||skill.Cooldown>=2f,skill.Name);
                    Assert.IsNotEmpty(skill.Description(weapon));
                    Assert.NotNull(RuneGlyphs.Icon(skill.Glyph));
                    forms.Add(skill.Form);effects.Add(skill.Effect);
                }
            }
            Assert.AreEqual(180,names.Count);
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(RuneForm)),forms);
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(RuneEffect)),effects);
        }

        [Test]
        public void RasterizerRejectsShortMalformedStrokesAndPreservesAspectAndOrientation()
        {
            var raster=new RuneRasterizer();
            Assert.IsFalse(raster.TryRender(new[]{new Vector2(.5f,.5f)},out _));
            Assert.IsFalse(raster.TryRender(new[]{new Vector2(.5f,.5f),new Vector2(.52f,.55f)},out _));
            Assert.IsFalse(raster.TryRender(new[]{Vector2.zero,new Vector2(float.NaN,.7f)},out _));
            Assert.IsTrue(raster.TryRender(RuneGlyphs.Example(RuneGlyph.Diagonal),out var tensor));
            Assert.AreEqual(3*224*224,tensor.Length);var original=raster.CopyInk();
            var shifted=RuneGlyphs.Example(RuneGlyph.Diagonal).Select(p=>p*.6f+new Vector2(.05f,.19f)).ToArray();
            Assert.IsTrue(raster.TryRender(shifted,out _));
            var transformed=raster.CopyInk();
            double mae=original.Zip(transformed,(a,b)=>Math.Abs(a-b)).Average();Assert.Less(mae,1f);
            var reflected=shifted.Select(p=>new Vector2(p.x,1-p.y)).ToArray();
            Assert.IsTrue(raster.TryRender(reflected,out _));
            Assert.Greater(original.Zip(raster.CopyInk(),(a,b)=>Math.Abs(a-b)).Average(),10f);
        }

        [Test,Timeout(180000)]
        public void ImportedOnnxRecognizesReferenceGlyphsAndRejectsUnequippedWinner()
        {
            using var classifier=new RuneClassifier(RuneBackend.Cpu);
            Assert.AreEqual(120,classifier.Info.referenceCount);
            Assert.IsTrue(RuneRecognitionDefaults.DefaultFlexible);
            Assert.AreEqual(.80f,RuneRecognitionDefaults.GameplayThreshold);
            Assert.AreEqual(RuneRecognitionDefaults.GameplayThreshold,classifier.Info.gameplayThreshold);
            Assert.AreEqual(RuneRecognitionDefaults.Margin,classifier.Info.gameplayMargin);
            Assert.AreEqual(10,classifier.References.PerGlyph);
            Assert.AreEqual(22_180_842,classifier.Info.modelBytes);
            int accepted=0;
            for(int i=0;i<RuneGlyphs.Count;i++)
            {
                RuneGlyph glyph=(RuneGlyph)i;
                var match=classifier.RecognizeSync(RuneGlyphs.Example(glyph),RuneReferenceBank.AllMask,
                    RuneRecognitionDefaults.StrictThreshold,RuneRecognitionDefaults.Margin);
                Debug.Log($"Rune canonical {glyph}: {match.Status} {match.Glyph} similarity={match.Score:F4} gap={match.Gap:F4} inference={match.Milliseconds:F1}ms");
                Assert.AreEqual(glyph,match.Glyph,RuneGlyphs.Name(glyph));
                Assert.GreaterOrEqual(match.Score,.80f,glyph.ToString());
                if(match.Accepted)accepted++;
            }
            Assert.GreaterOrEqual(accepted,10,"On-screen templates must be recognizable at strict threshold");
            var unsupported=classifier.RecognizeSync(RuneGlyphs.Example(RuneGlyph.Slash),1<<(int)RuneGlyph.Wall,.8f,.05f);
            Assert.AreEqual(RuneRecognitionStatus.Unsupported,unsupported.Status);
            var invalid=classifier.RecognizeSync(new[]{Vector2.one*.5f});
            Assert.AreEqual(RuneRecognitionStatus.TooShort,invalid.Status);
            // A quick dodge can be drawn from either end of its template.
            var reverse=RuneGlyphs.Example(RuneGlyph.Diagonal).Reverse().ToArray();
            var dodge=classifier.RecognizeSync(reverse,1<<(int)RuneGlyph.Diagonal,.90f,.05f);
            Assert.IsTrue(dodge.Accepted&&dodge.Glyph==RuneGlyph.Diagonal);
            var mirrored=RuneGlyphs.Example(RuneGlyph.Diagonal).Select(p=>new Vector2(1-p.x,p.y)).ToArray();
            dodge=classifier.RecognizeSync(mirrored,1<<(int)RuneGlyph.Diagonal,.90f,.05f);
            Assert.IsTrue(dodge.Accepted&&dodge.Glyph==RuneGlyph.Diagonal,$"Mirrored dodge: {dodge.Status}, {dodge.Score:F4}");
        }

        [Serializable] private sealed class HeldoutCase{public int label;public Vector2[] points;}
        [Serializable] private sealed class HeldoutSet{public HeldoutCase[] cases;}

        // The optional real holdout stays on its writer's machine; no raw strokes are committed.
        [Test,Timeout(300000)]
        public void LocalUnseenStrokeHoldoutUsesExactUnityPreprocessingAndModel()
        {
            var path=Environment.GetEnvironmentVariable("LIGHTSOULS_RUNE_HOLDOUT");
            if(string.IsNullOrEmpty(path)||!File.Exists(path))Assert.Ignore("Optional local holdout fixture not supplied.");
            var data=JsonUtility.FromJson<HeldoutSet>(File.ReadAllText(path));
            Assert.AreEqual(65,data.cases.Length);
            using var classifier=new RuneClassifier(RuneBackend.Cpu);
            int known=0,unknown=0,top1=0,strict=0,flexible=0,unknownStrict=0,unknownFlexible=0,wrongStrict=0;var inference=new List<float>();
            foreach(var row in data.cases)
            {
                var m=classifier.RecognizeSync(row.points,RuneReferenceBank.AllMask,.80f,.05f);
                if(m.Milliseconds>0)inference.Add(m.Milliseconds);
                bool strictAccept=m.Accepted&&m.Score>=.90f;
                if(row.label<0){unknown++;if(m.Accepted)unknownFlexible++;if(strictAccept)unknownStrict++;continue;}
                known++;bool correct=(int)m.Glyph==row.label&&m.Status!=RuneRecognitionStatus.TooShort&&m.Status!=RuneRecognitionStatus.Unavailable;
                if(correct)top1++;
                if(m.Accepted&&correct)flexible++;
                if(strictAccept){if(correct)strict++;else wrongStrict++;}
            }
            inference.Sort();
            Debug.Log($"RUNE_HOLDOUT known={known} unknown={unknown} top1={top1} strict_correct={strict} strict_wrong={wrongStrict} strict_unknown={unknownStrict} flexible_correct={flexible} flexible_unknown={unknownFlexible} CPU_median_ms={inference[inference.Count/2]:F1} CPU_p95_ms={inference[(int)Math.Floor((inference.Count-1)*.95)]:F1}");
            Assert.AreEqual(60,known);Assert.AreEqual(5,unknown);Assert.GreaterOrEqual(top1,57);
            Assert.GreaterOrEqual(strict,45);Assert.AreEqual(0,wrongStrict);Assert.AreEqual(0,unknownStrict);
            Assert.GreaterOrEqual(flexible,53);Assert.LessOrEqual(unknownFlexible,1);
        }
    }
}
