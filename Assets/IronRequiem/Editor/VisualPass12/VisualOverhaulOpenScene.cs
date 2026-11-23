using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    /// Pase visual 12 · opens a scene in the Editor (single mode). Never discards unsaved changes: it stops instead.
    public static class VisualOverhaulOpenScene
    {
        public static string Open(string path)
        {
            path = (path ?? "").Trim();
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Hay cambios sin guardar en " + SceneManager.GetSceneAt(i).path);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            return "abierta " + path;
        }
    }
}
