#!/usr/bin/env python3
"""
CTF Stage — Game server (interactive match driver).

A realistic, controllable driver for testing the stage visualization. On launch
it generates a round of challenges across random categories, starts a countdown,
and lets you type terse commands so you can watch the UI react in real time.

Speaks protocol v1 (../protocol.md) — the SAME events as mock_server.py, so the
web prototype and Unity both work against it unchanged.

Usage:
    python game_server.py [minutes] [challenges]
      minutes     round length         (default 10)
      challenges  how many to release   (default 3, each a random category)

    First team to solve 2 challenges wins the round (qualifier rule).

    e.g.  python game_server.py 10     → 10-min round, 3 random challenges
          python game_server.py 10 5   → 10-min round, 5 random challenges

Challenge codes are <category-letter><index>, e.g. c1 = crypto #1, p2 = pwn #2:
    crypto=c  web=w  wifi=i  reverse=r  forensics=f  pwn=p  osint=o  iot=t  b2r=b

Drive it (in this terminal):
    a c1            Team A solves challenge c1     → fires crypto attack on B
    b p2            Team B solves p2
    a x w1          Team A submits a WRONG flag on w1
    board           reprint the challenge board
    new [m] [n]     start a fresh round (replays the intro)
    ban a iot       ban a category (semis/finals)
    pause | resume  countdown
    end [a|b|draw]  force-end the match
    name a Cynx     rename a team
    help | quit
"""

import asyncio
import json
import random
import sys
import time

import websockets

HOST, PORT = "localhost", 8080
PROTOCOL_VERSION = 1
WIN_TARGET = 2          # first team to this many solves advances (qualifier rule)
DEFAULT_POINTS = 100

CAT_LETTER = {
    "crypto": "c", "web": "w", "wifi": "i", "reverse": "r", "forensics": "f",
    "pwn": "p", "osint": "o", "iot": "t", "b2r": "b",
}
ALL_CATS = list(CAT_LETTER.keys())

CLIENTS: set = set()


# --------------------------------------------------------------------------- #
class Game:
    def __init__(self) -> None:
        self.round = "Qualifier"
        self.phase = "idle"           # idle | live | ended
        self.teamA = {"name": "Team Alpha", "score": 0, "solves": []}
        self.teamB = {"name": "Team Bravo", "score": 0, "solves": []}
        self.banned: list[str] = []
        self.remaining = 600
        self.running = False
        self.challenges: dict[str, dict] = {}   # code -> {cat, name, points, solvedBy}
        self.first_blood = True
        self.over = False

    def team(self, side: str) -> dict:
        return self.teamA if side == "A" else self.teamB

    def generate(self, minutes: float, n_challenges: int) -> None:
        self.challenges.clear()
        counts: dict[str, int] = {}
        for _ in range(max(1, n_challenges)):
            c = random.choice(ALL_CATS)            # any category, repeats allowed
            counts[c] = counts.get(c, 0) + 1
            code = f"{CAT_LETTER[c]}{counts[c]}"
            self.challenges[code] = {
                "cat": c, "name": f"{c.capitalize()} #{counts[c]}",
                "points": DEFAULT_POINTS, "solvedBy": set(),
            }
        self.remaining = int(minutes * 60)
        self.running = True
        self.phase = "live"
        self.over = False
        self.first_blood = True
        self.teamA["score"] = 0; self.teamA["solves"] = []
        self.teamB["score"] = 0; self.teamB["solves"] = []

    def board_lines(self) -> list[str]:
        out = []
        by_cat: dict[str, list[str]] = {}
        for code, ch in self.challenges.items():
            by_cat.setdefault(ch["cat"], []).append(code)
        for c, codes in by_cat.items():
            out.append(f"  {c:<10} {'  '.join(sorted(codes))}")
        return out

    def challenges_payload(self) -> list[dict]:
        return [{"code": code, "category": ch["cat"], "points": ch["points"]}
                for code, ch in self.challenges.items()]

    def snapshot(self) -> dict:
        return {
            "round": self.round,
            "phase": self.phase,
            "timer": {"remainingSec": self.remaining, "running": self.running},
            "teamA": self.teamA,
            "teamB": self.teamB,
            "bannedCategories": self.banned,
        }


