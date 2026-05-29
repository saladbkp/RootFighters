using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CtfStage
{
    /// <summary>
    /// Keyboard-driven battle mode with a pre-match setup screen.
    ///
    /// Flow:
    ///   1. Setup screen: type 4 team names → press Enter
    ///   2. Category card reveal: 3 random categories shade-in one by one (roll.mp3)
    ///   3. Match starts (only the revealed categories are "live")
    ///   4. During match: keyboard fires attacks
    ///   5. N = end match → back to setup
    ///
    /// Team A: 1-9 keys  Team B: ASDFG  Team C: ZXCV  Team D: 7890
    /// </summary>
    public class KeyboardDriver : MonoBehaviour
    {
        StageClient client;
        int scoreA, scoreB, scoreC, scoreD;
        int solvesA, solvesB, solvesC, solvesD; // solve counts for win check
        int solveCount;
        int winThreshold = 2; // solves needed to win
        bool matchActive;
        bool inSetup = true;
        bool inReveal;
        bool inBanPhase;

        // Setup UI
        Canvas setupCanvas;
        InputField[] nameInputs = new InputField[4];
        InputField timerInput;
        InputField winInput;
        Text promptText;
        CanvasGroup setupGroup;

        // Ban phase UI
        CanvasGroup banGroup;
        Text banTitle, banInstructions;
        Image[] banCardBgs;
        Text[] banCardTexts;
        int banTeamIndex; // which team is banning (0=A,1=B,2=C,3=D)
        int banStep;      // 0 = team A banning, 1 = team B, etc.
        int banTotalSteps; // 4 for FFA, 2 for 2v2
        List<string> bannedCategories = new List<string>();
        bool banEnabled;
        Text banToggleText;

        // Card reveal UI
        CanvasGroup revealGroup;
        Image[] cardBgs;
        Text[] cardTexts;
        Image[] cardIcons;
        Transform revealCardParent; // parent for dynamically created cards
        Text revealTitle;
        string[] pickedCategories;

        // Card art sprites (loaded from Assets/cards/)
        Sprite sprBackCard, sprRedCard, sprBlueCard, sprPurpleCard;

        // Audio
        AudioSource rollSrc;
        AudioClip rollClip;

        static readonly Key[] teamAKeys = {
            Key.Digit1, Key.Digit2, Key.Digit3,
            Key.Digit4, Key.Digit5, Key.Digit6,
            Key.Q, Key.W, Key.E
        };

        static readonly Key[] teamBKeys = {
            Key.A, Key.S, Key.D, Key.F, Key.G
        };

        static readonly Key[] teamCKeys = {
            Key.Z, Key.X, Key.C, Key.V
        };

        static readonly Key[] teamDKeys = {
            Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0
        };

        static readonly string[] allCategories = {
            "pwn", "web", "crypto", "reverse", "forensics", "wifi",
            "iot", "osint", "b2r"
        };

        Font font;
        Sprite white;

        void Start()
        {
            client = GetComponent<StageClient>();
            font = LoadFont();

            // Audio
            rollSrc = gameObject.AddComponent<AudioSource>();
            rollSrc.playOnAwake = false;
            banSrc = gameObject.AddComponent<AudioSource>();
            banSrc.playOnAwake = false;
#if UNITY_EDITOR
            rollClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/roll.wav");
            banClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Music/ban.wav");
            sprBackCard = LoadSprite("Assets/cards/back_card.png");
            sprRedCard = LoadSprite("Assets/cards/red_card.png");
            sprBlueCard = LoadSprite("Assets/cards/blue_card.png");
            sprPurpleCard = LoadSprite("Assets/cards/purple_card.png");
#endif

            BuildSetupUI();
            BuildRevealUI();
            ShowSetup();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (inReveal) return;

            // Ban phase: number keys 1-9 to select category to ban
            // Must check BEFORE inSetup — ban phase starts from setup
            if (inBanPhase)
            {
                for (int k = 0; k < 9; k++)
                {
                    Key key = k < 9 ? (Key)((int)Key.Digit1 + k) : Key.Digit0;
                    if (k < allCategories.Length && kb[key].wasPressedThisFrame)
                    {
                        BanSelectCategory(k);
                        return;
                    }
                }
                return;
            }

            if (inSetup)
            {
                // Tab to cycle between fields
                if (kb[Key.Tab].wasPressedThisFrame)
                {
                    CycleInputField();
                    return;
                }
                // B to toggle ban phase
                if (kb[Key.B].wasPressedThisFrame && !IsAnyInputFieldFocused())
                {
                    banEnabled = !banEnabled;
                    if (banToggleText != null)
                        banToggleText.text = $"[ B ] TOGGLE BAN PHASE: {(banEnabled ? "ON" : "OFF")}";
                    return;
                }
                // Enter to confirm and start reveal
                if (kb[Key.Enter].wasPressedThisFrame || kb[Key.NumpadEnter].wasPressedThisFrame)
                {
                    StartCategoryReveal();
                    return;
                }
                return;
            }

            if (inReveal) return;

            if (kb[Key.N].wasPressedThisFrame)
            {
                RestartGame();
                return;
            }

            if (!matchActive) return;

            // Map keys to picked categories (cycle through the 3 active ones)
            for (int i = 0; i < teamAKeys.Length; i++)
                if (kb[teamAKeys[i]].wasPressedThisFrame) { FirePickedAttack("A", i); return; }

            for (int i = 0; i < teamBKeys.Length; i++)
                if (kb[teamBKeys[i]].wasPressedThisFrame) { FirePickedAttack("B", i); return; }

            for (int i = 0; i < teamCKeys.Length; i++)
                if (kb[teamCKeys[i]].wasPressedThisFrame) { FirePickedAttack("C", i); return; }

            for (int i = 0; i < teamDKeys.Length; i++)
                if (kb[teamDKeys[i]].wasPressedThisFrame) { FirePickedAttack("D", i); return; }
        }

        void FirePickedAttack(string team, int keyIndex)
        {
            if (pickedCategories == null || pickedCategories.Length == 0) return;
            string category = pickedCategories[keyIndex % pickedCategories.Length];
            TryFireAttack(team, category);
        }

        int activeFieldIndex;

        void CycleInputField()
        {
            activeFieldIndex = (activeFieldIndex + 1) % 6; // 4 names + timer + win
            if (activeFieldIndex < 4)
            {
                nameInputs[activeFieldIndex].Select();
                nameInputs[activeFieldIndex].ActivateInputField();
            }
            else if (activeFieldIndex == 4)
            {
                timerInput.Select();
                timerInput.ActivateInputField();
            }
            else
            {
                winInput.Select();
                winInput.ActivateInputField();
            }
        }

        bool IsAnyInputFieldFocused()
        {
            for (int i = 0; i < nameInputs.Length; i++)
                if (nameInputs[i] != null && nameInputs[i].isFocused) return true;
            if (timerInput != null && timerInput.isFocused) return true;
            if (winInput != null && winInput.isFocused) return true;
            return false;
        }

        // ==================== SETUP SCREEN ==================== //

        void ShowSetup()
        {
            inSetup = true;
            inBanPhase = false;
            matchActive = false;
            bannedCategories.Clear();
            setupCanvas.gameObject.SetActive(true);
            setupGroup.alpha = 1f;
            setupGroup.blocksRaycasts = true;
            setupGroup.interactable = true;
            revealGroup.alpha = 0f;
            activeFieldIndex = 0;
            StartCoroutine(FocusFirstField());
        }

        IEnumerator FocusFirstField()
        {
            yield return null;
            nameInputs[0].Select();
            nameInputs[0].ActivateInputField();
        }

        void HideSetup()
        {
            setupGroup.interactable = false;
            setupGroup.blocksRaycasts = false;
            StartCoroutine(Fade(setupGroup, 0f, 0.5f));
        }

        // ==================== CATEGORY REVEAL ==================== //

        void StartCategoryReveal()
        {
            // Parse win threshold from input
            if (winInput != null && int.TryParse(winInput.text, out int wt) && wt > 0)
                winThreshold = wt;
            else
                winThreshold = 2;

            // Reset solve counts
            solvesA = solvesB = solvesC = solvesD = 0;

            if (banEnabled)
            {
                // Ban phase first, then card reveal
                // Semi-final: force 5 challenges, win=3
                winThreshold = 3;
                HideSetup();
                StartBanPhase();
            }
            else
            {
                inReveal = true;
                PickCategoriesAndReveal();
            }
        }

        void PickCategoriesAndReveal()
        {
            // Pick random categories, excluding banned ones
            int numCards = banEnabled ? 5 : Mathf.Max(3, winThreshold + 1);
            numCards = Mathf.Min(numCards, allCategories.Length - bannedCategories.Count);

            var pool = new List<string>();
            for (int i = 0; i < allCategories.Length; i++)
                if (!bannedCategories.Contains(allCategories[i]))
                    pool.Add(allCategories[i]);

            numCards = Mathf.Min(numCards, pool.Count);
            pickedCategories = new string[numCards];
            for (int i = 0; i < numCards; i++)
            {
                int idx = Random.Range(0, pool.Count);
                pickedCategories[i] = pool[idx];
                pool.RemoveAt(idx);
            }

            // Build the correct number of card UI slots
            BuildRevealCards(numCards);

            inReveal = true;
            StartCoroutine(RevealSequence());
        }

        IEnumerator RevealSequence()
        {
            // Fade out setup, fade in reveal
            HideSetup();
            yield return new WaitForSeconds(0.6f);

            revealGroup.alpha = 1f;
            revealTitle.text = "CHALLENGES";
            revealTitle.color = new Color(0.93f, 0.94f, 1f);

            // Reset all cards to back face (hidden)
            int cardCount = pickedCategories.Length;
            for (int i = 0; i < cardCount; i++)
            {
                cardBgs[i].sprite = sprBackCard ?? White();
                cardBgs[i].color = Color.white;
                cardTexts[i].text = "";
                cardTexts[i].color = new Color(1, 1, 1, 0);
                cardIcons[i].color = new Color(0, 0, 0, 0);
                cardBgs[i].rectTransform.localScale = Vector3.one;
            }

            yield return new WaitForSeconds(0.5f);

            // Reveal each card one by one
            for (int i = 0; i < cardCount; i++)
            {
                // Play ban sound (louder than roll) for each card flip
                if (banClip != null && banSrc != null) banSrc.PlayOneShot(banClip, 1f);
                else if (rollClip != null) rollSrc.PlayOneShot(rollClip, 0.9f);

                // Slot machine style: rapid shuffle of category names on back card
                // Card spins on Y axis during shuffle, slowing down
                float shuffleDuration = 2.5f;
                float elapsed = 0f;
                float interval = 0.06f;
                float nextTick = 0f;
                var rt = cardBgs[i].rectTransform;
                float spinSpeed = 720f; // degrees per second, slows down
                float angle = 0f;
                bool showingBack = true;

                while (elapsed < shuffleDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / shuffleDuration;

                    // Spin the card (Y rotation), slowing down over time
                    float currentSpeed = Mathf.Lerp(spinSpeed, 60f, progress * progress);
                    angle += currentSpeed * Time.deltaTime;

                    // Use cos to simulate 3D Y-rotation via X scale
                    float cosAngle = Mathf.Cos(angle * Mathf.Deg2Rad);
                    rt.localScale = new Vector3(Mathf.Abs(cosAngle), 1f, 1f);

                    // Swap back/front visual based on which "side" faces camera
                    bool nowShowingBack = cosAngle > 0;
                    if (nowShowingBack != showingBack)
                    {
                        showingBack = nowShowingBack;
                        cardBgs[i].sprite = showingBack ? (sprBackCard ?? White()) : (sprBackCard ?? White());
                    }

                    // Flash category names when card faces us
                    if (elapsed >= nextTick && Mathf.Abs(cosAngle) > 0.5f)
                    {
                        string rndCat = allCategories[Random.Range(0, allCategories.Length)];
                        var rndInfo = StageConfig.Cat(rndCat);
                        cardTexts[i].text = rndInfo.label.ToUpper();
                        cardTexts[i].color = Color.Lerp(new Color(1, 1, 1, 0.3f), new Color(1, 1, 1, 0.8f), progress);

                        interval = Mathf.Lerp(0.06f, 0.35f, progress);
                        nextTick = elapsed + interval;
                    }
                    // Hide text when card is edge-on
                    if (Mathf.Abs(cosAngle) < 0.3f)
                        cardTexts[i].color = new Color(1, 1, 1, 0);

                    yield return null;
                }

                // Ensure card is face-forward before flip
                rt.localScale = Vector3.one;

                // === CARD FLIP: scale X → 0, swap to front, scale X → 1 ===
                float flipDur = 0.3f;

                // Flip to edge (shrink X)
                float ft = 0f;
                while (ft < flipDur)
                {
                    ft += Time.deltaTime;
                    float sx = Mathf.Lerp(1f, 0f, ft / flipDur);
                    rt.localScale = new Vector3(sx, 1f, 1f);
                    yield return null;
                }
                rt.localScale = new Vector3(0f, 1f, 1f);

                // Swap to front card sprite + show category name
                var info = StageConfig.Cat(pickedCategories[i]);
                cardBgs[i].sprite = PickCardFront(pickedCategories[i]);
                cardBgs[i].color = Color.white;
                cardTexts[i].text = info.label.ToUpper();
                cardTexts[i].color = Color.white;
                cardIcons[i].color = new Color(info.color.r, info.color.g, info.color.b, 0.25f);

                // Flip open (expand X back)
                ft = 0f;
                while (ft < flipDur)
                {
                    ft += Time.deltaTime;
                    float sx = Mathf.Lerp(0f, 1f, ft / flipDur);
                    rt.localScale = new Vector3(sx, 1f, 1f);
                    yield return null;
                }
                rt.localScale = Vector3.one;

                // Pop effect after flip + particle burst
                StartCoroutine(Pop(rt, 1.12f, 0.3f));
                SpawnCardVfx(rt, pickedCategories[i]);

                yield return new WaitForSeconds(1.2f);
            }

            // All revealed — show "FIGHT!" then start match
            yield return new WaitForSeconds(1.5f);

            revealTitle.text = "FIGHT!";
            revealTitle.color = new Color(1f, 0.85f, 0.35f);
            StartCoroutine(Pop(revealTitle.rectTransform, 1.3f, 0.4f));
            yield return new WaitForSeconds(4f);

            yield return Fade(revealGroup, 0f, 0.5f);
            setupCanvas.gameObject.SetActive(false);
            inReveal = false;
            inSetup = false;
            LaunchMatch();
        }

        // ==================== BAN PHASE ==================== //

        AudioClip banClip;
        AudioSource banSrc;

        void StartBanPhase()
        {
            inBanPhase = true;
            bannedCategories.Clear();
            banStep = 0;
            banTotalSteps = 2; // semi-final: 2 teams ban (A and B)

            BuildBanUI();
            banGroup.alpha = 1f;
            UpdateBanDisplay();
        }

        void UpdateBanDisplay()
        {
            string[] teamLabels = { "TEAM A", "TEAM B", "TEAM C", "TEAM D" };
            Color[] teamColors = { StageConfig.TeamAColor, StageConfig.TeamBColor, StageConfig.TeamCColor, StageConfig.TeamDColor };

            string teamName = GetSetupTeamName(banStep);
            banTitle.text = $"{teamLabels[banStep]} BAN";
            banTitle.color = teamColors[banStep];
            banInstructions.text = $"{teamName}: Press 1-{allCategories.Length} to ban a category";

            // Update card visuals — show ban count per category
            for (int i = 0; i < allCategories.Length; i++)
            {
                var info = StageConfig.Cat(allCategories[i]);
                int banCount = 0;
                foreach (var b in bannedCategories)
                    if (b == allCategories[i]) banCount++;

                if (banCount > 0)
                {
                    banCardBgs[i].color = new Color(0.15f, 0.05f, 0.05f, 0.9f);
                    string banLabel = banCount > 1 ? $"[BANNED x{banCount}]" : "[BANNED]";
                    banCardTexts[i].text = info.label.ToUpper() + "\n" + banLabel;
                    banCardTexts[i].color = new Color(0.5f, 0.2f, 0.2f);
                }
                else
                {
                    banCardBgs[i].color = new Color(info.color.r * 0.15f, info.color.g * 0.15f, info.color.b * 0.15f, 0.9f);
                    banCardTexts[i].text = $"{i + 1}. {info.label.ToUpper()}";
                    banCardTexts[i].color = info.color;
                }
            }
        }

        string GetSetupTeamName(int idx)
        {
            string[] defaults = { "Team Alpha", "Team Bravo", "Team Charlie", "Team Delta" };
            if (idx < nameInputs.Length && !string.IsNullOrWhiteSpace(nameInputs[idx].text))
                return nameInputs[idx].text.Trim();
            return defaults[idx];
        }

        void BanSelectCategory(int catIndex)
        {
            if (catIndex >= allCategories.Length) return;
            string cat = allCategories[catIndex];

            // Duplicate bans allowed (same category can be banned by both teams)
            bannedCategories.Add(cat);
            string[] teams = { "A", "B", "C", "D" };
            client.InjectBan(new BanData { team = teams[banStep], category = cat });

            // Play ban sound + VFX
            StartCoroutine(BanVfx(catIndex));

            banStep++;
            if (banStep >= banTotalSteps)
            {
                StartCoroutine(BanPhaseEnd());
            }
            else
            {
                UpdateBanDisplay();
            }
        }

        IEnumerator BanVfx(int catIndex)
        {
            // Play ban sound
            if (banClip != null && banSrc != null)
                banSrc.PlayOneShot(banClip, 1f);

            // Flash the banned card red
            if (catIndex < banCardBgs.Length)
            {
                var img = banCardBgs[catIndex];
                var origColor = img.color;

                // Red flash
                img.color = new Color(0.8f, 0.1f, 0.1f, 1f);
                yield return new WaitForSeconds(0.15f);
                img.color = new Color(0.5f, 0.05f, 0.05f, 1f);
                yield return new WaitForSeconds(0.1f);

                // Shake effect
                var rt = (RectTransform)img.transform;
                var origPos = rt.anchoredPosition;
                for (int s = 0; s < 6; s++)
                {
                    rt.anchoredPosition = origPos + new Vector2(Random.Range(-8f, 8f), Random.Range(-5f, 5f));
                    yield return new WaitForSeconds(0.03f);
                }
                rt.anchoredPosition = origPos;

                // Scale pop + particle VFX
                StartCoroutine(Pop(rt, 1.15f, 0.25f));
                SpawnCardVfx(rt, allCategories[catIndex]);

                // Settle to banned color
                img.color = new Color(0.15f, 0.05f, 0.05f, 0.9f);
            }

            // Strikethrough X overlay on the card
            if (catIndex < banCardTexts.Length)
            {
                var info = StageConfig.Cat(allCategories[catIndex]);
                int banCount = 0;
                foreach (var b in bannedCategories)
                    if (b == allCategories[catIndex]) banCount++;
                string banLabel = banCount > 1 ? $"[BANNED x{banCount}]" : "[BANNED]";
                banCardTexts[catIndex].text = info.label.ToUpper() + "\n" + banLabel;
                banCardTexts[catIndex].color = new Color(0.5f, 0.2f, 0.2f);
            }

            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator BanPhaseEnd()
        {
            // Brief pause after last ban
            yield return new WaitForSeconds(1.5f);

            banTitle.text = "BANS LOCKED";
            banTitle.color = new Color(1f, 0.4f, 0.4f);
            banInstructions.text = $"{bannedCategories.Count} categories banned";
            yield return new WaitForSeconds(2f);

            // Fade out ban UI → transition to card reveal
            yield return Fade(banGroup, 0f, 0.5f);
            if (banGroup.transform.parent != null)
                Destroy(banGroup.gameObject);
            inBanPhase = false;

            // Now do the card reveal with remaining categories
            PickCategoriesAndReveal();
        }

        void BuildBanUI()
        {
            // Reuse the setup canvas
            var go = new GameObject("BanPhase", typeof(RectTransform));
            go.transform.SetParent(setupCanvas.transform, false);
            Stretch((RectTransform)go.transform);
            banGroup = go.AddComponent<CanvasGroup>();
            banGroup.alpha = 0f;

            // Dark bg
            var bg = new GameObject("BanBg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = White();
            bgImg.color = new Color(0.03f, 0.02f, 0.06f, 0.95f);
            Stretch((RectTransform)bg.transform);

            // Title
            banTitle = MakeLabel(go.transform, "TEAM A BAN", 72,
                StageConfig.TeamAColor, TextAnchor.MiddleCenter);
            Place(banTitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 350), new Vector2(1200, 100));

            // Instructions
            banInstructions = MakeLabel(go.transform, "Press 1-9 to ban a category", 30,
                new Color(0.6f, 0.65f, 0.8f), TextAnchor.MiddleCenter);
            Place(banInstructions, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 270), new Vector2(1000, 40));

            // Category cards (3x3 grid)
            banCardBgs = new Image[allCategories.Length];
            banCardTexts = new Text[allCategories.Length];

            for (int i = 0; i < allCategories.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                float xOff = (col - 1) * 380;
                float yOff = 100 - row * 150;

                var cardGo = new GameObject($"BanCard{i}", typeof(RectTransform));
                cardGo.transform.SetParent(go.transform, false);
                var cardImg = cardGo.AddComponent<Image>();
                cardImg.sprite = White();
                var info = StageConfig.Cat(allCategories[i]);
                cardImg.color = new Color(info.color.r * 0.15f, info.color.g * 0.15f, info.color.b * 0.15f, 0.9f);
                var cardRT = (RectTransform)cardGo.transform;
                cardRT.anchorMin = new Vector2(0.5f, 0.5f);
                cardRT.anchorMax = new Vector2(0.5f, 0.5f);
                cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = new Vector2(xOff, yOff);
                cardRT.sizeDelta = new Vector2(340, 120);
                banCardBgs[i] = cardImg;

                var catText = MakeLabel(cardGo.transform, $"{i + 1}. {info.label.ToUpper()}", 36,
                    info.color, TextAnchor.MiddleCenter);
                Place(catText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    Vector2.zero, new Vector2(320, 100));
                banCardTexts[i] = catText;
            }
        }

        // ==================== MATCH ==================== //

        void LaunchMatch()
        {
            scoreA = scoreB = scoreC = scoreD = 0;
            solvesA = solvesB = solvesC = solvesD = 0;
            solveCount = 0;
            matchActive = true;

            string nA = string.IsNullOrWhiteSpace(nameInputs[0].text) ? "Team Alpha" : nameInputs[0].text.Trim();
            string nB = string.IsNullOrWhiteSpace(nameInputs[1].text) ? "Team Bravo" : nameInputs[1].text.Trim();
            string nC = string.IsNullOrWhiteSpace(nameInputs[2].text) ? "Team Charlie" : nameInputs[2].text.Trim();
            string nD = string.IsNullOrWhiteSpace(nameInputs[3].text) ? "Team Delta" : nameInputs[3].text.Trim();

            string catList = string.Join(", ", pickedCategories).ToUpper();

            // Parse timer (default 30 minutes)
            int timerMin = 30;
            if (!string.IsNullOrWhiteSpace(timerInput.text))
                int.TryParse(timerInput.text.Trim(), out timerMin);
            if (timerMin <= 0) timerMin = 30;

            var data = new MatchStartData
            {
                round = banEnabled ? $"SEMI-FINAL — {catList}" : $"FFA — {catList}",
                teamA = new TeamRef { name = nA },
                teamB = new TeamRef { name = nB },
                teamC = new TeamRef { name = nC },
                teamD = new TeamRef { name = nD },
                durationSec = timerMin * 60,
                bannedCategories = bannedCategories.ToArray()
            };

            client.InjectMatchStart(data);
            StartCoroutine(CountdownCoroutine(timerMin * 60));

            string banInfo = bannedCategories.Count > 0 ? $", Banned: {string.Join(",", bannedCategories)}" : "";
            Debug.Log($"[KeyboardDriver] Match started! Win:{winThreshold} solves, Categories: {catList}{banInfo}, Timer: {timerMin}min");
        }

        Coroutine countdownCo;

        IEnumerator CountdownCoroutine(int totalSec)
        {
            int remaining = totalSec;
            while (remaining >= 0 && matchActive)
            {
                client.InjectTimer(new TimerData { remainingSec = remaining, running = true });
                yield return new WaitForSeconds(1f);
                remaining--;
            }
            if (matchActive)
            {
                // Time's up — auto end
                client.InjectTimer(new TimerData { remainingSec = 0, running = false });
                RestartGame();
            }
        }

        void TryFireAttack(string team, string category)
        {
            // Only allow attacks with the revealed categories
            if (pickedCategories != null)
            {
                bool allowed = false;
                for (int i = 0; i < pickedCategories.Length; i++)
                    if (pickedCategories[i] == category) { allowed = true; break; }
                if (!allowed) return;
            }

            solveCount++;
            int pts = 100;

            switch (team)
            {
                case "A": scoreA += pts; solvesA++; break;
                case "B": scoreB += pts; solvesB++; break;
                case "C": scoreC += pts; solvesC++; break;
                case "D": scoreD += pts; solvesD++; break;
            }

            var data = new SolveData
            {
                team = team,
                category = category,
                challenge = $"{category}_challenge_{solveCount}",
                points = pts,
                scoreA = scoreA,
                scoreB = scoreB,
                scoreC = scoreC,
                scoreD = scoreD,
                solveCount = solveCount
            };

            client.InjectSolve(data);
            Debug.Log($"[KeyboardDriver] {team} attacks with {category}! A:{scoreA}({solvesA}) B:{scoreB}({solvesB}) C:{scoreC}({solvesC}) D:{scoreD}({solvesD})");

            // Check win threshold
            CheckWinCondition();
        }

        void RestartGame()
        {
            StopAllCoroutines(); // stop countdown etc.

            if (matchActive)
            {
                int max = Mathf.Max(scoreA, Mathf.Max(scoreB, Mathf.Max(scoreC, scoreD)));
                string winner = scoreA == max ? "A" : scoreB == max ? "B" : scoreC == max ? "C" : "D";
                if (scoreA == scoreB && scoreB == scoreC && scoreC == scoreD) winner = "DRAW";

                var endData = new MatchEndData
                {
                    winner = winner,
                    scoreA = scoreA,
                    scoreB = scoreB,
                    scoreC = scoreC,
                    scoreD = scoreD,
                    reason = "restart"
                };
                client.InjectMatchEnd(endData);
            }

            matchActive = false;
            Invoke(nameof(ShowSetup), 3f);
            Debug.Log("[KeyboardDriver] Game ended. Returning to setup...");
        }

        void CheckWinCondition()
        {
            if (!matchActive || winThreshold <= 0) return;

            string winner = null;
            if (solvesA >= winThreshold) winner = "A";
            else if (solvesB >= winThreshold) winner = "B";
            else if (solvesC >= winThreshold) winner = "C";
            else if (solvesD >= winThreshold) winner = "D";

            if (winner != null)
            {
                Debug.Log($"[KeyboardDriver] {winner} reached win threshold ({winThreshold} solves)!");
                matchActive = false;
                StopAllCoroutines();

                client.InjectMatchEnd(new MatchEndData
                {
                    winner = winner,
                    scoreA = scoreA,
                    scoreB = scoreB,
                    scoreC = scoreC,
                    scoreD = scoreD,
                    reason = $"First to {winThreshold} solves"
                });

                Invoke(nameof(ShowSetup), 5f);
            }
        }

        // ==================== UI CONSTRUCTION ==================== //

        void BuildSetupUI()
        {
            // Root canvas (shared by setup + reveal)
            var canvasGo = new GameObject("SetupCanvas", typeof(RectTransform));
            setupCanvas = canvasGo.AddComponent<Canvas>();
            setupCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            setupCanvas.sortingOrder = 200;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Setup content container (independent from reveal)
            var go = new GameObject("SetupContent", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            Stretch((RectTransform)go.transform);
            setupGroup = go.AddComponent<CanvasGroup>();

            // full-screen dark bg
            var bg = new GameObject("Bg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = White();
            bgImg.color = new Color(0.03f, 0.02f, 0.06f, 0.95f);
            Stretch((RectTransform)bg.transform);

            // Title
            var title = MakeLabel(go.transform, "CTF ARENA SETUP", 72,
                new Color(0.93f, 0.94f, 1f), TextAnchor.MiddleCenter);
            Place(title, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 280), new Vector2(1200, 100));

            var subtitle = MakeLabel(go.transform, "Enter team names, then press ENTER to start",
                28, new Color(0.6f, 0.65f, 0.8f), TextAnchor.MiddleCenter);
            Place(subtitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 210), new Vector2(1000, 40));

            // Team input fields (4 rows)
            string[] defaults = { "Team Alpha", "Team Bravo", "Team Charlie", "Team Delta" };
            Color[] colors = { StageConfig.TeamAColor, StageConfig.TeamBColor, StageConfig.TeamCColor, StageConfig.TeamDColor };
            string[] labels = { "TEAM A", "TEAM B", "TEAM C", "TEAM D" };

            for (int i = 0; i < 4; i++)
            {
                float yOffset = 100 - i * 90;

                // Label
                var lbl = MakeLabel(go.transform, labels[i], 32, colors[i], TextAnchor.MiddleRight);
                Place(lbl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-280, yOffset), new Vector2(200, 44));

                // Input field background
                var fieldGo = new GameObject($"Input{i}", typeof(RectTransform));
                fieldGo.transform.SetParent(go.transform, false);
                var fieldBg = fieldGo.AddComponent<Image>();
                fieldBg.sprite = White();
                fieldBg.color = new Color(0.1f, 0.08f, 0.16f, 0.9f);
                var fieldRT = (RectTransform)fieldGo.transform;
                fieldRT.anchorMin = new Vector2(0.5f, 0.5f);
                fieldRT.anchorMax = new Vector2(0.5f, 0.5f);
                fieldRT.pivot = new Vector2(0.5f, 0.5f);
                fieldRT.anchoredPosition = new Vector2(80, yOffset);
                fieldRT.sizeDelta = new Vector2(500, 56);

                // Text child for InputField
                var textGo = new GameObject("Text", typeof(RectTransform));
                textGo.transform.SetParent(fieldGo.transform, false);
                var txt = textGo.AddComponent<Text>();
                txt.font = font;
                txt.fontSize = 32;
                txt.fontStyle = FontStyle.Bold;
                txt.color = colors[i];
                txt.alignment = TextAnchor.MiddleLeft;
                txt.supportRichText = false;
                var textRT = (RectTransform)textGo.transform;
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = new Vector2(12, 4);
                textRT.offsetMax = new Vector2(-12, -4);

                // Placeholder
                var phGo = new GameObject("Placeholder", typeof(RectTransform));
                phGo.transform.SetParent(fieldGo.transform, false);
                var phTxt = phGo.AddComponent<Text>();
                phTxt.font = font;
                phTxt.fontSize = 32;
                phTxt.fontStyle = FontStyle.Italic;
                phTxt.color = new Color(colors[i].r, colors[i].g, colors[i].b, 0.35f);
                phTxt.alignment = TextAnchor.MiddleLeft;
                phTxt.text = defaults[i];
                var phRT = (RectTransform)phGo.transform;
                phRT.anchorMin = Vector2.zero;
                phRT.anchorMax = Vector2.one;
                phRT.offsetMin = new Vector2(12, 4);
                phRT.offsetMax = new Vector2(-12, -4);

                // InputField component
                var inputField = fieldGo.AddComponent<InputField>();
                inputField.textComponent = txt;
                inputField.placeholder = phTxt;
                inputField.characterLimit = 24;
                inputField.caretColor = colors[i];
                inputField.selectionColor = new Color(colors[i].r, colors[i].g, colors[i].b, 0.25f);

                nameInputs[i] = inputField;
            }

            // Timer input field
            float timerY = 100 - 4 * 80; // below team D, tighter spacing
            var timerLbl = MakeLabel(go.transform, "TIMER (MIN)", 32,
                new Color(0.8f, 0.75f, 0.9f), TextAnchor.MiddleRight);
            Place(timerLbl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-280, timerY), new Vector2(200, 44));

            var timerFieldGo = new GameObject("TimerInput", typeof(RectTransform));
            timerFieldGo.transform.SetParent(go.transform, false);
            var timerBg = timerFieldGo.AddComponent<Image>();
            timerBg.sprite = White();
            timerBg.color = new Color(0.1f, 0.08f, 0.16f, 0.9f);
            var timerRT = (RectTransform)timerFieldGo.transform;
            timerRT.anchorMin = new Vector2(0.5f, 0.5f);
            timerRT.anchorMax = new Vector2(0.5f, 0.5f);
            timerRT.pivot = new Vector2(0.5f, 0.5f);
            timerRT.anchoredPosition = new Vector2(80, timerY);
            timerRT.sizeDelta = new Vector2(500, 56);

            var timerTxtGo = new GameObject("Text", typeof(RectTransform));
            timerTxtGo.transform.SetParent(timerFieldGo.transform, false);
            var timerTxt = timerTxtGo.AddComponent<Text>();
            timerTxt.font = font;
            timerTxt.fontSize = 32;
            timerTxt.fontStyle = FontStyle.Bold;
            timerTxt.color = new Color(0.8f, 0.75f, 0.9f);
            timerTxt.alignment = TextAnchor.MiddleLeft;
            timerTxt.supportRichText = false;
            var timerTxtRT = (RectTransform)timerTxtGo.transform;
            timerTxtRT.anchorMin = Vector2.zero;
            timerTxtRT.anchorMax = Vector2.one;
            timerTxtRT.offsetMin = new Vector2(12, 4);
            timerTxtRT.offsetMax = new Vector2(-12, -4);

            var timerPhGo = new GameObject("Placeholder", typeof(RectTransform));
            timerPhGo.transform.SetParent(timerFieldGo.transform, false);
            var timerPhTxt = timerPhGo.AddComponent<Text>();
            timerPhTxt.font = font;
            timerPhTxt.fontSize = 32;
            timerPhTxt.fontStyle = FontStyle.Italic;
            timerPhTxt.color = new Color(0.8f, 0.75f, 0.9f, 0.35f);
            timerPhTxt.alignment = TextAnchor.MiddleLeft;
            timerPhTxt.text = "30";
            var timerPhRT = (RectTransform)timerPhGo.transform;
            timerPhRT.anchorMin = Vector2.zero;
            timerPhRT.anchorMax = Vector2.one;
            timerPhRT.offsetMin = new Vector2(12, 4);
            timerPhRT.offsetMax = new Vector2(-12, -4);

            timerInput = timerFieldGo.AddComponent<InputField>();
            timerInput.textComponent = timerTxt;
            timerInput.placeholder = timerPhTxt;
            timerInput.characterLimit = 3;
            timerInput.contentType = InputField.ContentType.IntegerNumber;
            timerInput.caretColor = new Color(0.8f, 0.75f, 0.9f);

            // Win threshold input field
            float winY = timerY - 80;
            var winLbl = MakeLabel(go.transform, "WIN (SOLVES)", 32,
                new Color(1f, 0.85f, 0.35f), TextAnchor.MiddleRight);
            Place(winLbl, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-280, winY), new Vector2(200, 44));

            var winFieldGo = new GameObject("WinInput", typeof(RectTransform));
            winFieldGo.transform.SetParent(go.transform, false);
            var winBg = winFieldGo.AddComponent<Image>();
            winBg.sprite = White();
            winBg.color = new Color(0.1f, 0.08f, 0.16f, 0.9f);
            var winRT = (RectTransform)winFieldGo.transform;
            winRT.anchorMin = new Vector2(0.5f, 0.5f);
            winRT.anchorMax = new Vector2(0.5f, 0.5f);
            winRT.pivot = new Vector2(0.5f, 0.5f);
            winRT.anchoredPosition = new Vector2(80, winY);
            winRT.sizeDelta = new Vector2(500, 56);

            var winTxtGo = new GameObject("Text", typeof(RectTransform));
            winTxtGo.transform.SetParent(winFieldGo.transform, false);
            var winTxt = winTxtGo.AddComponent<Text>();
            winTxt.font = font;
            winTxt.fontSize = 32;
            winTxt.fontStyle = FontStyle.Bold;
            winTxt.color = new Color(1f, 0.85f, 0.35f);
            winTxt.alignment = TextAnchor.MiddleLeft;
            winTxt.supportRichText = false;
            var winTxtRT = (RectTransform)winTxtGo.transform;
            winTxtRT.anchorMin = Vector2.zero;
            winTxtRT.anchorMax = Vector2.one;
            winTxtRT.offsetMin = new Vector2(12, 4);
            winTxtRT.offsetMax = new Vector2(-12, -4);

            var winPhGo = new GameObject("Placeholder", typeof(RectTransform));
            winPhGo.transform.SetParent(winFieldGo.transform, false);
            var winPhTxt = winPhGo.AddComponent<Text>();
            winPhTxt.font = font;
            winPhTxt.fontSize = 32;
            winPhTxt.fontStyle = FontStyle.Italic;
            winPhTxt.color = new Color(1f, 0.85f, 0.35f, 0.35f);
            winPhTxt.alignment = TextAnchor.MiddleLeft;
            winPhTxt.text = "2";
            var winPhRT = (RectTransform)winPhGo.transform;
            winPhRT.anchorMin = Vector2.zero;
            winPhRT.anchorMax = Vector2.one;
            winPhRT.offsetMin = new Vector2(12, 4);
            winPhRT.offsetMax = new Vector2(-12, -4);

            winInput = winFieldGo.AddComponent<InputField>();
            winInput.textComponent = winTxt;
            winInput.placeholder = winPhTxt;
            winInput.characterLimit = 2;
            winInput.contentType = InputField.ContentType.IntegerNumber;
            winInput.caretColor = new Color(1f, 0.85f, 0.35f);

            // Ban toggle hint
            float banY = winY - 60;
            var banHint = MakeLabel(go.transform, "[ B ] TOGGLE BAN PHASE: OFF",
                26, new Color(0.6f, 0.5f, 0.7f), TextAnchor.MiddleCenter);
            Place(banHint, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, banY), new Vector2(600, 36));
            banToggleText = banHint;

            // Prompt at bottom
            promptText = MakeLabel(go.transform, "[ ENTER ] START  |  [ TAB ] NEXT FIELD  |  [ B ] BAN TOGGLE",
                26, new Color(0.5f, 0.55f, 0.7f), TextAnchor.MiddleCenter);
            Place(promptText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, banY - 50), new Vector2(1000, 36));
        }

        void BuildRevealUI()
        {
            // Reveal is a sibling of setupGroup, NOT a child — so they fade independently
            var go = new GameObject("RevealContent", typeof(RectTransform));
            go.transform.SetParent(setupCanvas.transform, false);
            Stretch((RectTransform)go.transform);
            revealGroup = go.AddComponent<CanvasGroup>();
            revealGroup.alpha = 0f;

            // Dark bg for reveal screen
            var bg = new GameObject("RevealBg", typeof(RectTransform));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.AddComponent<Image>();
            bgImg.sprite = White();
            bgImg.color = new Color(0.03f, 0.02f, 0.06f, 0.95f);
            Stretch((RectTransform)bg.transform);

            // Title
            revealTitle = MakeLabel(go.transform, "CHALLENGES", 64,
                new Color(0.93f, 0.94f, 1f), TextAnchor.MiddleCenter);
            Place(revealTitle, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0, 240), new Vector2(1000, 90));

            // Card parent (cards created dynamically based on count)
            var cardParent = new GameObject("CardParent", typeof(RectTransform));
            cardParent.transform.SetParent(go.transform, false);
            Stretch((RectTransform)cardParent.transform);
            revealCardParent = cardParent.transform;
        }

        void BuildRevealCards(int count)
        {
            // Destroy old cards
            if (revealCardParent != null)
            {
                for (int c = revealCardParent.childCount - 1; c >= 0; c--)
                    Destroy(revealCardParent.GetChild(c).gameObject);
            }

            cardBgs = new Image[count];
            cardTexts = new Text[count];
            cardIcons = new Image[count];

            // Layout: scale card size based on count to fit screen
            float cardW, cardH, spacing;
            if (count <= 3)
            {
                cardW = 520; cardH = 780; spacing = 580;
            }
            else if (count <= 5)
            {
                cardW = 340; cardH = 510; spacing = 370;
            }
            else
            {
                cardW = 280; cardH = 420; spacing = 310;
            }

            float totalWidth = (count - 1) * spacing;
            float startX = -totalWidth / 2f;

            for (int i = 0; i < count; i++)
            {
                float xOffset = startX + i * spacing;

                var cardGo = new GameObject($"Card{i}", typeof(RectTransform));
                cardGo.transform.SetParent(revealCardParent, false);
                var cardImg = cardGo.AddComponent<Image>();
                cardImg.sprite = sprBackCard ?? White();
                cardImg.type = Image.Type.Simple;
                cardImg.preserveAspect = true;
                cardImg.color = Color.white;
                var cardRT = (RectTransform)cardGo.transform;
                cardRT.anchorMin = new Vector2(0.5f, 0.5f);
                cardRT.anchorMax = new Vector2(0.5f, 0.5f);
                cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.anchoredPosition = new Vector2(xOffset, -10);
                cardRT.sizeDelta = new Vector2(cardW, cardH);
                cardBgs[i] = cardImg;

                // Diamond icon
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(cardGo.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = White();
                iconImg.color = new Color(0, 0, 0, 0);
                var iconRT = (RectTransform)iconGo.transform;
                iconRT.anchorMin = new Vector2(0.5f, 0.5f);
                iconRT.anchorMax = new Vector2(0.5f, 0.5f);
                iconRT.pivot = new Vector2(0.5f, 0.5f);
                iconRT.anchoredPosition = new Vector2(0, 20);
                iconRT.sizeDelta = new Vector2(120, 120);
                iconRT.localEulerAngles = new Vector3(0, 0, 45);
                cardIcons[i] = iconImg;

                // Category name text
                int fontSize = count <= 3 ? 58 : count <= 5 ? 42 : 34;
                var catText = MakeLabel(cardGo.transform, "", fontSize,
                    new Color(1, 1, 1, 0), TextAnchor.MiddleCenter);
                catText.fontStyle = FontStyle.BoldAndItalic;
                var catOutline = catText.gameObject.AddComponent<Outline>();
                catOutline.effectColor = new Color(0, 0, 0, 0.85f);
                catOutline.effectDistance = new Vector2(3, -3);
                float textY = count <= 3 ? -240 : count <= 5 ? -160 : -130;
                Place(catText, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0, textY), new Vector2(300, 60));
                cardTexts[i] = catText;
            }
        }

        // ==================== HELPERS ==================== //

        Text MakeLabel(Transform parent, string s, int size, Color c, TextAnchor anchor)
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

        static Sprite LoadSprite(string path)
        {
#if UNITY_EDITOR
            var tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null)
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
#endif
            return null;
        }

        /// <summary>Pick card front sprite by category.</summary>
        Sprite PickCardFront(string category)
        {
            // Explicit mapping: warm→red, cool→blue, dark/mystic→purple
            switch (category)
            {
                case "pwn":       return sprRedCard    ?? sprPurpleCard; // red
                case "web":       return sprBlueCard   ?? sprPurpleCard; // green→blue card
                case "crypto":    return sprRedCard    ?? sprPurpleCard; // orange→red card
                case "reverse":   return sprBlueCard   ?? sprPurpleCard; // blue
                case "forensics": return sprPurpleCard ?? sprRedCard;    // purple
                case "wifi":      return sprRedCard    ?? sprPurpleCard; // yellow→red card
                case "iot":       return sprPurpleCard ?? sprRedCard;    // dark purple
                case "osint":     return sprBlueCard   ?? sprPurpleCard; // white→blue card
                case "b2r":       return sprRedCard    ?? sprPurpleCard; // gold→red card
                default:          return sprPurpleCard ?? sprRedCard;
            }
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
                float k = t / dur;
                float s = k < 0.5f
                    ? Mathf.Lerp(1f, peak, k * 2f)
                    : Mathf.Lerp(peak, 1f, (k - 0.5f) * 2f);
                rt.localScale = baseScale * s;
                yield return null;
            }
            rt.localScale = baseScale;
        }

        // ==================== CARD VFX ==================== //

        void SpawnCardVfx(RectTransform cardRT, string category)
        {
            var info = StageConfig.Cat(category);
            StartCoroutine(CardGlowBurst(cardRT, info.color));
        }

        IEnumerator CardGlowBurst(RectTransform cardRT, Color catColor)
        {
            // Create expanding glow ring behind the card
            var glowGo = new GameObject("GlowBurst", typeof(RectTransform));
            glowGo.transform.SetParent(cardRT.parent, false);
            glowGo.transform.SetSiblingIndex(cardRT.GetSiblingIndex()); // behind card
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.sprite = White();
            glowImg.color = new Color(catColor.r, catColor.g, catColor.b, 0.8f);
            var glowRT = (RectTransform)glowGo.transform;
            glowRT.anchorMin = cardRT.anchorMin;
            glowRT.anchorMax = cardRT.anchorMax;
            glowRT.pivot = cardRT.pivot;
            glowRT.anchoredPosition = cardRT.anchoredPosition;
            glowRT.sizeDelta = cardRT.sizeDelta;

            // Also create sparkle particles (small squares that fly outward)
            int sparkCount = 12;
            var sparks = new RectTransform[sparkCount];
            var sparkDirs = new Vector2[sparkCount];
            for (int s = 0; s < sparkCount; s++)
            {
                var spk = new GameObject("Spark", typeof(RectTransform));
                spk.transform.SetParent(cardRT.parent, false);
                var spkImg = spk.AddComponent<Image>();
                spkImg.sprite = White();
                float hueShift = Random.Range(-0.05f, 0.05f);
                Color.RGBToHSV(catColor, out float h, out float sv, out float v);
                spkImg.color = Color.HSVToRGB(h + hueShift, sv * 0.7f, Mathf.Min(v * 1.3f, 1f));
                var spkRT = (RectTransform)spk.transform;
                spkRT.anchorMin = new Vector2(0.5f, 0.5f);
                spkRT.anchorMax = new Vector2(0.5f, 0.5f);
                spkRT.pivot = new Vector2(0.5f, 0.5f);
                spkRT.anchoredPosition = cardRT.anchoredPosition;
                float size = Random.Range(8f, 20f);
                spkRT.sizeDelta = new Vector2(size, size);
                spkRT.localEulerAngles = new Vector3(0, 0, Random.Range(0f, 360f));
                sparks[s] = spkRT;
                float angle = (360f / sparkCount) * s + Random.Range(-15f, 15f);
                sparkDirs[s] = new Vector2(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                ) * Random.Range(300f, 600f);
            }

            // Animate: glow expands + fades, sparks fly outward
            float dur = 0.8f;
            float t = 0f;
            Vector2 baseSize = cardRT.sizeDelta;
            Vector2 basePos = cardRT.anchoredPosition;

            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;

                // Glow expands and fades
                float scale = 1f + k * 0.6f;
                glowRT.sizeDelta = baseSize * scale;
                glowImg.color = new Color(catColor.r, catColor.g, catColor.b, 0.8f * (1f - k));

                // Sparks fly outward and fade
                for (int s = 0; s < sparkCount; s++)
                {
                    if (sparks[s] == null) continue;
                    sparks[s].anchoredPosition = basePos + sparkDirs[s] * k;
                    var img = sparks[s].GetComponent<Image>();
                    if (img != null) img.color = new Color(img.color.r, img.color.g, img.color.b, 1f - k);
                    sparks[s].sizeDelta *= 0.995f; // slowly shrink
                }

                yield return null;
            }

            // Cleanup
            Destroy(glowGo);
            for (int s = 0; s < sparkCount; s++)
                if (sparks[s] != null) Destroy(sparks[s].gameObject);
        }
    }
}
