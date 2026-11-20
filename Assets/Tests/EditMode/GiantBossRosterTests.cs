using System.Linq;
using LightSouls.Combat;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Common;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace LightSouls.MapGeneration.Tests
{
    public class GiantBossRosterTests
    {
        [Test]
        public void FiveAuthoredGiantsHaveFiveUniqueMovesAndRealHitColliders()
        {
            var prefabs=Resources.LoadAll<GameObject>("Combat/Bosses");
            Assert.AreEqual(5,prefabs.Length);
            Assert.AreEqual(5,prefabs.Select(p=>p.GetComponent<BossEnemy>().Kind).Distinct().Count());
            foreach(var prefab in prefabs)
            {
                var boss=prefab.GetComponent<BossEnemy>();Assert.NotNull(boss);
                var def=boss.Definition;
                Assert.AreEqual(5,def.Skills.Count);Assert.AreEqual(5,def.Skills.Select(s=>s.Name).Distinct().Count());
                Assert.AreEqual(def.Health,prefab.GetComponent<HealthComponent>().MaxHealth);
                var col=prefab.GetComponent<CapsuleCollider>();Assert.Greater(col.height,4f);Assert.Greater(col.radius,.85f);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length,Is.EqualTo(1),"Art may not add hidden blockers.");
                Assert.Greater(prefab.GetComponentsInChildren<MeshRenderer>(true).Length,25);
                Assert.NotNull(prefab.transform.Find("Art_BossSilhouette"));
                Assert.IsTrue(def.Skills.All(s=>s.CastTime(2)>=.78f && s.RestTime(2)>=.85f && s.StageCount(2)>=s.StageCount(1)));
            }
            var dragon=prefabs.Single(p=>p.GetComponent<BossEnemy>().Kind==BossKind.Aurex);
            Assert.NotNull(dragon.transform.Find("Art_BossSilhouette/Wing_L/FacetedMembrane"));
            Assert.NotNull(dragon.transform.Find("Art_BossSilhouette/Wing_R/FacetedMembrane"));
        }

        [Test]
        public void RingSectorAndLaneHaveExactSafeSpace()
        {
            var ring=BossZone.Ring(Vector3.zero,2,7);
            Assert.IsFalse(ring.Contains(Vector3.zero));Assert.IsTrue(ring.Contains(Vector3.right*4));Assert.IsFalse(ring.Contains(Vector3.right*8));
            var sector=BossZone.Sector(Vector3.zero,Vector3.forward,8,90);
            Assert.IsTrue(sector.Contains(Vector3.forward*6));Assert.IsFalse(sector.Contains(Vector3.back*4));Assert.IsFalse(sector.Contains(Vector3.right*3));
            var lane=BossZone.Lane(Vector3.zero,Vector3.forward*11,1.3f);
            Assert.IsTrue(lane.Contains(new Vector3(1.2f,1,6)));Assert.IsFalse(lane.Contains(new Vector3(1.4f,0,6)));
            Assert.IsFalse(lane.Contains(Vector3.forward*12));Assert.IsFalse(lane.Contains(Vector3.back));
        }

        [Test]
        public void AllTwentyFiveSkillsBuildBoundedWarnedStagesInBothPhases()
        {
            var arena=new BossArena(new Bounds(new Vector3(36,2,-12),new Vector3(28,4,28)),0);
            Vector3 origin=arena.Center;
            foreach(var boss in BossRoster.All)
                for(int skill=0;skill<boss.Skills.Count;skill++)
                    for(int phase=1;phase<=2;phase++)
                        for(int stage=0;stage<boss.Skills[skill].StageCount(phase);stage++)
                            foreach(var target in new[]{origin+Vector3.forward*4.7f,arena.Center+new Vector3(13,0,-13),origin+Vector3.right*9})
                            {
                                var plan=BossPatterns.Build(boss.Kind,skill,stage,phase,origin,target,arena);
                                Assert.That(plan.Zones.Count,Is.InRange(1,7),boss.Kind+" / "+skill);
                                foreach(var zone in plan.Zones)
                                {
                                    Assert.IsTrue(arena.Contains(zone.Origin));
                                    if(zone.Shape==TelegraphShape.Lane){Assert.Greater(zone.Length,0f);Assert.IsTrue(arena.Contains(zone.End));Assert.Greater(zone.HalfWidth,0);}
                                    else Assert.Greater(zone.Radius,0);
                                }
                                if(plan.Motion!=BossMotion.None)Assert.IsTrue(arena.Contains(plan.Destination));
                            }
        }

        [Test]
        public void AdvancedTelegraphsRenderExactBoundariesWithoutColliders()
        {
            var ring=BossZone.Ring(Vector3.zero,3,8).Draw("RingTest",Color.red,1.2f,CombatVfxElement.Ember,null);
            var sector=BossZone.Sector(Vector3.one,Vector3.right,6.2f,112).Draw("SectorTest",Color.cyan,1.1f,CombatVfxElement.Frost,null);
            try
            {
                Assert.AreEqual(TelegraphShape.Annulus,ring.Shape);Assert.AreEqual(3,ring.InnerRadius);Assert.AreEqual(8,ring.Radius);
                Assert.AreEqual(2,ring.GetComponentsInChildren<LineRenderer>().Length,"Both ring edges need an explicit outline.");
                Assert.AreEqual(TelegraphShape.Sector,sector.Shape);Assert.AreEqual(112,sector.SectorAngle);
                Assert.That(Vector3.Angle(sector.transform.forward,Vector3.right),Is.LessThan(.1f));
                Assert.AreEqual(0,ring.GetComponentsInChildren<Collider>().Length+sector.GetComponentsInChildren<Collider>().Length);
                ring.ClipToArena(new Bounds(Vector3.zero,new Vector3(10f,5f,10f)));
                Assert.IsFalse(ring.GetComponent<LineRenderer>().enabled,"A warning outline must not continue through room walls.");
                Assert.IsTrue(ring.transform.Find("SafeInnerEdge").GetComponent<LineRenderer>().enabled);
            }
            finally{Object.DestroyImmediate(ring.gameObject);Object.DestroyImmediate(sector.gameObject);}
        }

        [Test]
        public void BossRotationVisitsAllFiveAndNeverImmediatelyRepeats()
        {
            var old=Random.state;RunState.Reset();
            try
            {
                int[] first=Enumerable.Range(0,5).Select(_=>RunState.TakeNextBossIndex(5)).ToArray();
                int[] second=Enumerable.Range(0,5).Select(_=>RunState.TakeNextBossIndex(5)).ToArray();
                Assert.AreEqual(5,first.Distinct().Count());Assert.AreEqual(5,second.Distinct().Count());Assert.AreNotEqual(first[4],second[0]);
            }
            finally{RunState.Reset();Random.state=old;}
        }
    }
}
