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
        readonly Color ink = new Color(0.93f, 0.94f, 1f);

        // HUD
        Text aName, aScore, bName, bScore, roundText, timerText, bannerText;
        CanvasGroup bannerGroup;
        // overlays
        CanvasGroup standbyGroup, introGroup, resultGroup;
        Text introA, introB, introVS, introRound;
        Text resultText, resultScore;
        // attack banner (中二 style)
        CanvasGroup attackBannerGroup;
        Text attackCategoryText, attackSubText;
        Image attackBannerBg, attackSlashLine;
        Coroutine attackBannerCo;

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
            aScore.text = bScore.text = "0";
            roundText.text = introRound.text = (d.round ?? "").ToUpper();
            HideStandby();
            StopAllCoroutines();
            resultGroup.alpha = 0f;
            StartCoroutine(IntroSequence());
        }

        void HandleSolve(SolveData d)
        {
            aScore.text = d.scoreA.ToString();
            bScore.text = d.scoreB.ToString();
            ShowAttackBanner(d.team, d.category);
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
            $"{(d.team == "A" ? aName.text : bName.text)} BANNED {StageConfig.Cat(d.category).label}",
            new Color(1f, 0.82f, 0.48f));

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
                : $"{(d.winner == "A" ? aName.text : bName.text)} WINS";
            resultText.color = d.winner == "A" ? teamAColor : d.winner == "B" ? teamBColor : ink;
            resultScore.text = $"{d.scoreA} : {d.scoreB}";

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
            string teamName = team == "A" ? aName.text : bName.text;
            Color teamCol = team == "A" ? teamAColor : teamBColor;

            string[] names;
            if (!AttackNames.TryGetValue(category, out names))
                names = new[] { category.ToUpper() + " ATTACK!", "— Skill Activated —" };

            attackCategoryText.text = names[0];
            attackCategoryText.color = info.color;
            attackSubText.text = $"{teamName}  {names[1]}";
            attackSubText.color = teamCol;
            attackBannerBg.color = new Color(info.color.r * 0.15f, info.color.g * 0.15f, info.color.b * 0.15f, 0.85f);
            attackSlashLine.color = info.color;

            if (attackBannerCo != null) StopCoroutine(attackBannerCo);
            attackBannerCo = StartCoroutine(AttackBannerSequence());
        }

        IEnumerator AttackBannerSequence()
        {
            var catRT = attackCategoryText.rectTransform;
            var subRT = attackSubText.rectTransform;
            var slashRT = attackSlashLine.rectTransform;

            // initial: off-screen
            attackBannerGroup.alpha = 0f;
            float catY = catRT.anchoredPosition.y;
            float subY = subRT.anchoredPosition.y;
            catRT.anchoredPosition = new Vector2(1200, catY);
            subRT.anchoredPosition = new Vector2(-1200, subY);
            slashRT.localScale = new Vector3(0f, 1f, 1f);

            // === PHASE 1: SLASH IN (1.5s) ===
            yield return Fade(attackBannerGroup, 1f, 0.1f);

            // slide text in from sides (0.4s with overshoot)
            float dur = 0.4f;
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float ease = EaseOutBack(Mathf.Clamp01(t / dur));
                catRT.anchoredPosition = new Vector2(Mathf.Lerp(1200f, 0f, ease), catY);
                subRT.anchoredPosition = new Vector2(Mathf.Lerp(-1200f, 0f, ease), subY);
                slashRT.localScale = new Vector3(Mathf.Lerp(0f, 1f, ease), 1f, 1f);
                yield return null;
            }

            StartCoroutine(Pop(catRT, 1.25f, 0.25f));

            // hold visible (1.5 - 0.1 - 0.4 = 1.0s)
            yield return new WaitForSeconds(1.0f);

            // === PHASE 2: SLASH OUT (1.5s) ===
            t = 0f;
            dur = 0.5f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float ease = k * k;
                catRT.anchoredPosition = new Vector2(Mathf.Lerp(0f, -1400f, ease), catY);
                subRT.anchoredPosition = new Vector2(Mathf.Lerp(0f, 1400f, ease), subY);
                slashRT.localScale = new Vector3(Mathf.Lerp(1f, 0f, k), 1f, 1f);
                yield return null;
            }

            yield return Fade(attackBannerGroup, 0f, 0.15f);

            // brief dramatic pause before VFX
            yield return new WaitForSeconds(0.85f);

            attackBannerCo = null;

            // banner done → NOW fire VFX
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

            // ---- HUD ----
            roundText = Label(canvas.transform, "—", 34, new Color(0.73f, 0.66f, 1f), TextAnchor.MiddleCenter);
            Place(roundText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -44), new Vector2(900, 50));
            timerText = Label(canvas.transform, "0:00", 72, ink, TextAnchor.MiddleCenter);
            Place(timerText, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -104), new Vector2(500, 96));

            aName = Label(canvas.transform, "Team Alpha", 44, teamAColor, TextAnchor.MiddleLeft);
            Place(aName, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(240, -52), new Vector2(520, 60));
            aScore = Label(canvas.transform, "0", 84, ink, TextAnchor.MiddleLeft);
            Place(aScore, new Vector2(0, 1f), new Vector2(0, 1f), new Vector2(240, -120), new Vector2(420, 96));

            bName = Label(canvas.transform, "Team Bravo", 44, teamBColor, TextAnchor.MiddleRight);
            Place(bName, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-240, -52), new Vector2(520, 60));
            bScore = Label(canvas.transform, "0", 84, ink, TextAnchor.MiddleRight);
            Place(bScore, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-240, -120), new Vector2(420, 96));

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

            // ---- intro overlay (VS) ----
            introGroup = Overlay("Intro", new Color(0.02f, 0.02f, 0.05f, 0.82f));
            introRound = Label(introGroup.transform, "", 40, new Color(0.73f, 0.66f, 1f), TextAnchor.MiddleCenter);
            Place(introRound, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(1200, 60));
            introA = Label(introGroup.transform, "Team Alpha", 84, teamAColor, TextAnchor.MiddleCenter);
            Place(introA, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-460, 20), new Vector2(820, 120));
            introVS = Label(introGroup.transform, "VS", 180, Color.white, TextAnchor.MiddleCenter);
            Place(introVS, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(400, 240));
            introB = Label(introGroup.transform, "Team Bravo", 84, teamBColor, TextAnchor.MiddleCenter);
            Place(introB, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(460, 20), new Vector2(820, 120));
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

            // category name (big, centered)
            attackCategoryText = Label(atkGo.transform, "", 80, Color.white, TextAnchor.MiddleCenter);
            Place(attackCategoryText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 10), new Vector2(1600, 110));
            var catOutline = attackCategoryText.gameObject.AddComponent<Outline>();
            catOutline.effectColor = new Color(0, 0, 0, 0.9f);
            catOutline.effectDistance = new Vector2(3, -3);

            // subtitle (team name + flavor text)
            attackSubText = Label(atkGo.transform, "", 36, ink, TextAnchor.MiddleCenter);
            Place(attackSubText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, -55), new Vector2(1400, 50));
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
