using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    [RequireComponent(typeof(Health))]
    public sealed class EnemyPowerIndicators : MonoBehaviour
    {
        [SerializeField] Renderer[] indicators;
        [SerializeField] int[] materialSlots;
        MaterialPropertyBlock block;
        Health health;
        public Renderer[] Indicators=>indicators;
        public bool UsesMaterialSlots=>materialSlots!=null&&materialSlots.Length==indicators.Length&&materialSlots.Length>0;
        public bool IsPoweredOff{get;private set;}
        public void Configure(Renderer[] views){indicators=views;materialSlots=null;}
        public void ConfigureMaterialSlots(Renderer[] views,int[] slots){indicators=views;materialSlots=slots;}
        void Awake()=>health=GetComponent<Health>();
        void OnEnable(){if(!health)health=GetComponent<Health>();health.OnDie+=PowerOff;IsPoweredOff=false;if(UsesMaterialSlots)SetSlots(false);else foreach(var r in indicators)if(r)r.enabled=true;}
        void OnDisable(){if(health)health.OnDie-=PowerOff;}
        void PowerOff(){IsPoweredOff=true;if(UsesMaterialSlots)SetSlots(true);else foreach(var r in indicators)if(r)r.enabled=false;}
        void SetSlots(bool off)
        {
            if(block==null)block=new MaterialPropertyBlock();
            for(int i=0;i<indicators.Length;i++)
            {
                var r=indicators[i];int slot=materialSlots[i];if(!r||slot<0||slot>=r.sharedMaterials.Length)continue;var material=r.sharedMaterials[slot];if(!material)continue;r.GetPropertyBlock(block,slot);
                if(material.HasProperty("_EmissionColor"))block.SetColor("_EmissionColor",off?Color.black:material.GetColor("_EmissionColor"));
                if(material.HasProperty("_BaseColor"))block.SetColor("_BaseColor",off?Color.black:material.GetColor("_BaseColor"));r.SetPropertyBlock(block,slot);
            }
        }
    }
}
