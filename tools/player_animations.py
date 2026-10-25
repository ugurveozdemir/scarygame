"""Makes the player's first-person animations of handling props, posing the body, the arm, the view and the prop
together frame by frame, so a hand never slips off what it holds:
  locker_hide:    the right hand pulls the locker door open by its handle, the player steps in turning around,
                  hooks their fingers into a vent slit and pulls the door shut while backing in.
  locker_exit:    the right hand pushes the locker door open flat-handed and the player steps out after it.
  battery_pickup: the player squats, picks a battery up off the floor, glances at it and pockets it.
  bed_hide:       the player drops flat alongside a bed, slides sideways under it in one go pushing off the
                  floor with the left hand (the right one holds the camera) and turns their head to watch the room.

Usage (Blender 5):
  Blender --background --factory-startup --python tools/player_animations.py -- export <move>
      writes props/<prop>/<move>.glb, which the game plays (interaction/PlayerAnimation.cs)
  Blender --background --factory-startup --python tools/player_animations.py -- render <move> <dir> <view> <frames>
      renders preview frames; view is fp (first person) or one of the move's debug views; frames is a list
      like 0,19,40 or all

Coordinates are Blender's (Z up); Godot's x, y, z is Blender's x, -z, y. Each move is made in its prop's space:
the prop sits at the origin as in its Godot scene.
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parent.parent
FBX = str(ROOT / "assets/rocketbox/male_adult_12/Male_Adult_12.fbx")
TEX = str(ROOT / "assets/rocketbox/male_adult_12") + "/"

FPS = 30

# The camera sits in the middle of the head, which is hidden, and the game puts its camera on the exported Eye.
# The body stands a little lower than the model so that point is at the game's head height
# (Player.StandingHeadHeight).
HEAD_CENTER = Vector((0.0, -0.02, 1.67))
HEAD_HEIGHT = 1.6

# Locker: the door's hinge and the pull bar on its free edge (door-local), mirroring props/locker/locker.tscn.
HINGE = Vector((0.32, 0.29, 0.0))
BAR_CENTER = Vector((-0.58, 0.0525, 1.25))
BAR_RADIUS = 0.01
PULL_DEG = -30.0
OPEN_DEG = -80.0

# Battery: a cylinder standing on the floor, mirroring props/battery/battery.tscn.
BATTERY_RADIUS = 0.03
BATTERY_HEIGHT = 0.1

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = args[0] if args else "export"
MOVE = args[1] if len(args) > 1 else "locker_hide"
OUT = args[2] if len(args) > 2 else ""
CAM = args[3] if len(args) > 3 else "fp"
FRAMES = args[4] if len(args) > 4 else "0"


def smooth(a, b, t):
    """0 before a, 1 after b, smoothstep in between."""
    if b <= a:
        return 1.0 if t >= b else 0.0
    x = min(max((t - a) / (b - a), 0.0), 1.0)
    return x * x * (3 - 2 * x)


def ease_out(a, b, t):
    x = min(max((t - a) / (b - a), 0.0), 1.0)
    return 1 - (1 - x) ** 3


def lerp(a, b, t):
    return a + (b - a) * t


def catmull(points, t):
    """Catmull-Rom through (time, Vector) points."""
    times = [p[0] for p in points]
    if t <= times[0]:
        return points[0][1].copy()
    if t >= times[-1]:
        return points[-1][1].copy()
    i = max(k for k in range(len(times) - 1) if times[k] <= t)
    p0 = points[max(i - 1, 0)][1]
    p1, p2 = points[i][1], points[i + 1][1]
    p3 = points[min(i + 2, len(points) - 1)][1]
    u = (t - times[i]) / (times[i + 1] - times[i])
    u = u * u * (3 - 2 * u)
    return 0.5 * ((2 * p1) + (-p0 + p2) * u + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u
                  + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u)


# ---------------------------------------------------------------- scene

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)


def material(name, color, metallic=0.0, roughness=0.6):
    m = bpy.data.materials.new(name)
    bsdf = m.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (*color, 1)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    return m


def box(name, center, size, mat, parent=None):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o = bpy.context.object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    o.data.materials.append(mat)
    if parent:
        attach(o, parent)
    return o


def empty(name, location):
    bpy.ops.object.empty_add(location=location)
    o = bpy.context.object
    o.name = name
    return o


def attach(child, parent):
    """Parents keeping the child where it is in the world."""
    child.parent = parent
    child.matrix_parent_inverse = parent.matrix_world.inverted()


def subtract(target, cutter):
    mod = target.modifiers.new("cut", "BOOLEAN")
    mod.operation = "DIFFERENCE"
    mod.object = cutter
    cutter.hide_render = True
    cutter.hide_viewport = True


def build_room(walls, lights):
    floor = material("floor", (0.18, 0.18, 0.17), roughness=0.8)
    box("Floor", (0, 0.5, -0.05), (4, 4, 0.1), floor)
    if walls:
        wall = material("wall", (0.32, 0.33, 0.30), roughness=0.9)
        box("BackWall", (0, -0.35, 1.5), (4, 0.1, 3), wall)
        box("NeighbourL", (0.71, -0.0, 1.05), (0.7, 0.6, 2.1), material("metal2", (0.22, 0.27, 0.25), 0.6, 0.5))
        box("NeighbourR", (-0.71, -0.0, 1.05), (0.7, 0.6, 2.1), material("metal3", (0.22, 0.27, 0.25), 0.6, 0.5))

    for location, energy, color in lights:
        bpy.ops.object.light_add(type="POINT", location=location)
        bpy.context.object.data.energy = energy
        bpy.context.object.data.color = color
    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.02, 0.025, 1)


def build_locker():
    """Returns the door: an empty on the hinge, exported; its geometry is only for previews."""
    metal = material("locker", (0.25, 0.30, 0.28), 0.6, 0.45)
    handle = material("handle", (0.05, 0.05, 0.05), 0.8, 0.35)
    body = box("LockerBody", (0, 0, 1.05), (0.7, 0.6, 2.1), metal)
    subtract(body, box("Inner", (0, 0, 1.05), (0.64, 0.54, 2.04), metal))
    subtract(body, box("Front", (0, 0.3, 1.05), (0.62, 0.1, 2.0), metal))

    door = empty("Door", HINGE)
    sheet = box("Sheet", HINGE + Vector((-0.32, 0, 1.05)), (0.64, 0.025, 2.02), metal, door)
    for i, z in enumerate((1.4, 1.52, 1.64, 1.76)):
        subtract(sheet, box(f"Slit{i}", HINGE + Vector((-0.32, 0, z)), (0.5, 0.1, 0.06), metal, door))
    # A pull bar standing off the door face, with a gap behind it for the fingers.
    bar = HINGE + BAR_CENTER
    bpy.ops.mesh.primitive_cylinder_add(radius=BAR_RADIUS, depth=0.17, location=bar)
    bpy.context.object.data.materials.append(handle)
    attach(bpy.context.object, door)
    for dz in (-0.075, 0.075):
        box("Standoff", bar + Vector((0, -0.025, dz)), (0.016, 0.05, 0.016), handle, door)
    return door


def build_battery():
    """Returns the battery: an empty at its centre, exported; its mesh is only for previews."""
    battery = empty("Battery", (0, 0, BATTERY_HEIGHT / 2))
    battery.rotation_mode = "QUATERNION"  # it tumbles freely in the hand
    bpy.ops.mesh.primitive_cylinder_add(radius=BATTERY_RADIUS, depth=BATTERY_HEIGHT, location=battery.location)
    bpy.context.object.data.materials.append(material("battery", (0.8, 0.6, 0.1), 0.6, 0.4))
    attach(bpy.context.object, battery)
    return battery


def build_bed():
    """The bed of props/furniture/bed.tscn; it doesn't move, so nothing of it is exported."""
    frame = material("bed_frame", (0.55, 0.57, 0.6), 0.7, 0.4)
    linen = material("linen", (0.7, 0.7, 0.66), roughness=0.9)
    box("Base", (0, 0, 0.41), (0.94, 2.0, 0.06), frame)
    box("Mattress", (0, 0, 0.52), (0.88, 1.92, 0.16), linen)
    box("Pillow", (0.03, 0.72, 0.65), (0.6, 0.35, 0.1), linen)
    for x in (-0.45, 0.45):
        for y, height in ((0.98, 1.0), (-0.98, 0.75)):
            bpy.ops.mesh.primitive_cylinder_add(radius=0.022, depth=height, location=(x, y, height / 2))
            bpy.context.object.data.materials.append(frame)
    box("FarWall", (-2.6, 0, 1.5), (0.1, 6, 3), material("wall", (0.32, 0.33, 0.30), roughness=0.9))


