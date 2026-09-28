# GLTF Loading Guide

Learn how to import external 3D models at runtime using S1MAPI's GLTF loader.

## Overview

S1MAPI includes a runtime GLTF/GLB importer using Newtonsoft.Json and Unity.
For static 3D models, use self-contained GLB 2.0 bytes
with uncompressed triangle geometry. `GltfLoader.LoadGlb(byte[], Shader?)` remains
the public entry point; call it on Unity's main thread after shaders are available.

This is a limited importer, not a full glTF implementation. Use the compatibility
profile below when an editor validates exports for MAPI. A successful preview in
another glTF viewer does not prove that every feature will import here.

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

GLTF JSON can resolve external binary buffers relative to `basePath`. External
image files are not loaded by the active importer; embed images in buffer views
or base64 data URIs:

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

1. Reflecting X in positions, normals, tangents, and node translations
2. Converting node rotation to `(x, -y, -z, w)`
3. Reversing triangle winding and flipping texture V

Check the intended orientation and texture mapping in the target game shader.

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

4. **Resource cleanup**: GLTF imports create meshes, materials, and textures. Destroy runtime meshes, materials, and textures when no longer shared or needed;
   destroying the root GameObject alone does not release those assets.

5. **Packed material maps**: Metallic-roughness conversion creates a Unity-compatible runtime texture per material. Cache imported models instead of repeatedly loading the same asset.

## Troubleshooting

### Model Appears Black
- Check if the shader is supported (URP Lit recommended)
- Verify textures are being loaded correctly

### Model Orientation is Wrong
- S1MAPI should handle conversion automatically
- Check the exporter's transforms and apply/bake mirrored transforms before export

### Model is Too Large/Small
- Adjust `localScale` to match your scene
- Common GLTF scale factors: 0.01, 0.1, 1.0, 100 (depends on source)

### Missing Textures (GLTF JSON)
- External image files are not loaded by the active path
- Embed PNG/JPEG images in buffer views or base64 data URIs

## Static GLB Compatibility Profile

| Feature | Current behavior / editor rule |
|---|---|
| Geometry | `TRIANGLES` (`mode: 4`, including omitted mode), indexed or non-indexed. Non-indexed triangles use consecutive vertex triples. Each accepted primitive becomes one submesh with its material slot preserved. |
| Other modes | Points, lines, line loops/strips, triangle strips/fans are skipped with a warning. Triangulate before export. |
| Indices | Unsigned byte, unsigned short, unsigned int. Empty/incomplete triangle lists and indices outside the primitive's vertex range are skipped before appending vertices. |
| Vertex accessors | Export dense float32 `POSITION`/`NORMAL` (`VEC3`), `TEXCOORD_0` (`VEC2`), and `TANGENT` (`VEC4`). Existing readers honor byte offsets and byte stride for these attributes. Attribute counts must match positions. Missing POSITION or mismatched decoded attribute counts skip that primitive. |
| Optional attributes | Missing UVs use `(0,0)` when another primitive has UVs. Missing normals/tangents are generated by default through Unity, preserving authored values on other primitives. Tangent generation requires UVs somewhere in the mesh and complete/generated normals. Export authored normals/UVs/tangents for controlled shading; generated normals follow shared vertices, and absent UVs cannot yield meaningful normal mapping. |
| Generation switches | `new GltfImporter().GenerateNormals(false).GenerateTangents(false)` disables generation. Entirely absent attributes stay absent; holes in a mixed attribute stream are zero-filled. |
| Materials | Material slots, fallback materials, base color, metallic/roughness conversion, normal/occlusion maps, alpha settings and emission have implementation paths. Shader-specific rendering is not glTF material fidelity. `KHR_materials_emissive_strength` is handled; unlit/material extensions in general are not. |
| Images | Embedded PNG/JPEG in buffer views or base64 data URIs. External image paths and KTX2/Basis textures are unsupported. Texture-coordinate set selection and `KHR_texture_transform` are not applied; use `TEXCOORD_0` with baked transforms. |
| Hierarchy | Nodes and their TRS are imported, including shared meshes. The active path imports all nodes, not only the selected scene. Export one intended scene with no unused nodes. Bake mirrored matrix transforms; negative-scale matrix decomposition is not verified. |

### Reject or warn before importing

An editor targeting this importer should reject sparse accessors, normalized integer
vertex attributes, Draco/meshopt compression, quantization, and geometry referencing
buffers other than buffer 0. The mesh reader still consumes the first resolved
buffer and does not decode these features. They are not reliably rejected at
runtime and can silently produce incorrect geometry. A self-contained GLB can
still contain unsupported features; the file extension alone is insufficient.

Warn for `COLOR_0`, additional UV sets, skins, morph targets, animations, and
unrecognized required extensions. Vertex colors/additional UVs are ignored.
The active node path creates `MeshRenderer` components, not skinned renderers or
blend shapes. Animation parsing exists but is outside this static compatibility
profile and has not been validated by these tests. `ImportSkins`,
`ImportBlendShapes`, and `ReadableMeshes` options are not wired into this loading
path; imported meshes currently remain CPU-readable.

Validate GLB structure and accessor bounds before loading. Some malformed inputs
return null or skip a primitive, but other malformed accessor/buffer references
can still throw. A non-null root can contain partial or empty geometry. Inspect
warnings and renderer/mesh counts; this API does not return a structured validation
report. Keep the [Khronos glTF 2.0 specification](https://registry.khronos.org/glTF/specs/2.0/glTF-2.0.html)
and MAPI's narrower profile as separate validation checks.

### Regression coverage

[Runtime regression instructions](../Tests/Gltf/README.md) cover synthetic GLBs
through the public loader in real Unity Mono and IL2CPP processes. They check
index widths, non-indexed winding/offsets, missing and mixed attributes, material
slots, mode rejection, malformed index lists, and an unlit triangle render.
They do not certify arbitrary exporter output, texture/PBR fidelity, gameplay
placement, networking, animations, or full glTF compliance.

## Next Steps

- [Building Guide](building.md) - Add GLTF models to buildings
- [Examples](examples.md) - Complete examples with GLTF
- [API Reference](xref:S1MAPI.Gltf.GltfLoader) - Full API docs