GAME = Game()


# --------------------------------------------------------------------------- #
def envelope(t: str, data: dict) -> str:
    return json.dumps({"v": PROTOCOL_VERSION, "type": t, "ts": time.time(), "data": data})


async def broadcast(t: str, data: dict) -> None:
    if not CLIENTS:
        print(f"   (no clients) {t} {data}")
        return
    payload = envelope(t, data)
    dead = []
    for ws in CLIENTS:
        try:
            await ws.send(payload)
        except websockets.ConnectionClosed:
            dead.append(ws)
    for ws in dead:
        CLIENTS.discard(ws)
    print(f"   → {t} {data}")


async def send_state() -> None:
    await broadcast("STATE", GAME.snapshot())


async def handler(websocket) -> None:
    CLIENTS.add(websocket)
    print(f"\n[+] frontend connected ({len(CLIENTS)} total)")
    try:
        await websocket.send(envelope("STATE", GAME.snapshot()))
        async for _ in websocket:
            pass
    except websockets.ConnectionClosed:
        pass
    finally:
        CLIENTS.discard(websocket)
        print(f"[-] frontend disconnected ({len(CLIENTS)} total)")


# --------------------------------------------------------------------------- #
async def do_new(minutes: float, n_challenges: int) -> None:
    GAME.generate(minutes, n_challenges)
    print("\n=== NEW ROUND ===")
    for ln in GAME.board_lines():
        print(ln)
    print(f"  {len(GAME.challenges)} challenges · first to {WIN_TARGET} wins · "
          f"{int(GAME.remaining)//60} min\n")
    await broadcast("MATCH_START", {
        "round": GAME.round,
        "teamA": {"name": GAME.teamA["name"]},
        "teamB": {"name": GAME.teamB["name"]},
        "durationSec": GAME.remaining,
        "bannedCategories": GAME.banned,
        "challenges": GAME.challenges_payload(),   # extra (frontend ignores if unused)
    })
    await send_state()


async def do_solve(side: str, code: str) -> None:
    ch = GAME.challenges.get(code)
    if not ch:
        print(f"   no such challenge '{code}'  (type 'board')")
        return
    if side in ch["solvedBy"]:
        print(f"   {side} already solved {code}")
        return
    ch["solvedBy"].add(side)
    t = GAME.team(side)
    t["score"] += ch["points"]
    t["solves"].append(ch["cat"])

    if GAME.first_blood:
        GAME.first_blood = False
        await broadcast("ANNOUNCE", {"text": f"FIRST BLOOD — {t['name']}", "level": "hype"})

    await broadcast("SOLVE", {
        "team": side, "category": ch["cat"], "challenge": code,
        "points": ch["points"],
        "scoreA": GAME.teamA["score"], "scoreB": GAME.teamB["score"],
        "solveCount": len(t["solves"]),
    })

    if not GAME.over and len(t["solves"]) >= WIN_TARGET:
        GAME.over = True
        GAME.running = False
        await broadcast("ANNOUNCE", {"text": f"{t['name']} ADVANCES! ({WIN_TARGET} solved)", "level": "hype"})
        await do_end(side, reason="advanced")


async def do_wrong(side: str, code: str) -> None:
    ch = GAME.challenges.get(code)
    cat = ch["cat"] if ch else "unknown"
    await broadcast("WRONG", {"team": side, "category": cat, "challenge": code})


async def do_ban(side: str, cat: str) -> None:
    if cat in CAT_LETTER and cat not in GAME.banned:
        GAME.banned.append(cat)
    await broadcast("BAN", {"team": side, "category": cat})
    await send_state()


async def do_end(winner: str, reason: str = "manual") -> None:
    GAME.phase = "ended"
    GAME.running = False
    await broadcast("MATCH_END", {
        "winner": winner,
        "scoreA": GAME.teamA["score"], "scoreB": GAME.teamB["score"],
        "reason": reason,
    })
    await send_state()