def build_player(hidden_bones):
    root = empty("PlayerRoot", (0, 0, 0))

    bpy.ops.import_scene.fbx(filepath=FBX)
    arm = next(o for o in bpy.context.selected_objects if o.type == "ARMATURE")
    for o in bpy.context.selected_objects:
        if o.type == "MESH":
            skin_material(o)
    # The model comes with a take that would hold the armature in place.
    arm.animation_data_clear()
    arm.parent = root
    arm.location.z -= HEAD_CENTER.z - HEAD_HEIGHT
    # Shrunk as HideBonesModifier does in the game.
    for name in hidden_bones:
        arm.pose.bones[name].scale = (0.001, 0.001, 0.001)

    # The eye carries the view; the game reads it from the exported animation.
    eye_at = Vector((HEAD_CENTER.x, HEAD_CENTER.y, HEAD_HEIGHT))
    eye = empty("Eye", eye_at)
    eye.parent = root
    eye.location = eye_at

    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.name = "FirstPerson"
    cam.parent = eye
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(75)
    cam.data.clip_start = 0.02
    return root, arm, eye, cam


def skin_material(mesh):
    for m in mesh.data.materials:
        nodes = m.node_tree.nodes
        bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
        img = nodes.new("ShaderNodeTexImage")
        img.image = bpy.data.images.load(TEX + "body_color.jpg")
        m.node_tree.links.new(img.outputs["Color"], bsdf.inputs["Base Color"])
        nrm_img = nodes.new("ShaderNodeTexImage")
        nrm_img.image = bpy.data.images.load(TEX + "body_normal.jpg")
        nrm_img.image.colorspace_settings.name = "Non-Color"
        nrm = nodes.new("ShaderNodeNormalMap")
        m.node_tree.links.new(nrm_img.outputs["Color"], nrm.inputs["Color"])
        m.node_tree.links.new(nrm.outputs["Normal"], bsdf.inputs["Normal"])
        bsdf.inputs["Roughness"].default_value = 0.6
        # Like Godot, don't draw faces seen from behind, so the view never shows the inside of the body.
        m.use_backface_culling = True


# ---------------------------------------------------------------- body posing (armature space)

