using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Items
{
    [CreateAssetMenu(fileName="Effect_Cleanse",menuName="LightSouls/Combat/Consumable Effects/Cleanse")]
    public sealed class CleanseEffectSO : ConsumableEffectSO
    {
        public override bool CanApply(ConsumableUseContext context,out string reason)
        {
            bool afflicted=context.User!=null&&context.User.GetComponent<StatusEffectReceiver>()?.HasNegativeEffects==true;
            reason=afflicted?null:"Không có hiệu ứng xấu để thanh tẩy";return afflicted;
        }
        public override void Apply(ConsumableUseContext context)
        {
            context.User.GetComponent<StatusEffectReceiver>()?.Cleanse();
            VfxFactory.SpellImpact(context.User.transform.position,1.8f,new Color(.48f,1f,.8f),CombatVfxElement.Life);
        }
        public override string Describe()=>"Thanh tẩy bỏng và làm chậm đang có hiệu lực";
    }
}
