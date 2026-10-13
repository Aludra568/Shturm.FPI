using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace RicochetBerlin.EditorTools
{
    /// <summary>
    /// Builds the whole game from source assets: project settings, sprite import, data assets,
    /// prefabs and the Game scene. Re-runnable: everything it creates is overwritten.
    /// Menu: Ricochet ▸ Build Everything.
    /// </summary>
    public static class GameBuilder
    {
        const string Root = "Assets/_Game";
        const string Gen = Root + "/Art/Generated/";
        const string Ken = Root + "/Art/Kenney/";
        const string Snd = Root + "/Audio/";
        const string Prefabs = Root + "/Prefabs/";
        const string Data = Root + "/Data/";
        const string ScenePath = Root + "/Scenes/Game.unity";

        static Material lit, unlit;

        [MenuItem("Ricochet/Build Everything")]
        public static void BuildEverything()
        {
            SetupProject();
            ImportArt();
            AssetDatabase.Refresh();
            lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");
            Directory.CreateDirectory(Prefabs);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var palette = CreatePalette();
            var bank = CreateSoundBank();
            var prefabs = CreatePrefabs(palette);
            BuildScene(palette, bank, prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ricochet] Build complete");
        }

        [MenuItem("Ricochet/Rebuild Level Only")]
        public static void RebuildLevel()
        {
            var builder = Object.FindAnyObjectByType<LevelBuilder>();
            if (builder == null) { Debug.LogError("No LevelBuilder in the open scene"); return; }
            builder.Build();
            EditorSceneManager.MarkSceneDirty(builder.gameObject.scene);
        }

        // ================================================================== project settings

        static void SetupProject()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SetLayer(layers, Layers.Walls, "Walls");
            SetLayer(layers, Layers.Cover, "Cover");
            SetLayer(layers, Layers.Player, "Player");
            SetLayer(layers, Layers.Enemy, "Enemy");
            tagManager.ApplyModifiedProperties();

            var physics = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset")[0]);
            physics.FindProperty("m_Gravity").vector2Value = Vector2.zero;
            physics.FindProperty("m_QueriesStartInColliders").boolValue = false;
            physics.FindProperty("m_QueriesHitTriggers").boolValue = false;
            physics.ApplyModifiedProperties();

            // Editor play mode receives input even when the Game view is not focused (handy for testing,
            // has no effect on builds)
            string inputSettingsPath = Data + "InputSystemSettings.asset";
            Directory.CreateDirectory(Data);
            var inputSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputSettings>(inputSettingsPath);
            if (inputSettings == null)
            {
                inputSettings = ScriptableObject.CreateInstance<UnityEngine.InputSystem.InputSettings>();
                AssetDatabase.CreateAsset(inputSettings, inputSettingsPath);
            }
            inputSettings.editorInputBehaviorInPlayMode =
                UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            inputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.ResetAndDisableNonBackgroundDevices;
            EditorUtility.SetDirty(inputSettings);
            UnityEngine.InputSystem.InputSystem.settings = inputSettings;

            PlayerSettings.productName = "Shturm";
            PlayerSettings.companyName = "FPI";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.runInBackground = true;

            string iconPath = Root + "/UI/icon.png";
            AssetDatabase.ImportAsset(iconPath);
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        }

        static void SetLayer(SerializedProperty layers, int index, string name) =>
            layers.GetArrayElementAtIndex(index).stringValue = name;

        // ================================================================== sprite import

        static readonly Dictionary<string, float> PixelsPerUnit = new()
        {
            { "ground_asphalt", 64 }, { "ground_courtyard", 64 }, { "roof_ruin", 96 },
            { "rubble_0", 72 }, { "rubble_1", 72 }, { "rubble_2", 72 }, { "slab", 90 },
            { "crater", 128 }, { "manhole", 80 }, { "scorch", 128 },
            { "drone_kamikaze", 128 }, { "drone_gunner", 128 }, { "drone_heavy", 128 }, { "rotor", 128 },
            { "flag", 96 }, { "evac_pad", 58 },
            { "repair", 128 }, { "soft_square", 128 },
            { "headlight", 200 }, { "mangal_cage", 100 }, { "mangal_slat", 100 }, { "wire_line", 64 }, { "detonator", 100 },
            { "supply_crate", 90 }, { "heli_body", 100 }, { "heli_rotor", 100 }, { "heli_tail_rotor", 100 }, { "weak_spot", 128 }, { "icon_mangal", 128 }, { "icon_allies", 128 }, { "icon_twin", 128 }, { "icon_repair", 128 }, { "fx_soft", 64 }, { "fx_glow", 128 }, { "fx_spark", 64 }, { "fx_ring", 64 }, { "white", 4 },
        };

        static readonly HashSet<string> TiledSprites = new() { "ground_asphalt", "ground_courtyard", "roof_ruin", "slab", "soft_square", "wire_line" };

        static void ImportArt()
        {
            foreach (string folder in new[] { Gen, Ken })
            {
                foreach (string file in Directory.GetFiles(folder, "*.png"))
                {
                    string path = file.Replace('\\', '/');
                    string name = Path.GetFileNameWithoutExtension(path);
                    if (name.StartsWith("ui_")) continue; // UI Toolkit textures stay plain textures
                    AssetDatabase.ImportAsset(path);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = PixelsPerUnit.TryGetValue(name, out float ppu) ? ppu : 100f;
                    importer.mipmapEnabled = false;
                    importer.filterMode = FilterMode.Bilinear;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.alphaIsTransparency = true;

                    var settings = new TextureImporterSettings();
                    importer.ReadTextureSettings(settings);
                    settings.spriteMeshType = TiledSprites.Contains(name) ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
                    settings.spriteAlignment = (int)SpriteAlignment.Center;
                    settings.wrapMode = name.StartsWith("ground") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    importer.SetTextureSettings(settings);
                    if (name == "roof_ruin") importer.spriteBorder = new Vector4(24, 24, 24, 24);
                    if (name.StartsWith("tank_")) importer.spritePixelsPerUnit = 110f;
                    if (name == "soft_square") importer.spriteBorder = new Vector4(34, 34, 34, 34); // 0.27u soft edge
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>UI skin textures used from GameUI.uss: crisp, uncompressed; the hazard tape tiles.</summary>
        static void ImportSkin()
        {
            foreach (string file in Directory.GetFiles(Root + "/UI/Skin", "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = path.EndsWith("hazard.png") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        /// <summary>A long music track: streamed from disk instead of decompressed into memory.</summary>
        static AudioClip StreamedClip(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                AudioImporterSampleSettings settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>Onboarding card illustrations: crisp, uncompressed UI textures.</summary>
        static Object[] CardPictures()
        {
            var list = new List<Object>();
            foreach (string file in Directory.GetFiles(Root + "/UI/Cards", "*.png"))
            {
                string path = file.Replace('\\', '/');
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
                list.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(path));
            }
            return list.ToArray();
        }

        static Sprite S(string path) => AssetDatabase.LoadAssetAtPath<Sprite>(path + ".png");
        static Sprite G(string name) => S(Gen + name);
        static Sprite K(string name) => S(Ken + name);

        static AudioClip[] Clips(params string[] names) =>
            names.Select(n => AssetDatabase.LoadAssetAtPath<AudioClip>(Snd + n)).Where(c => c != null).ToArray();

        // ================================================================== data assets

        static T CreateOrReplace<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static LevelPalette CreatePalette()
        {
            Directory.CreateDirectory(Data);
            var p = CreateOrReplace<LevelPalette>(Data + "BerlinPalette.asset");
            p.litMaterial = lit;
            p.unlitMaterial = unlit;
            p.ground = G("ground_asphalt");
            p.courtyard = G("ground_courtyard");
            p.roof = G("roof_ruin");
            p.dropShadow = G("soft_square");
            p.rubble = new[] { G("rubble_0"), G("rubble_1"), G("rubble_2") };
            p.slab = G("slab");
            p.crates = new[] { K("crateMetal"), K("crateWood") };
            p.tankTrap = K("barricadeMetal");
            p.crater = G("crater");
            p.wire = K("wireCrooked");
            p.deadTrees = new[] { K("treeBrown_twigs") };
            p.manhole = G("manhole");
            p.scorch = G("scorch");
            p.oilSpills = new[] { K("oilSpill_large"), K("oilSpill_small") };
            EditorUtility.SetDirty(p);
            return p;
        }

        static SoundBank CreateSoundBank()
        {
            var b = CreateOrReplace<SoundBank>(Data + "SoundBank.asset");
            b.tankShot = Clips("explosionCrunch_000.ogg", "explosionCrunch_001.ogg");
            b.ricochet = Clips("impactMetal_light_000.ogg", "impactMetal_light_001.ogg", "impactMetal_light_002.ogg");
            b.wallImpact = Clips("impactMining_000.ogg", "impactMining_001.ogg");
            b.unitHit = Clips("impactPlate_medium_001.ogg", "impactTin_medium_000.ogg");
            b.explosion = Clips("lowFrequency_explosion_000.ogg", "lowFrequency_explosion_001.ogg", "explosionCrunch_002.ogg");
            b.playerHit = Clips("impactPlate_heavy_000.ogg", "impactPunch_heavy_000.ogg");
            b.armorHit = Clips("impactMetal_000.ogg", "impactBell_heavy_000.ogg");
            b.droneShot = Clips("laserSmall_002.ogg");
            b.hatchOpen = Clips("impactMetal_002.ogg");
            b.arming = Clips("arming_chirp.wav");
            b.nitro = Clips("thrusterFire_000.ogg");
            b.radioBeep = Clips("radio_beep.wav");
            b.flagPickup = Clips("confirmation_002.ogg");
            b.alarm = Clips("alarm_siren.wav");
            b.win = Clips("confirmation_004.ogg");
            b.lose = Clips("error_006.ogg");
            b.uiClick = Clips("click_002.ogg");
            b.repair = Clips("select_003.ogg");
            b.powerup = Clips("maximize_006.ogg");
            b.threatSense = Clips("glitch_002.ogg");
            b.abilitySmoke = Clips("lowFrequency_explosion_001.ogg");
            b.abilityArmor = Clips("forceField_000.ogg");
            b.reloaded = Clips("impactMetal_light_002.ogg");
            b.engineLoop = Clips("spaceEngineLow_000.ogg").FirstOrDefault();
            b.droneLoop = Clips("spaceEngineSmall_001.ogg").FirstOrDefault();
            EditorUtility.SetDirty(b);
            return b;
        }

        // ================================================================== prefabs

        class PrefabSet
        {
            public GameObject player, flag, evac, barrel, fire;
            public Drone kamikaze, gunner, heavy;
        }

        static PrefabSet CreatePrefabs(LevelPalette palette)
        {
            var set = new PrefabSet();
            Projectile playerShell = Save(MakeShell("Shell_Player", Team.Player, K("bulletSand2_outline"), new Color(1f, 0.85f, 0.45f), 8.5f, 3, 1f, 8f, true)).GetComponent<Projectile>();
            Projectile droneShell = Save(MakeShell("Shell_Drone", Team.Enemy, K("bulletRed2_outline"), new Color(1f, 0.3f, 0.25f), 3.8f, 1, 18f, 0f, false)).GetComponent<Projectile>();
            Projectile heavyShell = Save(MakeShell("Shell_Heavy", Team.Enemy, K("bulletRed2_outline"), new Color(1f, 0.25f, 0.5f), 4.2f, 2, 20f, 0f, false)).GetComponent<Projectile>();

            GameObject repair = Save(MakeRepair());
            set.barrel = Save(MakeBarrel());
            set.fire = Save(MakeFire());
            palette.explosiveBarrel = set.barrel;
            palette.fire = set.fire;
            EditorUtility.SetDirty(palette);

            dropPrefab = repair;
            set.kamikaze = Save(MakeKamikaze()).GetComponent<Drone>();
            set.gunner = Save(MakeGunner("Drone_Gunner", G("drone_gunner"), droneShell, hp: 4f, speed: 1f, radius: 0.36f,
                                         rotors: 4, burst: 1, interval: 3.2f)).GetComponent<Drone>();
            AssetDatabase.DeleteAsset(Prefabs + "Drone_Heavy.prefab"); // replaced by the boss
            AssetDatabase.DeleteAsset(Prefabs + "Drone_Boss.prefab");  // replaced by the helicopter
            set.heavy = Save(MakeBoss(heavyShell)).GetComponent<Drone>();
            set.flag = Save(MakeFlag());
            set.evac = Save(MakeEvac());
            Save(MakeAllyDrone());
            Save(MakeSmokeScreen());
            Save(MakeTripwire());
            set.player = Save(MakePlayer(playerShell, CreateTankClasses()));
            Save(MakeSupplyCrate());
            return set;
        }

        static T LoadPrefab<T>(string name) where T : Component =>
            AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab").GetComponent<T>();

        static GameObject Save(GameObject go)
        {
            string path = Prefabs + go.name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject MakeShell(string name, Team team, Sprite sprite, Color color, float speed, int bounces,
                                    float damage, float selfDamage, bool reveals)
        {
            var go = new GameObject(name);
            var sr = Sprite(go, sprite, SortOrder.Projectiles, unlit);
            sr.transform.localScale = Vector3.one * 1.3f;

            var trailGo = Child(go, "Trail");
            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.widthMultiplier = 0.13f;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.sharedMaterial = unlit;
            trail.colorGradient = Gradient(color, new Color(color.r, color.g, color.b, 0f));
            trail.sortingOrder = SortOrder.Projectiles - 1;
            trail.minVertexDistance = 0.05f;

            var light = Light(Child(go, "Glow"), color, 0.9f, 1.6f, shadows: false);
            light.pointLightInnerRadius = 0.2f;

            var p = go.AddComponent<Projectile>();
            Set(p, "team", team);
            Set(p, "speed", speed);
            Set(p, "maxBounces", bounces);
            Set(p, "damage", damage);
            Set(p, "selfDamage", selfDamage);
            Set(p, "sparkColor", color);
            Set(p, "trail", trail);
            if (reveals) Set(go.AddComponent<RevealSource>(), "radius", 1.8f);
            return go;
        }

        static GameObject dropPrefab;

        static GameObject MakeRepair()
        {
            var go = new GameObject("RepairKit");
            var icon = Sprite(Child(go, "Icon"), G("repair"), SortOrder.Props + 6, unlit);
            Light(Child(go, "Glow"), new Color(0.4f, 1f, 0.5f), 0.8f, 1.6f, shadows: false);
            Set(go.AddComponent<RepairPickup>(), "icon", icon);
            return go;
        }

        static GameObject MakeBarrel()
        {
            var go = new GameObject("ExplosiveBarrel") { layer = Layers.Cover };
            Sprite(go, K("barrelRed_top"), SortOrder.Props, lit).transform.localScale = Vector3.one * 1.25f;
            go.AddComponent<CircleCollider2D>().radius = 0.3f;
            var h = go.AddComponent<Health>();
            Set(h, "team", Team.Neutral);
            Set(h, "maxHealth", 1f);
            go.AddComponent<ExplosiveBarrel>();
            return go;
        }

        static GameObject MakeFire()
        {
            var go = new GameObject("Fire");
            var light = Light(go, new Color(1f, 0.55f, 0.2f), 1.1f, 4f, shadows: true);
            light.pointLightInnerRadius = 0.3f;
            var flicker = go.AddComponent<LightFlicker>();
            Set(flicker, "fireLight", light);
            Set(flicker, "ember", G("fx_soft"));
            Set(go.AddComponent<RevealSource>(), "radius", 2.6f);
            return go;
        }

        class DroneParts
        {
            public Transform body;
            public Transform[] rotors;
            public AudioSource buzz;
            public float radius;
        }

        static GameObject DroneBase(string name, Sprite sprite, float hp, float radius, int rotors, out DroneParts parts)
        {
            var go = new GameObject(name) { layer = Layers.Enemy };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.linearDamping = 1f;
            rb.mass = radius * 2f;
            // hitbox covers the rotor tips, not just the body
            float armReach = sprite.rect.width * 0.36f / sprite.pixelsPerUnit + 0.17f;
            go.AddComponent<CircleCollider2D>().radius = Mathf.Max(radius, armReach);
            var h = go.AddComponent<Health>();
            Set(h, "team", Team.Enemy);
            Set(h, "maxHealth", hp);

            Transform body = Child(go, "Body").transform;
            var visual = Sprite(Child(body.gameObject, "Sprite"), sprite, SortOrder.Units, lit);
            visual.transform.localRotation = Quaternion.Euler(0, 0, 90f); // textures face +x, gameplay forward is +y

            // shadow under the drone sells that it hovers
            var shadow = Sprite(Child(go, "Shadow"), G("fx_soft"), SortOrder.Units - 1, lit);
            shadow.color = new Color(0f, 0f, 0f, 0.55f);
            shadow.transform.localPosition = new Vector3(0.12f, -0.15f, 0);
            shadow.transform.localScale = Vector3.one * radius * 2.6f;

            float armLength = sprite.rect.width * 0.36f / sprite.pixelsPerUnit;
            var rotorList = new List<Transform>();
            for (int i = 0; i < rotors; i++)
            {
                float a = Mathf.PI / 4f + i * 2f * Mathf.PI / rotors + Mathf.PI / 2f;
                var r = Sprite(Child(body.gameObject, "Rotor"), G("rotor"), SortOrder.Units + 1, lit);
                r.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * armLength;
                r.transform.localScale = Vector3.one * (radius > 0.5f ? 1.1f : 0.8f);
                r.color = new Color(0.6f, 0.62f, 0.66f, 0.6f);
                rotorList.Add(r.transform);
            }

            var buzz = go.AddComponent<AudioSource>();
            buzz.playOnAwake = false;
            buzz.volume = 0f;
            Set(go.AddComponent<FogHidden>(), "radius", radius);
            parts = new DroneParts { body = body, rotors = rotorList.ToArray(), buzz = buzz, radius = radius };
            return go;
        }

        /// <summary>Shared Drone field wiring (fields live on the abstract base class).</summary>
        static void WireDrone(Drone d, DroneParts m, float speed, float accel, float turn)
        {
            Set(d, "speed", speed);
            Set(d, "acceleration", accel);
            Set(d, "turnSpeed", turn);
            Set(d, "bodyRadius", m.radius);
            Set(d, "body", m.body);
            SetArray(d, "rotors", m.rotors);
            Set(d, "buzz", m.buzz);
            Set(d, "dropPrefab", dropPrefab);
        }

        static GameObject MakeKamikaze()
        {
            var go = DroneBase("Drone_Kamikaze", G("drone_kamikaze"), 1f, 0.32f, 4, out DroneParts parts);
            Transform body = parts.body;
            var warn = Light(Child(body.gameObject, "Warning"), new Color(1f, 0.2f, 0.1f), 0f, 1.6f, shadows: false);
            var d = go.AddComponent<KamikazeDrone>();
            WireDrone(d, parts, 1.6f, 5f, 220f);
            Set(d, "warningLight", warn);
            Set(d, "deathExplosionSize", 0.6f);
            return go;
        }

        static GameObject MakeGunner(string name, Sprite sprite, Projectile shell, float hp, float speed, float radius,
                                     int rotors, int burst, float interval)
        {
            var go = DroneBase(name, sprite, hp, radius, rotors, out DroneParts parts);
            Transform body = parts.body;
            var eye = Light(Child(body.gameObject, "Eye"), new Color(1f, 0.25f, 0.2f), 0.6f, 1f, shadows: false);
            eye.transform.localPosition = new Vector3(0, radius * 0.7f, 0);
            var d = go.AddComponent<GunnerDrone>();
            WireDrone(d, parts, speed, 8f, 240f);
            Set(d, "shellPrefab", shell);
            Set(d, "burstCount", burst);
            Set(d, "fireInterval", interval);
            Set(d, "muzzleDistance", radius + 0.25f);
            Set(d, "eyeLight", eye);
            Set(d, "windUp", 0.9f);
            Set(d, "deathExplosionSize", 0.75f);
            return go;
        }

        /// <summary>
        /// The evac guardian: an attack helicopter, unique per round, revealed once spotted.
        /// Spinning rotors, capsule hitbox along the fuselage, armour everywhere except a glowing weak
        /// spot that moves each phase (HelicopterBoss) - reach it with ricochets.
        /// </summary>
        static GameObject MakeBoss(Projectile shell)
        {
            var go = new GameObject("Boss_Helicopter") { layer = Layers.Enemy };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.linearDamping = 1f;
            rb.mass = 20f;
            var h = go.AddComponent<Health>();
            Set(h, "team", Team.Enemy);
            Set(h, "maxHealth", 24f);

            // Body rotates to face the tank; the rotor hub sits on the pivot
            Transform body = Child(go, "Body").transform;
            var hull = Sprite(Child(body.gameObject, "Fuselage"), G("heli_body"), SortOrder.Units + 1, lit);
            hull.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            var col = body.gameObject.AddComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(0.95f, 2.1f);
            col.offset = new Vector2(0f, -0.25f);
            // unlit so the weak spot reads even at the edge of the light
            var weak = Sprite(Child(body.gameObject, "WeakSpot"), G("weak_spot"), SortOrder.Units + 5, unlit);
            var mainRotor = Sprite(Child(body.gameObject, "MainRotor"), G("heli_rotor"), SortOrder.Units + 4, lit);
            var tailRotor = Sprite(Child(body.gameObject, "TailRotor"), G("heli_tail_rotor"), SortOrder.Units + 3, lit);
            tailRotor.transform.localPosition = new Vector3(0.12f, -1.1f, 0f);
            var eye = Light(Child(body.gameObject, "NoseLight"), new Color(1f, 0.25f, 0.2f), 0.8f, 1.6f, shadows: false);
            eye.transform.localPosition = new Vector3(0f, 0.85f, 0f);

            var shadow = Sprite(Child(go, "Shadow"), G("fx_soft"), SortOrder.Units - 1, lit);
            shadow.color = new Color(0f, 0f, 0f, 0.6f);
            shadow.transform.localPosition = new Vector3(0.25f, -0.35f, 0);
            shadow.transform.localScale = new Vector3(2.4f, 3.2f, 1f);

            var buzz = go.AddComponent<AudioSource>();
            buzz.playOnAwake = false;
            buzz.volume = 0f;
            var fog = go.AddComponent<FogHidden>();
            Set(fog, "radius", 1.1f);
            Set(fog, "stayRevealed", true);

            var d = go.AddComponent<GunnerDrone>();
            WireDrone(d, new DroneParts { body = body, rotors = new[] { mainRotor.transform, tailRotor.transform }, buzz = buzz, radius = 0.85f },
                      0.75f, 4f, 60f);
            Set(d, "isBoss", true);
            Set(d, "shellPrefab", shell);
            Set(d, "burstCount", 1);
            Set(d, "fireInterval", 2.6f);
            Set(d, "muzzleDistance", 1.2f);
            Set(d, "eyeLight", eye);
            Set(d, "windUp", 0.9f);
            Set(d, "preferredMin", 3.5f);
            Set(d, "preferredMax", 7f);
            Set(d, "deathExplosionSize", 2.2f);
            Set(d, "dropChance", 1f);

            var boss = go.AddComponent<HelicopterBoss>();
            Set(boss, "body", body);
            Set(boss, "weakMarker", weak);
            var so = new SerializedObject(boss);
            var phases = so.FindProperty("phases");
            phases.arraySize = 3;
            SetPhase(phases.GetArrayElementAtIndex(0), "ХВОСТ", 180f, 55f, new Vector2(0f, -1.15f), 80f, 1, 2.8f);
            SetPhase(phases.GetArrayElementAtIndex(1), "ПРАВЫЙ БОРТ", -90f, 50f, new Vector2(0.5f, -0.15f), 100f, 1, 2.5f);
            SetPhase(phases.GetArrayElementAtIndex(2), "ЛЕВЫЙ БОРТ", 90f, 50f, new Vector2(-0.5f, -0.15f), 120f, 1, 2.2f);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        static void SetPhase(SerializedProperty p, string name, float angle, float halfArc, Vector2 marker,
                             float turn, int burst, float interval)
        {
            p.FindPropertyRelative("weakSpotName").stringValue = name;
            p.FindPropertyRelative("weakAngle").floatValue = angle;
            p.FindPropertyRelative("weakHalfArc").floatValue = halfArc;
            p.FindPropertyRelative("markerPosition").vector2Value = marker;
            p.FindPropertyRelative("turnSpeed").floatValue = turn;
            p.FindPropertyRelative("burst").intValue = burst;
            p.FindPropertyRelative("fireInterval").floatValue = interval;
        }

        static GameObject MakeAllyDrone()
        {
            var go = new GameObject("AllyDrone");
            var body = Child(go, "Body").transform;
            var sr = Sprite(Child(body.gameObject, "Sprite"), G("drone_kamikaze"), SortOrder.Units + 2, lit);
            sr.color = new Color(0.45f, 0.85f, 1f);
            sr.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            body.localScale = Vector3.one * 0.55f;
            var rotors = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                float a = Mathf.PI / 4f + i * Mathf.PI / 2f;
                var r = Sprite(Child(body.gameObject, "Rotor"), G("rotor"), SortOrder.Units + 3, lit);
                r.transform.localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * 0.27f;
                r.transform.localScale = Vector3.one * 0.8f;
                rotors.Add(r.transform);
            }
            Light(Child(go, "Glow"), new Color(0.4f, 0.8f, 1f), 0.7f, 1.2f, shadows: false);
            var ally = go.AddComponent<AllyDrone>();
            Set(ally, "body", body);
            SetArray(ally, "rotors", rotors.ToArray());
            return go;
        }

        static GameObject MakeSmokeScreen()
        {
            var go = new GameObject("SmokeScreen");
            Set(go.AddComponent<SmokeScreen>(), "puff", G("fx_soft"));
            return go;
        }

        static GameObject MakeTripwire()
        {
            var go = new GameObject("Tripwire");
            go.AddComponent<BoxCollider2D>().isTrigger = true;
            var wire = Sprite(Child(go, "Wire"), G("wire_line"), SortOrder.Props + 1, lit);
            wire.drawMode = SpriteDrawMode.Tiled;
            wire.size = new Vector2(2f, 0.12f);
            wire.color = new Color(1f, 1f, 1f, 0.55f); // half-visible, and only in the light

            var det = new GameObject("Detonator") { layer = Layers.Cover };
            det.transform.SetParent(go.transform, false);
            Sprite(det, G("detonator"), SortOrder.Props + 2, lit).transform.localScale = Vector3.one * 1.1f;
            det.AddComponent<CircleCollider2D>().radius = 0.17f;
            var hp = det.AddComponent<Health>();
            Set(hp, "team", Team.Neutral);
            Set(hp, "maxHealth", 1f);
            var led = Sprite(Child(det, "Led"), G("fx_soft"), SortOrder.Props + 3, unlit);
            led.color = new Color(1f, 0.15f, 0.1f, 0.8f);
            led.transform.localScale = Vector3.one * 0.12f;
            led.transform.localPosition = new Vector3(0.05f, 0.05f, 0);

            var trip = go.AddComponent<Tripwire>();
            Set(trip, "wire", wire);
            Set(trip, "detonator", hp);
            Set(trip, "detonatorLed", led);
            return go;
        }

        static GameObject MakeFlag()
        {
            var go = new GameObject("Flag");
            var banner = Child(go, "Banner");
            Sprite(banner, G("flag"), SortOrder.Props + 5, lit).transform.localScale = Vector3.one * 1.1f;
            Light(Child(go, "Glow"), new Color(1f, 0.35f, 0.25f), 1.2f, 2.4f, shadows: false);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.85f;
            var fog = go.AddComponent<FogHidden>();
            Set(fog, "radius", 0.5f);
            Set(fog, "stayRevealed", true);
            banner.AddComponent<Billboard>();
            Set(go.AddComponent<FlagPickup>(), "banner", banner.transform);
            return go;
        }

        static GameObject MakeEvac()
        {
            var go = new GameObject("EvacZone");
            var pad = Sprite(Child(go, "Pad"), G("evac_pad"), SortOrder.Decals + 2, unlit);
            pad.color = new Color(1f, 1f, 1f, 0.85f);
            var beacon = Light(Child(go, "Beacon"), new Color(0.35f, 1f, 0.45f), 1.5f, 5f, shadows: true);
            var e = go.AddComponent<EvacZone>();
            Set(e, "beacon", beacon);
            Set(e, "padVisual", pad.transform);
            Set(e, "smokeSprite", G("fx_soft"));
            return go;
        }

        static TankClass[] CreateTankClasses()
        {
            Directory.CreateDirectory(Data + "Tanks");
            return new[]
            {
                TankAsset("light", "РЫСЬ", "Быстрый и хрупкий", hp: 50f, speed: 3f, turn: 150f,
                          cooldown: 0.55f, damage: 1f, ability: AbilityKind.Nitro, size: new Vector2(0.58f, 0.8f), ratings: (3, 1, 1), track: 0.21f, barrel: 0.28f, muzzle: 0.5f),
                TankAsset("medium", "ВОЛК", "Сбалансированный", hp: 75f, speed: 2.4f, turn: 120f,
                          cooldown: 0.7f, damage: 2f, ability: AbilityKind.Smoke, size: new Vector2(0.68f, 0.92f), ratings: (2, 2, 2), track: 0.25f, barrel: 0.33f, muzzle: 0.58f),
                TankAsset("heavy", "МЕДВЕДЬ", "Медленный и мощный", hp: 100f, speed: 1.8f, turn: 90f,
                          cooldown: 0.95f, damage: 3f, ability: AbilityKind.ActiveArmor, size: new Vector2(0.84f, 1.06f), ratings: (1, 3, 3), track: 0.31f, barrel: 0.37f, muzzle: 0.66f),
            };
        }

        static TankClass TankAsset(string id, string name, string desc, float hp, float speed, float turn, float cooldown,
                                   float damage, AbilityKind ability, Vector2 size, (int speed, int armor, int fire) ratings,
                                   float track, float barrel, float muzzle)
        {
            var c = CreateOrReplace<TankClass>(Data + "Tanks/Tank_" + id + ".asset");
            c.displayName = name;
            c.description = desc;
            c.maxHealth = hp;
            c.moveSpeed = speed;
            c.turnSpeed = turn;
            c.fireCooldown = cooldown;
            c.shellDamage = damage;
            c.colliderSize = size;
            c.ability = ability;
            c.preview = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/UI/Tanks/menu_tank_" + id + ".png");
            c.defaultColorIndex = id switch { "light" => 1, "heavy" => 2, _ => 0 }; // sand / olive / grey
            c.speedRating = ratings.speed;
            c.armorRating = ratings.armor;
            c.fireRating = ratings.fire;
            c.hull = G("tank_" + id + "_hull");
            c.track = G("tank_" + id + "_track");
            c.turret = G("tank_" + id + "_turret");
            c.barrel = G("tank_" + id + "_barrel");
            c.twinBarrel = G("tank_" + id + "_barrel_twin");
            c.damage = G("tank_" + id + "_damage");
            c.trackDebris = G("tank_" + id + "_debris");
            c.trackOffset = track;
            c.barrelOffset = barrel;
            c.muzzleDistance = muzzle;
            EditorUtility.SetDirty(c);
            return c;
        }

        static GameObject MakeSupplyCrate()
        {
            var go = new GameObject("SupplyCrate");
            var shadow = Sprite(Child(go, "Shadow"), G("soft_square"), SortOrder.Props - 1, lit);
            shadow.drawMode = SpriteDrawMode.Sliced;
            shadow.size = new Vector2(1.2f, 1.2f);
            shadow.color = new Color(0f, 0f, 0f, 0.8f);
            shadow.transform.localPosition = new Vector3(0.1f, -0.12f, 0f);
            Sprite(Child(go, "Crate"), G("supply_crate"), SortOrder.Props, lit);
            // icon is unlit: loot is the one thing worth spotting in the dark
            var icon = Sprite(Child(go, "Icon"), G("icon_mangal"), SortOrder.Markers - 1, unlit);
            var glow = Light(Child(go, "Glow"), Color.white, 1f, 2f, shadows: false);
            // hidden in the dark like everything else; once spotted it stays marked
            var fog = go.AddComponent<FogHidden>();
            Set(fog, "radius", 0.5f);
            Set(fog, "stayRevealed", true);
            var crate = go.AddComponent<SupplyCrate>();
            Set(crate, "icon", icon);
            Set(crate, "glow", glow);
            Set(crate, "mangalIcon", G("icon_mangal"));
            Set(crate, "alliesIcon", G("icon_allies"));
            Set(crate, "twinIcon", G("icon_twin"));
            Set(crate, "repairIcon", G("icon_repair"));
            return go;
        }

        static GameObject MakePlayer(Projectile shell, TankClass[] classes)
        {
            var go = new GameObject("PlayerTank") { layer = Layers.Player };
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.mass = 5f;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var hitbox = go.AddComponent<CapsuleCollider2D>();
            hitbox.direction = CapsuleDirection2D.Vertical;
            hitbox.size = new Vector2(0.68f, 0.92f);
            var health = go.AddComponent<Health>();
            Set(health, "team", Team.Player);
            Set(health, "maxHealth", 100f);
            TankClass m = classes[1];


            var trackL = Sprite(Child(go, "TrackLeft"), m.track, SortOrder.Units, lit);
            var trackR = Sprite(Child(go, "TrackRight"), m.track, SortOrder.Units, lit);
            var hull = Sprite(Child(go, "Hull"), m.hull, SortOrder.Units + 1, lit);
            var damage = Sprite(Child(go, "DamageOverlay"), m.damage, SortOrder.Units + 2, lit);
            var lights = new Object[2];
            for (int i = 0; i < 2; i++)
                lights[i] = Sprite(Child(go, "Headlight"), G("headlight"), SortOrder.Units + 3, unlit);

            var turretGo = Child(go, "Turret");
            var barrel = Sprite(Child(turretGo, "Barrel"), m.barrel, SortOrder.Turret, lit);
            var turretSprite = Sprite(Child(turretGo, "TurretBody"), m.turret, SortOrder.Turret + 1, lit);

            var cone = Light(Child(turretGo, "Flashlight"), new Color(1f, 0.94f, 0.82f), 1.9f, 11f, shadows: true);
            cone.falloffIntensity = 0.5f;
            var near = Light(Child(go, "NearLight"), new Color(0.9f, 0.92f, 1f), 0.75f, 2.8f, shadows: true);
            near.falloffIntensity = 0.7f;

            // headlights throw a short beam ahead of the hull: the front is obvious without HUD clutter
            var beam = Light(Child(go, "HeadlightBeam"), new Color(1f, 0.92f, 0.7f), 0.7f, 2.6f, shadows: true);
            beam.pointLightOuterAngle = 70f;
            beam.pointLightInnerAngle = 30f;
            beam.pointLightInnerRadius = 0.1f;
            beam.falloffIntensity = 0.6f;

            var cage = Sprite(Child(go, "MangalCage"), G("mangal_cage"), SortOrder.Turret + 3, lit);

            var exhaust = Child(go, "Exhaust");
            exhaust.transform.localPosition = new Vector3(0, -0.45f, 0);

            var carried = Child(go, "CarriedFlag");
            Sprite(carried, G("flag"), SortOrder.Turret + 2, lit).transform.localScale = Vector3.one * 0.55f;
            carried.transform.localPosition = new Vector3(-0.2f, -0.3f, 0);

            var aimGo = Child(go, "AimLine");
            var line = aimGo.AddComponent<LineRenderer>();
            line.sharedMaterial = unlit;
            line.widthMultiplier = 0.05f;
            line.numCapVertices = 2;
            line.colorGradient = Gradient(new Color(1f, 0.35f, 0.25f, 0.55f), new Color(1f, 0.35f, 0.25f, 0.08f));
            line.sortingOrder = SortOrder.Projectiles - 2;

            var engine = go.AddComponent<AudioSource>();
            engine.playOnAwake = false;

            // wiring
            go.AddComponent<TankController>();
            var turret = turretGo.AddComponent<TankTurret>();
            Set(turret, "barrel", barrel);
            Set(turret, "shellPrefab", shell);
            var preview = aimGo.AddComponent<AimPreview>();
            Set(preview, "turret", turret);

            go.AddComponent<MenuShowcase>();
            var ability = go.AddComponent<TankAbility>();
            Set(ability, "smokePrefab", LoadPrefab<SmokeScreen>("SmokeScreen"));
            var tank = go.AddComponent<PlayerTank>();
            SetArray(tank, "classes", classes);
            Set(tank, "turret", turret);
            Set(tank, "hullSprite", hull);
            Set(tank, "turretSprite", turretSprite);
            Set(tank, "barrelSprite", barrel);
            Set(tank, "trackLeft", trackL);
            Set(tank, "trackRight", trackR);
            Set(tank, "damageOverlay", damage);
            SetArray(tank, "headlights", lights);
            Set(tank, "headlightBeam", beam);
            Set(tank, "mangalCage", cage);
            Set(tank, "slatSprite", G("mangal_slat"));
            Set(tank, "allyPrefab", LoadPrefab<AllyDrone>("AllyDrone"));
            Set(tank, "carriedFlag", carried);
            Set(tank, "exhaust", exhaust.transform);
            Set(tank, "engine", engine);
            Set(tank, "flameSprite", G("fx_soft"));
            Set(tank, "smokeSprite", G("fx_soft"));


            var tracks = go.AddComponent<TrackMarks>();
            Set(tracks, "trackSprite", K("tracksSmall"));
            Set(tracks, "material", lit);

            var vision = go.AddComponent<VisionSystem>();
            Set(vision, "eye", turretGo.transform);
            Set(vision, "coneLight", cone);
            Set(vision, "nearLight", near);
            return go;
        }

        // ================================================================== scene

        static void BuildScene(LevelPalette palette, SoundBank bank, PrefabSet prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene unloads assets nobody references yet, so re-load everything we wire up by path
            palette = AssetDatabase.LoadAssetAtPath<LevelPalette>(Data + "BerlinPalette.asset");
            bank = AssetDatabase.LoadAssetAtPath<SoundBank>(Data + "SoundBank.asset");
            prefabs = new PrefabSet
            {
                player = LoadPrefab<Transform>("PlayerTank").gameObject,
                flag = LoadPrefab<Transform>("Flag").gameObject,
                evac = LoadPrefab<Transform>("EvacZone").gameObject,
                kamikaze = LoadPrefab<Drone>("Drone_Kamikaze"),
                gunner = LoadPrefab<Drone>("Drone_Gunner"),
                heavy = LoadPrefab<Drone>("Boss_Helicopter"),
            };
            lit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat");
            unlit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat");

            // lighting: dim moonlit city, everything else comes from the tank's flashlight and fires
            var global = new GameObject("GlobalLight (Moon)").AddComponent<Light2D>();
            global.lightType = Light2D.LightType.Global;
            global.color = new Color(0.55f, 0.62f, 0.85f);
            global.intensity = 0.008f; // outside the flashlight the city is pitch dark
            global.targetSortingLayers = Fx.AllSortingLayers();

            // input + camera
            var input = new GameObject("Input").AddComponent<InputReader>();
            var rig = new GameObject("CameraRig");
            rig.transform.position = new Vector3(0, 0, -10);
            var camGo = Child(rig, "Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;
            camGo.AddComponent<AudioListener>();
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            CreatePostProcessing();

            // level
            var levelGo = new GameObject("Level");
            var level = levelGo.AddComponent<LevelBuilder>();
            Set(level, "mapFile", AssetDatabase.LoadAssetAtPath<TextAsset>(Data + "Maps/berlin.txt"));
            Set(level, "palette", palette);
            level.Build();
            Transform markers = levelGo.transform.Find("Markers");
            Vector3 start = markers.GetComponentsInChildren<MapMarker>().First(m => m.kind == MarkerKind.PlayerStart).transform.position;

            // player
            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.player);
            player.transform.position = start;
            var tank = player.GetComponent<PlayerTank>();
            Set(player.GetComponent<TankController>(), "input", input);
            var turret = player.GetComponentInChildren<TankTurret>();
            Set(turret, "input", input);
            Set(player.GetComponent<VisionSystem>(), "input", input);
            Set(player.GetComponent<TankAbility>(), "input", input);

            var cameraRig = rig.AddComponent<CameraRig>();
            Set(cameraRig, "target", player.transform);
            Set(cameraRig, "cam", cam);
            rig.transform.position = new Vector3(start.x, start.y, -10);

            // objectives
            var flag = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.flag);
            var evac = (GameObject)PrefabUtility.InstantiatePrefab(prefabs.evac);

            // systems
            var systems = new GameObject("Systems");
            var gm = Child(systems, "GameManager").AddComponent<GameManager>();
            Set(gm, "player", tank);

            var audio = Child(systems, "Audio").AddComponent<Audio>();
            Set(audio, "bank", bank);

            var fx = Child(systems, "Fx").AddComponent<Fx>();
            Set(fx, "litMaterial", lit);
            Set(fx, "unlitMaterial", unlit);
            Set(fx, "soft", G("fx_soft"));
            Set(fx, "glow", G("fx_glow"));
            Set(fx, "spark", G("fx_spark"));
            Set(fx, "ring", G("fx_ring"));
            Set(fx, "scorch", G("scorch"));
            Set(fx, "muzzle", K("shotOrange"));
            SetArray(fx, "explosionFrames", Enumerable.Range(1, 5).Select(i => (Object)K("explosion" + i)).ToArray());
            SetArray(fx, "smokeFrames", Enumerable.Range(1, 5).Select(i => (Object)K("explosionSmoke" + i)).ToArray());

            var nav = Child(systems, "NavGrid").AddComponent<NavGrid>();
            Set(nav, "level", level);
            Set(nav, "target", player.transform);

            var spawner = Child(systems, "SpawnDirector").AddComponent<SpawnDirector>();
            Set(spawner, "markersRoot", markers);
            var so = new SerializedObject(spawner);
            var types = so.FindProperty("types");
            types.arraySize = 2;
            SetType(types.GetArrayElementAtIndex(0), prefabs.kamikaze, 0f, 1f, 3);
            SetType(types.GetArrayElementAtIndex(1), prefabs.gunner, 0f, 1f, 3);
            so.ApplyModifiedPropertiesWithoutUndo();

            var trips = Child(systems, "TripwireDirector").AddComponent<TripwireDirector>();
            Set(trips, "tripwirePrefab", LoadPrefab<Tripwire>("Tripwire"));

            var music = Child(systems, "Music").AddComponent<MusicPlayer>();
            Set(music, "menuMusic", StreamedClip(Snd + "menu_theme.mp3"));
            Set(music, "combatMusic", StreamedClip(Snd + "music_war_drums.wav"));
            Set(music, "bossMusic", StreamedClip(Snd + "boss_theme.mp3"));
            Set(music, "creditsMusic", StreamedClip(Snd + "credits_theme.mp3"));

            var powerups = Child(systems, "PowerupDirector").AddComponent<PowerupDirector>();
            Set(powerups, "cratePrefab", LoadPrefab<SupplyCrate>("SupplyCrate"));

            var objectives = Child(systems, "ObjectiveDirector").AddComponent<ObjectiveDirector>();
            Set(objectives, "markersRoot", markers);
            Set(objectives, "flag", flag.GetComponent<FlagPickup>());
            Set(objectives, "evac", evac.GetComponent<EvacZone>());
            Set(objectives, "bossPrefab", prefabs.heavy);

            // UI
            var uiGo = new GameObject("UI");
            var doc = uiGo.AddComponent<UIDocument>();
            doc.panelSettings = CreatePanelSettings();
            doc.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/GameUI.uxml");
            var ui = uiGo.AddComponent<GameUI>();
            Set(ui, "input", input);
            Set(ui, "cam", cam);
            Set(uiGo.AddComponent<ThreatSense>(), "cam", cam);
            var zzz = uiGo.AddComponent<IdleZzz>();
            Set(zzz, "input", input);
            Set(zzz, "cam", cam);
            ImportSkin();
            SetArray(uiGo.AddComponent<Onboarding>(), "pictures", CardPictures());
            uiGo.AddComponent<Credits>();

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SetType(SerializedProperty p, Drone prefab, float unlock, float weight, int max)
        {
            p.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            p.FindPropertyRelative("unlockTime").floatValue = unlock;
            p.FindPropertyRelative("weight").floatValue = weight;
            p.FindPropertyRelative("maxAlive").intValue = max;
        }

        static void CreatePostProcessing()
        {
            string path = Data + "PostProcess.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.1f);
            bloom.scatter.Override(0.65f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.45f);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(16f);
            color.saturation.Override(-14f);
            color.postExposure.Override(0.15f);
            var grain = profile.Add<FilmGrain>(true);
            grain.intensity.Override(0.18f);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);

            var volume = new GameObject("PostProcessing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        static PanelSettings CreatePanelSettings()
        {
            string themePath = Root + "/UI/RuntimeTheme.tss";
            if (!File.Exists(themePath))
            {
                File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
            }
            var panel = CreateOrReplace<PanelSettings>(Root + "/UI/PanelSettings.asset");
            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            EditorUtility.SetDirty(panel);
            return panel;
        }

        // ================================================================== small helpers

        static GameObject Child(GameObject parent, string name)
        {
            var go = new GameObject(name) { layer = parent.layer };
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        static SpriteRenderer Sprite(GameObject go, Sprite sprite, int order, Material mat)
        {
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.sharedMaterial = mat;
            return sr;
        }

        static Light2D Light(GameObject go, Color color, float intensity, float radius, bool shadows)
        {
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.shadowsEnabled = shadows;
            l.shadowIntensity = shadows ? 0.92f : 0f;
            l.targetSortingLayers = Fx.AllSortingLayers();
            return l;
        }

        static Gradient Gradient(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                      new[] { new GradientAlphaKey(a.a, 0), new GradientAlphaKey(b.a, 1) });
            return g;
        }

        /// <summary>Sets a private [SerializeField] through SerializedObject (keeps fields encapsulated).</summary>
        static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[Ricochet] {target.GetType().Name}.{field} not found"); return; }
            switch (value)
            {
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case Color c: p.colorValue = c; break;
                case Rect r: p.rectValue = r; break;
                case System.Enum e: p.enumValueIndex = System.Convert.ToInt32(e); break;
                default: Debug.LogError($"[Ricochet] unsupported type for {field}"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
