using UnityEngine;
namespace IronRequiem
{
    // Main-menu-only presentation. Settings, focus and panel navigation remain owned by RequiemMenuView.
    public sealed class MainMenuPanelPresentation:MonoBehaviour
    {
        [SerializeField] GameObject home;
        [SerializeField] UnityEngine.UI.Graphic[] branding=new UnityEngine.UI.Graphic[0];
        Transform credits;bool searched;
        public void Configure(GameObject homePanel,UnityEngine.UI.Graphic[] graphics){home=homePanel;branding=graphics;}
        // 20/11: the credits screen lives inside the same frame as the home buttons, so the frame and the title stay
        // visible there too (it used to float over the hangar with no panel behind it). Options still hides them.
        void LateUpdate()
        {
            if(!searched&&home){searched=true;credits=home.transform.parent?home.transform.parent.Find("CreditsPanel"):null;}
            bool visible=(home&&home.activeInHierarchy)||(credits&&credits.gameObject.activeInHierarchy);
            foreach(var graphic in branding)if(graphic)graphic.enabled=visible;
        }
    }
}
