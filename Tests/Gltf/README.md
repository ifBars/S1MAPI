# GLB runtime regression tests

These are opt-in MelonLoader tests, not a `dotnet test` suite. They exercise
`GltfLoader.LoadGlb(byte[], Shader?)` inside real Unity, including native Mesh
assignment and material creation. All GLBs are generated from synthetic data in
`GltfRuntimeTests.cs`; no game assets or sample models are distributed.

## Build

Configure the repository's ignored `local.build.props` with your Mono and IL2CPP
assembly paths. Build serially because the parent project changes target framework
by configuration. Clear `UserLibsPath` on the command line to prevent automatic
deployment into your normal game install:

```powershell
dotnet build Tests/Gltf/S1MAPI.Gltf.RuntimeTests.csproj -c Mono -p:UserLibsPath= -p:GeneratePackageOnBuild=false
dotnet build Tests/Gltf/S1MAPI.Gltf.RuntimeTests.csproj -c Il2cpp -p:UserLibsPath= -p:GeneratePackageOnBuild=false
```

The Mono test targets netstandard2.1 and references MelonLoader's `net35` assembly
beside the configured `net6` directory. Override `LoaderAssemblies` if your
installation uses another compatible loader layout. IL2CPP tests target net6.0.
The test project does not package or deploy its output.

## Run each backend separately

Use a disposable, isolated game install with MelonLoader already set up for the
matching backend. Leave your usual Mods, UserLibs, game configuration, and saves
untouched. The test only needs the Menu scene; do not load a save.

1. Copy `Tests/Gltf/bin/<configuration>/<framework>/S1MAPI.Gltf.RuntimeTests.dll`
   into the isolated install's `Mods` directory.
2. Copy the matching `S1MAPI_Mono.dll` or `S1MAPI_Il2cpp.dll` from that same build
   directory into its `UserLibs`. Do not copy Unity reference DLLs from build
   output into either directory. Keep unrelated mods out of the test install.
3. Set `MAPI_GLB_TEST_RESULT` to a new absolute output filename whose parent exists
   and launch that install from the same shell. For example:

   ```powershell
   $env:MAPI_GLB_TEST_RESULT = 'D:\MAPI-test-results\mono-run-001.txt'
   $game = Start-Process 'D:\Isolated-MAPI-Mono\Schedule I.exe' -WorkingDirectory 'D:\Isolated-MAPI-Mono' -WindowStyle Hidden -PassThru
   ```

4. Require `COMPLETE 22 cases; failures=0` in `MelonLoader/Latest.log` and 22
   `PASS|...` lines in the result file. A loaded mod or a running game alone is not
   a pass. Stop on `FAIL|...`, loader failure, process exit, or a bounded startup
   timeout (100 seconds is enough on the development machine). Record the last
   scene/log marker when startup fails.
5. Open `<result-file>.png`. The render probe must show a red triangle on black
   and also checks a minimum red-pixel count. This validates visibility and
   front-face winding with a URP Unlit shader, not lit/PBR/texture fidelity.
6. Stop only the process you launched. Copy any evidence needed for review, then
   remove the test DLL, test library, isolated install and disposable outputs.
   Clear the shell variable with `Remove-Item Env:\MAPI_GLB_TEST_RESULT`.

Repeat with IL2CPP and a different result filename. Report each runtime separately.
The result does not validate loaded-save gameplay, game-specific integration,
networking, texture import, animation, skinning, or arbitrary GLBs.

## Covered behavior

- Non-indexed triangles, X reflection, reversed winding, and offsets after an indexed primitive.
- Indexed triangles with unsigned byte, unsigned short, and unsigned int indices.
- Default materials and material order after a skipped primitive.
- Rejection of all six non-triangle primitive modes before appending geometry.
- Missing normals, disabled generation, mixed optional attributes in both orders,
  authored attribute preservation, UV flipping and zero-filled missing UVs.
- Missing POSITION, mismatched attribute counts, out-of-range indices, and incomplete triangle lists.
- Rendering a synthetic non-indexed triangle through Unity.

Unsupported features and broader validation limits are listed in the
[GLTF loading guide](../../docs/gltf-loading.md#static-glb-compatibility-profile).
