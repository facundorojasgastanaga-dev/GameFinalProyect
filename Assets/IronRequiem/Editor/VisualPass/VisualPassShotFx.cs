using System.Linq;
using System.Text;
using Unity.FPS.Game;
using Unity.FPS.Gameplay;
using UnityEditor;
using UnityEngine;

namespace IronRequiem.EditorTools
{
    // Task 2: shot effects for every player weapon (VO cannon, VO shotgun, stolen T-07 / Big Bot cannons).
    public static class VisualPassShotFx
    {
        const string Out = "Assets/IronRequiem/Prefabs/VisualPass/VFX";
        const string MatOut = "Assets/IronRequiem/Art/VisualPass/ShotFx";
        const string LibPath = "Assets/IronRequiem/Resources/IronRequiem/ShotFxLibrary.asset";
        static readonly string[] PlayerWeapons =
        {
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_IR_ArmCannon_VO.prefab",
            "Assets/IronRequiem/Prefabs/VisualOverhaul/Weapon_Shotgun_ArmModule_VO.prefab",
            "Assets/IronRequiem/Prefabs/Weapons/Weapon_T07_Temporary_Optimized.prefab",
            "Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_Temporary.prefab",
            "Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_Bazooka_v08.prefab",
            "Assets/IronRequiem/Prefabs/Weapons/Weapon_BigBot_ReferenceCharge_v09.prefab",
            "Assets/IronRequiem/Prefabs/CombatFollowup/BigBot_PlayerWeapon.prefab",
        };

        [MenuItem("Tools/Iron Requiem/Visual Pass/T2 Audit player shot FX")]
        static void Audit()
        {
            var sb = new StringBuilder("[T2 audit]\n");
            foreach (var path in PlayerWeapons)
            {
                var w = AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<WeaponController>(); if (!w) { sb.AppendLine("missing " + path); continue; }
                var proj = w.ProjectilePrefab as ProjectileStandard;
                sb.AppendLine($"== {System.IO.Path.GetFileName(path)} type={w.ShootType} bullets={w.BulletsPerShot} delay={w.DelayBetweenShots} muzzle={N(w.WeaponMuzzle)} flash={N(w.MuzzleFlashPrefab)} unparent={w.UnparentMuzzleFlash} recoil={w.RecoilForce} sfx={N(w.ShootSfx)}");
                if (proj) sb.AppendLine($"   proj={proj.name} speed={proj.Speed} life={proj.MaxLifeTime} impact={N(proj.ImpactVfx)} impLife={proj.ImpactVfxLifetime} trail={(proj.GetComponentInChildren<TrailRenderer>(true) is TrailRenderer t ? t.time + "s w" + t.widthMultiplier : "none")}");
                if (w.MuzzleFlashPrefab) foreach (var ps in w.MuzzleFlashPrefab.GetComponentsInChildren<ParticleSystem>(true))
                        sb.AppendLine($"   flash ps {ps.name} life={ps.main.startLifetime.constantMax:0.###} size={ps.main.startSize.constantMax:0.###} dur={ps.main.duration:0.##} sim={ps.main.simulationSpace}");
            }
            Debug.Log(sb.ToString());
        }

        static string N(Object o) => o ? o.name : "none";

        static Material ParticleMat(string name, Color color, bool additive)
        {
            VisualPassMech.EnsureFolder(MatOut);
            var path = $"{MatOut}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0); m.SetFloat("_ZWrite", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000; m.enableInstancing = true;
            EditorUtility.SetDirty(m); return m;
        }

