using UnityEngine;

namespace LightSouls.Combat.Effects
{
    public enum TelegraphStyle { Danger, Blessing, Aim, Impact }
    public enum TelegraphShape { Circle, Lane }

    /// <summary>
    /// Animated, non-blocking warning surface. The thin outline is the exact gameplay radius/width;
    /// the inner runes and travelling charge line only communicate the existing wind-up.
    /// </summary>
    public sealed class ArcaneTelegraph : MonoBehaviour
    {
        private const float SurfaceHeight = .08f;
        private static readonly Color DangerEdge = new Color(1f, .28f, .13f, 1f);
        private LineRenderer outline;
        private MeshRenderer surface;
        private MaterialPropertyBlock properties;
        private Transform owner;
        private bool followsOwnerLife;
        private bool timed;
        private float startTime, duration, opacity = 1f;
        private Color accent;
        private CombatVfxElement element;

        public TelegraphShape Shape { get; private set; }
        public TelegraphStyle Style { get; private set; }
        public float Radius { get; private set; }
        public float HalfWidth { get; private set; }
        public float Length { get; private set; }
        public float Progress { get; private set; }
        public bool IsVisible => surface != null && surface.enabled;

        public static ArcaneTelegraph Circle(string name, Vector3 center, float radius, Color color,
            float windup, CombatVfxElement element = CombatVfxElement.Steel, TelegraphStyle style = TelegraphStyle.Danger,
            Transform owner = null, Transform parent = null)
        {
            var marker = Create(name, color, windup, element, style, owner, parent);
            marker.Shape = TelegraphShape.Circle;
            marker.SetCircle(center, radius);
            marker.Render();
            return marker;
        }

        public static ArcaneTelegraph Lane(string name, Vector3 start, Vector3 end, float halfWidth, Color color,
            float windup, CombatVfxElement element = CombatVfxElement.Steel, Transform owner = null)
        {
            var marker = Create(name, color, windup, element, TelegraphStyle.Danger, owner, null);
            marker.Shape = TelegraphShape.Lane;
            marker.SetLane(start, end, halfWidth);
            marker.Render();
            return marker;
        }

        private static ArcaneTelegraph Create(string name, Color color, float windup, CombatVfxElement element,
            TelegraphStyle style, Transform owner, Transform parent)
        {
            // Retain the precise line renderer (including the existing aim contract) as a fine luminous rim.
            var line = GroundCombatMarker.Create(name, parent);
            var marker = line.gameObject.AddComponent<ArcaneTelegraph>();
            marker.outline = line;
            marker.owner = owner;
            marker.followsOwnerLife = owner != null;
            marker.accent = color;
            marker.Style = style;
            marker.element = element;
            marker.timed = windup > 0f;
            marker.duration = Mathf.Max(.05f, windup);
            marker.startTime = Time.time;
            marker.properties = new MaterialPropertyBlock();
            var plane = new GameObject("Art_ArcaneGlyph");
            plane.transform.SetParent(line.transform, false);
            plane.transform.localPosition = Vector3.up * .005f;
            marker.surface = ArcaneVfxMaterials.AddSurface(plane, ArcaneVfxMaterials.Plane, ArcaneVfxMaterials.Glyph);
            return marker;
        }

        public void SetCircle(Vector3 center, float radius)
        {
            Shape = TelegraphShape.Circle;
            transform.position = center + Vector3.up * SurfaceHeight;
            float newRadius = Mathf.Max(.01f, radius);
            if (!Mathf.Approximately(Radius, newRadius) || outline.positionCount == 0)
                GroundCombatMarker.Circle(outline, newRadius, Style == TelegraphStyle.Aim ? .035f : .05f, OutlineColor());
            Radius = newRadius;
            surface.transform.localPosition = Vector3.up * .005f;
            surface.transform.localRotation = Quaternion.identity;
            surface.transform.localScale = new Vector3(Radius * 2f, 1f, Radius * 2f);
        }

        public void SetLane(Vector3 start, Vector3 end, float halfWidth)
        {
            Shape = TelegraphShape.Lane;
            HalfWidth = Mathf.Max(.01f, halfWidth);
            Vector3 delta = end - start; delta.y = 0f;
            Length = delta.magnitude;
            Vector3 forward = Length > .01f ? delta / Length : Vector3.forward;
            // A blocked charge can have a zero-length destination. Retain its small endpoint, not an invalid mesh.
            Vector3 side = Vector3.Cross(Vector3.up, forward) * HalfWidth;
            transform.SetPositionAndRotation(start + Vector3.up * SurfaceHeight, Quaternion.identity);
            outline.loop = true;
            outline.widthMultiplier = Mathf.Min(.045f, HalfWidth * .22f);
            outline.positionCount = 4;
            outline.SetPosition(0, -side); outline.SetPosition(1, side);
            outline.SetPosition(2, delta + side); outline.SetPosition(3, delta - side);
            surface.transform.localPosition = delta * .5f + Vector3.up * .005f;
            surface.transform.localRotation = Quaternion.LookRotation(forward, Vector3.up);
            surface.transform.localScale = new Vector3(HalfWidth * 2f, 1f, Mathf.Max(.015f, Length));
        }

        public void SetProgress(float progress)
        {
            Progress = Mathf.Clamp01(progress);
            Render();
        }

        public void SetOpacity(float value)
        {
            opacity = Mathf.Clamp01(value);
            Render();
        }

        public void SetVisible(bool visible)
        {
            if (outline != null) outline.enabled = visible;
            if (surface != null) surface.enabled = visible;
        }

        public void Remove()
        {
            SetVisible(false);
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        private void Update()
        {
            if (followsOwnerLife && (owner == null || !owner.gameObject.activeInHierarchy)) { Remove(); return; }
            if (!timed) return;
            float elapsed = Time.time - startTime;
            SetProgress(elapsed / duration);
            // Explicit owner cleanup normally happens at resolution. This is a guard against interrupted casts.
            if (elapsed > duration + .18f) Remove();
        }

        private Color OutlineColor()
        {
            Color color = Style == TelegraphStyle.Danger ? Color.Lerp(DangerEdge, accent, .18f) : accent;
            color.a = opacity * (Style == TelegraphStyle.Aim ? .34f : .64f) * accent.a;
            return color;
        }

        private void Render()
        {
            if (surface == null) return;
            properties.SetColor("_Accent", accent);
            properties.SetFloat("_Progress", Progress);
            properties.SetFloat("_Opacity", opacity);
            properties.SetFloat("_Style", (float)Style);
            properties.SetFloat("_Shape", (float)Shape);
            properties.SetFloat("_Element", (float)element);
            surface.SetPropertyBlock(properties);
            GroundCombatMarker.Tint(outline, OutlineColor());
        }
    }
}
