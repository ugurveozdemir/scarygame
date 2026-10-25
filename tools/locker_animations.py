"""Makes the first-person animations of a locker, posing the arm, the view and the door together frame by frame,
so the hand never slips off the door:
  hide: the right hand pulls the door open by its handle, the player steps in turning around, hooks their
        fingers into a vent slit and pulls the door shut while backing in.
  exit: the right hand pushes the door open flat-handed and the player steps out after it.

Usage (Blender 5):
  Blender --background --factory-startup --python tools/locker_animations.py -- export <move>
      writes props/locker/locker_<move>.glb, which the game plays (interaction/PlayerAnimation.cs)
  Blender --background --factory-startup --python tools/locker_animations.py -- render <move> <dir> <view> <frames>
      renders preview frames; view is fp (first person) or a debug view: tp, close, hand, hand2, top, side,
      inside; frames is a list like 0,19,40 or all

Coordinates are Blender's (Z up). The locker sits at the origin with its front facing +Y and its hinge at +X,
mirroring props/locker/locker.tscn (Godot x, y, z is Blender x, -z, y).
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

# Player placement. The camera sits at the game's head height (Player.StandingHeadHeight), forward at the face,
# so the hidden head never shows; the game puts its camera on the exported Eye.
APPROACH = Vector((-0.2, 0.84, 0.0))
PULL = Vector((-0.25, 1.0, 0.0))
# Stepping in: around the open door into the opening, then back against the locker's rear.
MIDWAY = Vector((-0.12, 0.62, 0.0))
ENTRY = Vector((0.02, 0.24, 0.0))
HIDE = Vector((0.0, 0.0, 0.03))
# Stepping out: through the free-edge side of the opening, clear of the door, then to the game's ExitPoint.
DOORWAY = Vector((-0.08, 0.42, 0.01))
EXIT = Vector((0.0, 0.8, 0.0))
CAMERA_OFFSET = Vector((0.0, -0.08, 1.6))
PITCH_DEG = -15.0

# Door: hinge position, free-edge handle bar (door-local), how far the hand pulls before letting go.
HINGE = Vector((0.32, 0.29, 0.0))
BAR_CENTER = Vector((-0.58, 0.0525, 1.25))
BAR_RADIUS = 0.01
PULL_DEG = -30.0
OPEN_DEG = -80.0

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
MODE = args[0] if args else "export"
MOVE = args[1] if len(args) > 1 else "hide"
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


def build_room():
    wall = material("wall", (0.32, 0.33, 0.30), roughness=0.9)
    floor = material("floor", (0.18, 0.18, 0.17), roughness=0.8)
    box("Floor", (0, 0.5, -0.05), (4, 4, 0.1), floor)
    box("BackWall", (0, -0.35, 1.5), (4, 0.1, 3), wall)
    box("NeighbourL", (0.71, -0.0, 1.05), (0.7, 0.6, 2.1), material("metal2", (0.22, 0.27, 0.25), 0.6, 0.5))
    box("NeighbourR", (-0.71, -0.0, 1.05), (0.7, 0.6, 2.1), material("metal3", (0.22, 0.27, 0.25), 0.6, 0.5))

    bpy.ops.object.light_add(type="POINT", location=(-0.6, 1.6, 2.6))
    bpy.context.object.data.energy = 120
    bpy.context.object.data.color = (1.0, 0.92, 0.8)
    bpy.ops.object.light_add(type="POINT", location=(0.8, 1.2, 1.4))
    bpy.context.object.data.energy = 25
    bpy.context.object.data.color = (0.75, 0.85, 1.0)
    world = bpy.context.scene.world or bpy.data.worlds.new("World")
    bpy.context.scene.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.02, 0.02, 0.025, 1)


def build_locker():
    metal = material("locker", (0.25, 0.30, 0.28), 0.6, 0.45)
    handle = material("handle", (0.05, 0.05, 0.05), 0.8, 0.35)
    body = box("LockerBody", (0, 0, 1.05), (0.7, 0.6, 2.1), metal)
    subtract(body, box("Inner", (0, 0, 1.05), (0.64, 0.54, 2.04), metal))
    subtract(body, box("Front", (0, 0.3, 1.05), (0.62, 0.1, 2.0), metal))

    bpy.ops.object.empty_add(location=HINGE)
    door = bpy.context.object
    door.name = "Door"
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


def build_player():
    bpy.ops.object.empty_add(location=(0, 0, 0))
    root = bpy.context.object
    root.name = "PlayerRoot"

    bpy.ops.import_scene.fbx(filepath=FBX)
    arm = next(o for o in bpy.context.selected_objects if o.type == "ARMATURE")
    for o in bpy.context.selected_objects:
        if o.type == "MESH":
            skin_material(o)
    arm.parent = root

    # The eye carries the view's pitch; the game reads it from the exported animation.
    bpy.ops.object.empty_add()
    eye = bpy.context.object
    eye.name = "Eye"
    eye.parent = root
    eye.location = CAMERA_OFFSET
    eye.rotation_euler = (math.radians(90 + PITCH_DEG), 0, math.pi)

    bpy.ops.object.camera_add()
    cam = bpy.context.object
    cam.name = "FirstPerson"
    cam.parent = eye
    cam.data.sensor_fit = "VERTICAL"
    cam.data.angle_y = math.radians(75)
    cam.data.clip_start = 0.02
    return root, arm, cam


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


# ---------------------------------------------------------------- arm posing (armature space)

class Arm:
    def __init__(self, arm, side):
        self.arm = arm
        self.side = side
        p = f"Bip01 {side} "
        self.names = dict(upper=p + "UpperArm", fore=p + "Forearm", hand=p + "Hand")
        self.fingers = [[p + f"Finger{i}{j}" for j in ("", "1", "2")] for i in range(5)]
        bones = arm.data.bones
        h = lambda n: bones[n].head_local.copy()
        self.shoulder = h(self.names["upper"])
        self.elbow = h(self.names["fore"])
        self.wrist = h(self.names["hand"])
        knuckles = sum((h(f[0]) for f in self.fingers[1:]), Vector()) / 4
        fingers = (knuckles - self.wrist).normalized()
        across = (h(self.fingers[1][0]) - h(self.fingers[4][0])).normalized()  # towards the thumb
        normal = across.cross(fingers) if side == "R" else fingers.cross(across)
        normal.normalize()
        across = fingers.cross(normal) if side == "R" else normal.cross(fingers)
        self.rest_frame = Matrix((fingers, normal, across)).transposed()
        # The point that touches a grip: the middle of the knuckles, on the palm's surface.
        self.grip_point = knuckles + normal * 0.012 * 100
        self.rest_hand = bones[self.names["hand"]].matrix_local.copy()
        self.curl_axis = {}
        for i, chain in enumerate(self.fingers):
            for n in chain:
                local = bones[n].matrix_local.to_3x3().inverted()
                axis = across if i else fingers  # the thumb swings across the palm instead
                self.curl_axis[n] = (local @ axis).normalized()

    def hand_rotation(self, fingers, palm):
        """Armature-space rotation turning the rest hand so its fingers and palm face the given directions."""
        fingers = fingers.normalized()
        palm = (palm - fingers * palm.dot(fingers)).normalized()
        across = fingers.cross(palm) if self.side == "R" else palm.cross(fingers)
        frame = Matrix((fingers, palm, across)).transposed()
        return frame @ self.rest_frame.transposed()

    def wrist_for_grip(self, rotation, grip):
        """Where the wrist goes so that the hand's grip point lands on `grip`."""
        return grip - rotation @ (self.grip_point - self.wrist)

    def pose(self, wrist, rotation, pole, curl, thumb, grip):
        pb = self.arm.pose.bones
        s, e0, w0 = self.shoulder, self.elbow, self.wrist
        upper_len = (e0 - s).length
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

        rest_bend = self._bend(s, e0, w0)
        self._aim(self.names["upper"], s, e0 - s, rest_bend, elbow - s, bend)
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
        """`grip` holds the degrees per joint (knuckle, middle, tip) of each finger, thumb first, when closed."""
        sign = 1 if self.side == "R" else -1
        for i, chain in enumerate(self.fingers):
            amount = thumb if i == 0 else curl
            for n, deg in zip(chain, grip[i]):
                pb = self.arm.pose.bones[n]
                pb.rotation_mode = "QUATERNION"
                pb.rotation_quaternion = Quaternion(self.curl_axis[n], sign * math.radians(deg) * amount)


