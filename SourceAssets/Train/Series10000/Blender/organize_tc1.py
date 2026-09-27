"""Organize the saved Tc1 edits without changing the user's source blend.

Run with Blender --background <source blend> --python <this script>.
"""
import bpy
from pathlib import Path


SOURCE = Path(bpy.data.filepath)
DESTINATION = SOURCE.with_name("Series10000_UnityExport_Organized.blend")
PROJECT_ROOT = Path(__file__).resolve().parents[4]
scene = bpy.data.scenes["Series10000_Tc1"]
collection = bpy.data.collections["Tc1_Export"]
root = scene.objects["Tc1"]

radio_body = bpy.data.materials.new("Series10000_ProtectionRadio_Body")
radio_body.diffuse_color = (0.4433962, 0.4433962, 0.4433962, 1.0)
radio_body.use_nodes = True
body_shader = radio_body.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
body_shader.inputs["Base Color"].default_value = radio_body.diffuse_color
body_output = radio_body.node_tree.nodes.new("ShaderNodeOutputMaterial")
radio_body.node_tree.links.new(body_shader.outputs["BSDF"], body_output.inputs["Surface"])

radio_button = bpy.data.materials.new("Series10000_ProtectionRadio_Button")
radio_button.use_nodes = True
button_shader = radio_button.node_tree.nodes.new("ShaderNodeBsdfPrincipled")
button_output = radio_button.node_tree.nodes.new("ShaderNodeOutputMaterial")
radio_button.node_tree.links.new(button_shader.outputs["BSDF"], button_output.inputs["Surface"])
button_image = bpy.data.images.load(
    str(PROJECT_ROOT / "Assets/Nakatetsu/Train/Series10000/Textures/ProtectionRadio.png"),
    check_existing=True,
)
button_image.pack()
texture_node = radio_button.node_tree.nodes.new("ShaderNodeTexImage")
texture_node.image = button_image
radio_button.node_tree.links.new(
    texture_node.outputs["Color"],
    button_shader.inputs["Base Color"],
)

# Original FBX object names, mesh data, UVs and material slots stay unchanged.
additions = {
    "立方体": ("Tc1_CabAdded_01", "Series10000_CabUntextured"),
    "手置き": ("Tc1_Handrest", "Series10000_CabUntextured"),
    "立方体.001": ("Tc1_CabAdded_02", "Series10000_CabUntextured"),
    "防護無線_全面": ("Tc1_ProtectionRadio_Housing", "Series10000_ProtectionRadio_Body"),
    "防護無線_本体": ("Tc1_ProtectionRadio_Face", "Series10000_ProtectionRadio_Button"),
}

for old_name, (new_name, material_name) in additions.items():
    obj = scene.objects.get(old_name)
    if obj is None or obj.type != "MESH":
        raise RuntimeError(f"Tc1 mesh not found: {old_name}")
    if obj.data.materials:
        raise RuntimeError(f"Expected the new mesh to have no material: {old_name}")
    if bpy.data.objects.get(new_name) is not None:
        raise RuntimeError(f"Target object name already exists: {new_name}")

    world_matrix = obj.matrix_world.copy()
    if collection not in obj.users_collection:
        collection.objects.link(obj)
    for other_collection in tuple(obj.users_collection):
        if other_collection != collection:
            other_collection.objects.unlink(obj)
    obj.parent = root
    obj.matrix_world = world_matrix
    obj.name = new_name
    obj.data.materials.append(bpy.data.materials[material_name])

assert len(scene.objects) == 43
assert all(collection in scene.objects[name].users_collection for name, _ in additions.values())
bpy.context.window.scene = scene
bpy.ops.wm.save_as_mainfile(filepath=str(DESTINATION))
print(f"ORGANIZED {DESTINATION}", flush=True)
