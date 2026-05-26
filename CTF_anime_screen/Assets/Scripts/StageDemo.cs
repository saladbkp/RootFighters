using System.Collections;
using UnityEngine;

namespace CtfStage
{
    /// <summary>
    /// Minimal verification consumer. Put it on the SAME GameObject as
    /// StageClient, press Play, then drive backend/mock_server.py.
    ///
    /// What you'll see:
    ///   • every event logged to the Console (color-coded for SOLVE)
    ///   • the main Camera background flashes the category color on each SOLVE
    ///
    /// This proves the pipeline end-to-end with zero art. Once it works,
    /// replace these handlers with your real VFX/character/HUD code.
    /// </summary>
    [RequireComponent(typeof(StageClient))]
    public class StageDemo : MonoBehaviour
    {
        StageClient client;
        Camera cam;
        Color baseBg;

        void Awake()
        {
            client = GetComponent<StageClient>();
            cam = Camera.main;
            if (cam) baseBg = cam.backgroundColor;
        }

        void OnEnable()
        {
            client.OnConnectionChanged += c => Debug.Log($"[StageDemo] connection: {(c ? "LIVE" : "offline")}");
            client.OnState       += HandleState;
            client.OnMatchStart  += d => Debug.Log($"[MATCH_START] {d.round}: {d.teamA.name} vs {d.teamB.name} ({d.durationSec}s)");
            client.OnSolve       += HandleSolve;
            client.OnWrong       += d => Debug.Log($"[WRONG] Team {d.team} ({d.category})");
            client.OnBan         += d => Debug.Log($"[BAN] Team {d.team} → {d.category}");
            client.OnTimer       += HandleTimer;
            client.OnMatchEnd    += d => Debug.Log($"[MATCH_END] winner={d.winner}  {d.scoreA}:{d.scoreB}  ({d.reason})");
            client.OnAnnounce    += d => Debug.Log($"[ANNOUNCE] {d.text}");
        }

        void HandleState(MatchState s)
        {
            Debug.Log($"[STATE] {s.round} [{s.phase}]  {s.teamA.name} {s.teamA.score} : {s.teamB.score} {s.teamB.name}");
        }

        void HandleSolve(SolveData d)
        {
            var c = StageConfig.Cat(d.category);
            Debug.Log($"<color=#{ColorUtility.ToHtmlStringRGB(c.color)}>[SOLVE] Team {d.team} → {c.label}  " +
                      $"effect=\"{c.effect}\"  (+{d.points})  score {d.scoreA}:{d.scoreB}</color>");
            // TODO: this is where you'd play the real attack VFX:
            //   var prefab = LookupEffectPrefab(c.effect);
            //   SpawnAttack(d.team, prefab, c.color);
            if (cam) { StopAllCoroutines(); StartCoroutine(FlashBg(c.color)); }
        }

        // TIMER is ~1/sec; logging every tick is noisy, so only log milestones.
        int lastLogged = -1;
        void HandleTimer(TimerData d)
        {
            if (!d.running) return;
            if (d.remainingSec <= 10 || d.remainingSec % 60 == 0)
            {
                if (d.remainingSec != lastLogged)
                {
                    lastLogged = d.remainingSec;
                    Debug.Log($"[TIMER] {d.remainingSec}s left");
                }
            }
        }

        IEnumerator FlashBg(Color c)
        {
            float t = 0f, dur = 0.45f;
            while (t < dur)
            {
                t += Time.deltaTime;
                if (cam) cam.backgroundColor = Color.Lerp(c, baseBg, t / dur);
                yield return null;
            }
            if (cam) cam.backgroundColor = baseBg;
        }
    }
}
