# Unity Conventions

Engine-level rules every agent obeys. They exist for two reasons: **merge safety** and
**token cost**. Both are non-negotiable.

---

## 1. Project layout

All first-party content lives under `Assets/_Project/`. Third-party packages stay where
the importer put them and are **never edited** (patch only via an ADR).

```
Assets/_Project/
  Code/
    Gameplay/        gameplay systems, player, entities        [asmdef Game.Gameplay]
    Systems/         save, scene flow, config, input, audio    [asmdef Game.Systems]
    AI/              agents, behaviours, navigation            [asmdef Game.AI]
    Net/             networking, replication                   [asmdef Game.Net]
    UI/              HUD, menus, presenters                    [asmdef Game.UI]
    Editor/          editor tools, importers, build            [asmdef Game.Editor]
    Tests/
      EditMode/      pure logic tests                          [asmdef Game.Tests.EditMode]
      PlayMode/      integration and scene tests               [asmdef Game.Tests.PlayMode]
  Config/            ScriptableObject assets (generated from /config)
  Data/              runtime-readable JSON copies (StreamingAssets mirror)
  Art/
    Concept/         non-shipping reference images
    Generated/       raw ComfyUI output, before cleanup
    Textures/ Sprites/ Materials/ Models/ VFX/ UI/
  Audio/             Music/ SFX/ Mixers/
  Prefabs/           Gameplay/ UI/ VFX/ Net/
  Scenes/            Boot/ Menu/ Levels/ Sandbox/
  Settings/          render pipeline, input actions, quality
```

**Dependency direction:** `UI -> Gameplay -> Systems`, `AI -> Gameplay -> Systems`,
`Net -> Gameplay -> Systems`. `Systems` depends on none of our other assemblies.
Reverse dependencies are forbidden; asmdefs enforce it and the compiler is the gate.

---

## 2. Scenes and prefabs are machine files

**Rule: never open a `.unity`, `.prefab`, `.asset` or `.meta` file for reading.**

| Need | Command |
|---|---|
| What is in this scene | `.claude/tools/unity-inspect.ps1 -Path Assets/.../Level_01.unity` |
| What is in this prefab | `.claude/tools/unity-inspect.ps1 -Path Assets/.../Player.prefab` |
| Who references X | `.claude/tools/unity-inspect.ps1 -FindReferences <guid or path>` |
| What assets exist | `.claude/tools/asset-index.ps1` |

Writing them by hand is worse than reading them: GUIDs and fileIDs have to stay
internally consistent. **Agents author scenes and prefabs through editor scripts**
in `Assets/_Project/Code/Editor/`, never by editing YAML.

If a change genuinely needs manual scene work, the story says so and the **user** does
it in the Editor; the story records what was done.

### Scene discipline
- One **Boot** scene loads everything; levels load additively.
- No cross-scene references. Wiring goes through a ScriptableObject or a registry.
- A scene holds placement, not logic. Logic lives on prefabs.
- Scenes produce the worst merge conflicts in Unity: **one story owns one scene**.

### Prefab discipline
- Prefer prefab variants over duplicated prefabs.
- A prefab exposes serialized fields for **references**, not for balance numbers.
  Numbers come from config - see `config-protocol.md`.
- Nesting beyond three levels needs a stated reason.

---

## 3. C# and MonoBehaviour rules

- No work in `Update()` that could be event-driven. Per-frame work is a budget item.
- **Zero per-frame allocation** in gameplay code: no LINQ, no boxing enumerators in hot
  paths, no string concatenation, no `GetComponent` in `Update`. Cache in `Awake`.
- `[SerializeField] private` over `public`. A public field is an API you did not design.
- No `GameObject.Find`, no `SendMessage`, no `Resources.Load` in new code.
- Singletons only for genuine engine-level services, and then behind a Systems-layer
  service locator that tests can substitute.
- Coroutines for scene and sequence flow; `async`/`await` with a cancellation token for
  I/O. Do not mix the two inside one subsystem.
- Physics in `FixedUpdate`, input reading in `Update`, camera in `LateUpdate`.
- Anything that can be a `struct` under 16 bytes should be one.

---

## 4. Determinism and save compatibility

- Never use `Random` without an explicit seeded stream for anything a player can
  replay, share or save.
- Save data is versioned. A migration is written in the same story that changes the
  schema. Loading an old save must not throw.
- Time of day, physics steps and network ticks derive from one clock. Do not scatter
  `Time.time` across systems.

---

## 5. Assets and import

- Naming: `<category>_<subject>_<variant>`, for example `tex_crate_wood_01`,
  `sfx_ui_click_01`, `prefab_enemy_scout`. Lowercase, underscores, ASCII only.
- Textures: power of two, compressed, mip maps off for UI, sRGB off for masks.
- Every art asset has a budget entry in `docs/art/ASSET-BUDGET.md`.
- Import settings are enforced by an `AssetPostprocessor`, never set by hand.

---

## 6. Version control

- `.gitattributes` marks Unity YAML for `unityyamlmerge`. `Library/`, `Temp/`, `Logs/`
  and `Build/` are ignored.
- Never commit `Library/`. Never commit a build. Never commit generated art that has
  not been through `/gen-asset` cleanup.
- `git push` always requires user approval.

---

## 7. Verification

An agent that changed C# must prove it compiles:

```
.claude/tools/unity-log.ps1 -Errors
```

If the Unity Editor is not running, say so. Do not claim it compiles. "It should work"
is not verification. See `.claude/docs/definition-of-done.md`.
