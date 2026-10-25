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
| Interact (doors, lockers, beds, batteries, notes, switches) | E |
| Put a note down, step away from a keypad | E or Esc |
| Type a code on a keypad | Number keys, Backspace |
| Raise / lower camcorder | Right mouse button |
| Night vision | F (camcorder raised) |
| Replace battery | R |
| Release mouse | Esc |

## Story

The player is Subject 14 of the Vespera Sleep & Neuro-Rehabilitation Clinic, wiped thirteen times, and the
objectives on screen are memos their previous trial typed into the camcorder's display. The whole story, its
threats, acts and the places it needs are in [STORY.md](STORY.md).

A trial runs through four acts and ends one of two ways:

1. **Ground zero.** Wake in the parking lot, find the battery and note in the plaza's trash can, and reach the
   barred hideout in the last room on the right through the showers' vent. The nurse station's intercom pages the
   south rooms and draws the surgeon there.
2. **Ghosts in the archive.** In the basement's archive, Trial 13's tape shows the service lift's code. The main
   breaker in the electrical room powers the lift, bursts the basement's lights and frees the Remnant.
3. **The silent ward.** The lift goes up to the new second floor, past where any memo reaches: Sleepwalkers, a
   wall of security monitors showing the player's own route, the chief physician's office with the case file,
   the lock console and the pump room key.
4. **Fog over the grounds.** Releasing the locks sounds the reset siren and sends the surgeon outside. Through the
   gate is *The Loop*: the floodlight resets the player and the next trial begins. Under the fountain is *Signal
   Lost*: tearing out the transmitter and escaping through the drainage tunnel.

`story/GameState.cs` keeps the trial number across reloads. Each reset, by getting caught or at the gate, starts
the next trial: one more tally mark in the hideout, and after the loop a warning on its wall, different first
memos and a different note in the trash can. `story/Story.cs` ties the acts together; every prop does its own work.

## Camera

The first-person view sits on a small camera rig (`player/PlayerCamera.cs`) between the head and the camera.
It adds the procedural movement that makes the view feel handheld: a step bob that scales with speed, a lean
when strafing, an aim that drifts in a figure of eight while sprinting (the head nods with each footfall and turns
with each stride), a slightly wider field of view while sprinting, and a small roll and yaw lag when turning the
view. A hit adds a shake impulse and snaps the head away from the blow before it eases back. The view hands, the
camcorder and the interaction ray are children of the camera, so they all move with these effects.

## Rendering

The game renders at native resolution with 4x MSAA, 8x anisotropic filtering and high-quality soft shadows,
and textures are imported with mipmaps and high-quality VRAM compression. The asylum adds SSAO, screen-space
reflections and a few dim room lights so the PBR materials stay readable in the dark. Dense volumetric fog
gives the lights a hazy glow and a dark distance fog swallows corridors and the grounds after about 20 m.
The camcorder's infrared light casts shadows too, which keeps it from leaking through walls.

Godot doesn't hide what is behind walls by itself, so from the grounds the whole building's insides are drawn.
To keep that cheap, the lights inside the building fade out, shadows and all, beyond 24 m, and small props, beds
and notes beyond 25 to 35 m, where the fog has swallowed them anyway. Many small copies of one thing, like tapes,
medicine boxes or broken glass, are a single multimesh. Anything animated that casts shadows makes every
shadowed light around it redraw its shadows each frame, so the Sleepwalkers cast none and stop moving when the
player is far away. Security monitors render their cameras a few frames a second, in turn.

## Project structure

Scenes and their scripts live together, grouped by feature.

| Folder | Contents |
|---|---|
| `player/` | First-person controller (movement, crouch, hiding, getting hit, dying), procedural camera effects and first-person hands |
| `camcorder/` | Camcorder logic, recording HUD and the screen-effect shader |
| `interaction/` | `IInteractable` and `IHidingSpot` interfaces, the look-at interactor and the scripted moves into hiding spots |
| `objectives/` | The on-screen memos and the trigger areas that hand out new ones |
| `story/` | The trial counter, the story's acts and endings, notes and the note reader, captions, the title card, the keypad screen and the render layers that decide who sees what |
| `enemy/` | The surgeon's AI (patrol, hearing, sight, chase, punches, searching hiding spots) and its voice, the Remnant and the Sleepwalkers |
| `props/` | Interactable objects (doors and their bars, lockers, batteries, the intercom, the lift and its keypad, the breaker, the tape deck, the lock console, keys, the hatch, ladders and the transmitter), security monitors, furniture (beds, tables, chairs, a TV), decorations (tally marks, rows of boxes, broken glass) and the grounds' props (trees, fountain, lamps, cars, fence, the sliding gate) |
| `levels/` | Levels and their shared environment |
| `lighting/` | Light behaviours such as flickering, with its buzz, and the alarm's beacons |
| `audio/` | Footsteps and the surfaces they sound on, the ambience and the chase music |
| `materials/` | PBR materials, the asylum interior shader and the exterior materials (brick, concrete, asphalt, paving, lawn, fountain water) |
| `assets/` | Third-party models, animations, textures and sounds, see [CREDITS.md](CREDITS.md) |
| `tools/` | `synthesize_sounds.py`, which makes the story's machine sounds in `assets/sounds/story`, and `locker_animations.py`, the Blender script that makes the animations of getting into and out of a locker |

