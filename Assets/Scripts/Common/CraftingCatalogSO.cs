using System;
using System.Collections.Generic;
using UnityEngine;

namespace LightSouls.Common
{
    public enum CraftRecipeKind { Forge, Upgrade, Alchemy, Refine }
    public enum CraftResult { Success, MissingIngredients, NoSpace, InvalidRecipe }

    [Serializable]
    public struct CraftIngredient
    {
        public ItemDefinitionSO Item;
        [Min(1)] public int Count;
        public CraftIngredient(ItemDefinitionSO item,int count){Item=item;Count=count;}
    }

    [Serializable]
    public sealed class CraftRecipe
    {
        public string Id;
        public CraftRecipeKind Kind;
        public ItemDefinitionSO Output;
        [Min(1)] public int OutputCount=1;
        [TextArea] public string Note;
        public List<CraftIngredient> Ingredients=new List<CraftIngredient>();

        public string KindLabel=>Kind switch
        {
            CraftRecipeKind.Upgrade=>"NÂNG CẤP",
            CraftRecipeKind.Alchemy=>"GIẢ KIM",
            CraftRecipeKind.Refine=>"TINH LUYỆN",
            _=>"CHẾ TẠO"
        };

        public bool IsValid=>!string.IsNullOrEmpty(Id)&&Output!=null&&!string.IsNullOrEmpty(Output.ItemId)&&OutputCount>0&&
            Ingredients!=null&&Ingredients.Count>0&&Ingredients.TrueForAll(i=>i.Item!=null&&i.Count>0&&!string.IsNullOrEmpty(i.Item.ItemId)&&i.Item.ItemId!=Output.ItemId);
    }

    /// <summary>The same authored catalogue drives recipes, material sources, and dungeon rewards.</summary>
    [CreateAssetMenu(fileName="CraftingCatalog",menuName="LightSouls/Crafting/Catalog")]
    public sealed class CraftingCatalogSO : ScriptableObject
    {
        public const string ResourcePath="Combat/Crafting/CraftingCatalog";
        [SerializeField] private List<CraftMaterialSO> materials=new List<CraftMaterialSO>();
        [SerializeField] private List<CraftRecipe> recipes=new List<CraftRecipe>();
        public IReadOnlyList<CraftMaterialSO> Materials=>materials;
        public IReadOnlyList<CraftRecipe> Recipes=>recipes;
        public static CraftingCatalogSO Load()=>Resources.Load<CraftingCatalogSO>(ResourcePath);
        public CraftRecipe FindRecipe(string id)=>recipes.Find(r=>r!=null&&r.Id==id);
        public CraftMaterialSO FindMaterial(string id)=>materials.Find(m=>m!=null&&m.ItemId==id);
    }

    /// <summary>Lobby asks the inventory for a transactional craft without knowing combat internals.</summary>
    public interface ICraftingInventory : IInventory
    {
        int GetCraftableCount(ItemDefinitionSO item);
        CraftResult EvaluateCraft(CraftRecipe recipe,int batches=1);
        CraftResult TryCraft(CraftRecipe recipe,int batches=1);
    }
}