# ---------------------------------------------------------------- animation

class Grip:
    """Where and how a hand holds the door, in the door's space."""

    def __init__(self, point, fingers, palm, approach, curl):
        self.point = Vector(point)
        self.fingers = Vector(fingers)
        self.palm = Vector(palm)
        self.approach = Vector(approach)  # offset of the open hand just before it closes
        self.curl = curl


# Outside: fingers into the gap behind the bar, palm facing the hinge.
BAR = Grip(BAR_CENTER + Vector((-0.012, 0.004, 0)), (0, -1, 0), (1, 0, 0), (-0.05, 0.07, -0.02),
           ((25, 20, 25), (75, 95, 55), (80, 100, 55), (80, 100, 55), (80, 95, 50)))
# Inside: fingers through the lowest vent slit, palm down, hooked over its edge.
SLIT = Grip((-0.22, -0.0175, 1.393), (0, 1, 0), (0, 0, -1), (0.0, -0.07, -0.03),
            ((10, 10, 10), (15, 65, 50), (15, 70, 50), (15, 70, 50), (15, 65, 45)))
# Inside: a flat hand on the door near its free edge, fingers up over the slits, thumb clear of the door,
# pushing it open. The palm's heel is thicker than the knuckles, so the grip point stays off the door.
PUSH = Grip((-0.42, -0.03, 1.50), (0, 0, 1), (0, 1, 0), (0.0, -0.08, -0.04),
            ((-25, -10, 0), (-8, -4, 0), (-8, -4, 0), (-8, -4, 0), (-8, -4, 0)))

