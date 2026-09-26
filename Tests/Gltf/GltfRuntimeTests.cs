using System.Text;
using MelonLoader;
using S1MAPI.Gltf;
using UnityEngine;

[assembly: MelonInfo(typeof(S1MAPI.Tests.Gltf.GltfRuntimeTests), "MAPI GLB regression tests", "1.0.0", "S1MAPI")]
[assembly: MelonGame(null, null)]

namespace S1MAPI.Tests.Gltf;

/// <summary>Opt-in Unity runtime tests exercising the public GLB byte-array entry point.</summary>
public sealed class GltfRuntimeTests : MelonMod
{
    private bool _ran;
    private readonly List<string> _results = new();

    /// <summary>Runs once in Menu when MAPI_GLB_TEST_RESULT names an output file.</summary>
    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        string? resultPath = Environment.GetEnvironmentVariable("MAPI_GLB_TEST_RESULT");
        if (_ran || sceneName != "Menu" || string.IsNullOrEmpty(resultPath)) return;
        _ran = true;
        Run("non-indexed", () => CheckMesh("{\"attributes\":{\"POSITION\":0}}", mesh =>
        {
            Equal(mesh.GetTriangles(0).ToArray(), new[] { 2, 1, 0 });
            Require(mesh.vertices[1].x == -1, "X coordinate conversion");
        }));
        foreach (int component in new[] { 5121, 5123, 5125 })
            Run($"indexed-{component}", () => CheckMesh("{\"attributes\":{\"POSITION\":0},\"indices\":1}",
                mesh => Equal(mesh.GetTriangles(0).ToArray(), new[] { 2, 1, 0 }), component));
        Run("mixed-offsets", () => CheckMesh("{\"attributes\":{\"POSITION\":0},\"indices\":1},{\"attributes\":{\"POSITION\":0}}",
            mesh => { Require(mesh.vertexCount == 6, "vertex count"); Equal(mesh.GetTriangles(1).ToArray(), new[] { 5, 4, 3 }); }));
        Run("default-material", () => WithModel("{\"attributes\":{\"POSITION\":0}},{\"attributes\":{\"POSITION\":0}}", model =>
        {
            var materials = model.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            Require(materials.Length == 2 && materials[0] != null && materials[1] != null, "every default material slot assigned");
        }));
        foreach (int mode in new[] { 0, 1, 2, 3, 5, 6 })
            Run($"unsupported-mode-{mode}", () => CheckMesh($"{{\"attributes\":{{\"POSITION\":0}},\"mode\":{mode}}},{{\"attributes\":{{\"POSITION\":0}}}}",
                mesh => { Require(mesh.vertexCount == 3 && mesh.subMeshCount == 1, "unsupported primitive skipped before append"); Equal(mesh.GetTriangles(0).ToArray(), new[] { 2, 1, 0 }); }));
        Run("mixed-attributes", () => CheckMesh("{\"attributes\":{\"POSITION\":0}},{\"attributes\":{\"POSITION\":0,\"NORMAL\":2,\"TEXCOORD_0\":3,\"TANGENT\":4}}", mesh =>
        {
            Require(mesh.normals.Length == 6, "full normals");
            Require(mesh.uv.Length == 6 && mesh.uv[3].y == 1 && mesh.uv[4].x == 1, "UV offset and flip");
            Require(mesh.tangents.Length == 6, "full tangents");
            Require(mesh.tangents[3].z == 1 && mesh.tangents[3].w == -1, "authored tangent preserved");
        }));
        Run("attributes-before-missing", () => CheckMesh("{\"attributes\":{\"POSITION\":0,\"NORMAL\":2,\"TEXCOORD_0\":3,\"TANGENT\":4}},{\"attributes\":{\"POSITION\":0}}", mesh =>
        {
            Require(mesh.uv.Length == 6 && mesh.uv[0].y == 1 && mesh.uv[3].y == 0, "trailing UV defaults");
            Require(mesh.normals.Length == 6 && mesh.normals[0].y == 1, "authored normal preserved");
            Require(mesh.tangents.Length == 6 && mesh.tangents[0].z == 1 && mesh.tangents[0].w == -1, "authored tangent preserved");
        }));
        Run("mismatched-attribute-count", () => CheckMesh("{\"attributes\":{\"POSITION\":0,\"NORMAL\":5}},{\"attributes\":{\"POSITION\":0}}",
            mesh => Require(mesh.vertexCount == 3 && mesh.subMeshCount == 1, "mismatched primitive skipped atomically")));
        Run("missing-position", () => CheckMesh("{\"attributes\":{\"NORMAL\":2}},{\"attributes\":{\"POSITION\":0}}",
            mesh => Require(mesh.vertexCount == 3 && mesh.subMeshCount == 1, "missing POSITION skipped atomically")));
        Run("material-order-after-skip", () => WithModel("{\"attributes\":{\"POSITION\":0},\"mode\":1,\"material\":0},{\"attributes\":{\"POSITION\":0},\"material\":1},{\"attributes\":{\"POSITION\":0}}", model =>
        {
            var materials = model.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            Require(materials.Length == 2 && materials[0].name == "Blue" && materials[1].name == "DefaultGltfMaterial", "material slots follow accepted primitives");
        }));
        Run("missing-normals", () => CheckMesh("{\"attributes\":{\"POSITION\":0}}", mesh =>
            Require(mesh.normals.Length == 3 && mesh.normals[0].z > 0.99f, "generated normals and winding")));
        Run("generation-disabled", () => WithModel("{\"attributes\":{\"POSITION\":0}}", model =>
            Require(model.GetComponentInChildren<MeshFilter>().sharedMesh.normals.Length == 0, "normals remain absent"), generate: false));
        Run("invalid-index", () => CheckMesh("{\"attributes\":{\"POSITION\":0},\"indices\":1},{\"attributes\":{\"POSITION\":0}}",
            mesh => Require(mesh.vertexCount == 3 && mesh.subMeshCount == 1, "bad primitive skipped atomically"), badIndex: true));
        Run("partial-triangle", () => CheckMesh("{\"attributes\":{\"POSITION\":0},\"indices\":1},{\"attributes\":{\"POSITION\":0}}",
            mesh => Require(mesh.vertexCount == 3 && mesh.subMeshCount == 1, "partial triangle skipped"), indexCount: 2));
        Run("render-non-indexed", () => RenderTriangle(resultPath + ".png"));
        File.WriteAllLines(resultPath, _results);
        MelonLogger.Msg($"[MAPI.GlbTests] COMPLETE {_results.Count} cases; failures={_results.Count(x => x.StartsWith("FAIL"))}");
    }

    private void Run(string name, Action test)
    {
        try { test(); _results.Add($"PASS|{name}"); }
        catch (Exception ex) { _results.Add($"FAIL|{name}|{ex}"); }
        MelonLogger.Msg($"[MAPI.GlbTests] {_results[^1]}");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal(int[] actual, int[] expected) =>
        Require(actual.SequenceEqual(expected), $"indices: [{string.Join(",", actual)}] expected [{string.Join(",", expected)}]");

    private static void RenderTriangle(string path)
    {
        WithModel("{\"attributes\":{\"POSITION\":0}}", model =>
        {
            foreach (Transform child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            var cameraObject = new GameObject("MAPI GLB test camera");
            var texture = new RenderTexture(256, 256, 24);
            var pixels = new Texture2D(256, 256, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            var material = model.GetComponentInChildren<MeshRenderer>().sharedMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            Require(shader != null, "URP Unlit shader available for render probe");
            material.shader = shader;
            material.SetColor("_BaseColor", Color.red);
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 0.7f;
                camera.transform.position = new Vector3(-0.5f, 0.5f, 3);
                camera.transform.rotation = Quaternion.Euler(0, 180, 0);
                camera.targetTexture = texture;
                camera.Render();
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, ImageConversion.EncodeToPNG(pixels));
                Require(pixels.GetPixels().Count(color => color.r > 0.5f && color.g < 0.1f) > 5000, "visible front-facing red triangle");
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.Destroy(cameraObject);
                UnityEngine.Object.Destroy(texture);
                UnityEngine.Object.Destroy(pixels);
            }
        });
    }

    private static void CheckMesh(string primitives, Action<Mesh> assertion, int component = 5123, bool badIndex = false, int indexCount = 3) =>
        WithModel(primitives, model => assertion(model.GetComponentInChildren<MeshFilter>().sharedMesh), component, badIndex, indexCount);

    private static void WithModel(string primitives, Action<GameObject> assertion, int component = 5123, bool badIndex = false, int indexCount = 3, bool generate = true)
    {
        byte[] bytes = CreateGlb(primitives, component, badIndex, indexCount);
        GameObject? model = generate ? GltfLoader.LoadGlb(bytes) : new GltfImporter().GenerateNormals(false).GenerateTangents(false).LoadGlb(bytes);
        Require(model != null, "loader returned null");
        try { assertion(model!); }
        finally
        {
            if (model != null)
            {
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>()) UnityEngine.Object.Destroy(filter.sharedMesh);
                foreach (MeshRenderer renderer in model.GetComponentsInChildren<MeshRenderer>())
                    foreach (Material material in renderer.sharedMaterials) if (material != null) UnityEngine.Object.Destroy(material);
                UnityEngine.Object.Destroy(model);
            }
        }
    }

    // Synthetic, self-contained data. No game or third-party model assets are needed.
    private static byte[] CreateGlb(string primitives, int component, bool badIndex, int indexCount)
    {
        using var bin = new MemoryStream();
        using var writer = new BinaryWriter(bin);
        foreach (float f in new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }) writer.Write(f);
        foreach (uint i in new uint[] { 0, 1, badIndex ? 3u : 2u })
        {
            if (component == 5121) writer.Write((byte)i);
            else if (component == 5123) writer.Write((ushort)i);
            else writer.Write(i);
        }
        int indexBytes = (int)bin.Length - 36;
        while (bin.Length % 4 != 0) writer.Write((byte)0);
        int normalOffset = (int)bin.Length;
        for (int i = 0; i < 3; i++) { writer.Write(0f); writer.Write(1f); writer.Write(0f); }
        int uvOffset = (int)bin.Length;
        foreach (float f in new float[] { 0, 0, 1, 0, 0, 1 }) writer.Write(f);
        int tangentOffset = (int)bin.Length;
        for (int i = 0; i < 3; i++) { writer.Write(0f); writer.Write(0f); writer.Write(1f); writer.Write(-1f); }
        string json = "{\"asset\":{\"version\":\"2.0\"},\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0}]," +
            $"\"buffers\":[{{\"byteLength\":{bin.Length}}}],\"bufferViews\":[" +
            $"{{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36}},{{\"buffer\":0,\"byteOffset\":36,\"byteLength\":{indexBytes}}}," +
            $"{{\"buffer\":0,\"byteOffset\":{normalOffset},\"byteLength\":36}},{{\"buffer\":0,\"byteOffset\":{uvOffset},\"byteLength\":24}},{{\"buffer\":0,\"byteOffset\":{tangentOffset},\"byteLength\":48}}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\",\"min\":[0,0,0],\"max\":[1,1,0]}," +
            $"{{\"bufferView\":1,\"componentType\":{component},\"count\":{indexCount},\"type\":\"SCALAR\"}}," +
            "{\"bufferView\":2,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"},{\"bufferView\":3,\"componentType\":5126,\"count\":3,\"type\":\"VEC2\"},{\"bufferView\":4,\"componentType\":5126,\"count\":3,\"type\":\"VEC4\"},{\"bufferView\":2,\"componentType\":5126,\"count\":2,\"type\":\"VEC3\"}]," +
            "\"materials\":[{\"name\":\"Red\",\"pbrMetallicRoughness\":{\"baseColorFactor\":[1,0,0,1]}},{\"name\":\"Blue\",\"pbrMetallicRoughness\":{\"baseColorFactor\":[0,0,1,1]}}]," +
            $"\"meshes\":[{{\"primitives\":[{primitives}]}}]}}";
        while (Encoding.UTF8.GetByteCount(json) % 4 != 0) json += " ";
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
        using var glb = new MemoryStream();
        using var output = new BinaryWriter(glb);
        output.Write(0x46546C67u); output.Write(2u); output.Write((uint)(28 + jsonBytes.Length + bin.Length));
        output.Write(jsonBytes.Length); output.Write(0x4E4F534Au); output.Write(jsonBytes);
        output.Write((int)bin.Length); output.Write(0x004E4942u); output.Write(bin.ToArray());
        return glb.ToArray();
    }
}
