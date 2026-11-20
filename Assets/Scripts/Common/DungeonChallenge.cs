using UnityEngine;

namespace LightSouls.Common
{
    /// <summary>Shared UI/combat description; challenge scales by region and by the run, not by framerate.</summary>
    public readonly struct DungeonChallenge
    {
        public readonly string Tier, Traits;
        public readonly float EnemyHealth, EnemyDamage, EnemyRecovery, SpecialChance;
        public readonly Color Accent;
        public DungeonChallenge(string tier, string traits, float hp, float damage, float recovery, float specialChance, Color accent)
        { Tier=tier; Traits=traits; EnemyHealth=hp; EnemyDamage=damage; EnemyRecovery=recovery; SpecialChance=specialChance; Accent=accent; }

        public static DungeonChallenge For(string dungeon) => dungeon switch
        {
            "dungeon.lava" => new DungeonChallenge("II · KHẮC NGHIỆT", "Dung nham • Vật cản • Hỏa ấn", 1.36f, 1.20f, .90f, .55f, new Color32(229,124,64,255)),
            "dungeon.maze" => new DungeonChallenge("III · THỬ THÁCH", "Ngập nước • Bẫy • Mê cung", 1.49f, 1.28f, .85f, .67f, new Color32(115,204,199,255)),
            _ => new DungeonChallenge("I · NGUY HIỂM", "Cầu đá • Di tích • Đấu trường", 1.22f, 1.12f, .95f, .43f, new Color32(230,190,112,255))
        };

        public float Progression(int entered, int graphDepth, int roomsCleared) => 1f + Mathf.Min(.55f,
            Mathf.Max(0,entered-1)*.10f + Mathf.Clamp(graphDepth,0,8)*.0275f + Mathf.Clamp(roomsCleared,0,15)*.012f);
    }
}
