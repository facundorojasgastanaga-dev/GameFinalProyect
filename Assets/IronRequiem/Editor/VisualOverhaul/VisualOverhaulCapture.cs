using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace IronRequiem.Editor
{
    // Evidence renders of prefabs in an isolated preview scene (nothing is added to or saved in user scenes).
    public static class VisualOverhaulCapture
    {
        public static string Shots => VisualOverhaulDriver.Reports + "/Shots";

        // args: "prefabPath|out.png|camPos x,y,z|lookAt x,y,z[|lightIntensity]" (camera space relative to the prefab root)
        public static string Prefab(string args)
        {
            var a = args.Split('|'); var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(a[0]);
            Vector3 V(string s) { var p = s.Split(',').Select(float.Parse).ToArray(); return new Vector3(p[0], p[1], p[2]); }
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var camGo = new GameObject("Cam"); SceneManagerMove(camGo, scene); var cam = camGo.AddComponent<Camera>(); cam.scene = scene;
                cam.transform.position = V(a[2]); cam.transform.LookAt(V(a[3])); cam.fieldOfView = 40; cam.nearClipPlane = .01f; cam.farClipPlane = 100;
                cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .055f, .06f);
                var data = cam.GetUniversalAdditionalCameraData(); data.renderPostProcessing = false;
                float k = a.Length > 4 ? float.Parse(a[4]) : 1;
                var key = new GameObject("Key"); SceneManagerMove(key, scene); var l = key.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.6f * k; l.color = new Color(1f, .96f, .9f); key.transform.rotation = Quaternion.Euler(35, -40, 0);
                var fill = new GameObject("Fill"); SceneManagerMove(fill, scene); var f = fill.AddComponent<Light>(); f.type = LightType.Point; f.intensity = 3 * k; f.range = 12; f.color = new Color(.6f, .75f, 1f); fill.transform.position = V(a[2]) + new Vector3(-1, 1, 0);
                var rim = new GameObject("Rim"); SceneManagerMove(rim, scene); var rl = rim.AddComponent<Light>(); rl.type = LightType.Directional; rl.intensity = .8f * k; rl.color = new Color(.5f, .7f, 1f); rim.transform.rotation = Quaternion.Euler(20, 160, 0);
                FrameGrab.Save(cam, a[1].Contains("/") ? a[1] : Shots + "/" + a[1], 960, 540);
                return "saved " + a[1];
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void SceneManagerMove(GameObject g, UnityEngine.SceneManagement.Scene s) => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, s);

        // args: "scenePath|prefix|x,y,z>x,y,z|..." Renders from the scene player camera (moved temporarily; the scene is
        // reopened afterwards without saving).
        public static string Scene(string args)
        {
            var a = args.Split(new[] { (char)124 }); var setup = EditorSceneManager.GetSceneManagerSetup();
            EditorSceneManager.OpenScene(a[0], OpenSceneMode.Single);
            Vector3 V(string t) { var q = t.Split((char)44).Select(float.Parse).ToArray(); return new Vector3(q[0], q[1], q[2]); }
            var cam = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Base && c.name == "Main Camera");
            var stack = cam.GetUniversalAdditionalCameraData().cameraStack; var saved = stack.ToList(); stack.Clear();
            int n = 0;
            for (int i = 2; i < a.Length; i++)
            {
                var pl = a[i].Split((char)62); cam.transform.position = V(pl[0]); cam.transform.LookAt(V(pl[1])); cam.fieldOfView = 70;
                FrameGrab.Save(cam, Shots + "/" + a[1] + "-" + (i - 1) + ".png", 1280, 720); n++;
            }
            stack.AddRange(saved);
            EditorSceneManager.OpenScene(VisualOverhaulScenes.Lab, OpenSceneMode.Single);
            return "shots " + n;
        }

        public static string Many(string list) => string.Join("\n", list.Split(';').Where(s => s.Trim().Length > 0).Select(s => { try { return Prefab(s.Trim()); } catch (System.Exception e) { return "ERR " + s + " " + e.Message; } }));
    }
}
