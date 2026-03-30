# Coding Standards

S1MAPI is a mesh and building construction library for Schedule 1 mods.
The core principle: **no compile-time dependencies on ScheduleOne types** to remain resilient across game updates.
S1MAPI uses Unity primitives and FishNet only—no `Assembly-CSharp` imports. Where game-type
interaction is unavoidable, use reflection with graceful fallbacks.

## General Best Practice
* Review the codebase thoroughly before submitting a PR.
* Keep S1MAPI free of ScheduleOne type dependencies.
* S1MAPI handles meshes and buildings; S1API handles game component integration.
  * Mods typically use both: S1MAPI for construction, S1API for game logic.

## File and Namespace Structure
* All classes must exist in a logical namespace matching the folder structure.
* Core initialization and resource management in `S1MAPI.Core`.
* Mesh generation in `S1MAPI.ProceduralMesh`.
* Building construction in `S1MAPI.Building`.
* GLTF loading in `S1MAPI.Gltf`.
* Utilities in `S1MAPI.Utils`.

```csharp
namespace S1MAPI.ProceduralMesh { ... }
namespace S1MAPI.Building { ... }
```

## Naming Conventions
* In general, naming follows standard C# conventions and Jetbrains Rider suggestions.
* **PascalCase** for class names, methods, properties, and non-private fields.
* **camelCase** for local variables and parameters.
* Prefix private and internal fields with `_`.

```csharp
private readonly List<Vector3> _vertices;
private Material? _material;
internal GameObject _rootObject;
```

* Static readonly fields use PascalCase.

```csharp
private static readonly HashSet<UnityEngine.Object> _trackedResources;
public static readonly string DefaultMeshName = "ProceduralMesh";
```

* Use nested static classes for constants instead of flat constant lists.

```csharp
public static class Constants
{
    public static class Mesh
    {
        public const int MaxVerticesPerMesh = 65535;
        public const int DefaultCylinderSegments = 12;
    }
}
```

* Enums do not need to be prefixed with `E`.
* Prefer descriptive names without redundant suffixes:
  * `MeshBuilder` not `MeshBuilderManager`
  * `BuildingUtilities` not `BuildingUtilitySystem`

## Access Modifiers
* Public API classes, methods, and properties should use `public` with XML documentation.
* Internal implementation details must be marked as `internal`.
* Use `sealed` on classes when inheritance is not intended.

```csharp
public sealed class ProceduralMeshBuilder { ... }
```

* Arrow functions (`=>`) are placed below the declaration, indented once.

```csharp
public static int TrackedResourceCount =>
    _trackedResources.Count;

public GameObject GetRoot() =>
    _root;
```

* Use `readonly` or `const` for immutable values.
* Nullable variables should be declared using `?`.

```csharp
private Material? _material;
public Material? DefaultMaterial { get; set; }
```

## Fluent Builder Pattern
S1MAPI uses fluent builders as its primary API style. Builders should:
* Return `this` from configuration methods for chaining.
* Place configuration methods before terminal `Build()` methods.
* Provide sensible defaults so minimal configuration is required.

```csharp
GameObject building = new BuildingBuilder("MyShop")
    .SetFootprint(6f, 9f)
    .SetHeight(6f)
    .AddFloor()
    .AddWalls()
    .AddRoof()
    .Build(position, rotation);
```

## Resource Management

Unity automatically handles resource cleanup when GameObjects are destroyed. Use `S1MAPI.Utils.DebugLog` for logging:

```csharp
DebugLog.Info($"Built mesh: {_name} ({_vertices.Count} vertices)");
DebugLog.Warning("Attempted to register null resource");
DebugLog.Error($"Failed to load GLTF: {path}");
```

## FishNet & Networking
S1MAPI uses FishNet for networked prefabs (e.g., doors that sync across clients).
* NetworkBehaviour components should follow FishNet conventions.
* Prefabs intended for network spawning must be registered appropriately.
* Keep networking logic separate from mesh generation logic.

## Documentation
* All public API declarations must have XML documentation summaries.

```csharp
/// <summary>
/// Add a box (cube) to the mesh.
/// </summary>
/// <param name="center">Center position of the box</param>
/// <param name="size">Size of the box</param>
/// <returns>This builder for method chaining</returns>
public ProceduralMeshBuilder AddBox(Vector3 center, Vector3 size) { ... }
```

* Internal members should have brief documentation explaining their purpose.

## Conditional Build Compilation
* Use `#if IL2CPP` and `#if MONO` for platform-specific logic when necessary.

```csharp
#if IL2CPP
using Il2CppUnityEngine;
#elif MONO
using UnityEngine;
#endif
```

* Use `#region` and `#endregion` to organize code sections.

```csharp
#region Fields
private readonly string _name;
private readonly List<Vector3> _vertices;
#endregion

#region Public API
public ProceduralMeshBuilder AddBox(...) { ... }
#endregion
```

## Code Organization
* Organize class members in this order:
  1. Fields (private, internal, public)
  2. Constructors
  3. Properties
  4. Public API methods
  5. Private/internal helper methods
  6. Event handlers

* Group related members together using regions.

## What **NOT** to Do
* **Do not** add compile-time references to ScheduleOne types (`Assembly-CSharp.dll`).
  S1MAPI must remain update-resilient—no `using ScheduleOne.*` imports or direct type usage.
  When interaction with game types is unavoidable (e.g. NPC navigation), use **reflection**
  with graceful fallbacks so the code degrades safely if the game changes.
* **Do not** use magic strings—prefer enums or constants.
* **Do not** ignore compiler warnings.
* **Do not** leave commented-out code in commits.
* **Do not** use `var` when the type is not immediately obvious.

```csharp
// Bad
var x = GetSomething();

// Good
MeshData x = GetSomething();

// Also good (type is obvious)
var builder = new ProceduralMeshBuilder("Test");
```

## Git Commit Standards
* Follow Conventional Commits format with module scope.

```
feat(ProceduralMesh): add cylinder mesh generator
fix(Building): null reference in BuildingBuilder.Build
docs(Core): update core documentation
refactor(Gltf): extract node processing logic
```
