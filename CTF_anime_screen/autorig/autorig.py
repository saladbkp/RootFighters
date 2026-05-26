#!/usr/bin/env python3
"""
Auto-rig and animate a static GLB/FBX model using Blender headless.
Outputs an FBX with embedded Idle/Attack/Hurt animation clips.

Usage:
    # Direct (Blender as module - needs bpy installed):
    python3 autorig.py model.glb --output model_animated.fbx

    # Via Blender headless (recommended):
    blender --background --python autorig.py -- model.glb --output model_animated.fbx
"""

import sys
import os
import argparse
import math

def parse_args():
    # When run via "blender --python script.py -- args", args after -- go to sys.argv
    argv = sys.argv
    if "--" in argv:
        argv = argv[argv.index("--") + 1:]
    else:
        argv = argv[1:]

    parser = argparse.ArgumentParser(description="Auto-rig and animate a 3D model")
    parser.add_argument("input", help="Input GLB/FBX file path")
    parser.add_argument("--output", "-o", help="Output FBX path (default: input_animated.fbx)")
    parser.add_argument("--idle-duration", type=float, default=2.0, help="Idle animation duration (seconds)")
    parser.add_argument("--attack-duration", type=float, default=0.8, help="Attack animation duration (seconds)")
    parser.add_argument("--hurt-duration", type=float, default=0.5, help="Hurt animation duration (seconds)")
    return parser.parse_args(argv)


