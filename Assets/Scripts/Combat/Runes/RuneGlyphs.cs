using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Combat.Runes
{
    // Order is part of the model/reference-bank contract. Do not reorder.
    public enum RuneGlyph { Slash, Thrust, Diagonal, Cleave, Lightning, Ring, Crescent, Wave, Flame, Vortex, Ward, Wall }

    public static class RuneGlyphs
    {
        public const int Count = 12;
        private static readonly string[] ids = { "slash", "thrust", "diagonal", "cleave", "lightning", "ring", "crescent", "wave", "flame", "vortex", "ward", "wall" };
        private static readonly string[] names = { "Nét ngang", "Nét dọc", "Nét chéo", "Góc V", "Tia chớp", "Vòng tròn", "Trăng khuyết", "Làn sóng", "Nét S", "Xoáy ốc", "Tam giác", "Hình vuông" };
        private static readonly Texture2D[] icons = new Texture2D[Count];

        public static string Id(RuneGlyph glyph) => ids[(int)glyph];
        public static string Name(RuneGlyph glyph) => names[(int)glyph];
        public static Texture2D Icon(RuneGlyph glyph)
        {
            int index = (int)glyph;
            if (icons[index] == null) icons[index] = Resources.Load<Texture2D>("RuneCasting/Glyphs/" + ids[index]);
            return icons[index];
        }

        public static Vector2[] Example(RuneGlyph glyph)
        {
            Vector2 P(double x, double y) => new Vector2((float)x, (float)y);
            switch (glyph)
            {
                case RuneGlyph.Slash: return Polyline(P(.14,.5), P(.86,.5));
                case RuneGlyph.Thrust: return Polyline(P(.5,.16), P(.5,.84));
                case RuneGlyph.Diagonal: return Polyline(P(.22,.78), P(.78,.22));
                case RuneGlyph.Cleave: return Polyline(P(.15,.19), P(.50,.82), P(.85,.19));
                case RuneGlyph.Lightning: return Polyline(P(.19,.18),P(.82,.18),P(.18,.82),P(.82,.82));
                case RuneGlyph.Ring: return Curve(81,t=>P(.5+.32*Math.Cos(-Math.PI/2+t*2*Math.PI),.5+.32*Math.Sin(-Math.PI/2+t*2*Math.PI)));
                case RuneGlyph.Crescent: return Curve(81,t=>P(.5+.32*Math.Cos(Math.PI/4+t*1.5*Math.PI),.5+.32*Math.Sin(Math.PI/4+t*1.5*Math.PI)));
                case RuneGlyph.Wave: return Curve(81,t=>P(.14+.72*t,.5+.15*Math.Sin(2*Math.PI*t)));
                case RuneGlyph.Flame: return Curve(81,t=>P(.5+.18*Math.Sin(2*Math.PI*t),.14+.72*t));
                case RuneGlyph.Vortex: return Curve(121,t=>P(.5+(.34*(1-t)+.045)*Math.Cos(-Math.PI/2+t*2.7*Math.PI),.5+(.34*(1-t)+.045)*Math.Sin(-Math.PI/2+t*2.7*Math.PI)));
                case RuneGlyph.Ward: return Polyline(P(.5,.13),P(.87,.82),P(.13,.82),P(.5,.13));
                case RuneGlyph.Wall: return Polyline(P(.18,.18),P(.82,.18),P(.82,.82),P(.18,.82),P(.18,.18));
                default: return Array.Empty<Vector2>();
            }
        }

        private static Vector2[] Curve(int count, Func<double, Vector2> sample)
        {
            var path = new Vector2[count];
            for (int i=0;i<count;i++) path[i] = sample(i/(double)(count-1));
            return path;
        }

        private static Vector2[] Polyline(params Vector2[] vertices)
        {
            var distances = new double[vertices.Length];
            for(int i=1;i<vertices.Length;i++) distances[i]=distances[i-1]+Vector2.Distance(vertices[i-1],vertices[i]);
            var path = new Vector2[81]; int segment=0;
            for(int i=0;i<path.Length;i++)
            {
                double t=distances[^1]*i/(path.Length-1);
                while(segment<vertices.Length-2&&distances[segment+1]<t)segment++;
                double span=distances[segment+1]-distances[segment];
                path[i]=Vector2.LerpUnclamped(vertices[segment],vertices[segment+1],span>0?(float)((t-distances[segment])/span):0);
            }
            return path;
        }
    }
}
