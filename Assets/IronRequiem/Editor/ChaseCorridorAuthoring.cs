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
    public static class ChaseCorridorAuthoring
    {
        public const string Lab = "Assets/IronRequiem/Scenes/ChaseCorridorLab.unity", Report = "Tools/IronRequiem/Reports/ChaseCorridor";
        const string Art = "Assets/IronRequiem/Art/Levels/ChaseCorridor", Queue = "Tools/IronRequiem/Requests/chase-corridor.txt";
        static readonly DateTime Loaded = DateTime.UtcNow;
        static readonly Dictionary<Vector3, Mesh> cubes = new Dictionary<Vector3, Mesh>();
        static ChaseCorridorAuthoring() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Queue) || File.GetLastWriteTimeUtc("Assets/IronRequiem/Editor/ChaseCorridorAuthoring.cs") > Loaded) return;
            if (Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty)) return;
            string op = File.ReadAllText(Queue).Trim(); File.Delete(Queue); Directory.CreateDirectory(Report);
            try { if (op == "open") { var scene = EditorSceneManager.OpenScene(Lab); LabelRestart(scene); EditorSceneManager.SaveScene(scene); } else if (op == "reserve") ReserveUpperTurret(); else { Build(); ConnectPrevious(); } IronRequiemSetupTools.WriteAudit(); }
            catch (Exception e) { File.WriteAllText(Report + "/error.txt", e.ToString()); Debug.LogException(e); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Environment/Bunker/Bunker_" + name + ".mat");
        static Transform Point(Transform parent, string name, Vector3 position)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform; }
        static GameObject Box(Transform parent, string name, Vector3 p, Vector3 scale, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.position = p; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            if (!cubes.TryGetValue(scale, out var mesh))
            {
                mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh); mesh.name = "CorridorCube_" + cubes.Count; var v = mesh.vertices; var n = mesh.normals; var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) { var q = Vector3.Scale(v[i], scale); uv[i] = Mathf.Abs(n[i].x) > .5f ? new Vector2(q.z, q.y) : Mathf.Abs(n[i].y) > .5f ? new Vector2(q.x, q.z) : new Vector2(q.x, q.y); }
                mesh.uv = uv; mesh.RecalculateTangents(); AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Art + "/" + mesh.name + ".asset")); cubes.Add(scale, mesh);
            }
            go.GetComponent<MeshFilter>().sharedMesh = mesh; return go;
        }
        static Mesh SawMesh()
        {
            const int n = 96; var vertices = new List<Vector3>(); var triangles = new List<int>();
            vertices.Add(new Vector3(0, 0, -.12f)); vertices.Add(new Vector3(0, 0, .12f));
            for (int side = 0; side < 2; side++) for (int i = 0; i < n; i++) { float angle = i * Mathf.PI * 2 / n, radius = i % 4 == 0 ? 3.1f : i % 4 == 1 ? 2.65f : 2.9f; vertices.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, side == 0 ? -.12f : .12f)); }
            for (int i = 0; i < n; i++) { int a = 2 + i, b = 2 + (i + 1) % n, c = a + n, d = b + n; triangles.AddRange(new[] { 0, b, a, 1, c, d, a, b, d, a, d, c }); }
            var mesh = new Mesh { name = "GiantSaw_24Teeth" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); var uv = vertices.Select(v => new Vector2(v.x, v.y)).ToArray(); mesh.uv = uv; mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, Art + "/GiantSaw.asset"); return mesh;
        }
        static void Build()
        {
            if (File.Exists(Lab)) throw new Exception("Preserving existing chase level"); Directory.CreateDirectory(Art); AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset("Assets/IronRequiem/Scenes/CombatTwoRoomsLab.unity", Lab)) throw new Exception("Scene copy failed");
            var previous = SceneManager.GetActiveScene(); var scene = EditorSceneManager.OpenScene(Lab, OpenSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var original = All<CombatTwoRoomsLab>(scene).Single(); var old = new SerializedObject(original); var oldWaves = old.FindProperty("waves");
                var t07 = oldWaves.GetArrayElementAtIndex(0).FindPropertyRelative("Groups").GetArrayElementAtIndex(0).FindPropertyRelative("Prefab").objectReferenceValue as GameObject;
                var sawBot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/IronRequiem/Prefabs/Enemies/SawBot_SawPolish_v04.prefab");
                foreach (var enemy in new[] { t07, sawBot }) if (!enemy || !enemy.GetComponent<Health>() || !enemy.GetComponent<Unity.FPS.AI.EnemyController>()) throw new Exception("Missing current enemy prefab");
                var message = (TMP_Text)old.FindProperty("message").objectReferenceValue; var oldDoor = (GameObject)old.FindProperty("gate").objectReferenceValue;
                Object.DestroyImmediate(oldDoor); Object.DestroyImmediate(original.gameObject);
                foreach (var root in scene.GetRootGameObjects().Where(g => g.name == "TwoRoomsGeometry" || g.name == "WaveSpawnPoints")) Object.DestroyImmediate(root);
                var geometry = new GameObject("ChaseCorridorGeometry"); var parent = geometry.transform; var floor = Mat("Floor"); var wall = Mat("Wall"); var metal = Mat("RustMetal"); var stripe = Mat("Hazard");
                foreach (var span in new[] { new Vector2(-18, 48), new Vector2(52, 98), new Vector2(102, 148), new Vector2(152, 200) }) Box(parent, "FloorBank", new Vector3(0, -.5f, (span.x + span.y) * .5f), new Vector3(18, 1, span.y - span.x), floor);
                foreach (float x in new[] { -8.5f, 8.5f }) Box(parent, "CorridorWall", new Vector3(x, 4, 91), new Vector3(1, 8, 218), wall);
                Box(parent, "Roof", new Vector3(0, 8.3f, 91), new Vector3(18, .6f, 218), Mat("Ceiling")); Box(parent, "LobbyRearWall", new Vector3(0, 4, -18.5f), new Vector3(18, 8, 1), wall); Box(parent, "ExitWall", new Vector3(0, 4, 200.5f), new Vector3(18, 8, 1), wall);
                foreach (float z in new[] { 30f, 82f, 132f }) foreach (float x in new[] { -6f, 6f }) Box(parent, "SideCover", new Vector3(x, .7f, z), new Vector3(2, 1.4f, 2), metal);
                foreach (float z in new[] { 65f, 115f, 170f }) Box(parent, "WallPillar", new Vector3(z == 115 ? -7.3f : 7.3f, 3.5f, z), new Vector3(1, 7, 2), wall);
                var player = All<PlayerCharacterController>(scene).Single(); player.transform.SetPositionAndRotation(new Vector3(0, .03f, -12), Quaternion.identity); PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
                var entrance = Box(null, "ChaseEntranceGate", new Vector3(0, 4, 0), new Vector3(16, 8, .6f), stripe); entrance.SetActive(false);
                var exit = Box(null, "ChaseExitGate", new Vector3(0, 4, 188), new Vector3(16, 8, .6f), stripe);
                foreach (float z in new[] { 48f, 98f, 148f })
                {
                    foreach (float edge in new[] { z - .15f, z + 4.15f }) Box(parent, "FallWarningEdge", new Vector3(0, .015f, edge), new Vector3(16, .03f, .3f), stripe, false);
                    Box(parent, "PitGlow", new Vector3(0, -6, z + 2), new Vector3(16, .1f, 4), Mat("LampRed"), false);
                    var hazard = new GameObject("ChaseGap_" + z, typeof(BoxCollider), typeof(Rigidbody), typeof(GapFallRecovery)); hazard.transform.position = new Vector3(0, -4, z + 2); hazard.GetComponent<BoxCollider>().size = new Vector3(16, 5, 4); hazard.GetComponent<BoxCollider>().isTrigger = true; hazard.GetComponent<Rigidbody>().isKinematic = true; hazard.GetComponent<Rigidbody>().useGravity = false;
                    var points = Point(null, "ReturnPoints_" + z, Vector3.zero); var xs = new[] { -5f, 0f, 5f }; var recovery = hazard.GetComponent<GapFallRecovery>(); recovery.Configure(player, xs.Select(x => Point(points, "NearReturn", new Vector3(x, .05f, z - 4))).ToArray(), xs.Select(x => Point(points, "FarReturn", new Vector3(x, .05f, z + 8))).ToArray());
                    var config = new SerializedObject(recovery); config.FindProperty("upperFloorHeight").floatValue = 0; config.FindProperty("gapNearZ").floatValue = z; config.FindProperty("gapFarZ").floatValue = z + 4; config.FindProperty("halfWidth").floatValue = 8; config.ApplyModifiedPropertiesWithoutUndo();
                }
                var mesh = SawMesh(); var carriage = Point(null, "GiantSawCarriage", new Vector3(0, 0, 2.5f)); var blades = new List<Transform>();
                foreach (float x in new[] { -5f, 0f, 5f })
                {
                    var blade = new GameObject("GiantSaw", typeof(MeshFilter), typeof(MeshRenderer)); blade.transform.SetParent(carriage, false); blade.transform.localPosition = new Vector3(x, 2.7f, 0); blade.GetComponent<MeshFilter>().sharedMesh = mesh; blade.GetComponent<MeshRenderer>().sharedMaterial = metal; blades.Add(blade.transform);
                    var hub = Box(blade.transform, "PowerHub", blade.transform.position, new Vector3(.65f, .65f, .4f), Mat("LampRed"), false);
                }
                Box(carriage, "MachineFrame", new Vector3(0, 5.8f, 2.5f), new Vector3(16, .35f, .6f), stripe, false);
                var motor = carriage.gameObject.AddComponent<AudioSource>(); motor.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/IronRequiem/Audio/SFX/SawBot/SawMotor_Attack_Loop.wav"); motor.playOnAwake = false; motor.loop = true; motor.volume = .65f; motor.spatialBlend = .7f; motor.minDistance = 6; motor.maxDistance = 50; motor.outputAudioMixerGroup = AudioUtility.GetAudioGroup(AudioUtility.AudioGroups.EnemyAttack);
                var go = new GameObject("ChaseEncounter", typeof(BoxCollider), typeof(Rigidbody), typeof(EnemySpawnQueue), typeof(ChaseCorridorEncounter)); go.transform.position = new Vector3(0, 2, 10); go.GetComponent<BoxCollider>().size = new Vector3(16, 4, 2); go.GetComponent<BoxCollider>().isTrigger = true; go.GetComponent<Rigidbody>().isKinematic = true; go.GetComponent<Rigidbody>().useGravity = false;
                var queue = go.GetComponent<EnemySpawnQueue>(); queue.Configure(1, 1); var positions = Point(null, "ChaseEnemySpawns", Vector3.zero);
                var zones = new[] { new ChaseCorridorEncounter.Ambush { ActivationZ = 23, Prefabs = new[] { t07, t07 }, Points = new[] { Point(positions, "T07_A", new Vector3(-4.5f, 0, 34)), Point(positions, "T07_B", new Vector3(4.5f, 0, 34)) } }, new ChaseCorridorEncounter.Ambush { ActivationZ = 75, Prefabs = new[] { sawBot }, Points = new[] { Point(positions, "SawBot", new Vector3(-4, 0, 86)) } }, new ChaseCorridorEncounter.Ambush { ActivationZ = 125, Prefabs = new[] { t07, t07 }, Points = new[] { Point(positions, "T07_C", new Vector3(-4.5f, 0, 136)), Point(positions, "T07_D", new Vector3(4.5f, 0, 136)) } } };
                go.GetComponent<ChaseCorridorEncounter>().Configure(player, All<CombatLabMenu>(scene).Single(), queue, entrance, exit, carriage, blades.ToArray(), motor, message, zones);
                int pickupIndex = 0; foreach (var pickup in All<HealthPickup>(scene)) { var locations = new[] { new Vector3(-5, .5f, 39), new Vector3(5, .5f, 89), new Vector3(-5, .5f, 139), new Vector3(5, .5f, 176) }; pickup.transform.position = locations[pickupIndex++ % locations.Length]; PrefabUtility.RecordPrefabInstancePropertyModifications(pickup.transform); }
                var font = message.font;
                void Sign(string name, string text, Vector3 p) { var label = new GameObject(name).AddComponent<TextMeshPro>(); label.font = font; label.text = text; label.fontSize = 5; label.color = new Color(.2f, .9f, 1); label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(14, 3); label.transform.position = p; }
                Sign("EntranceSign", "03 / PERSECUCIÓN\nUNA VEZ DENTRO, AVANZA", new Vector3(0, 5.8f, 9)); Sign("ExitSign", "SALIDA", new Vector3(0, 4, 199.5f));
                foreach (float z in new[] { -8f, 18f, 40f, 63f, 87f, 113f, 137f, 163f, 187f })
                { Box(parent, "CeilingLamp", new Vector3(0, 7.5f, z), new Vector3(3, .15f, .5f), Mat("LampCold"), false); var light = new GameObject("CorridorLight").AddComponent<Light>(); light.type = LightType.Point; light.shadows = LightShadows.None; light.range = 24; light.intensity = 4; light.color = new Color(.65f, .82f, 1); light.transform.position = new Vector3(0, 7, z); }
                Physics.SyncTransforms(); var settings = NavMesh.GetSettingsByID(0); settings.agentRadius = 2.25f; settings.agentHeight = 3.05f; settings.agentClimb = .25f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f; var sources = new List<NavMeshBuildSource>(); NavMeshBuilder.CollectSources(parent, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
                var nav = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(new Vector3(0, 2, 91), new Vector3(22, 18, 224)), Vector3.zero, Quaternion.identity); if (!nav) throw new Exception("Corridor navigation failed"); AssetDatabase.CreateAsset(nav, Art + "/CorridorNavMesh.asset"); var surface = geometry.AddComponent<Unity.AI.Navigation.NavMeshSurface>(); surface.agentTypeID = 0; surface.collectObjects = Unity.AI.Navigation.CollectObjects.Children; surface.navMeshData = nav; surface.AddData();
                LabelRestart(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); var entries = EditorBuildSettings.scenes.ToList(); if (!entries.Any(s => s.path == Lab)) entries.Add(new EditorBuildSettingsScene(Lab, true)); EditorBuildSettings.scenes = entries.ToArray();
                File.WriteAllText(Report + "/build.txt", "Independent corridor218m total/16m wide; pursuit entrance10m, finish184m. Three4m gaps at48/98/148. Three shared24-tooth6.2m saw discs,8m/s,2s anticipation,240rpm,100HP contact/.35s. Four current T07 and one SawBot, staggered through existing EnemySpawnQueue. Four original health pickups repositioned. All settings serialized; no enemy/player stats changed.");
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }
        static void ConnectPrevious()
        {
            const string previousPath = "Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity"; var scene = SceneManager.GetSceneByPath(previousPath); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(previousPath, OpenSceneMode.Additive);
            try
            {
                if (All<ChaseSceneEntrance>(scene).Length > 0) return; var active = SceneManager.GetActiveScene(); SceneManager.SetActiveScene(scene);
                try
                {
                    var door = Box(null, "Sector03Door", new Vector3(0, 11.5f, 25), new Vector3(8, 7, .5f), Mat("Hazard"));
                    foreach (float x in new[] { -4.4f, 4.4f }) Box(null, "Sector03Frame", new Vector3(x, 11.5f, 25), new Vector3(.7f, 7, 1), Mat("RustMetal"));
                    var trigger = new GameObject("Sector03Entrance", typeof(BoxCollider), typeof(Rigidbody), typeof(ChaseSceneEntrance)); trigger.transform.position = new Vector3(0, 10, 24); trigger.GetComponent<BoxCollider>().size = new Vector3(8, 4, 1); trigger.GetComponent<BoxCollider>().isTrigger = true; trigger.GetComponent<Rigidbody>().isKinematic = true; trigger.GetComponent<Rigidbody>().useGravity = false; trigger.GetComponent<ChaseSceneEntrance>().Configure(All<CombatTwoRoomsLab>(scene).Single(), door);
                    var label = new GameObject("Sector03Sign").AddComponent<TextMeshPro>(); label.font = All<TMP_Text>(scene).First(t => t.font).font; label.text = "SECTOR 03\nPERSECUCIÓN"; label.fontSize = 4; label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(8, 3); label.transform.position = new Vector3(0, 13.3f, 24.6f);
                    EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); File.WriteAllText(Report + "/connection.txt", "Upper room exit at(0,8,24) unlocks only after existing completion. Loads ChaseCorridorLab into play; original MainScene and CombatTwoRoomsLab untouched.");
                }
                finally { SceneManager.SetActiveScene(active); }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        static void LabelRestart(Scene scene)
        {
            var panel = (GameObject)new SerializedObject(All<CombatLabMenu>(scene).Single()).FindProperty("deathPanel").objectReferenceValue;
            foreach (var label in panel.GetComponentsInChildren<TMP_Text>(true)) if (label.text.Contains("CUARTO 1")) { label.text = "REINICIAR PERSECUCIÓN"; EditorUtility.SetDirty(label); }
        }
        static void ReserveUpperTurret()
        {
            const string path = "Assets/IronRequiem/Scenes/CombatTwoFloorsLab.unity"; var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.isLoaded; if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var waves = new SerializedObject(All<CombatTwoRoomsLab>(scene).Single()); var groups = waves.FindProperty("waves").GetArrayElementAtIndex(3).FindPropertyRelative("Groups"); Transform fixedPoint = null;
                for (int i = 0; i < groups.arraySize; i++) { var p = groups.GetArrayElementAtIndex(i).FindPropertyRelative("FixedPoint").objectReferenceValue as Transform; if (p) fixedPoint = p; }
                if (!fixedPoint) throw new Exception("Missing upper turret point"); var slots = waves.FindProperty("secondRoomSpawns"); string result = "";
                for (int i = 0; i < slots.arraySize; i++)
                {
                    var point = slots.GetArrayElementAtIndex(i).objectReferenceValue as Transform; if (!point || Vector3.Distance(point.position, fixedPoint.position) > .2f) continue;
                    result += point.name + ": " + point.position + " -> (-7,8,20)\n"; point.position = new Vector3(-7, 8, 20); EditorUtility.SetDirty(point);
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); File.WriteAllText(Report + "/turret-reservation.txt", result + "Fixed turret point is now separate from general enemy slots; quantities/compositions unchanged.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
