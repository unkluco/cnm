using System;
using System.Collections.Generic;
using LightSouls.Combat.Effects;
using UnityEngine;

namespace LightSouls.Combat.Bosses
{
    public enum BossKind { Varkhan, Nhalis, Orgrim, Noctyr, Aurex }
    public enum BossMotion { None, Charge, Blink }
    public enum BossStatus { None, Burn, Chill }

    public sealed class BossSkill
    {
        public string Name { get; }
        public float Windup { get; }
        public float Recovery { get; }
        public float Damage { get; }
        public BossStatus Status { get; }
        private readonly int stages, enragedStages;

        public BossSkill(string name, float windup, float recovery, float damage, int stages = 1,
            int enragedStages = 0, BossStatus status = BossStatus.None)
        {
            Name = name; Windup = windup; Recovery = recovery; Damage = damage;
            this.stages = stages; this.enragedStages = enragedStages == 0 ? stages : enragedStages; Status = status;
        }

        public int StageCount(int phase) => phase >= 2 ? enragedStages : stages;
        public float CastTime(int phase) => phase >= 2 ? Mathf.Max(.78f, Windup * .86f) : Windup;
        public float RestTime(int phase) => phase >= 2 ? Mathf.Max(.85f, Recovery * .83f) : Recovery;
    }

    public sealed class BossDefinition
    {
        public BossKind Kind { get; }
        public string Name { get; }
        public string Title { get; }
        public float Health { get; }
        public float Height { get; }
        public float Radius { get; }
        public float Speed { get; }
        public float PreferredDistance { get; }
        public Color Accent { get; }
        public CombatVfxElement Element { get; }
        public IReadOnlyList<BossSkill> Skills { get; }

        public BossDefinition(BossKind kind, string name, string title, float health, float height, float radius,
            float speed, float distance, Color accent, CombatVfxElement element, params BossSkill[] skills)
        {
            Kind = kind; Name = name; Title = title; Health = health; Height = height; Radius = radius;
            Speed = speed; PreferredDistance = distance; Accent = accent; Element = element;
            Skills = Array.AsReadOnly(skills);
        }
    }

    /// <summary>All five encounters share the same warning/damage geometry, but have their own rhythm and moves.</summary>
    public static class BossRoster
    {
        private static BossSkill S(string name, float cast, float rest, float damage, int stages = 1, int rage = 0,
            BossStatus status = BossStatus.None) => new BossSkill(name, cast, rest, damage, stages, rage, status);

        private static readonly BossDefinition[] roster =
        {
            new BossDefinition(BossKind.Varkhan, "VARKHAN", "CHÚA TỂ TRO TÀN", 480, 4.45f, 1.12f, 2.3f, 4.8f,
                new Color(1f,.39f,.08f), CombatVfxElement.Ember,
                S("Hỏa đao",1.05f,1.25f,19,1,2,BossStatus.Burn),
                S("Vành tro đỏ",1.3f,1.5f,18,1,2),
                S("Tam ấn dung nham",1.12f,1.45f,16,1,2,BossStatus.Burn),
                S("Hỏa xa",1.35f,1.7f,22),
                S("Mưa thiên thạch",1.02f,1.65f,13,2,3,BossStatus.Burn)),
            new BossDefinition(BossKind.Nhalis, "NHALIS", "NỮ VƯƠNG BĂNG VĨNH CỬU", 420, 4.35f, .94f, 1.9f, 7.2f,
                new Color(.23f,.79f,1f), CombatVfxElement.Frost,
                S("Vương miện băng",1.23f,1.4f,17,1,2,BossStatus.Chill),
                S("Băng tuyến",1.12f,1.4f,15,1,2,BossStatus.Chill),
                S("Tuyết ấn",1.18f,1.55f,14,2,2,BossStatus.Chill),
                S("Băng vỡ",1.12f,1.3f,18,1,2),
                S("Lưới pha lê",1.25f,1.65f,16,2,2,BossStatus.Chill)),
            new BossDefinition(BossKind.Orgrim, "ORGRIM", "CỰ THẦN THẠCH ẤN", 550, 4.55f, 1.35f, 1.75f, 4.6f,
                new Color(.96f,.66f,.23f), CombatVfxElement.Stone,
                S("Nghiền đất",1.42f,1.65f,23),
                S("Song quyền",1.08f,1.5f,17,2,2),
                S("Vết nứt cổ đại",1.28f,1.4f,19,1,2),
                S("Cự thạch lao",1.42f,1.8f,24),
                S("Vòng địa chấn",1.18f,1.65f,18,2,3)),
            new BossDefinition(BossKind.Noctyr, "NOCTYR", "VUA HƯ KHÔNG", 455, 4.5f, 1.0f, 2.5f, 6.3f,
                new Color(.72f,.31f,1f), CombatVfxElement.Void,
                S("Bước qua hư vô",1.28f,1.6f,20),
                S("Nguyệt thực",1.24f,1.4f,18),
                S("Tam bóng kiếm",1.16f,1.35f,18,1,2),
                S("Mảnh gương",1.02f,1.45f,13,2,3),
                S("Liềm hư không",1.15f,1.55f,17,2,2)),
            new BossDefinition(BossKind.Aurex, "AUREX", "ĐẠI LONG LÔI ẤN", 510, 4.3f, 1.48f, 2.0f, 6f,
                new Color(1f,.84f,.24f), CombatVfxElement.Storm,
                S("Lôi tức",1.3f,1.5f,21),
                S("Cánh bão",1.17f,1.35f,18,1,2),
                S("Thiên lôi",1.04f,1.4f,14,2,3),
                S("Đuôi lôi long",1.15f,1.45f,19,1,2),
                S("Sét phân nhánh",1.30f,1.7f,19,1,2))
        };