def pose_posture(arm_obj, root, drop, back, bend, lean):
    """
    Lowers the body: the hips drop by `drop` and move back by `back` (metres), the whole body tips forward around
    the hips by `lean` degrees (90 is lying face down) and the back bends forward by `bend` degrees, split between
    the waist and the chest.
    """
    pb = arm_obj.pose.bones
    names = ("Bip01 Pelvis", "Bip01 Spine", "Bip01 Spine2")
    for name in names:
        pb[name].matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()

    to_arm = arm_obj.matrix_world.inverted().to_3x3()
    body = root.matrix_world.to_3x3().normalized()
    # Tipping forward turns the top towards the body's front (-Y) around its left side (+X).
    axis = (to_arm.normalized() @ (body @ Vector((1, 0, 0)))).normalized()
    pelvis = pb["Bip01 Pelvis"]
    hips = pelvis.head.copy()
    tip = Matrix.Translation(hips) @ Matrix.Rotation(math.radians(lean), 4, axis) @ Matrix.Translation(-hips)
    pelvis.matrix = Matrix.Translation(to_arm @ (body @ Vector((0, back, -drop)))) @ tip @ pelvis.matrix
    bpy.context.view_layer.update()

    for name, share in (("Bip01 Spine", 0.6), ("Bip01 Spine2", 0.4)):
        bone = pb[name]
        head = bone.head.copy()
        turn = Matrix.Translation(head) @ Matrix.Rotation(math.radians(bend * share), 4, axis) @ Matrix.Translation(-head)
        bone.matrix = turn @ bone.matrix
        bpy.context.view_layer.update()


def pose_head(arm_obj, root, lift):
    """Tips the head back by `lift` degrees around its base; returns the head bone in world space."""
    bone = arm_obj.pose.bones["Bip01 Head"]
    scale = bone.scale.copy()
    bone.matrix_basis = Matrix.Identity(4)
    bone.scale = scale
    bpy.context.view_layer.update()
    to_arm = arm_obj.matrix_world.inverted().to_3x3()
    axis = (to_arm.normalized() @ (root.matrix_world.to_3x3().normalized() @ Vector((1, 0, 0)))).normalized()
    base = bone.head.copy()
    bone.matrix = Matrix.Translation(base) @ Matrix.Rotation(math.radians(-lift), 4, axis) @ Matrix.Translation(-base) @ bone.matrix
    bpy.context.view_layer.update()
    return arm_obj.matrix_world @ bone.matrix


class Arm:
    def __init__(self, arm, side):
        self.arm = arm
        self.side = side
        p = f"Bip01 {side} "
        self.names = dict(upper=p + "UpperArm", fore=p + "Forearm", hand=p + "Hand")
        self.fingers = [[p + f"Finger{i}{j}" for j in ("", "1", "2")] for i in range(5)]
        bones = arm.data.bones
        h = lambda n: bones[n].head_local.copy()
        self.rest_shoulder = h(self.names["upper"])
        self.rest_elbow = h(self.names["fore"])
        self.rest_wrist = h(self.names["hand"])
        knuckles = sum((h(f[0]) for f in self.fingers[1:]), Vector()) / 4
        fingers = (knuckles - self.rest_wrist).normalized()
        across = (h(self.fingers[1][0]) - h(self.fingers[4][0])).normalized()  # towards the thumb
        normal = across.cross(fingers) if side == "R" else fingers.cross(across)
        normal.normalize()
        across = fingers.cross(normal) if side == "R" else normal.cross(fingers)
        self.rest_frame = Matrix((fingers, normal, across)).transposed()
        # The point that touches a grip: the middle of the knuckles, on the palm's surface.
        self.grip_point = knuckles + normal * 0.012 * 100
        self.rest_hand = bones[self.names["hand"]].matrix_local.copy()
        self.curl_axis = {}
        self.spread_axis = {}
        for i, chain in enumerate(self.fingers):
            for n in chain:
                local = bones[n].matrix_local.to_3x3().inverted()
                axis = across if i else fingers  # the thumb swings across the palm instead
                self.curl_axis[n] = (local @ axis).normalized()
                self.spread_axis[n] = (local @ normal).normalized()

    @property
    def shoulder(self):
        """Where the shoulder is now, with the body's posture."""
        return self.arm.pose.bones[self.names["upper"]].head.copy()

    def hand_matrix(self):
        """The posed hand bone, in armature space."""
        return self.arm.pose.bones[self.names["hand"]].matrix.copy()

    def hand_rotation(self, fingers, palm):
        """Armature-space rotation turning the rest hand so its fingers and palm face the given directions."""
        fingers = fingers.normalized()
        palm = (palm - fingers * palm.dot(fingers)).normalized()
        across = fingers.cross(palm) if self.side == "R" else palm.cross(fingers)
        frame = Matrix((fingers, palm, across)).transposed()
        return frame @ self.rest_frame.transposed()

    def wrist_for_grip(self, rotation, grip):
        """Where the wrist goes so that the hand's grip point lands on `grip`."""
        return grip - rotation @ (self.grip_point - self.rest_wrist)

    def pose(self, wrist, rotation, pole, curl, thumb, grip):
        pb = self.arm.pose.bones
        s, e0, w0 = self.shoulder, self.rest_elbow, self.rest_wrist
        upper_len = (e0 - self.rest_shoulder).length
        fore_len = (w0 - e0).length
        to_wrist = wrist - s
        d = min(to_wrist.length, (upper_len + fore_len) * 0.999)
        dir_ = to_wrist.normalized()
        # Elbow on the side of the pole, law of cosines.
        a = (upper_len ** 2 - fore_len ** 2 + d ** 2) / (2 * d)
        hgt = math.sqrt(max(upper_len ** 2 - a ** 2, 0))
        bend = (pole - s)
        bend = (bend - dir_ * bend.dot(dir_)).normalized()
        elbow = s + dir_ * a + bend * hgt
        wrist = s + dir_ * d

        rest_bend = self._bend(self.rest_shoulder, e0, w0)
        self._aim(self.names["upper"], s, e0 - self.rest_shoulder, rest_bend, elbow - s, bend)
        bpy.context.view_layer.update()
        self._aim(self.names["fore"], elbow, w0 - e0, rest_bend, wrist - elbow, bend)
        bpy.context.view_layer.update()
        hand = Matrix.Translation(wrist) @ (rotation @ self.rest_hand.to_3x3()).to_4x4()
        pb[self.names["hand"]].matrix = hand
        bpy.context.view_layer.update()
        self._curl(curl, thumb, grip)

    @staticmethod
    def _bend(s, e, w):
        d = (w - s).normalized()
        v = e - s
        return (v - d * v.dot(d)).normalized()

    def _aim(self, name, head, rest_dir, rest_bend, new_dir, new_bend):
        bone = self.arm.data.bones[name]
        r0 = Matrix((rest_dir.normalized(), rest_bend, rest_dir.normalized().cross(rest_bend))).transposed()
        nd = new_dir.normalized()
        nb = (new_bend - nd * new_bend.dot(nd)).normalized()
        r1 = Matrix((nd, nb, nd.cross(nb))).transposed()
        rot = r1 @ r0.transposed()
        self.arm.pose.bones[name].matrix = Matrix.Translation(head) @ (rot @ bone.matrix_local.to_3x3()).to_4x4()

    def _curl(self, curl, thumb, grip):
        """Bends the fingers `curl` (the thumb `thumb`) of the way into `grip`'s pose and spreads them as it says."""
        sign = 1 if self.side == "R" else -1
        for i, chain in enumerate(self.fingers):
            amount = thumb if i == 0 else curl
            for j, (n, deg) in enumerate(zip(chain, grip.curl[i])):
                pb = self.arm.pose.bones[n]
                pb.rotation_mode = "QUATERNION"
                bend = Quaternion(self.curl_axis[n], sign * math.radians(deg) * amount)
                spread = Quaternion(self.spread_axis[n], sign * math.radians(grip.spread[i]) if j == 0 else 0.0)
                pb.rotation_quaternion = spread @ bend


