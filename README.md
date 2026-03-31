# S1MAPI - Schedule 1 Mapping API

**S1MAPI** is a mapping and construction library for Schedule 1 mods. Create procedural meshes, build structures, and load GLTF assets without asset bundles.

[![GitHub release](https://img.shields.io/github/v/release/ifBars/S1MAPI?include_prereleases&sort=semver)](https://github.com/ifBars/S1MAPI/releases)
[![GitHub stars](https://img.shields.io/github/stars/ifBars/S1MAPI)](https://github.com/ifBars/S1MAPI/stargazers)

## What S1MAPI Does

- **Procedural Meshes**: Generate 3D shapes at runtime (boxes, spheres, cylinders, capsules)
- **Building Construction**: Create buildings with walls, floors, roofs, windows, and furniture
- **GLTF Loading**: Import external 3D models without external dependencies
- **Update Resilience**: Works across game updates by avoiding Assembly-CSharp types

## Installation

### For Players

If a mod you want to use requires S1MAPI:

1. Download the latest S1MAPI package from [GitHub Releases](https://github.com/ifBars/S1MAPI/releases) or Thunderstore
2. Extract the zip - it contains a `UserLibs` folder with both `S1MAPI_Mono.dll` and `S1MAPI_Il2Cpp.dll`
3. Copy the correct DLL for your Steam branch to your game's `UserLibs` folder:
   - **Regular Steam branch (default)**: Use `S1MAPI_Il2Cpp.dll`
   - **Alternate Steam branch**: Use `S1MAPI_Mono.dll`
4. Launch the game - S1MAPI will load automatically with MelonLoader

**To check your Steam branch:** Right-click Schedule One in Steam → Properties → Betas. If it shows "None" or no selection, you're on the regular branch (use Il2Cpp).

### For Developers
Clone the repository and build:

```bash
git clone https://github.com/ifBars/S1MAPI.git
cd S1MAPI
dotnet build -c Mono   # For Mono builds
dotnet build -c Il2cpp # For IL2CPP builds
```

## Requirements

- **Game**: Schedule 1
- **Mod Loader**: MelonLoader 0.7.0+

## Learn More

- [Getting Started](docs/getting-started.md) - Installation and your first project
- [Examples](docs/examples.md) - Code examples and patterns
- [MAPITesting Repository](https://github.com/ifBars/MAPITesting) - Full working mod example

## Relationship to S1API

S1MAPI and S1API are complementary:
- **S1MAPI**: Mesh construction, building generation, GLTF loading (no game dependencies)
- **S1API**: Game component wrappers, entity management, quests (wraps game types)

## Contributing

Contributions are welcome! See [Contributing Guide](docs/contributing.md) for guidelines.

## License

GNU GPL v3 License - see [LICENSE](LICENSE) file.
