using System;
using System.Collections.Generic;
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
        // 17/11/2026 (E7, MAP_FX_AUDIT r2 F1): one burst set per prefab. The first Prepare used to fix a single prefab for
        // the whole scene, so whichever enemy woke first decided every death burst in the room (BigBot/SawBot original vs
        // the _IR variant of HoverBot/Turret/T-07). The first prefab keeps the original arrays and warm-up (16); other
        // prefabs get their own set of BurstCapacity slots, 8 warmed at Prepare and the rest created on demand.
        public const int ExtraWarmBursts = 8;
        static readonly Dictionary<GameObject, RobotBodyBurst[]> sets = new Dictionary<GameObject, RobotBodyBurst[]>();
        public static int BurstPrefabs => sets.Count;
        public static int ArmorInUse { get { int count = 0; if (armor != null) foreach (var a in armor) if (a && a.InUse) count++; return count; } }
        public static int BurstsInUse { get { int count = 0; foreach (var set in sets.Values) foreach (var b in set) if (b && b.InUse) count++; return count; } }
        public static int CreatedBursts { get; private set; }
        public static int DroppedArmor { get; private set; }
        public static int ArmsInUse { get {int count=0;if(arms!=null)foreach(var arm in arms)if(arm && arm.InUse)count++;return count;} }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() { SceneManager.sceneUnloaded -= ClearAll; root = null; armor = null; arms=null; bursts = null; burstPrefab = null; sets.Clear(); CreatedBursts = DroppedArmor = 0; }
        public static void Prepare(GameObject prefab, Scene scene)
        {
            if (root) { AddSet(prefab, ExtraWarmBursts); return; }
            sets.Clear();
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
            if (prefab) { sets[prefab] = bursts; for (int i = 0; i < 16; i++) CreateBurst(prefab, bursts, i); }
        }
        static RobotBodyBurst[] AddSet(GameObject prefab, int warm)
        {
            if (!prefab || !root) return null;
            if (sets.TryGetValue(prefab, out var set)) return set;
            set = new RobotBodyBurst[BurstCapacity]; sets[prefab] = set;
            if (!burstPrefab) { burstPrefab = prefab; bursts = set; }
            for (int i = 0; i < warm; i++) CreateBurst(prefab, set, i);
            return set;
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
        static RobotBodyBurst CreateBurst(GameObject prefab, RobotBodyBurst[] set, int i)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, root.transform); instance.SetActive(false);
            set[i] = instance.GetComponent<RobotBodyBurst>(); CreatedBursts++; return set[i];
        }
        /// Plays the scene's first prepared burst (legacy entry point).
        public static bool Burst(Transform anchor, Vector3 position, Action onBurst, float scale = 1f) => Burst(burstPrefab, anchor, position, onBurst, scale);
        /// Plays a burst of this prefab (E7). Callers pass the prefab they prepared, so each enemy keeps its own look.
        public static bool Burst(GameObject prefab, Transform anchor, Vector3 position, Action onBurst, float scale = 1f)
        {
            if (!prefab || !root) return false;
            if (!sets.TryGetValue(prefab, out var set)) set = AddSet(prefab, 0);
            if (set == null) return false;
            for (int i = 0; i < set.Length; i++)
            {
                var slot = set[i]; if (!slot) slot = CreateBurst(prefab, set, i);
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