# ---------------------------------------------------------------- grips and hand motion

class Grip:
    """Where and how a hand holds something, in that thing's space."""

    def __init__(self, point, fingers, palm, approach, curl, spread=(0, 0, 0, 0, 0)):
        self.point = Vector(point)
        self.fingers = Vector(fingers)
        self.palm = Vector(palm)
        self.approach = Vector(approach)  # offset of the open hand just before it closes
        # Degrees per joint (knuckle, middle, tip) of each finger, thumb first, when closed.
        self.curl = curl
        # Degrees each finger turns sideways at the knuckle, thumb first.
        self.spread = spread


# Locker, outside: fingers into the gap behind the bar, palm facing the hinge.
BAR = Grip(BAR_CENTER + Vector((-0.012, 0.004, 0)), (0, -1, 0), (1, 0, 0), (-0.05, 0.07, -0.02),
           ((25, 20, 25), (75, 95, 55), (80, 100, 55), (80, 100, 55), (80, 95, 50)))
# Locker, inside: fingers through the lowest vent slit, palm down, hooked over its edge.
SLIT = Grip((-0.22, -0.0175, 1.393), (0, 1, 0), (0, 0, -1), (0.0, -0.07, -0.03),
            ((10, 10, 10), (15, 65, 50), (15, 70, 50), (15, 70, 50), (15, 65, 45)))
# Locker, inside: a flat hand on the door near its free edge, fingers up over the slits, thumb clear of the door,
# pushing it open. The palm's heel is thicker than the knuckles, so the grip point stays off the door.
PUSH = Grip((-0.42, -0.03, 1.50), (0, 0, 1), (0, 1, 0), (0.0, -0.08, -0.04),
            ((-25, -10, 0), (-8, -4, 0), (-8, -4, 0), (-8, -4, 0), (-8, -4, 0)))
# Battery (space of its centre): a claw from above, fingers over its far side, thumb on the near side.
CLAW = Grip((0.0, 0.0, BATTERY_HEIGHT / 2 + 0.012), (0, -1, -0.35), (0, 0, -1), (0.0, 0.05, 0.08),
            ((35, 30, 20), (45, 70, 40), (45, 75, 40), (45, 75, 40), (45, 70, 40)))

# A hand flat on the floor; only its finger pose is used.
FLAT = Grip((0, 0, 0), (0, -1, 0), (0, 0, -1), (0, 0, 0),
            ((10, 0, 0), (5, 10, 5), (5, 10, 5), (5, 10, 5), (5, 10, 5)),
            (-25, -12, -2, 8, 18))

# How a hand hangs at the side, relative to its shoulder, in the player's space.
HANG = Vector((-0.03, 0.0, -0.52))