## Status

Prototype: a dark, textured asylum to explore with the camcorder. A 28 m corridor with six 9 x 9 m rooms leads
north into a second wing: a day room with tables, a TV and a nurse station, a ward with four beds, a shower room
and a balcony outside the day room's barred windows. Two crawl-only ventilation ducts link the first side rooms
to the ward and the showers; crouch to get through them, the enemy can't follow. The left middle room is the
service lift's lobby.
In the last room on the right a door opens onto a long concrete stairwell, 32 steps down to a basement under the
building, below the grounds: a service corridor with pipes along the ceiling, a boiler room lit only by the
boiler's fire and a work lamp, a tiled morgue with a wall of cold chambers and covered bodies, a pitch-dark
storage room, an archive of tapes and an electrical room. It is darker than the floor above, so the camcorder's
night vision does most of the seeing there.
The ground ends at the building's concrete base, so the stairwell and the basement can pass below it.
Above the main corridor the lift reaches a second floor, the sleep ward: two dormitories, a lift lobby, the chief
physician's office, an observation room looking through a one-way mirror into a sealed sleep lab, and a pharmacy
whose fire door opens onto a metal fire escape down the east wall.
The player wakes up in the parking lot at night and walks up to the building. Behind the corridor's south end a
door opens onto stairs down to the grounds: a lawn with trees, a fountain on a paved plaza with a padlocked
maintenance hatch beside it, benches and street lamps, a parking lot of cars under tarps, and a road out to a
sliding iron gate, held by a magnetic lock, in the chain-link fence around it all. Under the plaza lie the pump
room and a drainage tunnel that runs east under the fence. The building stands on a 3 m concrete base, so the balcony overlooks
the grounds. There are doors to open,
rows of lockers to hide in, beds to crawl under and spare batteries lying around,
and a masked surgeon that patrols, hears footsteps, chases on sight and punches you when it catches up.
Each hit shoves you back, snaps your head away, flashes the screen red and leaves your ears ringing, and the
edges of the view stay redder the more hurt you are; four hits in quick succession knock you down. Between hits
you can run: you are faster than it when you sprint. After 8 seconds without being hit you start to recover,
one hit's worth every 6 seconds, and the red fades from the edges. Where it loses you it checks a couple of hiding spots nearby at random,
tearing lockers open and kneeling to look under beds, and drags out whoever it finds to hit them again. It
always finds a player it saw hiding.
Everything that matters for the feel has a sound: footsteps on every surface, doors, lockers, the camcorder,
the enemy's breathing, steps, roars, grunts and punches, the player's breathing, cries and racing heart, humming
lamps, a television left on, dripping showers and knocking pipes, wind in the vents, windows and trees, things
moving far off in the building, owls and a dog far off, a rattling fence and creaking gate, flapping tarps, the
fountain, the building's hum and the night outside, a tension drone and chase music.
The objective shows near the top of the screen for a few seconds whenever it changes, typed out like a memo on the
camcorder's display. An `ObjectiveTrigger` area in the level hands out a new one the first time the player walks
in; notes, the tape and the story hand out the rest.

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
| World | Master | Everything heard from outside the player's head. A low-pass filter muffles it right after a hit |
| Sfx | World | Footsteps, doors, lockers, the camcorder, the enemy |
| Ambience | World | The building's hum, the night, the fountain, buzzing lamps |
| Reverb | World | The room reverb of sounds made inside the building |
| Player | Master | The player's own body: breathing, heartbeat, the blows landing, cries of pain, the ringing ears, the death sound |
| Music | Master | The tension drone, the chase music and the sting |

Footsteps (`audio/Footsteps.cs`, `audio/footsteps.tscn`) play the sound of the ground under the feet. A floor body
says what it is made of with a `surface` metadata entry (the building is `tile`, the stairs `stairs`, the lawn
`grass`), and a `SurfaceZone` area on the "zones" physics layer overrides it where one body has several surfaces:
the vents, the balcony, the paths, the parking lot, the road, the manhole cover and the shrubs. The player steps at the bottom of each head bob, so
the steps match the camera. Strides lengthen as the player speeds up, like a real walker's: about 1.5 steps a second
walking and 2.4 sprinting. The enemy steps whenever one of its animated feet lands (`audio/SkeletonFootsteps.cs`),
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
which also gets heavier from sprinting). Hiding muffles the world through the same low-pass filter that dazes
the player after a hit, and the player holds their breath, so only shaky breaths through the nose escape.

