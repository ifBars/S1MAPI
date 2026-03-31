# Getting Started

This guide walks you through installing S1MAPI and creating your first procedural mesh.

## Installation

### For Users (Installing Mods)

1. **Install MelonLoader**
   - Download from [melonwiki.xyz](https://melonwiki.xyz/#/README)
   - Install 0.7.0 for Schedule 1
   - Verify installation by launching the game

2. **Install S1MAPI**
   - Download the latest release from [GitHub Releases](https://github.com/ifBars/S1MAPI/releases)
   - Extract the ZIP file
   - Copy the contents to your Schedule 1 game directory
   - The `UserLibs` folder from the release merges with your existing `UserLibs`

3. **Install Mods Requiring S1MAPI**
   - Place mod DLLs in the `Mods` folder as usual
   - S1MAPI loads automatically before your mods

### For Developers

#### Option 1: Example Project (Recommended)

The **[MAPITesting repository](https://github.com/ifBars/MAPITesting)** provides a complete working mod that demonstrates:
- Building construction with `BuildingBuilder`
- Interior decoration with `InteriorBuilder`
- GLTF model loading (neon signs)
- Prefab placement with networking
- Integration with S1API for game entities (storage, doors)

**You are free to use this project as a template!**
- Copy the `.csproj`, `.sln`, and project structure
- Use the build configuration and MelonLoader integration
- Reference the csproj as a starting point for your own mods

The license only restricts copying the **specific examples** (the dispensary building, decorations, signage). S1MAPI's `BuildingConfig` presets and all code patterns are GPL-licensed and free to use. This may change in the future to support more open source once I release it as it's own mod. Until then feel free to contact me on discord if you have any concerns: ifbars

#### Option 2: Manual Setup

1. **Create a new MelonLoader mod project**

2. **Add S1MAPI reference**
   - Download the appropriate S1MAPI DLL from [releases](https://github.com/ifBars/S1MAPI/releases)
   - Add `S1MAPI_Mono.dll` as a project reference

3. **Build your mod**
   ```bash
   dotnet build -c Mono   # For Mono builds
   dotnet build -c Il2cpp # For IL2CPP builds
   ```

Note: S1MAPI works similarly to S1API, in the way that you only need to reference the Mono dll of S1MAPI. It is on the users of the mod to have the correct Mono/Il2Cpp dll installed in their `UserLibs` folder inside their Schedule 1 installation.

## Your First Mesh

Create a simple colored cube in your mod's `OnInitializeMelon`:

```csharp
using S1MAPI.ProceduralMesh;
using UnityEngine;

public class YourMod : MelonMod
{
    public override void OnInitializeMelon()
    {
        // Create a red box at position (0, 1, 0)
        GameObject cube = new ProceduralMeshBuilder("MyRedCube")
            .AddBox(new Vector3(0, 1, 0), Vector3.one)
            .SetColor(Color.red)
            .Build();

        LoggerInstance.Msg("Created a red cube!");
    }
}
```

## Creating More Shapes

Combine multiple shapes into a single mesh:

```csharp
GameObject snowman = new ProceduralMeshBuilder("Snowman")
    .AddSphere(new Vector3(0, 0.5f, 0), 0.5f)   // Body
    .AddSphere(new Vector3(0, 1.25f, 0), 0.35f) // Head
    .AddSphere(new Vector3(0, 1.8f, 0), 0.15f)  // Hat top
    .SetColor(Color.white)
    .ApplyFlatShading()
    .Build();
```

## Creating a Building

Use `BuildingBuilder` for structured constructions:

```csharp
using S1MAPI.Building;
using S1MAPI.Building.Config;

GameObject shop = new BuildingBuilder("MyBuilding")
    .WithConfig(BuildingConfig.Medium)
    .AddFloor()
    .AddCeiling()
    .AddWalls(southDoor: true, eastWindow: true)
    .AddRoofTrim()
    .AddLights()
    .Build();

// Position the building
shop.transform.position = new Vector3(100, 0, 50);
shop.transform.rotation = Quaternion.Euler(0, 90, 0);
```

## Loading GLTF Models

Embed GLB files in your mod assembly and load them at runtime:

```csharp
using S1MAPI.Gltf;
using S1MAPI.Utils;

// Load from embedded resource (file must be marked as embedded resource in csproj)
byte[]? glbBytes = EmbeddedResourceLoader.LoadBytes("YourMod.Resources.neon_sign.glb");
GameObject? model = GltfLoader.LoadGlb(glbBytes);

if (model != null)
{
    model.transform.position = new Vector3(0, 0, 0);
    model.transform.localScale = Vector3.one * 0.5f;
}
```

**In your `.csproj`, mark GLB files as embedded resources:**
```xml
<ItemGroup>
  <EmbeddedResource Include="Resources\**\*.glb" />
</ItemGroup>
```

For more control, use `GltfImporter` directly:

```csharp
byte[]? glbData = EmbeddedResourceLoader.LoadBytes("YourMod.Resources.sign.glb");

GameObject? model = new GltfImporter()
    .SetEmissionIntensity(3.0f)  // Boost emission for neon signs
    .Load(glbData);
```

See the [GLTF Loading Guide](gltf-loading.md) for complete documentation.

## Using Materials

Create materials with different visual properties:

```csharp
using S1MAPI.Utils;

// Opaque red material
Material redOpaque = MaterialPresets.Opaque(Color.red);

// Transparent blue glass
Material blueGlass = MaterialPresets.Glass(Color.blue, alpha: 0.3f);

// Metallic silver
Material silverMetal = MaterialPresets.Metal(Color.gray, metallic: 0.9f);

// Glowing green
Material glowingGreen = MaterialPresets.Emissive(Color.green, intensity: 2.0f);

// Apply to mesh
GameObject cube = new ProceduralMeshBuilder("GlowingCube")
    .AddBox(Vector3.zero, Vector3.one)
    .SetMaterial(glowingGreen)
    .Build();
```

## Next Steps

- [Procedural Mesh Guide](procedural-mesh.md) - Deep dive into mesh generation
- [Building Guide](building.md) - Create complete buildings
- [GLTF Loading](gltf-loading.md) - Import external models
- [Examples](examples.md) - Complete code examples
- Explore the API <xref:S1MAPI> for detailed documentation

### License Notice

The MAPITesting repository uses a **Preview Learning-Only License (PLOL)**:

**You ARE free to:**
- Copy the project structure (`.csproj`, `.sln`, build configuration)
- Use S1MAPI's `BuildingConfig` presets (`BuildingConfig.Large`, etc.)
- Study the code patterns and learn from them
- Build your own mods using the same approach

**You may NOT:**
- Copy the specific dispensary building example (GreenLabDispensary.cs)
- Copy the decorations, signage, or exact interior layouts
- Publish a "reskinned" version of the example

The key distinction: **Building configurations from S1MAPI are GPL-licensed and free to use. The specific examples in MAPITesting are protected.**
