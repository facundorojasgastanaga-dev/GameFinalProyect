using System;
using System.IO;
using System.Linq;
using Unity.FPS.AI;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.EditorTools.Vision
{
    /// Chat 11 · F3 inside a real Map01 session (Play Mode, nothing saved): an old "Transfer" airlock with a T07 at
    /// its far end, before (vision off) and after (vision on), then the player in the same spot firing at it so the
    /// Game view screenshot shows the muzzle flash next to the highlight. Restores the user's setting.
    public static class VisionF3Session
    {
        const string Menu = "Tools/Iron Requiem/Vision/";
        const string Out = "Tools/IronRequiem/Reports/Vision/F3";
        const string T07 = "Assets/IronRequiem/Prefabs/Campaign/T07_Current_Repaired.prefab";
        static GameObject enemy; static Vector3 eye, aim; static float userIntensity = 1;

        [MenuItem(Menu + "F3 Airlock setup + before-after (play)")]
        public static void Airlock()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode required.");
            Directory.CreateDirectory(Out + "/captures");
            var candidates = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Where(t => t.name.StartsWith("Transfer", StringComparison.Ordinal)).ToArray();
            var transfer = candidates.Where(t => t.gameObject.activeInHierarchy)
                .Select(t => (t, b: Bounds(t))).Where(x => x.b.size.x > 2 && x.b.size.z > 2).OrderByDescending(x => x.b.size.x + x.b.size.z).FirstOrDefault();
            if (!transfer.t) { Log("no active Transfer airlock found; candidates: " + string.Join(", ", candidates.Select(c => $"{c.name}@{c.gameObject.scene.name} active={c.gameObject.activeInHierarchy} size={Bounds(c).size}"))); return; }
            // the interior is the floor slab of the chamber, not the whole airlock object (walls, frame, outside)
            var floors = transfer.t.GetComponentsInChildren<Renderer>().Where(r => r.name.IndexOf("Floor", StringComparison.OrdinalIgnoreCase) >= 0 && r.bounds.size.y < 1.5f)
                .OrderByDescending(r => r.bounds.size.x * r.bounds.size.z).ToArray();
            Log("airlock children: " + string.Join(", ", transfer.t.GetComponentsInChildren<Renderer>().Take(20).Select(r => $"{r.name}{r.bounds.size}")));
            var b = floors.Length > 0 ? floors[0].bounds : transfer.b; bool alongX = b.size.x >= b.size.z;
            var axis = alongX ? Vector3.right : Vector3.forward; float half = (alongX ? b.extents.x : b.extents.z) - .9f;
            float floorY = b.min.y; if (Physics.Raycast(b.center + Vector3.up, Vector3.down, out var hit, b.size.y + 2, ~0, QueryTriggerInteraction.Ignore)) floorY = hit.point.y;
            var center = new Vector3(b.center.x, floorY, b.center.z);
            var far = center + axis * half; eye = center - axis * half + Vector3.up * 1.65f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(T07);
            if (enemy) Object.Destroy(enemy);
            enemy = Object.Instantiate(prefab, far, Quaternion.LookRotation(-axis, Vector3.up)); enemy.name = "VisionF3_T07";
            // like a real spawn, the enemy belongs to the airlock's scene (the active scene can be the protected hangar)
            SceneManager.MoveGameObjectToScene(enemy, transfer.t.gameObject.scene);
            foreach (var agent in enemy.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>()) agent.enabled = false;
            var health = enemy.GetComponent<Unity.FPS.Game.Health>();
            if (health) { health.Invincible = true; if (health.CurrentHealth <= 0) health.CurrentHealth = health.MaxHealth; } // captured in its spawn frame (before Health.Start)
            aim = far + Vector3.up * 1.2f;
            bool clear = !Physics.Linecast(eye, aim, out var block, ~0, QueryTriggerInteraction.Ignore) || block.transform.IsChildOf(enemy.transform);
            Log($"floor={(floors.Length > 0 ? floors[0].name : "none")} bounds={b} eye={eye} enemy={far} lineOfSight={(clear ? "clear" : "blocked by " + block.collider.name)}");
            userIntensity = RequiemSettings.VisionIntensity;
            float dist = Vector3.Distance(new Vector3(eye.x, 0, eye.z), new Vector3(far.x, 0, far.z));
            foreach (var (tag, value) in new[] { ("antes", 0f), ("despues", Mathf.Max(userIntensity, 1)) })
            {
                RequiemSettings.SetVisionIntensity(value); if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow();
                Shot($"{Out}/captures/Esclusa_Transfer_{tag}_{dist:0}m.jpg");
            }
            var vision = EnemyDarkReadability.Instance; var ec = enemy.GetComponent<EnemyController>();
            Log($"airlock {transfer.t.parent?.name}/{transfer.t.name} scene={transfer.t.gameObject.scene.name} distance={dist:0.0} m darkness={(vision ? vision.DarknessOf(ec) : -1):0.00} illuminance={(vision ? vision.Illuminance(aim) : -1):0.00} highlighted={(vision && vision.IsHighlighted(ec))}");
        }

        /// Puts the player where the camera was, gives the station cannon and holds the trigger; take a Game view screenshot next.
        [MenuItem(Menu + "F3 Player fires at the airlock enemy (play)")]
        public static void Fire()
        {
            if (!EditorApplication.isPlaying || !enemy) throw new InvalidOperationException("Run the airlock setup first.");
            var player = Object.FindAnyObjectByType<PlayerCharacterController>();
            player.TeleportToCheckpoint(eye - Vector3.up * 1.65f); Physics.SyncTransforms();
            var dir = aim - eye; float yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(dir, Vector3.up)).eulerAngles.y;
            float pitch = -Mathf.Asin(dir.normalized.y) * Mathf.Rad2Deg; player.SetViewAngles(yaw, pitch);
            var weapons = player.GetComponent<PlayerWeaponsManager>(); var station = Object.FindAnyObjectByType<WeaponMountStation>();
            if (station && station.WeaponPrefab && !weapons.HasWeapon(station.WeaponPrefab)) weapons.AddWeapon(station.WeaponPrefab);
            for (int i = 0; i < 9; i++) { var w = weapons.GetWeaponAtSlotIndex(i); if (w && station && w.SourcePrefab == station.WeaponPrefab.gameObject) { weapons.SwitchToWeaponIndex(i, true); weapons.SnapActiveWeaponUp(); } }
            if (EnemyDarkReadability.Instance) EnemyDarkReadability.Instance.ApplyNow();
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left, true));
            Log("player firing at the airlock enemy; active=" + (weapons.GetActiveWeapon() ? weapons.GetActiveWeapon().name : "none"));
        }
        [MenuItem(Menu + "F3 Release trigger + restore setting (play)")]
        public static void Release()
        {
            if (Mouse.current != null) InputSystem.QueueStateEvent(Mouse.current, new MouseState());
            RequiemSettings.SetVisionIntensity(userIntensity);
            if (enemy) Object.Destroy(enemy);
            Log("released; vision intensity restored to " + userIntensity);
        }

        static Bounds Bounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }
        static void Shot(string file)
        {
            const int W = 960, H = 540;
            var go = new GameObject("VisionF3_Camera") { hideFlags = HideFlags.DontSave }; var cam = go.AddComponent<Camera>(); var data = go.AddComponent<UniversalAdditionalCameraData>();
            cam.enabled = false; cam.fieldOfView = 60; cam.nearClipPlane = .05f; cam.farClipPlane = 400; cam.allowHDR = true;
            data.renderPostProcessing = true; data.volumeLayerMask = ~0; data.volumeTrigger = go.transform; data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye, Vector3.up));
            var rt = RenderTexture.GetTemporary(W, H, 24); var tex = new Texture2D(W, H, TextureFormat.RGB24, false); var prev = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); File.WriteAllBytes(file, tex.EncodeToJPG(85)); }
            finally { cam.targetTexture = null; RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go); }
        }
        static void Log(string line) { Directory.CreateDirectory(Out); File.AppendAllText(Out + "/session.txt", DateTime.Now.ToString("s") + " " + line + "\n"); Debug.Log("[VisionF3] " + line); }
    }
}
