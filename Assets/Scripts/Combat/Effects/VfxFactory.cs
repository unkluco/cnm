using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Effects
{
    /// <summary>Dark-fantasy combat effects composed from shared, compact procedural shaders and bounded particle systems.</summary>
    public static class VfxFactory
    {
        public const string MaterialResourcePath = "VFX/Mat_VFX_Additive";
        public static Material Material => ArcaneVfxMaterials.Particle(ArcaneParticlePattern.Glow);
        private static readonly Color Ember = new Color(1f, .42f, .075f, 1f);
        private static readonly Color Smoke = new Color(.26f, .22f, .26f, .63f);
        private const string TrailName = "VFX_ProjectileTrail";

        public static ParticleSystem Burst(Vector3 position, Color color, int count = 16, float speed = 4f,
            float size = .18f, float lifetime = .45f, float gravity = .6f)
        {
            return BurstPattern("VFX_Burst", position, color, count, speed, size, lifetime, gravity,
                gravity < -.1f ? ArcaneParticlePattern.Glow : ArcaneParticlePattern.Spark, stretch: speed > 2f && gravity >= 0f);
        }

        /// <summary>Short hit stop-like visual accent without pausing simulation: flash, directed splinters and a fading core.</summary>
        public static void HitImpact(Vector3 point, Vector3 direction, Color color, CombatVfxElement element, float strength = .5f)
        {
            if (color.a <= 0f) return;
            strength = Mathf.Clamp01(strength);
            var pattern = element == CombatVfxElement.Frost ? ArcaneParticlePattern.Crystal : ArcaneParticlePattern.Spark;
            if (CombatVfxBudget.AllowSecondaryDetail)
                Flash(point, Color.Lerp(color, Color.white, .22f), Mathf.Lerp(.36f, .66f, strength));
            BurstPattern("VFX_ImpactSparks", point, color, Mathf.RoundToInt(Mathf.Lerp(9, 19, strength)),
                Mathf.Lerp(3.4f, 5.7f, strength), Mathf.Lerp(.12f, .23f, strength), .32f, .65f, pattern,
                direction == Vector3.zero ? Vector3.up : (direction.normalized + Vector3.up * .45f).normalized, pattern == ArcaneParticlePattern.Spark);
            if (element == CombatVfxElement.Ember && CombatVfxBudget.AllowSecondaryDetail)
                BurstPattern("VFX_ImpactEmbers", point, Ember, 5, 1.4f, .30f, .38f, -.5f, ArcaneParticlePattern.Flame, Vector3.up);
        }

        public static void Muzzle(Vector3 point, Vector3 direction, Color color, CombatVfxElement element, bool heavy = false)
        {
            if (color.a <= 0f) return;
            Flash(point, color, heavy ? .71f : .43f);
            BurstPattern("VFX_MuzzleSparks", point, color, heavy ? 9 : 5, heavy ? 5f : 3.5f, .12f, .22f, 0f,
                element == CombatVfxElement.Frost ? ArcaneParticlePattern.Crystal : ArcaneParticlePattern.Spark, direction, true);
        }

        private static void Flash(Vector3 point, Color color, float size)
        {
            var ps = BurstPattern("VFX_ImpactFlash", point, color, 1, 0f, size, .14f, 0f, ArcaneParticlePattern.Flash);
            var shape = ps.shape; shape.enabled = false;
        }

        public static ParticleSystem Ring(Vector3 position, Color color, float radius = 2.5f, float lifetime = .4f, int count = 48,
            CombatVfxElement element = CombatVfxElement.Steel)
        {
            ArcaneGroundPulse.Spawn(position, radius, color, lifetime, element);
            // The smooth ring is a shader surface; only a handful of sparks ride its edge.
            var ps = Create("VFX_ShockwaveSparks", null, position + Vector3.up * .095f, ArcaneParticlePattern.Spark, true);
            ConfigureBurst(ps, color, Mathf.Clamp(count / 3, 4, 18), radius / Mathf.Max(.08f, lifetime), .16f, lifetime, 0f);
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .08f; shape.radiusThickness = 0f; shape.rotation = new Vector3(90f, 0f, 0f);
            ps.Play(); return ps;
        }

        /// <summary>Distinct elemental releases for the existing enemy patterns and heavy weapons.</summary>
        public static void SpellImpact(Vector3 ground, float radius, Color color, CombatVfxElement element)
        {
            radius = Mathf.Max(.35f, radius);
            ArcaneGroundPulse.Spawn(ground, radius, color, .47f, element);
            Vector3 point = ground + Vector3.up * .22f;
            switch (element)
            {
                case CombatVfxElement.Ember:
                    FlameArea(ground, radius, color);
                    BurstPattern("VFX_ScatteredEmbers", point, Ember, 18, 4.8f, .18f, .53f, .23f, ArcaneParticlePattern.Spark, Vector3.up, true);
                    if (CombatVfxBudget.AllowSecondaryDetail) SmokeBurst(point + Vector3.up * .25f, radius);
                    break;
                case CombatVfxElement.Frost:
                    BurstPattern("VFX_IceShards", point, color, 22, 4.2f, .37f, .57f, .35f, ArcaneParticlePattern.Crystal, Vector3.up);
                    if (CombatVfxBudget.AllowSecondaryDetail)
                        BurstPattern("VFX_FrostMist", point, new Color(.50f, .81f, 1f, .55f), 9, 1.8f, .55f, .54f, -.05f, ArcaneParticlePattern.Glow);
                    break;
                case CombatVfxElement.Stone:
                    BurstPattern("VFX_StoneImpact", point, color, 14, 5f, .22f, .43f, .7f, ArcaneParticlePattern.Spark, Vector3.up, true);
                    if (CombatVfxBudget.AllowSecondaryDetail) GroundDebris(point, radius);
                    break;
                case CombatVfxElement.Life:
                    BurstPattern("VFX_Blessing", point, color, 20, 2.2f, .25f, .78f, -.64f, ArcaneParticlePattern.Crystal, Vector3.up);
                    break;
                case CombatVfxElement.Void:
                case CombatVfxElement.Blood:
                    BurstPattern("VFX_SpectralRelease", point, color, 18, 3.1f, .23f, .48f, -.32f, ArcaneParticlePattern.Crystal, Vector3.up);
                    break;
                default:
                    BurstPattern("VFX_MetalRelease", point, color, 16, 4.7f, .19f, .38f, .6f, ArcaneParticlePattern.Spark, Vector3.up, true);
                    break;
            }
            if (CombatVfxBudget.AllowSecondaryDetail) Flash(point + Vector3.up * .18f, color, Mathf.Clamp(radius * .2f, .42f, .95f));
        }

        public static void Explosion(Vector3 ground, float radius, Color color)
        {
            SpellImpact(ground, radius, color, CombatVfxElement.Ember);
            if (CombatVfxBudget.AllowSecondaryDetail)
                Flash(ground + Vector3.up * .65f, new Color(1f, .82f, .38f, 1f), Mathf.Clamp(radius * .65f, .8f, 2.1f));
        }

        private static void FlameArea(Vector3 ground, float radius, Color color)
        {
            var ps = Create("VFX_FlameEruption", null, ground + Vector3.up * .12f, ArcaneParticlePattern.Flame, true);
            ConfigureBurst(ps, color, Mathf.Clamp(Mathf.RoundToInt(radius * 5f), 9, 20), 1.1f, Mathf.Clamp(radius * .35f, .49f, 1.05f), .65f, -.4f);
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f; shape.radius = radius * .58f; shape.rotation = new Vector3(-90f, 0f, 0f);
            var main = ps.main; main.startRotation = new ParticleSystem.MinMaxCurve(-.17f, .17f);
            var size = ps.sizeOverLifetime; size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, .45f), new Keyframe(.18f, 1f), new Keyframe(1f, .08f)));
            ps.Play();
        }

        private static void SmokeBurst(Vector3 point, float radius)
        {
            var smoke = Create("VFX_EmberSmoke", null, point, ArcaneParticlePattern.Smoke, true);
            ConfigureBurst(smoke, Smoke, 7, 1.1f, Mathf.Clamp(radius * .25f, .4f, .78f), .9f, -.30f);
            var shape = smoke.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius * .25f;
            var size = smoke.sizeOverLifetime; size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .6f, 1f, 1.55f));
            smoke.Play();
        }

        private static void GroundDebris(Vector3 point, float radius)
        {
            var debris = Create("VFX_StoneChips", null, point, ArcaneParticlePattern.Solid, true);
            ConfigureBurst(debris, new Color(.47f, .43f, .43f, 1f), 9, Mathf.Clamp(radius * 1.7f, 2f, 4f), .24f, .58f, 1.1f);
            var renderer = debris.GetComponent<ParticleSystemRenderer>();
            var chunk = Resources.Load<Mesh>("Art/DarkFantasy/Meshes/BeveledBox");
            if (chunk != null) { renderer.renderMode = ParticleSystemRenderMode.Mesh; renderer.mesh = chunk; }
            var main = debris.main; main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f); main.startRotationY = main.startRotationX; main.startRotationZ = main.startRotationX;
            var shape = debris.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = radius * .17f;
            debris.Play();
            if (CombatVfxBudget.AllowSecondaryDetail)
                BurstPattern("VFX_StoneDust", point, new Color(.48f, .41f, .36f, .58f), 7, 1.9f, .37f, .62f, -.12f, ArcaneParticlePattern.Smoke);
        }

        /// <summary>Continuous status choreography. Reapplying a status reuses this root and its child layers.</summary>
        public static ParticleSystem BurningAura(Transform parent, float radius = .46f)
        {
            var flame = Persistent("VFX_Burn", parent, Vector3.up * .22f, Ember, 24f, .43f, .62f, .6f, .95f,
                ArcaneParticlePattern.Flame, 27, radius, ParticleSystemSimulationSpace.Local);
            UpwardCone(flame, radius * .95f, 12f);
            var main = flame.main; main.startRotation = new ParticleSystem.MinMaxCurve(-.13f, .13f);
            main.gravityModifier = -.34f; flame.Play();
            var embers = Persistent("VFX_Burn_Embers", flame.transform, Vector3.up * .31f, new Color(1f,.65f,.18f,1f), 10f, .10f, .76f, .75f, 1.45f,
                ArcaneParticlePattern.Spark, 12, radius * .7f, ParticleSystemSimulationSpace.World);
            UpwardCone(embers, radius * .7f, 21f); SetStretch(embers); embers.Play();
            if (CombatVfxBudget.AllowSecondaryDetail)
            {
                var smoke = Persistent("VFX_Burn_Smoke", flame.transform, Vector3.up * .6f, Smoke, 5f, .35f, .84f, .18f, .38f,
                    ArcaneParticlePattern.Smoke, 8, radius*.38f, ParticleSystemSimulationSpace.World);
                UpwardCone(smoke, radius*.38f, 14f);
                var scale = smoke.sizeOverLifetime; scale.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f,.6f,1f,1.6f)); smoke.Play();
            }
            return flame;
        }

        public static ParticleSystem FrostAura(Transform parent, Color color)
        {
            var ps = Persistent("VFX_Slow", parent, Vector3.up * .17f, color, 16f, .24f, .84f, .15f, .46f,
                ArcaneParticlePattern.Crystal, 20, .5f, ParticleSystemSimulationSpace.Local);
            UpwardCone(ps, .47f, 10f); ps.Play(); return ps;
        }

        public static ParticleSystem StunAura(Transform parent, Color color)
        {
            var ps = Persistent("VFX_Stun", parent, Vector3.up * 2f, color, 13f, .22f, .50f, .08f, .18f,
                ArcaneParticlePattern.Flash, 12, .32f, ParticleSystemSimulationSpace.Local);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.rotation = new Vector3(90f,0f,0f);
            ps.Play(); return ps;
        }

        public static ParticleSystem WeaponEnchant(Transform parent, Color color, float rate, CombatVfxElement element, bool melee)
        {
            ArcaneParticlePattern pattern = element == CombatVfxElement.Ember ? ArcaneParticlePattern.Flame :
                element == CombatVfxElement.Frost ? ArcaneParticlePattern.Crystal : ArcaneParticlePattern.Glow;
            var ps = Persistent("VFX_WeaponAura", parent, melee ? Vector3.up * .47f : Vector3.zero, color, rate,
                element == CombatVfxElement.Ember ? .24f : .18f, .48f, .06f, .33f, pattern, 24, .21f, ParticleSystemSimulationSpace.Local);
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = melee ? new Vector3(.20f, .94f, .19f) : new Vector3(.22f,.35f,.53f);
            if (element == CombatVfxElement.Ember) { var main = ps.main; main.gravityModifier = -.25f; }
            ps.Play(); return ps;
        }

        public static ParticleSystem Aura(Transform parent, Color color, float rate = 14f, float radius = .35f,
            Vector3 localOffset = default, float size = .14f, float riseSpeed = .8f, string name = "VFX_Aura")
        {
            var ps = Persistent(name, parent, localOffset, color, rate, size, .85f, .05f, .3f,
                ArcaneParticlePattern.Glow, 32, radius, ParticleSystemSimulationSpace.World);
            var main = ps.main; main.gravityModifier = -riseSpeed * .3f;
            ps.Play(); return ps;
        }

        public static ParticleSystem Beam(Transform parent, Color color, float height = 1.6f, float rate = 18f)
        {
            var ps = Persistent("VFX_Beam", parent, Vector3.zero, color, rate, .16f, 1.1f, height / 1.1f, height / 1.1f,
                ArcaneParticlePattern.Glow, 30, .28f, ParticleSystemSimulationSpace.World);
            UpwardCone(ps, .28f, 0f); ps.Play(); return ps;
        }

        public static void TimedAura(Transform parent, string key, Color color, float duration, float rate = 18f, float radius = .5f)
        {
            if (parent == null || duration <= 0f) return;
            string name = "VFX_Timed_" + key;
            Transform existing = parent.Find(name);
            TimedDestroy timer = existing != null ? existing.GetComponent<TimedDestroy>() :
                Aura(parent, color, rate, radius, Vector3.up, .16f, 1f, name).gameObject.AddComponent<TimedDestroy>();
            if (timer != null) timer.ExtendTo(Time.time + duration);
        }

        public static void RemoveTimedAura(Transform parent, string key)
        {
            if (parent == null) return;
            Transform existing = parent.Find("VFX_Timed_" + key);
            if (existing != null) Object.Destroy(existing.gameObject);
        }

        public static TrailRenderer AttachTrail(GameObject target, Color color, float width = .18f, float time = .22f)
        {
            if (target == null || color.a <= 0f) return null;
            var child = target.transform.Find(TrailName);
            if (child == null) { child = new GameObject(TrailName).transform; child.SetParent(target.transform, false); }
            var trail = child.GetComponent<TrailRenderer>() ?? child.gameObject.AddComponent<TrailRenderer>();
            trail.sharedMaterial = ArcaneVfxMaterials.Ribbon;
            trail.time = time; trail.minVertexDistance = .055f; trail.widthMultiplier = width;
            trail.widthCurve = new AnimationCurve(new Keyframe(0,1),new Keyframe(.35f,.62f),new Keyframe(1,0));
            trail.textureMode = LineTextureMode.Stretch;
            trail.numCornerVertices = 1; trail.numCapVertices = 1;
            trail.shadowCastingMode = ShadowCastingMode.Off; trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.Lerp(color,Color.white,.35f),0),new GradientColorKey(color,.38f),new GradientColorKey(color,1) },
                new[] { new GradientAlphaKey(color.a,0),new GradientAlphaKey(color.a*.62f,.42f),new GradientAlphaKey(0,1) });
            trail.colorGradient = gradient; trail.emitting = true;
            return trail;
        }

        public static void ReleaseTrail(GameObject target)
        {
            var child = target != null ? target.transform.Find(TrailName) : null;
            if (child == null) return;
            var trail = child.GetComponent<TrailRenderer>();
            if (trail == null) return;
            child.SetParent(null, true); trail.emitting = false;
            Object.Destroy(child.gameObject, trail.time + .05f);
        }

        public static void TintProjectile(GameObject target, Color color)
        {
            if (target == null || color.a <= 0f) return;
            var renderer = target.GetComponentInChildren<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null) return;
            var block = new MaterialPropertyBlock(); renderer.GetPropertyBlock(block);
            Color tint = new Color(color.r,color.g,color.b,1);
            block.SetColor("_BaseColor", tint); block.SetColor("_Color", tint);
            if (renderer.sharedMaterial.HasProperty("_EmissionColor")) block.SetColor("_EmissionColor", tint * 1.15f);
            renderer.SetPropertyBlock(block);
        }

        private static ParticleSystem BurstPattern(string name, Vector3 position, Color color, int count, float speed,
            float size, float lifetime, float gravity, ArcaneParticlePattern pattern, Vector3 direction = default, bool stretch = false)
        {
            var ps = Create(name, null, position, pattern, true);
            ConfigureBurst(ps, color, count, speed, size, lifetime, gravity);
            var shape = ps.shape; shape.enabled = true;
            if (direction.sqrMagnitude > .001f)
            { shape.shapeType = ParticleSystemShapeType.Cone; shape.radius = .06f; shape.angle = 31f; shape.rotation = Quaternion.FromToRotation(Vector3.forward,direction.normalized).eulerAngles; }
            else { shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .12f; }
            if (stretch) SetStretch(ps);
            ps.Play(); return ps;
        }

        private static void ConfigureBurst(ParticleSystem ps, Color color, int count, float speed, float size, float lifetime, float gravity)
        {
            count=CombatVfxBudget.BurstCount(count);
            var main=ps.main; main.loop=false; main.duration=Mathf.Max(.08f,lifetime);
            main.startLifetime=new ParticleSystem.MinMaxCurve(lifetime*.65f,lifetime); main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.38f,speed);
            main.startSize=new ParticleSystem.MinMaxCurve(size*.62f,size); main.startColor=new ParticleSystem.MinMaxGradient(color,Color.Lerp(color,Color.white,.19f));
            main.gravityModifier=gravity;main.maxParticles=count;main.stopAction=ParticleSystemStopAction.Destroy;
            var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,(short)count)});
            FadeAndShrink(ps);
        }

        private static ParticleSystem Persistent(string name, Transform parent, Vector3 offset, Color color, float rate, float size,
            float life, float minSpeed, float maxSpeed, ArcaneParticlePattern pattern, int max, float radius, ParticleSystemSimulationSpace space)
        {
            var ps = Create(name,parent,Vector3.zero,pattern,false); ps.transform.localPosition=offset;
            var main=ps.main;main.loop=true;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(life*.68f,life);
            main.startSpeed=new ParticleSystem.MinMaxCurve(minSpeed,maxSpeed);main.startSize=new ParticleSystem.MinMaxCurve(size*.63f,size);
            main.startColor=new ParticleSystem.MinMaxGradient(color,Color.Lerp(color,Color.white,.16f));main.maxParticles=Mathf.Clamp(max,4,36);main.simulationSpace=space;
            var shape=ps.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=radius;
            var emission=ps.emission;emission.rateOverTime=Mathf.Clamp(rate,0,32);FadeAndShrink(ps);
            return ps;
        }

        private static void UpwardCone(ParticleSystem ps,float radius,float angle)
        {var s=ps.shape;s.enabled=true;s.shapeType=ParticleSystemShapeType.Cone;s.radius=radius;s.angle=angle;s.rotation=new Vector3(-90,0,0);}

        private static void SetStretch(ParticleSystem ps)
        {var r=ps.GetComponent<ParticleSystemRenderer>();r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=1.75f;r.velocityScale=.12f;r.cameraVelocityScale=0;}

        private static ParticleSystem Create(string name,Transform parent,Vector3 position,ArcaneParticlePattern pattern,bool transient)
        {
            var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);else go.transform.position=position;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=ArcaneVfxMaterials.Particle(pattern);renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>{ParticleSystemVertexStream.Position,ParticleSystemVertexStream.Color,ParticleSystemVertexStream.UV});
            if(transient)go.AddComponent<TransientVfxToken>();
            return ps;
        }

        private static void FadeAndShrink(ParticleSystem ps)
        {
            var col=ps.colorOverLifetime;col.enabled=true;var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.82f,.35f),new GradientAlphaKey(0,1)});col.color=gradient;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.16f));
        }
    }
}
