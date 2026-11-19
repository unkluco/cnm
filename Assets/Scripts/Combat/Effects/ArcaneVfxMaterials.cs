using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Effects
{
    /// <summary>Visual language shared by weapons and enemy abilities. It does not change any damage rules.</summary>
    public enum CombatVfxElement { Steel, Ember, Frost, Void, Storm, Stone, Life, Blood }

    public enum ArcaneParticlePattern { Glow, Spark, Flame, Smoke, Crystal, Flash, Solid }

    public static class ArcaneVfxMaterials
    {
        private const string ResourceRoot = "VFX/Arcane/";
        private static Material glyph, blade, ribbon, ghost;
        private static Mesh plane;
        private static readonly Dictionary<ArcaneParticlePattern, Material> particles = new();

        public static Material Glyph => glyph != null ? glyph : (glyph = Create("ArcaneGlyph", "Arcane Ground"));
        public static Material Blade => blade != null ? blade : (blade = Create("ArcaneBlade", "Arcane Slash"));
        public static Material Ghost => ghost != null ? ghost : (ghost = Create("ArcaneGhost", "Arcane Afterimage"));
        public static Material Ribbon
        {
            get
            {
                if (ribbon == null)
                {
                    ribbon = Create("ArcaneBlade", "Arcane Ribbon");
                    ribbon.SetFloat("_TrailMode", 1f);
                    ribbon.SetColor("_Tint", Color.white);
                }
                return ribbon;
            }
        }

        public static Mesh Plane
        {
            get
            {
                if (plane != null) return plane;
                plane = new Mesh { name = "Arcane VFX Ground Quad", hideFlags = HideFlags.DontSave };
                plane.vertices = new[] { new Vector3(-.5f, 0, -.5f), new Vector3(-.5f, 0, .5f), new Vector3(.5f, 0, .5f), new Vector3(.5f, 0, -.5f) };
                plane.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                plane.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                plane.RecalculateBounds();
                return plane;
            }
        }

        public static Material Particle(ArcaneParticlePattern pattern)
        {
            if (particles.TryGetValue(pattern, out var existing) && existing != null) return existing;
            var mat = Create("ArcaneParticle", "Arcane " + pattern);
            mat.SetFloat("_Pattern", (float)pattern);
            bool matte = pattern == ArcaneParticlePattern.Smoke || pattern == ArcaneParticlePattern.Solid;
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(matte ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            mat.SetFloat("_Glow", matte ? 1f : pattern == ArcaneParticlePattern.Flash ? 1.8f : pattern == ArcaneParticlePattern.Flame ? 1.23f : 1.48f);
            if (matte) mat.renderQueue = 3007;
            particles[pattern] = mat;
            return mat;
        }

        public static MeshRenderer AddSurface(GameObject surface, Mesh mesh, Material mat)
        {
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        private static Material Create(string resource, string displayName)
        {
            // Shader assets are under Resources so the player build cannot strip them.
            Shader shader = Resources.Load<Shader>(ResourceRoot + resource);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            return new Material(shader) { name = displayName, hideFlags = HideFlags.DontSave };
        }
    }
}