async def timer_loop() -> None:
    while True:
        await asyncio.sleep(1)
        if GAME.running and GAME.remaining > 0:
            GAME.remaining -= 1
            await broadcast("TIMER", {"remainingSec": GAME.remaining, "running": True})
            if GAME.remaining == 0:
                GAME.running = False
                a, b = GAME.teamA["score"], GAME.teamB["score"]
                w = "A" if a > b else "B" if b > a else "DRAW"
                await do_end(w, reason="timeout")


# --------------------------------------------------------------------------- #
HELP = """
commands (challenge codes shown by 'board'):
  a <code>          Team A solves a challenge      e.g. a c1
  b <code>          Team B solves a challenge      e.g. b p2
  a x <code>        Team A WRONG flag              e.g. a x w1   (also: a wrong w1)
  board             reprint the challenge board
  new [m] [n]       fresh round (minutes / #challenges, default 10/3)
  ban a|b <cat>     ban a category
  pause | resume    countdown
  end [a|b|draw]    force-end
  name a|b <text>   rename a team
  help | quit
""".strip()


async def run_command(line: str) -> bool:
    parts = line.split()
    if not parts:
        return True
    cmd = parts[0].lower()

    def side_of(s: str):
        s = s.upper()
        return s if s in ("A", "B") else None

    try:
        if cmd in ("quit", "exit", "q"):
            return False
        if cmd in ("help", "h", "?"):
            print(HELP)
        elif cmd == "board":
            print("=== BOARD ===")
            for ln in GAME.board_lines():
                print(ln)
            print(f"  score  A {GAME.teamA['score']} : {GAME.teamB['score']} B   "
                  f"| {int(GAME.remaining)//60}:{int(GAME.remaining)%60:02d} left")
        elif cmd == "new":
            m = float(parts[1]) if len(parts) > 1 else 10
            n = int(parts[2]) if len(parts) > 2 else 3
            await do_new(m, n)
        elif cmd in ("a", "b"):
            side = cmd.upper()
            rest = parts[1:]
            if not rest:
                print("   usage: a <code>  |  a x <code>")
            elif rest[0].lower() in ("x", "w", "wrong"):
                await do_wrong(side, rest[1])
            elif rest[0].lower() == "solve":
                await do_solve(side, rest[1])
            else:
                await do_solve(side, rest[0])
        elif cmd == "ban":
            sd = side_of(parts[1])
            await do_ban(sd or "A", parts[2].lower())
        elif cmd == "pause":
            GAME.running = False
            await broadcast("TIMER", {"remainingSec": GAME.remaining, "running": False})
        elif cmd == "resume":
            GAME.running = True
            await broadcast("TIMER", {"remainingSec": GAME.remaining, "running": True})
        elif cmd == "end":
            w = parts[1].upper() if len(parts) > 1 else "DRAW"
            await do_end(w if w in ("A", "B", "DRAW") else "DRAW")
        elif cmd == "name":
            sd = side_of(parts[1])
            if sd and len(parts) > 2:
                GAME.team(sd)["name"] = " ".join(parts[2:])
                await send_state()
            else:
                print("   usage: name a|b <text>")
        else:
            print(f"   unknown command '{cmd}'  (try 'help')")
    except (IndexError, ValueError):
        print("   bad arguments  (try 'help')")
    return True


async def console_loop() -> None:
    loop = asyncio.get_event_loop()
    print(HELP)
    while True:
        line = await loop.run_in_executor(None, sys.stdin.readline)
        if not line:
            break
        if not await run_command(line.strip()):
            break
    print("bye.")


async def main() -> None:
    minutes = float(sys.argv[1]) if len(sys.argv) > 1 else 10
    n_challenges = int(sys.argv[2]) if len(sys.argv) > 2 else 3
    async with websockets.serve(handler, HOST, PORT):
        print(f"listening on ws://{HOST}:{PORT} — open ../web/index.html\n")
        await do_new(minutes, n_challenges)
        print("connect the screen, then type 'new' to replay the intro.\n")
        await asyncio.gather(timer_loop(), console_loop())


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
