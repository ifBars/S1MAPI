# Examples

This guide provides practical examples of using S1MAPI in your Schedule 1 mods.

## Quick Examples

### No Initialization Required

S1MAPI works immediately without any setup:

```csharp
using S1MAPI.ProceduralMesh;
using UnityEngine;

public class YourSchedule1Mod : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Just use it!
        GameObject cube = new ProceduralMeshBuilder("MyCube")
            .AddBox(Vector3.zero, Vector3.one)
            .SetColor(Color.green)
            .Build();
    }
}
```

## Procedural Mesh Examples

### Create a Colored Box

```csharp
GameObject box = new ProceduralMeshBuilder("MyBox")
    .AddBox(Vector3.zero, new Vector3(1f, 1f, 1f))
    .SetColor(Color.red)
    .Build();
```

### Create a Low-Poly Sphere

```csharp
GameObject sphere = new ProceduralMeshBuilder("MySphere")
    .AddSphere(Vector3.zero, 0.5f, subdivisions: 4)
    .SetColor(Color.blue)
    .ApplyFlatShading()
    .Build();
```

### Create a Cylinder

```csharp
Vector3 start = new Vector3(0, 0, 0);
Vector3 end = new Vector3(0, 2, 0);

GameObject cylinder = new ProceduralMeshBuilder("MyCylinder")
    .AddCylinder(start, end, radius: 0.3f, segments: 12)
    .SetColor(Color.green)
    .Build();
```

### Combine Multiple Shapes

```csharp
GameObject complex = new ProceduralMeshBuilder("ComplexObject")
    .AddBox(Vector3.zero, new Vector3(1f, 0.2f, 1f))  // Base
    .AddCylinder(
        new Vector3(0, 0.1f, 0), 
        new Vector3(0, 2f, 0), 
        0.1f, 
        12
    )  // Pole
    .AddSphere(new Vector3(0, 2f, 0), 0.3f, 6)  // Top
    .SetColor(new Color(0.8f, 0.6f, 0.4f))  // Brown
    .ApplyFlatShading()
    .Build();
```

## Material Examples

### Create Materials with Presets

```csharp
using S1MAPI.Utils;

// Opaque material
Material opaque = MaterialPresets.Opaque(Color.red);

// Transparent material
Material transparent = MaterialPresets.Transparent(Color.blue, alpha: 0.5f);

// Glass material
Material glass = MaterialPresets.Glass(Color.cyan, alpha: 0.3f);

// Metallic material
Material metal = MaterialPresets.Metal(Color.gray, metallic: 0.9f, smoothness: 0.95f);

// Emissive (glowing) material
Material emissive = MaterialPresets.Emissive(Color.yellow, intensity: 2.0f);
```

### Apply Material to Mesh

```csharp
Material customMaterial = MaterialPresets.Metal(Color.silver);

GameObject metalBox = new ProceduralMeshBuilder("MetalBox")
    .AddBox(Vector3.zero, Vector3.one)
    .SetMaterial(customMaterial)
    .Build();
```

## Building Examples

### Basic Building

```csharp
using S1MAPI.Building;
using S1MAPI.Building.Config;

GameObject shop = new BuildingBuilder("MyDispensary")
    .WithConfig(BuildingConfig.Medium)
    .AddFloor()
    .AddCeiling()
    .AddWalls(southDoor: true)
    .AddLights()
    .Build();

shop.transform.position = new Vector3(100, 0, 50);
```

### Building with Custom Palette

```csharp
var palette = new BuildingPalette
{
    FloorColor = new Color(0.5f, 0.35f, 0.25f),
    WallColor = new Color(0.9f, 0.9f, 0.85f),
    CeilingColor = new Color(0.95f, 0.95f, 0.95f),
    TrimColor = new Color(0.3f, 0.25f, 0.2f),
    AccentColor = new Color(0.29f, 0.48f, 0.29f)
};

GameObject shop = new BuildingBuilder("Shop")
    .WithConfig(BuildingConfig.Large)
    .WithPalette(palette)
    .AddFloor()
    .AddCeiling()
    .AddWalls(southDoor: true, eastWindow: true)
    .AddRoofTrim()
    .AddCornerPillars(width: 0.5f)
    .AddLights()
    .Build();
```

