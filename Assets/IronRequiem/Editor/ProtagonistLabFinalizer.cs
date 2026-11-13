using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;
namespace IronRequiem.Editor
{
    public static class ProtagonistLabFinalizer
    {
        public static void PreserveCameraOverrides()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Preserving user scene edits");
            var scene=EditorSceneManager.OpenScene(ProtagonistLabBuilder.Lab);var player=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single();
            var preview=EditorSceneManager.OpenPreviewScene(CombatV03Tools.Main);
            int copied=0;
            try
            {
                var original=preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<PlayerCharacterController>(true)).Single();var map=new Dictionary<Object,Object>();
                foreach(var source in original.GetComponentsInChildren<Transform>(true))
                {
                    string path=AnimationUtility.CalculateTransformPath(source,original.transform);var target=path==""?player.transform:player.transform.Find(path);if(!target)continue;
                    target.localPosition=source.localPosition;target.localRotation=source.localRotation;target.localScale=source.localScale;
                    map[source]=target;map[source.gameObject]=target.gameObject;
                    foreach(var component in source.GetComponents<Component>().Where(c=>c))
                    {
                        var destination=target.GetComponent(component.GetType());if(destination)map[component]=destination;
                        if(destination&&(component is Camera||component is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)){EditorUtility.CopySerialized(component,destination);copied++;}
                    }
                }
                foreach(var component in player.GetComponentsInChildren<Component>(true).Where(c=>c&&(c is Camera||c is UnityEngine.Rendering.Universal.UniversalAdditionalCameraData)))
                {
                    var so=new SerializedObject(component);var property=so.GetIterator();while(property.Next(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.objectReferenceValue&&map.TryGetValue(property.objectReferenceValue,out var target))property.objectReferenceValue=target;so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save own integration laboratory");
            int missing=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new InvalidOperationException("Missing scripts in laboratory: "+missing);
            File.WriteAllText(ProtagonistIntegrationTools.Output+"/final-audit.txt","Preserved "+copied+" camera/URP data components and existing camera/socket local transforms from a read-only MainScene preview. Missing scripts=0. Only integration laboratory saved.");
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
