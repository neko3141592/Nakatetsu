"""Export all four scenes from Series10000_UnityExport.blend.
Run with Blender --background <blend file> --python <this script>.
The editable Blender meshes are not triangulated or otherwise changed.
"""
import bpy
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[4]
OUTPUT = PROJECT_ROOT / 'Assets/Nakatetsu/Train/Series10000/Models'
OUTPUT.mkdir(parents=True, exist_ok=True)
original_scene = bpy.context.window.scene
try:
    for car in ('Tc1', 'Mp', 'M', 'T'):
        scene = bpy.data.scenes['Series10000_' + car]
        bpy.context.window.scene = scene
        for obj in scene.objects:
            obj.select_set(obj.type in {'MESH', 'EMPTY'})
        bpy.context.view_layer.objects.active = scene.objects[car]
        bpy.ops.export_scene.fbx(
            filepath=str(OUTPUT / (car + '.fbx')),
            use_selection=True,
            object_types={'MESH', 'EMPTY'},
            apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS',
            bake_space_transform=True,
            use_mesh_modifiers=True,
            mesh_smooth_type='OFF',
            use_tspace=True,
            use_triangles=True,
            bake_anim=False,
            axis_forward='-Z',
            axis_up='Y',
            path_mode='COPY',
            embed_textures=True,
        )
        print('EXPORTED', car, flush=True)
finally:
    bpy.context.window.scene = original_scene
