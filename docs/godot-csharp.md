# Working with Godot as text (C#)

The owner never opens the Godot editor. Every `.tscn`, `.tres`, `.cs` file and `project.godot` is text, and the agent edits it directly. Read this before creating or changing scenes and resources.

## Files

- **Never hand-invent `uid://` values.** In new scenes and resources, reference `ext_resource` by `path` only and omit uids, or let `godot --headless --editor --quit` generate them.
- Godot writes `*.cs.uid` files next to scripts. Commit them.
- Never edit `.godot/` or `*.import` files (generated). Do commit `*.import` files.
- Bulky scene text (like a room layout) may be generated with a small script. Keep the generated `.tscn` readable, because the `.tscn` is the source of truth.
- `.tscn` `Transform3D(...)` values are the basis **row-major** (`x.x, y.x, z.x, x.y, y.y, z.y, x.z, y.z, z.z`) followed by the origin.
- Folders Godot must not scan (docs, tests) contain an empty `.gdignore`.

## C# rules the compiler won't always flag

- Every Godot script class is `public partial class`, and its name matches its file name exactly (`Player.cs` → `class Player`), or Godot can't attach it.
- Custom resources are `[GlobalClass] public partial class X : Resource` with `[Export]` properties, and their `.tres` files use `script_class="X"`.
- After adding or renaming a class, rebuild before referencing it from a scene.
- Put `///` doc comments *before* all attributes (for example before `[ExportGroup]`), not between them.
- Plain C# classes (states, pure logic) don't need `partial` and can be created with `new`. Godot script classes can also be created with `new` (`new SmokeDriver { ... }`).

## No code the compiler can't see

- No string node paths across scenes (`GetNode("A/B")`). Use typed `[Export] public NodeType X { get; set; } = null!;` references. In the `.tscn`, list them in the node's `node_paths=PackedStringArray("X")` and assign `X = NodePath("../Y")`.
- Within one scene, `GetNode<T>("%UniqueName")` with `unique_name_in_owner = true` is fine.
- Connect signals with C# events (`button.Pressed += OnPressed;`), not `Connect("name", ...)`. Unsubscribe in `_ExitTree` when the source outlives the listener.
- Use the generated `SignalName.X` / `PropertyName.X` constants, never string literals. Input actions come from `InputActions`.

## Shaders

- Global shader uniforms are declared in `project.godot` under `[shader_globals]` and named in `ShaderGlobals`. Godot passes `color` globals to shaders **without** sRGB-to-linear conversion (unlike material colors and `source_color` uniforms), so a shader using one as albedo must convert it (see `srgb_to_linear` in `assets/shaders/biome_surface.gdshader`). Values stay in sRGB everywhere they are written.
- `.tscn` and `.tres` files accept `;` comment lines after the header line. Use them sparingly, for instance to say what a photo spot shows.

## Physics interpolation

It is on project-wide. Move physics bodies, and the visuals under them, in `_PhysicsProcess`. A node moved in `_Process` (like the camera) sets `PhysicsInterpolationMode = Off` and reads targets with `GetGlobalTransformInterpolated()`. After a teleport, call `ResetPhysicsInterpolation()`.

## Regenerating the C# project

`godot --headless --build-solutions --quit` does **not** create a missing `.csproj` in 4.7.2: Godot only generates it when the editor saves a *new* C# script. If it ever has to be regenerated, run a throwaway `extends SceneTree` script with `godot --headless --editor --script x.gd` that `ResourceSaver.save()`s a new `CSharpScript` to a path that doesn't exist yet. Afterwards, re-add the project's edits:
- `<Nullable>enable</Nullable>`
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`
- `<RootNamespace>TheDeep</RootNamespace>`
- `<Compile Remove="tests/**" />`
