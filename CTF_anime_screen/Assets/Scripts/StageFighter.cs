using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CtfStage
{
    /// <summary>
    /// One team's character. Supports three animation modes:
    /// 1. AnimationClips from GLB → cloned as legacy + played via Animation component
    /// 2. Existing Animator → trigger-based
    /// 3. No animation → procedural lunge/flinch
    /// </summary>
    public class StageFighter : MonoBehaviour
    {
        [Header("Model hooks")]
        public Animator animator;
        public Transform fistPoint;
        public int facing = 1;

        [Header("Animator trigger names")]
        public string attackTrigger = "Attack";
        public string hurtTrigger = "Hurt";
        public string winTrigger = "Win";

        [Header("Animation Clips (assigned at runtime by StageBootstrap)")]
        public AnimationClip idleClip;
        public AnimationClip attackClip;
        public AnimationClip hurtClip;
        public AnimationClip winClip;

        Vector3 baseLocalPos, baseScale;
        Coroutine punching;
        bool clipAnimReady;
        Animation legacyAnim;

        void Awake()
        {
            baseLocalPos = transform.localPosition;
            baseScale = transform.localScale;
        }

        void Start()
        {
            StartCoroutine(DelayedSetup());
        }

        IEnumerator DelayedSetup()
        {
            yield return null; // wait one frame for StageBootstrap to assign clips

            if (idleClip != null || attackClip != null)
            {
                Debug.Log($"[StageFighter] {name}: setting up clip anim. idle={idleClip?.name}, attack={attackClip?.name}");
                SetupClipAnimation();
            }
            else
            {
                Debug.Log($"[StageFighter] {name}: no clips, using procedural fallback");
            }
        }

        void SetupClipAnimation()
        {
            legacyAnim = gameObject.GetComponent<Animation>();
            if (legacyAnim == null) legacyAnim = gameObject.AddComponent<Animation>();

            if (idleClip != null)
            {
                var clip = CloneAsLegacy(idleClip, WrapMode.Loop);
                legacyAnim.AddClip(clip, "idle");
                legacyAnim.clip = clip;
                legacyAnim.Play("idle");
                Debug.Log($"[StageFighter] {name}: playing idle ({idleClip.length:F2}s, {CountCurves(idleClip)} curves)");
            }
            if (attackClip != null)
            {
                var clip = CloneAsLegacy(attackClip, WrapMode.Once);
                legacyAnim.AddClip(clip, "attack");
            }
            if (hurtClip != null)
            {
                var clip = CloneAsLegacy(hurtClip, WrapMode.Once);
                legacyAnim.AddClip(clip, "hurt");
            }
            if (winClip != null)
            {
                var clip = CloneAsLegacy(winClip, WrapMode.Once);
                legacyAnim.AddClip(clip, "win");
            }
            clipAnimReady = true;
        }

        /// Clone an AnimationClip and convert to legacy mode so Animation component can play it
        static AnimationClip CloneAsLegacy(AnimationClip src, WrapMode wrap)
        {
            var clone = new AnimationClip();
            clone.legacy = true;
            clone.wrapMode = wrap;
            clone.name = src.name + "_legacy";

#if UNITY_EDITOR
            var bindings = AnimationUtility.GetCurveBindings(src);
            foreach (var binding in bindings)
            {
                var curve = AnimationUtility.GetEditorCurve(src, binding);
                if (curve != null)
                    clone.SetCurve(binding.path, binding.type, binding.propertyName, curve);
            }

            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(src);
            foreach (var binding in objBindings)
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(src, binding);
                AnimationUtility.SetObjectReferenceCurve(clone, binding, keyframes);
            }
#endif

            clone.EnsureQuaternionContinuity();
            return clone;
        }

        static int CountCurves(AnimationClip clip)
        {
#if UNITY_EDITOR
            return AnimationUtility.GetCurveBindings(clip).Length;
#else
            return -1;
#endif
        }

        public Vector3 FistPosition =>
            fistPoint != null ? fistPoint.position
                              : transform.position + new Vector3(facing * 0.5f, 1.1f, 0f);

        public void Attack()
        {
            if (clipAnimReady && legacyAnim != null && legacyAnim.GetClip("attack") != null)
            {
                legacyAnim.CrossFade("attack", 0.15f);
                if (punching != null) StopCoroutine(punching);
                punching = StartCoroutine(ReturnToIdle(attackClip.length));
                return;
            }
            if (animator != null) animator.SetTrigger(attackTrigger);
            else Punch(new Vector3(facing * 0.5f, 0, 0), 1.12f, 0.25f);
        }

        public void Hurt()
        {
            if (clipAnimReady && legacyAnim != null && legacyAnim.GetClip("hurt") != null)
            {
                legacyAnim.CrossFade("hurt", 0.15f);
                if (punching != null) StopCoroutine(punching);
                punching = StartCoroutine(ReturnToIdle(hurtClip.length));
                return;
            }
            if (animator != null) animator.SetTrigger(hurtTrigger);
            else Punch(new Vector3(-facing * 0.3f, 0, 0), 0.9f, 0.25f);
        }

        public void Win()
        {
            if (clipAnimReady && legacyAnim != null && legacyAnim.GetClip("win") != null)
            {
                legacyAnim.CrossFade("win", 0.15f);
                return;
            }
            if (animator != null) animator.SetTrigger(winTrigger);
            else Punch(Vector3.zero, 1.15f, 0.4f);
        }

        IEnumerator ReturnToIdle(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (legacyAnim != null && legacyAnim.GetClip("idle") != null)
                legacyAnim.CrossFade("idle", 0.3f);
            punching = null;
        }

        void Punch(Vector3 offset, float scale, float dur)
        {
            if (punching != null) StopCoroutine(punching);
            punching = StartCoroutine(PunchCo(offset, scale, dur));
        }

        IEnumerator PunchCo(Vector3 offset, float scale, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float e = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                transform.localPosition = baseLocalPos + offset * e;
                transform.localScale = Vector3.Lerp(baseScale, baseScale * scale, e);
                yield return null;
            }
            transform.localPosition = baseLocalPos;
            transform.localScale = baseScale;
            punching = null;
        }
    }
}
