# CTF Stage Visualization

A data-driven, anime-style **2v2 battle arena** big-screen for a live CTF
tournament. Each time a team solves a challenge, their mascot fires an attack
whose color & effect match the challenge **category**. Built decoupled so the
visual layer can be the web prototype (now) or **Unity WebGL** (later) without
changing the backend.

```
CTFd (real, later) ──▶ backend (bracket/timer logic + WS) ──▶ frontend (web now / Unity later)
                                  protocol v1 (protocol.md)
```

## Layout

```
protocol.md              the JSON/WebSocket contract — the single source of truth
backend/
  game_server.py         ⭐ match driver: 3 random challenges, first to 2 wins
  mock_server.py         lower-level event poker + scenario macros (sim/spam/clutch)
  requirements.txt
web/                     the web prototype frontend (vanilla JS, no build step)
  index.html  style.css
  config.js              ⭐ category → color + effect mapping (retune here)
  effects.js             the 9 particle effects
  arena.js               arena + mascots + attack choreography
  main.js                WebSocket client + HUD + offline keyboard preview
CTF_anime_screen/        the existing Unity 6 (URP) project — Unity layer goes here
mock-api.py              the original tiny mock (kept for reference; superseded)
```

## Run it (two terminals)

**1 — backend** (pick one; both speak the same protocol on ws://localhost:8080)
```bash
cd backend
pip install -r requirements.txt
python game_server.py 10         # ⭐ 10-min round, 3 random challenges, first to 2 wins
# or: python mock_server.py      # manual event poker + scenario macros
```

`game_server.py` commands: `board` (list codes like c1=crypto#1), `a c1` (Team A
solves), `b p1`, `a x w1` (wrong flag), `new` (fresh round), `help`.

**2 — serve the web page** (ES modules need http://, not file://)
```bash
cd web
python3 -m http.server 8000
```
Open <http://localhost:8000>. The status dot turns green when connected.

### Drive the show (in the backend terminal)
```
start Final 1800     begin a 30-min final
s A pwn              Team A solves Pwn   → red explosion on B
s B crypto 200       Team B solves Crypto for 200
w A web              Team A wrong flag   → A flinches
ban B iot            Team B bans IoT
timer 600            start a 10-min countdown
sim web              both teams solve Web ~simultaneously
spam A 5             5 wrong flags from A
clutch               last-second winning solve
end A                end, A wins
```

### Preview effects WITHOUT the backend
Open the page and use the keyboard (legend bottom-right, toggle with `d`):
`1`–`9` = Team A fires that category · `shift`+`1`–`9` = Team B ·
`,` / `.` = wrong A/B · `space` = simultaneous.

## Category → color → effect

| key | category | color | effect |
|-----|----------|-------|--------|
| 1 `pwn` | Pwn | red | angular explosion |
| 2 `web` | Web | green | matrix code rain |
| 3 `wifi` | WiFi | yellow | lightning |
| 4 `reverse` | Reverse | blue | gears + math |
| 5 `forensics` | Forensics | purple | psychic spiral |
| 6 `crypto` | Crypto | orange | energy vortex |
| 7 `iot` | IoT | black | shadow tendrils |
| 8 `osint` | OSINT | white | light/radar flash |
| 9 `b2r` | B2R | **gold** | root-shell crown |

> Your "8 main colors" + **gold for B2R** (the 9th category). Change anything
> in one place: `web/config.js`.

## Asset / IP note

The mascots and effects are **original & procedural** — intentionally *not*
Pokémon or any existing IP. For a public, paid event, do **not** ship ripped or
AI-cloned Pokémon models/moves. Keep the "creature-battle" *feeling* with your
own designs. Real 3D models belong in the Unity layer (Meshy/Tripo text-to-3D,
or cartoon FX from the Unity Asset Store).

## Going live with CTFd

Freeze `protocol.md`. Replace only the mock's data source with a CTFd poller:
poll `GET /api/v1/submissions?type=correct`, track the last seen id, map
team→side & challenge.category→key, and emit the same `SOLVE` events. Bracket /
ban / timer logic stays in the backend (CTFd has no native bracket). The
frontend never changes.