### Building with Furniture

```csharp
GameObject shop = new BuildingBuilder("FurnishedShop")
    .WithConfig(BuildingConfig.Medium)
    .AddFloor()
    .AddCeiling()
    .AddWalls(southDoor: true)
    .AddFurniture(FurnitureType.Counter, "north")
    .AddFurniture(FurnitureType.Table, "center")
    .AddFurniture(FurnitureType.Chair, "south")
    .AddLights()
    .Build();

// For S1 game furniture meshes, use InteriorBuilder
var interior = new InteriorBuilder(shop.transform);
interior.AddDesk(new Vector3(4, 0, 5), Quaternion.identity);
interior.AddChair(new Vector3(4, 0, 4), Quaternion.Euler(0, 180, 0));
interior.Build();
```

### Advanced Building (Terrain, Navigation, & Networked Doors)

This complete example demonstrates how to prep the environment using `TerrainClearer` and `FlattenTerrain`, utilize complex wall openings like door and window combinations, generate an interior A* `NavigationBuilder` for NPC pathfinding, and place multiplayer-synced interactables securely using `PrefabPlacer` connected to FishNet.

```csharp
using S1MAPI.Building;
using S1MAPI.Building.Components;
using S1MAPI.Building.Config;
using S1MAPI.Building.Structural;
using S1MAPI.S1;
using S1MAPI.Utils;
using UnityEngine;

public static class StorefrontSpawner
{
    private static NavigationBuilder _navBuilder;

    public static void SpawnStorefront(Vector3 position)
    {
        BuildingBuilder builder = new BuildingBuilder("CoffeeShop")
            .DefineRoom(12f, 4f, 10f)
            .AddFloor()
            .AddCeiling()
            // Storefront: Central door, flanking windows
            .AddWalls(
                south: WallOpening.DoorWithWindows(
                    doorWidth: 1.8f, doorHeight: 2.2f,
                    leftWindow: WallOpening.Window(width: 1.5f, height: 2.0f, sillHeight: 0.5f, count: 1),
                    rightWindow: WallOpening.Window(width: 1.5f, height: 2.0f, sillHeight: 0.5f, count: 1)
                ),
                north: null, east: null, west: null
            )
            .AddFoundation(height: 0.2f)
            .AddBaseMolding()
            .AddCornerTrim()
            // Backroom divider 6 meters inward along the Z-axis
            .AddInteriorWall(InteriorWallAxis.X, 6f, opening: WallOpening.Door(1.2f, 2.1f))
            // Finish with a polished parapet style roof
            .AddParapetRoof(ParapetPreset.Shallow);

        GameObject coffeeShop = builder.Build();
        coffeeShop.transform.position = position;

        // 1. Clear terrain foliage and trees that would clip through the shop
        TerrainClearer.ClearAroundBuilding(coffeeShop, new Vector3(12f, 4f, 10f), new ClearingOptions { Padding = 2f });

        // 2. Flatten the terrain directly underneath
        builder.FlattenTerrain();

        // 3. Build A* NPC navigation grid for the interior (Must scan around our created walls)
        _navBuilder = builder.CreateNavigationBuilder();
        _navBuilder.Build();

        // 4. Place networked doors via PrefabPlacer for FishNet multiplayer replication
        PrefabPlacer placer = new PrefabPlacer(coffeeShop.transform);

        // Entrance door
        placer.Place(
            Prefabs.MetalGlassDoor, 
            new Vector3(6f, 0f, 0f), 
            Quaternion.identity, 
            networked: true, 
            enableComponents: true
        );

        // Interior backroom door
        placer.Place(
            Prefabs.ClassicalWoodenDoor, 
            new Vector3(6f, 0f, 6f), 
            Quaternion.identity, 
            networked: true
        );
    }
}
```

## GLTF Examples

### Load from Embedded Resource

