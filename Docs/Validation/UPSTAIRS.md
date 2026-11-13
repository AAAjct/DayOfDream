# Eqi School expanded exploration — 2026-11-03

The school now connects the main block, both side wings, three classroom floors, and rooftop access through both external stair routes.

The evaluated Blender export is cut geometrically to open the continuous rear corridors and 96 classroom entrances. This works on beveled source meshes; the earlier six-face-box heuristic alone could leave partitions intact. The source `.blend` is unchanged.

Unity uses thin sloping stair slabs with sloping undersides, sufficient space to enter the next flight from the return walkway, continuous corridor floors, and rooftop landing/wing links. Disabling the decorative tread collider avoids snagging individual risers. The slab undersides keep upper flights from blocking a player's head halfway up the flight below. Obsolete wing end guards are disabled so they do not close the wing corridors.

`upstairs_routes.json` records 49 passing checks: all 96 classroom entrance casts, continuous main/wing corridor casts on classroom floors and roofs, and 36 actual CharacterController waypoint checks covering both ground-to-rooftop routes. The final recorded rooftop foot height is approximately 13.18 m.

The scene contains floor/roof wayfinding, guide lights, and readable records in the archive, second-floor office, third-floor broadcast room, and rooftop. This is an Editor-validated exploration prototype, without enemies or a completed story progression system. Existing standalone builds have not been rebuilt as part of filming.
