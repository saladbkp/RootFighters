using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CtfStage
{
    /// <summary>
    /// Ceremony + HUD layer, built entirely from code (uGUI). Shows:
    ///   • Standby screen (idle)
    ///   • Match intro:  TEAM A  VS  TEAM B
    ///   • Battle HUD:   names / scores / round / countdown / announce banner
    ///   • Result screen: winner + final score
    /// Driven by StageClient events. Uses the built-in legacy font so it renders
    /// with no asset import. Added automatically by StageBootstrap.
    /// </summary>
    [RequireComponent(typeof(StageClient))]
    public class StageScreens : MonoBehaviour
    {
        StageClient client;
        Font font;
        Canvas canvas;
        Sprite white;

        readonly Color teamAColor = StageConfig.TeamAColor;
        readonly Color teamBColor = StageConfig.TeamBColor;
        readonly Color teamCColor = StageConfig.TeamCColor;
        readonly Color teamDColor = StageConfig.TeamDColor;
        readonly Color ink = new Color(0.93f, 0.94f, 1f);

        // HUD
        Text aName, aScore, bName, bScore, cName, cScore, dName, dScore;
        Text aFlags, bFlags, cFlags, dFlags; // 🚩 per-solve flag counters
        Text roundText, timerText, bannerText;
        CanvasGroup bannerGroup;
        int[] solveCounts = new int[4]; // A=0, B=1, C=2, D=3
        // overlays
        CanvasGroup standbyGroup, introGroup, resultGroup;
        Text introA, introB, introC, introD, introVS, introRound;
        Text resultText, resultScore;
        // attack banner (中二 style)
        CanvasGroup attackBannerGroup;
        Text attackCategoryText, attackSubText;
        Image attackBannerBg, attackSlashLine;
        Text wingTextL, wingTextR;    // wing decoration texts
        Text attackGlowText;          // colored glow layer behind main text
        Image diamondL, diamondR;     // diamond accents on slash lines
        Coroutine attackBannerCo;
        // solve log (ranking feed)
        Text solveLogText;
        List<string> solveLogEntries = new List<string>();
        const int maxLogEntries = 8;

        /// <summary>Invoked when the attack banner finishes its exit animation.</summary>
        public event System.Action OnAttackBannerDone;

        void Awake()
        {
            client = GetComponent<StageClient>();
            font = LoadFont();
            BuildUI();
        }

        void OnEnable()
        {
            client.OnState += HandleState;
            client.OnMatchStart += HandleMatchStart;
            client.OnSolve += HandleSolve;
            client.OnTimer += HandleTimer;
            client.OnMatchEnd += HandleMatchEnd;
            client.OnAnnounce += HandleAnnounce;
            client.OnBan += HandleBan;
        }

        void OnDisable()
        {
            client.OnState -= HandleState;
            client.OnMatchStart -= HandleMatchStart;
            client.OnSolve -= HandleSolve;
            client.OnTimer -= HandleTimer;
            client.OnMatchEnd -= HandleMatchEnd;
            client.OnAnnounce -= HandleAnnounce;
            client.OnBan -= HandleBan;
        }

        void Start() => ShowStandby();

        // ================= event handlers ================= //
        void HandleState(MatchState s)
        {
            if (s.teamA != null) { aName.text = s.teamA.name; aScore.text = s.teamA.score.ToString(); }
            if (s.teamB != null) { bName.text = s.teamB.name; bScore.text = s.teamB.score.ToString(); }
            if (!string.IsNullOrEmpty(s.round)) roundText.text = s.round.ToUpper();
            if (s.timer != null) timerText.text = MMSS(s.timer.remainingSec);
            if (s.phase == "live") HideStandby();
            else if (s.phase == "idle") ShowStandby();
        }

        void HandleMatchStart(MatchStartData d)
        {
            if (d.teamA != null) aName.text = introA.text = d.teamA.name;
            if (d.teamB != null) bName.text = introB.text = d.teamB.name;
            if (d.teamC != null) cName.text = introC.text = d.teamC.name;
            if (d.teamD != null) dName.text = introD.text = d.teamD.name;
            aScore.text = bScore.text = cScore.text = dScore.text = "0";
            solveCounts = new int[4];
            aFlags.text = bFlags.text = cFlags.text = dFlags.text = "";
            roundText.text = introRound.text = (d.round ?? "").ToUpper();
            solveLogEntries.Clear();
            UpdateSolveLogDisplay();
            HideStandby();
            StopAllCoroutines();
            resultGroup.alpha = 0f;
            StartCoroutine(IntroSequence());
        }

        void HandleSolve(SolveData d)
        {
            aScore.text = d.scoreA.ToString();
            bScore.text = d.scoreB.ToString();
            cScore.text = d.scoreC.ToString();
            dScore.text = d.scoreD.ToString();

            // Add flag emoji for each solve
            int idx = TeamIndex(d.team);
            if (idx >= 0) solveCounts[idx]++;
            UpdateFlagDisplay();

            ShowAttackBanner(d.team, d.category);
            AddSolveLog(d);
        }

        void HandleTimer(TimerData d)
        {
            timerText.text = MMSS(d.remainingSec);
            timerText.color = (d.running && d.remainingSec <= 30) ? new Color(1f, 0.3f, 0.3f) : ink;
        }

        void HandleMatchEnd(MatchEndData d) => StartCoroutine(ResultSequence(d));

        void HandleAnnounce(AnnounceData d) => ShowBanner(d.text,
            d.level == "hype" ? new Color(1f, 0.85f, 0.35f) : ink);

        void HandleBan(BanData d) => ShowBanner(
            $"{TeamName(d.team)} BANNED {StageConfig.Cat(d.category).label}",
            new Color(1f, 0.82f, 0.48f));

        static int TeamIndex(string t)
        {
            switch (t) { case "A": return 0; case "B": return 1; case "C": return 2; case "D": return 3; default: return -1; }
        }

        string TeamName(string t)
        {
            switch (t) { case "A": return aName.text; case "B": return bName.text; case "C": return cName.text; case "D": return dName.text; default: return t; }
        }

        Color GetTeamColor(string t) => StageConfig.TeamColor(t);

        void UpdateFlagDisplay()
        {
            // Use simple flag marker that renders in Arial
            aFlags.text = FlagString(solveCounts[0]);
            bFlags.text = FlagString(solveCounts[1]);
            cFlags.text = FlagString(solveCounts[2]);
            dFlags.text = FlagString(solveCounts[3]);
        }

        static string FlagString(int count)
        {
            if (count == 0) return "";
            // Use * stars that always render in Arial
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Mathf.Min(count, 12); i++)
                sb.Append("* ");
            if (count > 12) sb.Append(".. ");
            sb.Append($"[{count}]");
            return sb.ToString();
        }

        // ================= sequences ================= //
        IEnumerator IntroSequence()
        {
            introGroup.alpha = 0f;
            yield return Fade(introGroup, 1f, 0.3f);
            StartCoroutine(Pop(introVS.rectTransform, 1.25f, 0.35f));
            yield return new WaitForSeconds(2.2f);
            yield return Fade(introGroup, 0f, 0.4f);
        }

        IEnumerator ResultSequence(MatchEndData d)
        {
            bool draw = d.winner == "DRAW";
            resultText.text = draw ? "DRAW"
                : $"{TeamName(d.winner)} WINS";
            resultText.color = draw ? ink : GetTeamColor(d.winner);
            resultScore.text = $"A:{d.scoreA}  B:{d.scoreB}  C:{d.scoreC}  D:{d.scoreD}";

            resultGroup.alpha = 0f;
            yield return Fade(resultGroup, 1f, 0.4f);
            StartCoroutine(Pop(resultText.rectTransform, 1.15f, 0.4f));
            yield return new WaitForSeconds(5f);
            yield return Fade(resultGroup, 0f, 0.5f);
            ShowStandby();
        }

        Coroutine bannerCo;

        void ShowBanner(string text, Color color)
        {
            bannerText.text = text;
            bannerText.color = color;
            if (bannerCo != null) StopCoroutine(bannerCo);
            bannerCo = StartCoroutine(BannerCo());
        }

        IEnumerator BannerCo()
        {
            yield return Fade(bannerGroup, 1f, 0.2f);
            yield return new WaitForSeconds(2.2f);
            yield return Fade(bannerGroup, 0f, 0.4f);
        }

        void ShowStandby() { standbyGroup.alpha = 1f; }
        void HideStandby() { standbyGroup.alpha = 0f; }

        // ================= SOLVE LOG (ranking feed) ================= //

        void AddSolveLog(SolveData d)
        {
            string teamName = TeamName(d.team);
            Color teamColor = GetTeamColor(d.team);
            string hex = ColorUtility.ToHtmlStringRGB(teamColor);
            var info = StageConfig.Cat(d.category);
            string catHex = ColorUtility.ToHtmlStringRGB(info.color);

            string entry = $"<color=#{hex}>{teamName}</color>  <color=#{catHex}>✦ {d.category.ToUpper()}</color>  +{d.points}";
            solveLogEntries.Insert(0, entry);

            if (solveLogEntries.Count > maxLogEntries)
                solveLogEntries.RemoveAt(solveLogEntries.Count - 1);

            UpdateSolveLogDisplay();
        }

        void UpdateSolveLogDisplay()
        {
            if (solveLogText == null) return;
            solveLogText.text = string.Join("\n", solveLogEntries);
        }

        // ================= ATTACK BANNER (中二 style) ================= //

        static readonly Dictionary<string, string[]> AttackNames = new Dictionary<string, string[]>
        {
            { "pwn",       new[] { "⚡ BINARY EXPLOSION ⚡",    "— Memory Corruption —" } },
            { "web",       new[] { "▓ MATRIX OVERRIDE ▓",       "— Code Injection —" } },
            { "wifi",      new[] { "⚡ THUNDER STRIKE ⚡",       "— Signal Hijack —" } },
            { "reverse",   new[] { "⚙ GEAR DECRYPTION ⚙",      "— Logic Unraveled —" } },
            { "forensics", new[] { "✦ PSYCHIC TRACE ✦",         "— Evidence Extracted —" } },
            { "crypto",    new[] { "◎ VORTEX CIPHER ◎",         "— Key Cracked —" } },
            { "iot",       new[] { "▪ SHADOW CONTROL ▪",        "— Device Pwned —" } },
            { "osint",     new[] { "☆ FLASH REVELATION ☆",      "— Target Exposed —" } },
            { "b2r",       new[] { "★ ROOT BREACH ★",           "— Total Domination —" } },
        };

        void ShowAttackBanner(string team, string category)
        {
            var info = StageConfig.Cat(category);
            string teamName = TeamName(team);
            Color teamCol = GetTeamColor(team);

            string[] names;
            if (!AttackNames.TryGetValue(category, out names))
                names = new[] { category.ToUpper() + " ATTACK!", "— Skill Activated —" };

            attackCategoryText.text = $"⟨  {names[0]}  ⟩";
            attackCategoryText.color = info.color;
            attackSubText.text = $"✦ {teamName}  {names[1]} ✦";
            attackSubText.color = teamCol;
            attackBannerBg.color = new Color(info.color.r * 0.15f, info.color.g * 0.15f, info.color.b * 0.15f, 0.85f);
            attackSlashLine.color = info.color;

            // glow text mirrors main text
            attackGlowText.text = attackCategoryText.text;
            attackGlowText.color = new Color(info.color.r, info.color.g, info.color.b, 0.45f);

            // tint wings & diamonds to category color
            Color wingCol = new Color(info.color.r, info.color.g, info.color.b, 0.7f);
            wingTextL.color = wingCol;
            wingTextR.color = wingCol;
            diamondL.color = info.color;
            diamondR.color = info.color;

            if (attackBannerCo != null) StopCoroutine(attackBannerCo);
            attackBannerCo = StartCoroutine(AttackBannerSequence());
        }

        IEnumerator AttackBannerSequence()
        {
            var catRT = attackCategoryText.rectTransform;
            var subRT = attackSubText.rectTransform;
            var slashRT = attackSlashLine.rectTransform;
            var glowRT = attackGlowText.rectTransform;
            var wlRT = wingTextL.rectTransform;
            var wrRT = wingTextR.rectTransform;

            // initial: off-screen, wings collapsed
            attackBannerGroup.alpha = 0f;
            float catY = catRT.anchoredPosition.y;
            float subY = subRT.anchoredPosition.y;
            catRT.anchoredPosition = new Vector2(1200, catY);
            subRT.anchoredPosition = new Vector2(-1200, subY);
            slashRT.localScale = new Vector3(0f, 1f, 1f);
            catRT.localScale = Vector3.one;
            subRT.localScale = Vector3.one;
            glowRT.localScale = Vector3.one * 1.08f;
            wlRT.localScale = new Vector3(0f, 0.5f, 1f);
            wrRT.localScale = new Vector3(0f, 0.5f, 1f);

            // === PHASE 1: SLASH IN (fast) ===
            yield return Fade(attackBannerGroup, 1f, 0.1f);

            float dur = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float ease = EaseOutBack(Mathf.Clamp01(t / dur));
                catRT.anchoredPosition = new Vector2(Mathf.Lerp(1200f, 0f, ease), catY);
                subRT.anchoredPosition = new Vector2(Mathf.Lerp(-1200f, 0f, ease), subY);
                slashRT.localScale = new Vector3(Mathf.Lerp(0f, 1f, ease), 1f, 1f);
                // wings spread out with overshoot
                float ws = Mathf.Lerp(0f, 1f, ease);
                wlRT.localScale = new Vector3(ws, Mathf.Lerp(0.5f, 1f, ease), 1f);
                wrRT.localScale = new Vector3(ws, Mathf.Lerp(0.5f, 1f, ease), 1f);
                yield return null;
            }

            StartCoroutine(Pop(catRT, 1.25f, 0.25f));

            // === PHASE 2: HOLD briefly, then SCALE UP + gradual FADE OUT ===
            yield return new WaitForSeconds(0.35f);

            t = 0f;
            dur = 1.2f; // slower exit so it doesn't vanish abruptly
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                // smooth ease-in fade — starts gentle, accelerates at end
                float fadeEase = Mathf.SmoothStep(0f, 1f, k);

                float s = Mathf.Lerp(1f, 1.4f, k);
                catRT.localScale = Vector3.one * s;
                subRT.localScale = Vector3.one * s;

                // glow layer scales slightly larger + fades faster
                glowRT.localScale = Vector3.one * s * 1.08f;
                attackGlowText.color = new Color(
                    attackGlowText.color.r,
                    attackGlowText.color.g,
                    attackGlowText.color.b,
                    Mathf.Lerp(0.45f, 0f, fadeEase));

                // wings scale up + fade
                float ws = Mathf.Lerp(1f, 1.5f, k);
                wlRT.localScale = Vector3.one * ws;
                wrRT.localScale = Vector3.one * ws;

                attackBannerGroup.alpha = 1f - fadeEase;
                slashRT.localScale = new Vector3(Mathf.Lerp(1f, 0f, k), 1f, 1f);
                yield return null;
            }

            attackBannerGroup.alpha = 0f;
            catRT.localScale = Vector3.one;
            subRT.localScale = Vector3.one;

            yield return new WaitForSeconds(0.6f);

            attackBannerCo = null;
            OnAttackBannerDone?.Invoke();
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // ================= UI construction ================= //
        void BuildUI()
        {
            var go = new GameObject("StageUI", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // ---- HUD (4-corner layout) ----
            roundText = Label(canvas.transform, "—", 34, new Color(0.73f, 0.66f, 1f), TextAnchor.MiddleCenter);
            Place(roundText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(900, 50));
            timerText = Label(canvas.transform, "0:00", 72, ink, TextAnchor.MiddleCenter);
            Place(timerText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(500, 96));

            // Team A — top-left corner
            aName = Label(canvas.transform, "Team Alpha", 44, teamAColor, TextAnchor.UpperLeft);
            Place(aName, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(350, -50), new Vector2(400, 54));
            aScore = Label(canvas.transform, "0", 64, ink, TextAnchor.UpperLeft);
            Place(aScore, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(350, -102), new Vector2(200, 68));
            aFlags = Label(canvas.transform, "", 30, teamAColor, TextAnchor.UpperLeft);
            Place(aFlags, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(350, -166), new Vector2(400, 36));

            // Team B — top-right corner
            bName = Label(canvas.transform, "Team Bravo", 44, teamBColor, TextAnchor.UpperRight);
            Place(bName, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-350, -50), new Vector2(400, 54));
            bScore = Label(canvas.transform, "0", 64, ink, TextAnchor.UpperRight);
            Place(bScore, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-350, -102), new Vector2(200, 68));
            bFlags = Label(canvas.transform, "", 30, teamBColor, TextAnchor.UpperRight);
            Place(bFlags, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-350, -166), new Vector2(400, 36));

            // Team C — bottom-left corner
            cName = Label(canvas.transform, "Team Charlie", 44, teamCColor, TextAnchor.LowerLeft);
            Place(cName, new Vector2(0, 0), new Vector2(0, 0), new Vector2(350, 130), new Vector2(400, 54));
            cScore = Label(canvas.transform, "0", 64, ink, TextAnchor.LowerLeft);
            Place(cScore, new Vector2(0, 0), new Vector2(0, 0), new Vector2(350, 68), new Vector2(200, 68));
            cFlags = Label(canvas.transform, "", 30, teamCColor, TextAnchor.LowerLeft);
            Place(cFlags, new Vector2(0, 0), new Vector2(0, 0), new Vector2(350, 170), new Vector2(400, 36));

            // Team D — bottom-right corner
            dName = Label(canvas.transform, "Team Delta", 44, teamDColor, TextAnchor.LowerRight);
            Place(dName, new Vector2(1f, 0), new Vector2(1f, 0), new Vector2(-350, 130), new Vector2(400, 54));
            dScore = Label(canvas.transform, "0", 64, ink, TextAnchor.LowerRight);
            Place(dScore, new Vector2(1f, 0), new Vector2(1f, 0), new Vector2(-350, 68), new Vector2(200, 68));
            dFlags = Label(canvas.transform, "", 30, teamDColor, TextAnchor.LowerRight);
            Place(dFlags, new Vector2(1f, 0), new Vector2(1f, 0), new Vector2(-350, 170), new Vector2(400, 36));

            // ---- solve log (bottom-center) ----
            var logBgGo = new GameObject("SolveLogBg", typeof(RectTransform));
            logBgGo.transform.SetParent(canvas.transform, false);
            var logBgImg = logBgGo.AddComponent<Image>();
            logBgImg.sprite = White();
            logBgImg.color = new Color(0.02f, 0.01f, 0.04f, 0.6f);
            var logBgRT = Place(logBgImg, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(480, 240));

            solveLogText = Label(logBgGo.transform, "", 22, ink, TextAnchor.UpperCenter);
            solveLogText.supportRichText = true;
            solveLogText.lineSpacing = 1.4f;
            solveLogText.horizontalOverflow = HorizontalWrapMode.Overflow;
            solveLogText.verticalOverflow = VerticalWrapMode.Truncate;
            var logTextRT = solveLogText.rectTransform;
            logTextRT.anchorMin = new Vector2(0f, 0f);
            logTextRT.anchorMax = new Vector2(1f, 1f);
            logTextRT.offsetMin = new Vector2(10f, 10f);
            logTextRT.offsetMax = new Vector2(-10f, -10f);

            // ---- announce banner ----
            var bGo = new GameObject("Banner", typeof(RectTransform));
            bGo.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)bGo.transform);
            bannerGroup = bGo.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerText = Label(bGo.transform, "", 60, ink, TextAnchor.MiddleCenter);
            Place(bannerText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1600, 130));

            // ---- standby overlay ----
            standbyGroup = Overlay("Standby", new Color(0.03f, 0.02f, 0.06f, 0.92f));
            var stTitle = Label(standbyGroup.transform, "CTF ARENA", 110, ink, TextAnchor.MiddleCenter);
            Place(stTitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1600, 160));
            var stSub = Label(standbyGroup.transform, "STANDBY", 48, new Color(0.6f, 0.7f, 1f), TextAnchor.MiddleCenter);
            Place(stSub, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -70), new Vector2(900, 70));

            // ---- intro overlay (4-team FFA) ----
            introGroup = Overlay("Intro", new Color(0.02f, 0.02f, 0.05f, 0.82f));
            introRound = Label(introGroup.transform, "", 40, new Color(0.73f, 0.66f, 1f), TextAnchor.MiddleCenter);
            Place(introRound, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(1200, 60));
            introA = Label(introGroup.transform, "Team Alpha", 64, teamAColor, TextAnchor.MiddleCenter);
            Place(introA, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, 80), new Vector2(600, 90));
            introB = Label(introGroup.transform, "Team Bravo", 64, teamBColor, TextAnchor.MiddleCenter);
            Place(introB, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, 80), new Vector2(600, 90));
            introC = Label(introGroup.transform, "Team Charlie", 64, teamCColor, TextAnchor.MiddleCenter);
            Place(introC, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380, -60), new Vector2(600, 90));
            introD = Label(introGroup.transform, "Team Delta", 64, teamDColor, TextAnchor.MiddleCenter);
            Place(introD, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(380, -60), new Vector2(600, 90));
            introVS = Label(introGroup.transform, "2v2v2v2", 100, Color.white, TextAnchor.MiddleCenter);
            Place(introVS, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(400, 200));
            introGroup.alpha = 0f;

            // ---- result overlay ----
            resultGroup = Overlay("Result", new Color(0.02f, 0.02f, 0.05f, 0.88f));
            resultText = Label(resultGroup.transform, "", 120, ink, TextAnchor.MiddleCenter);
            Place(resultText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(1700, 200));
            resultScore = Label(resultGroup.transform, "", 80, ink, TextAnchor.MiddleCenter);
            Place(resultScore, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(900, 110));
            resultGroup.alpha = 0f;

            // ---- attack banner overlay (中二 slash-in) ----
            var atkGo = new GameObject("AttackBanner", typeof(RectTransform));
            atkGo.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)atkGo.transform);
            attackBannerGroup = atkGo.AddComponent<CanvasGroup>();
            attackBannerGroup.alpha = 0f;
            attackBannerGroup.blocksRaycasts = false;

            // dark bg band at CENTER of screen
            var bgGo = new GameObject("AtkBg", typeof(RectTransform));
            bgGo.transform.SetParent(atkGo.transform, false);
            attackBannerBg = bgGo.AddComponent<Image>();
            attackBannerBg.sprite = White();
            attackBannerBg.color = new Color(0.05f, 0.02f, 0.08f, 0.78f);
            var bgRT = (RectTransform)bgGo.transform;
            bgRT.anchorMin = new Vector2(0, 0.5f);
            bgRT.anchorMax = new Vector2(1, 0.5f);
            bgRT.pivot = new Vector2(0.5f, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = new Vector2(0, 200);

            // slash line (colored line above the banner text)
            var slashGo = new GameObject("Slash", typeof(RectTransform));
            slashGo.transform.SetParent(atkGo.transform, false);
            attackSlashLine = slashGo.AddComponent<Image>();
            attackSlashLine.sprite = White();
            attackSlashLine.color = Color.white;
            var slashRT = (RectTransform)slashGo.transform;
            slashRT.anchorMin = new Vector2(0, 0.5f);
            slashRT.anchorMax = new Vector2(1, 0.5f);
            slashRT.pivot = new Vector2(0.5f, 0.5f);
            slashRT.anchoredPosition = new Vector2(0, 50);
            slashRT.sizeDelta = new Vector2(0, 4);

            // second accent line (bottom)
            var slash2Go = new GameObject("Slash2", typeof(RectTransform));
            slash2Go.transform.SetParent(atkGo.transform, false);
            var slash2Img = slash2Go.AddComponent<Image>();
            slash2Img.sprite = White();
            slash2Img.color = new Color(1, 1, 1, 0.4f);
            var slash2RT = (RectTransform)slash2Go.transform;
            slash2RT.anchorMin = new Vector2(0, 0.5f);
            slash2RT.anchorMax = new Vector2(1, 0.5f);
            slash2RT.pivot = new Vector2(0.5f, 0.5f);
            slash2RT.anchoredPosition = new Vector2(0, -50);
            slash2RT.sizeDelta = new Vector2(0, 3);

            // category name (big, centered, italic-bold for drama)
            attackCategoryText = Label(atkGo.transform, "", 80, Color.white, TextAnchor.MiddleCenter);
            attackCategoryText.fontStyle = FontStyle.BoldAndItalic;
            Place(attackCategoryText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(1600, 110));
            var catOutline = attackCategoryText.gameObject.AddComponent<Outline>();
            catOutline.effectColor = new Color(0, 0, 0, 0.9f);
            catOutline.effectDistance = new Vector2(3, -3);
            // add second outline for thicker border
            var catOutline2 = attackCategoryText.gameObject.AddComponent<Outline>();
            catOutline2.effectColor = new Color(0, 0, 0, 0.6f);
            catOutline2.effectDistance = new Vector2(-2, 2);

            // glow text layer (slightly larger, behind, blurred feel via bigger shadow)
            attackGlowText = Label(atkGo.transform, "", 80, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter);
            attackGlowText.fontStyle = FontStyle.BoldAndItalic;
            Place(attackGlowText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(1600, 110));
            attackGlowText.rectTransform.localScale = Vector3.one * 1.08f;
            // move glow behind main text
            attackGlowText.transform.SetSiblingIndex(attackCategoryText.transform.GetSiblingIndex());

            // === WING DECORATIONS (Unicode chars as Text) ===
            wingTextL = Label(atkGo.transform, "<<  >>", 64, Color.white, TextAnchor.MiddleCenter);
            wingTextL.fontStyle = FontStyle.Bold;
            Place(wingTextL, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-480, 10), new Vector2(200, 120));

            wingTextR = Label(atkGo.transform, "<<  >>", 64, Color.white, TextAnchor.MiddleCenter);
            wingTextR.fontStyle = FontStyle.Bold;
            Place(wingTextR, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(480, 10), new Vector2(200, 120));

            // === DIAMOND ACCENTS on slash lines ===
            diamondL = MakeDiamond(atkGo.transform);
            Place(diamondL, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380, 50), new Vector2(16, 16));
            diamondL.rectTransform.localEulerAngles = new Vector3(0, 0, 45);

            diamondR = MakeDiamond(atkGo.transform);
            Place(diamondR, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(380, 50), new Vector2(16, 16));
            diamondR.rectTransform.localEulerAngles = new Vector3(0, 0, 45);

            // subtitle (team name + flavor text)
            attackSubText = Label(atkGo.transform, "", 36, ink, TextAnchor.MiddleCenter);
            attackSubText.fontStyle = FontStyle.Italic;
            Place(attackSubText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -55), new Vector2(2500, 50));
        }

        /// <summary>Tiny diamond accent (rotated square).</summary>
        Image MakeDiamond(Transform parent)
        {
            var go = new GameObject("Diamond", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = White();
            img.color = Color.white;
            return img;
        }

        CanvasGroup Overlay(string name, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            Stretch((RectTransform)go.transform);
            var img = go.AddComponent<Image>();
            img.sprite = White();
            img.color = bg;
            return go.AddComponent<CanvasGroup>();
        }

        Text Label(Transform parent, string s, int size, Color c, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = c;
            t.alignment = anchor;
            t.text = s;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.7f);
            sh.effectDistance = new Vector2(2, -2);
            return t;
        }

        static RectTransform Place(Graphic g, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var rt = g.rectTransform;
            rt.anchorMin = aMin;
            rt.anchorMax = aMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Sprite White()
        {
            if (white != null) return white;
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            t.SetPixels(px);
            t.Apply();
            white = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return white;
        }

        static Font LoadFont()
        {
            var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (f == null) f = Font.CreateDynamicFontFromOSFont("Arial", 16);
            return f;
        }

        static string MMSS(int s)
        {
            s = Mathf.Max(0, s);
            return $"{s / 60}:{(s % 60):00}";
        }

        static IEnumerator Fade(CanvasGroup cg, float to, float dur)
        {
            float from = cg.alpha, t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            cg.alpha = to;
        }

        static IEnumerator Pop(RectTransform rt, float peak, float dur)
        {
            Vector3 baseScale = Vector3.one;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float e = Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI);
                rt.localScale = baseScale * (1f + (peak - 1f) * e);
                yield return null;
            }
            rt.localScale = baseScale;
        }
    }
}
