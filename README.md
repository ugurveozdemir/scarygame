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
| `enemy/` | Stalker AI (patrol, hearing, sight, chase, search), its voice and its strangle attack |
| `props/` | Interactable objects (doors, lockers, batteries), furniture (beds, tables, chairs, a TV) and the grounds' props (trees, fountain, lamps, cars, fence) |
| `levels/` | Levels and their shared environment |
| `lighting/` | Light behaviours such as flickering, with its buzz |
| `audio/` | Footsteps and the surfaces they sound on, the ambience and the chase music |
| `materials/` | PBR materials, the asylum interior shader and the exterior materials (brick, concrete, asphalt, paving, lawn, fountain water) |
| `assets/` | Third-party models, animations, textures and sounds, see [CREDITS.md](CREDITS.md) |

## Status

Prototype: a dark, textured asylum to explore with the camcorder. A 28 m corridor with six 9 x 9 m rooms leads
north into a second wing: a day room with tables, a TV and a nurse station, a ward with four beds, a shower room
and a balcony outside the day room's barred windows. Two crawl-only ventilation ducts link the first side rooms
to the ward and the showers; crouch to get through them, the enemy can't follow.
Behind the spawn point a door opens onto stairs down to the asylum grounds at night: a lawn with trees, a fountain
on a paved plaza, benches and street lamps, a parking lot of cars under tarps, and a road out to a locked iron
gate in the chain-link fence around it all. The building stands on a 3 m concrete base, so the balcony overlooks
the grounds. There are doors to open,
lockers to hide in and spare batteries lying around,
and a masked surgeon that patrols, hears footsteps, chases on sight and catches players it sees hiding.
When it catches you it grabs you by the shoulders and strangles you.
Everything that matters for the feel has a sound: footsteps on every surface, doors, lockers, the camcorder,
the enemy's breathing, steps, roars and grunts, the strangling, the player's breathing and racing heart, humming
lamps, a television left on, dripping showers and knocking pipes, wind in the vents, windows and trees, things
moving far off in the building, owls and a dog far off, a rattling fence and creaking gate, flapping tarps, the
fountain, the building's hum and the night outside, a tension drone and chase music.
Next up: an objective and an exit.

## Sound

The sounds are field recordings, most of them from BigSoundBank, cut into variations and mixed in
`assets/sounds` (see [CREDITS.md](CREDITS.md)). Short sounds are WAV and loops are OGG Vorbis, imported with
looping on. Sounds with variations are `AudioStreamRandomizer` resources, which pick a different take each time
and vary pitch and volume a little. Every sound is an audio player in the scene of whatever makes it, set up there
with its level and range; scripts only start and fade them.

Buses (`default_bus_layout.tres`):

| Bus | Sends to | Contents |
|---|---|---|
| Master | | A hard limiter, so the loudest moments never clip |
| World | Master | Everything heard from outside the player's head. A low-pass filter muffles it as the player passes out |
| Sfx | World | Footsteps, doors, lockers, the camcorder, the enemy |
| Ambience | World | The building's hum, the night, the fountain, buzzing lamps |
| Reverb | World | The room reverb of sounds made inside the building |
| Player | Master | The player's own body: breathing, heartbeat, choking, the ringing ears, the death sound |
| Music | Master | The tension drone, the chase music and the sting |

Footsteps (`audio/Footsteps.cs`, `audio/footsteps.tscn`) play the sound of the ground under the feet. A floor body
says what it is made of with a `surface` metadata entry (the building is `tile`, the stairs `stairs`, the lawn
`grass`), and a `SurfaceZone` area on the "zones" physics layer overrides it where one body has several surfaces:
the vents, the balcony, the paths, the parking lot, the road, the manhole cover and the shrubs. The player steps at the bottom of each head bob, so
the steps match the camera. The enemy steps whenever one of its animated feet lands (`audio/SkeletonFootsteps.cs`),
which keeps the steps in time with every animation and blend.

The `Indoors` area covers the building: it sends the sounds made inside it to the reverb bus, and `audio/Ambience.cs`
uses it to fade between the building's hum and the night. `audio/ChaseMusic.cs` follows the enemy: a sting when it
spots the player, then music until it loses them. `audio/TensionDrone.cs` keeps a low drone under everything that
swells while the enemy searches nearby. Fixed sound sources like the dripping showers and the wind in the vents,
windows and trees are players under `SoundEmitters` in the level. Occasional ones, like the knocking pipes, a bed
creaking in the ward or something falling far off in the building, use `audio/RandomSound.cs`, which plays at random
intervals. The fence rattles where someone walks into it.

The player's fear is heard from inside their head (`player/Dread.cs`): the closer the enemy and the more it hunts
them, the faster their heart beats, one sampled beat at a time, and the harder they breathe (`player/Breathing.cs`,
which also gets heavier from sprinting). Hiding in a locker muffles the world through the same low-pass filter that
choking uses, and the player holds their breath, so only shaky breaths through the nose escape.

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

## Grounds

The outdoor models are photoscanned Poly Haven assets and the ground textures come from ambientCG (see
[CREDITS.md](CREDITS.md)). The scanned trees have millions of triangles, so their game versions in
`assets/polyhaven/` were reduced in Blender: a share of the leaves is kept and scaled up to keep the canopy full,
and leaves, branches and trunk are decimated to about 55k triangles per tree, with alpha-clipped leaves.
`Tree.cs` fades trees out in the fog and the ones beyond the fence cast no shadows.

The lawn shader (`materials/exterior/lawn.gdshader`) blends two scales of the grass texture with noise and adds
dry patches, so the texture doesn't visibly repeat. `Fence.cs` builds the chain-link fence along its corner
points with multimeshes and solid walls. The stairs have an invisible ramp over them so walking up and down is
smooth. The night sky is the `kloppenheim_02` HDRI and a directional moon light; the street lamps cast no
shadows, which keeps the grounds above 30 FPS.

## Level editing

The enemy walks on the level's navigation mesh. After changing walls or lockers, select the
`Navigation` node in the level and press **Bake NavigationMesh** in the editor toolbar.
Doors are deliberately kept outside the `Navigation` node so the enemy can path through them.
Furniture, lockers, trees and the grounds' props go inside it, so the enemy walks around them, and the bake
covers the grounds too, down the stairs. The vents are lower than the enemy's
navigation agent, so the bake leaves them out and they stay a safe route for the player.

A new floor needs a `surface` metadata entry on its body, or a `SurfaceZone` under `SoundZones` where it shares a
body with other surfaces; otherwise its footsteps sound like concrete. A new room in the building needs to stay
inside the `Indoors` area for its reverb and hum.
