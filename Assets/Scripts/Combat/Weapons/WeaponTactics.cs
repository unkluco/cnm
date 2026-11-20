using System.Collections.Generic;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Stats;
using LightSouls.Common;
using UnityEngine;
using UnityEngine.AI;

namespace LightSouls.Combat.Weapons
{
    public enum WeaponCastKind { None, Blink, StoneWall, IceWall, Ward }

    /// <summary>Terrain and displacement are real physics/NavMesh operations, never just VFX.</summary>
    public static class WeaponTactics
    {
        private static readonly int EnvironmentMask = 1 << 8;
        private static readonly int ActorMask = (1 << 6) | (1 << 7);
        private static readonly object WardSource = new object();

        internal static Vector3 Flat(Vector3 v) { v.y=0; return v.sqrMagnitude>.0001f?v.normalized:Vector3.forward; }
        private static string Family(WeaponCastKind kind) => kind==WeaponCastKind.StoneWall||kind==WeaponCastKind.IceWall?"Wall":kind.ToString();
        private static string Key(WeaponCastKind kind) => "combat.cast."+Family(kind);
        public static float CastCooldownRemaining(WeaponDataSO weapon) => weapon==null||weapon.TacticalCast==WeaponCastKind.None ? 0f :
            Mathf.Max(RunState.CooldownRemaining(Key(weapon.TacticalCast)),RunState.CooldownRemaining("combat.cast.global"));

        public static string Describe(WeaponDataSO weapon)
        {
            string action=weapon.TacticalCast switch
            {
                WeaponCastKind.Blink=>"Bước Ảnh "+weapon.TacticalDistance.ToString("0.#")+"m (không xuyên tường/cửa)",
                WeaponCastKind.StoneWall=>"Dựng vách đá trong "+weapon.TacticalDuration.ToString("0.#")+"s",
                WeaponCastKind.IceWall=>"Dựng vách băng trong "+weapon.TacticalDuration.ToString("0.#")+"s",
                WeaponCastKind.Ward=>"Lá chắn -28% sát thương trong "+weapon.TacticalDuration.ToString("0.#")+"s",
                _=>string.Empty
            };
            return action+" · Hồi "+weapon.TacticalCooldown.ToString("0.#")+"s";
        }

        public static bool TryCast(WeaponDataSO weapon, GameObject owner, Vector3 aim)
        {
            if(weapon==null||owner==null||!owner.CompareTag("Player")||weapon.TacticalCast==WeaponCastKind.None||CastCooldownRemaining(weapon)>0) return false;
            var player=owner.GetComponent<PlayerController>();
            if(player==null||player.IsDead||!player.InLockedCombat||player.Movement.IsDashing)return false;
            Vector3 p=owner.transform.position,d=Flat(aim); bool success=false;
            switch(weapon.TacticalCast)
            {
                case WeaponCastKind.Blink:
                    if(TryFindTravel(p,d,Mathf.Min(4.5f,weapon.TacticalDistance),.46f,true,out var destination))
                    {
                        VfxFactory.SpellImpact(p,1.1f,weapon.WeaponColor,CombatVfxElement.Void);
                        player.Teleport(destination+Vector3.up*.035f);
                        VfxFactory.SpellImpact(destination,1.25f,weapon.WeaponColor,CombatVfxElement.Void);success=true;
                    }
                    break;
                case WeaponCastKind.StoneWall: case WeaponCastKind.IceWall:
                    foreach(float angle in new[]{55f,-55f,0f})
                    {
                        success=TemporaryRuneWall.TryCreate(owner,p,Quaternion.Euler(0,angle,0)*d,Mathf.Clamp(weapon.TacticalDistance,1.8f,3.3f),
                            Mathf.Clamp(weapon.TacticalDuration,2f,6f),weapon.TacticalCast==WeaponCastKind.IceWall,weapon.WeaponColor);
                        if(success)break;
                    }
                    break;
                case WeaponCastKind.Ward:
                    var stats=PlayerStats.GetOrAdd(owner);
                    stats.SetModifiers(WardSource,new[]{new StatModifier(StatType.DamageTaken,.72f)},Mathf.Min(4f,weapon.TacticalDuration));
                    VfxFactory.TimedAura(owner.transform,"ArsenalWard",weapon.WeaponColor,weapon.TacticalDuration,22f,.54f);
                    VfxFactory.Ring(p,weapon.WeaponColor,1.45f,.45f);success=true;break;
            }
            if(success){RunState.StartCooldown(Key(weapon.TacticalCast),weapon.TacticalCooldown);RunState.StartCooldown("combat.cast.global",1.1f);}
            return success;
        }

        public static bool TryFindTravel(Vector3 origin,Vector3 direction,float distance,float radius,bool avoidHazards,out Vector3 destination)
        {
            destination=origin;direction=Flat(direction);
            if(!NavMesh.SamplePosition(origin,out var start,.9f,NavMesh.AllAreas))return false;
            // The live physics line sees locked gates and new temporary terrain even before carving updates.
            if(Physics.SphereCast(origin+Vector3.up*.86f,radius,direction,out var hit,distance,EnvironmentMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0,hit.distance-.22f);
            for(int i=0;i<7 && distance>.65f;i++,distance-=.5f)
            {
                Vector3 candidate=start.position+direction*distance;
                if(!NavMesh.SamplePosition(candidate,out var sample,.65f,NavMesh.AllAreas))continue;
                if(NavMesh.Raycast(start.position,sample.position,out _,NavMesh.AllAreas))continue;
                Vector3 offset=sample.position-origin;offset.y=0;if(offset.sqrMagnitude<.42f||Vector3.Dot(offset,direction)<.55f)continue;
                if(Physics.CheckCapsule(sample.position+Vector3.up*(radius+.10f),sample.position+Vector3.up*1.3f,radius,EnvironmentMask|ActorMask,QueryTriggerInteraction.Ignore))continue;
                if(avoidHazards&&Physics.CheckSphere(sample.position+Vector3.up*.36f,radius,1<<11,QueryTriggerInteraction.Collide))continue;
                destination=sample.position;return true;
            }
            return false;
        }

        public static bool TryDisplace(EnemyBase enemy,Vector3 direction,float distance)
        {
            if(enemy==null||enemy.IsDead||enemy.IsCharmed||enemy is BossEnemy)return false;
            var agent=enemy.GetComponent<NavMeshAgent>();
            if(agent==null||!agent.isOnNavMesh)return false;
            if(!TryFindTravel(enemy.transform.position,direction,Mathf.Clamp(distance,.6f,3.8f),agent.radius,false,out var p))return false;
            // Warp to the constrained endpoint and explicitly interrupt a pending windup.
            if(!agent.Warp(p))return false;
            agent.ResetPath();enemy.TryStun(.22f,out _);
            return true;
        }
    }
}
