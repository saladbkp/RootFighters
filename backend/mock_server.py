#!/usr/bin/env python3
"""
CTF Stage Visualization — Mock backend (WebSocket).

Broadcasts protocol v1 events (see ../protocol.md) to every connected frontend
(the web prototype now, Unity later). Drive it interactively from the terminal,
or fire scripted scenarios for the hard-to-stage moments (simultaneous solve,
wrong-flag spam, last-second clutch).

Run:
    pip install -r requirements.txt
    python mock_server.py            # listens on ws://localhost:8080

Then open ../web/index.html in a browser. Type `help` in this terminal.

This file has NO knowledge of CTFd. When it's time to go live, keep the same
broadcast() calls and replace the interactive commands with a CTFd poller
(see protocol.md → "Later: swapping the mock for real CTFd").
"""

import asyncio
import json
import sys
import time

import websockets

HOST = "localhost"
PORT = 8080
PROTOCOL_VERSION = 1
DEFAULT_DURATION = 30 * 60  # 30 minutes, per the qualifier rules
DEFAULT_POINTS = 100

CATEGORIES = [
    "pwn", "web", "wifi", "reverse", "forensics",
    "crypto", "iot", "osint", "b2r",
]
# Friendly aliases → canonical key
ALIASES = {
    "binary": "pwn", "bin": "pwn",
    "rev": "reverse", "re": "reverse",
    "crypt": "crypto", "cry": "crypto",
    "for": "forensics", "forensic": "forensics", "dfir": "forensics",
    "recon": "osint", "osi": "osint",
    "wireless": "wifi",
    "boot2root": "b2r", "root": "b2r",
}

CLIENTS: set = set()


# --------------------------------------------------------------------------- #
# Match state                                                                  #
# --------------------------------------------------------------------------- #
class State:
    def __init__(self) -> None:
        self.round = "Qualifier"
        self.phase = "idle"  # idle | live | ended
        self.teamA = {"name": "Team Alpha", "score": 0, "solves": []}
        self.teamB = {"name": "Team Bravo", "score": 0, "solves": []}
        self.banned: list[str] = []
        self.remaining = DEFAULT_DURATION
        self.timer_running = False
        self.first_blood = True

    def team(self, side: str) -> dict:
        return self.teamA if side == "A" else self.teamB

    def snapshot(self) -> dict:
        return {
            "round": self.round,
            "phase": self.phase,
            "timer": {"remainingSec": self.remaining, "running": self.timer_running},
            "teamA": self.teamA,
            "teamB": self.teamB,
            "bannedCategories": self.banned,
        }


STATE = State()


# --------------------------------------------------------------------------- #
# Networking                                                                   #
# --------------------------------------------------------------------------- #
def envelope(msg_type: str, data: dict) -> str:
    return json.dumps({"v": PROTOCOL_VERSION, "type": msg_type, "ts": time.time(), "data": data})


async def broadcast(msg_type: str, data: dict) -> None:
    """Send one event to every connected frontend."""
    if not CLIENTS:
        # Still useful to see what *would* have been sent.
        print(f"   (no clients connected) {msg_type} {data}")
        return
    payload = envelope(msg_type, data)
    dead = []
    for ws in CLIENTS:
        try:
            await ws.send(payload)
        except websockets.ConnectionClosed:
            dead.append(ws)
    for ws in dead:
        CLIENTS.discard(ws)
    print(f"   → {msg_type} {data}")


async def send_state() -> None:
    await broadcast("STATE", STATE.snapshot())


async def handler(websocket) -> None:
    CLIENTS.add(websocket)
    print(f"\n[+] frontend connected ({len(CLIENTS)} total)")
    try:
        # Sync the newcomer to current state immediately.
        await websocket.send(envelope("STATE", STATE.snapshot()))
        async for _ in websocket:  # frontends are listeners; ignore inbound
            pass
    except websockets.ConnectionClosed:
        pass
    finally:
        CLIENTS.discard(websocket)
        print(f"[-] frontend disconnected ({len(CLIENTS)} total)")


# --------------------------------------------------------------------------- #
# High-level actions (these are what the real CTFd bridge will also call)      #
# --------------------------------------------------------------------------- #
def norm_cat(raw: str) -> str | None:
    key = ALIASES.get(raw.lower(), raw.lower())
    return key if key in CATEGORIES else None


