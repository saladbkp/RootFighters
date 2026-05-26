using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CtfStage
{
    /// <summary>
    /// Multi-layer procedural VFX with unique per-category textures and mesh effects.
    /// Lightning bolts, shockwave rings, matrix glyphs, gear shapes, spiral orbits —
    /// all generated at runtime (zero asset import).
    /// </summary>
    public static class StageVfx
    {
        // ══════════════════════════════════════════════════════════════════ //
        //  PREFAB OVERRIDE SYSTEM
        //  If a prefab is registered for an effect name, PlaySignature
        //  will Instantiate it instead of generating procedural particles.
        // ══════════════════════════════════════════════════════════════════ //
        static readonly Dictionary<string, GameObject> _prefabOverrides = new Dictionary<string, GameObject>();

        public static void RegisterPrefab(string effectName, GameObject prefab)
        {
            if (prefab == null) return;
            _prefabOverrides[effectName] = prefab;
            Debug.Log($"[StageVfx] Registered prefab override for '{effectName}': {prefab.name}");
        }

        public struct Spec
        {
            public int count;
            public float speedMin, speedMax;
            public float gravity;
            public float lifeMin, lifeMax;
            public float sizeMin, sizeMax;
            public Vector3 drift;
            public Vector3 spawnOffset;
            public bool hasRing;
            public int ringCount;
            public float ringSpeed, ringLife, ringSize;
            public bool hasTrail;
            public int trailCount;
            public float trailLife, trailSize, trailGravity;
            public bool hasSparks;
            public int sparkCount;
            public float sparkSpeed;
            public ParticleSystemShapeType shapeType;
            public float shapeRadius, shapeArc;
            public float rotationMin, rotationMax;
            public string texType; // "dot","star","streak","square","ring","cross"
        }

        public static readonly Dictionary<string, Spec> Specs = new Dictionary<string, Spec>
        {
            { "explosion", new Spec { // PWN — angular shrapnel
                count = 80, speedMin = 5f, speedMax = 14f, gravity = 1.2f,
                lifeMin = 0.5f, lifeMax = 1.2f, sizeMin = 0.25f, sizeMax = 0.8f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.3f,
                texType = "star",
                hasRing = true, ringCount = 40, ringSpeed = 8f, ringLife = 0.6f, ringSize = 0.3f,
                hasSparks = true, sparkCount = 30, sparkSpeed = 12f,
                hasTrail = true, trailCount = 20, trailLife = 1.5f, trailSize = 0.5f, trailGravity = 0.8f,
                rotationMin = -180f, rotationMax = 180f,
            }},
            { "matrix", new Spec { // WEB — code rain squares
                count = 140, speedMin = 0.2f, speedMax = 0.8f, gravity = 0f,
                lifeMin = 1.0f, lifeMax = 2.5f, sizeMin = 0.06f, sizeMax = 0.18f,
                drift = new Vector3(0, -5f, 0), spawnOffset = new Vector3(0, 5f, 0),
                shapeType = ParticleSystemShapeType.Box, shapeRadius = 4f,
                texType = "square",
                hasTrail = true, trailCount = 80, trailLife = 2.0f, trailSize = 0.12f, trailGravity = 0f,
                hasSparks = true, sparkCount = 15, sparkSpeed = 3f,
            }},
            { "lightning", new Spec { // WiFi — electric sparks
                count = 60, speedMin = 6f, speedMax = 18f, gravity = 0.3f,
                lifeMin = 0.1f, lifeMax = 0.4f, sizeMin = 0.06f, sizeMax = 0.18f,
                shapeType = ParticleSystemShapeType.Cone, shapeRadius = 0.1f, shapeArc = 15f,
                texType = "streak",
                hasRing = true, ringCount = 50, ringSpeed = 14f, ringLife = 0.25f, ringSize = 0.12f,
                hasSparks = true, sparkCount = 50, sparkSpeed = 18f,
                hasTrail = true, trailCount = 30, trailLife = 0.6f, trailSize = 0.1f, trailGravity = 0f,
            }},
            { "gears", new Spec { // REVERSE — hexagonal gears
                count = 45, speedMin = 1.5f, speedMax = 4f, gravity = -0.15f,
                lifeMin = 1.5f, lifeMax = 3.0f, sizeMin = 0.3f, sizeMax = 0.8f,
                drift = new Vector3(0, 2.5f, 0),
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.5f,
                texType = "cross",
                rotationMin = -360f, rotationMax = 360f,
                hasRing = true, ringCount = 24, ringSpeed = 3f, ringLife = 1.5f, ringSize = 0.4f,
                hasTrail = true, trailCount = 30, trailLife = 2f, trailSize = 0.2f, trailGravity = -0.1f,
            }},
            { "psychic", new Spec { // FORENSICS — spiral orbs
                count = 80, speedMin = 2f, speedMax = 6f, gravity = -0.05f,
                lifeMin = 1.0f, lifeMax = 2.0f, sizeMin = 0.12f, sizeMax = 0.4f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.2f,
                texType = "ring",
                rotationMin = -540f, rotationMax = 540f,
                hasRing = true, ringCount = 60, ringSpeed = 4f, ringLife = 1.2f, ringSize = 0.2f,
                hasTrail = true, trailCount = 50, trailLife = 1.5f, trailSize = 0.18f, trailGravity = 0f,
            }},
            { "vortex", new Spec { // CRYPTO — swirl energy
                count = 100, speedMin = 3f, speedMax = 8f, gravity = 0f,
                lifeMin = 0.7f, lifeMax = 1.5f, sizeMin = 0.12f, sizeMax = 0.35f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.3f,
                texType = "star",
                rotationMin = -720f, rotationMax = 720f,
                hasRing = true, ringCount = 50, ringSpeed = 6f, ringLife = 0.8f, ringSize = 0.25f,
                hasTrail = true, trailCount = 40, trailLife = 1.2f, trailSize = 0.15f, trailGravity = 0f,
                hasSparks = true, sparkCount = 20, sparkSpeed = 10f,
            }},
            { "shadow", new Spec { // IOT — dark smoke tendrils
                count = 70, speedMin = 1.5f, speedMax = 5f, gravity = -0.3f,
                lifeMin = 1.2f, lifeMax = 2.5f, sizeMin = 0.4f, sizeMax = 1.2f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.4f,
                texType = "dot",
                hasTrail = true, trailCount = 50, trailLife = 2.5f, trailSize = 0.6f, trailGravity = -0.15f,
                hasSparks = true, sparkCount = 15, sparkSpeed = 6f,
            }},
            { "flash", new Spec { // OSINT — bright expanding rings
                count = 50, speedMin = 5f, speedMax = 14f, gravity = 0f,
                lifeMin = 0.25f, lifeMax = 0.6f, sizeMin = 0.15f, sizeMax = 0.5f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.1f,
                texType = "ring",
                hasRing = true, ringCount = 80, ringSpeed = 16f, ringLife = 0.4f, ringSize = 0.2f,
                hasSparks = true, sparkCount = 30, sparkSpeed = 14f,
                hasTrail = true, trailCount = 20, trailLife = 0.5f, trailSize = 0.3f, trailGravity = 0f,
            }},
            { "root", new Spec { // B2R — golden crown eruption
                count = 60, speedMin = 2f, speedMax = 7f, gravity = 0.6f,
                lifeMin = 0.6f, lifeMax = 1.5f, sizeMin = 0.2f, sizeMax = 0.5f,
                drift = new Vector3(0, 4f, 0),
                shapeType = ParticleSystemShapeType.Cone, shapeRadius = 0.2f, shapeArc = 30f,
                texType = "star",
                hasRing = true, ringCount = 40, ringSpeed = 5f, ringLife = 0.8f, ringSize = 0.3f,
                hasSparks = true, sparkCount = 25, sparkSpeed = 8f,
                hasTrail = true, trailCount = 30, trailLife = 1.8f, trailSize = 0.25f, trailGravity = -0.2f,
            }},
            { "miss", new Spec {
                count = 14, speedMin = 1f, speedMax = 3f, gravity = 0.6f,
                lifeMin = 0.3f, lifeMax = 0.6f, sizeMin = 0.1f, sizeMax = 0.2f,
                shapeType = ParticleSystemShapeType.Sphere, shapeRadius = 0.15f,
                texType = "dot",
            }},
        };

        public static Spec SpecFor(string effect)
            => Specs.TryGetValue(effect, out var s) ? s : Specs["flash"];

        // ══════════════════════════════════════════════════════════════════ //
        //  PROCEDURAL TEXTURES — each shape is visually distinct
        // ══════════════════════════════════════════════════════════════════ //
        static readonly Dictionary<string, Texture2D> _texCache = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Material> _matCache = new Dictionary<string, Material>();

        static Texture2D GetTex(string type)
        {
            if (_texCache.TryGetValue(type, out var t)) return t;
            int sz = 64;
            switch (type)
            {
                case "star":    t = GenStar(sz); break;
                case "streak":  t = GenStreak(sz); break;
                case "square":  t = GenSquare(sz); break;
                case "ring":    t = GenRing(sz); break;
                case "cross":   t = GenCross(sz); break;
                default:        t = GenDot(sz, true); break; // "dot"
            }
            _texCache[type] = t;
            return t;
        }

        static Material GetMat(string texType)
        {
            if (_matCache.TryGetValue(texType, out var m)) return m;
            var tex = GetTex(texType);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            m = new Material(shader) { mainTexture = tex };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            _matCache[texType] = m;
            return m;
        }

        // ── soft glow dot ──
        static Texture2D GenDot(int sz, bool soft)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d);
                    if (soft) a *= a;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── 4-point star / explosion shard ──
        static Texture2D GenStar(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float c = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = Mathf.Abs(x - c) / c;
                    float dy = Mathf.Abs(y - c) / c;
                    float cross = Mathf.Min(dx, dy); // diamond-ish
                    float radial = Mathf.Sqrt(dx * dx + dy * dy);
                    float star = Mathf.Clamp01(1f - Mathf.Min(cross * 3f, radial));
                    star *= star;
                    t.SetPixel(x, y, new Color(1, 1, 1, star));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── elongated streak (for lightning / sparks) ──
        static Texture2D GenStreak(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float c = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = Mathf.Abs(x - c) / c;
                    float dy = Mathf.Abs(y - c) / c;
                    // narrow horizontal streak
                    float a = Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 4f);
                    a = Mathf.Pow(a, 0.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── sharp-edged square (matrix glyph block) ──
        static Texture2D GenSquare(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            int border = sz / 8;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    bool inside = x >= border && x < sz - border && y >= border && y < sz - border;
                    // inner glyph pattern (simple grid lines)
                    bool glyph = inside && (x % (sz / 4) < 2 || y % (sz / 4) < 2
                                 || (x + y) % (sz / 3) < 3);
                    float a = inside ? (glyph ? 1f : 0.7f) : 0f;
                    // soft outer edge
                    if (inside)
                    {
                        float ex = Mathf.Min(x - border, sz - border - 1 - x) / (float)border;
                        float ey = Mathf.Min(y - border, sz - border - 1 - y) / (float)border;
                        float edge = Mathf.Clamp01(Mathf.Min(ex, ey));
                        a *= Mathf.Lerp(0.5f, 1f, edge);
                    }
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── hollow ring (psychic / OSINT) ──
        static Texture2D GenRing(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float c = sz / 2f;
            float inner = 0.55f, outer = 0.85f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / c;
                    float ring = 1f - Mathf.Abs(d - (inner + outer) * 0.5f) / ((outer - inner) * 0.5f);
                    ring = Mathf.Clamp01(ring);
                    ring *= ring;
                    // add a center dot too
                    float dot = Mathf.Clamp01(1f - d / 0.2f);
                    float a = Mathf.Max(ring, dot * 0.6f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── cross / gear-teeth (reverse engineering) ──
        static Texture2D GenCross(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float c = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float dx = Mathf.Abs(x - c) / c;
                    float dy = Mathf.Abs(y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    // gear teeth: 6-fold symmetry
                    float angle = Mathf.Atan2(y - c, x - c);
                    float teeth = Mathf.Abs(Mathf.Sin(angle * 3f)); // 6 teeth
                    float gearR = 0.5f + teeth * 0.25f;
                    float gear = Mathf.Clamp01(1f - Mathf.Abs(d - gearR) / 0.15f);
                    // center hole
                    float hole = Mathf.Clamp01(d / 0.25f);
                    // cross arms
                    float cross = Mathf.Clamp01(1f - Mathf.Min(dx, dy) * 6f) * Mathf.Clamp01(1f - d / 0.9f);
                    float a = Mathf.Max(gear * hole, cross * 0.7f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ── bright glow (center flash) ──
        static Texture2D GenGlow(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            float r = sz / 2f;
            for (int y = 0; y < sz; y++)
                for (int x = 0; x < sz; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - d * d);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            return t;
        }

        // ══════════════════════════════════════════════════════════════════ //
        //  GRADIENTS
        // ══════════════════════════════════════════════════════════════════ //
        static Gradient FadeGrad(float hold = 0.3f)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, hold), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static Gradient FlashGrad()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.15f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        // ══════════════════════════════════════════════════════════════════ //
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════════════ //

        /// <summary>Full multi-layer signature effect for a category impact.</summary>
        public static void PlaySignature(Vector3 pos, string effectName, Color color, Color glow)
        {
            // If a prefab override exists, spawn it and skip procedural VFX
            if (_prefabOverrides.TryGetValue(effectName, out var prefab))
            {
                var go = Object.Instantiate(prefab, pos, Quaternion.identity);
                go.name = $"vfx_{effectName}_prefab";
                // Tint any particle systems to match category color
                foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(color, glow);
                }
                // Auto-destroy after 5 seconds
                Object.Destroy(go, 5f);
                // Still play the center flash for extra punch
                PlayCenterFlash(pos, glow);
                return;
            }

            // Fallback: procedural VFX
            var s = SpecFor(effectName);

            PlayBurst(pos, s, color, glow);                     // Layer 1: core
            if (s.hasRing)   PlayRing(pos, s, color, glow);     // Layer 2: shockwave
            if (s.hasTrail)  PlayAura(pos, s, color, glow);     // Layer 3: lingering
            if (s.hasSparks) PlaySparks(pos, s, color, glow);   // Layer 4: sparks
            PlayCenterFlash(pos, glow);                         // Layer 5: flash

            // special mesh effects per category
            if (effectName == "lightning")
                SpawnLightningBolt(pos, color, glow);
            else if (effectName == "explosion")
                SpawnShockwaveDisc(pos, color);
            else if (effectName == "psychic" || effectName == "vortex")
                SpawnSpiralOrbit(pos, color, glow, effectName == "vortex" ? 2f : 1.2f);
            else if (effectName == "flash")
                SpawnExpandingRings(pos, glow, 3);
            else if (effectName == "root")
                SpawnPillarBeam(pos, glow);
        }

        /// <summary>Single-layer burst (also used for muzzle flash).</summary>
        public static void PlayBurst(Vector3 pos, Spec s, Color color, Color glow)
        {
            var go = new GameObject("vfx_core");
            go.transform.position = pos + s.spawnOffset;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.duration = Mathf.Max(0.1f, s.lifeMax);
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.lifeMin, s.lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.speedMin, s.speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(s.sizeMin, s.sizeMax);
            main.startColor = new ParticleSystem.MinMaxGradient(color, glow);
            main.gravityModifier = s.gravity;
            main.maxParticles = s.count + 20;
            main.stopAction = ParticleSystemStopAction.Destroy;
            if (s.rotationMin != 0 || s.rotationMax != 0)
                main.startRotation = new ParticleSystem.MinMaxCurve(
                    s.rotationMin * Mathf.Deg2Rad, s.rotationMax * Mathf.Deg2Rad);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.count) });

            var shape = ps.shape;
            shape.shapeType = s.shapeType;
            shape.radius = s.shapeRadius > 0 ? s.shapeRadius : 0.15f;
            if (s.shapeType == ParticleSystemShapeType.Cone && s.shapeArc > 0)
                shape.angle = s.shapeArc;
            if (s.shapeType == ParticleSystemShapeType.Box)
                shape.scale = new Vector3(s.shapeRadius, 0.3f, 1f);

            if (s.drift != Vector3.zero)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = s.drift.x; vel.y = s.drift.y; vel.z = s.drift.z;
            }

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = FadeGrad();

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = GetMat(s.texType ?? "dot");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ── Layer 2: Shockwave ring ──
        static void PlayRing(Vector3 pos, Spec s, Color color, Color glow)
        {
            var go = new GameObject("vfx_ring");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop();

            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.duration = s.ringLife;
            main.startLifetime = s.ringLife;
            main.startSpeed = s.ringSpeed;
            main.startSize = s.ringSize;
            main.startColor = new ParticleSystem.MinMaxGradient(glow, Color.Lerp(glow, Color.white, 0.5f));
            main.maxParticles = s.ringCount + 10;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.ringCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.05f; shape.arc = 360f;

            var col = ps.colorOverLifetime;
            col.enabled = true; col.color = FlashGrad();

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 2.5f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = GetMat("streak");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ── Layer 3: Lingering aura ──
        static void PlayAura(Vector3 pos, Spec s, Color color, Color glow)
        {
            var go = new GameObject("vfx_aura");
            go.transform.position = pos + s.spawnOffset * 0.5f;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop();

            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.duration = s.trailLife;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.trailLife * 0.5f, s.trailLife);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(s.trailSize * 0.5f, s.trailSize);
            main.startColor = new ParticleSystem.MinMaxGradient(Color.Lerp(color, glow, 0.3f), glow);
            main.gravityModifier = s.trailGravity;
            main.maxParticles = s.trailCount + 10;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startRotation = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] {
                new ParticleSystem.Burst(0f, (short)(s.trailCount / 2)),
                new ParticleSystem.Burst(0.15f, (short)(s.trailCount / 2)),
            });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.6f;

            var noise = ps.noise;
            noise.enabled = true; noise.strength = 1.5f; noise.frequency = 2f;
            noise.scrollSpeed = 1f; noise.octaveCount = 2;

            var col = ps.colorOverLifetime;
            col.enabled = true; col.color = FadeGrad(0.1f);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.8f, 1f, 0f));

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = GetMat(s.texType ?? "dot");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ── Layer 4: Hot sparks with trails ──
        static void PlaySparks(Vector3 pos, Spec s, Color color, Color glow)
        {
            var go = new GameObject("vfx_sparks");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop();

            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.duration = 0.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.sparkSpeed * 0.5f, s.sparkSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(color, Color.white, 0.7f), Color.white);
            main.gravityModifier = 1.5f;
            main.maxParticles = s.sparkCount + 10;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)s.sparkCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;

            var trails = ps.trails;
            trails.enabled = true; trails.lifetime = 0.3f; trails.dieWithParticles = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var tGrad = new Gradient();
            tGrad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(glow, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });
            trails.colorOverLifetime = tGrad;

            var col = ps.colorOverLifetime;
            col.enabled = true; col.color = FlashGrad();

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = GetMat("streak");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.trailMaterial = GetMat("dot");
            ps.Play();
        }

        // ── Layer 5: Center glow flash ──
        static void PlayCenterFlash(Vector3 pos, Color glow)
        {
            var go = new GameObject("vfx_flash");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop();

            var main = ps.main;
            main.loop = false; main.playOnAwake = false;
            main.duration = 0.4f; main.startLifetime = 0.35f;
            main.startSpeed = 0f; main.startSize = 3.5f;
            main.startColor = new Color(glow.r, glow.g, glow.b, 0.8f);
            main.maxParticles = 2;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = ps.shape; shape.enabled = false;

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 5f));

            var col = ps.colorOverLifetime;
            col.enabled = true; col.color = FlashGrad();

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = GetMat("dot");
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ══════════════════════════════════════════════════════════════════ //
        //  MESH EFFECTS — category-specific geometry
        // ══════════════════════════════════════════════════════════════════ //

        /// <summary>WiFi: zigzag lightning bolt using LineRenderer.</summary>
        static void SpawnLightningBolt(Vector3 origin, Color color, Color glow)
        {
            // spawn 3 bolts for a dramatic strike
            for (int b = 0; b < 3; b++)
            {
                var go = new GameObject("vfx_bolt");
                go.transform.position = origin;
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.startWidth = 0.15f - b * 0.03f;
                lr.endWidth = 0.03f;
                lr.startColor = glow;
                lr.endColor = color;
                var mat = new Material(Shader.Find("Sprites/Default") ??
                    Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                mat.color = glow;
                lr.material = mat;

                // zigzag from top to impact point
                int segs = 12;
                lr.positionCount = segs;
                Vector3 top = origin + Vector3.up * 6f + Random.insideUnitSphere * 1.5f;
                for (int i = 0; i < segs; i++)
                {
                    float t = i / (float)(segs - 1);
                    Vector3 p = Vector3.Lerp(top, origin, t);
                    if (i > 0 && i < segs - 1)
                        p += new Vector3(Random.Range(-0.6f, 0.6f), 0, Random.Range(-0.3f, 0.3f));
                    lr.SetPosition(i, p);
                }

                var fader = go.AddComponent<VfxAutoFade>();
                fader.Init(0.3f + b * 0.08f);
            }
        }

        /// <summary>PWN: expanding shockwave disc on the ground.</summary>
        static void SpawnShockwaveDisc(Vector3 pos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "vfx_shockwave";
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            go.transform.position = pos - Vector3.up * 0.3f;
            go.transform.localScale = new Vector3(0.1f, 0.02f, 0.1f);

            var rend = go.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                Shader.Find("Sprites/Default"));
            mat.color = new Color(color.r, color.g, color.b, 0.6f);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * 3f);
            // transparent
            mat.SetFloat("_Surface", 1f); // transparent
            mat.SetFloat("_Blend", 0f);
            mat.renderQueue = 3000;
            rend.material = mat;

            var expander = go.AddComponent<VfxExpandAndFade>();
            expander.Init(12f, 0.6f); // expand to 12m radius over 0.6s
        }

        /// <summary>FORENSICS/CRYPTO: orbiting particles in a spiral.</summary>
        static void SpawnSpiralOrbit(Vector3 center, Color color, Color glow, float duration)
        {
            var go = new GameObject("vfx_spiral");
            go.transform.position = center;
            var orbit = go.AddComponent<VfxSpiralOrbit>();
            orbit.Init(color, glow, 8, 1.5f, duration);
        }

        /// <summary>OSINT: multiple expanding ring outlines.</summary>
        static void SpawnExpandingRings(Vector3 pos, Color glow, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("vfx_ringline");
                go.transform.position = pos;
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.startWidth = 0.08f; lr.endWidth = 0.08f;
                lr.startColor = glow; lr.endColor = glow;
                var mat = new Material(Shader.Find("Sprites/Default") ??
                    Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                mat.color = glow;
                lr.material = mat;

                int segs = 36;
                lr.positionCount = segs;
                for (int s = 0; s < segs; s++)
                {
                    float angle = s / (float)segs * Mathf.PI * 2f;
                    float r = 0.1f;
                    lr.SetPosition(s, pos + new Vector3(Mathf.Cos(angle) * r, i * 0.3f, Mathf.Sin(angle) * r));
                }

                var expander = go.AddComponent<VfxRingExpand>();
                expander.Init(pos + Vector3.up * i * 0.3f, 6f, 0.5f + i * 0.15f);
            }
        }

        /// <summary>B2R: vertical beam pillar shooting upward.</summary>
        static void SpawnPillarBeam(Vector3 pos, Color glow)
        {
            var go = new GameObject("vfx_pillar");
            go.transform.position = pos;
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.5f; lr.endWidth = 0.05f;
            lr.startColor = glow; lr.endColor = new Color(glow.r, glow.g, glow.b, 0f);
            var mat = new Material(Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            mat.color = glow;
            lr.material = mat;
            lr.positionCount = 2;
            lr.SetPosition(0, pos);
            lr.SetPosition(1, pos + Vector3.up * 8f);

            var fader = go.AddComponent<VfxAutoFade>();
            fader.Init(1.2f);
        }
    }

    // ══════════════════════════════════════════════════════════════════════ //
    //  HELPER MONOBEHAVIOURS — self-destroying mesh effect drivers
    // ══════════════════════════════════════════════════════════════════════ //

    public class VfxAutoFade : MonoBehaviour
    {
        float life;
        float elapsed;
        LineRenderer lr;
        public void Init(float duration) { life = duration; lr = GetComponent<LineRenderer>(); }
        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= life) { Destroy(gameObject); return; }
            float a = 1f - elapsed / life;
            if (lr != null) { lr.startColor = Fade(lr.startColor, a); lr.endColor = Fade(lr.endColor, a * 0.5f); }
        }
        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);
    }

    public class VfxExpandAndFade : MonoBehaviour
    {
        float targetRadius, duration, elapsed;
        Renderer rend;
        public void Init(float radius, float dur) { targetRadius = radius; duration = dur; rend = GetComponent<Renderer>(); }
        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Destroy(gameObject); return; }
            float t = elapsed / duration;
            float r = Mathf.Lerp(0.1f, targetRadius, t);
            transform.localScale = new Vector3(r, 0.02f, r);
            if (rend != null)
            {
                var c = rend.material.color;
                rend.material.color = new Color(c.r, c.g, c.b, (1f - t) * 0.6f);
            }
        }
    }

    public class VfxSpiralOrbit : MonoBehaviour
    {
        GameObject[] orbs;
        float duration, elapsed, radius;
        public void Init(Color color, Color glow, int count, float r, float dur)
        {
            radius = r; duration = dur;
            orbs = new GameObject[count];
            for (int i = 0; i < count; i++)
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "orb";
                var col = orb.GetComponent<Collider>();
                if (col != null) Destroy(col);
                orb.transform.localScale = Vector3.one * 0.15f;
                var rend = orb.GetComponent<Renderer>();
                var mat = rend.material;
                mat.color = Color.Lerp(color, glow, 0.5f);
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", glow * 2f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                // add trail
                var trail = orb.AddComponent<TrailRenderer>();
                trail.time = 0.4f; trail.startWidth = 0.1f; trail.endWidth = 0f;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.startColor = glow; trail.endColor = new Color(color.r, color.g, color.b, 0f);
                orbs[i] = orb;
            }
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { foreach (var o in orbs) if (o) Destroy(o); Destroy(gameObject); return; }
            float fade = 1f - elapsed / duration;
            for (int i = 0; i < orbs.Length; i++)
            {
                if (orbs[i] == null) continue;
                float angle = (elapsed * 360f / 0.5f + i * 360f / orbs.Length) * Mathf.Deg2Rad;
                float r = radius * Mathf.Sin(elapsed / duration * Mathf.PI); // expand then contract
                float y = Mathf.Sin(elapsed * 3f + i) * 0.5f;
                orbs[i].transform.position = transform.position + new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
                orbs[i].transform.localScale = Vector3.one * 0.15f * fade;
            }
        }
    }

    public class VfxRingExpand : MonoBehaviour
    {
        Vector3 center;
        float targetRadius, duration, elapsed;
        LineRenderer lr;
        public void Init(Vector3 c, float r, float dur) { center = c; targetRadius = r; duration = dur; lr = GetComponent<LineRenderer>(); }
        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Destroy(gameObject); return; }
            float t = elapsed / duration;
            float r = Mathf.Lerp(0.1f, targetRadius, t);
            float a = (1f - t);
            for (int i = 0; i < lr.positionCount; i++)
            {
                float angle = i / (float)lr.positionCount * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * r, 0, Mathf.Sin(angle) * r));
            }
            lr.startColor = new Color(lr.startColor.r, lr.startColor.g, lr.startColor.b, a);
            lr.endColor = new Color(lr.endColor.r, lr.endColor.g, lr.endColor.b, a);
            lr.startWidth = 0.08f * a;
            lr.endWidth = 0.08f * a;
        }
    }
}
