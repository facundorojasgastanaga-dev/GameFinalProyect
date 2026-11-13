using System.IO;
using UnityEngine;

namespace IronRequiem
{
    // Evidence capture for tests and Editor tools (allocates; never used by gameplay). Renders a base camera, with
    // its URP overlay stack (FPS weapon camera), into a temporary render texture and writes a PNG. Does not need
    // the Game view or window focus.
    public static class FrameGrab
    {
        public static void Save(Camera camera, string path, int width = 1280, int height = 720)
        {
            var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            try
            {
                camera.targetTexture = rt; camera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            }
            finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
        }
    }
}