# Hanging at the side, in the player's space (facing -Y before turning).
REST = Vector((-0.24, 0.02, 0.90))
LEFT_REST = Vector((0.24, 0.02, 0.90))


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


def door_matrix(angle):
    return Matrix.Translation(HINGE) @ Matrix.Rotation(math.radians(angle), 4, "Z")


class Frame:
    """The scene posed for one frame: helpers to place the hand relative to the player and the door."""

    def __init__(self, move, arm_obj, right, root, f):
        self.move = move
        self.right = right
        self.f = f
        self.to_arm = arm_obj.matrix_world.inverted()
        self.rot_to_arm = self.to_arm.to_3x3().normalized()
        self.root = root.matrix_world.copy()
        self.rest = self.body(*REST)
        self.rest_rot = right.hand_rotation(self.heading(0, 0, -1), self.heading(1, 0, 0))

    def body(self, x, y, z):
        """A point in the player's space, in armature space."""
        return self.to_arm @ (self.root @ Vector((x, y, z)))

    def heading(self, x, y, z):
        """A direction in the player's space, in armature space."""
        return self.rot_to_arm @ (self.root.to_3x3().normalized() @ Vector((x, y, z)))

    def hold(self, grip, frame=None, approach=0.0):
        """Wrist and hand rotation holding `grip` with the door as it is at `frame` (this one by default)."""
        m = door_matrix(self.move.door_angle(self.f if frame is None else frame))
        to_arm = self.rot_to_arm @ m.to_3x3()
        rot = self.right.hand_rotation(to_arm @ grip.fingers, to_arm @ grip.palm)
        at = self.to_arm @ (m @ (grip.point + grip.approach * approach))
        return self.right.wrist_for_grip(rot, at), rot

    @staticmethod
    def turn(r0, r1, t):
        return r0.to_quaternion().slerp(r1.to_quaternion(), t).to_matrix()

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


