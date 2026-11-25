using System.Collections.Generic;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Combat.Stats;
using LightSouls.Combat.Weapons;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Runes
{
    /// <summary>One impact budget shared by all strikes/projectiles of a cast.</summary>
    public sealed class RuneImpact
    {
        public readonly RuneSkill Skill;
        private readonly HashSet<IDamageable> affected=new HashSet<IDamageable>();
        private bool chained,charmed;
        private float healed;
        public RuneImpact(RuneSkill skill){Skill=skill;}

        public void OnHit(WeaponHitContext context)
        {
            if(Skill.Effect==RuneEffect.None||context.DamageDealt<=0||context.Attacker==null||context.Target==null||
                !affected.Add(context.Damageable))return;
            float power=Skill.Power,duration=Skill.Duration;
            var enemy=context.Target.GetComponent<EnemyBase>();
            var health=context.Target.GetComponent<HealthComponent>();
            Color color=context.Weapon!=null?context.Weapon.WeaponColor:DarkFantasyTheme.Gold;
            bool applied=false;
            if(Skill.Effect==RuneEffect.Leech)
            {
                var ownerHealth=context.Attacker.GetComponent<HealthComponent>();
                if(ownerHealth!=null&&!ownerHealth.IsDead)
                {
                    float amount=Mathf.Min(context.DamageDealt*Mathf.Clamp(power,.05f,.30f),Mathf.Max(0f,8f-healed));
                    float previous=ownerHealth.CurrentHealth;ownerHealth.Heal(amount);healed+=Mathf.Max(0,ownerHealth.CurrentHealth-previous);
                    if(amount>0)VfxFactory.TimedAura(context.Attacker.transform,"RuneLeech",new Color(.83f,.27f,.38f),.55f,16f,.45f);
                }
                return;
            }
            if(context.Damageable==null||context.Damageable.IsDead||enemy!=null&&enemy.IsCharmed)return;
            Vector3 away=context.Target.transform.position-context.Attacker.transform.position;
            switch(Skill.Effect)
            {
                case RuneEffect.Push: applied=WeaponTactics.TryDisplace(enemy,away,power);break;
                case RuneEffect.Pull: applied=WeaponTactics.TryDisplace(enemy,-away,Mathf.Min(power,Mathf.Max(.6f,away.magnitude-1.3f)));break;
                case RuneEffect.Freeze:
                    var frost=StatusEffectReceiver.GetOrAdd(context.Target);
                    if(enemy is BossEnemy){frost.ApplySlow(.82f,Mathf.Min(1.3f,duration));applied=true;}
                    else if(frost.TryStun(duration)){frost.ApplySlow(.03f,Mathf.Min(duration,1.85f));applied=true;}
                    break;
                case RuneEffect.Slow:
                    StatusEffectReceiver.GetOrAdd(context.Target).ApplySlow(Mathf.Clamp(power,.40f,.85f),Mathf.Clamp(duration,1,4));applied=true;break;
                case RuneEffect.Stun:
                    applied=StatusEffectReceiver.GetOrAdd(context.Target).TryStun(Mathf.Min(duration,1.5f));break;
                case RuneEffect.Burn:
                    StatusEffectReceiver.GetOrAdd(context.Target).ApplyBurn(Mathf.Clamp(context.DamageDealt*power,.3f,11f),Mathf.Min(3.5f,duration),context.Attacker);applied=true;break;
                case RuneEffect.Expose:
                    if(health!=null){health.Expose(Mathf.Clamp(power,1.08f,1.22f),Mathf.Min(duration,4));VfxFactory.TimedAura(context.Target.transform,"RuneArmorCrack",color,duration,14f,.5f);applied=true;}break;
                case RuneEffect.Charm:
                    if(!charmed&&enemy!=null){charmed=enemy.TryCharm(context.Attacker,Mathf.Min(8f,duration),Mathf.Clamp(power,.2f,.55f));applied=charmed;}break;
                case RuneEffect.Chain:
                    if(!chained){chained=true;applied=Chain(context,color,power);}break;
            }
            if(applied)VfxFactory.Ring(context.Target.transform.position,color,1.05f,.32f,20);
        }

        private static bool Chain(WeaponHitContext context,Color color,float power)
        {
            Vector3 origin=context.Target.transform.position;
            var seen=new HashSet<EnemyBase>();int count=0;
            var original=context.Target.GetComponent<EnemyBase>();if(original!=null)seen.Add(original);
            foreach(var col in Physics.OverlapSphere(origin,4.2f,1<<7,QueryTriggerInteraction.Ignore))
            {
                var enemy=col.GetComponentInParent<EnemyBase>();
                if(enemy==null||enemy.IsDead||enemy.IsCharmed||!seen.Add(enemy))continue;
                Vector3 target=enemy.transform.position;
                if(Physics.Linecast(origin+Vector3.up*.8f,target+Vector3.up*.8f,1<<8,QueryTriggerInteraction.Ignore))continue;
                WeaponHitResolver.Apply(null,Mathf.Clamp(context.DamageDealt*power,1f,32f),context.Attacker,enemy,target+Vector3.up*.8f,false,
                    target-origin,CombatVfxElement.Storm,color);
                ArcaneTelegraph.Lane("Rune_Chain",origin,target,.12f,color,.24f,CombatVfxElement.Storm,null,TelegraphStyle.Impact);
                if(++count>=2)break;
            }
            return count>0;
        }
    }

    public static class RuneSkillExecutor
    {
        private const int EnvironmentMask=1<<8,MaximumAreaTargets=6;
        private static readonly object FocusSource=new object();

        public static WeaponCastKind TacticalKind(RuneForm form)=>form switch
        {
            RuneForm.Blink=>WeaponCastKind.Blink,RuneForm.StoneWall=>WeaponCastKind.StoneWall,
            RuneForm.IceWall=>WeaponCastKind.IceWall,RuneForm.Ward=>WeaponCastKind.Ward,_=>WeaponCastKind.None
        };
        public static string FamilyKey(RuneSkill skill)=>skill.Form==RuneForm.Focus?"combat.rune.family.Focus":skill.Effect switch
        {
            RuneEffect.Freeze or RuneEffect.Stun=>"combat.rune.family.HardControl",
            RuneEffect.Push or RuneEffect.Pull=>"combat.rune.family.Displace",
            RuneEffect.Charm=>"combat.rune.family.Charm",
            RuneEffect.Leech=>"combat.rune.family.Leech",
            RuneEffect.Chain=>"combat.rune.family.Chain",
            RuneEffect.Expose=>"combat.rune.family.Expose",_=>null
        };
        public static float FamilyDelay(RuneSkill skill)=>skill.Form==RuneForm.Focus?8f:skill.Effect switch
        {
            RuneEffect.Freeze or RuneEffect.Stun=>4.2f,RuneEffect.Push or RuneEffect.Pull=>3.2f,
            RuneEffect.Charm=>9f,RuneEffect.Leech=>5.5f,RuneEffect.Chain=>3.8f,RuneEffect.Expose=>3f,_=>0f
        };

        public static bool Execute(WeaponBase weapon,Transform attackOrigin,Vector3 direction,Transform target,LayerMask enemyLayer,
            GameObject owner,RuneSkill skill,float cooldown,out string feedback)
        {
            feedback=string.Empty;
            if(weapon==null||weapon.Data==null||owner==null||skill==null||skill.Form==RuneForm.Dash)return false;
            direction=WeaponTactics.Flat(direction);
            var data=weapon.Data;
            var impact=new RuneImpact(skill);
            var kind=TacticalKind(skill.Form);
            if(kind!=WeaponCastKind.None)
            {
                var player=owner.GetComponent<PlayerController>();
                if(player==null||!player.InLockedCombat){feedback="Chỉ dùng trong phòng giao tranh";return false;}
                if(!WeaponTactics.TryCast(kind,owner,direction,skill.Area,skill.Duration,cooldown,data.WeaponColor))
                {feedback="Không có vị trí hoặc chưa hồi kỹ năng";return false;}
                weapon.PlayRuneVisual(attackOrigin,direction,skill);
                if(skill.Form==RuneForm.Blink&&skill.DamageScale>0)
                    AttackArea(data,owner,owner.transform.position,owner.transform.position,direction,1.7f,360f,0,
                        RuneForm.Nova,enemyLayer,skill,impact);
                return true;
            }
            if(skill.Form==RuneForm.Focus)
            {
                var player=owner.GetComponent<PlayerController>();
                if(player==null||!player.InLockedCombat){feedback="Chỉ dùng trong phòng giao tranh";return false;}
                PlayerStats.GetOrAdd(owner).SetModifiers(FocusSource,
                    new[]{new StatModifier(StatType.DamageDealt,1.15f),new StatModifier(StatType.AttackCooldown,.88f)},Mathf.Min(skill.Duration,4.5f));
                VfxFactory.TimedAura(owner.transform,"RuneFocus",data.WeaponColor,Mathf.Min(skill.Duration,4.5f),20f,.52f);
                VfxFactory.Ring(owner.transform.position,data.WeaponColor,1.35f,.5f);weapon.PlayRuneVisual(attackOrigin,direction,skill);
                return true;
            }
            if(skill.Form==RuneForm.Weapon)return weapon.ExecuteRuneAttack(attackOrigin,direction,enemyLayer,owner,skill,impact);
            weapon.PlayRuneVisual(attackOrigin,direction,skill);
            Vector3 origin=owner.transform.position,center=origin;
            float range=Mathf.Clamp(skill.Reach(data),.7f,14f);
            float radius=skill.Form==RuneForm.Sector?range:Mathf.Max(.65f,skill.Area);
            float width=Mathf.Max(.25f,skill.Area);
            if(skill.Form==RuneForm.Lane)
            {
                range=ClipTravel(origin,direction,range);
                radius=range;
                ArcaneTelegraph.Lane("Rune_Lane",origin,origin+direction*range,width,data.WeaponColor,.45f,data.Vfx.element,null,TelegraphStyle.Impact);
                VfxFactory.Ring(origin,data.WeaponColor,.85f,.24f,18);
            }
            else if(skill.Form==RuneForm.TargetBurst)
            {
                float distance=target!=null?Mathf.Min(range,Vector3.Distance(origin,target.position)):Mathf.Min(range,5.5f);
                center=origin+direction*ClipTravel(origin,direction,distance);
                if(target!=null&&!Physics.Linecast(origin+Vector3.up*.85f,target.position+Vector3.up*.85f,EnvironmentMask,QueryTriggerInteraction.Ignore))
                    center=new Vector3(target.position.x,origin.y,target.position.z);
                VfxFactory.SpellImpact(center,radius,data.WeaponColor,data.Vfx.element);
                ArcaneTelegraph.Circle("Rune_TargetBurst",center,radius,data.WeaponColor,.42f,data.Vfx.element,TelegraphStyle.Impact);
            }
            else if(skill.Form==RuneForm.Sector)
            {
                ArcaneTelegraph.Sector("Rune_Sweep",origin,direction,radius,skill.Arc,data.WeaponColor,.4f,data.Vfx.element,null,TelegraphStyle.Impact);
            }
            else
            {
                VfxFactory.SpellImpact(center,radius,data.WeaponColor,data.Vfx.element);
                ArcaneTelegraph.Circle("Rune_Nova",center,radius,data.WeaponColor,.42f,data.Vfx.element,TelegraphStyle.Impact);
            }
            AttackArea(data,owner,origin,center,direction,radius,skill.Arc,width,skill.Form,enemyLayer,skill,impact);
            return true;
        }

        private static float ClipTravel(Vector3 origin,Vector3 direction,float distance)
        {
            if(Physics.Raycast(origin+Vector3.up*.85f,direction,out var hit,distance,EnvironmentMask,QueryTriggerInteraction.Ignore))
                return Mathf.Max(.15f,hit.distance-.15f);
            return distance;
        }

        private static void AttackArea(WeaponDataSO data,GameObject owner,Vector3 origin,Vector3 center,Vector3 direction,
            float radius,float arc,float width,RuneForm form,LayerMask enemyLayer,RuneSkill skill,RuneImpact impact)
        {
            if(skill.DamageScale<=0)return;
            var colliders=Physics.OverlapCapsule(center+Vector3.up*.2f,center+Vector3.up*2f,
                form==RuneForm.Lane?radius+width:radius,enemyLayer,QueryTriggerInteraction.Ignore);
            // Closest targets first so the explicit mobile/damage budget is deterministic.
            System.Array.Sort(colliders,(a,b)=>((a.bounds.center-center).sqrMagnitude).CompareTo((b.bounds.center-center).sqrMagnitude));
            var hit=new HashSet<IDamageable>();int count=0;
            foreach(var collider in colliders)
            {
                var target=WeaponHitResolver.FindDamageable(collider);
                if(target==null||target.IsDead||WeaponHitResolver.IsFriendly(owner,target)||hit.Contains(target))continue;
                var component=target as Component;
                if(component==null||component.transform.root==owner.transform.root)continue;
                Vector3 point=collider.bounds.center,offset=point-center;offset.y=0;
                if(form==RuneForm.Lane)
                {
                    float along=Vector3.Dot(offset,direction);
                    if(along<0||along>radius|| (offset-direction*along).magnitude>width+.18f)continue;
                }
                else if(offset.magnitude>radius+.12f||(form==RuneForm.Sector&&Vector3.Angle(direction,offset)>arc*.5f))continue;
                if(Physics.Linecast(origin+Vector3.up*.8f,point,EnvironmentMask,QueryTriggerInteraction.Ignore)||
                    (form==RuneForm.TargetBurst&&Physics.Linecast(center+Vector3.up*.65f,point,EnvironmentMask,QueryTriggerInteraction.Ignore)))continue;
                hit.Add(target);
                float damage=data.Damage*skill.DamageScale;
                // Area damage isn't allowed to inherit every old passive proc.
                WeaponHitResolver.Apply(data,damage,owner,target,collider.ClosestPoint(center+Vector3.up*.8f),runeImpact:impact);
                if(++count>=MaximumAreaTargets)break;
            }
        }
    }
}
