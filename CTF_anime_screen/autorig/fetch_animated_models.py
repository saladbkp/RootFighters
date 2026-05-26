#!/usr/bin/env python3
"""
Download free pre-animated 3D models from Quaternius.com
These models come with built-in animations (Idle, Attack, etc.) — no rigging needed.

Usage:
    python3 fetch_animated_models.py
"""

import urllib.request
import os
import zipfile
import sys

# Quaternius free animated model packs (CC0 license)
MODELS = {
    "animated_monsters": {
        "url": "https://quaternius.com/packs/ultimateanimatedmonsters.html",
        "direct": "https://dl.quaternius.com/packs/Ultimate%20Animated%20Monsters.zip",
        "desc": "40+ animated monsters with Idle, Walk, Attack, Death animations"
    },
    "animated_characters": {
        "url": "https://quaternius.com/packs/ultimateanimatedcharacters.html",
        "direct": "https://dl.quaternius.com/packs/Ultimate%20Animated%20Characters.zip",
        "desc": "30+ animated characters with full animation sets"
    },
    "animated_fantasy": {
        "url": "https://quaternius.com/packs/ultimateanimatedfantasy.html",
        "direct": "https://dl.quaternius.com/packs/Ultimate%20Animated%20Fantasy.zip",
        "desc": "Fantasy characters (knights, wizards, etc.) with animations"
    },
}

OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Models", "Animated")


def download(name, info):
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    zip_path = os.path.join(OUTPUT_DIR, f"{name}.zip")

    if os.path.exists(zip_path):
        print(f"  Already downloaded: {zip_path}")
        return zip_path

    print(f"  Downloading {name}...")
    print(f"  URL: {info['direct']}")
    print(f"  This may take a minute (large file)...")

    try:
        urllib.request.urlretrieve(info["direct"], zip_path, reporthook=progress)
        print(f"\n  Saved: {zip_path}")
        return zip_path
    except Exception as e:
        print(f"\n  ERROR: {e}")
        print(f"  Manual download: {info['url']}")
        return None


def progress(block, block_size, total):
    downloaded = block * block_size
    if total > 0:
        pct = min(100, downloaded * 100 // total)
        mb = downloaded / 1024 / 1024
        total_mb = total / 1024 / 1024
        sys.stdout.write(f"\r  {mb:.1f}/{total_mb:.1f} MB ({pct}%)")
        sys.stdout.flush()


def extract(zip_path, name):
    extract_dir = os.path.join(OUTPUT_DIR, name)
    if os.path.exists(extract_dir):
        print(f"  Already extracted: {extract_dir}")
        return extract_dir

    print(f"  Extracting...")
    with zipfile.ZipFile(zip_path, 'r') as z:
        z.extractall(extract_dir)

    # List FBX/GLB files found
    animated = []
    for root, dirs, files in os.walk(extract_dir):
        for f in files:
            if f.endswith(('.fbx', '.FBX', '.glb', '.gltf')):
                animated.append(os.path.join(root, f))

    print(f"  Found {len(animated)} model files")
    for a in animated[:10]:
        print(f"    - {os.path.relpath(a, extract_dir)}")
    if len(animated) > 10:
        print(f"    ... and {len(animated) - 10} more")

    return extract_dir


def main():
    print("=" * 60)
    print("Animated Model Downloader")
    print("Models from Quaternius.com (CC0 — free for any use)")
    print("=" * 60)
    print()

    for i, (name, info) in enumerate(MODELS.items(), 1):
        print(f"[{i}] {name}")
        print(f"    {info['desc']}")
        print()

    choice = input("Download which? (1/2/3/all/q): ").strip().lower()

    if choice == 'q':
        return

    targets = []
    if choice == 'all':
        targets = list(MODELS.items())
    elif choice in ('1', '2', '3'):
        idx = int(choice) - 1
        targets = [list(MODELS.items())[idx]]
    else:
        print("Invalid choice")
        return

    for name, info in targets:
        print(f"\n{'='*40}")
        print(f"Processing: {name}")
        print(f"{'='*40}")
        zip_path = download(name, info)
        if zip_path:
            extract(zip_path, name)

    print(f"\n✅ Done! Models are in: {os.path.abspath(OUTPUT_DIR)}")
    print("Import the FBX files into Unity — they come with animations built in.")
    print("In Unity: drag FBX to scene, set Rig → Generic, Apply.")


if __name__ == "__main__":
    main()
