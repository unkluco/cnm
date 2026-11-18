using UnityEngine;

namespace LightSouls.Combat.Effects
{
    /// <summary>
    /// Tạo hiệu ứng hạt (ParticleSystem) bằng code, không cần prefab: nổ tia, vòng sóng, hào quang, cột sáng, vệt đạn.
    /// Mọi hiệu ứng dùng chung vật liệu cộng sáng ở Resources/VFX/Mat_VFX_Additive (do ItemSetBuilder tạo).
    /// Hiệu ứng một lần tự hủy khi chạy xong; hiệu ứng lặp (Aura, Beam) trả về để nơi gọi tự hủy.
    /// </summary>
    public static class VfxFactory
    {
        public const string MaterialResourcePath = "VFX/Mat_VFX_Additive";

        private static Material sharedMaterial;
        private static Material trailMaterial;

        public static Material Material
        {
            get
            {
                if (sharedMaterial == null)
                {
                    sharedMaterial = Resources.Load<Material>(MaterialResourcePath);
                    if (sharedMaterial == null)
                    {
                        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                        if (shader == null) shader = Shader.Find("Sprites/Default");
                        sharedMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
                    }
                }

                return sharedMaterial;
            }
        }

        /// <summary>Tia hạt bắn tung tóe một lần (trúng đòn, nổ, uống thuốc).</summary>
        public static ParticleSystem Burst(Vector3 position, Color color, int count = 16, float speed = 4f,
            float size = 0.18f, float lifetime = 0.45f, float gravity = 0.6f)
        {
            ParticleSystem ps = Create("VFX_Burst", null, position);
            var main = ps.main;
            main.loop = false;
            main.duration = lifetime;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.gravityModifier = gravity;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(1, count)) });

            FadeAndShrink(ps, color);
            ps.Play();
            return ps;
        }

        /// <summary>Vòng sóng lan ra trên mặt đất (đập búa, nổ, dịch chuyển).</summary>
        public static ParticleSystem Ring(Vector3 position, Color color, float radius = 2.5f, float lifetime = 0.4f, int count = 48)
        {
            ParticleSystem ps = Create("VFX_Ring", null, position + Vector3.up * 0.1f);
            var main = ps.main;
            main.loop = false;
            main.duration = lifetime;
            main.startLifetime = lifetime;
            main.startSpeed = radius / Mathf.Max(0.05f, lifetime);
            main.startSize = 0.28f;
            main.startColor = color;
            main.gravityModifier = 0f;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;
            shape.radiusThickness = 0f;
            shape.rotation = new Vector3(90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            FadeAndShrink(ps, color);
            ps.Play();
            return ps;
        }

        /// <summary>Hào quang lặp gắn vào một đối tượng (vũ khí, nhân vật bị đốt/đóng băng...). Nơi gọi tự hủy.</summary>
        public static ParticleSystem Aura(Transform parent, Color color, float rate = 14f, float radius = 0.35f,
            Vector3 localOffset = default(Vector3), float size = 0.14f, float riseSpeed = 0.8f, string name = "VFX_Aura")
        {
            ParticleSystem ps = Create(name, parent, Vector3.zero);
            ps.transform.localPosition = localOffset;
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.4f));
            main.gravityModifier = -riseSpeed * 0.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;

            var emission = ps.emission;
            emission.rateOverTime = rate;

            FadeAndShrink(ps, color);
            ps.Play();
            return ps;
        }

        /// <summary>Cột sáng bốc lên từ mặt đất (vật phẩm rơi, độ hiếm cao). Nơi gọi tự hủy.</summary>
        public static ParticleSystem Beam(Transform parent, Color color, float height = 1.6f, float rate = 18f)
        {
            ParticleSystem ps = Create("VFX_Beam", parent, Vector3.zero);
            ps.transform.localPosition = Vector3.zero;
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = 1.1f;
            main.startSpeed = height / 1.1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = ps.shape;
            shape.enabled = true;
            // Hình nón góc 0 hướng lên trời: hạt bay thẳng đứng thành cột sáng.
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 0f;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = rate;

            FadeAndShrink(ps, color);
            ps.Play();
            return ps;
        }

        /// <summary>Hào quang có hạn trên một đối tượng; gọi lại cùng key thì gia hạn thay vì tạo chồng.</summary>
        public static void TimedAura(Transform parent, string key, Color color, float duration, float rate = 18f, float radius = 0.5f)
        {
            if (parent == null || duration <= 0f)
            {
                return;
            }

            string name = "VFX_Timed_" + key;
            Transform existing = parent.Find(name);
            TimedDestroy timer;
            if (existing != null)
            {
                timer = existing.GetComponent<TimedDestroy>();
            }
            else
            {
                ParticleSystem ps = Aura(parent, color, rate, radius, Vector3.up * 1f, 0.16f, 1f, name);
                timer = ps.gameObject.AddComponent<TimedDestroy>();
            }

            if (timer != null)
            {
                timer.ExtendTo(Time.time + duration);
            }
        }

        public static void RemoveTimedAura(Transform parent, string key)
        {
            if (parent == null)
            {
                return;
            }

            Transform existing = parent.Find("VFX_Timed_" + key);
            if (existing != null)
            {
                Object.Destroy(existing.gameObject);
            }
        }

        /// <summary>Gắn vệt sáng mờ dần sau đối tượng bay (đạn, bom).</summary>
        public static TrailRenderer AttachTrail(GameObject target, Color color, float width = 0.18f, float time = 0.2f)
        {
            if (target == null || color.a <= 0f)
            {
                return null;
            }

            var trail = target.GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = target.AddComponent<TrailRenderer>();
            }

            if (trailMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default");
                trailMaterial = shader != null ? new Material(shader) { hideFlags = HideFlags.DontSave } : Material;
            }

            trail.sharedMaterial = trailMaterial;
            trail.time = time;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = width;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.Lerp(color, Color.white, 0.4f), 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
            trail.emitting = true;
            return trail;
        }

        private static ParticleSystem Create(string name, Transform parent, Vector3 position)
        {
            var go = new GameObject(name);
            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                go.transform.position = position;
            }

            // AddComponent khiến hệ hạt tự chạy ngay; dừng lại để chỉnh thông số trước khi Play.
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return ps;
        }

        private static void FadeAndShrink(ParticleSystem ps, Color color)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = gradient;

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        }
    }
}
