using UnityEngine;

namespace CtfStage
{
    /// <summary>
    /// One-component scene builder. Add this to a single empty GameObject and press
    /// Play — it creates the camera, light, ground, two fighters, and wires up
    /// StageClient + StageDirector + StageAudio. Run a backend (game_server.py) and
    /// drive solves to see attacks fly.
    ///
    /// To use real characters: make a prefab from your VRoid/FBX model, put a
    /// StageFighter on it (assign Animator + a hand bone as FistPoint), and drop it
    /// into Team A Model / Team B Model below. Empty slots fall back to capsules.
    /// </summary>
    public class StageBootstrap : MonoBehaviour
    {
        [Header("Characters (optional — empty = placeholder capsules)")]
        public GameObject teamAModel;
        public GameObject teamBModel;

        [Header("Model Rotation Fix (per-model, tweak in Inspector)")]
        public Vector3 teamARotationFix = new Vector3(0f, 0f, 0f);
        public Vector3 teamBRotationFix = new Vector3(0f, 0f, 0f);

        [Header("Model Position & Scale Override")]
        [Tooltip("Manual position offset for Team A")]
        public Vector3 teamAPositionOffset = Vector3.zero;
        [Tooltip("Manual position offset for Team B")]
        public Vector3 teamBPositionOffset = Vector3.zero;
        [Tooltip("Manual scale override (0 = auto 3m)")]
        public float teamAScale = 0f;
        public float teamBScale = 0f;

        [Header("Layout")]
        public float teamSpacing = 4f;     // fighters at x = ±teamSpacing
        public float groundY = 0f;
        public float capsuleHeight = 1f;

        [Header("Placeholder colors")]
        public Color teamA = new Color(0.20f, 0.84f, 1f);   // cyan  (#33d6ff)
        public Color teamB = new Color(1f, 0.31f, 0.85f);   // magenta (#ff4fd8)
        public Color ground = new Color(0.03f, 0.02f, 0.06f);
        public Color sky = new Color(0.015f, 0.01f, 0.04f);

        [Header("VFX Prefab Overrides (optional — empty = procedural particles)")]
        [Tooltip("PWN — red explosion (e.g. Cartoon FX fire/explosion prefab)")]
        public GameObject vfxExplosion;
        [Tooltip("WEB — green matrix code rain")]
        public GameObject vfxMatrix;
        [Tooltip("WiFi — yellow lightning strike")]
        public GameObject vfxLightning;
        [Tooltip("REVERSE — blue gears")]
        public GameObject vfxGears;
        [Tooltip("FORENSICS — purple psychic aura")]
        public GameObject vfxPsychic;
        [Tooltip("CRYPTO — orange vortex")]
        public GameObject vfxVortex;
        [Tooltip("IoT — dark shadow tendrils")]
        public GameObject vfxShadow;
        [Tooltip("OSINT — white flash burst")]
        public GameObject vfxFlash;
        [Tooltip("B2R — gold crown / pillar beam")]
        public GameObject vfxRoot;

        [Header("Environment Prefab (optional — empty = procedural sci-fi stage)")]
        [Tooltip("Drag a scene/environment prefab here (e.g. Church from Colonial City pack)")]
        public GameObject environmentPrefab;
        [Tooltip("Scale of the environment prefab")]
        public float environmentScale = 1f;
        [Tooltip("Position offset for the environment")]
        public Vector3 environmentOffset = Vector3.zero;

        void Start()
        {
            // Register VFX prefab overrides before anything spawns effects
            StageVfx.RegisterPrefab("explosion", vfxExplosion);
            StageVfx.RegisterPrefab("matrix",    vfxMatrix);
            StageVfx.RegisterPrefab("lightning",  vfxLightning);
            StageVfx.RegisterPrefab("gears",      vfxGears);
            StageVfx.RegisterPrefab("psychic",    vfxPsychic);
            StageVfx.RegisterPrefab("vortex",     vfxVortex);
            StageVfx.RegisterPrefab("shadow",     vfxShadow);
            StageVfx.RegisterPrefab("flash",      vfxFlash);
            StageVfx.RegisterPrefab("root",       vfxRoot);

            BuildCamera();
            BuildLight();
            StageEnvironment.Build(teamA, teamB, teamSpacing, groundY, sky);

            // Spawn environment prefab if provided (e.g. Church from Colonial City pack)
            if (environmentPrefab != null)
            {
                var env = Instantiate(environmentPrefab, environmentOffset, Quaternion.identity);
                env.name = "Environment";
                env.transform.localScale = Vector3.one * environmentScale;
            }

            // Auto-load models from GLB paths if not assigned in Inspector
            if (teamAModel == null) teamAModel = LoadModel("Assets/Models/Alien.fbx");
            if (teamBModel == null) teamBModel = LoadModel("Assets/Models/Ninja.fbx");

            StageFighter fA = BuildFighter("Fighter_A", -teamSpacing, +1, teamA, teamAModel, teamARotationFix, teamAPositionOffset, teamAScale);
            AutoAssignClips(fA, teamAModel, "Idle", "Attack", "HitRecieve", "Win");

            StageFighter fB = BuildFighter("Fighter_B", +teamSpacing, -1, teamB, teamBModel, teamBRotationFix, teamBPositionOffset, teamBScale);
            AutoAssignClips(fB, teamBModel, "Idle", "Attack", "HitRecieve", "Win");

            var stage = new GameObject("Stage");
            stage.AddComponent<StageClient>();               // connects on Start
            var director = stage.AddComponent<StageDirector>();
            director.teamA = fA;
            director.teamB = fB;
            stage.AddComponent<StageAudio>();                // synth SFX + BGM
            stage.AddComponent<StageScreens>();              // standby / VS intro / HUD / result

            Debug.Log("[StageBootstrap] scene built. Run a backend and drive solves.");
        }

