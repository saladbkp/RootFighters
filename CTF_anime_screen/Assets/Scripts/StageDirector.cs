using System.Collections;
using UnityEngine;

namespace CtfStage
{
    /// <summary>
    /// Battle choreography (Street-Fighter flow). On SOLVE: the attacker plays its
    /// attack animation; after a short windup (so the VFX lands on the punch
    /// contact) a category-colored projectile launches from the attacker's FIST,
    /// flies to the defender, bursts with the matching StageVfx effect, and the
    /// defender plays Hurt. On WRONG: the offending fighter flinches. On MATCH_END:
    /// the winner plays Win.
    ///
    /// Camera zoom-in: on each solve the camera punches toward the attacker,
    /// then smoothly returns. Gives each attack a cinematic beat.
    ///
    /// Fighters are assigned by StageBootstrap (or in the Inspector). A = left, B = right.
    /// </summary>
    [RequireComponent(typeof(StageClient))]
    public class StageDirector : MonoBehaviour
    {
        [Header("Fighters (auto-set by StageBootstrap if empty)")]
        public StageFighter teamA;
        public StageFighter teamB;

        [Header("Tuning")]
        public float projectileSeconds = 0.35f;
        public float arcHeight = 1.2f;
        public float projectileSize = 0.4f;
        public float impactHeight = 1.5f;        // where on the defender the hit lands

        [Header("Camera Zoom")]
        public float zoomCloseUpDist = 4.0f;      // how close the camera gets to attacker
        public float zoomInDuration = 0.4f;        // time to zoom into attacker
        public float attackHoldDuration = 0.8f;    // hold close-up during attack anim
        public float zoomOutDuration = 0.5f;       // time to zoom back out
        public float screenShakeIntensity = 0.15f;
        public float screenShakeDuration = 0.3f;

        StageClient client;
        StageScreens screens;
        Camera mainCam;
        Vector3 camRestPos;
        Quaternion camRestRot;
        Coroutine camCoroutine;

        // queued attack waiting for banner to finish
        StageFighter pendingAtk, pendingDef;
        CategoryInfo pendingInfo;
        bool attackQueued;

        void Awake()
        {
            client = GetComponent<StageClient>();
            screens = GetComponent<StageScreens>();
        }

        void Start()
        {
            mainCam = Camera.main;
            if (mainCam != null)
            {
                camRestPos = mainCam.transform.position;
                camRestRot = mainCam.transform.rotation;
            }
            if (screens != null)
                screens.OnAttackBannerDone += FireQueuedAttack;
        }

        void OnDestroy()
        {
            if (screens != null)
                screens.OnAttackBannerDone -= FireQueuedAttack;
        }

        void OnEnable()
        {
            client.OnSolve += HandleSolve;
            client.OnWrong += HandleWrong;
            client.OnMatchEnd += HandleEnd;
        }

        void OnDisable()
        {
            client.OnSolve -= HandleSolve;
            client.OnWrong -= HandleWrong;
            client.OnMatchEnd -= HandleEnd;
        }

        void HandleSolve(SolveData d)
        {
            var info = StageConfig.Cat(d.category);
            bool aAttacks = d.team == "A";
            StageFighter atk = aAttacks ? teamA : teamB;
            StageFighter def = aAttacks ? teamB : teamA;
            if (atk == null || def == null) return;

            // queue the attack — banner plays first, then FireQueuedAttack runs
            pendingAtk = atk;
            pendingDef = def;
            pendingInfo = info;
            attackQueued = true;

            // if no StageScreens (shouldn't happen), fire immediately
            if (screens == null)
                FireQueuedAttack();
        }

        void FireQueuedAttack()
        {
            if (!attackQueued) return;
            attackQueued = false;

            var atk = pendingAtk;
            var def = pendingDef;
            var info = pendingInfo;
            if (atk == null || def == null) return;

            // Start the full cinematic attack sequence
            StartCoroutine(CinematicAttack(atk, def, info));
        }

