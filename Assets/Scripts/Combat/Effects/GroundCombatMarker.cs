using UnityEngine;
using UnityEngine.Rendering;

namespace LightSouls.Combat.Effects
{
    /// <summary>Lightweight, non-blocking ground outlines shared by aim feedback and enemy warnings.</summary>
    public static class GroundCombatMarker
    {
        private static Material material;

        public static LineRenderer Create(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            if (material == null)
            {
                material = Resources.Load<Material>("VFX/Mat_CombatMarker");
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            }
            line.sharedMaterial = material;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            return line;
        }

        public static void Circle(LineRenderer line, float radius, float width, Color color)
        {
            const int segments = 80;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = width;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (Mathf.PI * 2f / segments);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            Tint(line, color);
        }

        public static void Lane(LineRenderer line, Vector3 start, Vector3 end, float halfWidth, Color color)
        {
            Vector3 delta = end - start;
            delta.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, delta.normalized) * halfWidth;
            line.transform.position = start + Vector3.up * 0.075f;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = 0.11f;
            line.SetPositions(new[] { -side, side, delta + side, delta - side });
            Tint(line, color);
        }

        public static void Tint(LineRenderer line, Color color)
        {
            line.startColor = line.endColor = color;
        }
    }
}
