using UnityEngine;
using UnityEngine.UI;
namespace IronRequiem
{
    /// v4: cyan screen-edge glow while the slide guard window is open, pulsing on each deflected hit.
    public sealed class SlideGuardScreenFx : MonoBehaviour
    {
        [SerializeField] SlideGuardFeedback feedback;
        [SerializeField] Image edge;
        [SerializeField] Color color=new Color(.2f,.95f,1f,1f);
        [SerializeField,Range(0,1)] float maxAlpha=.55f;
        public float Alpha {get;private set;}
        public void Configure(SlideGuardFeedback source,Image image){feedback=source;edge=image;}
        void LateUpdate()
        {
            if(!feedback||!edge)return;
            Alpha=feedback.EdgeIntensity*maxAlpha;
            bool on=Alpha>.004f;if(edge.enabled!=on)edge.enabled=on;
            if(!on)return;
            var c=color;c.a=Alpha;edge.color=c;
        }
    }
}