        /// <summary>
        /// Full cinematic attack:
        /// 1. Zoom into attacker (close-up)
        /// 2. Attacker plays attack animation, hold for drama
        /// 3. Zoom out while projectile fires
        /// 4. Impact VFX + screen shake on hit
        /// </summary>
        IEnumerator CinematicAttack(StageFighter atk, StageFighter def, CategoryInfo info)
        {
            // === PHASE 1: ZOOM INTO ATTACKER ===
            if (mainCam != null)
            {
                Vector3 atkCenter = atk.transform.position + Vector3.up * impactHeight;
                Vector3 dirToCam = (camRestPos - atkCenter).normalized;
                Vector3 closeUpPos = atkCenter + dirToCam * zoomCloseUpDist;
                // Look at the attacker
                if (camCoroutine != null) StopCoroutine(camCoroutine);
                camCoroutine = StartCoroutine(CameraMoveTo(closeUpPos, atkCenter, zoomInDuration));
            }

            yield return new WaitForSeconds(zoomInDuration * 0.5f);

            // === PHASE 2: ATTACK ANIMATION (during close-up) ===
            atk.Attack();

            // Hold close-up so the player can see the attack pose
            yield return new WaitForSeconds(attackHoldDuration);

            // === PHASE 3: ZOOM OUT + FIRE PROJECTILE ===
            // Start zooming back while the projectile flies
            if (mainCam != null)
            {
                if (camCoroutine != null) StopCoroutine(camCoroutine);
                camCoroutine = StartCoroutine(CameraMoveTo(camRestPos, Vector3.up * impactHeight, zoomOutDuration));
            }

            // Small delay then fire projectile
            yield return new WaitForSeconds(0.15f);

            Vector3 from = atk.FistPosition;
            Vector3 to = def.transform.position + Vector3.up * impactHeight;

            // muzzle flash at the fist
            StageVfx.PlayBurst(from, StageVfx.SpecFor("flash"), info.color, info.glow);

            // projectile fist → target (slight arc)
            var proj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = proj.GetComponent<Collider>();
            if (col != null) Destroy(col);
            proj.transform.localScale = Vector3.one * projectileSize;
            TintEmissive(proj.GetComponent<Renderer>(), info.color, info.glow);

            // projectile trail
            var trail = proj.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = projectileSize * 0.8f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trail.startColor = info.glow;
            trail.endColor = new Color(info.color.r, info.color.g, info.color.b, 0f);

            float t = 0f;
            while (t < projectileSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / projectileSeconds);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y += Mathf.Sin(k * Mathf.PI) * arcHeight;
                proj.transform.position = p;
                proj.transform.Rotate(Vector3.forward, 720f * Time.deltaTime);
                yield return null;
            }
            Destroy(proj);

            // === PHASE 4: IMPACT ===
            StageVfx.PlaySignature(to, info.effect, info.color, info.glow);
            def.Hurt();

            // Screen shake on impact
            if (mainCam != null)
            {
                if (camCoroutine != null) StopCoroutine(camCoroutine);
                camCoroutine = StartCoroutine(CameraShake());
            }
        }

        void HandleWrong(WrongData d)
        {
            StageFighter who = d.team == "A" ? teamA : teamB;
            if (who == null) return;
            who.Hurt();
            StageVfx.PlayBurst(who.FistPosition, StageVfx.SpecFor("miss"),
                new Color(1f, 0.32f, 0.32f), new Color(1f, 0.6f, 0.6f));

            // small shake on wrong
            if (mainCam != null)
            {
                if (camCoroutine != null) StopCoroutine(camCoroutine);
                camCoroutine = StartCoroutine(CameraShake());
            }
        }

        void HandleEnd(MatchEndData d)
        {
            if (d.winner == "A" && teamA != null) teamA.Win();
            else if (d.winner == "B" && teamB != null) teamB.Win();
        }

        // ================= Camera Effects ================= //

        IEnumerator CameraMoveTo(Vector3 targetPos, Vector3 lookAt, float duration)
        {
            Vector3 startPos = mainCam.transform.position;
            Quaternion startRot = mainCam.transform.rotation;
            Quaternion targetRot = Quaternion.LookRotation(lookAt - targetPos);

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float ease = EaseInOutQuad(Mathf.Clamp01(t / duration));
                mainCam.transform.position = Vector3.Lerp(startPos, targetPos, ease);
                mainCam.transform.rotation = Quaternion.Slerp(startRot, targetRot, ease);
                yield return null;
            }
            mainCam.transform.position = targetPos;
            mainCam.transform.rotation = targetRot;
            camCoroutine = null;
        }

        IEnumerator CameraShake()
        {
            float t = 0f;
            while (t < screenShakeDuration)
            {
                t += Time.deltaTime;
                float decay = 1f - Mathf.Clamp01(t / screenShakeDuration);
                Vector3 shake = Random.insideUnitSphere * screenShakeIntensity * 0.5f * decay;
                mainCam.transform.position = camRestPos + shake;
                yield return null;
            }
            mainCam.transform.position = camRestPos;
            camCoroutine = null;
        }

        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
        static float EaseInOutQuad(float t) => t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;

        static void TintEmissive(Renderer r, Color baseCol, Color glow)
        {
            if (r == null) return;
            var m = r.material; // instance
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", baseCol);
            if (m.HasProperty("_Color")) m.SetColor("_Color", baseCol);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow * 2f);
        }
    }
}
