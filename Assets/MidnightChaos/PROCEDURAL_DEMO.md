# ProceduralDemo

`ProceduralDemo` is a standalone procedural-generation showcase. It does not
spawn a Player and does not depend on `StylizedNatureMapGenerator`.

`ProceduralCombatDemo` is the separate network combat scene. The Host spawns
one `DiagnosticNetworkPlayer` per connected client only after world generation,
runtime NavMesh building, and spawn validation have completed. Recreate first
despawns players and enemies, then respawns players after the replacement world
is ready. Enemy spawn points are still markers only; use **Spawn Enemy** on the
Host to create enemies manually.

## Open and run

1. Open `Assets/MidnightChaos/Generated/Scenes/ProceduralDemo.unity`.
2. Enter Play Mode.
3. Start one instance as **Host**. Start another build/editor instance as
   **Client**, using the Host LAN address and port from the settings asset.
4. Only the Host can use **Recreate** and **Spawn Enemy**.

The world order is fixed:

`seed/layout -> terrain and obstacles -> runtime NavMesh -> validate spawn points -> ready`

Enemy spawn-point generation is part of the layout, but no enemy is spawned
automatically. **Spawn Enemy** creates one network enemy at the next valid point
after the runtime NavMesh is ready.

## Configuration

Edit
`Assets/MidnightChaos/Resources/Procedural/ProceduralWorldSettings.asset` to
change map size, terrain shape, prefab catalogs, category counts and clearances,
spawn-point counts, NavMesh values, enemy limit, and LAN port.

Each catalog slot now references a `WorldObjectDefinition` in
`Assets/MidnightChaos/Definitions/World/Procedural`. A definition owns only the
stable string ID, category, prefab reference, and static flags. Repeated
definition references in a catalog are intentional deterministic weights; do
not sort or remove them casually. Runtime transform data owns a global,
contiguous `LayoutIndex` and never uses a prefab index as identity.

The complete outer rim is flattened to one deterministic elevation:
`Base Height - Edge Drop + Edge Height Offset`. `Edge Falloff Start` still
controls where flattening begins, while `Edge Drop` remains the base amount of
lowering toward the rim.

Tree, rock, and ore prefabs must contain a child named exactly `BottomPoint`.
Its position is the terrain contact point and its rotation is the frame aligned
to the generated surface. Vegetation may omit it and uses renderer-bounds
fallback placement.

Use `Dynamic Carving` for destructible environment objects. Add and tune a
`NavMeshObstacle` on every prefab and enable `Carving`. Runtime generation does
not create missing obstacles and treats them as an invalid prefab contract.
The current catalog prefabs already have authored obstacles; do not run
`Migrate Missing Obstacles Into Prefabs` for the metadata migration. Existing
authored obstacles are never overwritten. Dynamic objects also receive a
`NavMeshModifier` so their child colliders are not baked into the static
NavMesh before carving.

Configure each enemy's `NavMeshAgent` directly on its prefab. Its Agent Type ID
must match `Nav Mesh Agent Type Id` in the settings. Keep the prefab agent
disabled: the Host enables it immediately before spawning, while Client copies
remain disabled because only the Host owns navigation.

Enemy tuning now lives in
`Assets/MidnightChaos/Definitions/Enemies/FireMageMeleeEnemy.asset`. It owns the
Fire Mage visual, patrol radius/speed/wait, detection and lose ranges, LOS mask,
chase/repath values, combat values, and animation state names. Builder refreshes
preserve authored values in this asset. The capsule renderer is hidden but its
collider remains the gameplay collider; Fire Mage is instantiated locally under
`VisualRoot`, with root motion forced off on every peer.

Enemy AI is Host-authoritative:

`Patrol -> Chase -> Attack/Recover -> Chase`, returning to patrol around its
original spawn when no valid target remains. It evaluates nearest living players
at a fixed interval using distance plus line of sight, applies a short blocked-LOS
grace, and throttles `SetDestination`. Attack damage remains immediate on the
server; a replicated attack sequence drives the visual animation without adding
`NetworkAnimator`.

`DiagnosticEnemyDebugGizmos` can be Off, Selected Only, or Always. Independent
toggles visualize patrol area, detection/lose/attack ranges, target and retarget
candidate, LOS hit, current destination, NavMesh path, and agent validity.

The settings Inspector includes field tooltips and a
`Validate Procedural Prefab Contracts` button for anchors, colliders, obstacles,
and enemy Agent Type checks.

When a configuration or generation rule changes, increment `Generator Version`
before distributing a build. Host and Client also compare a deterministic layout
hash, including the ordered stable-definition catalogs.
Metadata migration is generation version 4: the numeric hash intentionally
changes from version 3 because stable definition IDs, flags, and `LayoutIndex`
replace prefab names and indices in the hash. Host and Client equivalence is
preserved only when both builds use the same version-4 definition catalog.

## Rendering performance

The project reserves these layers in `ProjectSettings/TagManager.asset`:

- `Vegetation` (8)
- `Tree` (9)
- `SmallProp` (10)
- `Resource` (11)
- `Grass` (12)

Do not rename or reorder them without updating the procedural setup. Camera
distance culling uses these layers and spherical culling. All distances, camera
far clip, vegetation chunk size, vegetation shadows, GPU instancing, and tree
particles are configurable under **Rendering Performance (Local Only)** in the
world settings asset. These settings are intentionally excluded from the layout
hash because they do not change placement, physics, networking, or NavMesh.

With `Use Instanced Vegetation` enabled, decorative Vegetation and Grass store
the same deterministic transforms but do not create one GameObject per
instance. Draw batches remain separated by category, mesh, material, submesh,
and LOD, then use `Graphics.RenderMeshInstanced`. Grass source-prefab physics,
network, or script components are ignored by the GPU path and reported as
warnings; invalid metadata or missing instanced render data are errors.

Grass uses its own deterministic random stream. Each cluster selects one Grass
definition, while position, yaw, terrain normal, spacing, and uniform scale vary
per instance. Uniform scale changes height, width, and depth together; it is not
height-only scaling.

The runtime debug UI reports logical vegetation count, vegetation GameObject
count, chunks, and prepared draw groups. For the default catalog, the required
result is `GameObjects=0`. The number of prepared draw groups is not the number
submitted each frame: distance and Unity frustum culling reject whole chunks.

## Verification gate

Run the EditMode tests in `MidnightChaos.Procedural.EditModeTests`, then perform:

| Test | Required result |
| --- | --- |
| Host run 1 | World and NavMesh become ready |
| Host run 2 | World and NavMesh become ready |
| Host + one Client | Seed and layout hash match |
| Late Client join | Current descriptor regenerates the same world |
| Recreate/restart | Previous generated root and enemies are cleared |
| Instanced plants | Grass=8,000, Vegetation=2,000 and both `GameObjects=0` |
| Grass clusters | Cluster totals, per-definition totals, placed/rejected metrics are coherent |
| Camera distance culling | Grass/vegetation/tree/resources disappear at configured distances |
| Render-only settings | Seed, Revision, and Layout Hash remain unchanged |
| Combat Host | Player spawns only after `World ready`; manual enemy patrols |
| Combat Host + Client | Both players replicate; enemy target/chase agrees |
| Combat recreate | Old players/enemies despawn; players respawn on new map |
| LOS obstacle | Enemy does not acquire through solid geometry |

For each LAN run, also confirm that enemies appear only after pressing the Host
button and that `Spawn Enemy` remains disabled until NavMesh reports ready.
