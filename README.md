# Scariest

A first-person found-footage horror game, inspired by Outlast, built with Godot 4 and C#.

## Requirements

- [Godot 4.7 (.NET edition)](https://godotengine.org/download)
- [.NET SDK 8 or newer](https://dotnet.microsoft.com/download)

## Running

Open the project in the Godot editor and press **F5**, or from the terminal:

```sh
dotnet build
godot --path .
```

## Controls

| Action | Key |
|---|---|
| Move | W A S D |
| Sprint | Shift |
| Crouch | Ctrl or C (hold) |
| Interact (doors, lockers, batteries) | E |
| Raise / lower camcorder | Right mouse button |
| Night vision | F (camcorder raised) |
| Replace battery | R |
| Release mouse | Esc |

## Camera

The first-person view sits on a small camera rig (`player/PlayerCamera.cs`) between the head and the camera.
It adds the procedural movement that makes the view feel handheld: a step bob that scales with speed, a lean
when strafing, a slightly wider field of view while sprinting, and a small roll and yaw lag when turning the
view. Getting grabbed adds a shake impulse and the strangle adds a continuous shake. The view hands, the
camcorder and the interaction ray are children of the camera, so they all move with these effects.

## Rendering

The game renders at native resolution with 4x MSAA, 8x anisotropic filtering and high-quality soft shadows,
and textures are imported with mipmaps and high-quality VRAM compression. The asylum adds SSAO, screen-space
reflections, volumetric fog and a few dim room lights so the PBR materials stay readable in the dark.
The camcorder's infrared light casts shadows too, which keeps it from leaking through walls.

## Project structure

Scenes and their scripts live together, grouped by feature.

| Folder | Contents |
|---|---|
| `player/` | First-person controller (movement, crouch, hiding, being grabbed), procedural camera effects and first-person hands |
| `camcorder/` | Camcorder logic, recording HUD and the screen-effect shader |
| `interaction/` | `IInteractable` interface and the look-at interactor |
| `enemy/` | Stalker AI (patrol, hearing, sight, chase, search) and its strangle attack |
| `props/` | Interactable objects: doors, lockers, batteries |
| `levels/` | Levels and their shared environment |
| `lighting/` | Light behaviours such as flickering |
| `materials/` | PBR materials and the asylum interior shader |
| `assets/` | Third-party models, animations and textures, see [CREDITS.md](CREDITS.md) |

## Status

Prototype: a dark, textured test level (a 28 m corridor with six 9 x 9 m rooms) to explore with the camcorder,
doors to open and lockers to hide in,
and a masked surgeon that patrols, hears footsteps, chases on sight and catches players it sees hiding.
When it catches you it grabs you by the shoulders and strangles you.
Next up: sound, an objective and an exit.

## Animation

The enemy is a masked surgeon and the player's arms belong to a man in a denim jacket, both from the
Microsoft Rocketbox library. The Quaternius animation libraries and the Rocketbox models are all imported with a
`SkeletonProfileHumanoid` bone map (`assets/*/*bone_map.tres`), so every animation targets the standard humanoid
skeleton and plays on every character. Rocketbox characters also need:

- `fix_silhouette` in their import settings, because they rest in an A-pose and the animations expect a T-pose.
- `Bip01` mapped to `Root`, and the Quaternius imports keeping constant tracks, so the root track grounds them.

Godot does not notice edits to a bone map, so after changing one, delete the model's files in `.godot/imported`
to reimport it. Importing a Rocketbox FBX logs errors about missing `.tga` files: the FBX still points at the
author's original textures, which our own materials (`assets/rocketbox/*/*.tres`) replace.

The enemy model is scaled up to about 2.15 m. Its movement speeds are scaled with it so the feet don't slide.

The strangle attack (`enemy/StrangleAttack.cs`) is not a canned animation. The enemy lunges in mid-sprint,
grabs the player's shoulders and shakes them, then pushes them to arm's length and strangles them.
A `TwoBoneIK3D` modifier moves the hands to grab points on the player (`Player/GrabPoints`) and a
`LookAtModifier3D` turns the head towards the player's face, so the move fits wherever the player stands.
The IK needs its elbow pole markers (`LeftElbowPole`, `RightElbowPole`) to solve.

The player's own hands (`player/view_hands.tscn`) only appear while reaching for something: opening
or closing a door or locker. They belong to a full body placed around the camera, with its head shrunk away by
`HideBonesModifier`; IK places the wrist on a grip point, `HandAimModifier` turns the hand to the grip point's
orientation and `GripFingersModifier` curls the fingers by the amount the interaction asks for.

A grip point says how the hand is held: its -Z axis is where the fingers point and its -Y axis is where the palm
faces (`ViewHands.Pose`), so a door handle is gripped palm down with curled fingers and a far door is pushed
with a flat hand. The arms are only a little longer than a real reach, so for a far grip `ViewHands` scales the
whole view model around the camera until the elbow stays comfortably bent; on screen it looks like a closer
reach but the hand still touches the grip.

Interactions never stop the player abruptly. Doors don't lock the controls at all: the hand pushes the
door and the player keeps walking. Getting into a locker is one timeline of overlapping steps
(`Locker.cs`); the walk up to it starts from the player's current velocity along a curve, so it blends
in from however the player was moving.

## Level editing

The enemy walks on the level's navigation mesh. After changing walls or lockers, select the
`Navigation` node in the level and press **Bake NavigationMesh** in the editor toolbar.
Doors are deliberately kept outside the `Navigation` node so the enemy can path through them.
