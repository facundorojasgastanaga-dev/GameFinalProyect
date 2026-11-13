using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace IronRequiem
{
    // Scene-owned pools, fixed maxima. No material instances; no persistent objects across scenes.
    public static class RobotDestructionPool
    {
        public const int ArmorCapacity = 16, BurstCapacity = 32;
        static GameObject root;
        static RobotArmorDebris[] armor;
        static RobotArmorDebris[] arms;
        static RobotBodyBurst[] bursts;
        static GameObject burstPrefab;
        public static int ArmorInUse { get { int count = 0; if (armor != null) foreach (var a in armor) if (a && a.InUse) count++; return count; } }
        public static int BurstsInUse { get { int count = 0; if (bursts != null) foreach (var b in bursts) if (b && b.InUse) count++; return count; } }
        public static int CreatedBursts { get; private set; }
        public static int DroppedArmor { get; private set; }
        public static int ArmsInUse { get {int count=0;if(arms!=null)foreach(var arm in arms)if(arm && arm.InUse)count++;return count;} }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { SceneManager.sceneUnloaded -= ClearAll; root = null; armor = null; arms=null; bursts = null; burstPrefab = null; CreatedBursts = DroppedArmor = 0; }
        public static void Prepare(GameObject prefab, Scene scene)
        {
            if (root) return;
            root = new GameObject("T07_DestructionPools"); SceneManager.MoveGameObjectToScene(root, scene);
            SceneManager.sceneUnloaded -= ClearAll; SceneManager.sceneUnloaded += ClearAll;
            armor = new RobotArmorDebris[ArmorCapacity]; bursts = new RobotBodyBurst[BurstCapacity]; burstPrefab = prefab; CreatedBursts = DroppedArmor = 0;
            arms=new RobotArmorDebris[8];
            for(int i=0;i<arms.Length;i++)
            {
                var part=new GameObject("RightArmDebris_"+i);part.transform.SetParent(root.transform,false);part.AddComponent<MeshFilter>();
                var view=part.AddComponent<MeshRenderer>();view.shadowCastingMode=ShadowCastingMode.Off;view.receiveShadows=false;
                arms[i]=part.AddComponent<RobotArmorDebris>();part.SetActive(false);
            }
            for (int i = 0; i < armor.Length; i++)
            {
                var go = new GameObject("ArmorDebris_" + i); go.transform.SetParent(root.transform, false); go.AddComponent<MeshFilter>();
                var renderer = go.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                armor[i] = go.AddComponent<RobotArmorDebris>(); go.SetActive(false);
            }
            if (prefab) for (int i = 0; i < 16; i++) CreateBurst(i);
        }
        static RobotArmorDebris Oldest(RobotArmorDebris[] set)
        {
            RobotArmorDebris best = null; if (set == null) return null;
            foreach (var d in set) if (d && d.InUse && (!best || d.Age > best.Age)) best = d;
            return best;
        }
        // F4: a sector unload clears its wreckage (pool root may live in the persistent session scene).
        static void ClearAll(Scene unloaded)
        {
            if (armor != null) foreach (var d in armor) if (d && d.InUse) d.Return();
            if (arms != null) foreach (var d in arms) if (d && d.InUse) d.Return();
        }
        static RobotBodyBurst CreateBurst(int i)
        {
            var instance = UnityEngine.Object.Instantiate(burstPrefab, root.transform); instance.SetActive(false);
            bursts[i] = instance.GetComponent<RobotBodyBurst>(); CreatedBursts++; return bursts[i];
        }
        public static bool Burst(Transform anchor, Vector3 position, Action onBurst, float scale = 1f)
        {
            if (bursts == null || !burstPrefab) return false;
            for (int i = 0; i < bursts.Length; i++)
            {
                var slot = bursts[i]; if (!slot) slot = CreateBurst(i);
                if (slot.InUse) continue; slot.Begin(anchor, position, onBurst, scale); return true;
            }
            return false;
        }
        public static void ThrowArmor(Mesh mesh, Material material, Material interior, Vector3 position, Quaternion rotation, Vector3 scale, float lifetime, LayerMask mask)
        {
            if (armor != null) foreach (var slot in armor)
                if (slot && !slot.InUse) { slot.Begin(mesh, material, interior, position, rotation, scale, lifetime, mask); return; }
            // F4: wreckage persists for the sector; when the budget is full recycle the oldest piece.
            var oldest = Oldest(armor); if (oldest) { oldest.Return(); oldest.Begin(mesh, material, interior, position, rotation, scale, lifetime, mask); return; }
            DroppedArmor++;
        }
        public static void ThrowArm(SkinnedMeshRenderer source,Material material,Material interior,float lifetime,LayerMask mask)
        {
            if(arms!=null)foreach(var arm in arms)if(arm && !arm.InUse){arm.BeginArm(source,material,interior,lifetime,mask);return;}
            var old=Oldest(arms);if(old){old.Return();old.BeginArm(source,material,interior,lifetime,mask);}
        }
    }
}
