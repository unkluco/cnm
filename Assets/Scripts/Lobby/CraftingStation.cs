using LightSouls.Common;
using UnityEngine;

namespace LightSouls.Lobby
{
    [RequireComponent(typeof(Collider))]
    public sealed class CraftingStation : MonoBehaviour,IInteractable
    {
        [SerializeField] private CraftingCatalogSO catalog;
        [SerializeField] private ParticleSystem craftSparks;
        public string Prompt=>"Chế tạo";
        public CraftingCatalogSO Catalog=>catalog!=null?catalog:CraftingCatalogSO.Load();

        private void Awake()
        {
            gameObject.layer=12;var col=GetComponent<Collider>();if(col!=null)col.isTrigger=true;
        }
        public bool CanInteract(GameObject interactor)=>interactor!=null&&interactor.CompareTag("Player")&&
            interactor.GetComponent<ICraftingInventory>()!=null&&Catalog!=null;

        public void Interact(GameObject interactor)
        {
            if(!CanInteract(interactor)||BackButtonStack.HasOpenPanel)return;
            var ui=FindAnyObjectByType<CraftingPanelUI>(FindObjectsInactive.Include);
            if(ui==null)ui=CraftingPanelUI.Create();
            ui.Open(this,interactor.GetComponent<ICraftingInventory>());
        }

        public void Celebrate(){if(craftSparks!=null)craftSparks.Emit(24);}
    }
}
