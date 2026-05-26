# Auto-Rig & Animate Pipeline

Adds idle/attack/hurt animations to static GLB/FBX models using Blender headless.

## Prerequisites

```bash
brew install --cask blender
pip3 install bpy  # optional, Blender ships its own Python
```

## Usage

```bash
# Rig and animate a model (outputs FBX with embedded animations)
python3 autorig.py ../Assets/Models/TeamA_goblin.glb --output ../Assets/Models/TeamA_goblin_animated.fbx

# Batch all models
python3 autorig.py ../Assets/Models/TeamB_Barbarian.glb --output ../Assets/Models/TeamB_Barbarian_animated.fbx
```

## What it does

1. Imports GLB/FBX into Blender
2. Auto-detects mesh bounds → generates a simple bone rig (spine + arms + legs)
3. Creates 3 procedural animations:
   - **Idle**: gentle breathing/bobbing loop (2s)
   - **Attack**: forward lunge + arm swing (0.8s)
   - **Hurt**: stagger backward (0.5s)
4. Exports as FBX with animations embedded
5. Unity auto-detects AnimationClips from the FBX

## Alternative: Pre-animated models

If auto-rigging doesn't work well, use these free sources with built-in animations:

- [Quaternius.com](https://quaternius.com) — CC0 low-poly animated characters
- [Kenney.nl/assets](https://kenney.nl/assets) — CC0 game assets
- [Kay Lousberg](https://kaylousberg.com/game-assets) — free animated characters
- Unity Asset Store — search "free animated character"