```csharp
using S1MAPI.Utils;
using S1MAPI.Gltf;

byte[]? glbData = EmbeddedResourceLoader.LoadBytes("YourNamespace.Resources.sign.glb");

if (glbData != null)
{
    GameObject? sign = GltfLoader.LoadGlb(glbData);
    
    if (sign != null)
    {
        sign.transform.position = new Vector3(0, 2, 5);
        sign.transform.localScale = Vector3.one * 0.5f;
    }
}
```

### Load with Custom Settings

```csharp
GameObject? model = new GltfImporter()
    .SetShader(Shader.Find("Universal Render Pipeline/Lit"))
    .SetEmissionIntensity(3.0f)
    .Load(glbData);
```

## Utility Examples

### Material Creation

```csharp
using S1MAPI.Utils;

// Create materials with MaterialPresets
Material opaque = MaterialPresets.Opaque(Color.red);
Material transparent = MaterialPresets.Transparent(Color.blue, alpha: 0.7f);
Material glass = MaterialPresets.Glass(Color.cyan, alpha: 0.3f);
Material metal = MaterialPresets.Metal(Color.gray, metallic: 0.9f, smoothness: 0.95f);
Material emissive = MaterialPresets.Emissive(Color.yellow, intensity: 2.0f);
```

## Complete Examples

### Creating a Crate

```csharp
using S1MAPI.ProceduralMesh;
using S1MAPI.Utils;
using UnityEngine;

public static class ProceduralObjects
{
    public static GameObject CreateCrate(Vector3 position)
    {
        Color woodColor = new Color(0.6f, 0.4f, 0.2f);
        
        return new ProceduralMeshBuilder("WoodenCrate")
            .AddBox(Vector3.zero, new Vector3(1f, 1f, 1f))
            .SetColor(woodColor)
            .ApplyFlatShading()
            .Build();
    }
}
```

### Creating a Glowing Beacon

```csharp
using S1MAPI.ProceduralMesh;
using S1MAPI.Utils;
using UnityEngine;

public static class BeaconCreator
{
    public static GameObject Create(Vector3 position)
    {
        // Create the base
        GameObject beacon = new ProceduralMeshBuilder("Beacon")
            .AddCylinder(
                position,
                position + Vector3.up * 0.5f,
                0.3f,
                8
            )
            .SetMaterial(MaterialPresets.Metal(Color.gray))
            .Build();

        // Create glowing top
        GameObject light = new ProceduralMeshBuilder("BeaconLight")
            .AddSphere(position + Vector3.up * 0.75f, 0.2f, 6)
            .SetMaterial(MaterialPresets.Emissive(Color.yellow, intensity: 3f))
            .Build();

        light.transform.SetParent(beacon.transform);

        // Add actual light component
        Light lightComponent = light.AddComponent<Light>();
        lightComponent.color = Color.yellow;
        lightComponent.intensity = 5f;
        lightComponent.range = 10f;

        return beacon;
    }
}
```

## Full Project Example

See the **[MAPITesting Repository](https://github.com/ifBars/MAPITesting)** for a complete working mod that demonstrates:

- Building construction with `BuildingBuilder`
- Custom color palettes
- Interior decoration with `InteriorBuilder`
- GLTF model loading (neon signs)
- Prefab placement with networking
- Integration with S1API for game entities (storage, doors)

### Key Files in MAPITesting

| File | Description |
|------|-------------|
| `Core.cs` | Main mod entry point, scene initialization |
| `Buildings/GreenLabDispensary.cs` | Complete dispensary building example |
| `Buildings/DispensarySpawner.cs` | Building spawner orchestrator |
| `Resources/neon_open_sign.glb` | Embedded GLTF model |

## Next Steps

- <xref:S1MAPI> - Complete API documentation
- [Getting Started](getting-started.md) - Installation guide
- [Procedural Mesh Guide](procedural-mesh.md) - Mesh generation deep dive
- [Building Guide](building.md) - Building construction
- [GLTF Loading](gltf-loading.md) - Import external models
