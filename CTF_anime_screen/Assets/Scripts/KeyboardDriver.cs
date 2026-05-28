using UnityEngine;
using UnityEngine.InputSystem;

namespace CtfStage
{
    /// <summary>
    /// Keyboard-driven battle mode — no backend needed.
    /// 
    /// Player 1 (Team A): keys 1-6 fire attacks (each key = different category).
    /// Player 2 (Team B): keys A, S, D, F, G fire attacks.
    /// N = restart / new game.
    ///
    /// Injects SolveData / MatchStartData / MatchEndData directly into StageClient
    /// events, so StageDirector, StageScreens, StageAudio all work unchanged.
    /// </summary>
    public class KeyboardDriver : MonoBehaviour
    {
        StageClient client;
        int scoreA;
        int scoreB;
        int solveCount;
        bool matchActive;

        // 1-6 → Team A categories
        static readonly Key[] teamAKeys = {
            Key.Digit1, Key.Digit2, Key.Digit3,
            Key.Digit4, Key.Digit5, Key.Digit6
        };

        // A S D F G → Team B categories
        static readonly Key[] teamBKeys = {
            Key.A, Key.S, Key.D, Key.F, Key.G
        };

        // Map index → category
        static readonly string[] categories = {
            "pwn", "web", "crypto", "reverse", "forensics", "wifi"
        };

        void Start()
        {
            client = GetComponent<StageClient>();
            StartMatch();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[Key.N].wasPressedThisFrame)
            {
                RestartGame();
                return;
            }

            if (!matchActive) return;

            // Team A attacks (1-6)
            for (int i = 0; i < teamAKeys.Length; i++)
            {
                if (kb[teamAKeys[i]].wasPressedThisFrame)
                {
                    FireAttack("A", categories[i % categories.Length]);
                    return;
                }
            }

            // Team B attacks (A S D F G)
            for (int i = 0; i < teamBKeys.Length; i++)
            {
                if (kb[teamBKeys[i]].wasPressedThisFrame)
                {
                    FireAttack("B", categories[i % categories.Length]);
                    return;
                }
            }
        }

        void StartMatch()
        {
            scoreA = 0;
            scoreB = 0;
            solveCount = 0;
            matchActive = true;

            var data = new MatchStartData
            {
                round = "Round 1",
                teamA = new TeamRef { name = "Team A" },
                teamB = new TeamRef { name = "Team B" },
                durationSec = 600,
                bannedCategories = new string[0]
            };

            client.InjectMatchStart(data);
            Debug.Log("[KeyboardDriver] Match started! 1-6 = Team A attack, ASDFG = Team B attack, N = restart");
        }

        void FireAttack(string team, string category)
        {
            solveCount++;
            int pts = 100;

            if (team == "A") scoreA += pts;
            else scoreB += pts;

            var data = new SolveData
            {
                team = team,
                category = category,
                challenge = $"{category}_challenge_{solveCount}",
                points = pts,
                scoreA = scoreA,
                scoreB = scoreB,
                solveCount = solveCount
            };

            client.InjectSolve(data);
            Debug.Log($"[KeyboardDriver] {team} attacks with {category}! Score: {scoreA}-{scoreB}");
        }

        void RestartGame()
        {
            if (matchActive)
            {
                // End current match
                string winner = scoreA > scoreB ? "A" : scoreB > scoreA ? "B" : "draw";
                var endData = new MatchEndData
                {
                    winner = winner,
                    scoreA = scoreA,
                    scoreB = scoreB,
                    reason = "restart"
                };
                client.InjectMatchEnd(endData);
            }

            // Small delay then start new match
            Invoke(nameof(StartMatch), 1.5f);
            Debug.Log("[KeyboardDriver] Restarting game...");
        }
    }
}
