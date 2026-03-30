# API Overview

This document provides a high-level overview of S1MAPI's main APIs and namespaces.

## Namespace Structure

S1MAPI is organized into logical namespaces that mirror the folder structure:

| Namespace | Purpose |
|-----------|---------|
| `S1MAPI.Core` | Main entry point, resource tracking, mesh/prefab references |
| `S1MAPI.ProceduralMesh` | Fluent builders for generating procedural meshes |
| `S1MAPI.Building` | Building construction, walls, floors, furniture |
| `S1MAPI.Gltf` | GLTF/GLB file loading and processing |
| `S1MAPI.Utils` | Material presets, constants, debug logging |
| `S1MAPI.Extensions` | Extension methods for Unity types |
| `S1MAPI.S1` | Schedule 1 specific meshes and materials |

## Core API (S1MAPI.Core)

The `S1MAPI` static class provides library metadata:

```csharp
public static string Version => Constants.LIBRARY_VERSION;
public static string Name => Constants.LIBRARY_NAME;
```

## Procedural Mesh API (S1MAPI.ProceduralMesh)

### ProceduralMeshBuilder

The `ProceduralMeshBuilder` class creates custom meshes using a fluent API:

```csharp
new ProceduralMeshBuilder("Name")
    .AddBox(center, size)
    .AddSphere(center, radius, subdivisions)
    .AddCylinder(start, end, radius, segments)
    .AddCapsule(start, end, radius)
    .SetMaterial(material)
    .SetColor(color)
    .ApplyFlatShading()
    .Build();        // Returns GameObject
    .BuildMesh();    // Returns Mesh only
```

**Key Methods:**
- `AddBox(Vector3 center, Vector3 size)` - Add a cube
- `AddSphere(Vector3 center, float radius, int subdivisions)` - Add a sphere
- `AddCylinder(Vector3 start, Vector3 end, float radius, int segments)` - Add a cylinder
- `AddCapsule(Vector3 start, Vector3 end, float radius)` - Add a capsule
- `SetMaterial(Material material)` - Apply a custom material
- `SetColor(Color color)` - Create and apply an opaque material
- `ApplyFlatShading()` - Generate hard-edged normals
- `Build()` - Create GameObject with MeshFilter and MeshRenderer
- `BuildMesh()` - Create Mesh object only

### PrimitiveBuilder

The `PrimitiveBuilder` static class creates Unity primitive GameObjects with materials:

```csharp
// Create a primitive with color
GameObject cube = PrimitiveBuilder.CreatePrimitive(
    PrimitiveType.Cube,
    "MyCube",
    localPosition: Vector3.zero,
    localScale: Vector3.one,
    color: Color.red,
    parent: parentTransform
);
```

**Use Cases:**
- Quick prototyping with Unity's built-in primitives (Cube, Sphere, Cylinder, Capsule, Plane, Quad)
- Simpler alternative to ProceduralMeshBuilder for basic shapes
- Supports parenting and material setup in one call

## Building API (S1MAPI.Building)

The `BuildingBuilder` class constructs complete buildings:

```csharp
new BuildingBuilder("Name")
    .WithConfig(BuildingConfig.Medium)
    .WithPalette(buildingPalette)
    .AddFloor()
    .AddCeiling()
    .AddWalls(northDoor: true, southDoor: true, eastWindow: true, westWindow: true)
    .AddRoofTrim()
    .AddCornerPillars()
    .AddFoundation()
    .AddLights(intensity: 1.0f, color: Color.white)
    .AddFurniture(FurnitureType.Desk, "north")
    .AddPrefab(prefab, position, rotation)
    .AddSlidingDoors(position, rotation, "6AM-6PM")
    .Build();
```

**Building Configuration:**
```csharp
// Preset configurations
BuildingConfig.Default
BuildingConfig.Tiny      // 3m x 2.4m x 3m
BuildingConfig.Small     // 5m x 3m x 4m
BuildingConfig.Medium    // 8m x 3m x 6m
BuildingConfig.Large     // 12m x 4m x 10m
BuildingConfig.Huge      // 20m x 6m x 15m
BuildingConfig.Warehouse // Industrial style

// Custom configuration
new BuildingConfig
{
    Width = 10f,
    Height = 4f,
    Depth = 8f,
    WallThickness = 0.2f,
    Palette = customPalette
};
```

**Interior Builder:**
```csharp
var interior = new InteriorBuilder(buildingTransform);
interior.AddDesk(position, rotation);
interior.AddChair(position, rotation);
interior.AddPlant(position, rotation);
interior.AddPrefab(Prefabs.ATM, position, rotation, networked: true);
interior.Build();
```

**Component Builders & Core Systems:**
- `WallBuilder` - Wall and opening generation
- `InteriorWallBuilder` - Interior wall and opening generation
- `InteriorBuilder` - Interior furniture and decoration placement using S1 meshes
- `FurnitureBuilder` - Procedural furniture creation (used internally by BuildingBuilder)
- `LightingBuilder` - Light fixture placement
- `DecorBuilder` - Decorative elements (trim, pillars, foundations, stairs)
- `RoofBuilder` - Parapet and hip roof generation
- `PrefabPlacer` - Prefab instantiation with networking (integrates with `NetworkedPrefabLinker`)
- `NavigationBuilder` & `InteriorPathGrid` - Custom A* NPC Pathfinding
- `TerrainClearer` & `TerrainFlattener` - Environment preparation
- `BuildingPartRegistry` - Post-build querying of generated geometry

