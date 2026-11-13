using Unity.FPS.AI;
using Unity.FPS.Game;
using UnityEngine;
namespace IronRequiem
{
    public sealed class EnemyCannonChargePresentation : MonoBehaviour
    {
        [SerializeField] EnemyController enemy;
        [SerializeField] GameObject effect;
        [SerializeField] Transform core,outer,inner,rays,spindle;
        ParticleSystem[] particles;Health health;bool shown;
        public bool IsVisible=>effect&&effect.activeSelf;
        public void Configure(EnemyController controller,GameObject root){enemy=controller;effect=root;core=root.transform.Find("EnergyCore");outer=root.transform.Find("OuterFrame");inner=root.transform.Find("InnerFrame");rays=root.transform.Find("EnergyRays");spindle=root.transform.Find("EnergySpindle");}
        void Awake(){health=GetComponent<Health>();particles=effect?effect.GetComponentsInChildren<ParticleSystem>(true):new ParticleSystem[0];Hide();}
        void LateUpdate()
        {
            float fraction=enemy?enemy.EnemyChargeFraction:0;
            if(!enemy||!enemy.enabled||!health||health.CurrentHealth<=0||fraction<=0||Time.timeScale<=0){Hide();return;}
            if(!shown){effect.SetActive(true);foreach(var p in particles)p.Play(true);shown=true;}
            float size=Mathf.Lerp(.04f,.28f,Mathf.SmoothStep(0,1,fraction));
            if(core)core.localScale=Vector3.one*size;
            if(outer){outer.localScale=Vector3.one*size*1.4f;outer.localRotation=Quaternion.Euler(18,30,Time.time*100);}
            if(inner){inner.localScale=Vector3.one*size;inner.localPosition=Vector3.forward*size;inner.localRotation=Quaternion.Euler(48,-32,45-Time.time*80);}
            if(rays){rays.localScale=Vector3.one*size*1.5f;rays.localRotation=Quaternion.Euler(0,0,-Time.time*95);}
            if(spindle)spindle.localScale=new Vector3(size*.12f,size*.12f,size);
        }
        void Hide(){if(!shown&&effect&&!effect.activeSelf)return;foreach(var p in particles)p.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(effect)effect.SetActive(false);shown=false;}
        void OnDisable(){if(enemy)enemy.CancelEnemyCharge();if(particles!=null)Hide();}
    }
}