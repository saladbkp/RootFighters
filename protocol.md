# CTF Stage Visualization — WebSocket Protocol (v1)

The single shared contract between the **backend** (mock now, real CTFd bridge later)
and **any frontend** (the web prototype now, Unity WebGL later).

> Both frontends consume the *exact same* messages. Build/tune against the web
> prototype, then point Unity's `NativeWebSocket` client at the same `ws://` URL.
> No protocol change needed.

- Transport: WebSocket, default `ws://localhost:8080`
- Encoding: UTF-8 JSON, one JSON object per message
- Direction: server → client (frontends are pure listeners in v1)

## Envelope

Every message has the same outer shape:

```json
{ "v": 1, "type": "SOLVE", "ts": 1716600000.123, "data": { } }
```

| field  | type    | meaning                                            |
|--------|---------|----------------------------------------------------|
| `v`    | int     | protocol version (currently `1`)                   |
| `type` | string  | event name (see below)                             |
| `ts`   | float   | server unix time (seconds)                         |
| `data` | object  | event-specific payload                             |

## Canonical category keys

Always lowercase. The frontend maps each to a color + effect (see `web/config.js`).

| key         | label     | color  | signature effect          |
|-------------|-----------|--------|---------------------------|
| `pwn`       | Pwn       | red    | angular cartoon explosion |
| `web`       | Web       | green  | matrix code rain          |
| `wifi`      | WiFi      | yellow | lightning strike          |
| `reverse`   | Reverse   | blue   | gears + math symbols      |
| `forensics` | Forensics | purple | psychic spiral aura       |
| `crypto`    | Crypto    | orange | energy vortex             |
| `iot`       | IoT       | black  | shadow tendrils           |
| `osint`     | OSINT     | white  | light flash / radar ping  |
| `b2r`       | B2R       | gold   | root-shell crown breach   |

> 9 categories, your "8 main colors" + **gold** added for **B2R** (the 9th).
> Change any color/effect in one place: `web/config.js`.

## Team identifiers

Teams are always referenced by side: `"A"` (left) or `"B"` (right).
Display names live in state (`teamA.name`, `teamB.name`).

## Events (server → client)

### `STATE` — full snapshot
Sent on connect and after any structural change. Frontend should treat this as
the source of truth and re-render the whole HUD.

```json
{ "v":1, "type":"STATE", "ts":0, "data": {
  "round": "Qualifier",
  "phase": "live",
  "timer": { "remainingSec": 1800, "running": true },
  "teamA": { "name": "Team Alpha", "score": 0, "solves": [] },
  "teamB": { "name": "Team Bravo", "score": 0, "solves": [] },
  "bannedCategories": []
}}
```

`phase`: `"idle" | "live" | "ended"`.
`solves`: array of category keys solved so far by that team (in order).

### `MATCH_START`
```json
{ "v":1, "type":"MATCH_START", "ts":0, "data": {
  "round": "Semifinal",
  "teamA": { "name": "Team Alpha" },
  "teamB": { "name": "Team Bravo" },
  "durationSec": 1800,
  "bannedCategories": ["iot"]
}}
```

### `SOLVE` — a team solved a challenge → fire that team's attack
```json
{ "v":1, "type":"SOLVE", "ts":0, "data": {
  "team": "A",
  "category": "pwn",
  "challenge": "babyrop",
  "points": 100,
  "scoreA": 100,
  "scoreB": 0,
  "solveCount": 1
}}
```

### `WRONG` — a team submitted a wrong flag → self flinch / "MISS"
```json
{ "v":1, "type":"WRONG", "ts":0, "data": {
  "team": "B",
  "category": "web",
  "challenge": "sqli-101"
}}
```

### `BAN` — semis/finals category ban
```json
{ "v":1, "type":"BAN", "ts":0, "data": { "team": "A", "category": "iot" } }
```

### `TIMER` — countdown tick (sent ~1/sec while running)
```json
{ "v":1, "type":"TIMER", "ts":0, "data": { "remainingSec": 95, "running": true } }
```

### `MATCH_END`
```json
{ "v":1, "type":"MATCH_END", "ts":0, "data": {
  "winner": "B",            // "A" | "B" | "DRAW"
  "scoreA": 200, "scoreB": 300,
  "reason": "timeout"        // "timeout" | "all-solved" | "manual"
}}
```

### `ANNOUNCE` — free-text banner (intros, "FIRST BLOOD", etc.)
```json
{ "v":1, "type":"ANNOUNCE", "ts":0, "data": { "text": "FINALS — Alpha vs Bravo", "level": "info" } }
```

## Later: swapping the mock for real CTFd

Keep this contract frozen. The real backend only changes its **data source**:

1. Poll `GET /api/v1/submissions?type=correct` (with an Admin token) every ~2s.
2. Track the highest submission `id` seen (`lastId`).
3. For each new correct submission, map CTFd `user/team` → side `A`/`B` and
   `challenge.category` → a canonical key, then broadcast a `SOLVE`.
4. Bracket / ban / timer logic lives in the backend (CTFd has no native bracket),
   and is emitted as `MATCH_START` / `BAN` / `TIMER` / `MATCH_END`.

The frontend never knows whether it's talking to the mock or the real bridge.