class Hide:
    name = "LockerHide"
    end = 104
    release_bar = 33
    release_slit = 84

    @staticmethod
    def door_angle(f):
        pull = smooth(19, 31, f) * PULL_DEG
        swing = ease_out(31, 48, f) * (OPEN_DEG - PULL_DEG)
        close = smooth(60, 76, f) * -OPEN_DEG
        return pull + swing + close

    @staticmethod
    def pitch(f):
        """Looks at whatever the hand is reaching for, and out through the slits once the door is shut."""
        pitch = lerp(PITCH_DEG, -40.0, smooth(2, 15, f))
        pitch = lerp(pitch, -26.0, smooth(21, 33, f))
        pitch = lerp(pitch, -12.0, smooth(31, 46, f))
        pitch = lerp(pitch, -30.0, smooth(48, 57, f))
        return lerp(pitch, -8.0, smooth(72, 90, f))

    @staticmethod
    def position(f):
        if f < 33:
            return APPROACH.lerp(PULL, smooth(19, 33, f))
        if f < 58:
            return catmull([(33, PULL), (45, MIDWAY), (58, ENTRY)], f)
        return ENTRY.lerp(HIDE, smooth(58, 74, f))

    @staticmethod
    def yaw(f):
        """Turns around towards the hinge side while stepping in, to face out of the locker."""
        return 180.0 * smooth(36, 56, f)

    @staticmethod
    def elbow(fr):
        """Outside the elbow swings out; inside the locker it stays down, clear of the door."""
        return fr.body(-0.6, 0.15, 0.6).lerp(fr.body(-0.25, 0.05, 0.4), smooth(40, 50, fr.f))

    @classmethod
    def hand(cls, fr):
        f = fr.f
        if f < cls.release_bar:
            if f < 21:
                return BAR, *fr.reach(BAR, 4, 19, (-0.30, -0.25, 1.05))
            return BAR, *fr.hold(BAR), 1.0
        if f < 47:
            return BAR, *fr.release(BAR, cls.release_bar, (-0.02, 0.06, -0.12), 47)
        if f < cls.release_slit:
            # Reach out for the open door's vent and pull it shut while backing in.
            if f < 60:
                return SLIT, *fr.reach(SLIT, 47, 58, (-0.28, -0.15, 1.0))
            return SLIT, *fr.hold(SLIT), 1.0
        return SLIT, *fr.release(SLIT, cls.release_slit, (0.0, 0.08, -0.08), 96)


class Exit:
    name = "LockerExit"
    end = 44
    release_push = 20

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

    @staticmethod
    def position(f):
        return catmull([(11, HIDE), (26, DOORWAY), (36, EXIT)], f)

    @staticmethod
    def yaw(f):
        return 180.0

    @staticmethod
    def elbow(fr):
        return fr.body(-0.25, 0.05, 0.4)

    @classmethod
    def hand(cls, fr):
        f = fr.f
        if f < 11:
            return PUSH, *fr.reach(PUSH, 0, 9, (-0.25, -0.12, 1.05))
        if f < cls.release_push:
            return PUSH, *fr.hold(PUSH), 1.0
        return PUSH, *fr.release(PUSH, cls.release_push, (0.0, 0.06, -0.10), 30)


MOVES = {"hide": Hide, "exit": Exit}