class Frame:
    """The scene posed for one frame: helpers to place the right hand relative to the player and a prop."""

    def __init__(self, move, arm_obj, right, left, root, f):
        self.move = move
        self.right = right
        self.f = f
        self.to_arm = arm_obj.matrix_world.inverted()
        self.rot_to_arm = self.to_arm.to_3x3().normalized()
        self.root = root.matrix_world.copy()
        self.rest = right.shoulder + self.heading(*HANG) * 100
        self.rest_rot = right.hand_rotation(self.heading(0, 0, -1), self.heading(1, 0, 0))
        self.left_rest = left.shoulder + self.heading(-HANG.x, HANG.y, HANG.z) * 100
        self.left_rest_rot = left.hand_rotation(self.heading(0, 0, -1), self.heading(-1, 0, 0))

    def body(self, x, y, z):
        """A point in the player's space (facing -Y), in armature space."""
        return self.to_arm @ (self.root @ Vector((x, y, z)))

    def heading(self, x, y, z):
        """A direction in the player's space, in armature space."""
        return self.rot_to_arm @ (self.root.to_3x3().normalized() @ Vector((x, y, z)))

    def hold(self, grip, frame=None, approach=0.0):
        """Wrist and hand rotation holding `grip` with the prop as it is at `frame` (this one by default)."""
        m = self.move.grip_space(grip, self.f if frame is None else frame)
        to_arm = self.rot_to_arm @ m.to_3x3().normalized()
        rot = self.right.hand_rotation(to_arm @ grip.fingers, to_arm @ grip.palm)
        at = self.to_arm @ (m @ (grip.point + grip.approach * approach))
        return self.right.wrist_for_grip(rot, at), rot

    @staticmethod
    def turn(r0, r1, t):
        return r0.to_quaternion().slerp(r1.to_quaternion(), t).to_matrix()

    def on_floor(self, arm, x, y, lift=0.0):
        """Wrist and rotation of `arm`'s hand lying flat on the floor at (x, y) in the player's space, fingers forward."""
        rot = arm.hand_rotation(self.heading(0, -1, 0), self.heading(0, 0, -1))
        return arm.wrist_for_grip(rot, self.body(x, y, 0.004 + lift)), rot

    def reach(self, grip, start, arrive, via):
        """Brings the hand from the side up to `grip` between frames `start` and `arrive`, opening then closing it."""
        f = self.f
        at, rot = self.hold(grip)
        pre, _ = self.hold(grip, approach=1.0)
        span = arrive - start
        wrist = catmull([(start, self.rest), (start + span * 0.45, self.body(*via)), (arrive - span * 0.25, pre),
                         (arrive, at)], f)
        rot = self.turn(self.rest_rot, rot, smooth(start, arrive - span * 0.25, f))
        curl = lerp(0.05, -0.15, smooth(start, start + span * 0.6, f)) + 1.15 * smooth(arrive - 3, arrive + 2, f)
        return wrist, rot, curl

    def release(self, grip, frame, back, done):
        """Lets go of `grip` at `frame` and drops the hand out of view by `done`, first moving it by `back`."""
        f = self.f
        at, rot = self.hold(grip, frame)
        away = at + self.heading(*back) * 100
        wrist = catmull([(frame, at), (frame + (done - frame) * 0.35, away), (done, self.rest)], f)
        rot = self.turn(rot, self.rest_rot, smooth(frame + 2, done, f))
        curl = lerp(1.0, -0.1, smooth(frame, frame + 4, f)) + 0.15 * smooth(frame + 6, done + 1, f)
        return wrist, rot, curl


def door_matrix(angle):
    return Matrix.Translation(HINGE) @ Matrix.Rotation(math.radians(angle), 4, "Z")


# ---------------------------------------------------------------- moves

WARM = (1.0, 0.92, 0.8)
COOL = (0.75, 0.85, 1.0)


class Move:
    """A move's timeline. Positions and yaw are the player's root in the prop's space; the player faces -Y at yaw 0."""
    end = 0
    walls = False
    # Bones shrunk away: the game hides the head and never shows the legs.
    hidden_bones = ("Bip01 Head", "Bip01 L Thigh", "Bip01 R Thigh")
    # Preview lights: location, energy, colour; and whether a light on the camera stands in for the camcorder's
    # night vision in the dark.
    lights = (((-0.6, 1.6, 2.6), 120, WARM), ((0.8, 1.2, 1.4), 25, COOL))
    camera_light = False
    debug_views = {}

    @staticmethod
    def posture(f):
        """Hip drop and back shift in metres, forward bend and whole-body lean in degrees."""
        return 0.0, 0.0, 0.0, 0.0

    @staticmethod
    def head_yaw(f):
        """How far the head turns left from the body, in degrees."""
        return 0.0

    @staticmethod
    def head_up(f):
        """How far the head tips back from the body, in degrees, like to look ahead while lying face down."""
        return 0.0

    @staticmethod
    def elbow(fr):
        return fr.body(-0.6, 0.15, 0.6)

    @staticmethod
    def left_hand(fr, left):
        """Wrist, rotation, elbow pole, finger curl and finger pose of the left hand, hanging at the side by default."""
        return fr.left_rest, fr.left_rest_rot, fr.body(0.3, 0.6, 1.0), 0.25, BAR

    def pose_prop(self, f, fr):
        """Poses the prop's animated parts after the hand is posed."""


class LockerMove(Move):
    prop = "locker"
    walls = True
    debug_views = {
        # name: (location, look at, lens, hidden objects)
        "tp": ((-1.25, 0.75, 1.55), (-0.3, 0.55, 1.25), 28, ("NeighbourR",)),
        "close": ((-0.85, 0.55, 1.45), (-0.26, 0.36, 1.25), 35, ("NeighbourR",)),
        "hand": ((-0.55, 0.75, 1.42), (-0.24, 0.36, 1.25), 60, ("NeighbourR",)),
        "hand2": ((0.05, 0.85, 1.30), (-0.26, 0.36, 1.25), 60, ("NeighbourR",)),
        "top": ((-0.25, 0.55, 2.3), (-0.25, 0.5, 1.2), 24, ("NeighbourR",)),
        "side": ((1.1, 0.35, 1.55), (0.0, 0.15, 1.3), 30, ("LockerBody", "NeighbourL")),
        "inside": ((-0.45, 0.1, 1.75), (0.1, 0.25, 1.35), 30, ("LockerBody", "NeighbourR")),
    }

    def __init__(self):
        self.door = build_locker()
        self.parts = (self.door,)

    def grip_space(self, grip, f):
        return door_matrix(self.door_angle(f))

    def pose_prop(self, f, fr):
        self.door.rotation_euler.z = math.radians(self.door_angle(f))

    def key_prop(self, f):
        self.door.keyframe_insert("rotation_euler", frame=f)