        public static IReadOnlyList<BossDefinition> All { get; } = Array.AsReadOnly(roster);
        public static BossDefinition Get(BossKind kind) => roster[Mathf.Clamp((int)kind, 0, roster.Length - 1)];
    }

    /// <summary>Horizontal hit shape. The same immutable snapshot draws the warning and determines damage.</summary>
    public readonly struct BossZone
    {
        public TelegraphShape Shape { get; }
        public Vector3 Origin { get; }
        public Vector3 Forward { get; }
        public float Radius { get; }
        public float InnerRadius { get; }
        public float HalfWidth { get; }
        public float Length { get; }
        public float Angle { get; }
        public Vector3 End => Origin + Forward * Length;

        private BossZone(TelegraphShape shape, Vector3 origin, Vector3 forward, float radius, float inner,
            float halfWidth, float length, float angle)
        {
            Shape = shape; Origin = origin; Forward = FlatDirection(forward); Radius = radius; InnerRadius = inner;
            HalfWidth = halfWidth; Length = length; Angle = angle;
        }

        public static Vector3 FlatDirection(Vector3 v) { v.y = 0; return v.sqrMagnitude > .0001f ? v.normalized : Vector3.forward; }
        public static BossZone Circle(Vector3 center, float radius) => new BossZone(TelegraphShape.Circle, center, Vector3.forward, radius, 0, 0, 0, 360);
        public static BossZone Ring(Vector3 center, float inner, float outer) => new BossZone(TelegraphShape.Annulus, center, Vector3.forward, outer, inner, 0, 0, 360);
        public static BossZone Sector(Vector3 center, Vector3 direction, float radius, float angle) => new BossZone(TelegraphShape.Sector, center, direction, radius, 0, 0, 0, angle);
        public static BossZone Lane(Vector3 start, Vector3 end, float halfWidth) => new BossZone(TelegraphShape.Lane, start, end - start, 0, 0, halfWidth,
            Vector3.ProjectOnPlane(end - start, Vector3.up).magnitude, 0);

        public bool Contains(Vector3 point)
        {
            Vector3 delta = point - Origin; delta.y = 0;
            if (Shape == TelegraphShape.Lane)
            {
                float along = Vector3.Dot(delta, Forward);
                return along >= 0f && along <= Length && Mathf.Abs(Vector3.Dot(delta, Vector3.Cross(Vector3.up, Forward))) <= HalfWidth;
            }
            float sq = delta.sqrMagnitude;
            if (sq > Radius * Radius) return false;
            if (Shape == TelegraphShape.Annulus) return sq >= InnerRadius * InnerRadius;
            if (Shape == TelegraphShape.Sector && sq > .0001f)
                return Vector3.Dot(delta.normalized, Forward) >= Mathf.Cos(Angle * .5f * Mathf.Deg2Rad);
            return true;
        }

        public ArcaneTelegraph Draw(string name, Color color, float time, CombatVfxElement element,
            Transform owner, TelegraphStyle style = TelegraphStyle.Danger)
        {
            switch (Shape)
            {
                case TelegraphShape.Lane: return ArcaneTelegraph.Lane(name, Origin, End, HalfWidth, color, time, element, owner, style);
                case TelegraphShape.Sector: return ArcaneTelegraph.Sector(name, Origin, Forward, Radius, Angle, color, time, element, owner, style);
                case TelegraphShape.Annulus: return ArcaneTelegraph.Annulus(name, Origin, InnerRadius, Radius, color, time, element, owner, style);
                default: return ArcaneTelegraph.Circle(name, Origin, Radius, color, time, element, style, owner);
            }
        }
    }