def animate(move, arm_obj, right, left, door, root, f):
    bpy.context.scene.frame_set(f)
    door.rotation_euler.z = math.radians(move.door_angle(f))
    root.location = move.position(f)
    root.rotation_euler.z = math.radians(move.yaw(f))
    bpy.data.objects["Eye"].rotation_euler.x = math.radians(90 + move.pitch(f))
    bpy.context.view_layer.update()

    fr = Frame(move, arm_obj, right, root, f)
    grip, wrist, rot, curl = move.hand(fr)
    right.pose(wrist, rot, move.elbow(fr), curl, max(curl, 0) * 0.9, grip.curl)
    left.pose(fr.body(*LEFT_REST), left.hand_rotation(fr.heading(0, 0, -1), fr.heading(-1, 0, 0)),
              fr.body(0.3, 0.6, 1.0), 0.25, 0.2, BAR.curl)


def key_pose(arm_obj, door, root, f):
    for pb in arm_obj.pose.bones:
        pb.keyframe_insert("location", frame=f)
        pb.keyframe_insert("rotation_quaternion", frame=f)
    door.keyframe_insert("rotation_euler", frame=f)
    root.keyframe_insert("location", frame=f)
    root.keyframe_insert("rotation_euler", frame=f)
    bpy.data.objects["Eye"].keyframe_insert("rotation_euler", frame=f)


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


def export(move, arm_obj, root, door):
    """Writes the player's body, eye and the door with their animation to the game's glb."""
    bpy.context.scene.name = move.name
    bpy.ops.object.select_all(action="DESELECT")
    for o in (root, arm_obj, bpy.data.objects["Eye"], door, *arm_obj.children):
        o.select_set(True)
    bpy.ops.export_scene.gltf(
        filepath=str(ROOT / f"props/locker/locker_{MOVE}.glb"),
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
    build_room()
    door = build_locker()
    root, arm_obj, cam = build_player()
    # The view model hides the head, as HideBonesModifier does in the game.
    arm_obj.pose.bones["Bip01 Head"].scale = (0.001, 0.001, 0.001)

    debug_cams = {
        # name: (location, look at, lens)
        "tp": ((-1.25, 0.75, 1.55), (-0.3, 0.55, 1.25), 28),
        "close": ((-0.85, 0.55, 1.45), (-0.26, 0.36, 1.25), 35),
        "hand": ((-0.55, 0.75, 1.42), (-0.24, 0.36, 1.25), 60),
        "hand2": ((0.05, 0.85, 1.30), (-0.26, 0.36, 1.25), 60),
        "top": ((-0.25, 0.55, 2.3), (-0.25, 0.5, 1.2), 24),
        "side": ((1.1, 0.35, 1.55), (0.0, 0.15, 1.3), 30),
        "inside": ((-0.45, 0.1, 1.75), (0.1, 0.25, 1.35), 30),
    }
    if CAM in debug_cams:
        location, target, lens = debug_cams[CAM]
        bpy.ops.object.camera_add(location=location)
        cam = bpy.context.object
        cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
        cam.data.lens = lens
        hidden = {"side": ("LockerBody", "NeighbourL"), "inside": ("LockerBody", "NeighbourR")}
        for name in hidden.get(CAM, ("NeighbourR",)):
            bpy.data.objects[name].hide_render = True
        bpy.ops.object.light_add(type="SUN", rotation=(0.6, 0.0, -2.2))
        bpy.context.object.data.energy = 3

    move = MOVES[MOVE]
    bpy.context.scene.frame_start = 0
    bpy.context.scene.frame_end = move.end
    right, left = Arm(arm_obj, "R"), Arm(arm_obj, "L")
    for f in range(0, move.end + 1):
        animate(move, arm_obj, right, left, door, root, f)
        key_pose(arm_obj, door, root, f)

    if MODE == "export":
        export(move, arm_obj, root, door)
        return

    setup_render(cam)
    frames = range(0, move.end + 1) if FRAMES == "all" else [int(x) for x in FRAMES.split(",")]
    for f in frames:
        bpy.context.scene.frame_set(f)
        bpy.context.scene.render.filepath = f"{OUT}/{CAM}_{f:03d}.png"
        bpy.ops.render.render(write_still=True)


main()