class LockerHide(LockerMove):
    name = "LockerHide"
    end = 104
    release_bar = 33
    release_slit = 84
    approach = Vector((-0.2, 0.84, 0.0))
    pull = Vector((-0.25, 1.0, 0.0))
    # Stepping in: around the open door into the opening, then back against the locker's rear.
    midway = Vector((-0.12, 0.62, 0.0))
    entry = Vector((0.02, 0.24, 0.0))
    hide = Vector((0.0, 0.0, 0.03))

    @staticmethod
    def door_angle(f):
        pull = smooth(19, 31, f) * PULL_DEG
        swing = ease_out(31, 48, f) * (OPEN_DEG - PULL_DEG)
        close = smooth(60, 76, f) * -OPEN_DEG
        return pull + swing + close

    @staticmethod
    def pitch(f):
        """Looks at whatever the hand is reaching for, and out through the slits once the door is shut."""
        pitch = lerp(-15.0, -40.0, smooth(2, 15, f))
        pitch = lerp(pitch, -26.0, smooth(21, 33, f))
        pitch = lerp(pitch, -12.0, smooth(31, 46, f))
        pitch = lerp(pitch, -30.0, smooth(48, 57, f))
        return lerp(pitch, -8.0, smooth(72, 90, f))

    def position(self, f):
        if f < 33:
            return self.approach.lerp(self.pull, smooth(19, 33, f))
        if f < 58:
            return catmull([(33, self.pull), (45, self.midway), (58, self.entry)], f)
        return self.entry.lerp(self.hide, smooth(58, 74, f))

    @staticmethod
    def yaw(f):
        """Turns around towards the hinge side while stepping in, to face out of the locker."""
        return 180.0 * smooth(36, 56, f)

    @staticmethod
    def elbow(fr):
        """Outside the elbow swings out; inside the locker it stays down, clear of the door."""
        return fr.body(-0.6, 0.15, 0.6).lerp(fr.body(-0.25, 0.05, 0.4), smooth(40, 50, fr.f))

    def hand(self, fr):
        f = fr.f
        if f < self.release_bar:
            if f < 21:
                return BAR, *fr.reach(BAR, 4, 19, (-0.30, -0.25, 1.05))
            return BAR, *fr.hold(BAR), 1.0
        if f < 47:
            return BAR, *fr.release(BAR, self.release_bar, (-0.02, 0.06, -0.12), 47)
        if f < self.release_slit:
            # Reach out for the open door's vent and pull it shut while backing in.
            if f < 60:
                return SLIT, *fr.reach(SLIT, 47, 58, (-0.28, -0.15, 1.0))
            return SLIT, *fr.hold(SLIT), 1.0
        return SLIT, *fr.release(SLIT, self.release_slit, (0.0, 0.08, -0.08), 96)


class LockerExit(LockerMove):
    name = "LockerExit"
    end = 44
    release_push = 20
    hide = LockerHide.hide
    # Stepping out: through the free-edge side of the opening, clear of the door, then to the game's ExitPoint.
    doorway = Vector((-0.08, 0.42, 0.01))
    exit = Vector((0.0, 0.8, 0.0))

    @staticmethod
    def door_angle(f):
        push = smooth(9, 20, f) * -45.0
        swing = ease_out(20, 30, f) * (OPEN_DEG + 45.0)
        return push + swing

    @staticmethod
    def pitch(f):
        """Glances down at the hand on the door, then looks ahead while stepping out."""
        pitch = lerp(-8.0, -14.0, smooth(1, 9, f))
        return lerp(pitch, 0.0, smooth(14, 32, f))

    def position(self, f):
        return catmull([(11, self.hide), (26, self.doorway), (36, self.exit)], f)

    @staticmethod
    def yaw(f):
        return 180.0

    @staticmethod
    def elbow(fr):
        return fr.body(-0.25, 0.05, 0.4)

    def hand(self, fr):
        f = fr.f
        if f < 11:
            return PUSH, *fr.reach(PUSH, 0, 9, (-0.25, -0.12, 1.05))
        if f < self.release_push:
            return PUSH, *fr.hold(PUSH), 1.0
        return PUSH, *fr.release(PUSH, self.release_push, (0.0, 0.06, -0.10), 30)


