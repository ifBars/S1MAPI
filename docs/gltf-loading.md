# GLTF Loading Guide

Learn how to import external 3D models at runtime using S1MAPI's GLTF loader.

## Overview

S1MAPI includes a native GLTF/GLB loader with no external dependencies. It handles:
- GLB (binary GLTF) files
- JSON-based GLTF files
- External resources ( textures, buffers)
- Coordinate system conversion (GLTF right-handed → Unity left-handed)
- Material creation and texture mapping

## Loading GLB Files

### From Bytes

```csharp
using S1MAPI.Gltf;

// Load GLB data from file or embedded resource
byte[] glbData = File.ReadAllBytes("path/to/model.glb");

// Load and return root GameObject
GameObject? model = GltfLoader.LoadGlb(glbData);

if (model != null)
{
    model.transform.position = new Vector3(0, 0, 0);
    model.transform.localScale = Vector3.one;
}
```

### Using the Generic Load Method

```csharp
// Load from bytes (auto-detects format)
GameObject? model = GltfLoader.Load(glbData);

// With custom shader
Shader customShader = Shader.Find("Universal Render Pipeline/Lit");
GameObject? model = GltfLoader.Load(glbData, shader: customShader);
```

### From File

```csharp
// Load directly from file path
GameObject? model = GltfLoader.LoadFromFile("path/to/model.glb");

// With custom shader
GameObject? model = GltfLoader.LoadFromFile("path/to/model.glb", myShader);
```

## Loading GLTF JSON

GLTF JSON files can reference external binary buffers and textures:

```csharp
string gltfJson = File.ReadAllText("path/to/model.gltf");
string basePath = "path/to/";  // Directory containing external resources

GameObject? model = GltfLoader.LoadFromJson(gltfJson, basePath);
```

## Using GltfImporter Directly

For more control over the import process:

```csharp
using S1MAPI.Gltf;

GameObject? model = new GltfImporter()
    .SetShader(myShader)                      // Custom shader for materials
    .SetEmissionIntensity(2.0f)               // Emission multiplier
    .Load(glbData);                            // Load the model
```

**GltfImporter Options:**

| Method | Description |
|--------|-------------|
| `SetShader(Shader shader)` | Use custom shader for materials |
| `SetEmissionIntensity(float intensity)` | Set emission color intensity multiplier |
| `Load(byte[] data)` | Load GLB or GLTF bytes |
| `LoadGlb(byte[] glbBytes)` | Load GLB specifically |
| `LoadGltfJson(string json, string basePath)` | Load GLTF JSON with base path |
| `LoadFromFile(string path)` | Load from file path |

## Loading Embedded Resources

Load GLB files embedded in your mod assembly:

```csharp
using S1MAPI.Utils;
using S1MAPI.Gltf;

// Load from embedded resource (assembly must contain the file)
byte[]? glbData = EmbeddedResourceLoader.LoadBytes("MyMod.Resources.sign.glb");

if (glbData != null)
{
    GameObject? sign = GltfLoader.LoadGlb(glbData);
    
    if (sign != null)
    {
        sign.transform.SetParent(building.transform);
        sign.transform.localPosition = new Vector3(12f, 2.4f, 4.85f);
        sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        sign.transform.localScale = Vector3.one * 0.3f;
    }
}
```

## Material Handling

### Default Material Behavior

- Materials are created using S1MAPI's default shader (URP Lit → Standard → Hidden/Internal-Colored)
- Textures are applied if found in the GLTF file
- Metallic-roughness textures are remapped from GLTF's blue/green channels to Unity's red/alpha metallic-smoothness layout
- Normal and occlusion textures are converted to linear data textures before being assigned to the shader
- Emission is supported with configurable intensity

### Custom Shader

```csharp
Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");

GameObject? model = new GltfImporter()
    .SetShader(urpShader)
    .Load(glbData);
```

## Coordinate System

GLTF uses a right-handed coordinate system, while Unity uses left-handed. S1MAPI automatically handles the conversion by:

1. Inverting the Z axis
2. Converting the rotation matrix
3. Properly orienting the mesh data

This means models appear correctly oriented without manual transformation.

## Example: Neon Sign

```csharp
using S1MAPI.Utils;
using S1MAPI.Gltf;
using UnityEngine;

public static class SignLoader
{
    public static GameObject LoadNeonSign(GameObject parentBuilding)
    {
        // Load embedded GLB
        byte[]? glbData = EmbeddedResourceLoader.LoadBytes("MyMod.Resources.neon_open_sign.glb");
        if (glbData == null)
        {
            Debug.LogWarning("Failed to load neon sign");
            return null;
        }

        // Import with emission
        GameObject? sign = new GltfImporter()
            .SetEmissionIntensity(4.0f)  // Boost emission for neon effect
            .Load(glbData);

        if (sign == null)
        {
            Debug.LogWarning("Failed to import neon sign");
            return null;
        }

        // Configure transform
        sign.name = "NeonOpenSign";
        sign.transform.SetParent(parentBuilding.transform);
        sign.transform.localPosition = new Vector3(12f, 2.4f, 4.85f);
        sign.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        sign.transform.localScale = Vector3.one * 0.3f;

        return sign;
    }
}
```

## Performance Considerations

1. **Cache loaded models**: If you load the same GLB multiple times, cache the result.

2. **Use appropriate scale**: GLTF models often use different scale conventions. Adjust `localScale` as needed.

3. **Emission intensity**: Higher values create brighter glow effects but check visual quality.

4. **Resource cleanup**: GLTF imports create meshes, materials, and textures. Unity handles cleanup automatically when GameObjects are destroyed.

5. **Packed material maps**: Metallic-roughness conversion creates a Unity-compatible runtime texture per material. Cache imported models instead of repeatedly loading the same asset.

## Troubleshooting

### Model Appears Black
- Check if the shader is supported (URP Lit recommended)
- Verify textures are being loaded correctly

### Model Orientation is Wrong
- S1MAPI should handle conversion automatically
- If still wrong, try flipping the Z scale: `model.transform.localScale = new Vector3(1, 1, -1)`

### Model is Too Large/Small
- Adjust `localScale` to match your scene
- Common GLTF scale factors: 0.01, 0.1, 1.0, 100 (depends on source)

### Missing Textures (GLTF JSON)
- Ensure `basePath` points to the correct directory
- Check that texture files exist and are readable

## Supported GLTF Features

- **Meshes**: Positions, normals, UVs, tangents, colors
- **Materials**: PBR metallic-roughness, unlit, emission
- **Textures**: PNG, JPEG formats
- **Nodes**: Hierarchy and transforms
- **Animations**: Not currently supported (contact if needed)

## Next Steps

- [Building Guide](building.md) - Add GLTF models to buildings
- [Examples](examples.md) - Complete examples with GLTF
- [API Reference](xref:S1MAPI.Gltf.GltfLoader) - Full API docs
