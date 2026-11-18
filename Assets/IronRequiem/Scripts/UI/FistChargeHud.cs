using UnityEngine;
using UnityEngine.UI;
namespace IronRequiem
{
    /// v4 HUD: charge ring under the crosshair while the fist charges; flashes when full. Zero GC per frame.
    public sealed class FistChargeHud : MonoBehaviour
    {
        [SerializeField] ProtagonistMelee melee;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image fill;
        [SerializeField] Image fullFlash;
        [SerializeField] Color chargeColor=new Color(.25f,.95f,1f,.9f),fullColor=new Color(.85f,1f,1f,1f);
        [SerializeField] FistChargeRadialGraphic radial;
        float shown,flash;bool wasFull;
        public float Shown=>shown;
        public void Configure(ProtagonistMelee attack,CanvasGroup canvasGroup,Image ring,Image full){melee=attack;group=canvasGroup;fill=ring;fullFlash=full;}
        /// Segmented radial meter driven every frame by this HUD (independent of the central ring's visibility).
        public void AttachRadial(FistChargeRadialGraphic radialMeter){radial=radialMeter;if(radial)radial.Driven=true;}
        void LateUpdate()
        {
            if(radial)radial.SetState(melee&&melee.IsCharging,melee?melee.ChargeLevel:0f,melee&&melee.FullyCharged);
            if(!melee||!group)return;
            bool charging=melee.IsCharging;
            shown=Mathf.MoveTowards(shown,charging?1:0,Time.unscaledDeltaTime*(charging?10f:5f));
            bool full=charging&&melee.FullyCharged;
            if(full&&!wasFull)flash=1;wasFull=full;
            flash=Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*3f);
            group.alpha=shown;
            if(fill){fill.fillAmount=charging?melee.ChargeLevel:fill.fillAmount;fill.color=full?Color.Lerp(fullColor,chargeColor,.5f+.5f*Mathf.Sin(Time.unscaledTime*24f)):chargeColor;}
            if(fullFlash){var c=fullColor;c.a=flash;fullFlash.color=c;fullFlash.rectTransform.localScale=Vector3.one*(1f+.6f*(1f-flash));}
        }
    }
}