class BatteryPickup(Move):
    """Squats over the battery, picks it up, glances at it on the way up and drops it into a pocket."""
    name = "BatteryPickup"
    prop = "battery"
    end = 46
    grab = 19
    # Standing behind the battery, which is a little to the right.
    start = Vector((0.12, 0.48, 0.0))
    lights = (((0.5, -0.4, 2.2), 150, WARM), ((-0.8, 1.2, 1.2), 25, COOL))
    debug_views = {
        "side": ((-1.3, 0.3, 0.9), (0.0, 0.35, 0.55), 30, ()),
        "front": ((0.3, -1.1, 0.9), (0.05, 0.3, 0.5), 30, ()),
        "close": ((-0.45, -0.1, 0.45), (0.0, 0.1, 0.1), 45, ()),
    }

    def __init__(self):
        self.battery = build_battery()
        self.parts = (self.battery,)
        self.rest_matrix = self.battery.matrix_world.copy()
        self.held = None  # the battery relative to the hand, once grabbed

    def grip_space(self, grip, f):
        return self.rest_matrix

    @staticmethod
    def posture(f):
        squat = smooth(2, 15, f) * (1 - smooth(22, 36, f))
        return 0.63 * squat, 0.05 * squat, 62.0 * squat, 0.0

    @staticmethod
    def pitch(f):
        """Looks down at the battery, at it in the hand on the way up, then ahead."""
        pitch = lerp(-30.0, -72.0, smooth(1, 14, f))
        pitch = lerp(pitch, -28.0, smooth(22, 32, f))
        return lerp(pitch, -5.0, smooth(36, 46, f))

    def position(self, f):
        return self.start

    @staticmethod
    def yaw(f):
        return 0.0

    def hand(self, fr):
        f = fr.f
        if f < self.grab + 2:
            return CLAW, *fr.reach(CLAW, 2, self.grab, (-0.22, -0.2, 0.55))
        # Lift it to the chest for a glance, then drop it into the right trouser pocket.
        _, grab_rot = fr.hold(CLAW)
        show_rot = fr.right.hand_rotation(fr.heading(0, -1, 0.15), fr.heading(0.35, 0, -1))
        held, _ = fr.hold(CLAW, self.grab)
        pocket = fr.body(-0.22, -0.02, 0.86)
        wrist = catmull([(self.grab + 2, held), (31, fr.body(-0.12, -0.34, 1.22)), (36, fr.body(-0.14, -0.3, 1.2)),
                         (self.end - 2, pocket)], f)
        rot = fr.turn(grab_rot, show_rot, smooth(self.grab + 4, 30, f))
        rot = fr.turn(rot, fr.rest_rot, smooth(36, self.end - 2, f))
        return CLAW, wrist, rot, 1.0

    @staticmethod
    def elbow(fr):
        return fr.body(-0.6, 0.3, 0.3)

    def pose_prop(self, f, fr):
        hand = fr.to_arm.inverted() @ fr.right.hand_matrix()
        if f < self.grab + 1:
            self.held = None
            self.battery.matrix_world = self.rest_matrix
            return
        if self.held is None:
            self.held = hand.inverted() @ self.rest_matrix
        self.battery.matrix_world = hand @ self.held

    def key_prop(self, f):
        self.battery.keyframe_insert("location", frame=f)
        self.battery.keyframe_insert("rotation_quaternion", frame=f)


class BedHide(Move):
    """
    Drops flat on the floor alongside the bed, slides sideways under it in one go pushing off the floor with the
    left hand, and turns the head to watch the room. The right hand holds the camera, so it is never seen.
    The bed's open side is -X and its head end +Y.
    """
    name = "BedHide"
    prop = "furniture"
    end = 46
    hidden_bones = Move.hidden_bones + ("Bip01 R UpperArm",)
    lights = (((-1.4, -0.6, 2.4), 150, WARM), ((-0.6, 1.4, 0.6), 30, COOL))
    camera_light = True
    debug_views = {
        "side": ((-1.2, -2.4, 1.1), (-0.5, 0.0, 0.3), 28, ()),
        "top": ((-0.5, 0.0, 3.2), (-0.5, 0.0, 0.2), 22, ("Mattress", "Base", "Pillow")),
        "hand": ((-1.5, 1.4, 0.5), (-0.8, 0.4, 0.05), 35, ()),
    }
    start = Vector((-0.9, 0.0, 0.0))
    # The hips lying beside the bed, head towards its head end, then under it.
    beside = Vector((-0.85, -0.45, 0.0))
    under = Vector((-0.1, -0.45, 0.0))
    slide = (16, 30)
    # Where the left hand pushes off, and where it lands again at the bed's edge once under.
    push = (Vector((-1.05, 0.55, 0.0)), Vector((-0.2, 1.0, 0.0)), 11, 21)
    edge = (Vector((-0.5, 0.5, 0.0)), Vector((-0.6, 1.0, 0.0)), 30)

    def __init__(self):
        build_bed()
        self.parts = ()

    @staticmethod
    def posture(f):
        """Drops to the knees and straight down onto the belly."""
        keys = [(0, Vector((0, 0, 0, 0))), (2, Vector((0, 0, 0, 0))), (7, Vector((0.4, 0.0, 20, 15))),
                (14, Vector((0.71, 0.0, 0, 90)))]
        return tuple(catmull(keys, f))

    @staticmethod
    def head_up(f):
        return 75.0 * smooth(7, 14, f)

    @staticmethod
    def head_yaw(f):
        return 90.0 * smooth(26, 38, f)

    @staticmethod
    def pitch(f):
        pitch = lerp(-25.0, -40.0, smooth(1, 8, f))
        pitch = lerp(pitch, -28.0, smooth(10, 16, f))
        return lerp(pitch, -22.0, smooth(30, 40, f))

    def position(self, f):
        if f < self.slide[0]:
            return catmull([(2, self.start), (14, self.beside)], f)
        return self.beside.lerp(self.under, smooth(*self.slide, f))

    @staticmethod
    def yaw(f):
        """Turns left from facing the bed to lying along it."""
        return 90.0 + 90.0 * smooth(2, 14, f)

    @staticmethod
    def on_floor(fr, arm, point, fingers):
        """Wrist and rotation of `arm`'s hand lying flat at `point`, fingers along `fingers`, in the bed's space."""
        rot = arm.hand_rotation(fr.rot_to_arm @ fingers.normalized(), fr.rot_to_arm @ Vector((0, 0, -1)))
        return arm.wrist_for_grip(rot, fr.to_arm @ (point + Vector((0, 0, 0.004)))), rot

    def hand(self, fr):
        # The right hand holds the camera; it is hidden.
        return BAR, fr.rest, fr.rest_rot, 0.2

    def left_hand(self, fr, left):
        f = fr.f
        push_at, push_rot = self.on_floor(fr, left, self.push[0], self.push[1])
        edge_at, edge_rot = self.on_floor(fr, left, self.edge[0], self.edge[1])
        planted, lifted_at = self.push[2], self.push[3]
        lift = fr.rot_to_arm @ Vector((0, 0, 0.07)) * 100
        if f < planted:
            wrist = catmull([(1, fr.left_rest), (6, fr.body(0.3, -0.4, 0.5)), (planted - 2, push_at + lift),
                             (planted, push_at)], f)
            rot = fr.turn(fr.left_rest_rot, push_rot, smooth(2, planted - 2, f))
        elif f < lifted_at:
            wrist, rot = push_at, push_rot
        else:
            # Lifts off as the body slides away and lands again at the bed's edge.
            wrist = catmull([(lifted_at, push_at), (lifted_at + 3, push_at + lift), (self.edge[2] - 2, edge_at + lift),
                             (self.edge[2], edge_at)], f)
            rot = fr.turn(push_rot, edge_rot, smooth(lifted_at, self.edge[2] - 1, f))
        # Lying down, the elbow rests on the floor out to the side.
        pole = fr.body(0.3, 0.6, 1.0).lerp(fr.body(0.6, 0.0, 0.1), smooth(4, 12, f))
        return wrist, rot, pole, 0.6 * smooth(6, planted, f), FLAT

    def key_prop(self, f):
        pass


