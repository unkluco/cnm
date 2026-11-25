using System;
using System.Collections.Generic;
using System.Text;
using LightSouls.Combat.Weapons;
using UnityEngine;

namespace LightSouls.Combat.Runes
{
    public enum RuneForm { Weapon, Sector, Nova, Lane, TargetBurst, Blink, StoneWall, IceWall, Ward, Focus, Dash }
    public enum RuneEffect { None, Push, Pull, Freeze, Slow, Stun, Burn, Leech, Charm, Chain, Expose }

    /// <summary>One actual action per glyph. No shared mutable WeaponData asset is modified at cast time.</summary>
    public sealed class RuneSkill
    {
        public readonly RuneGlyph Glyph;
        public readonly string Name;
        public readonly RuneForm Form;
        public readonly RuneEffect Effect;
        public readonly float Cooldown, DamageScale, RangeScale, Area, Arc, Spread, Power, Duration;
        public readonly int ExtraHits, PierceBonus;
        public readonly bool Basic;

        public RuneSkill(RuneGlyph glyph, string name, RuneForm form, float cooldown, float damage,
            float range=1f,float area=0f,float arc=0f,int extra=0,float spread=0f,int pierce=0,
            RuneEffect effect=RuneEffect.None,float power=0f,float duration=0f,bool basic=false)
        {
            Glyph=glyph;Name=name;Form=form;Cooldown=cooldown;DamageScale=damage;RangeScale=range;
            Area=area;Arc=arc;ExtraHits=extra;Spread=spread;PierceBonus=pierce;
            Effect=effect;Power=power;Duration=duration;Basic=basic;
        }

        public float BaseCooldown(WeaponDataSO weapon) => Basic ? Mathf.Max(Cooldown, weapon != null ? weapon.AttackRate : .65f) : Cooldown;
        public float Reach(WeaponDataSO weapon)
        {
            if(weapon==null)return 0;
            return Form==RuneForm.Nova?Area:Form==RuneForm.Blink?Area:Form==RuneForm.StoneWall||Form==RuneForm.IceWall?Area+1f:
                Form==RuneForm.Ward||Form==RuneForm.Focus||Form==RuneForm.Dash?0f:Mathf.Max(weapon.AttackRange*RangeScale,.6f);
        }
        public string Description(WeaponDataSO weapon)
        {
            if(Form==RuneForm.Dash)return "Lướt theo joystick hoặc hướng nhìn · hồi lướt riêng (cơ bản 1.85s)";
            string action=Form switch{
                RuneForm.Weapon=>weapon.Type==WeaponType.Melee?"Vung vũ khí":"Phóng đạn",
                RuneForm.Sector=>"Quét hình quạt", RuneForm.Nova=>"Sóng xung quanh bản thân",
                RuneForm.Lane=>"Đòn thẳng xuyên vùng", RuneForm.TargetBurst=>"Bùng nổ tại mục tiêu",
                RuneForm.Blink=>"Dịch chuyển an toàn tối đa "+Area.ToString("0.#")+"m",
                RuneForm.StoneWall=>"Vách đá tạm "+Duration.ToString("0.#")+"s",
                RuneForm.IceWall=>"Vách băng tạm "+Duration.ToString("0.#")+"s",
                RuneForm.Ward=>"Lá chắn giảm sát thương",
                _=>"Tập trung: tăng sát thương và tốc hồi đòn"
            };
            if(DamageScale>0)action+=" · "+Mathf.RoundToInt(DamageScale*100)+"% lực"+(ExtraHits>0?" · +"+ExtraHits+" nhát/tia":"");
            if(PierceBonus>0)action+=" · xuyên +"+PierceBonus;
            string fx=Effect switch{
                RuneEffect.Push=>"Đẩy",RuneEffect.Pull=>"Hút",RuneEffect.Freeze=>"Đóng băng",
                RuneEffect.Slow=>"Làm chậm",RuneEffect.Stun=>"Choáng",RuneEffect.Burn=>"Thiêu đốt",
                RuneEffect.Leech=>"Hút máu",RuneEffect.Charm=>"Thôi miên",RuneEffect.Chain=>"Sét lan",
                RuneEffect.Expose=>"Phá giáp",_=>string.Empty};
            if(fx.Length>0)action+=" · "+fx;
            return action+" · Hồi "+BaseCooldown(weapon).ToString("0.#")+"s";
        }
    }

