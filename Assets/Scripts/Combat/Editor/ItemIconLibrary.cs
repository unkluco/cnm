using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Editor
{
    /// <summary>Hình vẽ biểu tượng cho bộ vật phẩm mở rộng. Thêm vật phẩm mới: thêm một hàm vẽ và một dòng vào All.</summary>
    public static class ItemIconLibrary
    {
        private static readonly Color Wood = new Color(0.5f, 0.32f, 0.17f);
        private static readonly Color DarkWood = new Color(0.33f, 0.2f, 0.1f);
        private static readonly Color Steel = new Color(0.72f, 0.75f, 0.8f);
        private static readonly Color DarkSteel = new Color(0.34f, 0.36f, 0.4f);
        private static readonly Color Gold = new Color(0.95f, 0.75f, 0.2f);
        private static readonly Color Leather = new Color(0.32f, 0.2f, 0.14f);

        public static readonly Dictionary<string, Action<ItemIconPainter>> All = new Dictionary<string, Action<ItemIconPainter>>
        {
            { "Icon_BattleAxe", BattleAxe },
            { "Icon_Sledgehammer", Sledgehammer },
            { "Icon_TwinDaggers", TwinDaggers },
            { "Icon_Scythe", Scythe },
            { "Icon_FireSword", FireSword },
            { "Icon_Crossbow", Crossbow },
            { "Icon_SMG", Smg },
            { "Icon_IceStaff", IceStaff },
            { "Icon_GrenadeLauncher", GrenadeLauncher },
            { "Icon_Chainmail", Chainmail },
            { "Icon_HeatArmor", HeatArmor },
            { "Icon_ShadowCloak", ShadowCloak },
            { "Icon_WadingBoots", WadingBoots },
            { "Icon_SpikedBoots", SpikedBoots },
            { "Icon_SpringBoots", SpringBoots },
            { "Icon_LargePotion", LargePotion },
            { "Icon_RagePotion", RagePotion },
            { "Icon_HandBomb", HandBomb },
            { "Icon_TeleportScroll", TeleportScroll },
            { "Icon_PhoenixFeather", PhoenixFeather },
        };

        private static Vector2 V(float x, float y) => new Vector2(x, y);

        private static void BattleAxe(ItemIconPainter p)
        {
            p.Line(26, 16, 90, 102, 8, Wood);
            p.Line(30, 22, 42, 38, 9, Leather);
            p.Poly(DarkSteel, V(84, 98), V(66, 106), V(62, 98), V(80, 88));
            p.Poly(Steel, V(78, 90), V(98, 70), V(114, 78), V(120, 98), V(112, 116), V(94, 116), V(86, 104));
            p.Line(100, 72, 116, 82, 3, Color.white);
            p.Line(116, 84, 120, 100, 3, Color.white);
            p.Circle(88, 98, 4, Gold);
        }

        private static void Sledgehammer(ItemIconPainter p)
        {
            p.Line(34, 14, 72, 90, 8, Wood);
            p.Line(36, 18, 44, 34, 9, Leather);
            var dir = new Vector2(38, 76).normalized;
            var perp = new Vector2(-dir.y, dir.x);
            var center = V(80, 100);
            p.RotRect(center, perp, 32, 17, DarkSteel);
            p.RotRect(center + perp * 26, perp, 6, 19, new Color(0.25f, 0.26f, 0.3f));
            p.RotRect(center - perp * 26, perp, 6, 19, new Color(0.25f, 0.26f, 0.3f));
            p.RotRect(center, perp, 3, 18, Gold);
            p.Line(center.x + perp.x * 18 + dir.x * 12, center.y + perp.y * 18 + dir.y * 12,
                center.x - perp.x * 18 + dir.x * 12, center.y - perp.y * 18 + dir.y * 12, 2.5f, new Color(0.6f, 0.62f, 0.68f));
        }

        private static void TwinDaggers(ItemIconPainter p)
        {
            var green = new Color(0.55f, 0.95f, 0.8f);
            p.Blade(V(40, 36), V(104, 104), 12, Steel, Gold, Leather, 16);
            p.Blade(V(88, 36), V(24, 104), 12, Color.Lerp(Steel, green, 0.35f), Gold, Leather, 16);
        }

        private static void Scythe(ItemIconPainter p)
        {
            p.Line(44, 10, 66, 116, 6, DarkWood);
            p.Line(48, 28, 52, 44, 7, new Color(0.45f, 0.1f, 0.15f));
            var pts = new List<Vector2>();
            pts.AddRange(ItemIconPainter.Bezier(V(68, 116), V(42, 132), V(8, 82)));
            pts.AddRange(ItemIconPainter.Bezier(V(8, 82), V(34, 106), V(64, 102)));
            p.Poly(new Color(0.68f, 0.7f, 0.76f), pts.ToArray());
            List<Vector2> edge = ItemIconPainter.Bezier(V(10, 84), V(34, 106), V(62, 103));
            for (int i = 1; i < edge.Count; i++)
            {
                p.Line(edge[i - 1].x, edge[i - 1].y, edge[i].x, edge[i].y, 3, new Color(0.7f, 0.08f, 0.2f));
            }

            p.Circle(66, 110, 5, Gold);
        }

        private static void FireSword(ItemIconPainter p)
        {
            var flame = new Color(1f, 0.45f, 0.05f, 0.85f);
            var flameCore = new Color(1f, 0.85f, 0.2f, 0.9f);
            float[,] flames = { { 62, 70, 10 }, { 76, 84, 11 }, { 90, 98, 9 }, { 54, 58, 8 }, { 100, 108, 7 } };
            for (int i = 0; i < flames.GetLength(0); i++)
            {
                p.Circle(flames[i, 0] - 6, flames[i, 1] + 6, flames[i, 2], flame);
                p.Circle(flames[i, 0] + 6, flames[i, 1] - 6, flames[i, 2] * 0.8f, flame);
            }

            p.Blade(V(34, 34), V(108, 108), 15, new Color(1f, 0.38f, 0.1f), Gold, new Color(0.35f, 0.1f, 0.08f), 20);
            p.Line(44, 44, 96, 96, 4, flameCore);
            p.Circle(34, 34, 4, new Color(1f, 0.2f, 0.1f));
        }

        private static void Crossbow(ItemIconPainter p)
        {
            Vector2 prev = Vector2.zero;
            for (int a = 20; a <= 160; a += 7)
            {
                float rad = a * Mathf.Deg2Rad;
                var pt = V(64 + Mathf.Cos(rad) * 46, 50 + Mathf.Sin(rad) * 46);
                if (a > 20) p.Line(prev.x, prev.y, pt.x, pt.y, 7, DarkWood);
                prev = pt;
            }

            var left = V(64 + Mathf.Cos(160 * Mathf.Deg2Rad) * 46, 50 + Mathf.Sin(160 * Mathf.Deg2Rad) * 46);
            var right = V(64 + Mathf.Cos(20 * Mathf.Deg2Rad) * 46, 50 + Mathf.Sin(20 * Mathf.Deg2Rad) * 46);
            p.Line(left.x, left.y, 64, 62, 2, new Color(0.9f, 0.88f, 0.8f));
            p.Line(right.x, right.y, 64, 62, 2, new Color(0.9f, 0.88f, 0.8f));
            p.Line(64, 12, 64, 92, 11, Wood);
            p.Rect(58, 22, 70, 34, DarkSteel);
            p.Line(64, 44, 64, 112, 3, Steel);
            p.Poly(Steel, V(57, 106), V(64, 124), V(71, 106));
            p.Poly(new Color(0.85f, 0.2f, 0.2f), V(64, 52), V(56, 44), V(64, 48));
            p.Poly(new Color(0.85f, 0.2f, 0.2f), V(64, 52), V(72, 44), V(64, 48));
        }

        private static void Smg(ItemIconPainter p)
        {
            var body = new Color(0.22f, 0.23f, 0.26f);
            p.Rect(10, 62, 28, 76, body);
            p.Rect(10, 50, 17, 76, body);
            p.Poly(body, V(36, 58), V(50, 58), V(44, 32), V(30, 32));
            p.Poly(new Color(0.3f, 0.3f, 0.34f), V(62, 58), V(76, 58), V(72, 22), V(58, 22));
            p.Rect(26, 58, 98, 82, body);
            p.Rect(98, 66, 120, 74, DarkSteel);
            p.Rect(36, 82, 88, 86, DarkSteel);
            p.Line(32, 78, 94, 78, 2, new Color(0.5f, 0.52f, 0.58f));
            p.Rect(112, 64, 116, 76, new Color(0.15f, 0.15f, 0.17f));
            p.Circle(84, 70, 3, Gold);
        }

        private static void IceStaff(ItemIconPainter p)
        {
            var ice = new Color(0.5f, 0.85f, 1f);
            p.Line(30, 12, 80, 88, 6, new Color(0.62f, 0.68f, 0.8f));
            p.Line(34, 18, 40, 28, 8, new Color(0.3f, 0.35f, 0.5f));
            p.Line(76, 84, 72, 104, 3, Steel);
            p.Line(80, 86, 102, 96, 3, Steel);
            p.Poly(new Color(0.75f, 0.95f, 1f), V(88, 100), V(80, 126), V(96, 108));
            p.Poly(new Color(0.75f, 0.95f, 1f), V(88, 100), V(118, 112), V(102, 96));
            p.Poly(new Color(0.75f, 0.95f, 1f), V(88, 100), V(112, 80), V(98, 92));
            p.Circle(88, 100, 15, ice);
            p.Circle(88, 100, 9, new Color(0.8f, 0.97f, 1f));
            p.Circle(83, 105, 4, Color.white);
        }

        private static void GrenadeLauncher(ItemIconPainter p)
        {
            var olive = new Color(0.36f, 0.44f, 0.24f);
            var darkOlive = new Color(0.24f, 0.3f, 0.16f);
            p.Rect(8, 62, 26, 80, Wood);
            p.Poly(darkOlive, V(40, 56), V(54, 56), V(48, 30), V(34, 30));
            p.Rect(50, 64, 118, 90, olive);
            p.Rect(110, 62, 122, 92, darkOlive);
            p.Line(56, 86, 108, 86, 2, new Color(0.55f, 0.65f, 0.4f));
            p.Circle(44, 72, 22, darkOlive);
            p.Ring(44, 72, 16, 3, olive);
            for (int i = 0; i < 6; i++)
            {
                float rad = i * 60f * Mathf.Deg2Rad;
                p.Circle(44 + Mathf.Cos(rad) * 10, 72 + Mathf.Sin(rad) * 10, 3.5f, new Color(0.85f, 0.6f, 0.2f));
            }
        }

        private static Vector2[] Torso()
        {
            return new[] { V(34, 18), V(94, 18), V(98, 82), V(114, 94), V(102, 112), V(82, 106), V(64, 96), V(46, 106), V(26, 112), V(14, 94), V(30, 82) };
        }

        private static void Chainmail(ItemIconPainter p)
        {
            Vector2[] torso = Torso();
            p.Poly(new Color(0.58f, 0.6f, 0.65f), torso);
            for (int y = 22; y < 108; y += 9)
            for (int x = 20 + (y / 9 % 2) * 5; x < 110; x += 10)
            {
                if (ItemIconPainter.Inside(torso, x, y)) p.Ring(x, y, 3.5f, 1.6f, new Color(0.35f, 0.37f, 0.42f));
            }

            p.Rect(34, 18, 94, 26, Leather);
            p.Circle(64, 22, 4, Gold);
        }

        private static void HeatArmor(ItemIconPainter p)
        {
            p.Poly(new Color(0.62f, 0.2f, 0.1f), Torso());
            p.Poly(new Color(0.78f, 0.3f, 0.14f), V(40, 26), V(88, 26), V(90, 80), V(64, 90), V(38, 80));
            p.Poly(new Color(1f, 0.5f, 0.08f), V(64, 82), V(50, 58), V(56, 44), V(64, 36), V(72, 44), V(78, 58));
            p.Poly(new Color(1f, 0.85f, 0.25f), V(64, 66), V(58, 52), V(64, 42), V(70, 52));
            p.Line(20, 96, 40, 104, 3, Gold);
            p.Line(108, 96, 88, 104, 3, Gold);
        }

        private static void ShadowCloak(ItemIconPainter p)
        {
            var cloak = new Color(0.3f, 0.15f, 0.42f);
            p.Poly(cloak, V(42, 92), V(86, 92), V(112, 12), V(16, 12));
            p.Circle(64, 94, 24, cloak);
            p.Ellipse(64, 88, 13, 15, new Color(0.06f, 0.03f, 0.1f));
            p.Circle(58, 90, 2.5f, new Color(0.75f, 0.5f, 1f));
            p.Circle(70, 90, 2.5f, new Color(0.75f, 0.5f, 1f));
            p.Line(50, 66, 34, 16, 3, new Color(0.2f, 0.1f, 0.3f));
            p.Line(78, 66, 94, 16, 3, new Color(0.2f, 0.1f, 0.3f));
            p.Line(64, 66, 64, 14, 3, new Color(0.2f, 0.1f, 0.3f));
            p.Circle(64, 70, 6, Gold);
        }

        private static void Boot(ItemIconPainter p, Color main, Color sole, float yOffset)
        {
            float o = yOffset;
            p.Poly(main, V(38, 112 + o * 0.2f), V(72, 112 + o * 0.2f), V(74, 46 + o), V(106, 38 + o), V(110, 22 + o), V(36, 22 + o));
            p.Rect(34, 14 + o, 112, 24 + o, sole);
            p.Rect(36, 100 + o * 0.2f, 74, 112 + o * 0.2f, Color.Lerp(main, Color.white, 0.25f));
        }

        private static void WadingBoots(ItemIconPainter p)
        {
            Boot(p, new Color(0.12f, 0.5f, 0.55f), new Color(0.08f, 0.25f, 0.28f), 0);
            var drop = new Color(0.45f, 0.78f, 1f);
            p.Circle(20, 60, 6, drop);
            p.Poly(drop, V(15, 62), V(20, 74), V(25, 62));
            p.Circle(16, 34, 4, drop);
            p.Poly(drop, V(12.5f, 35), V(16, 43), V(19.5f, 35));
            p.Line(80, 30, 104, 30, 2, new Color(0.4f, 0.8f, 0.85f));
        }

        private static void SpikedBoots(ItemIconPainter p)
        {
            Boot(p, new Color(0.45f, 0.3f, 0.18f), DarkSteel, 10);
            for (int x = 38; x <= 104; x += 11)
            {
                p.Poly(Steel, V(x, 24), V(x + 8, 24), V(x + 4, 8));
            }

            p.Line(40, 70, 70, 70, 3, Gold);
            p.Line(40, 84, 70, 84, 3, Gold);
        }

        private static void SpringBoots(ItemIconPainter p)
        {
            Boot(p, new Color(0.92f, 0.68f, 0.18f), new Color(0.5f, 0.3f, 0.1f), 18);
            float[] xs = { 52, 92 };
            foreach (float x in xs)
            {
                for (int i = 0; i < 4; i++)
                {
                    float y0 = 30 - i * 6;
                    p.Line(x - 10, y0, x + 10, y0 - 3, 3, Steel);
                    p.Line(x + 10, y0 - 3, x - 10, y0 - 6, 3, Steel);
                }
            }

            p.Rect(36, 4, 112, 8, DarkSteel);
            p.Line(80, 52, 104, 50, 3, new Color(1f, 0.95f, 0.6f));
        }

        private static void LargePotion(ItemIconPainter p)
        {
            var glass = new Color(0.75f, 0.88f, 0.95f, 0.9f);
            p.Rect(52, 84, 76, 106, glass);
            p.Rect(48, 104, 80, 118, Wood);
            p.Circle(64, 50, 40, glass);
            p.Circle(64, 48, 35, new Color(0.88f, 0.08f, 0.16f));
            p.Ellipse(64, 74, 26, 6, new Color(1f, 0.35f, 0.4f));
            p.Ellipse(46, 58, 7, 13, new Color(1f, 1f, 1f, 0.6f));
            p.Circle(78, 34, 4, new Color(1f, 0.5f, 0.55f));
            p.Circle(70, 26, 3, new Color(1f, 0.5f, 0.55f));
        }

        private static void RagePotion(ItemIconPainter p)
        {
            var glass = new Color(0.75f, 0.88f, 0.95f, 0.9f);
            p.Poly(glass, V(50, 104), V(78, 104), V(78, 76), V(110, 16), V(18, 16), V(50, 76));
            p.Poly(new Color(1f, 0.32f, 0.08f), V(56, 64), V(72, 64), V(102, 20), V(26, 20));
            p.Rect(46, 102, 82, 116, Wood);
            p.Circle(56, 34, 5, new Color(1f, 0.75f, 0.3f));
            p.Circle(74, 46, 4, new Color(1f, 0.75f, 0.3f));
            p.Circle(66, 26, 3, new Color(1f, 0.75f, 0.3f));
            p.Line(40, 50, 52, 44, 3, new Color(0.6f, 0.05f, 0.05f));
            p.Line(88, 50, 76, 44, 3, new Color(0.6f, 0.05f, 0.05f));
            p.Line(58, 86, 58, 72, 3, new Color(1f, 1f, 1f, 0.6f));
        }

        private static void HandBomb(ItemIconPainter p)
        {
            p.Circle(58, 52, 38, new Color(0.15f, 0.15f, 0.19f));
            p.Circle(46, 66, 10, new Color(0.42f, 0.42f, 0.5f));
            p.RotRect(V(82, 86), new Vector2(1, 1), 10, 8, DarkSteel);
            p.Line(86, 92, 100, 108, 4, new Color(0.85f, 0.72f, 0.5f));
            var spark = new Color(1f, 0.85f, 0.2f);
            p.Line(94, 112, 112, 112, 3, spark);
            p.Line(103, 103, 103, 121, 3, spark);
            p.Line(96, 105, 110, 119, 2.5f, new Color(1f, 0.5f, 0.1f));
            p.Line(96, 119, 110, 105, 2.5f, new Color(1f, 0.5f, 0.1f));
            p.Circle(103, 112, 4, Color.white);
        }

        private static void TeleportScroll(ItemIconPainter p)
        {
            var paper = new Color(0.94f, 0.86f, 0.64f);
            var roll = new Color(0.78f, 0.62f, 0.38f);
            p.Rect(28, 28, 100, 100, paper);
            p.Rect(20, 92, 108, 108, roll);
            p.Rect(20, 20, 108, 36, roll);
            p.Circle(20, 100, 8, DarkWood);
            p.Circle(108, 100, 8, DarkWood);
            p.Circle(20, 28, 8, DarkWood);
            p.Circle(108, 28, 8, DarkWood);
            var rune = new Color(0.5f, 0.3f, 0.95f);
            p.Ring(64, 64, 21, 3, rune);
            p.Ring(64, 64, 12, 2, rune);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                p.Line(64 + Mathf.Cos(a) * 12, 64 + Mathf.Sin(a) * 12, 64 + Mathf.Cos(a) * 21, 64 + Mathf.Sin(a) * 21, 2, rune);
            }

            p.Circle(64, 64, 4, new Color(0.8f, 0.65f, 1f));
        }

        private static void PhoenixFeather(ItemIconPainter p)
        {
            Vector2 baseP = V(32, 18), tip = V(104, 116);
            var outer = new List<Vector2>();
            outer.AddRange(ItemIconPainter.Bezier(baseP, V(6, 96), tip));
            outer.AddRange(ItemIconPainter.Bezier(tip, V(116, 34), baseP));
            p.Poly(new Color(0.9f, 0.12f, 0.08f), outer.ToArray());
            var mid = new List<Vector2>();
            mid.AddRange(ItemIconPainter.Bezier(V(40, 30), V(26, 90), V(98, 108)));
            mid.AddRange(ItemIconPainter.Bezier(V(98, 108), V(102, 44), V(40, 30)));
            p.Poly(new Color(1f, 0.5f, 0.08f), mid.ToArray());
            var inner = new List<Vector2>();
            inner.AddRange(ItemIconPainter.Bezier(V(50, 44), V(46, 88), V(92, 100)));
            inner.AddRange(ItemIconPainter.Bezier(V(92, 100), V(88, 58), V(50, 44)));
            p.Poly(new Color(1f, 0.85f, 0.25f), inner.ToArray());
            p.Line(24, 8, 100, 110, 3, new Color(1f, 0.97f, 0.85f));
            p.Circle(108, 92, 3, Gold);
            p.Circle(20, 70, 2.5f, Gold);
            p.Circle(96, 30, 3, Gold);
        }
    }
}
