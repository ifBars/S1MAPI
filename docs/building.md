# Building Guide

Learn how to construct buildings using S1MAPI's semantic building API.

## Overview

The `BuildingBuilder` class provides a fluent API for creating complete buildings with walls, floors, roofs, windows, doors, and interior elements.

## Basic Building

```csharp
using S1MAPI.Building;
using S1MAPI.Building.Config;

GameObject shop = new BuildingBuilder("MyDispensary")
    .WithConfig(BuildingConfig.Medium)
    .AddFloor()
    .AddCeiling()
    .AddWalls(southDoor: true)
    .Build();

shop.transform.position = new Vector3(100, 0, 50);
```

## Building Configuration

### Preset Configurations

```csharp
.WithConfig(BuildingConfig.Tiny)      // 3m x 2.4m x 3m
.WithConfig(BuildingConfig.Small)     // 5m x 3m x 4m
.WithConfig(BuildingConfig.Medium)    // 8m x 3m x 6m
.WithConfig(BuildingConfig.Large)     // 12m x 4m x 10m
.WithConfig(BuildingConfig.Huge)      // 20m x 6m x 15m
.WithConfig(BuildingConfig.Warehouse) // Industrial style
```

### Custom Configuration

```csharp
var config = new BuildingConfig
{
    Width = 10f,
    Height = 4f,
    Depth = 8f,
    WallThickness = 0.2f,
    FloorThickness = 0.1f,
    CeilingThickness = 0.1f
};

new BuildingBuilder("CustomBuilding")
    .WithConfig(config)
    .AddFloor()
    .AddCeiling()
    .AddWalls()
    .Build();
```

## Color and Material Palettes

### Basic Colors

```csharp
new BuildingBuilder("Shop")
    .WithConfig(BuildingConfig.Medium)
    .AddFloor(color: new Color(0.5f, 0.35f, 0.25f))  // Wood color
    .AddCeiling()
    .AddWalls(color: new Color(0.9f, 0.9f, 0.9f))    // White walls
    .Build();
```

### Custom Palette

```csharp
var palette = new BuildingPalette
{
    FloorMaterial = Materials.ConcreteLightGrey,
    FloorColor = new Color(0.75f, 0.75f, 0.75f),
    WallMaterial = Materials.GraniteDullSalmonLighter,
    WallColor = new Color(0.94f, 0.94f, 0.94f),
    CeilingMaterial = Materials.ConcreteLightGrey,
    CeilingColor = new Color(0.94f, 0.94f, 0.94f),
    TrimMaterial = Materials.BrickWallRed,
    TrimColor = new Color(0.6f, 0.3f, 0.2f),
    PillarMaterial = Materials.BrickWallRed,
    PillarColor = new Color(0.6f, 0.3f, 0.2f),
    AccentColor = new Color(0.29f, 0.48f, 0.29f),
    AccentMaterial = null,
    LightColor = new Color(1f, 0.98f, 0.95f),
    LightIntensity = 1.2f
};

new BuildingBuilder("Shop")
    .WithConfig(BuildingConfig.Large)
    .WithPalette(palette)
    .AddFloor()
    .AddCeiling()
    .AddWalls()
    .Build();
```

## Walls and Openings

### Simple Openings

```csharp
.AddWalls(
    northDoor: true,   // Door on north wall
    southDoor: true,   // Door on south wall
    eastWindow: true,  // Window on east wall
    westWindow: true   // Window on west wall
)
```

### Custom Openings & Offsets

You can combine doors and windows on the same wall, and offset them horizontally:

```csharp
using S1MAPI.Building.Structural;

.AddWalls(
    north: WallOpening.DoorWithWindows(
        doorWidth: 1.5f, doorHeight: 2.5f,
        leftWindow: WallOpening.Window(width: 1f),
        rightWindow: WallOpening.Window(width: 1f)
    ),
    south: WallOpening.Window(width: 2f, height: 1.5f)
)
```

### Interior Walls

Segment your building by defining interior walls. Note that `InteriorWallAxis.X` walls run along the east-to-west axis, and `InteriorWallAxis.Z` walls run north-to-south.

```csharp
.AddInteriorWall(
    axis: InteriorWallAxis.X, 
    position: 4f, 
    opening: WallOpening.Door()
)
```

## Decorative Elements