def main():
    args = parse_args()

    if not args.output:
        base, _ = os.path.splitext(args.input)
        args.output = base + "_animated.fbx"

    try:
        import bpy
    except ImportError:
        print("ERROR: bpy (Blender Python module) not found.")
        print("Run via Blender headless instead:")
        print(f"  blender --background --python {__file__} -- {args.input} -o {args.output}")
        sys.exit(1)

    print(f"[autorig] Input:  {args.input}")
    print(f"[autorig] Output: {args.output}")

    # Clear scene
    bpy.ops.wm.read_factory_settings(use_empty=True)

    # Import model
    ext = os.path.splitext(args.input)[1].lower()
    if ext == ".glb" or ext == ".gltf":
        bpy.ops.import_scene.gltf(filepath=os.path.abspath(args.input))
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=os.path.abspath(args.input))
    elif ext == ".obj":
        bpy.ops.import_scene.obj(filepath=os.path.abspath(args.input))
    else:
        print(f"ERROR: Unsupported format '{ext}'")
        sys.exit(1)

    # Find the mesh object(s)
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
    if not meshes:
        print("ERROR: No mesh objects found in the model")
        sys.exit(1)

    print(f"[autorig] Found {len(meshes)} mesh(es)")

    # Join all meshes into one
    bpy.ops.object.select_all(action='DESELECT')
    for m in meshes:
        m.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()

    mesh_obj = bpy.context.active_object
    # Compute bounding box
    bbox = mesh_obj.bound_box
    min_z = min(v[2] for v in bbox)
    max_z = max(v[2] for v in bbox)
    min_y = min(v[1] for v in bbox)
    max_y = max(v[1] for v in bbox)
    min_x = min(v[0] for v in bbox)
    max_x = max(v[0] for v in bbox)
    height = max_z - min_z  # Blender Z-up
    center_x = (min_x + max_x) / 2
    center_y = (min_y + max_y) / 2

    print(f"[autorig] Mesh height: {height:.3f}, center: ({center_x:.3f}, {center_y:.3f})")

    # ── CREATE ARMATURE ──
    bpy.ops.object.select_all(action='DESELECT')
    bpy.ops.object.armature_add(enter_editmode=True, location=(center_x, center_y, min_z))
    armature_obj = bpy.context.active_object
    armature_obj.name = "Rig"
    armature = armature_obj.data
    armature.name = "RigData"

    # Remove default bone
    for b in armature.edit_bones:
        armature.edit_bones.remove(b)

    # Simple skeleton: Root → Spine → Chest → Head
    root_bone = armature.edit_bones.new("Root")
    root_bone.head = (0, 0, 0)
    root_bone.tail = (0, 0, height * 0.15)

    spine = armature.edit_bones.new("Spine")
    spine.head = root_bone.tail
    spine.tail = (0, 0, height * 0.4)
    spine.parent = root_bone

    chest = armature.edit_bones.new("Chest")
    chest.head = spine.tail
    chest.tail = (0, 0, height * 0.65)
    chest.parent = spine

    head = armature.edit_bones.new("Head")
    head.head = chest.tail
    head.tail = (0, 0, height * 0.85)
    head.parent = chest

    # Arms
    l_arm = armature.edit_bones.new("L_Arm")
    l_arm.head = (0, 0, height * 0.55)
    l_arm.tail = (height * 0.3, 0, height * 0.45)
    l_arm.parent = chest

    r_arm = armature.edit_bones.new("R_Arm")
    r_arm.head = (0, 0, height * 0.55)
    r_arm.tail = (-height * 0.3, 0, height * 0.45)
    r_arm.parent = chest

    bpy.ops.object.mode_set(mode='OBJECT')

    # ── PARENT MESH TO ARMATURE ──
    bpy.ops.object.select_all(action='DESELECT')
    mesh_obj.select_set(True)
    armature_obj.select_set(True)
    bpy.context.view_layer.objects.active = armature_obj
    bpy.ops.object.parent_set(type='ARMATURE_AUTO')

    print("[autorig] Armature created and mesh parented with automatic weights")

    # ── CREATE ANIMATIONS ──
    fps = 24

    def create_action(name, duration):
        action = bpy.data.actions.new(name=name)
        action.use_fake_user = True
        return action

    def add_bone_keyframes(action, bone_name, channel, frame, value):
        """Add keyframe to a bone's transform channel"""
        data_path = f'pose.bones["{bone_name}"].{channel}'
        if isinstance(value, (list, tuple)):
            for i, v in enumerate(value):
                fc = action.fcurves.find(data_path, index=i)
                if fc is None:
                    fc = action.fcurves.new(data_path, index=i)
                fc.keyframe_points.insert(frame, v)
        else:
            fc = action.fcurves.find(data_path, index=0)
            if fc is None:
                fc = action.fcurves.new(data_path, index=0)
            fc.keyframe_points.insert(frame, value)

    # IDLE: gentle bobbing on Root bone (Y translation oscillation)
    idle_action = create_action("Idle", args.idle_duration)
    idle_frames = int(args.idle_duration * fps)
    for f in range(idle_frames + 1):
        t = f / fps
        bob = math.sin(t * math.pi * 2 / args.idle_duration) * height * 0.02
        add_bone_keyframes(idle_action, "Root", "location", f, (0, 0, bob))
        # Slight chest rotation for breathing
        breath = math.sin(t * math.pi * 2 / args.idle_duration) * 0.03
        add_bone_keyframes(idle_action, "Chest", "rotation_euler", f, (breath, 0, 0))

    # ATTACK: forward lunge + arm swing
    atk_action = create_action("Attack", args.attack_duration)
    atk_frames = int(args.attack_duration * fps)
    mid = atk_frames // 2
    # Root moves forward
    add_bone_keyframes(atk_action, "Root", "location", 0, (0, 0, 0))
    add_bone_keyframes(atk_action, "Root", "location", mid, (0, height * 0.15, 0))
    add_bone_keyframes(atk_action, "Root", "location", atk_frames, (0, 0, 0))
    # Chest leans forward
    add_bone_keyframes(atk_action, "Chest", "rotation_euler", 0, (0, 0, 0))
    add_bone_keyframes(atk_action, "Chest", "rotation_euler", mid, (0.4, 0, 0))
    add_bone_keyframes(atk_action, "Chest", "rotation_euler", atk_frames, (0, 0, 0))
    # Right arm swings
    add_bone_keyframes(atk_action, "R_Arm", "rotation_euler", 0, (0, 0, 0))
    add_bone_keyframes(atk_action, "R_Arm", "rotation_euler", mid, (-1.2, 0, 0.5))
    add_bone_keyframes(atk_action, "R_Arm", "rotation_euler", atk_frames, (0, 0, 0))

    # HURT: stagger back
    hurt_action = create_action("HitRecieve", args.hurt_duration)
    hurt_frames = int(args.hurt_duration * fps)
    hurt_mid = hurt_frames // 2
    add_bone_keyframes(hurt_action, "Root", "location", 0, (0, 0, 0))
    add_bone_keyframes(hurt_action, "Root", "location", hurt_mid, (0, -height * 0.1, 0))
    add_bone_keyframes(hurt_action, "Root", "location", hurt_frames, (0, 0, 0))
    add_bone_keyframes(hurt_action, "Chest", "rotation_euler", 0, (0, 0, 0))
    add_bone_keyframes(hurt_action, "Chest", "rotation_euler", hurt_mid, (-0.3, 0, 0.1))
    add_bone_keyframes(hurt_action, "Chest", "rotation_euler", hurt_frames, (0, 0, 0))

    print(f"[autorig] Created animations: Idle({idle_frames}f), Attack({atk_frames}f), HitRecieve({hurt_frames}f)")

    # ── EXPORT FBX ──
    output_path = os.path.abspath(args.output)
    os.makedirs(os.path.dirname(output_path), exist_ok=True)

    # Push all actions into NLA tracks so they export as separate takes
    armature_obj.animation_data_create()
    for action in [idle_action, atk_action, hurt_action]:
        track = armature_obj.animation_data.nla_tracks.new()
        track.name = action.name
        track.strips.new(action.name, int(action.frame_range[0]), action)

    bpy.ops.export_scene.fbx(
        filepath=output_path,
        use_selection=False,
        bake_anim=True,
        bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=True,
        add_leaf_bones=False,
        path_mode='COPY',
        embed_textures=True,
    )

    print(f"[autorig] Exported: {output_path}")
    print("[autorig] Done! Import this FBX into Unity — animations will appear as clips.")


if __name__ == "__main__":
    main()