MOVES = {
    "locker_hide": LockerHide,
    "locker_exit": LockerExit,
    "battery_pickup": BatteryPickup,
    "bed_hide": BedHide,
}


# ---------------------------------------------------------------- baking

def animate(move, arm_obj, right, left, root, eye, eye_in_head, f):
    bpy.context.scene.frame_set(f)
    root.location = move.position(f)
    root.rotation_euler.z = math.radians(move.yaw(f))
    bpy.context.view_layer.update()
    pose_posture(arm_obj, root, *move.posture(f))

    # The eye stays in the middle of the head, which tips back as the move asks.
    head = pose_head(arm_obj, root, move.head_up(f))
    eye.location = root.matrix_world.inverted() @ (head.translation + head.to_3x3().normalized() @ eye_in_head)
    eye.rotation_euler = (math.radians(90 + move.pitch(f)), 0, math.pi + math.radians(move.head_yaw(f)))
    bpy.context.view_layer.update()

    fr = Frame(move, arm_obj, right, left, root, f)
    grip, wrist, rot, curl = move.hand(fr)
    right.pose(wrist, rot, move.elbow(fr), curl, max(curl, 0) * 0.9, grip)
    wrist, rot, pole, curl, grip = move.left_hand(fr, left)
    left.pose(wrist, rot, pole, curl, 0.2, grip)
    move.pose_prop(f, fr)


def key_pose(move, arm_obj, root, eye, f):
    for pb in arm_obj.pose.bones:
        pb.keyframe_insert("location", frame=f)
        pb.keyframe_insert("rotation_quaternion", frame=f)
    root.keyframe_insert("location", frame=f)
    root.keyframe_insert("rotation_euler", frame=f)
    eye.keyframe_insert("location", frame=f)
    eye.keyframe_insert("rotation_euler", frame=f)
    move.key_prop(f)


def setup_render(cam):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = 1280
    sc.render.resolution_y = 720
    sc.render.fps = FPS
    sc.eevee.taa_render_samples = 16
    sc.camera = cam
    sc.render.image_settings.file_format = "PNG"
    sc.view_settings.view_transform = "AgX"


def export(move, arm_obj, root, eye):
    """Writes the player's body, eye and the prop's parts with their animation to the game's glb."""
    bpy.context.scene.name = move.name
    bpy.ops.object.select_all(action="DESELECT")
    for o in (root, arm_obj, eye, *move.parts, *arm_obj.children):
        o.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(ROOT / f"props/{move.prop}/{MOVE}.glb"),
        use_selection=True,
        export_animation_mode="SCENE",
        export_anim_scene_split_object=False,
        export_frame_range=True,
        export_force_sampling=True,
        export_image_format="NONE",
        export_cameras=False,
        export_lights=False,
    )


def main():
    clear_scene()
    move_type = MOVES[MOVE]
    build_room(move_type.walls, move_type.lights)
    move = move_type()
    root, arm_obj, eye, cam = build_player(move_type.hidden_bones)
    bpy.context.view_layer.update()
    # The eye rides in the head, so it follows the body when it bends.
    head = arm_obj.matrix_world @ arm_obj.pose.bones["Bip01 Head"].matrix
    eye_in_head = head.to_3x3().normalized().inverted() @ (eye.matrix_world.translation - head.translation)

    if CAM in move.debug_views:
        location, target, lens, hidden = move.debug_views[CAM]
        bpy.ops.object.camera_add(location=location)
        cam = bpy.context.object
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        for name in hidden:
            bpy.data.objects[name].hide_render = True
        bpy.ops.object.light_add(type="SUN", rotation=(0.6, 0.0, -2.2))
        bpy.context.object.data.energy = 3

    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = move.end
    right, left = Arm(arm_obj, "R"), Arm(arm_obj, "L")
    for f in range(0, move.end + 1):
        animate(move, arm_obj, right, left, root, eye, eye_in_head, f)
        key_pose(move, arm_obj, root, eye, f)

    if MODE == "export":
        export(move, arm_obj, root, eye)
        return

    if move.camera_light:
        bpy.ops.object.light_add(type="SPOT")
        light = bpy.context.object
        light.parent = cam
        light.data.energy = 8
        light.data.spot_size = math.radians(100)
        light.data.color = (0.8, 1.0, 0.85)
    setup_render(cam)
    frames = range(0, move.end + 1) if FRAMES == "all" else [int(x) for x in FRAMES.split(",")]
    for f in frames:
        bpy.context.scene.frame_set(f)
        bpy.context.scene.render.filepath = f"{OUT}/{CAM}_{f:03d}.png"
        bpy.ops.render.render(write_still=True)


main()