    /// <summary>Four authored weapon actions plus one consistent, independently cooled dodge rune.</summary>
    public static class RuneSkillCatalog
    {
        public const int UniqueSkillsPerWeapon=4,SkillsPerWeapon=5;
        public static readonly RuneSkill DashSkill=new RuneSkill(RuneGlyph.Diagonal,"Lướt Né",RuneForm.Dash,1.85f,0f);
        public static readonly IReadOnlyList<RuneSkill> DashOnly=Array.AsReadOnly(new[]{DashSkill});
        private static readonly IReadOnlyList<RuneSkill> Empty = Array.Empty<RuneSkill>();
        private static readonly Dictionary<string,IReadOnlyList<RuneSkill>> Kits = Build();
        public static int WeaponCount=>Kits.Count;
        public static int SkillCount=>Kits.Count*SkillsPerWeapon;
        public static int AuthoredWeaponSkillCount=>Kits.Count*UniqueSkillsPerWeapon;
        public static IReadOnlyDictionary<string,IReadOnlyList<RuneSkill>> All=>Kits;
        public static IReadOnlyList<RuneSkill> For(WeaponDataSO weapon) => weapon!=null&&Kits.TryGetValue(weapon.ItemId,out var kit)?kit:Empty;
        public static RuneSkill Find(WeaponDataSO weapon,RuneGlyph glyph)
        {foreach(var skill in For(weapon))if(skill.Glyph==glyph)return skill;return null;}

        private static RuneSkill B(RuneGlyph g,string n,int extra=0,float damage=1,float cd=0)=>
            new RuneSkill(g,n,RuneForm.Weapon,cd,damage,extra:extra,basic:true);
        private static RuneSkill W(RuneGlyph g,string n,float cd,float dmg,RuneEffect e=RuneEffect.None,float p=0,float d=0,
            float range=1,int extra=0,float arc=0,float spread=0,int pierce=0)=>
            new RuneSkill(g,n,RuneForm.Weapon,cd,dmg,range,arc:arc,extra:extra,spread:spread,pierce:pierce,effect:e,power:p,duration:d);
        private static RuneSkill S(RuneGlyph g,string n,float cd,float dmg,float range,float arc,RuneEffect e=RuneEffect.None,float p=0,float d=0)=>
            new RuneSkill(g,n,RuneForm.Sector,cd,dmg,range,arc:arc,effect:e,power:p,duration:d);
        private static RuneSkill N(RuneGlyph g,string n,float cd,float dmg,float radius,RuneEffect e=RuneEffect.None,float p=0,float d=0)=>
            new RuneSkill(g,n,RuneForm.Nova,cd,dmg,area:radius,effect:e,power:p,duration:d);
        private static RuneSkill L(RuneGlyph g,string n,float cd,float dmg,float range,float width,RuneEffect e=RuneEffect.None,float p=0,float d=0)=>
            new RuneSkill(g,n,RuneForm.Lane,cd,dmg,range,area:width,effect:e,power:p,duration:d);
        private static RuneSkill T(RuneGlyph g,string n,float cd,float dmg,float radius,RuneEffect e=RuneEffect.None,float p=0,float d=0,float range=1)=>
            new RuneSkill(g,n,RuneForm.TargetBurst,cd,dmg,range,area:radius,effect:e,power:p,duration:d);
        private static RuneSkill C(RuneGlyph g,string n,RuneForm form,float cd,float area,float duration,float damage=0)=>
            new RuneSkill(g,n,form,cd,damage,area:area,duration:duration);