```csharp
.AddRoofTrim(height: 0.3f)                           // Roofline trim
.AddSecondaryRoofTrim(height: 0.15f)                 // Secondary trim
.AddCornerPillars(width: 0.5f)                       // Corner columns
.AddBaseMolding(height: 0.3f)                        // Baseboard
.AddFoundation(height: 2.0f, expandX: 0.3f, expandZ: 0.3f)  // Foundation
.AddStairs(WallSide.South, maxStepHeight: 0.2f, style: StairStyle.ClosedRiser) // Entry stairs
```

### Roofs

Top off the building with generated roofs:

```csharp
.AddParapetRoof(ParapetPreset.Shallow)               // Flat roof with parapet walls
.AddHipRoof(ridgeHeight: 2.0f, overhang: 0.3f)       // Sloped hip roof
```

### Environment Preparation

Always clear nature from the construction site to prevent foliage and rustle sounds from interfering with your building:

```csharp
// 1. Clear trees, foliage, and ambient rustle triggers
TerrainClearer.ClearAroundBuilding(buildingObject, new Vector3(10f, 4f, 10f), new ClearingOptions { Padding = 2f });

// 2. Flatten the terrain directly beneath the foundation
buildingBuilder.FlattenTerrain();
```

### NPC Pathfinding (A* Navigation)

S1MAPI replaces standard NavMesh repairers with a robust custom A* grid system (`InteriorPathGrid`). This solves edge cases and allows NPCs to navigate cleanly inside tight building footprints:

```csharp
NavigationBuilder navBuilder = buildingBuilder.CreateNavigationBuilder();
navBuilder.Build(); // Always call Build() after physical walls are placed
```

## Lighting

```csharp
.AddLights()                              // Default ceiling lights
.AddLights(intensity: 1.5f)               // Custom intensity
.AddLights(color: Color.white)            // Custom color
.AddLights(intensity: 1.2f, color: new Color(1f, 0.98f, 0.95f))  // Both

.AddAmbientLighting(intensity: 0.3f)      // Ambient fill light
```

## Furniture

### Semantic Positioning

```csharp
.AddFurniture(FurnitureType.Desk, "north")     // North wall
.AddFurniture(FurnitureType.Counter, "south")  // South wall
.AddFurniture(FurnitureType.Chair, "center")   // Center of room
.AddFurniture(FurnitureType.Table, "east")     // East side
```

### Exact Positioning

```csharp
.AddFurniture(
    FurnitureType.Counter,
    new Vector3(5, 0, 9),
    Quaternion.Euler(0, 180, 0),
    color: Color.gray
)
```

**Available Furniture Types:**
- `Desk`, `Counter`, `Table`, `Chair`
- `Shelf`, `DisplayCabinet`, `CoffeeTable`
- `Rug`, `Box`, `Sphere`, `Cylinder` (primitive shapes)

**Note:** For S1 game furniture meshes, use `InteriorBuilder` instead.

## Building Part Registry

When you call `builder.Build()`, a `BuildingPartRegistry` component is attached to the root GameObject. This registry catalogs all generated meshes automatically:

```csharp
BuildingPartRegistry registry = builder.Registry;

// Example: Change the material of all exterior walls at runtime
registry.SetMaterial(BuildingPart.ExteriorWalls, newMaterial);
```

## Prefabs & Multiplayer Sync

Place Schedule 1 game prefabs inside your building using `BuildingBuilder` or the `PrefabPlacer` component:

```csharp
// Fluent API
.AddPrefab(Prefabs.ATM, position, rotation)
```

Alternatively, you can use `PrefabPlacer` directly. This is crucial for networked interactables (like Doors and Light Switches) as it utilizes `NetworkedPrefabLinker` under the hood to perfectly synchronize the object's parent hierarchy to joining clients over FishNet:

```csharp
PrefabPlacer placer = new PrefabPlacer(building.transform);
placer.Place(Prefabs.MetalGlassDoor, position, rotation, networked: true);
```

**Note:** `BuildingBuilder.AddPrefab()` only accepts `PrefabRef` and always spawns networked. For non-networked S1 furniture meshes, use `InteriorBuilder` methods instead.

### ⚠️ Critical: Networked Parameter

**Always set `networked: true` for prefabs that have a `NetworkObject` component!**

Using `networked: false` (or omitting it) on networked prefabs like ATM, doors, or storage containers will **crash FishNet and break multiplayer**. This cannot be recovered without restarting the game.

