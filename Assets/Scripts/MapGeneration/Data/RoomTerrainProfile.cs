using UnityEngine;

namespace LightSouls.MapGeneration
{
    public enum DungeonTerrainRegion { AncientRuins, VolcanicDepths, DrownedLabyrinth }

    /// <summary>Authored terrain identity. Topology and doorway contracts remain on RoomModule.</summary>
    [DisallowMultipleComponent]
    public sealed class RoomTerrainProfile : MonoBehaviour
    {
        [SerializeField] private DungeonTerrainRegion region;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string layoutNote;
        [SerializeField] private bool hiddenTreasury;
        [SerializeField] private bool authoredLayout;

        public DungeonTerrainRegion Region => region;
        public string DisplayName => displayName;
        public string LayoutNote => layoutNote;
        public bool HiddenTreasury => hiddenTreasury;
        public bool AuthoredLayout => authoredLayout;

        public void Configure(DungeonTerrainRegion area, string label, string note, bool treasure, bool handcrafted)
        {
            region = area; displayName = label; layoutNote = note;
            hiddenTreasury = treasure; authoredLayout = handcrafted;
        }
    }
}
