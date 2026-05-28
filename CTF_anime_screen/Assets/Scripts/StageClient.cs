using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using NativeWebSocket;

namespace CtfStage
{
    /// <summary>
    /// WebSocket receiver for the CTF stage backend (protocol v1 — see protocol.md).
    ///
    /// SETUP:
    ///   1. Create an empty GameObject in the scene, name it "StageClient".
    ///   2. Add this component. Set URL (default ws://localhost:8080).
    ///   3. From your visual components, subscribe to the events below.
    ///      (Add StageDemo.cs to the same object first to confirm data flows.)
    ///
    /// This consumes the EXACT same backend as the web prototype — start
    /// backend/mock_server.py, press Play, and events arrive. Auto-reconnects.
    /// </summary>
    public class StageClient : MonoBehaviour
    {
        [Header("Connection")]
        public string url = "ws://localhost:8080";
        public float reconnectDelaySeconds = 2f;
        public bool verboseLog = true;

        // Subscribe to these from your VFX / HUD components.
        public event Action<MatchState>     OnState;
        public event Action<MatchStartData> OnMatchStart;
        public event Action<SolveData>      OnSolve;
        public event Action<WrongData>      OnWrong;
        public event Action<BanData>        OnBan;
        public event Action<TimerData>      OnTimer;
        public event Action<MatchEndData>   OnMatchEnd;
        public event Action<AnnounceData>   OnAnnounce;
        public event Action<bool>           OnConnectionChanged; // true = connected

        public bool IsConnected { get; private set; }

        WebSocket ws;
        bool quitting;
        bool reconnectScheduled;

        async void Start() => await Connect();

        async Task Connect()
        {
            ws = new WebSocket(url);

            ws.OnOpen += () =>
            {
                IsConnected = true;
                if (verboseLog) Debug.Log($"[StageClient] connected → {url}");
                OnConnectionChanged?.Invoke(true);
            };
            ws.OnError += (e) => Debug.LogWarning($"[StageClient] error: {e}");
            ws.OnClose += (e) =>
            {
                IsConnected = false;
                OnConnectionChanged?.Invoke(false);
                if (!quitting) ScheduleReconnect();
            };
            ws.OnMessage += (bytes) => Handle(Encoding.UTF8.GetString(bytes));

            try
            {
                await ws.Connect();   // blocks until the socket closes
            }
            catch (Exception ex)
            {
                if (verboseLog) Debug.LogWarning($"[StageClient] connect failed: {ex.Message}");
                if (!quitting) ScheduleReconnect();
            }
        }

        void ScheduleReconnect()
        {
            if (reconnectScheduled || quitting) return;
            reconnectScheduled = true;
            if (verboseLog) Debug.Log($"[StageClient] reconnecting in {reconnectDelaySeconds}s…");
            Invoke(nameof(Reconnect), reconnectDelaySeconds);
        }

        async void Reconnect()
        {
            reconnectScheduled = false;
            if (!quitting) await Connect();
        }

        void Update()
        {
            // The .NET socket queues callbacks; pump them on the main thread.
            // (WebGL delivers directly from JS, so this isn't needed there.)
#if !UNITY_WEBGL || UNITY_EDITOR
            ws?.DispatchMessageQueue();
#endif
        }

        void Handle(string json)
        {
            Envelope env;
            try { env = JsonUtility.FromJson<Envelope>(json); }
            catch (Exception ex) { Debug.LogWarning($"[StageClient] bad json: {ex.Message}"); return; }
            if (env == null || string.IsNullOrEmpty(env.type)) return;
            if (verboseLog) Debug.Log($"[StageClient] ← {env.type}");

            switch (env.type)
            {
                case "STATE":       OnState?.Invoke(JsonUtility.FromJson<StateMsg>(json).data); break;
                case "MATCH_START": OnMatchStart?.Invoke(JsonUtility.FromJson<MatchStartMsg>(json).data); break;
                case "SOLVE":       OnSolve?.Invoke(JsonUtility.FromJson<SolveMsg>(json).data); break;
                case "WRONG":       OnWrong?.Invoke(JsonUtility.FromJson<WrongMsg>(json).data); break;
                case "BAN":         OnBan?.Invoke(JsonUtility.FromJson<BanMsg>(json).data); break;
                case "TIMER":       OnTimer?.Invoke(JsonUtility.FromJson<TimerMsg>(json).data); break;
                case "MATCH_END":   OnMatchEnd?.Invoke(JsonUtility.FromJson<MatchEndMsg>(json).data); break;
                case "ANNOUNCE":    OnAnnounce?.Invoke(JsonUtility.FromJson<AnnounceMsg>(json).data); break;
                default:            if (verboseLog) Debug.Log($"[StageClient] unhandled type {env.type}"); break;
            }
        }

        // --- Inject events directly (used by KeyboardDriver, no WebSocket needed) ---
        public void InjectSolve(SolveData d)      { if (verboseLog) Debug.Log("[StageClient] inject SOLVE"); OnSolve?.Invoke(d); }
        public void InjectMatchStart(MatchStartData d) { if (verboseLog) Debug.Log("[StageClient] inject MATCH_START"); OnMatchStart?.Invoke(d); }
        public void InjectMatchEnd(MatchEndData d) { if (verboseLog) Debug.Log("[StageClient] inject MATCH_END"); OnMatchEnd?.Invoke(d); }
        public void InjectWrong(WrongData d)       { if (verboseLog) Debug.Log("[StageClient] inject WRONG"); OnWrong?.Invoke(d); }

        async void OnApplicationQuit()
        {
            quitting = true;
            CancelInvoke();
            await CloseSocket();
        }

        async void OnDestroy()
        {
            quitting = true;
            CancelInvoke();
            await CloseSocket();
        }

        async Task CloseSocket()
        {
            var w = ws;
            ws = null;
            if (w != null)
            {
                try { await w.Close(); } catch { /* already closed */ }
            }
        }
    }
}