        private static Dictionary<string,IReadOnlyList<RuneSkill>> Build()
        {
            var sets=new Dictionary<string,IReadOnlyList<RuneSkill>>(StringComparer.Ordinal);
            void Add(string id,params RuneSkill[] skills)
            {
                if(skills.Length!=UniqueSkillsPerWeapon)throw new ArgumentException("Four weapon skills required: "+id);
                var seen=new HashSet<RuneGlyph>{DashSkill.Glyph};foreach(var skill in skills)if(!seen.Add(skill.Glyph))throw new ArgumentException("Duplicate or reserved dodge glyph: "+id);
                var full=new RuneSkill[SkillsPerWeapon];Array.Copy(skills,full,skills.Length);full[SkillsPerWeapon-1]=DashSkill;
                sets.Add("weapon."+id,Array.AsReadOnly(full));
            }
            // Original arsenal.
            Add("iron_sword",B(RuneGlyph.Slash,"Thiết Trảm"),
                W(RuneGlyph.Cleave,"Phá Giáp",3.5f,1.35f,RuneEffect.Expose,1.13f,3f),
                S(RuneGlyph.Crescent,"Nguyệt Trảm",4.2f,.95f,1.25f,165f),
                N(RuneGlyph.Ring,"Kiếm Phong Hộ Thân",5.8f,.85f,2.5f,RuneEffect.Push,1.4f));
            Add("greatsword",B(RuneGlyph.Slash,"Đại Trảm"),
                W(RuneGlyph.Cleave,"Sơn Băng",5.2f,1.55f,RuneEffect.Stun,0,.75f),
                N(RuneGlyph.Ring,"Chấn Địa",6.4f,.85f,3.2f,RuneEffect.Push,1.8f),
                L(RuneGlyph.Wave,"Đại Kiếm Phong",5.5f,1.1f,2.0f,.76f));
            Add("dagger",B(RuneGlyph.Slash,"Tốc Nhận"),
                W(RuneGlyph.Thrust,"Đâm Kép",2.8f,.85f,extra:1,range:1.1f),
                W(RuneGlyph.Cleave,"Rạch Gân",4.2f,1.2f,RuneEffect.Slow,.60f,2.6f),
                C(RuneGlyph.Vortex,"Bước Mờ",RuneForm.Blink,8f,3.2f,0,.55f));
            Add("fire_sword",B(RuneGlyph.Slash,"Huyền Hỏa Trảm"),
                W(RuneGlyph.Flame,"Hỏa Kích",4.8f,1.12f,RuneEffect.Burn,.22f,3f),
                N(RuneGlyph.Ring,"Vòng Thiêu Đốt",6.8f,.9f,2.8f,RuneEffect.Burn,.14f,2.6f),
                L(RuneGlyph.Wave,"Sóng Lửa",5.8f,1.2f,2.0f,.72f));
            Add("spear",B(RuneGlyph.Thrust,"Xuyên Thương"),
                W(RuneGlyph.Lightning,"Đột Mũi",3.8f,1.45f,range:1.3f,arc:70),
                L(RuneGlyph.Wave,"Thương Phong",5f,1.15f,1.9f,.48f),
                S(RuneGlyph.Cleave,"Gạt Thương",4.5f,.9f,1.25f,125,RuneEffect.Push,1.9f));
            Add("battle_axe",B(RuneGlyph.Slash,"Chiến Phủ"),
                W(RuneGlyph.Cleave,"Bổ Cuồng",4.8f,1.5f,RuneEffect.Expose,1.14f,3f),
                S(RuneGlyph.Crescent,"Rìu Quét",4.3f,.95f,1.25f,190f),
                N(RuneGlyph.Ring,"Cuồng Chiến",6.5f,.88f,2.75f,RuneEffect.Stun,0,.65f));
            Add("sledgehammer",B(RuneGlyph.Slash,"Thiết Chùy"),
                W(RuneGlyph.Cleave,"Nện Nặng",5.5f,1.48f,RuneEffect.Stun,0,1f),
                N(RuneGlyph.Ring,"Địa Chấn",6.8f,.9f,3.5f,RuneEffect.Push,2.2f),
                C(RuneGlyph.Wall,"Thạch Lũy",RuneForm.StoneWall,11f,2.5f,4f));
            Add("twin_daggers",B(RuneGlyph.Slash,"Song Trảm"),
                W(RuneGlyph.Thrust,"Liên Nhận",3.5f,.74f,extra:1),
                S(RuneGlyph.Crescent,"Lốc Song Đao",4.5f,1.05f,1.45f,200f,RuneEffect.Slow,.68f,2f),
                C(RuneGlyph.Vortex,"Bóng Lướt",RuneForm.Blink,9f,3.4f,0,.65f));
            Add("scythe",B(RuneGlyph.Crescent,"Lưỡi Háo Hồn"),
                S(RuneGlyph.Slash,"Tả Nguyệt",4.2f,1.05f,1.35f,220f),
                N(RuneGlyph.Vortex,"Hồn Vực",6.9f,.8f,3.2f,RuneEffect.Pull,1.8f),
                N(RuneGlyph.Ring,"Thu Hồn",8.2f,.8f,2.8f,RuneEffect.Leech,.18f,0));
            Add("pistol",B(RuneGlyph.Thrust,"Đạn Thiết"),
                W(RuneGlyph.Lightning,"Đạn Xuyên",3.4f,1.45f,range:1.15f,pierce:1),
                W(RuneGlyph.Cleave,"Tam Phát",4.5f,.80f,extra:2,spread:20),
                T(RuneGlyph.Ring,"Đạn Chấn",5.5f,1.05f,1.7f,RuneEffect.Stun,0,.55f));
            Add("shotgun",B(RuneGlyph.Thrust,"Tán Xạ"),
                W(RuneGlyph.Cleave,"Xung Tán",4.8f,.90f,extra:2,spread:56),
                S(RuneGlyph.Wave,"Bộc Chấn",5.4f,2.55f,.75f,120f,RuneEffect.Push,2.0f),
                N(RuneGlyph.Ring,"Phản Xung",6.3f,2.1f,2.6f,RuneEffect.Push,2.3f));
            Add("smg",B(RuneGlyph.Thrust,"Liên Thanh",4,.9f,.78f),
                W(RuneGlyph.Wave,"Mưa Đạn",3.9f,.75f,extra:7,spread:30),
                L(RuneGlyph.Lightning,"Đường Đạn Xuyên",4.7f,4.1f,1.05f,.52f),
                C(RuneGlyph.Ward,"Tập Trung Hỏa Lực",RuneForm.Focus,10f,0,4.2f));
            Add("crossbow",B(RuneGlyph.Thrust,"Nỏ Tiễn"),
                W(RuneGlyph.Lightning,"Xuyên Tiễn",4.4f,1.3f,range:1.15f,pierce:2),
                W(RuneGlyph.Cleave,"Tam Tiễn",5f,.7f,extra:2,spread:27),
                T(RuneGlyph.Vortex,"Lưới Trói",6.8f,.8f,2.5f,RuneEffect.Slow,.5f,3f));
            Add("ice_staff",B(RuneGlyph.Thrust,"Băng Tiễn"),
                T(RuneGlyph.Ring,"Hàn Vực",6.3f,.9f,2.5f,RuneEffect.Freeze,0,1.75f),
                L(RuneGlyph.Wave,"Sương Tuyến",4.8f,1.1f,1.05f,.8f,RuneEffect.Slow,.55f,3f),
                C(RuneGlyph.Wall,"Băng Bích",RuneForm.IceWall,11f,2.3f,4f));
            Add("grenade_launcher",B(RuneGlyph.Thrust,"Pháo Cầu"),
                W(RuneGlyph.Cleave,"Pháo Kép",6.5f,.67f,extra:1,spread:24),
                T(RuneGlyph.Ring,"Bộc Phá",7.5f,1.25f,3.25f,RuneEffect.Push,1.6f),
                L(RuneGlyph.Wave,"Mảnh Vỡ",6.2f,.95f,1f,1.1f,RuneEffect.Burn,.13f,2.5f));

            // Expanded 30 weapons. Signature actions are explicit, not passive auto-casts.
            Add("arsenal_arcane_bow",B(RuneGlyph.Thrust,"Linh Tiễn"),
                W(RuneGlyph.Lightning,"Lôi Liên Tiễn",5f,1.1f,RuneEffect.Chain,.50f),
                W(RuneGlyph.Cleave,"Xuyên Linh Văn",4.2f,1.38f,range:1.2f,pierce:2),
                L(RuneGlyph.Wave,"Thiên Lôi Tuyến",6.7f,1.15f,1.1f,.76f,RuneEffect.Expose,1.12f,2.8f));
            Add("arsenal_bastion_hammer",B(RuneGlyph.Slash,"Thành Lũy Chùy"),
                W(RuneGlyph.Cleave,"Trấn Sơn",5.2f,1.5f,RuneEffect.Stun,0,.95f),
                N(RuneGlyph.Ring,"Phá Vòng Vây",6.8f,.95f,3.35f,RuneEffect.Push,2.7f),
                C(RuneGlyph.Wall,"Khắc Lũy",RuneForm.StoneWall,12f,2.6f,4.3f));
            Add("arsenal_blood_fang",B(RuneGlyph.Slash,"Huyết Nha"),
                W(RuneGlyph.Flame,"Huyết Khát",5f,1.35f,RuneEffect.Leech,.28f),
                S(RuneGlyph.Crescent,"Huyết Nguyệt",4.8f,1.2f,1.36f,175f,RuneEffect.Expose,1.12f,3f),
                N(RuneGlyph.Vortex,"Hút Hồn",7.4f,.92f,2.7f,RuneEffect.Pull,1.75f));
            Add("arsenal_cinder_cleaver",B(RuneGlyph.Slash,"Than Hồng Trảm"),
                W(RuneGlyph.Flame,"Hồng Liệt",4.8f,1.2f,RuneEffect.Burn,.24f,3.2f),
                S(RuneGlyph.Crescent,"Tro Tàn Quét",5f,.95f,1.3f,210),
                N(RuneGlyph.Ring,"Lò Lửa Tàn",7.1f,.90f,3.15f,RuneEffect.Burn,.18f,3f));
            Add("arsenal_dawn_blade",B(RuneGlyph.Slash,"Bình Minh Trảm"),
                C(RuneGlyph.Ward,"Hào Quang Hộ Thể",RuneForm.Ward,9.5f,0,3.5f),
                L(RuneGlyph.Wave,"Dải Nhật Quang",5.8f,1.15f,2f,.72f),
                W(RuneGlyph.Cleave,"Trảm Tà",4.6f,1.35f,RuneEffect.Expose,1.18f,3f));
            Add("arsenal_dream_scepter",B(RuneGlyph.Thrust,"Mộng Tiễn"),
                T(RuneGlyph.Vortex,"Mộng Lệnh",10f,.7f,1.8f,RuneEffect.Charm,.46f,7.2f),
                T(RuneGlyph.Ring,"Mộng Vực",6.3f,.82f,2.65f,RuneEffect.Slow,.54f,3.5f),
                C(RuneGlyph.Ward,"Tâm Ấn",RuneForm.Focus,10f,0,4.4f));
            Add("arsenal_earth_totem",B(RuneGlyph.Thrust,"Địa Thạch Tiễn"),
                C(RuneGlyph.Wall,"Địa Mạch Thành",RuneForm.StoneWall,11.5f,2.75f,4.6f),
                T(RuneGlyph.Ring,"Thạch Trấn",6.4f,1.05f,2.8f,RuneEffect.Push,2.1f),
                T(RuneGlyph.Cleave,"Thạch Chùy",5.1f,1.25f,1.9f,RuneEffect.Stun,0,.75f));
            Add("arsenal_eclipse_scythe",B(RuneGlyph.Crescent,"Nhật Thực Nhận"),
                N(RuneGlyph.Vortex,"Nhật Thực Vực",7.2f,.85f,3.5f,RuneEffect.Pull,2.4f),
                N(RuneGlyph.Ring,"Đoạt Nhật",8.8f,.82f,3.1f,RuneEffect.Leech,.19f),
                L(RuneGlyph.Wave,"Bóng Nguyệt Tuyến",6.2f,1.22f,1.7f,.85f));
            Add("arsenal_ember_orb",B(RuneGlyph.Thrust,"Sao Lửa"),
                T(RuneGlyph.Flame,"Thiên Hỏa",6.8f,1.15f,2.45f,RuneEffect.Burn,.21f,3f),
                T(RuneGlyph.Ring,"Sao Vỡ",8.2f,1.25f,3.6f,RuneEffect.Push,1.6f),
                L(RuneGlyph.Wave,"Thiêu Thiên Tuyến",6.6f,1.05f,1f,.95f,RuneEffect.Burn,.12f,2.7f));
            Add("arsenal_frost_halberd",B(RuneGlyph.Thrust,"Hàn Kích"),
                C(RuneGlyph.Wall,"Bích Hàn",RuneForm.IceWall,11f,2.7f,4.6f),
                W(RuneGlyph.Cleave,"Phong Băng Kích",6f,1.15f,RuneEffect.Freeze,0,1.8f,range:1.16f),
                S(RuneGlyph.Crescent,"Quét Sương",5.4f,.95f,1.26f,210f,RuneEffect.Slow,.53f,2.8f));
            Add("arsenal_frost_scepter",B(RuneGlyph.Thrust,"Hàn Ấn Tiễn"),
                T(RuneGlyph.Ring,"Hàn Ấn Vực",6.5f,1f,2.75f,RuneEffect.Freeze,0,2f),
                L(RuneGlyph.Wave,"Hàn Sương Tuyến",5.3f,1.1f,1.05f,.76f,RuneEffect.Slow,.48f,3f),
                C(RuneGlyph.Ward,"Băng Giáp",RuneForm.Ward,10f,0,3.4f));
            Add("arsenal_glacier_greatsword",B(RuneGlyph.Slash,"Băng Hà Trảm"),
                W(RuneGlyph.Cleave,"Băng Hà Phá",5.9f,1.44f,RuneEffect.Freeze,0,1.65f),
                N(RuneGlyph.Ring,"Bạch Băng Bạo",7.5f,.95f,3.45f,RuneEffect.Slow,.45f,3.1f),
                L(RuneGlyph.Wave,"Hàn Giang Tuyến",6.4f,1.18f,1.8f,.88f));
            Add("arsenal_gravestone_crossbow",B(RuneGlyph.Thrust,"Mộ Thạch Tiễn"),
                W(RuneGlyph.Lightning,"Thạch Xuyên",4.6f,1.35f,range:1.12f,pierce:2),
                W(RuneGlyph.Cleave,"Cốt Tiễn Kép",5.5f,.89f,RuneEffect.Expose,1.16f,3f,extra:1,spread:16),
                T(RuneGlyph.Ring,"Mộ Chấn",6.9f,1.0f,2.7f,RuneEffect.Stun,0,.9f));
            Add("arsenal_gravity_orb",B(RuneGlyph.Thrust,"Hư Trọng Cầu"),
                T(RuneGlyph.Vortex,"Hố Trọng Lực",7.3f,.9f,3.6f,RuneEffect.Pull,2.5f),
                T(RuneGlyph.Ring,"Phản Trọng",6.4f,1.1f,3.0f,RuneEffect.Push,2.5f),
                L(RuneGlyph.Wave,"Hư Không Tuyến",5.7f,1.13f,1.05f,.95f,RuneEffect.Slow,.62f,2.8f));
            Add("arsenal_gravity_spear",B(RuneGlyph.Thrust,"Trọng Thương"),
                S(RuneGlyph.Vortex,"Trọng Hút",6.5f,.95f,1.5f,155f,RuneEffect.Pull,2.2f),
                W(RuneGlyph.Cleave,"Phản Trọng Kích",4.8f,1.35f,RuneEffect.Push,2.2f,range:1.16f),
                L(RuneGlyph.Wave,"Thiên Trọng Tuyến",5.9f,1.2f,1.8f,.67f));
            Add("arsenal_guardian_maul",B(RuneGlyph.Slash,"Hộ Thành Chùy"),
                C(RuneGlyph.Wall,"Bức Thành Hộ Linh",RuneForm.StoneWall,11.8f,2.8f,4.5f),
                N(RuneGlyph.Ring,"Hộ Thành Chấn",6.7f,.98f,3.15f,RuneEffect.Push,2.4f),
                C(RuneGlyph.Ward,"Thủ Vệ Ấn",RuneForm.Ward,10.2f,0,3.8f));
            Add("arsenal_ice_totem",B(RuneGlyph.Thrust,"Vĩnh Băng Tiễn"),
                C(RuneGlyph.Wall,"Vĩnh Băng Thành",RuneForm.IceWall,11.5f,2.7f,5f),
                T(RuneGlyph.Ring,"Tuyết Linh Vực",6.9f,.9f,3.0f,RuneEffect.Freeze,0,2.1f),
                C(RuneGlyph.Ward,"Hàn Linh Giáp",RuneForm.Ward,10f,0,3.8f));
            Add("arsenal_mirage_sabre",B(RuneGlyph.Slash,"Ảo Trảm"),
                C(RuneGlyph.Vortex,"Bước Ảnh",RuneForm.Blink,8.5f,4.1f,0,.9f),
                S(RuneGlyph.Crescent,"Nguyệt Ảnh",4.4f,1.14f,1.38f,190f),
                C(RuneGlyph.Ward,"Ảo Tâm",RuneForm.Focus,9.5f,0,4f));
            Add("arsenal_obsidian_axe",B(RuneGlyph.Slash,"Hắc Diệm Phủ"),
                W(RuneGlyph.Flame,"Hắc Hỏa Bổ",5.4f,1.38f,RuneEffect.Burn,.21f,3.2f),
                W(RuneGlyph.Cleave,"Phá Hắc Giáp",4.9f,1.2f,RuneEffect.Expose,1.2f,3f),
                N(RuneGlyph.Ring,"Hắc Diệm Vực",7.2f,.94f,3.2f,RuneEffect.Burn,.16f,3f));
            Add("arsenal_prism_crossbow",B(RuneGlyph.Thrust,"Lăng Kính Tiễn"),
                W(RuneGlyph.Wave,"Khúc Xạ Xuyên",4.6f,1.28f,range:1.22f,pierce:3),
                W(RuneGlyph.Cleave,"Phân Quang",5.4f,.72f,extra:2,spread:29),
                W(RuneGlyph.Lightning,"Quang Phân Nhánh",6.2f,1.08f,RuneEffect.Chain,.47f));
            Add("arsenal_requiem_rapier",B(RuneGlyph.Thrust,"An Hồn Thích"),
                W(RuneGlyph.Cleave,"Song Thích",3.7f,.88f,range:1.1f,extra:1),
                S(RuneGlyph.Crescent,"Khúc An Hồn",6.5f,1.13f,1.25f,160f,RuneEffect.Leech,.20f),
                C(RuneGlyph.Vortex,"Mộ Ảnh",RuneForm.Blink,8.8f,3.8f,0,.7f));
            Add("arsenal_rime_dagger",B(RuneGlyph.Slash,"Sương Nhận"),
                W(RuneGlyph.Cleave,"Sương Trắng Phong",5.5f,1.25f,RuneEffect.Freeze,0,1.55f),
                W(RuneGlyph.Thrust,"Song Sương Thích",3.3f,.84f,extra:1,range:1.1f),
                N(RuneGlyph.Ring,"Sương Bạo",6.2f,1.14f,2.4f,RuneEffect.Slow,.5f,2.7f));
            Add("arsenal_siren_lantern",B(RuneGlyph.Thrust,"Mê Hồn Quang"),
                T(RuneGlyph.Vortex,"Mê Hồn Ca",10.5f,.67f,2.1f,RuneEffect.Charm,.42f,6.5f),
                T(RuneGlyph.Ring,"U Quang Trói",6.4f,.95f,3f,RuneEffect.Pull,1.65f),
                C(RuneGlyph.Ward,"Linh Đăng Hộ",RuneForm.Focus,10f,0,4.5f));
            Add("arsenal_solar_wand",B(RuneGlyph.Thrust,"Nhật Quang"),
                C(RuneGlyph.Ward,"Nhật Quang Giáp",RuneForm.Ward,9.8f,0,3.5f),
                L(RuneGlyph.Lightning,"Nhật Quang Thiên Tuyến",5.6f,1.25f,1.05f,.65f,RuneEffect.Chain,.40f),
                T(RuneGlyph.Ring,"Nhật Luân",6.4f,1.15f,2.6f,RuneEffect.Expose,1.16f,3f));
            Add("arsenal_tempest_mace",B(RuneGlyph.Slash,"Bão Tố Chùy"),
                W(RuneGlyph.Lightning,"Lôi Chùy",5.4f,1.25f,RuneEffect.Chain,.50f),
                N(RuneGlyph.Ring,"Bão Xung",6.3f,1f,3.1f,RuneEffect.Push,2f),
                L(RuneGlyph.Wave,"Bão Tuyến",5.6f,1.18f,1.85f,.9f));
            Add("arsenal_thorn_sword",B(RuneGlyph.Slash,"Linh Mộc Trảm"),
                W(RuneGlyph.Cleave,"Gai Phá Giáp",4.8f,1.28f,RuneEffect.Expose,1.20f,3.2f),
                S(RuneGlyph.Crescent,"Gai Trói",5.2f,1.05f,1.3f,195f,RuneEffect.Slow,.52f,3f),
                N(RuneGlyph.Ring,"Linh Mộc Hút Sinh",7.6f,.90f,2.8f,RuneEffect.Leech,.20f));
            Add("arsenal_thunder_halberd",B(RuneGlyph.Thrust,"Lôi Đình Kích"),
                W(RuneGlyph.Lightning,"Lôi Đình Phá",5.3f,1.25f,RuneEffect.Chain,.52f),
                S(RuneGlyph.Crescent,"Lôi Quét",5.1f,1.03f,1.3f,205f,RuneEffect.Stun,0,.55f),
                L(RuneGlyph.Wave,"Lôi Mạch",6.7f,1.2f,1.9f,.78f));
            Add("arsenal_twin_shade",B(RuneGlyph.Slash,"Song Ảnh Nhận"),
                C(RuneGlyph.Vortex,"Ảnh Giới",RuneForm.Blink,8.5f,4.1f,0,.78f),
                W(RuneGlyph.Cleave,"Tam Ảnh Trảm",4.1f,.82f,extra:1),
                N(RuneGlyph.Ring,"Bóng Ma Đoạt Hồn",7.8f,1.25f,2.6f,RuneEffect.Leech,.20f));
            Add("arsenal_warden_flail",B(RuneGlyph.Crescent,"Cai Ngục Liên"),
                S(RuneGlyph.Vortex,"Liên Chùy Kéo",6.8f,.94f,1.4f,160f,RuneEffect.Pull,2.5f),
                W(RuneGlyph.Cleave,"Cai Ngục Nện",5.2f,1.45f,RuneEffect.Stun,0,.86f),
                N(RuneGlyph.Ring,"Liên Chùy Trận",6.9f,1f,3.3f,RuneEffect.Push,2.15f));
            Add("arsenal_wind_fan",B(RuneGlyph.Wave,"Phiến Vũ"),
                W(RuneGlyph.Thrust,"Phong Tiễn Tập",4.2f,1.08f,extra:2,spread:20),
                N(RuneGlyph.Ring,"Cuồng Phong Xung",6.5f,2.2f,3.1f,RuneEffect.Push,2.7f),
                T(RuneGlyph.Vortex,"Cuồng Phong Vực",7.4f,2.05f,3.4f,RuneEffect.Pull,2.3f));
            return sets;
        }
    }
}
