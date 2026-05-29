using System;

namespace CtfStage
{
    // C# mirror of ../../../protocol.md (v1).
    //
    // JsonUtility-friendly by design: flat [Serializable] classes, string[] arrays
    // instead of dictionaries. The polymorphic envelope "data" is handled by
    // parsing each message twice in StageClient — once as Envelope (to read
    // `type`), then as the matching *Msg wrapper (to pull `data`).

    [Serializable]
    public class Envelope { public int v; public string type; public double ts; }

    // ---- shared sub-objects ------------------------------------------------- //
    [Serializable]
    public class TeamState { public string name; public int score; public string[] solves; }

    [Serializable]
    public class TimerState { public int remainingSec; public bool running; }

    [Serializable]
    public class TeamRef { public string name; }

    // ---- STATE -------------------------------------------------------------- //
    [Serializable]
    public class MatchState
    {
        public string round;
        public string phase;            // idle | live | ended
        public TimerState timer;
        public TeamState teamA;
        public TeamState teamB;
        public string[] bannedCategories;
    }
    [Serializable] public class StateMsg { public MatchState data; }

    // ---- MATCH_START -------------------------------------------------------- //
    [Serializable]
    public class MatchStartData
    {
        public string round;
        public TeamRef teamA;
        public TeamRef teamB;
        public TeamRef teamC;   // null in 2v2 mode
        public TeamRef teamD;   // null in 2v2 mode
        public int durationSec;
        public string[] bannedCategories;
    }
    [Serializable] public class MatchStartMsg { public MatchStartData data; }

    // ---- SOLVE -------------------------------------------------------------- //
    [Serializable]
    public class SolveData
    {
        public string team;             // "A" | "B" | "C" | "D"
        public string category;         // canonical key (see StageConfig)
        public string challenge;
        public int points;
        public int scoreA;
        public int scoreB;
        public int scoreC;
        public int scoreD;
        public int solveCount;
    }
    [Serializable] public class SolveMsg { public SolveData data; }

    // ---- WRONG -------------------------------------------------------------- //
    [Serializable]
    public class WrongData { public string team; public string category; public string challenge; }
    [Serializable] public class WrongMsg { public WrongData data; }

    // ---- BAN ---------------------------------------------------------------- //
    [Serializable]
    public class BanData { public string team; public string category; }
    [Serializable] public class BanMsg { public BanData data; }

    // ---- TIMER -------------------------------------------------------------- //
    [Serializable]
    public class TimerData { public int remainingSec; public bool running; }
    [Serializable] public class TimerMsg { public TimerData data; }

    // ---- MATCH_END ---------------------------------------------------------- //
    [Serializable]
    public class MatchEndData
    {
        public string winner;   // "A" | "B" | "C" | "D" | "DRAW"
        public int scoreA;
        public int scoreB;
        public int scoreC;
        public int scoreD;
        public string reason;
    }
    [Serializable] public class MatchEndMsg { public MatchEndData data; }

    // ---- ANNOUNCE ----------------------------------------------------------- //
    [Serializable]
    public class AnnounceData { public string text; public string level; }
    [Serializable] public class AnnounceMsg { public AnnounceData data; }
}
