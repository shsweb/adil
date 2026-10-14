# Fart Raft (Unity, iOS + Android)

A mobile party game prototype inspired by *Fart Together*: 2-4 animals share one phone and
one round inflatable raft, and fart it down floating water slides high above the clouds.

## Gameplay

- Each player has a **joystick** in a corner of the screen and an animal standing on the rim of the raft.
- **Push the joystick where you want the raft to go.** Your animal runs around the rim to the opposite side and
  farts a big green jet, pushing the raft that way. Let go to stop.
- Steep slides are fast on their own; on flat pools the raft glides for a few seconds after you stop farting.
- **Spiky obstacles kill** (spiky buoys, spinning spiked arms, swinging pendulums): boom, white flash, back to the
  last checkpoint. Walls, rocks and wooden blocks only bump you.
- Some parts of the slide have no walls: **fall off and you go back to the last checkpoint**.
- Funny obstacles that only knock you around: a **boxing glove** punching out of the wall, a giant **jumping fish**
  and a rolling **beach ball**.
- Grab a floating **BEANS** can for 4 seconds of **MEGA FART** (much stronger farts and a full gas tank).
- When **everyone farts at the same time** you get a **SYNC BOOST**.
- Each fart drains your gas ring. If it empties, you're out until it refills.
- **3 stages**, picked with the **STAGE** button on the menu; **NEXT STAGE** on the finish screen goes to the next one:
  1. **Sunny Slides**: 4 slides and 4 pools, 7 checkpoints. Warm-up.
  2. **Jungle Rapids**: longer, with sliding blocks, more buoys and spinners. 6 checkpoints.
  3. **Volcano Run**: steepest and busiest, with pendulums, four-arm spinners and more open edges. 8 checkpoints.
- Your best time is saved per stage, mode and player count.
- **Difficulty** (tap **MODE** on the menu, the choice is remembered):
  - **EASY**: the raft moves down the track by itself and always faces forward, so you only steer.
    Animals jump to the right spot instantly, farts are stronger, gas lasts longer, obstacles move slower
    and top speed is capped. The first spiky hit only bounces you (**OUCH!**); then the raft blinks for 2 s and passes through spiky things. Another hit within 4 seconds of the OUCH (after the blinking) kills.
  - **HARD**: the original physics - no auto-forward, the raft spins freely, one spiky hit kills.
- HUD: progress bar, timer, death counter and name tags over each animal.
- Real recorded fart sounds plus bump, crash, fall, checkpoint and finish sounds (all CC0, see `Audio/CREDITS.txt`).
- Modes: **Solo**, **2, 3 or 4 players**.
- **CHARACTERS** on the menu: the camera zooms onto the raft and every player flips through animals with **< >**
  (capy, penguin, croc, gorilla). Each animal has its own fart pitch and fart cloud color and does a happy dance
  when picked and at the finish. Picks are saved.
  The animated penguin is a free CC0 Gobkit model in `Characters/` (see `Characters/CREDITS.txt`) and needs the
  **glTFast** package: *Window > Package Manager > + > Install package by name* > `com.unity.cloud.gltfast`.
  Without it the penguin does not show up.

## Setup

1. In Unity Hub create a new project with the **Universal 3D** template (or **3D (Built-In)**). Unity 2022.3 LTS or Unity 6 recommended.
   If you create the project from the terminal with `Unity -createProject` instead, it has no packages, so add
   `"com.unity.ugui": "2.0.0",` to the `dependencies` in `Packages/manifest.json` (use `1.0.0` on Unity 2022).
2. Copy the `Assets/FartRaft` folder from this repo into your project's `Assets` folder.
3. In Unity's top menu click **Fart Raft > 1. Configure Project For iOS and Android**.
4. Click **Fart Raft > 2. Build Game Scene**. This generates all stages (course, raft, characters and UI) as `Assets/FartRaft/Scenes/Stage1.unity`, `Stage2.unity` and `Stage3.unity`, and adds them to the build.
5. Press **Play**. Keyboard controls in the editor:

   | Player | Hold a direction (= joystick) |
   | --- | --- |
   | 1 | W A S D |
   | 2 | Arrow keys |
   | 3 | I J K L |
   | 4 | Numpad 8 4 5 6 |

When updating an existing project, delete the old `Assets/FartRaft/Scripts` and `Assets/FartRaft/Editor`
folders before copying the new ones (removed scripts would otherwise stay behind and break compiling).

   To test real touch controls, install the **Unity Remote 5** app on your phone and pick it in
   *Project Settings > Editor > Device*.

## Better graphics

- **Fart Raft > 3. Upgrade Graphics To URP** installs the Universal Render Pipeline (click it again once the
  install finishes), switches the project to it and rebuilds the scene with bloom, tonemapping and color grading.
- **Free Asset Store packs are picked up automatically.** Import any of these (Window > Package Manager > My Assets),
  then run **Fart Raft > 2. Build Game Scene** again:
  - [EmbersStorm – Free Nature Pack](https://assetstore.unity.com/packages/3d/environments/embersstorm-free-nature-pack-369910):
    oaks and flowers (Sunny), pines and plants (Jungle), dead trees (Volcano), rocks everywhere.
  - [3D Low Poly Nature Packs Free](https://assetstore.unity.com/packages/3d/environments/landscapes/3d-low-poly-nature-packs-free-327057): extra trees and rocks.
  - [Beach Party & Fun Props Pack](https://assetstore.unity.com/packages/3d/props/exterior/beach-party-fun-props-pack-80918):
    umbrellas, chairs and sand castles on the Sunny cliffs, and the rolling beach ball.
  - [Quirky Series – FREE Animals Pack](https://assetstore.unity.com/packages/3d/characters/animals/quirky-series-free-animals-pack-178235):
    animals watching from the cliffs, and the jumping fish (herring).
  The Console shows how many models each stage found. Models are sized automatically, and Built-in materials are
  converted when the project uses URP (and the other way around). These packs are not part of this repo.
- To choose models yourself, select `Assets/FartRaft/EnvironmentTheme` and drag prefabs into its lists; non-empty
  lists win over the automatic picks. `Prop Scale` makes every model bigger or smaller.
- Rebuilding replaces the `Stage*.unity` scenes, so make manual scene edits in a copy.

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
