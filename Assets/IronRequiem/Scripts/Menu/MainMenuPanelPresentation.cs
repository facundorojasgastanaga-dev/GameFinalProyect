using UnityEngine;
namespace IronRequiem
{
    // Main-menu-only presentation. Settings, focus and panel navigation remain owned by RequiemMenuView.
    public sealed class MainMenuPanelPresentation:MonoBehaviour
    {
        [SerializeField] GameObject home;
        [SerializeField] UnityEngine.UI.Graphic[] branding=new UnityEngine.UI.Graphic[0];
        public void Configure(GameObject homePanel,UnityEngine.UI.Graphic[] graphics){home=homePanel;branding=graphics;}
        void LateUpdate(){bool visible=home&&home.activeInHierarchy;foreach(var graphic in branding)if(graphic)graphic.enabled=visible;}
    }
}
