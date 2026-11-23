using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.Editor.Map01
{
    /// Map01 F2 · Estructura (R1). Vista cenital ortográfica de cada cuarto, sin techos, con los marcadores pintados
    /// (slots rojos, slots grandes naranjas, anclajes amarillos, vanos cian). Todo lo temporal es HideAndDontSave y se
    /// destruye al terminar; los techos se ocultan con Renderer.forceRenderingOff (no serializado), así que no se guarda nada.
    public static class StructureCapture
    {
        const string Menu = Map01Paths.MenuRoot + "Structure/";
        public const string CaptureFolder = Map01Paths.ReportRoot + "/captures";
        const int LongSide = 2048;

        [MenuItem(Menu + "Capture overviews", priority = 40)]
        public static void CaptureAllMenu()
        {
            var log = new StringBuilder();
            log.AppendLine("Map01 Structure · capturas · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            Directory.CreateDirectory(CaptureFolder);
            foreach (var room in StructureAuthoring.LoadRooms())
            {
                if (!File.Exists(Map01Paths.ScenePath(room.scene))) { log.AppendLine(room.id + ": falta la escena (Structure/Build all); sin captura"); continue; }
                var scene = Map01Scenes.OpenRoom(room, false);
                var root = Map01Hierarchy.EnsureRoomRoot(scene, room);
                string file = CaptureRoom(room, root);
                log.AppendLine(room.id + ": " + file);
            }
            Map01Report.Write("structure_captures.txt", log.ToString());
        }

        static string CaptureRoom(StructureRoom room, Transform root)
        {
            var temps = new List<UnityEngine.Object>();
            var hidden = new List<Renderer>();
            RenderTexture rt = null; Texture2D tex = null; var previousActive = RenderTexture.active;
            try
            {
                // Ocultar techos (Envelope/Ceiling_*).
                var structure = Map01Hierarchy.Layer(root, Map01Layers.Structure);
                foreach (var r in structure.GetComponentsInChildren<Renderer>(true))
                    if (IsCeiling(r.transform) && !r.forceRenderingOff) { r.forceRenderingOff = true; hidden.Add(r); }

                var local = Map01Math.ToBounds(room.aabbLocal);
                var world = Map01Math.ToWorld(local, room);
                float aspect = world.size.x / Mathf.Max(0.01f, world.size.z);
                int w = aspect >= 1f ? LongSide : Mathf.Max(64, Mathf.RoundToInt(LongSide * aspect));
                int h = aspect >= 1f ? Mathf.Max(64, Mathf.RoundToInt(LongSide / aspect)) : LongSide;

                var camGo = Temp(new GameObject("Map01_CaptureCamera"), temps);
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                cam.orthographicSize = world.size.z / 2f + 1f;
                cam.aspect = w / (float)h;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.06f, 0.06f, 0.07f, 1f);
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = world.size.y + 20f;
                camGo.transform.SetPositionAndRotation(new Vector3(world.center.x, world.max.y + 5f, world.center.z), Quaternion.Euler(90f, 0f, 0f));

                var lightGo = Temp(new GameObject("Map01_CaptureLight"), temps);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.None;
                lightGo.transform.rotation = Quaternion.Euler(60f, 30f, 0f);

                DrawMarkers(room, root, structure, temps);

                rt = new RenderTexture(w, h, 24);
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                string path = CaptureFolder + "/" + room.id + ".png";
                File.WriteAllBytes(path, tex.EncodeToPNG());
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                foreach (var r in hidden) if (r) r.forceRenderingOff = false;
                foreach (var o in temps.Where(o => o)) UnityEngine.Object.DestroyImmediate(o);
                if (rt) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (tex) UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static bool IsCeiling(Transform t)
        {
            for (var p = t; p; p = p.parent)
            {
                if (p.name.StartsWith("Ceiling", StringComparison.Ordinal)) return true;
                if (p.name == StructureAuthoring.GeometryName) return false;
            }
            return false;
        }

        static void DrawMarkers(StructureRoom room, Transform root, Transform structure, List<UnityEngine.Object> temps)
        {
            var red = Unlit(new Color(0.95f, 0.15f, 0.12f), temps);
            var orange = Unlit(new Color(1f, 0.55f, 0.1f), temps);
            var yellow = Unlit(new Color(1f, 0.92f, 0.3f), temps);
            var cyan = Unlit(new Color(0.2f, 0.9f, 1f), temps);
            foreach (var z in room.spawnZones ?? new List<SpawnZone>())
            {
                bool big = z.kind == "big";
                foreach (var t in Map01Markers.SlotsOf(structure, z.id))
                    Dot(t.position + Vector3.up * 0.2f, big ? 5f : 1.6f, big ? orange : red, temps);
            }
            var anchors = Map01Markers.Find(structure, Map01Markers.Anchors);
            if (anchors) foreach (Transform t in anchors) Dot(t.position, 0.8f, yellow, temps);
            var openings = Map01Markers.Find(structure, Map01Markers.Openings);
            if (openings) foreach (Transform t in openings) Dot(t.position + Vector3.up * 0.3f, 1.2f, cyan, temps);
        }

        static Material Unlit(Color color, List<UnityEngine.Object> temps)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            temps.Add(m);
            return m;
        }

        static void Dot(Vector3 position, float diameter, Material mat, List<UnityEngine.Object> temps)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>(); if (col) UnityEngine.Object.DestroyImmediate(col);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.transform.position = position;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            temps.Add(go);
        }

        static GameObject Temp(GameObject go, List<UnityEngine.Object> temps)
        {
            go.hideFlags = HideFlags.HideAndDontSave;
            temps.Add(go);
            return go;
        }
    }
}
