using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LightSouls.Combat.Editor
{
    /// <summary>
    /// Vẽ biểu tượng vật phẩm 128x128 bằng code (không cần công cụ sinh ảnh).
    /// Toạ độ y hướng lên (gốc ở góc dưới trái). Có khử răng cưa, viền tối và bóng đổ để nhìn như icon game.
    /// </summary>
    public class ItemIconPainter
    {
        public const int Size = 128;
        private static readonly Color OutlineColor = new Color(0.07f, 0.05f, 0.04f, 1f);

        private readonly Color[] pixels = new Color[Size * Size];

        public void Blend(int x, int y, Color c, float coverage = 1f)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            float a = Mathf.Clamp01(c.a * coverage);
            if (a <= 0f) return;
            int i = y * Size + x;
            Color dst = pixels[i];
            float outA = a + dst.a * (1f - a);
            Color result = outA > 0f
                ? (new Color(c.r, c.g, c.b) * a + new Color(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / outA
                : Color.clear;
            result.a = outA;
            pixels[i] = result;
        }

        public void Circle(float cx, float cy, float r, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - r - 1); y <= Mathf.CeilToInt(cy + r + 1); y++)
            for (int x = Mathf.FloorToInt(cx - r - 1); x <= Mathf.CeilToInt(cx + r + 1); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                Blend(x, y, c, Mathf.Clamp01(r - d + 0.5f));
            }
        }

        public void Ellipse(float cx, float cy, float rx, float ry, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - ry - 1); y <= Mathf.CeilToInt(cy + ry + 1); y++)
            for (int x = Mathf.FloorToInt(cx - rx - 1); x <= Mathf.CeilToInt(cx + rx + 1); x++)
            {
                float nx = (x + 0.5f - cx) / rx;
                float ny = (y + 0.5f - cy) / ry;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                Blend(x, y, c, Mathf.Clamp01((1f - d) * Mathf.Min(rx, ry) + 0.5f));
            }
        }

        public void Ring(float cx, float cy, float r, float width, Color c)
        {
            for (int y = Mathf.FloorToInt(cy - r - width); y <= Mathf.CeilToInt(cy + r + width); y++)
            for (int x = Mathf.FloorToInt(cx - r - width); x <= Mathf.CeilToInt(cx + r + width); x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                Blend(x, y, c, Mathf.Clamp01(width * 0.5f - Mathf.Abs(d - r) + 0.5f));
            }
        }

        public void Rect(float x0, float y0, float x1, float y1, Color c)
        {
            Poly(c, new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1));
        }

        /// <summary>Đoạn thẳng có độ dày (đầu tròn).</summary>
        public void Line(float x0, float y0, float x1, float y1, float width, Color c)
        {
            var a = new Vector2(x0, y0);
            var b = new Vector2(x1, y1);
            float r = width * 0.5f;
            int minX = Mathf.FloorToInt(Mathf.Min(x0, x1) - r - 1), maxX = Mathf.CeilToInt(Mathf.Max(x0, x1) + r + 1);
            int minY = Mathf.FloorToInt(Mathf.Min(y0, y1) - r - 1), maxY = Mathf.CeilToInt(Mathf.Max(y0, y1) + r + 1);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(0.0001f, ab.sqrMagnitude);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                float d = Vector2.Distance(p, a + ab * t);
                Blend(x, y, c, Mathf.Clamp01(r - d + 0.5f));
            }
        }

        /// <summary>Đa giác lấp đầy, khử răng cưa bằng 4 điểm mẫu mỗi điểm ảnh.</summary>
        public void Poly(Color c, params Vector2[] pts)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Vector2 p in pts)
            {
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }

            for (int y = Mathf.FloorToInt(minY); y <= Mathf.CeilToInt(maxY); y++)
            for (int x = Mathf.FloorToInt(minX); x <= Mathf.CeilToInt(maxX); x++)
            {
                int inside = 0;
                if (Inside(pts, x + 0.25f, y + 0.25f)) inside++;
                if (Inside(pts, x + 0.75f, y + 0.25f)) inside++;
                if (Inside(pts, x + 0.25f, y + 0.75f)) inside++;
                if (Inside(pts, x + 0.75f, y + 0.75f)) inside++;
                if (inside > 0) Blend(x, y, c, inside / 4f);
            }
        }

        public static bool Inside(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > y) != (pts[j].y > y) &&
                    x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>Hình chữ nhật xoay: tâm, trục A (đơn vị) với nửa dài, trục B với nửa rộng.</summary>
        public void RotRect(Vector2 center, Vector2 axisA, float halfA, float halfB, Color c)
        {
            axisA.Normalize();
            var axisB = new Vector2(-axisA.y, axisA.x);
            Poly(c,
                center + axisA * halfA + axisB * halfB,
                center - axisA * halfA + axisB * halfB,
                center - axisA * halfA - axisB * halfB,
                center + axisA * halfA - axisB * halfB);
        }

        public static List<Vector2> Bezier(Vector2 a, Vector2 control, Vector2 b, int steps = 16)
        {
            var list = new List<Vector2>();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                list.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * control + t * t * b);
            }

            return list;
        }

        /// <summary>Lưỡi dao/kiếm: chuôi, chắn tay, lưỡi nhọn có gân sáng.</summary>
        public void Blade(Vector2 hilt, Vector2 tip, float width, Color blade, Color guard, Color grip, float gripLength = 18f)
        {
            Vector2 d = (tip - hilt).normalized;
            var p = new Vector2(-d.y, d.x);
            Vector2 shoulder = hilt + d * 6f;
            Poly(blade, shoulder + p * width * 0.5f, tip - d * width * 0.6f + p * width * 0.35f, tip,
                tip - d * width * 0.6f - p * width * 0.35f, shoulder - p * width * 0.5f);
            Line(shoulder.x + d.x * 2, shoulder.y + d.y * 2, tip.x - d.x * 8, tip.y - d.y * 8, Mathf.Max(1.5f, width * 0.18f),
                Color.Lerp(blade, Color.white, 0.55f));
            Vector2 pommel = hilt - d * gripLength;
            Line(hilt.x, hilt.y, pommel.x, pommel.y, Mathf.Max(4f, width * 0.45f), grip);
            Line(hilt.x + p.x * width * 0.95f, hilt.y + p.y * width * 0.95f, hilt.x - p.x * width * 0.95f, hilt.y - p.y * width * 0.95f, 5f, guard);
            Circle(pommel.x, pommel.y, Mathf.Max(3.5f, width * 0.32f), guard);
        }

        /// <summary>Thêm viền tối 2 px và bóng đổ nhẹ cho mọi phần đã vẽ.</summary>
        public Texture2D Finish()
        {
            var result = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float shadow = Alpha(x - 3, y + 3) * 0.35f;
                float outline = 0f;
                for (int oy = -2; oy <= 2; oy++)
                for (int ox = -2; ox <= 2; ox++)
                {
                    if (ox * ox + oy * oy > 5) continue;
                    outline = Mathf.Max(outline, Alpha(x + ox, y + oy));
                }

                Color under = new Color(0f, 0f, 0f, shadow);
                Color line = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, outline);
                Color baseColor = Over(line, under);
                result[y * Size + x] = Over(pixels[y * Size + x], baseColor);
            }

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.SetPixels(result);
            tex.Apply();
            return tex;
        }

        private float Alpha(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return 0f;
            return pixels[y * Size + x].a;
        }

        private static Color Over(Color top, Color bottom)
        {
            float a = top.a + bottom.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            Color c = (new Color(top.r, top.g, top.b) * top.a + new Color(bottom.r, bottom.g, bottom.b) * bottom.a * (1f - top.a)) / a;
            c.a = a;
            return c;
        }

        /// <summary>Vẽ rồi ghi PNG (ghi đè) và cấu hình nhập dạng Sprite. Trả về Sprite đã nhập.</summary>
        public static Sprite Save(string folder, string name, Action<ItemIconPainter> draw)
        {
            var painter = new ItemIconPainter();
            draw(painter);
            Texture2D tex = painter.Finish();
            string path = folder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Bilinear;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
