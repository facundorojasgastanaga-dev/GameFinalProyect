using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
namespace IronRequiem.Editor
{
    // Scope limited to explicit benchmark builds; this fixture has no inference models/workers.
    // The installed inference package and all game compute shaders are preserved.
    public sealed class PerformanceComputeStripper : IPreprocessComputeShaders
    {
        public int callbackOrder=>0;
        public void OnProcessComputeShader(ComputeShader shader,string kernelName,IList<ShaderCompilerData> data)
        {
            if(!Performance120Tools.Building&&!SecondaryShotgunAuthoring.Building)return;
            string path=AssetDatabase.GetAssetPath(shader);
            if(path.StartsWith("Packages/com.unity.ai.inference/")||path.Contains("com.unity.ai.inference@"))
            {if(Performance120Tools.Building)Performance120Tools.StrippedVariants+=data.Count;else SecondaryShotgunAuthoring.StrippedVariants+=data.Count;data.Clear();}
        }
    }
}