async def do_start(round_name: str | None, secs: int | None) -> None:
    if round_name:
        STATE.round = round_name
    STATE.phase = "live"
    STATE.teamA["score"] = 0
    STATE.teamA["solves"] = []
    STATE.teamB["score"] = 0
    STATE.teamB["solves"] = []
    STATE.first_blood = True
    STATE.remaining = secs if secs is not None else DEFAULT_DURATION
    STATE.timer_running = False
    await broadcast("MATCH_START", {
        "round": STATE.round,
        "teamA": {"name": STATE.teamA["name"]},
        "teamB": {"name": STATE.teamB["name"]},
        "durationSec": STATE.remaining,
        "bannedCategories": STATE.banned,
    })
    await send_state()


async def do_solve(side: str, cat: str, points: int = DEFAULT_POINTS) -> None:
    t = STATE.team(side)
    t["score"] += points
    t["solves"].append(cat)
    if STATE.first_blood:
        STATE.first_blood = False
        await broadcast("ANNOUNCE", {
            "text": f"FIRST BLOOD — {t['name']}", "level": "hype"})
    await broadcast("SOLVE", {
        "team": side,
        "category": cat,
        "challenge": f"{cat}-chal",
        "points": points,
        "scoreA": STATE.teamA["score"],
        "scoreB": STATE.teamB["score"],
        "solveCount": len(t["solves"]),
    })


async def do_wrong(side: str, cat: str | None) -> None:
    await broadcast("WRONG", {
        "team": side,
        "category": cat or "unknown",
        "challenge": f"{cat or 'unknown'}-chal",
    })


async def do_ban(side: str, cat: str) -> None:
    if cat not in STATE.banned:
        STATE.banned.append(cat)
    await broadcast("BAN", {"team": side, "category": cat})
    await send_state()


async def do_end(winner: str, reason: str = "manual") -> None:
    STATE.phase = "ended"
    STATE.timer_running = False
    await broadcast("MATCH_END", {
        "winner": winner,
        "scoreA": STATE.teamA["score"],
        "scoreB": STATE.teamB["score"],
        "reason": reason,
    })
    await send_state()


# --------------------------------------------------------------------------- #
# Timer task                                                                   #
# --------------------------------------------------------------------------- #
async def timer_loop() -> None:
    """Ticks once per second; broadcasts TIMER while running."""
    while True:
        await asyncio.sleep(1)
        if STATE.timer_running and STATE.remaining > 0:
            STATE.remaining -= 1
            await broadcast("TIMER", {"remainingSec": STATE.remaining,
                                      "running": STATE.timer_running})
            if STATE.remaining == 0:
                STATE.timer_running = False
                a, b = STATE.teamA["score"], STATE.teamB["score"]
                winner = "A" if a > b else "B" if b > a else "DRAW"
                await do_end(winner, reason="timeout")


# --------------------------------------------------------------------------- #
# Scripted scenarios (the moments that are painful to stage on real CTFd)      #
# --------------------------------------------------------------------------- #
async def scn_simultaneous(cat: str) -> None:
    """Both teams solve the same category within half a second."""
    print(f"   [scenario] simultaneous {cat} solve")
    await do_solve("A", cat)
    await asyncio.sleep(0.4)
    await do_solve("B", cat)


async def scn_spam(side: str, n: int) -> None:
    """A burst of wrong submissions from one team."""
    print(f"   [scenario] {n}x wrong from {side}")
    for _ in range(n):
        await do_wrong(side, "pwn")
        await asyncio.sleep(0.35)


async def scn_clutch() -> None:
    """Last-second win: timer drops to 5s, B solves at T-1 to overtake."""
    print("   [scenario] last-second clutch")
    STATE.remaining = 5
    STATE.timer_running = True
    await send_state()
    await asyncio.sleep(4)
    await do_solve("B", "b2r", points=500)
    await broadcast("ANNOUNCE", {"text": "CLUTCH! Bravo steals it!", "level": "hype"})


