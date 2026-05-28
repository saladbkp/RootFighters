using System.Collections.Generic;
using UnityEngine;

namespace CtfStage
{
    /// <summary>
    /// Stage sound: a per-category attack SFX, wrong-flag buzz, hype stinger,
    /// win fanfare, and a looping standby BGM. All sounds are SYNTHESIZED at
    /// runtime (no audio files needed) so it works immediately. Drop real
    /// AudioClips into the override slots later to replace any of them.
    ///
    /// Mirrors the "effects.js as spec" idea: one ToneSpec table parallels the
    /// nine StageVfx effects, keyed by the same effect name.
    /// </summary>
    [RequireComponent(typeof(StageClient))]
    public class StageAudio : MonoBehaviour
    {
        [Header("Volumes")]
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        [Range(0f, 1f)] public float bgmVolume = 0.25f;
        public bool enableBgm = true;

        [Header("Optional real-clip overrides (leave empty to use synth)")]
        public AudioClip bgmOverride;

        enum Wave { Sine, Square, Saw, Triangle }

        struct ToneSpec
        {
            public float freqStart, freqEnd;  // Hz, swept over the duration
            public float duration;            // seconds
            public Wave wave;
            public float noiseMix;            // 0..1 blend toward white noise
            public float vibrato;             // Hz (0 = none)
            public float decay;               // exp amplitude decay rate
            public float gain;
        }

        const int SR = 44100;

        StageClient client;
        AudioSource sfxSrc, bgmSrc;
        readonly Dictionary<string, AudioClip> sfx = new Dictionary<string, AudioClip>();
        AudioClip wrongClip, hypeClip, winClip, bgmClip;
        readonly System.Random rng = new System.Random(1234);

        // Real sound effect clips (loaded from Assets/Music/)
        AudioClip sndFirstBlood, sndNormalKill, sndDoubleKill, sndTripleKill, sndGameStart, sndGameEnd;
        int solveCount;
        float lastSolveTime;
        const float multiKillWindow = 5f; // seconds to count as multi-kill
        int comboCount;

        // effect name -> sound character (parallels StageVfx.Specs)
        static readonly Dictionary<string, ToneSpec> Tones = new Dictionary<string, ToneSpec>
        {
            { "explosion", new ToneSpec { freqStart = 220, freqEnd = 55,  duration = 0.5f,  wave = Wave.Square,   noiseMix = 0.6f, decay = 8,  gain = 0.55f } },
            { "matrix",    new ToneSpec { freqStart = 900, freqEnd = 1300,duration = 0.22f, wave = Wave.Square,   noiseMix = 0.1f, decay = 14, gain = 0.30f } },
            { "lightning", new ToneSpec { freqStart = 3200,freqEnd = 1400,duration = 0.25f, wave = Wave.Saw,      noiseMix = 0.8f, decay = 18, gain = 0.40f } },
            { "gears",     new ToneSpec { freqStart = 170, freqEnd = 120, duration = 0.30f, wave = Wave.Saw,      noiseMix = 0.2f, decay = 10, gain = 0.38f } },
            { "psychic",   new ToneSpec { freqStart = 520, freqEnd = 940, duration = 0.6f,  wave = Wave.Sine,     vibrato = 8, decay = 4,  gain = 0.34f } },
            { "vortex",    new ToneSpec { freqStart = 300, freqEnd = 1250,duration = 0.5f,  wave = Wave.Saw,      decay = 5,  gain = 0.32f } },
            { "shadow",    new ToneSpec { freqStart = 95,  freqEnd = 48,  duration = 0.6f,  wave = Wave.Sine,     noiseMix = 0.15f, decay = 5, gain = 0.48f } },
            { "flash",     new ToneSpec { freqStart = 1600,freqEnd = 1600,duration = 0.5f,  wave = Wave.Sine,     decay = 7,  gain = 0.36f } },
            { "root",      new ToneSpec { freqStart = 400, freqEnd = 900, duration = 0.6f,  wave = Wave.Triangle, decay = 4,  gain = 0.42f } },
        };
        static readonly ToneSpec WrongTone = new ToneSpec { freqStart = 160, freqEnd = 110, duration = 0.25f, wave = Wave.Square, decay = 12, gain = 0.40f };
        static readonly ToneSpec HypeTone  = new ToneSpec { freqStart = 600, freqEnd = 1500, duration = 0.4f, wave = Wave.Saw, decay = 6, gain = 0.42f };