**Networked prefabs include:**
- ATM, ModularSwitch
- Doors (any interactive doors)
- Storage containers
- Any prefab with multiplayer synchronization

**Non-networked objects:**
- Use `InteriorBuilder` methods like `AddDesk()`, `AddChair()`, `AddPlant()` for S1 furniture meshes
- These are static decorations without network synchronization

**Common Prefabs:**
- Networked (require `networked: true`): `ATM`, `ModularSwitch`, `SlidingDoors`
- Non-networked (decorative): Use `InteriorBuilder` methods instead (e.g., `AddDesk()`, `AddChair()`)

## Doors

```csharp
// Sliding double doors at door opening
.AddSlidingDoors(
    new Vector3(6, -0.058f, 0.0655f),  // Position at door opening
    Quaternion.Euler(0, 180, 0),       // Rotation
    "6AM-6PM"                          // Opening hours sign text
)
```

## Interior Decoration

For complex interiors, use `InteriorBuilder`:

```csharp
var interior = new InteriorBuilder(building.transform);

// Add S1 furniture meshes (non-networked decorative objects)
interior.AddDesk(new Vector3(6f, 0f, 9.2f), Quaternion.Euler(0f, 180f, 0f));
interior.AddChair(new Vector3(6f, 0f, 7f), Quaternion.Euler(0f, 0f, 0f));
interior.AddCabinet(new Vector3(0.2f, 0f, 3f), Quaternion.Euler(0f, 90f, 0f));

// Add decorations
interior.AddPlant(new Vector3(1f, 0f, 1f));
interior.AddClock(new Vector3(4f, 2.5f, 9.5f), Quaternion.Euler(0f, 180f, 0f));

// Place networked prefabs (MUST use networked: true for NetworkObject prefabs!)
interior.AddPrefab(
    Prefabs.ATM,
    new Vector3(3f, 0f, 5f),
    Quaternion.identity,
    networked: true  // CRITICAL: ATM has NetworkObject component
);

// Build organizes all objects under an Interior folder
interior.Build();
```

**Available Furniture Methods:**
- `AddDesk()`, `AddChair()`, `AddArmchair()`, `AddBench()`
- `AddTable()`, `AddOfficeTable()`, `AddBed()`
- `AddCabinet()`, `AddDrawer()`, `AddLocker()`, `AddSafe()`
- `AddBox()`, `AddBarrel()`, `AddBin()`, `AddFridge()`
- `AddPlant()`, `AddPlanter()`, `AddVase()`, `AddPainting()`
- `AddClock()`, `AddWallClock()`, `AddToilet()`
- `AddComputer()`, `AddScreen()`

**Note:** The `networked` parameter is critical for `AddPrefab()`. See the warning in the [Prefabs](#prefabs) section above.

## Complete Example

```csharp
using S1MAPI.Building;
using S1MAPI.Building.Config;
using UnityEngine;

public static class BuildingExamples
{
    public static GameObject CreateSmallShop(Vector3 position)
    {
        var palette = new BuildingPalette
        {
            FloorColor = new Color(0.5f, 0.35f, 0.25f),
            WallColor = new Color(0.9f, 0.9f, 0.85f),
            CeilingColor = new Color(0.95f, 0.95f, 0.95f),
            TrimColor = new Color(0.3f, 0.25f, 0.2f),
            LightColor = new Color(1f, 0.95f, 0.9f)
        };

        return new BuildingBuilder("SmallShop")
            .WithConfig(BuildingConfig.Small)
            .WithPalette(palette)
            .AddFloor()
            .AddCeiling()
            .AddWalls(southDoor: true, eastWindow: true)
            .AddRoofTrim()
            .AddBaseMolding()
            .AddLights(intensity: 1.0f)
            .AddFurniture(FurnitureType.Counter, "south")
            .AddFurniture(FurnitureType.Chair, "center")
            .Build();
    }
}
```

## Complete Example: Full Dispensary

See the [MAPITesting repository](https://github.com/ifBars/MAPITesting) for a complete working example of a dispensary building with:
- Custom palette with green accents
- Double sliding doors
- Display cabinets for products
- Wall shelves
- Neon GLTF sign
- Integration with S1API for game entities

## Next Steps

- [GLTF Loading](gltf-loading.md) - Add imported 3D models to buildings
- [API Reference](xref:S1MAPI.Building.BuildingBuilder) - Full API docs
- [Examples](examples.md) - More code examples
