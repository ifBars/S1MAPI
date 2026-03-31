# S1MAPI - Schedule 1 Mapping API

**S1MAPI** is a mapping and construction library for Schedule 1 mods. Create procedural meshes, build structures, and load GLTF assets without asset bundles.

[Documentation](https://ifbars.github.io/S1MAPI/) • [GitHub Repository](https://github.com/ifBars/S1MAPI)

---

## What S1MAPI Does

- **Procedural Meshes**: Generate 3D shapes at runtime such as boxes, spheres, cylinders, and capsules
- **Building Construction**: Create buildings with walls, floors, roofs, windows, and furniture
- **GLTF Loading**: Import external 3D models without extra runtime dependencies
- **Update Resilience**: Avoid ScheduleOne types so mods survive game updates more cleanly

---

## Installation

### For Players

If a mod you want to use requires S1MAPI:

1. Download the S1MAPI package from Thunderstore
2. Extract the package - it includes `UserLibs/S1MAPI_Mono.dll` and `UserLibs/S1MAPI_Il2Cpp.dll`
3. Copy the correct DLL for your Steam branch into your game's `UserLibs` folder:
   - **Regular Steam branch (default)**: Use `S1MAPI_Il2Cpp.dll`
   - **Alternate Steam branch**: Use `S1MAPI_Mono.dll`
4. Launch the game - S1MAPI will load automatically with MelonLoader

**To check your Steam branch:** Right-click Schedule 1 in Steam -> Properties -> Betas. If it shows "None" or no selection, you are on the regular branch and should use the Il2Cpp DLL.

### For Developers

To use S1MAPI in your mod project:

1. Add the **Mono** S1MAPI DLL as a reference to your mod project
2. Build your mod against the Mono DLL - it works for both runtimes
3. Tell users to install S1MAPI separately with the correct DLL for their Steam branch

---

## Building From Source

1. Clone the repository
2. Copy `local.build.props.example` to `local.build.props`
3. Edit `local.build.props` and set your game installation paths
4. Build with `dotnet build S1MAPI.csproj -c Mono` or `dotnet build S1MAPI.csproj -c Il2cpp`

---

## Requirements

- Schedule 1
- MelonLoader 0.7.0+
- .NET Framework 4.7.2+ or .NET 6.0+

---

## Learn More

- [Getting Started](https://ifbars.github.io/S1MAPI/articles/getting-started.html)
- [Examples](https://ifbars.github.io/S1MAPI/articles/examples.html)
- [API Reference](https://ifbars.github.io/S1MAPI/api/index.html)
- [Contributing Guide](https://github.com/ifBars/S1MAPI/blob/main/docs/contributing.md)

---

## License

GNU GPL v3 License - see the [LICENSE](https://github.com/ifBars/S1MAPI/blob/main/LICENSE) file.
