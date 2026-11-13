using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Unity.FPS.Game;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace IronRequiem.Editor
{
    // Fixed, local authoring operations for this cannon. No arbitrary script endpoint or scene saves.
    [InitializeOnLoad]
    public static class CannonChargeUpgrade
    {
        public const string Reports = "Tools/IronRequiem/Reports/CannonCharge";
        public const string WeaponPath = T07OptimizationBuilder.Weapon;
        public const string Vfx = "Assets/IronRequiem/VFX/CannonCharge";
        public const string ChargePrefab = "Assets/IronRequiem/Prefabs/VFX/CannonCharge_Magenta.prefab";
        public const string ProjectilePath = "Assets/IronRequiem/Prefabs/Weapons/Projectile_T07_Magenta.prefab";
        // Fitted to the actual eight rim vertices; FBX -> Unity negates X and divides by 100.
        public static readonly Vector3 MouthPoint = new Vector3(.00009319516f, -.002265465f, -.005319517f);
        public static readonly Vector3 MouthNormal = new Vector3(.07086702f, -.5761503f, -.8142658f).normalized;
        const string Request = "Tools/IronRequiem/Requests/cannon-charge.json";
        static CannonChargeUpgrade() { EditorApplication.update += Tick; }
        static void Tick()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            string operation = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                if (operation == "inspect") Inspect();
                else if (operation == "build") Build();
                else throw new InvalidOperationException("Unknown cannon operation: " + operation);
                File.WriteAllText(Reports + "/authoring-status.txt", operation + " complete " + DateTime.UtcNow.ToString("O"));
            }
            catch (Exception e) { File.WriteAllText(Reports + "/authoring-status.txt", operation + " FAILED " + e); Debug.LogException(e); }
        }
        [MenuItem("Tools/Iron Requiem/Weapons/Align Equipped Cannon and Build Magenta Charge")]
        public static void Build()
        {
            T07AnimationBuilder.RequireClean();
            if (!File.Exists("Tools/IronRequiem/Backups/20261030-cannon-charge/baseline.json")) throw new InvalidOperationException("Missing task backup.");
            Directory.CreateDirectory(Vfx); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            T07ScenePromotion.InScratchScene(() =>
            {
                var coreMaterial = Material("EnergyCore", CoreTexture());
                var lineMaterial = Material("EnergyFrame", StripeTexture());
                var sparkMaterial = Material("EnergySparks", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/IronRequiem/VFX/Mechanics/ParticleSoftCircle.asset"));
                var effect = new GameObject("CannonCharge_Magenta");
                try
                {
                    var core = Child(effect.transform, "EnergyCore"); core.localScale = Vector3.one * .035f;
                    var halo = MeshNode(core, "HotCoreAndHalo", Quad(), coreMaterial);
                    halo.localScale = Vector3.one * 1.65f; halo.localPosition = Vector3.forward;
                    MeshNode(effect.transform, "OuterFrame", Square(), lineMaterial);
                    MeshNode(effect.transform, "InnerFrame", Square(), lineMaterial).localPosition = new Vector3(0, 0, .018f);
                    MeshNode(effect.transform, "EnergyRays", Rays(), lineMaterial);
                    MeshNode(effect.transform, "EnergySpindle", Spindle(), lineMaterial);
                    var sparks = Child(effect.transform, "ConvergingSparks").gameObject.AddComponent<ParticleSystem>();
                    ConfigureParticles(sparks, sparkMaterial);
                    effect.SetActive(false);
                    PrefabUtility.SaveAsPrefabAsset(effect, ChargePrefab);
                }
                finally { Object.DestroyImmediate(effect); }

                var projectileSource = AssetDatabase.LoadAssetAtPath<GameObject>(DesignV02Upgrade.ChargedProjectilePath);
                var projectile = (GameObject)PrefabUtility.InstantiatePrefab(projectileSource);
                try
                {
                    projectile.name = "Projectile_T07_Magenta";
                    if (!projectile.GetComponent<ProjectileMuzzleOrigin>()) projectile.AddComponent<ProjectileMuzzleOrigin>();
                    var visual = projectile.GetComponent<ChargedProjectileDamage>().EnergyVisual;
                    visual.GetComponent<Renderer>().sharedMaterial = lineMaterial;
                    var trail = projectile.GetComponent<TrailRenderer>(); trail.sharedMaterial = sparkMaterial;
                    trail.startColor = new Color(1, .12f, 1); trail.endColor = new Color(.6f, .02f, 1, 0);
                    PrefabUtility.SaveAsPrefabAsset(projectile, ProjectilePath);
                }
                finally { Object.DestroyImmediate(projectile); }

                var weaponGo = PrefabUtility.LoadPrefabContents(WeaponPath);
                try
                {
                    var weapon = weaponGo.GetComponent<WeaponController>();
                    var visual = weaponGo.GetComponentsInChildren<Transform>(true).Single(t => t.name == "StolenCannonVisual");
                    var sourceRotation = Quaternion.Euler(180, 0, 0);
                    visual.localRotation = Quaternion.AngleAxis(-45, Vector3.forward) * Quaternion.FromToRotation(sourceRotation * MouthNormal, Vector3.forward) * sourceRotation;
                    visual.localScale = Vector3.one * 100;
                    Vector3 desiredMouth = new Vector3(.015f, .065f, .48f);
                    visual.localPosition = desiredMouth - visual.localRotation * Vector3.Scale(MouthPoint, visual.localScale);
                    // Keep scale-one socket under WeaponRoot. It is derived from the geometry, never
                    // parented under the FBX's scale-100 mesh; particles remain in metres.
                    weapon.WeaponMuzzle.SetParent(weapon.WeaponRoot.transform, false);
                    weapon.WeaponMuzzle.localPosition = desiredMouth;
                    weapon.WeaponMuzzle.localRotation = Quaternion.identity;
                    weapon.WeaponMuzzle.localScale = Vector3.one;
                    weapon.AimOffset = new Vector3(.25f, .15f, 0); // Retain vertical lift and compensate the native 0.5 zoom; keep the heavy cannon beside the reticle.
                    var previous = weapon.WeaponMuzzle.Find("ChargeEnergy"); if (previous) Object.DestroyImmediate(previous.gameObject);
                    var charge = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChargePrefab), weapon.WeaponMuzzle);
                    charge.name = "ChargeEnergy"; charge.transform.localPosition = Vector3.zero; charge.transform.localRotation = Quaternion.identity; charge.transform.localScale = Vector3.one;
                    var presentation = weaponGo.GetComponent<CannonChargePresentation>();
                    presentation.Configure(charge, charge.transform.Find("EnergyCore"), charge.GetComponentInChildren<ParticleSystem>(true));
                    presentation.ConfigureFrames(charge.transform.Find("OuterFrame"), charge.transform.Find("InnerFrame"), charge.transform.Find("EnergyRays"), charge.transform.Find("EnergySpindle"));
                    weapon.ProjectilePrefab = AssetDatabase.LoadAssetAtPath<ProjectileBase>(ProjectilePath);
                    weapon.MuzzleFlashPrefab = null; // The presentation uses the same accepted-shot event.
                    PrefabUtility.SaveAsPrefabAsset(weaponGo, WeaponPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(weaponGo); }
            });
            T07ScenePromotion.RestoreTransientDirtyFlags();
            Inspect();
        }

        static Transform Child(Transform parent, string name)
        { var child = new GameObject(name).transform; child.SetParent(parent, false); return child; }
        static Transform MeshNode(Transform parent, string name, Mesh mesh, Material material)
        {
            var child = Child(parent, name); child.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return child;
        }
        static Material Material(string name, Texture2D texture)
        {
            string path = Vfx + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.name = name; material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", name == "EnergyCore" ? new Color(2.2f, 2.2f, 2.2f, 1) : new Color(2.4f, .65f, 3.2f, 1));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 2);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        static Texture2D CoreTexture()
        {
            const string path = Vfx + "/MagentaCore.asset"; var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (texture) return texture;
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true) { name = "MagentaCore", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
                Color c = Color.Lerp(new Color(.65f, .02f, 1), new Color(1, .85f, 1), Mathf.Exp(-radius * radius * 70));
                c.a = Mathf.Exp(-radius * radius * 7) * Mathf.Clamp01((1 - radius) * 8); texture.SetPixel(x, y, c);
            }
            texture.Apply(); AssetDatabase.CreateAsset(texture, path); return texture;
        }
        static Texture2D StripeTexture()
        {
            const string path = Vfx + "/EnergyStripe.asset"; var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (texture) return texture;
            texture = new Texture2D(4, 32, TextureFormat.RGBA32, false, true) { name = "EnergyStripe", wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 32; y++) for (int x = 0; x < 4; x++)
            {
                float distance = Mathf.Abs((y - 15.5f) / 15.5f);
                float alpha = Mathf.Exp(-distance * distance * 9) * Mathf.Clamp01((1 - distance) * 6);
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
            texture.Apply(); AssetDatabase.CreateAsset(texture, path); return texture;
        }
        static Mesh SaveMesh(string name, Vector3[] vertices, Vector2[] uv, int[] triangles)
        {
            string path = Vfx + "/" + name + ".asset"; var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh) { mesh = new Mesh { name = name }; AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateBounds(); mesh.RecalculateNormals();
            EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh); return mesh;
        }
        static Mesh Quad() => SaveMesh("CoreQuad", new[] { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) },
            new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }, new[] { 0, 1, 2, 0, 2, 3 });
        static Mesh Square()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector3[] corners = { new Vector3(-.5f, -.5f), new Vector3(-.5f, .5f), new Vector3(.5f, .5f), new Vector3(.5f, -.5f) };
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i], b = corners[(i + 1) % 4], width = Vector3.Cross(b - a, Vector3.forward).normalized * .045f;
                int start = vertices.Count; vertices.AddRange(new[] { a - width, a + width, b + width, b - width });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }); triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return SaveMesh("AngularFrame", vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }
        static Mesh Rays()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3; var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                var tangent = Vector3.Cross(direction, Vector3.forward) * .025f; int start = vertices.Count;
                vertices.AddRange(new[] { direction * .12f - tangent, direction * .12f + tangent, direction * (.45f + .08f * (i % 2)) + Vector3.forward * .08f });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, new Vector2(1, .5f) }); triangles.AddRange(new[] { start, start + 1, start + 2 });
            }
            return SaveMesh("EnergyRays", vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }
        static Mesh Spindle()
        {
            var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI / 3; var width = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .5f; int start = vertices.Count;
                vertices.AddRange(new[] { -width, width, width * .6f + Vector3.forward, -width * .6f + Vector3.forward });
                uv.AddRange(new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }); triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return SaveMesh("EnergySpindle", vertices.ToArray(), uv.ToArray(), triangles.ToArray());
        }
        static void ConfigureParticles(ParticleSystem ps, Material material)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.18f, .3f); main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(.006f, .018f);
            main.startColor = new Color(1, .35f, 1, 1); main.gravityModifier = 0;
            var emission = ps.emission; emission.rateOverTime = 14;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = .05f; shape.radiusThickness = .15f;
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local; velocity.orbitalZ = 2; velocity.radial = -.4f;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(new Color(.65f, .04f, 1), 0), new GradientColorKey(new Color(1, .7f, 1), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.maxParticleSize = .05f;
        }
        [Serializable] class Record { public string prefab; public Node[] nodes; public Vector3 muzzle, muzzleForward; }
        [Serializable] class Node { public string path; public Vector3 position, euler, scale, worldPosition, lossyScale; public Bounds bounds; public bool enabled; }
        public static void Inspect()
        {
            Directory.CreateDirectory(Reports);
            var records = new[] { MechanicsLabUpgrade.CannonPrefab, WeaponPath, CharacterPrototypeBuilder.PlayerPath }.Select(path =>
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); var w = go.GetComponent<WeaponController>();
                return new Record { prefab = path, muzzle = w ? w.WeaponMuzzle.position : Vector3.zero, muzzleForward = w ? w.WeaponMuzzle.forward : Vector3.zero,
                    nodes = go.GetComponentsInChildren<Transform>(true).Select(t => new Node { path = AnimationUtility.CalculateTransformPath(t, go.transform), position = t.localPosition, euler = t.localEulerAngles, scale = t.localScale, worldPosition = t.position, lossyScale = t.lossyScale,
                    bounds = t.GetComponent<Renderer>() ? t.GetComponent<Renderer>().bounds : new Bounds(), enabled = t.GetComponent<Renderer>() && t.GetComponent<Renderer>().enabled }).ToArray() };
            }).ToArray();
            for (int i = 0; i < records.Length; i++) File.WriteAllText(Reports + "/hierarchy-" + i + ".json", JsonUtility.ToJson(records[i], true));
            IronRequiemSetupTools.WriteAudit();
        }
    }
}
