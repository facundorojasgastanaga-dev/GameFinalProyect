using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using Unity.FPS.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IronRequiem.Editor
{
    public static class T07ScenePromotion
    {
        const string Reports=T07AnimationBuilder.Reports;
        internal static void InScratchScene(Action action)
        {
            var previous=SceneManager.GetActiveScene();
            var scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scratch);
            try{action();}
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scratch,true);}
        }
        internal static void RestoreTransientDirtyFlags()
        {
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);if(!scene.isDirty)continue;
                if(string.IsNullOrEmpty(scene.path))throw new InvalidOperationException("An unsaved user scene must be preserved.");
                string copy=Path.GetFullPath(Reports+"/memory-"+Path.GetFileName(scene.path));
                if(!EditorSceneManager.SaveScene(scene,copy,true))throw new InvalidOperationException("Cannot snapshot dirty scene without saving its source.");
                if(!File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(scene.path)))
                    throw new InvalidOperationException("Dirty scene differs from its disk copy; preserve user changes: "+scene.path);
                if(SceneManager.sceneCount!=1)throw new InvalidOperationException("Restore transient state only with one verified scene open.");
                string original=scene.path;
                EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
                File.AppendAllText(Reports+"/transient-scenes.txt",DateTime.UtcNow.ToString("O")+" byte-identical memory snapshot, reopened unchanged scene: "+original+Environment.NewLine);
            }
        }
        public static void ReplaceScene(string path,string oldPrefab,string newPrefab)
        {
            T07AnimationBuilder.RequireClean();var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var matches=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EnemyController>(true))
                    .Where(e=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(e.gameObject)==oldPrefab).ToArray();
                foreach(var enemy in matches)
                {
                    var transform=enemy.transform;var position=transform.position;var rotation=transform.rotation;var scale=transform.localScale;
                    var patrols=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PatrolPath>(true)).ToArray();
                    var references=patrols.SelectMany(p=>p.EnemiesToAssign).Count(e=>e==enemy);
                    PrefabUtility.ReplacePrefabAssetOfPrefabInstance(enemy.gameObject,AssetDatabase.LoadAssetAtPath<GameObject>(newPrefab),
                        new PrefabReplacingSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,prefabOverridesOptions=PrefabOverridesOptions.KeepAllPossibleOverrides,changeRootNameToAssetName=false},InteractionMode.AutomatedAction);
                    if(transform.position!=position || transform.rotation!=rotation || transform.localScale!=scale || patrols.SelectMany(p=>p.EnemiesToAssign).Count(e=>e==enemy)!=references)
                        throw new InvalidOperationException("Transforms or patrol references changed during replacement.");
                }
                if(matches.Length>0 && !EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Cannot save "+path);
                File.AppendAllText(Reports+"/scene-migration.txt",DateTime.UtcNow.ToString("O")+" "+path+" instances="+matches.Length+" "+oldPrefab+" -> "+newPrefab+Environment.NewLine);
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        public static void Promote()
        {
            T07AnimationBuilder.RequireClean();
            foreach(string mode in new[]{"play","edit"})
            {
                var document=new XmlDocument();document.Load(Reports+"/"+mode+".xml");
                if(document.DocumentElement.GetAttribute("result")!="Passed")throw new InvalidOperationException("Lab "+mode+" verification must pass before promotion.");
            }
            const string scene="Assets/IronRequiem/Scenes/CharacterLab.unity";
            const string backup="Tools/IronRequiem/Backups/20261030-t07-animation-vfx/";
            if(!File.ReadAllBytes(scene).SequenceEqual(File.ReadAllBytes(backup+scene)))throw new InvalidOperationException("CharacterLab changed since backup; inspect user changes before replacing.");
            InScratchScene(()=>
            {
                var root=PrefabUtility.LoadPrefabContents(T07OptimizationBuilder.Enemy);
                try{T07AnimationAuthoring.ConfigureEnemy(root,false);PrefabUtility.SaveAsPrefabAsset(root,T07OptimizationBuilder.Enemy);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            });
            RestoreTransientDirtyFlags();
            ReplaceScene(T07OptimizationBuilder.Lab,T07AnimationBuilder.Draft,T07OptimizationBuilder.Enemy);
            ReplaceScene(scene,CharacterPrototypeBuilder.EnemyPath,T07OptimizationBuilder.Enemy);
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