    public readonly struct BossArena
    {
        public Bounds Bounds { get; }
        public float FloorY { get; }
        public Vector3 Center => new Vector3(Bounds.center.x, FloorY, Bounds.center.z);
        public BossArena(Bounds bounds, float floorY) { Bounds = bounds; FloorY = floorY; }

        public Vector3 Clamp(Vector3 position, float inset = 1f)
        {
            float x = Mathf.Min(inset, Bounds.extents.x * .8f), z = Mathf.Min(inset, Bounds.extents.z * .8f);
            return new Vector3(Mathf.Clamp(position.x, Bounds.min.x + x, Bounds.max.x - x), FloorY,
                Mathf.Clamp(position.z, Bounds.min.z + z, Bounds.max.z - z));
        }

        public bool Contains(Vector3 position) => position.x >= Bounds.min.x && position.x <= Bounds.max.x &&
            position.z >= Bounds.min.z && position.z <= Bounds.max.z;

        public Vector3 LaneEnd(Vector3 origin, Vector3 direction, float length, float inset)
        {
            direction = BossZone.FlatDirection(direction);
            Vector3 min = Bounds.min + new Vector3(inset, 0, inset), max = Bounds.max - new Vector3(inset, 0, inset);
            if (direction.x > .001f) length = Mathf.Min(length, (max.x - origin.x) / direction.x);
            else if (direction.x < -.001f) length = Mathf.Min(length, (min.x - origin.x) / direction.x);
            if (direction.z > .001f) length = Mathf.Min(length, (max.z - origin.z) / direction.z);
            else if (direction.z < -.001f) length = Mathf.Min(length, (min.z - origin.z) / direction.z);
            return origin + direction * Mathf.Max(.05f, length);
        }
    }

    public sealed class BossCastPlan
    {
        public List<BossZone> Zones { get; } = new List<BossZone>(7);
        public BossMotion Motion { get; set; }
        public Vector3 Destination { get; set; }
    }