The story's machines are synthesized instead of recorded: the memo beep, the intercom, the keypad, the lift, the
breaker, the bursting lights, the Remnant's static, the siren, the reset whine, the transmitter and broken glass
underfoot. `tools/synthesize_sounds.py` makes them with plain Python into `assets/sounds/story`; the loops among
them are imported with their loop mode set to forward. A step on broken glass carries 8 m however carefully it is
taken, which the Sleepwalkers hear: every audible footstep is a `Player.MadeNoise` event they listen for.

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

The enemy model is scaled up to about 2.15 m. Its walking animations play faster or slower to match its speed,
so the feet don't slide.

The enemy punches with the Quaternius jab and cross; the hit lands at the moment the fist is furthest out,
measured from the animations, and only if the player is still within reach in front of it. It tears lockers
open with the `Interact` animation and kneels beside beds with `Fixing_Kneeling`. A `LookAtModifier3D` turns
its head towards the player's face while it punches and into the hiding spot while it checks one.

The player's own hands (`player/view_hands.tscn`) only appear while reaching for something: opening
or closing a door, or pushing a locker open from inside. They belong to a full body placed around the camera, with its head shrunk away by
`HideBonesModifier`; IK places the wrist on a grip point, `HandAimModifier` turns the hand to the grip point's
orientation and `GripFingersModifier` curls the fingers by the amount the interaction asks for.

A grip point says how the hand is held: its -Z axis is where the fingers point and its -Y axis is where the palm
faces (`ViewHands.Pose`), so a door handle is gripped palm down with curled fingers and a far door is pushed
with a flat hand. The arms are only a little longer than a real reach, so for a far grip `ViewHands` scales the
whole view model around the camera until the elbow stays comfortably bent; on screen it looks like a closer
reach but the hand still touches the grip.

Interactions never stop the player abruptly. Doors don't lock the controls at all: the hand pushes the
door and the player keeps walking. Getting into a locker or under a bed is one timeline of overlapping steps
(`interaction/ScriptedMove.cs`); the walk up to it starts from the player's current velocity along a curve, so
it blends in from however the player was moving. Under a bed the player gets down on the floor and crawls in
sideways, turning to face out, and watches the room from floor level.

Getting into a locker is a hand-made animation instead of IK, because a hand that has to hold a handle, follow
a swinging door and change grips looks wrong when it is worked out on the fly. `tools/locker_animations.py`
builds the locker in Blender at the game's measurements, poses the arm, the view and the door together frame by
frame and exports them as `props/locker/locker_hide.glb`: the right hand takes the door's pull bar from the
side and pulls the door open, the player steps in turning around, hooks their fingers into the lowest vent slit
and pulls the door shut while backing in. Run it with Blender after changing the locker, the move or the arms:

```bash
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python tools/locker_animations.py -- export hide
```

`interaction/PlayerAnimation.cs` plays such an animation in the prop's space: the player's camera sits on the
animated eye, the prop's parts copy their animated twins and the animated body stands in for the player's arms.
Since the animation always starts from the same spot, the player first walks there from wherever they pressed
the key, so the hand lands on the handle from any angle. A locker whose door is already open skips to stepping
in.

Lockers and beds are hiding spots (`IHidingSpot`): each has a search point where the enemy stands to check it,
a point it looks at, and a way to drag out whoever hides there. A bed's open side is its local -X, so beds whose
-X side would face a wall are turned around in the level.

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

The second floor, the lift, the basement's new rooms, the fire escape and the pump room are CSG boxes in the
level's combiners like the rest of the building, with the story's pieces under the `Act1` to `Act4` and
`SignalLost` nodes. The lift's car is the same room on both floors, so the ride moves the player exactly one
storey (3.4 m); keep both cars identical when changing one.

Recordings and security cameras render the level live from their own cameras. What only a tape should show is on
render layer 11 (`VisualLayers.Past`), the Remnant only on layer 3, which the player's camera shows during night
vision, and enemies on layer 2, so tapes of the past leave them out.

The enemy walks on the level's navigation mesh. Its patrol takes it down the stairwell to the boiler room too. After changing walls or lockers, select the
`Navigation` node in the level and press **Bake NavigationMesh** in the editor toolbar.
Doors are deliberately kept outside the `Navigation` node so the enemy can path through them.
Furniture, lockers, trees and the grounds' props go inside it, so the enemy walks around them, and the bake
covers the grounds too, down the stairs. The vents are lower than the enemy's
navigation agent, so the bake leaves them out and they stay a safe route for the player.

A new floor needs a `surface` metadata entry on its body, or a `SurfaceZone` under `SoundZones` where it shares a
body with other surfaces; otherwise its footsteps sound like concrete. A new room in the building needs to stay
inside the `Indoors` area for its reverb and hum.
