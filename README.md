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

## Project structure

Scenes and their scripts live together, grouped by feature.

| Folder | Contents |
|---|---|
| `player/` | First-person controller (movement, crouch, hiding) |
| `camcorder/` | Camcorder logic, recording HUD and the screen-effect shader |
| `interaction/` | `IInteractable` interface and the look-at interactor |
| `enemy/` | Stalker AI: patrol, hearing, sight, chase and search |
| `props/` | Interactable objects: doors, lockers, batteries |
| `levels/` | Levels and their shared environment |
| `lighting/` | Light behaviours such as flickering |
| `greybox/` | Prototype materials, to be replaced by real art |

## Status

Greybox prototype: a dark test level to explore with the camcorder, doors to open and lockers to hide in,
and an enemy that patrols, hears footsteps, chases on sight and catches players it sees hiding.
Next up: sound, an objective and an exit.

## Level editing

The enemy walks on the level's navigation mesh. After changing walls or lockers, select the
`Navigation` node in the level and press **Bake NavigationMesh** in the editor toolbar.
Doors are deliberately kept outside the `Navigation` node so the enemy can path through them.
