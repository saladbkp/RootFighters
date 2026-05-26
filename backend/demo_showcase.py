#!/usr/bin/env python3
"""
CTF Stage — Category Showcase Demo (auto-play).

Starts a match, then fires one SOLVE every 6 seconds cycling through all 9
categories, alternating A/B teams. Perfect for recording a trailer or
demonstrating each category's unique VFX on stage.

Usage:
    python demo_showcase.py            # default 6s interval
    python demo_showcase.py 4          # 4s interval (faster)

Protocol v1 — same WebSocket as game_server / mock_server.
"""

import asyncio
import json
import sys
import time

import websockets

HOST, PORT = "localhost", 8080
V = 1
INTERVAL = float(sys.argv[1]) if len(sys.argv) > 1 else 10.0  # 10s: 3s banner + 1.5s zoom+attack + 0.5s projectile + 3s VFX + 2s pause

CATEGORIES = [
    "pwn", "web", "wifi", "reverse", "forensics",
    "crypto", "iot", "osint", "b2r",
]

CLIENTS: set = set()

# ── networking ──────────────────────────────────────────────────────────────── #

def envelope(t: str, d: dict) -> str:
    return json.dumps({"v": V, "type": t, "ts": time.time(), "data": d})


async def broadcast(t: str, d: dict):
    msg = envelope(t, d)
    dead = []
    for ws in CLIENTS:
        try:
            await ws.send(msg)
        except websockets.ConnectionClosed:
            dead.append(ws)
    for ws in dead:
        CLIENTS.discard(ws)
    print(f"  → {t} {d}")


async def handler(ws):
    CLIENTS.add(ws)
    print(f"[+] client connected ({len(CLIENTS)})")
    await ws.send(envelope("STATE", snapshot("idle", 0, 0)))
    try:
        async for _ in ws:
            pass
    except websockets.ConnectionClosed:
        pass
    finally:
        CLIENTS.discard(ws)
        print(f"[-] client disconnected ({len(CLIENTS)})")


def snapshot(phase, sa, sb):
    return {
        "round": "SHOWCASE",
        "phase": phase,
        "timer": {"remainingSec": 999, "running": phase == "live"},
        "teamA": {"name": "Dragon Force", "score": sa, "solves": []},
        "teamB": {"name": "Demon Squad", "score": sb, "solves": []},
        "bannedCategories": [],
    }


# ── main sequence ──────────────────────────────────────────────────────────── #

async def run_demo():
    # wait for at least one client
    print("Waiting for Unity/web to connect...")
    while not CLIENTS:
        await asyncio.sleep(0.3)

    print(f"\n{'='*50}")
    print(f"  SHOWCASE: {len(CATEGORIES)} categories, {INTERVAL}s each")
    print(f"{'='*50}\n")

    # send STATE + MATCH_START
    await broadcast("STATE", snapshot("live", 0, 0))
    await asyncio.sleep(0.3)
    await broadcast("MATCH_START", {
        "round": "SHOWCASE",
        "teamA": {"name": "Dragon Force"},
        "teamB": {"name": "Demon Squad"},
        "durationSec": 999,
        "bannedCategories": [],
    })

    # wait for intro animation
    await asyncio.sleep(3.0)

    score_a, score_b = 0, 0
    solve_count = 0

    for i, cat in enumerate(CATEGORIES):
        side = "A" if i % 2 == 0 else "B"
        solve_count += 1
        pts = 100

        if side == "A":
            score_a += pts
        else:
            score_b += pts

        print(f"\n  [{i+1}/{len(CATEGORIES)}] Team {side} → {cat.upper()}")

        # fire the solve (attack banner + VFX play simultaneously now)
        await broadcast("SOLVE", {
            "team": side,
            "category": cat,
            "challenge": f"{cat}-demo",
            "points": pts,
            "scoreA": score_a,
            "scoreB": score_b,
            "solveCount": solve_count,
        })

        # timer tick (cosmetic)
        remaining = 999 - (i + 1) * int(INTERVAL)
        await broadcast("TIMER", {"remainingSec": max(0, remaining), "running": True})

        # wait for VFX to play out
        await asyncio.sleep(INTERVAL)

    # end: A wins (has more solves since it went first)
    await asyncio.sleep(1.0)
    winner = "A" if score_a >= score_b else "B"
    await broadcast("MATCH_END", {
        "winner": winner,
        "scoreA": score_a,
        "scoreB": score_b,
        "reason": "all-solved",
    })

    print(f"\n{'='*50}")
    print(f"  SHOWCASE COMPLETE — {winner} wins {score_a}:{score_b}")
    print(f"{'='*50}")
    print("  Press Ctrl+C to exit, or keep running for replays.")

    # keep server alive
    while True:
        await asyncio.sleep(60)


async def main():
    async with websockets.serve(handler, HOST, PORT):
        print(f"Demo server on ws://{HOST}:{PORT}")
        print(f"Interval: {INTERVAL}s — connect Unity then showcase starts.")
        await run_demo()


if __name__ == "__main__":
    asyncio.run(main())
