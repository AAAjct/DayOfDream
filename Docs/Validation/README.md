# Editor validation — 2026-11-03

This section records the original movement prototype. The exploration check script and its JSON output now reflect the updated controls, including no jump and sprint bob.

- Unity 6000.3.27f2 compiled the added runtime and Editor scripts without errors.
- `exploration_checks.json`: 18/18 real Play Mode checks passed. Temporary virtual input devices were used; original input devices were restored afterward.
- Walking covered approximately 2.65 m in 1.1 s; sprinting covered approximately 4.95 m. The measured camera height variation across the tested flat paving section was 0 m after removing forced head bob and using continuous ground collision.
- E opened both door leaves to approximately ±100°. The player crossed the threshold and reached inside the teaching block at approximately `(0.65, 0.58, -18.29)`.
- Space produced an approximately 1.02 m jump, ignored a second mid-air press, and landed on the original ground level.
- F toggled the actual spotlight. `flashlight_on.png` and `flashlight_off.png` capture the same door-facing camera pose. The scene's reflection intensity is now 0.025, moon intensity 0.045, and spotlight intensity 240. These are art settings, not physical photometric measurements.
- `school_manifest.json` records the exported labels: `鹅 企 中 学`, `鹅企中学`, and `EQI MIDDLE SCHOOL`. Asset filenames retain their original Haojie identifiers so references remain stable.
- The final Editor Console check reported no errors or warnings after the diagnostic history was cleared.

The images beginning `before_` or `night_after_` are intermediate diagnostic captures. `eqi_school_sign.png` shows the final exported sign. This is Editor validation; a standalone build, hardware frame-time profile and long exploratory playthrough were not performed.
