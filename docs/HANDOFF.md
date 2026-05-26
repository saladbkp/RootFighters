# CTF Stage Visualization — Handoff & Roadmap

> Read this first. It is the single source of truth for picking up this project.
> Last updated: 2026-05-25.

## TL;DR

A data-driven, anime/fighting-game **2v2 battle-arena big screen** for a small live
CTF tournament. When a team solves a challenge, their character fires a
**category-colored attack** at the opponent. Built **decoupled**: one WebSocket
JSON protocol (`protocol.md`, v1) feeds both a **web prototype** (done) and a
**Unity (URP) stage** (the real target — mostly done, needs art + bracket).

- **Backend**: Python mock/game servers now; a real CTFd bridge later (optional).
- **Frontend**: web prototype works; Unity is the production screen.
- **Status**: end-to-end pipeline works (backend → Unity → VFX/SFX/HUD/screens).
  Remaining: tournament **bracket screen** (#8), drop in **real creature models**
  (#10), and production polish.

## Architecture

```
CTFd (real, LATER) ──▶ backend ──(WebSocket, protocol v1)──▶ frontend
                       bracket/ban/timer logic                web (done) / Unity (target)
```

- The protocol is frozen and shared. Tune visuals against the web prototype, then
  the Unity stage consumes the *same* events unchanged.
- Going live = swap only the backend's data source to poll CTFd
  `/api/v1/submissions`; emit the same `SOLVE` events. Frontend never changes.
  (For a 16-team live event, **manually driving `game_server.py` is a legitimate
  production approach** — an operator types solves as they happen on stage.)

## Repo map

```
protocol.md                     ⭐ the WebSocket JSON contract (v1) — FROZEN
README.md                       run instructions
docs/HANDOFF.md                 this file

backend/
  game_server.py                ⭐ match driver: `python game_server.py [min] [challenges]`
                                   default 10 min, 3 random challenges, first to 2 wins.
                                   cmds: board | a c1 | b p1 | a x w1 | new | ban | end | name | help
  mock_server.py                event poker + scenario macros (sim / spam / clutch); starts idle
  requirements.txt              websockets
mock-api.py                     original tiny mock (superseded; kept for reference)

web/                            web prototype (vanilla JS, no build step)
  index.html  style.css
  config.js                     ⭐ category → color + effect (the look, one place)
  effects.js                    ⭐ 9 canvas particle effects — THE DESIGN SPEC for Unity VFX
  arena.js                      arena bg + 2 mascots + attack choreography
  main.js                       ws client + HUD + offline keyboard preview (keys 1-9)

CTF_anime_screen/               Unity 6 (6000.4.8f1), URP, NativeWebSocket installed
  Assets/Scripts/  (namespace CtfStage)
    StageProtocol.cs            C# data classes mirroring protocol v1 (JsonUtility, double-parse)
    StageConfig.cs              category → color + effect (mirror of config.js) + team colors
    StageClient.cs              WebSocket receiver → strongly-typed C# events, auto-reconnect
    StageDemo.cs                sample logger (optional; NOT added by bootstrap)
    StageVfx.cs                 ⭐ 9 procedural particle bursts (port of effects.js)
    StageFighter.cs             ⭐ character layer: Animator hooks + FistPosition; capsule fallback
    StageDirector.cs            battle choreography: solve→attack→projectile(from fist)→impact→hurt
    StageAudio.cs               synthesized SFX per category + wrong/hype/win + looping BGM
    StageEnvironment.cs         procedural stage: gradient sky, neon grid, platforms, divider,
                                embers, + Bloom/Vignette post-processing (the "cool" look)
    StageScreens.cs             uGUI: Standby / "TEAM A VS TEAM B" intro / Result + battle HUD + banner
    StageBootstrap.cs           ⭐ ONE component builds the whole scene; has Team A/B Model slots
```

## Protocol v1 (see protocol.md for full schema)

- Transport: `ws://localhost:8080`, UTF-8 JSON, server→client.
- Envelope: `{ "v":1, "type":"<EVENT>", "ts":<float>, "data":{...} }`
- Events: `STATE`, `MATCH_START`, `SOLVE`, `WRONG`, `BAN`, `TIMER`, `MATCH_END`, `ANNOUNCE`.
- Teams referenced by side `"A"` (left) / `"B"` (right).
- **9 categories** (key → color → effect):
  `pwn`→red/explosion · `web`→green/matrix · `wifi`→yellow/lightning ·
  `reverse`→blue/gears · `forensics`→purple/psychic · `crypto`→orange/vortex ·
  `iot`→black/shadow · `osint`→white/flash · `b2r`→**gold**/root.

## What's DONE

- ✅ Protocol v1 + two backends (`game_server`, `mock_server`) — both passed automated end-to-end tests.
- ✅ Web prototype: arena + 9 effects + HUD + offline keyboard preview (syntax-checked).
- ✅ Unity receiver (`StageClient`) + config/protocol mirrors.
- ✅ Unity VFX core (`StageVfx` + `StageDirector` + `StageBootstrap`) — user confirmed it renders.
- ✅ Unity fighting-game character layer (`StageFighter`): attack/hurt/win, VFX from the fist;
  works with capsule placeholders AND real models (Team A/B Model slots).
- ✅ Unity audio (`StageAudio`): synth SFX + BGM.
- ✅ Unity environment (`StageEnvironment`): cool anime stage + Bloom — user confirmed (screenshot).
- ✅ Unity ceremony + HUD (`StageScreens`): standby / VS intro / result / live HUD / banner.

## What's NOT done — NEXT GOALS (priority order)

1. **#8 Tournament bracket screen** (biggest remaining build)
   - 16 teams, single/double elimination, randomized brackets, "ban 1 category" in semis/finals.
   - Needs a **protocol extension**: add a `BRACKET` event carrying matchups + results
     (and probably a small bracket data model in the backend, since CTFd has no bracket).
   - Render as a uGUI tree (build from code like StageScreens). Show between matches:
     who-plays-whom, advancement, current-match highlight.

2. **#10 Drop in real cute-creature models** (art task; code seam is READY)
   - Get ORIGINAL cute-monster models (NOT real Pokémon — public/paid event = IP risk) from
     Quaternius (CC0, animated) / Sketchfab / Asset Store.
   - Import `.fbx`/`.glb` → fix Built-in→URP materials if pink → scale ~1–2 m → make prefab →
     drop into `StageBootstrap.teamAModel/teamBModel`.
   - Optional: wire an Animator Controller with `Attack`/`Hurt`/`Win` triggers + assign a hand
     bone as `StageFighter.fistPoint`. Apply Unity Toon Shader (`com.unity.toonshader`) for the
     cel/outline anime look. Static models also work (procedural fallback).

3. **Production: real CTFd bridge** (optional for a small live event)
   - Poll `GET /api/v1/submissions?type=correct` (admin token), track last id, map
     team→side & challenge.category→canonical key, emit `SOLVE`. Keep protocol frozen.
   - Bracket/ban/timer logic stays in the backend.

4. **Polish**
   - Upgrade exotic VFX (matrix glyphs / lightning bolt / gears / shockwave rings) from the
     current approximated bursts to **VFX Graph** — use `web/effects.js` as the literal spec
     (its `life` & colors copy 1:1; speed/gravity/size scale px→meters).
   - TextMeshPro fonts (StageScreens uses legacy Text for zero-import reliability).
   - Team logos/names input, combo/streak effects, match transitions.
   - **Unity WebGL build** + projector/fullscreen ops (resolution, disable screensaver).

## How to run

**Backend** (one of):
```bash
cd backend && pip install -r requirements.txt
python game_server.py 10        # match driver (recommended)
# or: python mock_server.py     # event poker + scenarios; starts in Standby
```
`game_server` cmds: `board` (list codes), `a c1` (Team A solves crypto#1), `b p1`,
`a x w1` (wrong), `new` (fresh round / replay intro), `name a Cynx`, `end a`, `help`.

**Web prototype:**
```bash
cd web && python3 -m http.server 8000     # then open http://localhost:8000
```
Offline preview: keys `1-9` (Team A), `shift+1-9` (Team B), `,`/`.` (wrong), space (simultaneous).

**Unity stage:**
1. Open `CTF_anime_screen` in Unity 6.
2. New scene (Basic URP) → empty GameObject → add **one** component `StageBootstrap`.
   (It auto-builds camera/light/environment/fighters + StageClient/Director/Audio/Screens.)
3. Run a backend, press **Play**, drive solves. To use real models, fill the
   Team A/B Model slots on StageBootstrap.

## Key decisions & constraints — READ BEFORE CHANGING

- **IP**: use ORIGINAL "Pokémon-style" creatures, never actual Pokémon assets/rips
  (this is a public, paid event).
- **AI policy**: the tournament bans AI for *players*; using AI to build this *organizer
  infrastructure* is fine.
- **User comms**: the user converses in Chinese but wants all deliverables (code, comments,
  UI, docs) in **English**.
- **Mirrors must stay in sync**: `web/config.js` ↔ `StageConfig.cs` (colors/effects) and
  `web/effects.js` ↔ `StageVfx.cs` (motion/lifetimes). Change both.
- **Assistant cannot run the Unity Editor** — all Unity work is written as code (procedural
  scene/VFX/UI, no asset authoring). The USER presses Play and verifies. Write compile-safe C#.
- **Protocol is frozen at v1.** Extend additively (e.g., a new `BRACKET` event); don't break
  existing fields.

## Gotchas / known limitations

- Web uses ES modules → must be served over http (`python3 -m http.server`), not file://.
- Unity Particles use shader `"Universal Render Pipeline/Particles/Unlit"` (verified present);
  falls back to `Sprites/Default`.
- Bloom: `StageEnvironment` creates its own high-priority Volume + enables post on the camera.
  If no glow, confirm the URP renderer asset (`Assets/Settings/PC_Renderer.asset`) has
  Post-processing on.
- `StageScreens` uses **legacy `UnityEngine.UI.Text`** (built-in font, zero import) → may log a
  yellow deprecation warning (not an error). Swap to TextMeshPro for nicer type later.
- Unity exotic VFX are **approximated** as colored bursts (see Polish #4).

## Verification status (as of handoff)

- Backends: ✅ automated e2e tests pass.
- Web prototype: ⚠️ syntax-checked only; not visually verified by assistant.
- Unity #6 (VFX core) + environment: ✅ user-confirmed (screenshot).
- Unity audio (#7): built; user testing.
- Unity screens (#9): built; **awaiting user Play verification**.
- Task list lives in the session todo (IDs 1–10); #6 & #7 completed, #9 in progress,
  #8 & #10 pending, web-side #1–5 optional.
```