# --------------------------------------------------------------------------- #
# Interactive command console                                                 #
# --------------------------------------------------------------------------- #
HELP = """
commands:
  start [round] [secs]     begin a match           e.g. start Final 1800
  s <A|B> <cat> [pts]      a SOLVE                  e.g. s A pwn   |  s B crypto 200
  w <A|B> [cat]            a WRONG flag             e.g. w B web
  ban <A|B> <cat>          ban a category          e.g. ban A iot
  timer <secs>             set + start countdown    e.g. timer 600
  pause | resume           toggle countdown
  end [A|B|draw]           end the match
  name <A|B> <text...>     rename a team           e.g. name A Cynx
  announce <text...>       banner message
  -- scenarios --
  sim <cat>                both teams solve <cat> ~together
  spam <A|B> [n]           n wrong flags in a row (default 5)
  clutch                   last-second winning solve
  -- misc --
  state                    print current state
  cats                     list category keys
  help                     this menu
  quit                     exit
""".strip()


async def run_command(line: str) -> bool:
    """Execute one console line. Returns False to quit."""
    parts = line.split()
    if not parts:
        return True
    cmd, args = parts[0].lower(), parts[1:]

    def side(a: str) -> str | None:
        a = a.upper()
        return a if a in ("A", "B") else None

    try:
        if cmd in ("quit", "exit", "q"):
            return False
        elif cmd in ("help", "h", "?"):
            print(HELP)
        elif cmd == "cats":
            print("   " + ", ".join(CATEGORIES))
        elif cmd == "state":
            print(json.dumps(STATE.snapshot(), indent=2, ensure_ascii=False))
        elif cmd == "start":
            rnd = args[0] if args else None
            secs = int(args[1]) if len(args) > 1 else None
            await do_start(rnd, secs)
        elif cmd == "s":
            sd, cat = side(args[0]), norm_cat(args[1])
            pts = int(args[2]) if len(args) > 2 else DEFAULT_POINTS
            if not sd or not cat:
                print("   usage: s <A|B> <cat> [pts]")
            else:
                await do_solve(sd, cat, pts)
        elif cmd == "w":
            sd = side(args[0])
            cat = norm_cat(args[1]) if len(args) > 1 else None
            if not sd:
                print("   usage: w <A|B> [cat]")
            else:
                await do_wrong(sd, cat)
        elif cmd == "ban":
            sd, cat = side(args[0]), norm_cat(args[1])
            if not sd or not cat:
                print("   usage: ban <A|B> <cat>")
            else:
                await do_ban(sd, cat)
        elif cmd == "timer":
            STATE.remaining = int(args[0]) if args else DEFAULT_DURATION
            STATE.timer_running = True
            STATE.phase = "live"
            await broadcast("TIMER", {"remainingSec": STATE.remaining, "running": True})
        elif cmd == "pause":
            STATE.timer_running = False
            await broadcast("TIMER", {"remainingSec": STATE.remaining, "running": False})
        elif cmd == "resume":
            STATE.timer_running = True
            await broadcast("TIMER", {"remainingSec": STATE.remaining, "running": True})
        elif cmd == "end":
            w = args[0].upper() if args else "DRAW"
            w = w if w in ("A", "B", "DRAW") else "DRAW"
            await do_end(w)
        elif cmd == "name":
            sd = side(args[0])
            if not sd or len(args) < 2:
                print("   usage: name <A|B> <text...>")
            else:
                STATE.team(sd)["name"] = " ".join(args[1:])
                await send_state()
        elif cmd == "announce":
            await broadcast("ANNOUNCE", {"text": " ".join(args), "level": "info"})
        elif cmd == "sim":
            cat = norm_cat(args[0]) if args else "pwn"
            await scn_simultaneous(cat or "pwn")
        elif cmd == "spam":
            sd = side(args[0]) if args else "A"
            n = int(args[1]) if len(args) > 1 else 5
            await scn_spam(sd or "A", n)
        elif cmd == "clutch":
            await scn_clutch()
        else:
            print(f"   unknown command: {cmd}  (try 'help')")
    except (IndexError, ValueError):
        print("   bad arguments  (try 'help')")
    return True


async def console_loop() -> None:
    loop = asyncio.get_event_loop()
    print(HELP)
    print(f"\nlistening on ws://{HOST}:{PORT} — open ../web/index.html\n")
    while True:
        line = await loop.run_in_executor(None, sys.stdin.readline)
        if not line:  # EOF (Ctrl-D)
            break
        if not await run_command(line.strip()):
            break
    print("bye.")


# --------------------------------------------------------------------------- #
async def main() -> None:
    async with websockets.serve(handler, HOST, PORT):
        await asyncio.gather(timer_loop(), console_loop())


if __name__ == "__main__":
    try:
        asyncio.run(main())
    except KeyboardInterrupt:
        pass
