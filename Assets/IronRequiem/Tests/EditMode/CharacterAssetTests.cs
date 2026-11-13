using System.Linq;
using IronRequiem.Editor;
using NUnit.Framework;
using Unity.FPS.AI;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronRequiem.Tests
{
    public sealed class CharacterAssetTests
    {
        [Test]
        public void PlayerRetainsTemplateControllerCameraAndWeaponReferences()
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.PlayerPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(go),Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(go.GetComponents<PlayerCharacterController>(),Has.Length.EqualTo(1));
            Assert.That(go.GetComponent<Rigidbody>(),Is.Null);
            Assert.That(go.GetComponent<PlayerInputHandler>(),Is.Not.Null);
            Assert.That(go.GetComponent<PlayerCharacterController>().PlayerCamera,Is.Not.Null);
            var weapons=go.GetComponent<PlayerWeaponsManager>();
            Assert.That(weapons.WeaponCamera,Is.Not.Null);Assert.That(weapons.WeaponParentSocket,Is.Not.Null);
            Assert.That(weapons.StartingWeapons.All(w=>w && w.ProjectilePrefab && w.WeaponMuzzle),Is.True);
            Assert.That(weapons.StartingWeapons.Count,Is.EqualTo(1),"Design update: pistol only; original shotgun assets remain intact");
        }

        [Test]
        public void EnemyHasIndependentDamageJointAndRealMaterialMaps()
        {
            var go=AssetDatabase.LoadAssetAtPath<GameObject>(CharacterPrototypeBuilder.EnemyPath);
            Assert.That(PrefabUtility.GetPrefabAssetType(go),Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(go.GetComponent<PlayerCharacterController>(),Is.Null);
            Assert.That(go.GetComponent<EnemyController>(),Is.Not.Null);Assert.That(go.GetComponent<EnemyMobile>(),Is.Not.Null);
            var joint=go.GetComponentInChildren<DetachableCannon>();
            Assert.That(joint.transform.position.y-go.transform.position.y,Is.GreaterThan(.8f),"Cannon joint must stay at the elbow, above ground");
            Assert.That(joint.GetComponent<Health>(),Is.Not.EqualTo(go.GetComponent<Health>()));
            Assert.That(joint.GetComponent<Damageable>(),Is.Not.Null);Assert.That(joint.GetComponent<Collider>(),Is.Not.Null);
            var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/IronRequiem/Art/Enemies/T07New/Materials/T07_URP.mat");
            Assert.That(mat.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
            foreach(var property in new[]{"_BaseMap","_BumpMap","_MetallicGlossMap"}) Assert.That(mat.GetTexture(property),Is.Not.Null);
            var meshes=AssetDatabase.LoadAllAssetsAtPath("Assets/IronRequiem/Art/Enemies/T07New/T07_Prepared.fbx").OfType<Mesh>().ToArray();
            Assert.That(meshes,Has.Length.EqualTo(2));Assert.That(meshes.All(m=>m.vertexCount>0 && m.bindposeCount==0),Is.True);
            Assert.That(meshes.Sum(m=>m.triangles.Length/3),Is.InRange(50000,52000));
        }

        [Test]
        public void LabHasOnePlayerEnemyBakedNavigationAndNoMissingScripts()
        {
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.OpenScene(CharacterPrototypeBuilder.ScenePath);
                var roots=scene.GetRootGameObjects();
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<PlayerCharacterController>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<EnemyController>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),Is.Zero);
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<Unity.AI.Navigation.NavMeshSurface>()).Single().navMeshData,Is.Not.Null);
            }
            finally { if(setup.Length>0 && setup.Any(s=>s.isLoaded))EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
