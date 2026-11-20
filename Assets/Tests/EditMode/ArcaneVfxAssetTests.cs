using System.Linq;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace LightSouls.MapGeneration.Tests
{
    public class ArcaneVfxAssetTests
    {
        [Test]
        public void SharedShadersAreIncludedAndElementProfilesAreAuthored()
        {
            Assert.AreEqual("LightSouls/VFX/Arcane Glyph", ArcaneVfxMaterials.Glyph.shader.name);
            Assert.AreEqual("LightSouls/VFX/Arcane Blade", ArcaneVfxMaterials.Blade.shader.name);
            Assert.AreEqual("LightSouls/VFX/Arcane Particle", ArcaneVfxMaterials.Particle(ArcaneParticlePattern.Flame).shader.name);
            Assert.AreSame(ArcaneVfxMaterials.Particle(ArcaneParticlePattern.Flame), ArcaneVfxMaterials.Particle(ArcaneParticlePattern.Flame));
            Assert.AreEqual(CombatVfxElement.Ember, Weapon("Weapon_FireSword").Vfx.element);
            Assert.AreEqual(CombatVfxElement.Frost, Weapon("Weapon_IceStaff").Vfx.element);
            Assert.AreEqual(CombatVfxElement.Stone, Weapon("Weapon_Sledgehammer").Vfx.element);
            Assert.AreEqual(CombatVfxElement.Void, Weapon("Weapon_Sword_Greatsword").Vfx.element);
        }

        [Test]
        public void AnimatedGlyphsKeepExactCircleAndLaneBoundariesAndNoCollision()
        {
            var circle = ArcaneTelegraph.Circle("TestCircle", new Vector3(4,0,8),2.35f,Color.red,.9f,CombatVfxElement.Ember);
            var lane = ArcaneTelegraph.Lane("TestLane",Vector3.zero,new Vector3(3,0,4),.85f,Color.red,.75f,CombatVfxElement.Stone);
            try
            {
                Assert.AreEqual(TelegraphShape.Circle,circle.Shape);
                Assert.That(circle.Radius,Is.EqualTo(2.35f).Within(.001f));
                var line=circle.GetComponent<LineRenderer>();
                for(int i=0;i<line.positionCount;i++) Assert.That(line.GetPosition(i).magnitude,Is.EqualTo(circle.Radius).Within(.001f));
                var surface=circle.GetComponentInChildren<MeshRenderer>();
                Assert.That(surface.transform.localScale.x,Is.EqualTo(4.7f).Within(.001f));
                circle.SetProgress(.7f);var block=new MaterialPropertyBlock();surface.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_Progress"),Is.EqualTo(.7f).Within(.001f));
                Assert.IsTrue(circle.IsVisible);circle.SetVisible(false);Assert.IsFalse(circle.IsVisible);
                Assert.AreEqual(5f,lane.Length);Assert.AreEqual(.85f,lane.HalfWidth);
                var points=lane.GetComponent<LineRenderer>();
                Assert.That(Vector3.Distance(points.GetPosition(0),points.GetPosition(1)),Is.EqualTo(1.7f).Within(.001f));
                Assert.That(Vector3.Distance(points.GetPosition(1),points.GetPosition(2)),Is.EqualTo(5f).Within(.001f));
                Assert.IsEmpty(circle.GetComponentsInChildren<Collider>());Assert.IsEmpty(lane.GetComponentsInChildren<Collider>());
                Assert.AreSame(surface.sharedMaterial,lane.GetComponentInChildren<MeshRenderer>().sharedMaterial);
            }
            finally { Object.DestroyImmediate(circle.gameObject);Object.DestroyImmediate(lane.gameObject); }
        }

        [Test]
        public void SlashMeshTapersBothEndsAndUsesBoundedGeometry()
        {
            var mesh=SwordSlashWave.CreateSlashArcMesh(.43f,1f,120f,36);
            try
            {
                Assert.LessOrEqual(mesh.vertexCount,130);
                Assert.AreEqual(mesh.vertexCount,mesh.uv.Length);
                Assert.That(Vector3.Distance(mesh.vertices[0],mesh.vertices[1]),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(mesh.vertices[^2],mesh.vertices[^1]),Is.LessThan(.001f));
                Assert.Greater(Vector3.Distance(mesh.vertices[36],mesh.vertices[37]),.4f);
                Assert.That(mesh.uv.Last().x,Is.EqualTo(1f).Within(.001f));
            }
            finally{Object.DestroyImmediate(mesh);}
        }

        private static WeaponDataSO Weapon(string name)=>AssetDatabase.LoadAssetAtPath<WeaponDataSO>("Assets/ScriptableObjects/Combat/Weapons/"+name+".asset");
    }
}
