using System.Collections.Generic;
using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace IronRequiem
{
    // Decorative remnants retain their pool. Collectible modules own persistent energy and never recycle while held.
    public sealed class BigBotCannonDebris : MonoBehaviour
    {
        public const int Capacity=8;
        static GameObject poolRoot;static BigBotCannonDebris[] slots;
        static readonly List<BigBotCannonDebris> collectibles=new List<BigBotCannonDebris>();
        StolenCannon collectible;
        Rigidbody body;Collider solid;float age,lifetime;readonly List<Collider> ignored=new List<Collider>();
        public bool InUse {get;private set;}
        public static int InUseCount {get{int count=0;if(slots!=null)foreach(var slot in slots)if(slot&&slot.InUse)count++;return count;}}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){poolRoot=null;slots=null;collectibles.Clear();}
        void Awake(){body=GetComponent<Rigidbody>();solid=GetComponent<Collider>();}
        public static void Prepare(BigBotCannonDebris prefab,Scene scene)
        {
            if(poolRoot||!prefab)return;poolRoot=new GameObject("BigBot_CannonDebrisPool");SceneManager.MoveGameObjectToScene(poolRoot,scene);slots=new BigBotCannonDebris[Capacity];
            for(int i=0;i<slots.Length;i++){slots[i]=Instantiate(prefab,poolRoot.transform);slots[i].gameObject.SetActive(false);}
        }
        public static BigBotCannonDebris Spawn(Transform pivot,float seconds,Collider[] owner,Transform front=null)
        {
            if(slots==null)return null;BigBotCannonDebris slot=null;
            foreach(var candidate in slots)if(!candidate.InUse){slot=candidate;break;}
            if(!slot){slot=slots[0];foreach(var candidate in slots)if(candidate.age>slot.age)slot=candidate;slot.Return();}
            slot.transform.SetPositionAndRotation(pivot.position,pivot.rotation);slot.transform.localScale=pivot.lossyScale;
            if(front)foreach(var filter in slot.GetComponentsInChildren<MeshFilter>(true))if(filter.name.StartsWith("Pincer_"))
            {filter.transform.SetPositionAndRotation(front.position,front.rotation);filter.transform.localScale=Vector3.one;}
            slot.FitCollider();
            slot.gameObject.SetActive(true);slot.lifetime=seconds;slot.age=0;slot.InUse=true;slot.body.isKinematic=false;slot.solid.enabled=true;
            foreach(var c in owner)slot.Ignore(c);
            foreach(var actor in FindObjectsByType<Actor>())foreach(var c in actor.GetComponentsInChildren<Collider>())slot.Ignore(c);
            slot.body.linearVelocity=Vector3.up*1.2f+pivot.right*.65f;slot.body.angularVelocity=pivot.forward*1.7f;slot.body.WakeUp();return slot;
        }
        public static BigBotCannonDebris SpawnCollectible(StolenCannon prefab,Transform pivot,Collider[] owner,Transform front=null)
        {
            collectibles.RemoveAll(item=>!item);
            if(collectibles.Count>=Capacity)
            {
                BigBotCannonDebris oldest=null;
                foreach(var item in collectibles)
                    if(item.collectible.State!=StolenCannonState.Equipped&&(!oldest||item.age>oldest.age))oldest=item;
                if(!oldest)return null;
                collectibles.Remove(oldest);oldest.Return();
            }
            var module=Instantiate(prefab,pivot.position,pivot.rotation);
            SceneManager.MoveGameObjectToScene(module.gameObject,pivot.gameObject.scene);
            var drop=module.GetComponent<BigBotCannonDebris>();
            drop.collectible=module;drop.transform.localScale=pivot.lossyScale;
            if(front)foreach(var filter in drop.GetComponentsInChildren<MeshFilter>(true))if(filter.name.StartsWith("Pincer_"))
            {filter.transform.SetPositionAndRotation(front.position,front.rotation);filter.transform.localScale=Vector3.one;}
            drop.FitCollider();drop.lifetime=120;drop.age=0;drop.InUse=true;
            drop.body.isKinematic=false;drop.body.useGravity=true;drop.solid.enabled=true;
            foreach(var c in owner)drop.Ignore(c);
            module.MakeAvailable(drop.body,drop.solid);
            drop.body.linearVelocity=Vector3.up*1.2f+pivot.right*.65f;drop.body.angularVelocity=pivot.forward*1.7f;
            collectibles.Add(drop);return drop;
        }
        void FitCollider()
        {
            Bounds bounds=default;bool first=true;
            foreach(var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if(!filter.name.EndsWith("LOD0"))continue;var b=filter.sharedMesh.bounds;for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {var p=transform.InverseTransformPoint(filter.transform.TransformPoint(b.center+Vector3.Scale(b.extents,new Vector3(x,y,z))));if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);}
            }
            var box=GetComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;
        }
        void Ignore(Collider c){if(c&&c!=solid&&!ignored.Contains(c)){Physics.IgnoreCollision(solid,c,true);ignored.Add(c);}}
        void Update()
        {
            if(collectible&&collectible.State==StolenCannonState.Equipped)return;
            if(InUse&&(age+=Time.deltaTime)>=lifetime)Return();
        }
        public void Return()
        {
            foreach(var c in ignored)if(c&&solid)Physics.IgnoreCollision(solid,c,false);ignored.Clear();
            if(body){if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}body.isKinematic=true;}
            if(collectible){InUse=false;Destroy(gameObject);return;}
            InUse=false;gameObject.SetActive(false);
        }
    }
}