        // Instantiate a real model if provided, else a colored capsule. Either way
        // returns a configured StageFighter facing the center.
        StageFighter BuildFighter(string name, float x, int facing, Color color, GameObject model,
            Vector3 rotFix = default, Vector3 posOffset = default, float manualScale = 0f)
        {
            Vector3 pos = new Vector3(x, groundY, 0f);

            if (model != null)
            {
                var go = Instantiate(model);
                go.name = name;

                // Rotation: fix + face opponent
                go.transform.rotation = Quaternion.Euler(rotFix);
                float faceY = x > 0 ? -90f : 90f;
                go.transform.Rotate(Vector3.up, faceY, Space.World);

                // Scale: manual if set, otherwise auto-fit to 3m
                if (manualScale > 0f)
                {
                    go.transform.localScale = Vector3.one * manualScale;
                }
                else
                {
                    go.transform.position = Vector3.zero;
                    go.transform.localScale = Vector3.one;
                    Bounds b = new Bounds(Vector3.zero, Vector3.zero);
                    foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                        b.Encapsulate(r.bounds);
                    float h = Mathf.Max(b.size.y, 0.01f);
                    go.transform.localScale = Vector3.one * (3f / h);
                }

                // Position: team X + manual offset
                go.transform.position = new Vector3(x, groundY, 0f) + posOffset;

                Debug.Log($"[StageBootstrap] {name}: pos={go.transform.position}, scale={go.transform.localScale.x:F2}");

                // Only paint capsule fallbacks — imported models keep their own materials
                // foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                //     Paint(r, color, color * 1.5f);

                // Add fist point for attack projectiles
                var fistPt = new GameObject("Fist");
                fistPt.transform.SetParent(go.transform, false);
                fistPt.transform.localPosition = new Vector3(facing * 0.3f, 0.5f, -0.3f);

                var f = go.GetComponent<StageFighter>();
                if (f == null) f = go.AddComponent<StageFighter>();
                f.fistPoint = fistPt.transform;
                f.facing = facing;
                return f;
            }

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = name;
            body.transform.position = new Vector3(x, groundY + capsuleHeight, 0f);
            Paint(body.GetComponent<Renderer>(), color, color * 1.5f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var fist = new GameObject("Fist");
            fist.transform.SetParent(body.transform, false);
            fist.transform.localPosition = new Vector3(facing * 0.5f, 0.2f, -0.4f); // forward, toward camera

            var fighter = body.AddComponent<StageFighter>();
            fighter.fistPoint = fist.transform;
            fighter.facing = facing;
            return fighter;
        }

        void BuildCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.transform.position = new Vector3(0f, 4f, -11f);
            cam.transform.LookAt(new Vector3(0f, groundY + 1f, 0f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = sky;
            cam.fieldOfView = 50f;
        }

        void BuildLight()
        {
            var go = new GameObject("Directional Light");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1.2f;
            l.color = new Color(0.6f, 0.7f, 1f);  // cool sci-fi blue
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // URP Lit primitive: base color + soft emission so it reads on a dark stage.
        static void Paint(Renderer r, Color baseCol, Color emission)
        {
            if (r == null) return;
            var m = r.material;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        /// Load a model from an asset path (Editor only).
        static GameObject LoadModel(string assetPath)
        {
#if UNITY_EDITOR
            var obj = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (obj != null)
                Debug.Log($"[StageBootstrap] Loaded model from {assetPath}");
            else
                Debug.LogWarning($"[StageBootstrap] Failed to load model at {assetPath}");
            return obj;
#else
            return null;
#endif
        }

        /// Auto-assign animation clips from the model's FBX by clip name.
        /// FBX files contain multiple named clips (e.g. "Idle", "Bite_Front", "Death").
        static void AutoAssignClips(StageFighter fighter, GameObject model,
            string idleName, string attackName, string hurtName, string winName)
        {
            if (model == null) return;
#if UNITY_EDITOR
            string path = UnityEditor.AssetDatabase.GetAssetPath(model);
            if (string.IsNullOrEmpty(path)) return;

            var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            if (allAssets == null) return;

            var clips = new System.Collections.Generic.Dictionary<string, AnimationClip>();
            foreach (var obj in allAssets)
            {
                if (obj == null) continue;
                if (obj is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    clips[clip.name] = clip;
            }

            Debug.Log($"[StageBootstrap] {fighter.name}: found {clips.Count} clips in {path}: {string.Join(", ", clips.Keys)}");

            fighter.idleClip   = FindClip(clips, idleName);
            fighter.attackClip = FindClip(clips, attackName);
            fighter.hurtClip   = FindClip(clips, hurtName);
            fighter.winClip    = FindClip(clips, winName);

            Debug.Log($"[StageBootstrap] {fighter.name}: idle={fighter.idleClip?.name}, attack={fighter.attackClip?.name}, hurt={fighter.hurtClip?.name}, win={fighter.winClip?.name}");
#endif
        }

        /// Find a clip by name, trying exact match first, then partial match.
        static AnimationClip FindClip(System.Collections.Generic.Dictionary<string, AnimationClip> clips, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            // Exact match
            if (clips.TryGetValue(name, out var c)) return c;
            // Partial match (e.g. "MonsterArmature|Bite_Front" contains "Bite_Front")
            foreach (var kv in clips)
                if (kv.Key.Contains(name)) return kv.Value;
            return null;
        }
    }
}