    /// <summary>Builds a new locked attack for each announced stage. No stage follows the player after its warning.</summary>
    public static class BossPatterns
    {
        public static BossCastPlan Build(BossKind kind, int skill, int stage, int phase, Vector3 origin, Vector3 target, BossArena arena)
        {
            origin.y = target.y = arena.FloorY;
            target = arena.Clamp(target, 1.1f);
            Vector3 forward = BossZone.FlatDirection(target - origin), side = Vector3.Cross(Vector3.up, forward);
            Vector3 Rot(Vector3 d, float yaw) => Quaternion.Euler(0, yaw, 0) * d;
            var plan = new BossCastPlan { Destination = origin };
            void Circle(Vector3 p, float radius) => plan.Zones.Add(BossZone.Circle(arena.Clamp(p, radius + .45f), radius));
            void Ring(Vector3 p, float inner, float outer) => plan.Zones.Add(BossZone.Ring(p, inner, outer));
            void Fan(Vector3 p, Vector3 d, float radius, float angle) => plan.Zones.Add(BossZone.Sector(p, d, radius, angle));
            void Lane(Vector3 p, Vector3 d, float length, float width)
            {
                p = arena.Clamp(p, width + .55f);
                Vector3 end = arena.LaneEnd(p, d, length, width + .55f);
                plan.Zones.Add(BossZone.Lane(p, end, width));
            }
            void Spread(int count, float degrees, float length, float width, float offset = 0f)
            {
                for (int i = 0; i < count; i++) Lane(origin, Rot(forward, (i - (count - 1) * .5f) * degrees + offset), length, width);
            }
            void Charge(float width, float range)
            {
                plan.Motion = BossMotion.Charge;
                plan.Destination = arena.LaneEnd(origin, forward, Mathf.Min(range, Vector3.Distance(origin, target) + 2f), 3.3f);
                plan.Zones.Add(BossZone.Lane(origin, plan.Destination, width));
            }

            switch (kind)
            {
                case BossKind.Varkhan:
                    switch (skill)
                    {
                        case 0: Fan(origin, Rot(forward, stage == 0 ? -9 : 9), 6.2f, 112); break;
                        case 1: if (stage == 0) Ring(origin, 3.1f, 8f); else Circle(origin, 3.5f); break;
                        case 2: for (int i = -1; i <= 1; i++) Circle(target + (stage == 0 ? side : forward) * i * 3.15f, 1.72f); break;
                        case 3: Charge(1.85f, 11f); break;
                        default:
                            Circle(target, 2.25f); Circle(target + Rot(side, stage * 50f) * 4f, 1.8f);
                            Circle(target - Rot(side, stage * 50f) * 4f, 1.8f); break;
                    }
                    break;
                case BossKind.Nhalis:
                    switch (skill)
                    {
                        case 0: for (int i = 0; i < 6; i++) Circle(origin + Rot(forward, i * 60 + stage * 30) * 5.4f, 1.65f); break;
                        case 1: Spread(3, 24, 16f, .75f, stage * 12); break;
                        case 2: if (stage == 0) Circle(target, 3.25f); else Ring(target, 1.65f, 5.3f); break;
                        case 3:
                            for (int i = 0; i < 4; i++) Lane(origin, Rot(forward, i * 90 + stage * 45), 14f, 1.05f);
                            break;
                        default:
                            // Parallel freezing strips leave generous walkable gaps; second wave shifts to the gaps.
                            for (int i = -2; i <= 2; i++)
                            {
                                Vector3 start = arena.Center + side * (i * 5.4f + stage * 2.7f) - forward * 8.3f;
                                Lane(start, forward, 19f, .95f);
                            }
                            break;
                    }
                    break;
                case BossKind.Orgrim:
                    switch (skill)
                    {
                        case 0: Circle(origin, 5.4f); break;
                        case 1: Circle(target + side * (stage == 0 ? -.85f : .85f), 3.0f); break;
                        case 2: Spread(3, 40, 13f, 1.2f, stage * 20); break;
                        case 3: Charge(2.1f, 10f); break;
                        default:
                            if (stage == 0) Circle(origin, 4.2f);
                            else if (stage == 1) Ring(origin, 3.8f, 8.6f);
                            else Ring(origin, 8f, 12f);
                            break;
                    }
                    break;
                case BossKind.Noctyr:
                    switch (skill)
                    {
                        case 0:
                            plan.Motion = BossMotion.Blink; plan.Destination = arena.Clamp(target - forward * 2.9f, 3.1f);
                            plan.Zones.Add(BossZone.Circle(plan.Destination, 3.65f)); break;
                        case 1: Ring(target, 1.8f, 6.6f); break;
                        case 2:
                            for (int i = -1; i <= 1; i++)
                            {
                                Vector3 start = origin + side * (i * 3.1f);
                                Lane(start, Rot(BossZone.FlatDirection(target - start), stage == 0 ? 0 : i * 23), 13f, .78f);
                            }
                            break;
                        case 3: for (int i = 0; i < 4; i++) Circle(target + Rot(forward, i * 90 + 45 + 30 * stage) * 3.1f, 2.05f); break;
                        default: Fan(origin, Rot(forward, stage * 90), 7.5f, 88); Fan(origin, Rot(forward, 180 + stage * 90), 7.5f, 88); break;
                    }
                    break;
                default:
                    switch (skill)
                    {
                        case 0: Fan(origin, forward, 11f, 76); break;
                        case 1:
                            Lane(origin - side * 2.5f, Rot(forward, -13 + stage * 13), 11f, 1.5f);
                            Lane(origin + side * 2.5f, Rot(forward, 13 - stage * 13), 11f, 1.5f); break;
                        case 2:
                            Circle(target, 2.5f); Circle(target + Rot(side, stage * 40f) * 4.2f, 1.55f); break;
                        case 3: if (stage == 0) Ring(origin, 2.45f, 7.2f); else Circle(origin, 3.1f); break;
                        default: if (stage == 0) Spread(5, 18, 17f, .65f); else Circle(target, 3.6f); break;
                    }
                    break;
            }
            return plan;
        }
    }
}
