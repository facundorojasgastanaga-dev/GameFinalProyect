using UnityEngine;
using UnityEngine.UI;
namespace IronRequiem
{
    /// v4: screen-edge glow for the slide guard / dash. 24/11: off by default (the cyan tint over the camera and the
    /// red low-health pulse were removed; low health now shows CriticalDamageMessage). Enable showEdge to restore it.
    public sealed class SlideGuardScreenFx : MonoBehaviour
    {
        [SerializeField] SlideGuardFeedback feedback;
        [SerializeField] Image edge;
        [SerializeField] Color color=new Color(.2f,.95f,1f,1f);
        [SerializeField,Range(0,1)] float maxAlpha=.55f;
        [SerializeField] bool showEdge;
        public float Alpha {get;private set;}
        public void Configure(SlideGuardFeedback source,Image image){feedback=source;edge=image;}
        void LateUpdate()
        {
            if(!feedback||!edge)return;
            float strength = showEdge ? Mathf.Max(feedback.EdgeIntensity, feedback.DashGlow) : 0;
            Alpha=strength*maxAlpha;
            bool on=Alpha>.004f;if(edge.enabled!=on)edge.enabled=on;
            if(!on)return;
            var c=color;c.a=Alpha;edge.color=c;
        }
    }
}
