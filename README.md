# Fart Raft (Unity, iOS + Android)

A mobile party game prototype inspired by *Fart Together*: 2-4 animals (capybara, hippo,
croc, gorilla) share one phone and one round inflatable raft, and fart it down floating
water slides high above the clouds.

## Gameplay

- Each player has a **joystick** in a corner of the screen and an animal standing on the rim of the raft.
- **Push the joystick where you want the raft to go.** Your animal runs around the rim to the opposite side and
  farts a big green jet, pushing the raft that way. Let go to stop.
- Steep slides are fast on their own; on flat pools the raft glides for a few seconds after you stop farting.
- **Spiky obstacles kill** (spiky buoys, spinning spiked arms, swinging pendulums): boom, white flash, back to the
  last checkpoint. Walls, rocks and wooden blocks only bump you.
- Some parts of the slide have no walls: **fall off and you go back to the last checkpoint**.
- When **everyone farts at the same time** you get a **SYNC BOOST**.
- Each fart drains your gas ring. If it empties, you're out until it refills.
- Course: 4 steep slides and 4 pools, 7 checkpoints and a finish line. Your best time is saved (per mode).
- **Difficulty** (tap **MODE** on the menu, the choice is remembered):
  - **EASY**: the raft moves down the track by itself and always faces forward, so you only steer.
    Animals jump to the right spot instantly, farts are stronger, gas lasts longer, obstacles move slower
    and top speed is capped. The first spiky hit only bounces you (**OUCH!**); a second hit within 3 seconds kills.
  - **HARD**: the original physics - no auto-forward, the raft spins freely, one spiky hit kills.
- HUD: progress bar, timer, death counter and name tags over each animal.
- Real recorded fart sounds plus bump, crash, fall, checkpoint and finish sounds (all CC0, see `Audio/CREDITS.txt`).
- Modes: **Solo**, **2, 3 or 4 players**.

## Setup

1. In Unity Hub create a new project with the **Universal 3D** template (or **3D (Built-In)**). Unity 2022.3 LTS or Unity 6 recommended.
   If you create the project from the terminal with `Unity -createProject` instead, it has no packages, so add
   `"com.unity.ugui": "2.0.0",` to the `dependencies` in `Packages/manifest.json` (use `1.0.0` on Unity 2022).
2. Copy the `Assets/FartRaft` folder from this repo into your project's `Assets` folder.
3. In Unity's top menu click **Fart Raft > 1. Configure Project For iOS and Android**.
4. Click **Fart Raft > 2. Build Game Scene**. This generates the whole level, raft, characters and UI in `Assets/FartRaft/Scenes/Game.unity`.
5. Press **Play**. Keyboard controls in the editor:

   | Player | Hold a direction (= joystick) |
   | --- | --- |
   | 1 (Capy) | W A S D |
   | 2 (Hippo) | Arrow keys |
   | 3 (Croc) | I J K L |
   | 4 (Gorilla) | Numpad 8 4 5 6 |

When updating an existing project, delete the old `Assets/FartRaft/Scripts` and `Assets/FartRaft/Editor`
folders before copying the new ones (removed scripts would otherwise stay behind and break compiling).

   To test real touch controls, install the **Unity Remote 5** app on your phone and pick it in
   *Project Settings > Editor > Device*.

## Better graphics

- **Fart Raft > 3. Upgrade Graphics To URP** installs the Universal Render Pipeline (click it again once the
  install finishes), switches the project to it and rebuilds the scene with bloom, tonemapping and color grading.
- To use real models, import a free nature pack (e.g. *EmbersStorm – Free Nature Pack*, or *Ultimate Nature – Starter*
  for URP) and select `Assets/FartRaft/EnvironmentTheme`. Drag the pack's tree, bush, rock and flower prefabs into
  the lists (and optionally a water/grass material), then run **Fart Raft > 2. Build Game Scene** again.
  Adjust `Prop Scale` if the models are too big or small.
- Rebuilding the scene replaces `Game.unity`, so make manual scene edits in a copy.

## Building for phones

Install the modules in **Unity Hub > Installs > (your version) > Add modules**:

- **Android**: *Android Build Support* (with OpenJDK and Android SDK & NDK). Then *File > Build Settings > Android > Switch Platform > Build* to get an `.apk`/`.aab`.
- **iOS**: *iOS Build Support*. Building needs a **Mac with Xcode**. Unity exports an Xcode project, which you open in Xcode, sign with your Apple ID and run on an iPhone.

Change the bundle id `com.mystudio.fartraft` in *Project Settings > Player* before publishing.
Publishing costs: Google Play $25 one time, Apple Developer Program $99/year.

## Project layout

| Path | What |
| --- | --- |
| `Scripts/Raft.cs` | Raft physics (slides down the slide, tilts with the slope, bumper knock-back) |
| `Scripts/FartSeat.cs` | One player: position on the rim, thrust, gas tank, animation, particles, sound |
| `Scripts/FartJoystick.cs` | Multi-touch joystick (one per player) |
| `Scripts/GameManager.cs` | Menu, countdown, timer, deaths (crash/fall + respawn), progress bar, checkpoints, sync boost, best times |
| `Scripts/GameAudio.cs`, `Audio/` | Sound effects; `FartSound.cs` is a generated fallback if no fart clips exist |
| `Scripts/Spinner.cs`, `Pendulum.cs`, `Mover.cs`, `Bumper.cs`, `Hazard.cs`, `Checkpoint.cs`, `FinishLine.cs` | Course pieces (`Hazard` kills, `Bumper` bounces) |
| `Scripts/CameraFollow.cs`, `CoursePath.cs` | Camera that looks down the track |
| `Scripts/WorldLabel.cs`, `UVScroll.cs`, `KeyboardControls.cs` | Name tags, flowing water, editor testing |
| `Editor/FartRaftBuilder.cs` | Menu commands that configure the project and build the scene |

Tuning values (thrust, gas, boost, obstacle speeds) are all editable in the Inspector.

## Next steps

- Replace the primitive shapes with real models (e.g. low-poly packs from the Asset Store).
- Add real fart sounds and music.
- More levels: duplicate the course code in `FartRaftBuilder.BuildCourse` with new waypoints.
- Online multiplayer for phones: Unity **Netcode for GameObjects** with **Unity Relay + Lobby**
  (Unity Gaming Services), keeping the raft physics simulated only on the host.
