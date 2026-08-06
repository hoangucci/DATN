# ProceduralDemo

`ProceduralDemo` is a standalone procedural-generation showcase. It does not
spawn a Player and does not depend on `StylizedNatureMapGenerator`.

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
Use `Migrate Missing Obstacles Into Prefabs` once to write missing components
to the referenced prefab assets, then review `Center`, `Size`, and `Shape` on
each model. Existing authored obstacles are never overwritten. Dynamic objects
also receive a `NavMeshModifier` so their child colliders are not baked into the
static NavMesh before carving.

Configure each enemy's `NavMeshAgent` directly on its prefab. Its Agent Type ID
must match `Nav Mesh Agent Type Id` in the settings. Keep the prefab agent
disabled: the Host enables it immediately before spawning, while Client copies
remain disabled because only the Host owns navigation.

The settings Inspector includes field tooltips and a
`Validate Procedural Prefab Contracts` button for anchors, colliders, obstacles,
and enemy Agent Type checks.

When a configuration or generation rule changes, increment `Generator Version`
before distributing a build. Host and Client also compare a deterministic layout
hash, including the ordered prefab catalogs.

## Rendering performance

The project reserves these layers in `ProjectSettings/TagManager.asset`:

- `Vegetation` (8)
- `Tree` (9)
- `SmallProp` (10)
- `Resource` (11)

Do not rename or reorder them without updating the procedural setup. Camera
distance culling uses these layers and spherical culling. All distances, camera
far clip, vegetation chunk size, vegetation shadows, GPU instancing, and tree
particles are configurable under **Rendering Performance (Local Only)** in the
world settings asset. These settings are intentionally excluded from the layout
hash because they do not change placement, physics, networking, or NavMesh.

With `Use Instanced Vegetation` enabled, decorative vegetation stores the same
deterministic transforms but does not create one GameObject per instance. It is
grouped by chunk, mesh, material, submesh, and LOD, then submitted with
`Graphics.RenderMeshInstanced`. A prefab whose material does not support GPU
instancing falls back to the legacy GameObject path and emits one warning.

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
| Instanced vegetation | Logical count remains 10,000 and `GameObjects=0` |
| Camera distance culling | Vegetation/tree/resources disappear at configured distances |
| Render-only settings | Seed, Revision, and Layout Hash remain unchanged |

For each LAN run, also confirm that enemies appear only after pressing the Host
button and that `Spawn Enemy` remains disabled until NavMesh reports ready.
