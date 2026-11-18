using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    /// MAP_FX_AUDIT r2, approved M7+M8+M9. The campaign rooms have uncommitted user changes, so the originals are never
    /// opened for editing or saved: each fix goes into an Editor copy "<Room>_v2.unity" (AssetDatabase.CopyAsset, new
    /// GUID) with its own NavMesh asset "<Room>_v2_NavMesh.asset". Replaced objects are deactivated, not deleted, and the
    /// new ones carry an "_M7/_M8/_M9" suffix. Idempotent: rerunning finds the existing copy and fix objects.
    /// Queue: "IronCampaignMapFxRooms.Apply" (all) or "IronCampaignMapFxRooms.Apply Room02_Breach".
    /// Build Settings and CampaignFlow are not touched (the copies are not played by the campaign until the user swaps them).
    public static class IronCampaignMapFxRooms
    {
        const string Folder = "Assets/IronRequiem/Scenes/Campaign", Art = "Assets/IronRequiem/Art/Levels/Campaign";
        public const string Report = "Tools/IronRequiem/Reports/MapFxRooms";
        static StringBuilder log;

        public static string Apply(string args)
        {
            if (EditorApplication.isPlaying) return "refused: leave Play Mode first.";
            for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) return "refused: an open scene has unsaved changes.";
            var setup = EditorSceneManager.GetSceneManagerSetup();
            log = new StringBuilder("IronCampaignMapFxRooms.Apply " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n");
            var rooms = string.IsNullOrWhiteSpace(args) ? new[] { "Room01_Foundry", "Room02_Breach", "Room04_Reactor" } : new[] { args.Trim() };
            try
            {
                foreach (var room in rooms)
                {
                    log.AppendLine("== " + room);
                    var scene = OpenCopy(room);
                    if (room == "Room01_Foundry") FixRoom01(scene);
                    else if (room == "Room02_Breach") FixRoom02(scene);
                    else if (room == "Room04_Reactor") FixRoom04(scene);
                    else throw new ArgumentException("unknown room " + room);
                    Rebake(scene, room);
                    if (!EditorSceneManager.SaveScene(scene)) throw new Exception("save failed " + scene.path);
                    log.AppendLine("  saved " + scene.path);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
                Directory.CreateDirectory(Report); File.WriteAllText(Report + "/apply.txt", log.ToString());
            }
            return log.ToString();
        }

        static Scene OpenCopy(string room)
        {
            string src = Folder + "/" + room + ".unity", dst = Folder + "/" + room + "_v2.unity";
            if (!File.Exists(dst))
            {
                if (!AssetDatabase.CopyAsset(src, dst)) throw new Exception("copy failed " + src);
                log.AppendLine("  copied " + src + " -> " + dst + " (guid " + AssetDatabase.AssetPathToGUID(dst) + ")");
            }
            else log.AppendLine("  reusing existing copy " + dst);
            return EditorSceneManager.OpenScene(dst, OpenSceneMode.Single);
        }

        static Transform Find(Scene scene, string name, string parent = null)
        {
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .Where(t => t.name == name && (parent == null || (t.parent && t.parent.name == parent))).ToArray();
            if (all.Length != 1) throw new Exception("expected one '" + name + "' (parent " + parent + "), found " + all.Length);
            return all[0];
        }
        static Transform Existing(Transform parent, string name) { var t = parent.Find(name); return t; }

        // Clone a box-like object (no children) with a new world centre and world size, same material and collider.
        static Transform CloneBox(Transform source, string name, Vector3 worldCentre, Vector3 worldSize)
        {
            var existing = Existing(source.parent, name);
            if (existing) { log.AppendLine("  exists " + name); return existing; }
            if (source.childCount != 0) throw new Exception(source.name + " has children; refusing to clone");
            var go = Object.Instantiate(source.gameObject, source.parent); go.name = name; go.SetActive(true);
            Undo.RegisterCreatedObjectUndo(go, name);
            var t = go.transform; t.position = worldCentre; t.rotation = source.rotation;
            var ps = source.parent ? source.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(worldSize.x / ps.x, worldSize.y / ps.y, worldSize.z / ps.z);
            log.AppendLine("  created " + name + " centre=" + worldCentre.ToString("F2") + " size=" + worldSize.ToString("F2") + " from " + source.name);
            return t;
        }
        static void Disable(Transform t)
        {
            if (!t.gameObject.activeSelf) { log.AppendLine("  already inactive " + t.name); return; }
            Undo.RecordObject(t.gameObject, "disable " + t.name); t.gameObject.SetActive(false);
            log.AppendLine("  deactivated (kept) " + t.name + " at " + t.position.ToString("F2"));
        }

        // M7: 54 x 7.5 m strip with no floor between the near bank (ends z -26) and the south wall (inner face z -33.5).
        // The fill sits at y 7.5..8 so it stays above the persistent lobby roof (top y 7.5) underneath.
        static void FixRoom02(Scene scene)
        {
            var bank = Find(scene, "UpperNearBank", "TwoFloorsGeometry");
            CloneBox(bank, "UpperSouthFill_M7", new Vector3(0, 7.75f, -29.75f), new Vector3(54, .5f, 7.5f));
        }

        // M8: Room01's SouthWall (z -34.5..-33.5) stands 1 m inside the persistent lobby and through the weapon mount
        // station (z -34.9..-33.3). Within the lobby span (x -7..7) the lobby's own LobbyRear (z -35.5..-34.5) already
        // closes the room, so the wall is split into the two side spans and the original kept inactive.
        static void FixRoom01(Scene scene)
        {
            var wall = Find(scene, "SouthWall", "TwoFloorsGeometry");
            float y = wall.position.y, z = wall.position.z, h = wall.lossyScale.y, d = wall.lossyScale.z;
            CloneBox(wall, "SouthWall_West_M8", new Vector3(-17, y, z), new Vector3(20, h, d));
            CloneBox(wall, "SouthWall_East_M8", new Vector3(17, y, z), new Vector3(20, h, d));
            Disable(wall);
        }

        // M9: the CeilingSupport columns at (±23, 129) stand inside Ramp_V35 (x -26.55..-21.05, z 119.8..131.4) and its
        // mirror, cutting the ramp. Moved to x ±18.5 (clear of ramps, catwalks and ceiling conduits); originals kept inactive.
        static void FixRoom04(Scene scene)
        {
            var parents = new[] { "ReactorGeometry", "V35_Open" };
            foreach (var name in new[] { "CeilingSupport", "CeilingSupport_V35Ext" })
            {
                var cols = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                    .Where(t => t.name == name && t.parent && parents.Contains(t.parent.name)).ToArray();
                if (cols.Length != 2) throw new Exception("expected two " + name + ", found " + cols.Length);
                foreach (var c in cols)
                {
                    float side = Mathf.Sign(c.position.x);
                    CloneBox(c, name + (side < 0 ? "_West_M9" : "_East_M9"), new Vector3(side * 18.5f, c.position.y, c.position.z), c.lossyScale);
                    Disable(c);
                }
            }
        }

        // NavMesh: same settings as the last room bake (LevelOpenPass.RebuildNav, v35), written to a new asset per copy.
        static void Rebake(Scene scene, string room)
        {
            var surface = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>(true)).FirstOrDefault();
            if (!surface || !surface.navMeshData) throw new Exception("no NavMeshSurface with data in " + room);
            string original = AssetDatabase.GetAssetPath(surface.navMeshData), target = Art + "/" + room + "_v2_NavMesh.asset";
            if (original != target)
            {
                if (!File.Exists(target) && !AssetDatabase.CopyAsset(original, target)) throw new Exception("navmesh copy failed");
                surface.RemoveData(); Undo.RecordObject(surface, "navmesh v2"); surface.navMeshData = AssetDatabase.LoadAssetAtPath<NavMeshData>(target); surface.AddData();
                log.AppendLine("  navmesh asset " + original + " -> " + target);
            }
            var before = Tri();
            var settings = NavMesh.GetSettingsByID(surface.agentTypeID); settings.agentRadius = 2.25f; settings.agentHeight = 3.05f; settings.agentClimb = .25f; settings.agentSlope = 35; settings.overrideVoxelSize = true; settings.voxelSize = .12f;
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>(); NavMeshBuilder.CollectSources(surface.transform, -1, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            var bounds = surface.navMeshData.sourceBounds;
            bool ok = NavMeshBuilder.UpdateNavMeshData(surface.navMeshData, settings, sources, bounds);
            surface.RemoveData(); surface.AddData(); EditorUtility.SetDirty(surface.navMeshData); EditorUtility.SetDirty(surface); AssetDatabase.SaveAssets();
            log.AppendLine("  navmesh rebake " + (ok ? "ok" : "FAILED") + " sources=" + sources.Count + " bounds=" + bounds + " " + before + " -> " + Tri());
            if (!ok) throw new Exception("navmesh rebake failed " + room);
        }
        static string Tri()
        {
            var t = NavMesh.CalculateTriangulation(); float area = 0;
            for (int i = 0; i < t.indices.Length; i += 3) area += Vector3.Cross(t.vertices[t.indices[i + 1]] - t.vertices[t.indices[i]], t.vertices[t.indices[i + 2]] - t.vertices[t.indices[i]]).magnitude * .5f;
            return "verts=" + t.vertices.Length + " area=" + area.ToString("F0") + "m2";
        }
    }
}
