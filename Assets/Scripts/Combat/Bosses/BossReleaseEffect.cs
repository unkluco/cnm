using LightSouls.Combat.Effects;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Bosses
{
    /// <summary>Brief elemental release, independent of damage. All art is shared and self-cleaning.</summary>
    public sealed class BossReleaseEffect : MonoBehaviour
    {
        private ArcaneTelegraph marker;
        private LineRenderer stroke;
        private Color color;
        private float lifetime, width, age;

        public static void Play(BossZone zone, BossDefinition boss, int ordinal, BossArena arena)
        {
            Color tint = boss.Accent;
            var flash = zone.Draw("VFX_BossRelease", tint, 0, boss.Element, null, TelegraphStyle.Impact);
            flash.ClipToArena(arena.Bounds);
            flash.SetProgress(.95f); flash.SetOpacity(.88f);
            var fade = flash.gameObject.AddComponent<BossReleaseEffect>(); fade.marker = flash; fade.lifetime = .48f;

            switch (zone.Shape)
            {
                case TelegraphShape.Circle:
                    // SpellImpact already balances secondary particles. Clustered attacks never spawn extra lights.
                    VfxFactory.SpellImpact(zone.Origin,zone.Radius,tint,boss.Element);
                    break;
                case TelegraphShape.Lane:
                    Stroke(zone.Origin+Vector3.up*.35f, zone.End+Vector3.up*.35f, tint,
                        Mathf.Clamp(zone.HalfWidth*.68f,.26f,1f), boss.Element == CombatVfxElement.Storm);
                    if (ordinal < 3 && CombatVfxBudget.AllowSecondaryDetail)
                        VfxFactory.SpellImpact(Vector3.Lerp(zone.Origin,zone.End,.67f),Mathf.Min(1.2f,zone.HalfWidth),tint,boss.Element);
                    break;
                case TelegraphShape.Sector:
                    if (boss.Element == CombatVfxElement.Storm)
                    {
                        for (int i=-1;i<=1;i++)
                        {
                            Vector3 d=Quaternion.Euler(0,i*zone.Angle*.29f,0)*zone.Forward;
                            Stroke(zone.Origin+Vector3.up*1.15f,zone.Origin+d*zone.Radius*.93f+Vector3.up*.28f,tint,.64f,true);
                        }
                    }
                    else SwordSlashWave.Spawn(zone.Origin+Vector3.up*1.2f,zone.Forward,zone.Radius*.88f,zone.Angle,tint,boss.Element);
                    for(int i=-1;i<=1;i++)
                    {
                        Vector3 d=Quaternion.Euler(0,i*zone.Angle*.26f,0)*zone.Forward;
                        VfxFactory.Burst(zone.Origin+d*zone.Radius*.62f+Vector3.up*.3f,tint,7,3.7f,.28f,.45f,.23f);
                    }
                    break;
                case TelegraphShape.Annulus:
                    // Keep the safe middle empty, including at the moment of release.
                    float r=(zone.Radius+zone.InnerRadius)*.5f;
                    for(int i=0;i<6 && CombatVfxBudget.AllowSecondaryDetail;i++)
                    {
                        Vector3 p=zone.Origin+Quaternion.Euler(0,i*60f,0)*Vector3.forward*r;
                        if(arena.Contains(p))VfxFactory.Burst(p+Vector3.up*.4f,tint,7,3.6f,.25f,.48f,.15f);
                    }
                    break;
            }
        }

        private static void Stroke(Vector3 start, Vector3 end, Color tint, float width, bool lightning)
        {
            var go=new GameObject("VFX_BossEnergyStroke");
            var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=ArcaneVfxMaterials.Ribbon;
            line.shadowCastingMode=ShadowCastingMode.Off; line.receiveShadows=false;
            line.textureMode=LineTextureMode.Stretch; line.alignment=LineAlignment.View;
            line.positionCount=lightning?13:5; line.numCapVertices=2; line.numCornerVertices=1;
            Vector3 side=Vector3.Cross(Vector3.up,(end-start).normalized);
            for(int i=0;i<line.positionCount;i++)
            {
                float t=i/(float)(line.positionCount-1);
                Vector3 p=Vector3.Lerp(start,end,t);
                if(i>0 && i<line.positionCount-1)
                {
                    float wave=lightning?Mathf.Sin(i*8.71f)*Mathf.Min(width*.75f,.48f):Mathf.Sin(t*Mathf.PI)*width*.17f;
                    p+=side*wave+Vector3.up*Mathf.Sin(t*Mathf.PI)*.25f;
                }
                line.SetPosition(i,p);
            }
            var effect=go.AddComponent<BossReleaseEffect>(); effect.stroke=line; effect.color=tint; effect.width=width; effect.lifetime=.34f;
            effect.RenderStroke(1f);
        }

        private void RenderStroke(float a)
        {
            stroke.widthMultiplier=width*Mathf.Lerp(.23f,1f,a);
            var bright=Color.Lerp(color,Color.white,.48f); bright.a=a;
            var tail=color;tail.a=a*.64f;
            stroke.startColor=bright;stroke.endColor=tail;
        }

        private void Update()
        {
            age+=Time.deltaTime;
            float a=1f-Mathf.Clamp01(age/Mathf.Max(.05f,lifetime));
            if(marker!=null){marker.SetOpacity(a*.88f);marker.SetProgress(1f-a*.4f);}
            if(stroke!=null)RenderStroke(a);
            if(age>=lifetime)Destroy(gameObject);
        }
    }
}
