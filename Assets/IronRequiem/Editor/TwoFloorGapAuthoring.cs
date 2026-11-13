using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    [InitializeOnLoad]
    public static class TwoFloorGapAuthoring
    {
        public const string Lab = "Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity";
        public const string Report = "Tools/IronRequiem/Reports/TwoFloorGap";
        const string Queue = "Tools/IronRequiem/Requests/two-floor-gap.txt", Art = "Assets/IronRequiem/Art/Levels/TwoFloorGap";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static TwoFloorGapAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/TwoFloorGapAuthoring.cs") > Loaded) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "widen") WidenEntry(); else if (op == "visual") FinishVisuals(); else if (op == "open") EditorSceneManager.OpenScene(Lab); else Build(); IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.position = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static Transform Point(Transform parent, string name, Vector3 position)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform; }
        static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_" + name + ".mat");
        static void Build()
        {
            if (File.Exists(Lab)) throw new InvalidOperationException("Preserving existing two-floor level");
            Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset("Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity", Lab)) throw new Exception("Scene copy failed");
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var geometry = scene.GetRootGameObjects().Single(g => g.name == "TwoRoomsGeometry"); Object.DestroyImmediate(geometry);
                geometry = new GameObject("TwoFloorsGeometry"); var root = geometry.transform;
                var floor = Material("Floor"); var wall = Material("Wall"); var metal = Material("RustMetal"); var stripe = Material("Hazard");
                Box(root, "GroundFloor", new Vector3(0, -.5f, -1.5f), new Vector3(54, 1, 65), floor);
                Box(root, "UpperNearBank", new Vector3(0, 7.5f, -14), new Vector3(54, 1, 24), floor);
                Box(root, "UpperFarBank", new Vector3(0, 7.5f, 14), new Vector3(54, 1, 24), floor);
                Box(root, "Roof", new Vector3(0, 16.3f, -3.5f), new Vector3(54, .6f, 61), wall);
                Box(root, "WestWall", new Vector3(-27, 8, -3.5f), new Vector3(1, 16, 61), wall);
                Box(root, "SouthWall", new Vector3(0, 8, -34), new Vector3(54, 16, 1), wall);
                Box(root, "NorthWall", new Vector3(0, 12, 27), new Vector3(54, 8, 1), wall);
                Box(root, "NorthLowerWall", new Vector3(0, 4, 31.5f), new Vector3(54, 8, 1), wall);
                Box(root, "WestLowerExtension", new Vector3(-27, 4, 29), new Vector3(1, 8, 4), wall);
                Box(root, "EastLowerWall", new Vector3(27, 3.75f, -7.5f), new Vector3(1, 7.5f, 53), wall);
                Box(root, "EastUpperSouth", new Vector3(27, 12, -24), new Vector3(1, 8, 20), wall);
                Box(root, "EastUpperNorth", new Vector3(27, 12, 10.5f), new Vector3(1, 8, 33), wall);
                Box(root, "StairEntry", new Vector3(31, -.5f, 25), new Vector3(8, 1, 12), floor);
                var stairs = Point(root, "Staircase", Vector3.zero);
                for (int i = 0; i < 40; i++)
                {
                    float height = (i + 1) * .2f, z = 24 - (i + .5f) * .8f;
                    Box(stairs, "Step_" + (i + 1).ToString("00"), new Vector3(31, height * .5f, z), new Vector3(8, height, .8f), metal);
                    var lip = Box(stairs, "StepMark_" + i, new Vector3(31, height + .006f, z - .32f), new Vector3(8, .012f, .12f), stripe); Object.DestroyImmediate(lip.GetComponent<Collider>());
                }
                Box(root, "UpperLanding", new Vector3(31, 7.5f, -11), new Vector3(8, 1, 6), floor);
                Box(root, "StairOuterWall", new Vector3(35.5f, 8, 8.5f), new Vector3(1, 16, 46), wall);
                Box(root, "StairEndWall", new Vector3(31, 8, -14.5f), new Vector3(8, 16, 1), wall);
                Box(root, "StairRearWall", new Vector3(31, 8, 31.5f), new Vector3(8, 16, 1), wall);
                foreach (float y in new[] { 0f, 8f })
                {
                    foreach (int x in new[] { -1, 1 }) foreach (int z in new[] { -1, 1 }) Box(root, "SupportColumn", new Vector3(x * 14, y + 3.5f, z * 12), new Vector3(2, 7, 2), wall);
                    foreach (int x in new[] { -1, 1 }) { Box(root, "LowCover", new Vector3(x * 8, y + .65f, -6), new Vector3(4, 1.3f, 2), metal); Box(root, "HighCover", new Vector3(x * 9, y + 1.8f, 7), new Vector3(3, 3.6f, 2), metal); }
                }
                foreach (float z in new[] { -2.15f, 2.15f }) { var edge = Box(root, "GapWarningEdge", new Vector3(0, 8.012f, z), new Vector3(53, .024f, .3f), stripe); Object.DestroyImmediate(edge.GetComponent<Collider>()); }
                var player = All<PlayerCharacterController>(scene).Single(); player.transform.SetPositionAndRotation(new Vector3(0, .03f, -28), Quaternion.identity);
                PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                var coordinator = All<CombatTwoRoomsLab>(scene).Single(); var data = new SerializedObject(coordinator);
                data.FindProperty("firstRoom").boundsValue = new Bounds(new Vector3(0, 2, 0), new Vector3(48, 5, 48));
                // The upper encounter begins beyond the jump, so every enemy spawn has a complete path on its bank.
                data.FindProperty("secondRoom").boundsValue = new Bounds(new Vector3(0, 10, 14), new Vector3(48, 5, 22));
                var second = data.FindProperty("secondRoomSpawns"); for (int i = 0; i < second.arraySize; i++) { var p = (Transform)second.GetArrayElementAtIndex(i).objectReferenceValue; p.position = new Vector3(p.position.x, 8, p.position.z - 58); }
                var waves = data.FindProperty("waves"); var groups = waves.GetArrayElementAtIndex(3).FindPropertyRelative("Groups");
                for (int i = 0; i < groups.arraySize; i++) { var p = groups.GetArrayElementAtIndex(i).FindPropertyRelative("FixedPoint").objectReferenceValue as Transform; if (p) p.position = new Vector3(0, 8, 14); }
                var door = (GameObject)data.FindProperty("gate").objectReferenceValue; door.transform.SetPositionAndRotation(new Vector3(27, 3.75f, 25), Quaternion.identity); door.transform.localScale = new Vector3(1, 7.5f, 12);
                data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(coordinator);
                foreach (var pickup in All<HealthPickup>(scene)) if (pickup.transform.position.z > 34) { pickup.transform.position = new Vector3(pickup.transform.position.x, 8.5f, pickup.transform.position.z < 58 ? -14 : 18); PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform); }
                var hazard = new GameObject("GapFallRecovery", typeof(BoxCollider), typeof(Rigidbody), typeof(GapFallRecovery)); hazard.transform.position = new Vector3(0, 4.8f, 0);
                var trigger = hazard.GetComponent<BoxCollider>(); trigger.isTrigger = true; trigger.size = new Vector3(53, 3.2f, 4);
                var body = hazard.GetComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var returns = Point(null, "GapReturnPoints", Vector3.zero); var xs = new[] { -20f, -10f, 0f, 10f, 20f };
                hazard.GetComponent<GapFallRecovery>().Configure(player, xs.Select(x => Point(returns, "NearReturn_" + x, new Vector3(x, 8.05f, -6))).ToArray(), xs.Select(x => Point(returns, "FarReturn_" + x, new Vector3(x, 8.05f, 6))).ToArray());
                var font = All<TMP_Text>(scene).First(t => t.font).font;
                void Sign(string name, string message, Vector3 p, Vector3 rotation)
                {
                    var text = new GameObject(name).AddComponent<TextMeshPro>(); text.font = font; text.text = message; text.fontSize = 7; text.alignment = TextAlignmentOptions.Center; text.color = new Color(.15f, .9f, 1); text.rectTransform.sizeDelta = new Vector2(18, 3); text.transform.position = p; text.transform.rotation = Quaternion.Euler(rotation);
                }
                Sign("StairSign", "01   COMBATE\n02   ESCALERA >", new Vector3(0, 4.5f, 26.4f), Vector3.zero);
                Sign("GapSign", "SALTA LA GRIETA\nCAÍDA: -20 VIDA", new Vector3(0, 12, 8), Vector3.zero);
                foreach (float y in new[] { 7f, 15f }) foreach (float x in new[] { -15f, 15f }) foreach (float z in new[] { -18f, 18f })
                {
                    var light = new GameObject("FloorLamp").AddComponent<Light>(); light.type = LightType.Point; light.range = 32; light.intensity = 3; light.shadows = LightShadows.None; light.color = new Color(.75f, .88f, 1); light.transform.position = new Vector3(x, y, z);
                    var lamp = Box(root, "LampHousing", new Vector3(x, y + .3f, z), new Vector3(2, .15f, .5f), Material("LampCold")); Object.DestroyImmediate(lamp.GetComponent<Collider>());
                }
                var stairLight = new GameObject("StairLight").AddComponent<Light>(); stairLight.type = LightType.Point; stairLight.shadows = LightShadows.None; stairLight.intensity = 3; stairLight.range = 30; stairLight.transform.position = new Vector3(31, 12, 8);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .41f, .46f);
                BakeUVs(geometry); Bake(geometry);
                EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
                var entries = EditorBuildSettings.scenes.ToList(); if (!entries.Any(s => s.path == Lab)) entries.Add(new EditorBuildSettingsScene(Lab, true)); EditorBuildSettings.scenes = entries.ToArray();
                File.WriteAllText(Report + "/build.txt", "Two stacked floors,0m/8m.40 physical steps .2m rise/.8m tread;8m wide stair. Upper gap4m wide, full arena width. Existing waves, player, weapons, menu, pickups reused. Fall20HP, recover on take-off bank; five safe points per bank. NavMesh radius2.25m/climb.25m; no robot jump link across player challenge.");
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
        static void Bake(GameObject geometry)
        {
            Physics.SyncTransforms(); var surface = geometry.GetComponent<Unity.AI.Navigation.NavMeshSurface>(); if (!surface) surface = geometry.AddComponent<Unity.AI.Navigation.NavMeshSurface>(); surface.RemoveData();
            var settings = NavMesh.GetSettingsByID(0); settings.agentRadius = 2.25f; settings.agentHeight = 3.05f; settings.agentClimb = .25f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            var sources = new List<NavMeshBuildSource>(); NavMeshBuilder.CollectSources(geometry.transform, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var bounds = new Bounds(new Vector3(4, 7, -1), new Vector3(68, 22, 72)); var nav = surface.navMeshData;
            if (nav) { if (!NavMeshBuilder.UpdateNavMeshData(nav, settings, sources, bounds)) throw new Exception("Navigation rebake failed"); EditorUtility.SetDirty(nav); }
            else { nav = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity); if (!nav) throw new Exception("Navigation bake failed"); AssetDatabase.CreateAsset(nav, Art + "/TwoFloorsNavMesh.asset"); }
            surface.agentTypeID = 0; surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children; surface.navMeshData = nav; surface.AddData(); EditorUtility.SetDirty(surface);
        }
        static void BakeUVs(GameObject geometry)
        {
            // The bunker materials expect metres in their UVs. Never stretch one tile over a whole wall.
            var cache = new Dictionary<Vector3, Mesh>(); int index = 0;
            foreach (var filter in geometry.GetComponentsInChildren<MeshFilter>())
            {
                var size = filter.transform.localScale;
                if (!cache.TryGetValue(size, out var mesh))
                {
                    mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "TwoFloorCube_" + index; var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
                    for (int i = 0; i < uv.Length; i++) { var p = Vector3.Scale(vertices[i], size); var n = normals[i]; uv[i] = Mathf.Abs(n.x) > .5f ? new Vector2(p.z, p.y) : Mathf.Abs(n.y) > .5f ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y); }
                    mesh.uv = uv; mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Art + "/CubeUV_" + index++ + ".asset")); cache.Add(size, mesh);
                }
                filter.sharedMesh = mesh;
            }
        }
        static void FinishVisuals()
        {
            var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Additive);
            try { BakeUVs(scene.GetRootGameObjects().Single(g => g.name == "TwoFloorsGeometry")); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/visual.txt", "World-scaled UVs on copied cube meshes; shared bunker materials and original meshes preserved."); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        static void WidenEntry()
        {
            string backup = "Tools/IronRequiem/Backups/20261107-two-floor-gap/entry-before-fix"; Directory.CreateDirectory(backup); File.Copy(Lab, backup + "/CombatTwoFloorsLab.unity", true); File.Copy(Art + "/TwoFloorsNavMesh.asset", backup + "/TwoFloorsNavMesh.asset", true);
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var geometry = scene.GetRootGameObjects().Single(g => g.name == "TwoFloorsGeometry"); var root = geometry.transform;
                void Place(string name, Vector3 p, Vector3 scale) { var t = root.Find(name); t.position = p; t.localScale = scale; }
                Place("GroundFloor", new Vector3(0, -.5f, -1.5f), new Vector3(54, 1, 65)); Place("NorthWall", new Vector3(0, 12, 27), new Vector3(54, 8, 1));
                Box(root, "NorthLowerWall", new Vector3(0, 4, 31.5f), new Vector3(54, 8, 1), Material("Wall")); Box(root, "WestLowerExtension", new Vector3(-27, 4, 29), new Vector3(1, 8, 4), Material("Wall"));
                Place("StairEntry", new Vector3(31, -.5f, 25), new Vector3(8, 1, 12)); Place("StairOuterWall", new Vector3(35.5f, 8, 8.5f), new Vector3(1, 16, 46)); Place("StairRearWall", new Vector3(31, 8, 31.5f), new Vector3(8, 16, 1));
                var gate = (GameObject)new SerializedObject(All<CombatTwoRoomsLab>(scene).Single()).FindProperty("gate").objectReferenceValue; gate.transform.position = new Vector3(27, 3.75f, 25); gate.transform.localScale = new Vector3(1, 7.5f, 12);
                BakeUVs(geometry); Bake(geometry); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); File.WriteAllText(Report + "/entry-fix.txt", "Entrance was narrower than the4.5m navigation diameter after accounting for the first steps. Extended bottom landing and lower north wall; gate covers whole opening. Stairs and upper connection were already navigable. Existing nav asset updated with its GUID preserved. Copied cube UVs match existing bunker materials at world scale.");
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
