using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEngine;
namespace IronRequiem
{
    public sealed class WeaponHeatHud : MonoBehaviour
    {
        [SerializeField] PlayerWeaponsManager inventory;
        [SerializeField] GameObject content;
        [SerializeField] ThermalGaugeGraphic gauge;
        [SerializeField] TMP_Text percentage, state;
        [SerializeField] Vector2 wideOffset = new Vector2(-12,70);
        [SerializeField] Vector2 narrowOffset = new Vector2(-12,230);
        Canvas ownerCanvas;
        RectTransform layoutRoot;
        WeaponHeat heat;
        int displayed=-1;
        string lastState;
        public WeaponHeat BoundHeat=>heat;
        public void Configure(PlayerWeaponsManager manager,GameObject root,ThermalGaugeGraphic graphic,TMP_Text percent,TMP_Text label)
        {inventory=manager;content=root;gauge=graphic;percentage=percent;state=label;}
        void Start(){if(!inventory)inventory=FindAnyObjectByType<PlayerWeaponsManager>();if(inventory){inventory.OnSwitchedToWeapon+=Bind;Bind(inventory.GetActiveWeapon());}}
        void OnEnable(){ownerCanvas=GetComponentInParent<Canvas>();layoutRoot=(RectTransform)transform;Canvas.willRenderCanvases+=UpdateLayout;}
        void OnDisable(){Canvas.willRenderCanvases-=UpdateLayout;}
        void UpdateLayout(){if(!ownerCanvas||!layoutRoot)return;var bounds=ownerCanvas.rootCanvas.pixelRect;var target=bounds.height>0&&bounds.width/bounds.height<1.5f?narrowOffset:wideOffset;if(layoutRoot.anchoredPosition!=target)layoutRoot.anchoredPosition=target;}
        void OnDestroy(){if(inventory)inventory.OnSwitchedToWeapon-=Bind;}
        void Bind(WeaponController w){heat=w?w.GetComponent<WeaponHeat>():null;displayed=-1;Refresh();}
        void LateUpdate(){UpdateLayout();Refresh();}
        void Refresh()
        {
            bool visible=heat&&heat.HeatEnabled;content.SetActive(visible);if(!visible)return;
            float ratio=heat.NormalizedHeat;bool blocked=heat.IsOverheated;
            gauge.SetState(ratio,blocked);
            int value=Mathf.RoundToInt(ratio*100);if(displayed!=value){percentage.SetText("{0}%",value);displayed=value;}
            string label=heat.StateLabel;if(lastState!=label){state.text=label;lastState=label;}
            var tint=ratio>=.7f?new Color(1,.36f,.12f):new Color(.95f,.79f,.42f);
            tint*=ratio>=.7f?Mathf.Lerp(.8f,1,.5f+.5f*Mathf.Sin(Time.unscaledTime*5)):1;
            float pulse=ratio>=.7f?Mathf.Lerp(.88f,1,.5f+.5f*Mathf.Sin(Time.unscaledTime*5)):1;
            gauge.canvasRenderer.SetColor(new Color(pulse,pulse,pulse,1));
            state.color=tint;
        }
    }
}