## GLTF API (S1MAPI.Gltf)

Load external 3D models:

```csharp
// Load from bytes
GameObject? model = GltfLoader.Load(glbBytes);

// Load GLB specifically
GameObject? model = GltfLoader.LoadGlb(glbBytes);

// Load from file
GameObject? model = GltfLoader.LoadFromFile("path/to/model.glb");

// Load from JSON with base path for external resources
GameObject? model = GltfLoader.LoadFromJson(gltfJson, basePath);

// Using GltfImporter for more control
GameObject? model = new GltfImporter()
    .SetShader(customShader)
    .SetEmissionIntensity(2.0f)
    .Load(glbBytes);
```

## Utilities API (S1MAPI.Utils)

**Material Presets:**
```csharp
// Create materials with common configurations
Material opaque = MaterialPresets.Opaque(color);
Material transparent = MaterialPresets.Transparent(color, alpha: 0.5f);
Material glass = MaterialPresets.Glass(color, alpha: 0.3f);
Material clearGlass = MaterialPresets.ClearGlass(alpha: 0.3f);
Material metal = MaterialPresets.Metal(color, metallic: 0.8f, smoothness: 0.9f);
Material emissive = MaterialPresets.Emissive(color, intensity: 1.0f);

// From texture
Material textured = MaterialPresets.FromTextureName("textureName", color);

// Find existing material
Material? found = MaterialPresets.FindExistingMaterial("materialName");
```

**Embedded Resource Loading:**
```csharp
using S1MAPI.Utils;

// Load embedded resources from your assembly
Texture2D? texture = EmbeddedResourceLoader.LoadTexture("Namespace.Resources.image.png");
Sprite? sprite = EmbeddedResourceLoader.LoadSprite("Namespace.Resources.icon.png", pixelsPerUnit: 100f);
byte[]? data = EmbeddedResourceLoader.LoadBytes("Namespace.Resources.file.bin");
```

## Extension Methods (S1MAPI.Extensions)

Convenience methods for Unity types:

```csharp
// GameObject extensions
gameObject.DestroyChildren();
var children = gameObject.GetChildren();

// Transform extensions
transform.SetLayer(LayerMask.NameToLayer("Default"));
transform.ResetLocalTransform();

// Component extensions
component.DestroyIfExists<MeshRenderer>();
```

## PrefabRef API (S1MAPI.Core)

`PrefabRef` provides safe instantiation of FishNet networked prefabs:

```csharp
using S1MAPI.Core;

// Create a reference to a networked prefab
var atmPrefab = new PrefabRef("ATM");

// Find the prefab in FishNet's registry
GameObject? prefab = atmPrefab.Find();
```

### ⚠️ CRITICAL: Network vs Local Instantiation

**You MUST use the correct instantiation method based on whether the prefab has a `NetworkObject` component:**

```csharp
// For NETWORKED prefabs (has NetworkObject component) - SERVER ONLY
GameObject? atm = atmPrefab.InstantiateNetworked();
atm.transform.position = new Vector3(10, 0, 5);
```

```csharp
// For LOCAL/CLIENT-SIDE objects (no NetworkObject)
GameObject? decoration = decorationPrefab.Instantiate();
```

**WARNING:** Using `Instantiate()` on a networked prefab will **crash FishNet and break multiplayer**. This is not recoverable without restarting the game!

### When to Use Each Method

**Use `InstantiateNetworked()` for:**
- ATMs, doors, storage containers
- Any prefab with multiplayer synchronization
- Interactive game objects
- Any prefab from `S1.Prefabs` namespace

**Use `Instantiate()` for:**
- Static decorations without network sync (rare - prefer using InteriorBuilder or ProceduralMeshBuilder)
- Client-side visual effects
- Local UI elements
- Pure mesh/visual objects without NetworkObject component

### What InstantiateNetworked Does

1. Instantiates the prefab as inactive to prevent `Awake()` crashes
2. Initializes GUID fields to prevent parse errors
3. Spawns the object on the FishNet network
4. Activates the object with valid network state

## S1 Namespace (S1MAPI.S1)

Schedule 1 specific materials and meshes (requires game assembly at build time):

```csharp
// Schedule 1 materials
Material concrete = Materials.ConcreteLightGrey;
Material brick = Materials.BrickWallRed;
Material metal = Materials.MetalDarkGrey;

// Schedule 1 meshes (instantiate game prefabs)
GameObject cabinet = Meshes.DisplayCabinet.Instantiate("Name", position, rotation, parent);
GameObject computer = Meshes.Computer.Instantiate("Name", position, rotation, parent);
```

## Related Libraries

**S1API** ([GitHub](https://github.com/ifBars/S1API)) provides game integration:
- NPC creation and management
- Quest systems
- Product and storage management
- Door and security systems

Use S1MAPI for construction and S1API for game logic.
