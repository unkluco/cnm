using UnityEngine;

namespace LightSouls.Common
{
    [CreateAssetMenu(fileName="Material_",menuName="LightSouls/Crafting/Material")]
    public sealed class CraftMaterialSO : ItemDefinitionSO
    {
        [SerializeField,TextArea] private string sourceHint;
        [SerializeField,Min(0)] private float dropWeight=5f;
        [SerializeField,Min(1)] private int minDrop=1,maxDrop=3;
        [SerializeField] private string favoredDungeonId;
        [SerializeField,Min(1)] private float favoredMultiplier=2.5f;
        [SerializeField] private bool bossOnly;
        [SerializeField] private bool soldAtShop;

        public override ItemCategory Category=>ItemCategory.Material;
        public string SourceHint=>sourceHint;
        public float DropWeight=>dropWeight;
        public int MinDrop=>minDrop;
        public int MaxDrop=>Mathf.Max(minDrop,maxDrop);
        public string FavoredDungeonId=>favoredDungeonId;
        public float FavoredMultiplier=>favoredMultiplier;
        public bool BossOnly=>bossOnly;
        public bool SoldAtShop=>soldAtShop;
        public float WeightFor(string dungeon)=>!string.IsNullOrEmpty(favoredDungeonId)&&favoredDungeonId==dungeon?dropWeight*favoredMultiplier:dropWeight;
        public override string GetStatsText()=>"Nguyên liệu chế tạo\nNguồn: "+sourceHint;
    }
}
