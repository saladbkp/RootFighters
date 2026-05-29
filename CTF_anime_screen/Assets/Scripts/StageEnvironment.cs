using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CtfStage
{
    /// <summary>
    /// Builds the "cute &amp; cool" anime battle stage entirely from code:
    /// a gradient sky backdrop with a horizon glow, a neon grid floor, two
    /// team-colored glow platforms, a center divider, drifting ambient embers,
    /// and a Bloom/Vignette post-processing volume to make it all pop.
    ///
    /// All textures/materials are generated at runtime — no art assets needed.
    /// Called by StageBootstrap.
    /// </summary>
    public static class StageEnvironment
    {
        static Material _emberMat;

        public static void Build(Color teamA, Color teamB, float spacing, float groundY, Color sky,
            Color teamC = default, Color teamD = default)
        {
            Color neon = Color.Lerp(teamA, teamB, 0.5f);
            BuildBackdrop(sky, neon);
            BuildFloor(groundY, neon);
            float zBack = spacing * 0.8f;
            float zFront = -spacing * 0.4f;
            BuildPlatform("Platform_A", -spacing * 0.7f, zBack, groundY, teamA);
            BuildPlatform("Platform_B", +spacing * 0.7f, zBack, groundY, teamB);
            if (teamC != default) BuildPlatform("Platform_C", -spacing, zFront, groundY, teamC);
            if (teamD != default) BuildPlatform("Platform_D", +spacing, zFront, groundY, teamD);
            BuildDivider(groundY, neon);
            BuildEmbers(spacing, groundY, neon);
            BuildPostFx();
            EnablePostOnCamera();
        }

        // ---- sky backdrop (big unlit quad with a vertical gradient) ---------- //
        static void BuildBackdrop(Color sky, Color accent)
        {
            var top = sky * 0.5f; top.a = 1;
            var bottom = Color.black; bottom.a = 1;
            var tex = MakeVerticalGradient(256, top, bottom, accent * 0.8f, 0.42f, 0.14f);

            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Backdrop";
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.position = new Vector3(0f, 7f, 16f);
            q.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            q.transform.localScale = new Vector3(70f, 40f, 1f);

            var m = MakeMat("Universal Render Pipeline/Unlit", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            q.GetComponent<Renderer>().material = m;
        }

        // ---- neon grid floor ------------------------------------------------- //
        static void BuildFloor(float groundY, Color neon)
        {
            var grid = MakeGrid(256, new Color(0.02f, 0.015f, 0.04f, 1f), neon, 32);

            var g = GameObject.CreatePrimitive(PrimitiveType.Plane);
            g.name = "GridFloor";
            Object.Destroy(g.GetComponent<Collider>());
            g.transform.position = new Vector3(0f, groundY, 0f);
            g.transform.localScale = Vector3.one * 4f;

            var m = MakeMat("Universal Render Pipeline/Lit", grid);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.18f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.7f);
            m.SetTextureScale("_BaseMap", new Vector2(8, 8));
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionMap")) { m.SetTexture("_EmissionMap", grid); m.SetTextureScale("_EmissionMap", new Vector2(8, 8)); }
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", neon * 1.4f);
            g.GetComponent<Renderer>().material = m;
        }

        // ---- glowing team platforms ----------------------------------------- //
        static void BuildPlatform(string name, float x, float z, float groundY, Color color)
        {
            var p = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            p.name = name;
            Object.Destroy(p.GetComponent<Collider>());
            p.transform.position = new Vector3(x, groundY + 0.06f, z);
            p.transform.localScale = new Vector3(1.8f, 0.06f, 1.8f);
            var m = MakeMat("Universal Render Pipeline/Lit", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color * 0.6f);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * 2.5f);
            p.GetComponent<Renderer>().material = m;
        }

        // ---- center divider glow line --------------------------------------- //
        static void BuildDivider(float groundY, Color neon)
        {
            var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
            d.name = "CenterLine";
            Object.Destroy(d.GetComponent<Collider>());
            d.transform.position = new Vector3(0f, groundY + 0.02f, 0f);
            d.transform.localScale = new Vector3(0.08f, 0.04f, 16f);
            var m = MakeMat("Universal Render Pipeline/Unlit", null);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", neon * 2f);
            d.GetComponent<Renderer>().material = m;
        }

        // ---- drifting ambient embers ---------------------------------------- //
        static void BuildEmbers(float spacing, float groundY, Color neon)
        {
            if (_emberMat == null)
            {
                _emberMat = MakeMat("Universal Render Pipeline/Particles/Unlit", MakeDot(64));
            }

            // main embers — holographic blue/purple drift upward
            var go = new GameObject("Embers");
            go.transform.position = new Vector3(0f, groundY, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 0.5f, 1f, 0.6f),
                new Color(0.8f, 0.3f, 1f, 0.4f));
            main.gravityModifier = -0.03f;
            main.maxParticles = 200;

            var emission = ps.emission;
            emission.rateOverTime = 25f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spacing * 3f, 0.2f, 7f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = _emberMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();

            // floating data particles — green/cyan motes rising slowly (sci-fi "data in the air")
            BuildFloatingData(spacing, groundY);

            // ground-level neon sparks — small pink/blue flickers along the floor
            BuildNeonSparks(spacing, groundY, neon);
        }

        static void BuildFloatingData(float spacing, float groundY)
        {
            var mat = MakeMat("Universal Render Pipeline/Particles/Unlit", MakeSquareDot(32));
            var go = new GameObject("FloatingData");
            go.transform.position = new Vector3(0f, groundY + 2f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.15f, 1f, 0.5f, 0.3f),
                new Color(0f, 0.7f, 1f, 0.2f));
            main.gravityModifier = -0.02f;
            main.maxParticles = 150;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 20f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spacing * 3f, 4f, 6f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        static void BuildNeonSparks(float spacing, float groundY, Color neon)
        {
            var go = new GameObject("NeonSparks");
            go.transform.position = new Vector3(0f, groundY + 0.08f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.035f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.3f, 0.7f, 0.6f),
                new Color(0.3f, 0.5f, 1f, 0.6f));
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(spacing * 2.5f, 0.05f, 4f);

            var r = go.GetComponent<ParticleSystemRenderer>();
            if (_emberMat != null) r.material = _emberMat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Play();
        }

        // ---- post processing (bloom makes the neon glow) -------------------- //
        static void BuildPostFx()
        {
            var go = new GameObject("PostFX_Volume");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 10f;   // win over the project default profile
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.profile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(1.8f);
            bloom.threshold.Override(0.7f);
            bloom.scatter.Override(0.75f);

            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.38f);
            vig.smoothness.Override(0.6f);

            var ca = profile.Add<ColorAdjustments>(true);
            ca.postExposure.Override(0.2f);
            ca.saturation.Override(18f);
        }

        static void EnablePostOnCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }

        // ---- texture / material helpers ------------------------------------- //
        static Material MakeMat(string shaderName, Texture tex)
        {
            var sh = Shader.Find(shaderName) ?? Shader.Find("Sprites/Default");
            var m = new Material(sh);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            return m;
        }

        static Texture2D MakeVerticalGradient(int h, Color top, Color bottom, Color accent, float bandCenter, float bandWidth)
        {
            var t = new Texture2D(4, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                Color c = Color.Lerp(bottom, top, v);
                float band = Mathf.Exp(-((v - bandCenter) * (v - bandCenter)) / (2f * bandWidth * bandWidth));
                c += accent * band * 0.8f;
                c.a = 1f;
                for (int x = 0; x < 4; x++) t.SetPixel(x, y, c);
            }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        static Texture2D MakeGrid(int size, Color baseC, Color line, int cell)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x % cell, cell - (x % cell));
                    int dy = Mathf.Min(y % cell, cell - (y % cell));
                    float d = Mathf.Min(dx, dy);
                    float lineAmt = Mathf.Clamp01(1f - d / 2.2f);   // bright on the lines, soft falloff
                    t.SetPixel(x, y, Color.Lerp(baseC, line, lineAmt));
                }
            t.wrapMode = TextureWrapMode.Repeat;
            t.Apply();
            return t;
        }

        static Texture2D MakeDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01(1f - dist); a *= a;
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }

        // small glowing square for "floating data" particles
        static Texture2D MakeSquareDot(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            int pad = size / 6;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool inside = x >= pad && x < size - pad && y >= pad && y < size - pad;
                    float a = inside ? 0.8f : 0f;
                    // soft edge
                    if (inside)
                    {
                        int dx = Mathf.Min(x - pad, size - pad - 1 - x);
                        int dy = Mathf.Min(y - pad, size - pad - 1 - y);
                        int edge = Mathf.Min(dx, dy);
                        if (edge < 2) a = 0.4f;
                    }
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.wrapMode = TextureWrapMode.Clamp;
            t.Apply();
            return t;
        }
    }
}
