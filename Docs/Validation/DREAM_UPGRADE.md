# Inventory, bedroom and main menu

Validated in the real Unity 6000.6.9f1 Editor on 2026-11-13.

- MainMenu is the first enabled build scene. The four buttons are Start, Continue, Settings and Quit. Start loads AnonHouse; Continue restores the local exploration checkpoint. Continue is disabled without a readable checkpoint. Settings saves volume and mouse sensitivity. Quit exits the player, or stops Play Mode in the Editor.
- The menu uses the existing Anon model with separate lit materials, idle animation, a doorway set and a flickering practical light. The fixture emission follows the light intensity.
- The four inventory slots contain images rendered from the actual item models. Empty slots have no text, numbers or placeholder glyphs. Keys 1–4 still select slots. Q drops/throws ordinary items without drawing a trajectory.
- DV and spare batteries are permanent equipment. Their charge/count appears above the inventory; neither is exposed to the objective-slot drop operation. They remain with the player across death/night transitions.
- Eight house battery supplies span the bedroom, study, kitchen, hall, upper floor, cellar and exterior approach. Collected supplies hide their meshes and triggers, then return after 90 seconds. Pickup respects the existing capacity of ten spares.
- The bedroom has one enabled practical light at intensity 0.38, with its previous practical/window fill disabled. The bed uses separate walnut, cotton and quilt materials, plus a mesh quilt with folds and draped sides.
- The single local JSON checkpoint records position, day, objective inventory/world objects, cabinet/power state and DV/battery state. It saves every twenty seconds during safe standing exploration, when pausing, and on quit when gameplay is eligible. Starting a new game replaces this checkpoint. Continue resumes without replaying the initial wake animation. Battery supply cooldowns restart with scene loading.

Validation evidence: `output/dream_upgrade/verification-summary.json`, `validation.json`, and `drop-atomic.json`. Twenty-three background runtime checks passed. The background synthetic Q dispatch did not reach the gameplay tick, so Q was verified separately by explicitly advancing Input System Dynamic input: slot selection, charge, release/drop, no trajectory and retained equipment all passed. No runtime errors were captured by that runtime test. Final Unity compilation reported zero errors and zero warnings.

Images: `output/dream_upgrade/mainmenu-final.png`, `bedroom.png` and `bedroom-inventory.png`. A standalone player build was not performed.