        static ParticleSystem Burst(GameObject parent, string name, Material mat, int count, Vector2 life, Vector2 speed, Vector2 size, Color color, float gravity, float cone, bool stretch)
        {
            var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.duration = .2f; m.loop = false; m.playOnAwake = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y); m.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            m.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y); m.startColor = color; m.gravityModifier = gravity; m.maxParticles = count;
            var e = ps.emission; e.rateOverTime = 0; e.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = cone; sh.radius = .02f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); col.color = g;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, stretch ? AnimationCurve.Linear(0, 1, 1, .3f) : AnimationCurve.EaseInOut(0, .6f, 1, 1.6f));
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = .035f; r.lengthScale = 1; }
            return ps;
        }

        static GameObject SavePrefab(GameObject go, string path) { var p = PrefabUtility.SaveAsPrefabAsset(go, path); Object.DestroyImmediate(go); return p; }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T2 Build + apply shot FX")]
        static void Build()
        {
            var sb = new StringBuilder("[T2 build]\n");
            VisualPassMech.EnsureFolder(Out); VisualPassMech.EnsureFolder("Assets/IronRequiem/Resources/IronRequiem");
            var sparkAdd = ParticleMat("VP_ShotSpark", new Color(1f, .62f, .25f, 1), true);
            var flashAdd = ParticleMat("VP_ShotFlash", new Color(1f, .75f, .45f, 1), true);
            var dust = ParticleMat("VP_ShotDust", new Color(.52f, .48f, .44f, .55f), false);
            var smoke = ParticleMat("VP_ShotSmoke", new Color(.62f, .6f, .58f, .22f), false);

            // metal: hot sparks that bounce down + a tiny flash
            var metal = new GameObject("VP_Impact_Metal");
            Burst(metal, "Sparks", sparkAdd, 16, new Vector2(.18f, .45f), new Vector2(3, 8), new Vector2(.012f, .028f), new Color(1.8f, 1f, .45f), 1.4f, 55, true);
            Burst(metal, "Flash", flashAdd, 1, new Vector2(.05f, .06f), Vector2.zero, new Vector2(.22f, .28f), new Color(1.5f, 1f, .6f, .8f), 0, 0, false);
            var metalP = SavePrefab(metal, Out + "/VP_Impact_Metal.prefab");
            // concrete: grey puff + chips
            var dustGo = new GameObject("VP_Impact_Dust");
            Burst(dustGo, "Puff", dust, 6, new Vector2(.45f, .8f), new Vector2(.3f, .9f), new Vector2(.12f, .24f), new Color(.6f, .56f, .52f, .5f), -.05f, 35, false);
            Burst(dustGo, "Chips", dust, 8, new Vector2(.3f, .6f), new Vector2(1.5f, 4f), new Vector2(.015f, .03f), new Color(.35f, .32f, .3f, 1), 1.6f, 45, false);
            var dustP = SavePrefab(dustGo, Out + "/VP_Impact_Dust.prefab");
            // muzzle smoke: small, short, drifts forward/up
            var sm = new GameObject("VP_MuzzleSmoke");
            var smps = Burst(sm, "Smoke", smoke, 3, new Vector2(.35f, .6f), new Vector2(.25f, .6f), new Vector2(.04f, .08f), new Color(.7f, .68f, .65f, .22f), -.08f, 12, false);
            var smMain = smps.main; smMain.simulationSpace = ParticleSystemSimulationSpace.World;
            var smP = SavePrefab(sm, Out + "/VP_MuzzleSmoke.prefab");

            var lib = AssetDatabase.LoadAssetAtPath<ShotFxLibrary>(LibPath);
            if (!lib) { lib = ScriptableObject.CreateInstance<ShotFxLibrary>(); AssetDatabase.CreateAsset(lib, LibPath); }
            lib.metalImpact = metalP; lib.dustImpact = dustP; EditorUtility.SetDirty(lib);
            sb.AppendLine("  prefabs + library ok");

            System.IO.Directory.CreateDirectory("Tools/IronRequiem/Backups/T2");
            var touchedFlash = new System.Collections.Generic.HashSet<GameObject>(); var touchedProj = new System.Collections.Generic.HashSet<GameObject>();
            foreach (var path in PlayerWeapons)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!asset) continue;
                var bak = "Tools/IronRequiem/Backups/T2/" + System.IO.Path.GetFileName(path) + ".bak"; if (!System.IO.File.Exists(bak)) System.IO.File.Copy(path, bak);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var w = root.GetComponent<WeaponController>();
                    var fx = root.GetComponent<MuzzleShotFx>() ?? root.AddComponent<MuzzleShotFx>();
                    bool shotgun = path.Contains("Shotgun"); bool heavy = path.Contains("BigBot") || path.Contains("T07");
                    fx.Configure(smP, shotgun ? 1.5f : heavy ? 1.8f : 1f);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    if (w.MuzzleFlashPrefab) touchedFlash.Add(w.MuzzleFlashPrefab);
                    if (w.ProjectilePrefab) touchedProj.Add(w.ProjectilePrefab.gameObject);
                    sb.AppendLine($"  smoke on {System.IO.Path.GetFileName(path)}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // short, shaped flash: cap particle life to 60 ms (VP flashes are ours; template flashes untouched)
            foreach (var f in touchedFlash)
            {
                var p = AssetDatabase.GetAssetPath(f); if (!p.Contains("/VisualPass/") && !p.Contains("/IronRequiem/")) { sb.AppendLine("  flash kept (template) " + p); continue; }
                var bak = "Tools/IronRequiem/Backups/T2/" + System.IO.Path.GetFileName(p) + ".bak"; if (!System.IO.File.Exists(bak)) System.IO.File.Copy(p, bak);
                var root = PrefabUtility.LoadPrefabContents(p);
                try
                {
                    foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        var m = ps.main; var c = m.startLifetime;
                        float max = c.mode == ParticleSystemCurveMode.TwoConstants ? c.constantMax : c.constant;
                        if (max > .06f) { m.startLifetime = c.mode == ParticleSystemCurveMode.TwoConstants ? new ParticleSystem.MinMaxCurve(Mathf.Min(c.constantMin, .04f), .06f) : new ParticleSystem.MinMaxCurve(.06f); }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, p); sb.AppendLine("  flash shortened " + p);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            // tracers: short tapered trail so bolts read as streaks without smearing across the view
            foreach (var g in touchedProj)
            {
                var p = AssetDatabase.GetAssetPath(g); if (!p.StartsWith("Assets/IronRequiem")) { sb.AppendLine("  projectile kept " + p); continue; }
                var root = PrefabUtility.LoadPrefabContents(p);
                try
                {
                    var trails = root.GetComponentsInChildren<TrailRenderer>(true);
                    if (trails.Length == 0 && p.Contains("/VisualPass/"))
                    {
                        var tr = root.AddComponent<TrailRenderer>();
                        tr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MatOut + "/VP_ShotSpark.mat");
                        tr.time = .06f; tr.widthMultiplier = p.Contains("Shotgun") ? .03f : .045f; tr.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
                        tr.minVertexDistance = .05f; tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; tr.receiveShadows = false;
                        var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(new Color(1, .8f, .5f), 0), new GradientColorKey(new Color(1, .45f, .15f), 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(0, 1) });
                        tr.colorGradient = grad; trails = new[] { tr };
                    }
                    foreach (var t in trails) { t.time = Mathf.Min(t.time, .07f); t.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); t.minVertexDistance = .05f; }
                    PrefabUtility.SaveAsPrefabAsset(root, p); sb.AppendLine($"  trails={trails.Length} {p}");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        // play-mode: fire the active player weapon at a wall, then at a robot, for captures
        [MenuItem("Tools/Iron Requiem/Visual Pass/T2 Diag fire active weapon (play)")]
        static void Fire()
        {
            var pwm = Object.FindAnyObjectByType<PlayerWeaponsManager>(); var w = pwm ? pwm.GetActiveWeapon() : null;
            if (!w) { Debug.Log("[T2 fire] no weapon"); return; }
            var m = typeof(WeaponController).GetMethod("HandleShoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            m.Invoke(w, null);
            Debug.Log($"[T2 fire] {w.name} muzzle={w.WeaponMuzzle.position.ToString("F2")}");
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T2 Diag next weapon (play)")]
        static void Next()
        {
            var pwm = Object.FindAnyObjectByType<PlayerWeaponsManager>(); if (!pwm) return;
            pwm.SwitchWeapon(true); Debug.Log("[T2 next] " + (pwm.GetActiveWeapon() ? pwm.GetActiveWeapon().name : "none"));
        }

        [MenuItem("Tools/Iron Requiem/Visual Pass/T2 Diag list live FX (play)")]
        static void LiveFx()
        {
            var sb = new StringBuilder("[T2 live]\n"); var cam = Camera.main;
            foreach (var p in Object.FindObjectsByType<PooledFx>(FindObjectsSortMode.None))
            {
                int alive = 0; foreach (var ps in p.GetComponentsInChildren<ParticleSystem>()) alive += ps.particleCount;
                var r = p.GetComponentInChildren<Renderer>();
                sb.AppendLine($"  {p.name} active={p.gameObject.activeInHierarchy} pos={p.transform.position.ToString("F2")} d={Vector3.Distance(p.transform.position, cam.transform.position):0.0} particles={alive} layer={LayerMask.LayerToName(p.gameObject.layer)} r={(r ? r.enabled + "/" + r.isVisible : "none")}");
            }
            var pwm = Object.FindAnyObjectByType<PlayerWeaponsManager>(); var w = pwm ? pwm.GetActiveWeapon() : null;
            if (w)
            {
                foreach (Transform c in w.WeaponMuzzle) { int n = 0; foreach (var ps in c.GetComponentsInChildren<ParticleSystem>()) n += ps.particleCount; sb.AppendLine($"  muzzle child {c.name} active={c.gameObject.activeInHierarchy} particles={n} layer={LayerMask.LayerToName(c.gameObject.layer)}"); }
                sb.AppendLine($"  weapon layer={LayerMask.LayerToName(w.gameObject.layer)} cams: " + string.Join(",", System.Array.ConvertAll(Camera.allCameras, c => $"{c.name}(mask {c.cullingMask})")));
            }
            Debug.Log(sb.ToString());
        }
    }
}
