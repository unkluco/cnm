using System.Collections.Generic;
using LightSouls.Combat.Bosses;
using LightSouls.Combat.Effects;
using LightSouls.Combat.Enemies;
using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Combat.Weapons
{
    public enum TacticalHitKind { Push, Freeze, Pull, Charm, Chain, Expose }

    [CreateAssetMenu(fileName="HitEffect_Tactical",menuName="LightSouls/Combat/Hit Effects/Tactical")]
    public sealed class TacticalHitEffectSO : WeaponHitEffectSO
    {
        [SerializeField] private TacticalHitKind kind;
        [SerializeField, Min(.1f)] private float magnitude=2f;
        [SerializeField, Min(.1f)] private float duration=2f;
        [SerializeField, Min(.2f)] private float cooldown=3f;
        [SerializeField] private Color accent=Color.cyan;
        public TacticalHitKind Kind=>kind;
        public float Cooldown=>cooldown;
        private string Key=>"combat.hit."+kind;
        public float Remaining=>RunState.CooldownRemaining(Key);

        public override void OnHit(WeaponHitContext context)
        {
            if(context.Attacker==null||!context.Attacker.CompareTag("Player")||context.Target==null||context.Damageable==null||context.Damageable.IsDead||context.DamageDealt<=0||RunState.CooldownRemaining(Key)>0)return;
            var enemy=context.Target.GetComponent<EnemyBase>();
            if(enemy==null||enemy.IsCharmed)return;
            bool applied=false;Vector3 away=enemy.transform.position-context.Attacker.transform.position;
            switch(kind)
            {
                case TacticalHitKind.Push: applied=WeaponTactics.TryDisplace(enemy,away,magnitude);break;
                case TacticalHitKind.Pull: applied=WeaponTactics.TryDisplace(enemy,-away,Mathf.Min(magnitude,Mathf.Max(.6f,away.magnitude-1.3f)));break;
                case TacticalHitKind.Freeze:
                    var status=StatusEffectReceiver.GetOrAdd(context.Target);
                    if(enemy is BossEnemy){status.ApplySlow(.82f,Mathf.Min(1.3f,duration));applied=true;}
                    else if(status.TryStun(duration)){status.ApplySlow(.03f,duration);applied=true;}
                    break;
                case TacticalHitKind.Charm: applied=enemy.TryCharm(context.Attacker,Mathf.Min(8f,duration),Mathf.Clamp(magnitude,.2f,.6f));break;
                case TacticalHitKind.Expose:
                    enemy.GetComponent<HealthComponent>().Expose(Mathf.Clamp(magnitude,1.1f,1.25f),duration);
                    VfxFactory.TimedAura(enemy.transform,"ArmorCrack",accent,duration,14f,.55f);applied=true;break;
                case TacticalHitKind.Chain:
                    var visited=new HashSet<EnemyBase>{enemy};int count=0;
                    foreach(var col in Physics.OverlapSphere(enemy.transform.position,4.0f,1<<7,QueryTriggerInteraction.Ignore))
                    {
                        var other=col.GetComponentInParent<EnemyBase>();if(other==null||other.IsDead||other.IsCharmed||!visited.Add(other))continue;
                        if(Physics.Linecast(enemy.transform.position+Vector3.up*.8f,other.transform.position+Vector3.up*.8f,1<<8,QueryTriggerInteraction.Ignore))continue;
                        Vector3 end=other.transform.position+Vector3.up*.8f;
                        WeaponHitResolver.Apply(null,magnitude,context.Attacker,other,end,false,end-context.HitPoint,CombatVfxElement.Storm,accent);
                        ArcaneTelegraph.Lane("ArcChain",enemy.transform.position,other.transform.position,.12f,accent,.18f,CombatVfxElement.Storm,null,TelegraphStyle.Impact);
                        applied=true;if(++count==2)break;
                    }
                    break;
            }
            if(applied){RunState.StartCooldown(Key,cooldown);VfxFactory.Ring(enemy.transform.position,accent,1.15f,.32f,24);}
        }

        public override string Describe()
        {
            string description=kind switch
            {
                TacticalHitKind.Push=>"Đẩy lùi "+magnitude.ToString("0.#")+"m (boss kháng)",
                TacticalHitKind.Pull=>"Kéo quái tới "+magnitude.ToString("0.#")+"m (boss kháng)",
                TacticalHitKind.Freeze=>"Đóng băng "+duration.ToString("0.#")+"s; boss chỉ chậm",
                TacticalHitKind.Charm=>"Thôi miên 1 quái nhỏ dưới "+Mathf.RoundToInt(magnitude*100)+"% máu trong "+duration.ToString("0.#")+"s (không boss)",
                TacticalHitKind.Chain=>"Sét phân nhánh: "+magnitude.ToString("0")+" sát thương lên tối đa 2 quái khác",
                _=>"Phá phòng thủ: +"+Mathf.RoundToInt((magnitude-1)*100)+"% sát thương nhận trong "+duration.ToString("0.#")+"s"
            };
            return description+" · Hồi "+cooldown.ToString("0.#")+"s";
        }
    }
}
