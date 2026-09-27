"""Add the Tc1 announcement lamp to the organized export blend.

Run with Blender --background Series10000_UnityExport_Organized.blend \
    --python append_announcement_lamp.py -- --lamp-source <saved blend>

Only the three lamp objects are imported from the source. All existing objects,
materials, and material slots remain in the open organized blend.
"""
import argparse
import bpy
import sys
from pathlib import Path


parser = argparse.ArgumentParser()
parser.add_argument("--lamp-source", type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])

destination = Path(__file__).resolve().with_name(
    "Series10000_UnityExport_AnnouncementLamp.blend"
)
scene = bpy.data.scenes["Series10000_Tc1"]
export_collection = bpy.data.collections["Tc1_Export"]
car_root = scene.objects["Tc1"]

lamp_parts = {
    "知らせ灯_本体": ("Tc1_AnnouncementLamp_Body", "Series10000_CabUntextured"),
    "知らせ灯_遮光カバー": ("Tc1_AnnouncementLamp_Shade", "Series10000_CabUntextured"),
    "知らせ灯_点灯面": ("Tc1_AnnouncementLamp_Lens", "Series10000_Light"),
}

before = {
    obj.name: (
        obj.matrix_world.copy(),
        tuple(slot.material for slot in obj.material_slots),
    )
    for obj in scene.objects
}

with bpy.data.libraries.load(str(args.lamp_source), link=False) as (source, imported):
    missing = set(lamp_parts) - set(source.objects)
    if missing:
        raise RuntimeError(f"Lamp mesh not found in source: {sorted(missing)}")
    imported.objects = list(lamp_parts)

for obj in imported.objects:
    if obj is None or obj.type != "MESH":
        raise RuntimeError("The announcement lamp contains a missing or non-mesh part")
    if obj.parent is not None:
        raise RuntimeError(f"Lamp part has an unexpected parent: {obj.name}")
    if obj.name in scene.objects:
        raise RuntimeError(f"Lamp part already exists in Tc1: {obj.name}")

    original_name = obj.name
    # An appended object is not in a scene yet, so matrix_world is identity.
    # These source objects are unparented; matrix_basis is their saved world pose.
    original_world = obj.matrix_basis.copy()
    export_collection.objects.link(obj)
    obj.parent = car_root
    obj.matrix_world = original_world
    obj.name = lamp_parts[original_name][0]
    obj.data.name = obj.name

    # The three newly modeled parts have no assigned material in the source.
    # Reuse existing export materials without touching any older material slot.
    obj.data.materials.clear()
    obj.data.materials.append(bpy.data.materials[lamp_parts[original_name][1]])
    bpy.context.view_layer.update()
    if any(abs(a - b) > 0.000001 for row_a, row_b in zip(obj.matrix_world, original_world)
           for a, b in zip(row_a, row_b)):
        raise RuntimeError(f"World transform changed: {obj.name}")

for name, (world, slots) in before.items():
    obj = scene.objects[name]
    if obj.matrix_world != world or tuple(slot.material for slot in obj.material_slots) != slots:
        raise RuntimeError(f"Existing object changed: {name}")

bpy.context.window.scene = scene
bpy.ops.wm.save_as_mainfile(filepath=str(destination))
print(f"ORGANIZED {destination}", flush=True)
