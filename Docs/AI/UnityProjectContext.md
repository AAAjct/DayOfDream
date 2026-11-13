# DayOfDream project context

Last inspected: 2026-11-13. Workspace: `D:/unity/unity_file/project/DayOfDream`.

- Confirmed Unity 6000.6.9f1, URP 17.6.0, Input System 1.20.1, active input handler = Input System.
- The repository originally contained SampleScene and imported assets, without first-party gameplay. Mirror, SaveGameFree and DOTween are retained as vendor assets. The current exploration prototype is single-player.
- Runtime code lives in `Assets/Scripts/Horror`, namespace `DayOfDream.Horror`, within Assembly-CSharp. Editor scene creation and polish tools live in `Assets/Editor/Horror`, within Assembly-CSharp-Editor.
- `FirstPersonExplorer` owns input, CharacterController movement, camera look and interaction. It clones the existing Player action map; C toggle crouch, F flashlight and Esc pause are local additions. `HandheldFlashlight`, `HingedDoor`, `ReadableNote`, `HorrorHud`, `TomorinFirstPersonBody` and `SchoolLightFlicker` own their respective presentation/interaction responsibilities.
- The build list starts with `Assets/Scenes/MainMenu.unity`, followed by `Assets/Scenes/AnonHouse.unity` and `Assets/Scenes/HaojieSchool_Night.unity`. The self-contained `Assets/Prefabs/Horror/TomorinExplorer.prefab` contains camera, Tomorin body, flashlight and HUD. `DreamMainMenu` owns menu navigation/settings; `DreamProgress` stores the local exploration checkpoint. See `Docs/Validation/DREAM_UPGRADE.md` for the inventory, bedroom and battery changes.
- `com.unity.pipeline` 1.0.0 is the installed Editor bridge. The local Unity CLI can drive this exact project. Project-authored Pipeline port is 17900 because Windows excluded 7729–7928, including the entire automatic 7800–7849 range. Do not edit PackageCache or remove OS exclusions to solve this.
- Scene creation uses Unity Editor APIs, never manually authored scene YAML. The builder refuses to overwrite an existing scene or discard dirty open scenes. `HorrorSchoolPolish.Apply()` applies the current night lighting, doors, smooth ground and access platforms to the existing school scene.
- Gameplay validation runs in the real Editor using `Tools/ValidateExploration.cs` through `unity command script eval_file`; results are recorded under `Docs/Validation`. Compilation and these checks are separate from a standalone Windows build, which has not been validated.
- Existing user changes to vendor locations, render settings, project settings, deleted template assets and packages predated this task. Preserve them.

Evidence: `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`, `ProjectSettings/EditorBuildSettings.asset`, actual Editor inspection and first-party source files above.

- 2026-11-13 house expansion: four total levels (basement -3 m, ground 0 m, second 3.3 m, attic 6.6 m) plus courtyard, 26 actor-relative swinging doors and a continuous switchback stair tower. Expanded architecture is authored by ExpandedHouseBuilder and exported through Tools/export_anon_house.py. Existing puzzles remain on their original floors; basement breaker moved to z=-12.5 to clear the new passage.
- Bedroom safety: ordinary patrol excludes the bedroom; seeing a quiet player there is ignored. Only an audible room noise grants temporary entry. Walking and crouching are silent; running is a quiet 4 m cue. C toggles crouch. BedroomExperienceUpgrade owns the enlarged bed, removed headboard, dim ceiling fixture, nightstand, shared deluxe DV and wake markers.
- Waking and execution presentation uses PlayerConsciousness, ConsciousnessRenderFeature, Consciousness.shader and generated ClubAttack. HUD has four square objective slots; DV is equipment outside those slots.