        void Awake()
        {
            client = GetComponent<StageClient>();

            sfxSrc = gameObject.AddComponent<AudioSource>();
            sfxSrc.playOnAwake = false;
            bgmSrc = gameObject.AddComponent<AudioSource>();
            bgmSrc.playOnAwake = false;
            bgmSrc.loop = true;

            foreach (var kv in Tones)
                sfx[kv.Key] = Synth("sfx_" + kv.Key, kv.Value);
            wrongClip = Synth("sfx_wrong", WrongTone);
            hypeClip = Synth("sfx_hype", HypeTone);
            winClip = SynthChord("sfx_win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 1.2f, 3f, 0.4f);
            bgmClip = SynthPad("bgm_pad", new[] { 130.81f, 196f, 261.63f, 329.63f }, 4f, 0.18f);

            // Load real sound effects from Assets/Music/
            LoadSoundEffects();
        }

        void LoadSoundEffects()
        {
#if UNITY_EDITOR
            sndFirstBlood = LoadAudio("Assets/Music/first_blood.mp3");
            sndNormalKill = LoadAudio("Assets/Music/normal kill.wav");
            sndDoubleKill = LoadAudio("Assets/Music/double kill.wav");
            sndTripleKill = LoadAudio("Assets/Music/thriple kill.wav");
            sndGameStart = LoadAudio("Assets/Music/gamestart.wav");
            sndGameEnd = LoadAudio("Assets/Music/game end.wav");

            Debug.Log($"[StageAudio] Loaded sounds: firstBlood={sndFirstBlood != null}, normalKill={sndNormalKill != null}, doubleKill={sndDoubleKill != null}, tripleKill={sndTripleKill != null}, gameStart={sndGameStart != null}, gameEnd={sndGameEnd != null}");
#endif
        }

        static AudioClip LoadAudio(string path)
        {
#if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
#else
            return null;
#endif
        }

        void OnEnable()
        {
            client.OnSolve += HandleSolve;
            client.OnWrong += HandleWrong;
            client.OnAnnounce += HandleAnnounce;
            client.OnMatchStart += HandleMatchStart;
            client.OnMatchEnd += HandleMatchEnd;
        }

        void OnDisable()
        {
            client.OnSolve -= HandleSolve;
            client.OnWrong -= HandleWrong;
            client.OnAnnounce -= HandleAnnounce;
            client.OnMatchStart -= HandleMatchStart;
            client.OnMatchEnd -= HandleMatchEnd;
        }

        void Start()
        {
            if (!enableBgm) return;
            bgmSrc.clip = bgmOverride != null ? bgmOverride : bgmClip;
            bgmSrc.volume = bgmVolume;
            bgmSrc.Play();
        }

        // --- event handlers ------------------------------------------------- //
        void HandleSolve(SolveData d)
        {
            solveCount++;

            // Multi-kill detection: solves within 5s window
            if (Time.time - lastSolveTime < multiKillWindow)
                comboCount++;
            else
                comboCount = 1;
            lastSolveTime = Time.time;

            // Play appropriate sound
            if (solveCount == 1 && sndFirstBlood != null)
            {
                sfxSrc.PlayOneShot(sndFirstBlood, sfxVolume);
            }
            else if (comboCount >= 3 && sndTripleKill != null)
            {
                sfxSrc.PlayOneShot(sndTripleKill, sfxVolume);
            }
            else if (comboCount == 2 && sndDoubleKill != null)
            {
                sfxSrc.PlayOneShot(sndDoubleKill, sfxVolume);
            }
            else if (sndNormalKill != null)
            {
                sfxSrc.PlayOneShot(sndNormalKill, sfxVolume);
            }
            else
            {
                // Fallback to synth SFX
                string fx = StageConfig.Cat(d.category).effect;
                if (sfx.TryGetValue(fx, out var clip)) sfxSrc.PlayOneShot(clip, sfxVolume);
            }
        }

        void HandleWrong(WrongData d) => sfxSrc.PlayOneShot(wrongClip, sfxVolume);

        void HandleAnnounce(AnnounceData d)
        {
            if (d.level == "hype") sfxSrc.PlayOneShot(hypeClip, sfxVolume);
        }

        void HandleMatchStart(MatchStartData d)
        {
            solveCount = 0;
            comboCount = 0;
            lastSolveTime = 0f;
            if (sndGameStart != null)
                sfxSrc.PlayOneShot(sndGameStart, sfxVolume);
        }

        void HandleMatchEnd(MatchEndData d)
        {
            if (sndGameEnd != null)
                sfxSrc.PlayOneShot(sndGameEnd, sfxVolume);
            else
                sfxSrc.PlayOneShot(winClip, sfxVolume);
        }

        // --- synthesis ------------------------------------------------------ //
        float WaveSample(Wave w, float phase01)
        {
            switch (w)
            {
                case Wave.Square:   return phase01 < 0.5f ? 1f : -1f;
                case Wave.Saw:      return phase01 * 2f - 1f;
                case Wave.Triangle: return 1f - 4f * Mathf.Abs(phase01 - 0.5f);
                default:            return Mathf.Sin(phase01 * 2f * Mathf.PI);
            }
        }

        AudioClip Synth(string name, ToneSpec s)
        {
            int n = Mathf.Max(1, (int)(s.duration * SR));
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float u = t / s.duration;
                float freq = Mathf.Lerp(s.freqStart, s.freqEnd, u);
                if (s.vibrato > 0f) freq += Mathf.Sin(t * 2f * Mathf.PI * s.vibrato) * freq * 0.03f;
                phase += freq / SR;
                float ph = phase - Mathf.Floor(phase);
                float w = WaveSample(s.wave, ph);
                if (s.noiseMix > 0f) w = Mathf.Lerp(w, (float)(rng.NextDouble() * 2.0 - 1.0), s.noiseMix);
                float attack = Mathf.Min(1f, t * 250f);              // ~4ms attack
                float env = Mathf.Exp(-s.decay * t) * attack;
                data[i] = w * env * s.gain;
            }
            return ClipFrom(name, data);
        }

        AudioClip SynthChord(string name, float[] freqs, float dur, float decay, float gain)
        {
            int n = Mathf.Max(1, (int)(dur * SR));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float sum = 0f;
                foreach (var f in freqs) sum += Mathf.Sin(t * 2f * Mathf.PI * f);
                float env = Mathf.Exp(-decay * t) * Mathf.Min(1f, t * 200f);
                data[i] = (sum / freqs.Length) * env * gain;
            }
            return ClipFrom(name, data);
        }

        // Sustained, loopable pad (no hard decay; slow amplitude LFO).
        AudioClip SynthPad(string name, float[] freqs, float dur, float gain)
        {
            int n = Mathf.Max(1, (int)(dur * SR));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float sum = 0f;
                foreach (var f in freqs) sum += Mathf.Sin(t * 2f * Mathf.PI * f);
                float lfo = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 0.25f);  // gentle swell
                float edge = Mathf.Min(1f, Mathf.Min(t, dur - t) * 6f);            // soft loop edges
                data[i] = (sum / freqs.Length) * gain * lfo * edge;
            }
            return ClipFrom(name, data);
        }

        AudioClip ClipFrom(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SR, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
