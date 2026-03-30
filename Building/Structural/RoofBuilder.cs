using S1MAPI.Building.Config;
using S1MAPI.Core;
using S1MAPI.ProceduralMesh;
using S1MAPI.Utils;
using UnityEngine;

namespace S1MAPI.Building.Structural
{
    /// <summary>
    /// Preset sizing for parapet roofs.
    /// </summary>
    public enum ParapetPreset
    {
        /// <summary>
        /// Tall thick parapet wall with prominent cap and overhang.
        /// Creates a visible roof recess for a substantial commercial look.
        /// </summary>
        Deep,

        /// <summary>
        /// Shorter parapet wall with a subtle cap and slight overhang.
        /// Cleaner, less imposing appearance.
        /// </summary>
        Shallow
    }

    /// <summary>
    /// Creates roof structures including parapet walls and hip roofs.
    /// Extracted from DecorBuilder for SRP compliance.
    /// </summary>
    public sealed class RoofBuilder
    {
        #region Fields

        private readonly Transform _parent;
        private readonly Vector3 _roomSize;
        private readonly float _wallThickness;
        private readonly BuildingPalette _palette;

        #endregion

        #region Constructor

        /// <summary>
        /// Create a new roof builder.
        /// </summary>
        /// <param name="parent">Parent transform for roof elements</param>
        /// <param name="roomSize">Room dimensions</param>
        /// <param name="wallThickness">Wall thickness in meters</param>
        /// <param name="palette">Material and color palette</param>
        public RoofBuilder(Transform parent, Vector3 roomSize, float wallThickness, BuildingPalette palette)
        {
            _parent = parent;
            _roomSize = roomSize;
            _wallThickness = wallThickness;
            _palette = palette;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Add a parapet roof (raised wall and cap above the roofline).
        /// The cap extends past the parapet wall by the overhang amount, creating a ledge.
        /// Includes a thin roof slab at ceiling height. For interior ceilings, use AddCeiling()
        /// separately — the ceiling sits just below the roof slab with no overlap.
        /// </summary>
        /// <param name="preset">Sizing preset (Deep or Shallow). Overridden by explicit dimensions.</param>
        /// <param name="parapetHeight">Height of the parapet wall in meters. Null uses preset default.</param>
        /// <param name="parapetDepth">Depth of the parapet wall. Null uses wall thickness + padding.</param>
        /// <param name="capHeight">Height of the cap on top of the parapet. Null uses preset default.</param>
        /// <param name="capOverhang">How far the cap extends past the parapet wall on each side. Null uses preset default.</param>
        /// <param name="parapetColor">Color override for the parapet wall.</param>
        /// <param name="parapetMaterial">Material override for the parapet wall.</param>
        /// <param name="capColor">Color override for the cap.</param>
        /// <param name="capMaterial">Material override for the cap.</param>
        /// <returns>The roof container GameObject.</returns>
        public GameObject AddParapetRoof(
            ParapetPreset preset = ParapetPreset.Deep,
            float? parapetHeight = null,
            float? parapetDepth = null,
            float? capHeight = null,
            float? capOverhang = null,
            Color? parapetColor = null,
            Material? parapetMaterial = null,
            Color? capColor = null,
            Material? capMaterial = null)
        {
            // Resolve preset defaults
            float pHeight = parapetHeight ?? (preset == ParapetPreset.Deep
                ? Constants.Roof.DeepParapetHeight
                : Constants.Roof.ShallowParapetHeight);

            float pDepth = parapetDepth ?? (_wallThickness + Constants.Roof.ParapetDepthPadding);

            float cHeight = capHeight ?? (preset == ParapetPreset.Deep
                ? Constants.Roof.DeepCapHeight
                : Constants.Roof.ShallowCapHeight);

            float cOverhang = capOverhang ?? (preset == ParapetPreset.Deep
                ? Constants.Roof.DeepCapOverhang
                : Constants.Roof.ShallowCapOverhang);

            // Cap extends past parapet on exterior side only
            float capDepthExt = pDepth + cOverhang;
            float capCornerWidth = pDepth + 2f * cOverhang;
            float capOffset = cOverhang / 2f;

            GameObject container = BuildingUtilities.CreateFolder("ParapetRoof", _parent);

            // --- Parapet wall (4 strips above ceiling) ---
            Color wallColor = parapetColor ?? _palette.TrimColor;
            float wallY = _roomSize.y + pHeight / 2f;

            GameObject parapetN = PrimitiveBuilder.CreateBox("Parapet_North",
                new Vector3(_roomSize.x / 2f, wallY, _roomSize.z),
                new Vector3(_roomSize.x + pDepth, pHeight, pDepth),
                wallColor, container.transform);

            GameObject parapetS = PrimitiveBuilder.CreateBox("Parapet_South",
                new Vector3(_roomSize.x / 2f, wallY, 0f),
                new Vector3(_roomSize.x + pDepth, pHeight, pDepth),
                wallColor, container.transform);

            GameObject parapetE = PrimitiveBuilder.CreateBox("Parapet_East",
                new Vector3(_roomSize.x, wallY, _roomSize.z / 2f),
                new Vector3(pDepth, pHeight, _roomSize.z - pDepth),
                wallColor, container.transform);

            GameObject parapetW = PrimitiveBuilder.CreateBox("Parapet_West",
                new Vector3(0f, wallY, _roomSize.z / 2f),
                new Vector3(pDepth, pHeight, _roomSize.z - pDepth),
                wallColor, container.transform);

            Material? wallMat = parapetMaterial ?? _palette.TrimMaterial;
            if (wallMat != null)
            {
                ApplyMaterial(parapetN, wallMat);
                ApplyMaterial(parapetS, wallMat);
                ApplyMaterial(parapetE, wallMat);
                ApplyMaterial(parapetW, wallMat);
            }

            // --- Cap (4 strips on top of parapet, overhang on exterior side only) ---
            Color cColor = capColor ?? _palette.AccentColor;
            float capY = _roomSize.y + pHeight + cHeight / 2f;

            GameObject capN = PrimitiveBuilder.CreateBox("Cap_North",
                new Vector3(_roomSize.x / 2f, capY, _roomSize.z + capOffset),
                new Vector3(_roomSize.x + capCornerWidth, cHeight, capDepthExt),
                cColor, container.transform);

            GameObject capS = PrimitiveBuilder.CreateBox("Cap_South",
                new Vector3(_roomSize.x / 2f, capY, -capOffset),
                new Vector3(_roomSize.x + capCornerWidth, cHeight, capDepthExt),
                cColor, container.transform);

            GameObject capE = PrimitiveBuilder.CreateBox("Cap_East",
                new Vector3(_roomSize.x + capOffset, capY, _roomSize.z / 2f),
                new Vector3(capDepthExt, cHeight, _roomSize.z - pDepth),
                cColor, container.transform);

            GameObject capW = PrimitiveBuilder.CreateBox("Cap_West",
                new Vector3(-capOffset, capY, _roomSize.z / 2f),
                new Vector3(capDepthExt, cHeight, _roomSize.z - pDepth),
                cColor, container.transform);

            Material? cMat = capMaterial ?? _palette.AccentMaterial ?? _palette.TrimMaterial;
            if (cMat != null)
            {
                ApplyMaterial(capN, cMat);
                ApplyMaterial(capS, cMat);
                ApplyMaterial(capE, cMat);
                ApplyMaterial(capW, cMat);
            }

            // --- Roof slab (thin slab at ceiling height, bottom sits at _roomSize.y) ---
            Material roofSlabMat = _palette.WallMaterial
                ?? MaterialPresets.Opaque(_palette.WallColor);
            float roofSlabThickness = Constants.Roof.DefaultBaseSlabHeight;
            PrimitiveBuilder.CreateBox("ParapetRoofSlab",
                new Vector3(_roomSize.x / 2f, _roomSize.y + roofSlabThickness / 2f, _roomSize.z / 2f),
                new Vector3(_roomSize.x, roofSlabThickness, _roomSize.z),
                roofSlabMat.color, container.transform);

            return container;
        }

        /// <summary>
        /// Add a hip (four-slope) roof using custom mesh geometry.
        /// All four sides slope inward to a central ridge that is shorter than the building length.
        /// For square buildings, the ridge collapses to a point (pyramid roof).
        /// A base slab sits at ceiling height and the slopes start from the top of the slab,
        /// giving the roof visible thickness when viewed from below.
        /// For interior ceilings, use AddCeiling() separately — the ceiling sits just below
        /// the roof slab with no overlap.
        /// </summary>
        /// <param name="ridgeHeight">Height of the ridge peak above the ceiling in meters.</param>
        /// <param name="overhang">How far the roof eaves extend past the walls in meters.</param>
        /// <param name="ridgeAlongX">If true, ridge runs along X axis. If false, along Z. Null auto-selects the longer axis.</param>
        /// <param name="roofColor">Color for the sloped roof planes.</param>
        /// <param name="roofMaterial">Material for the sloped roof planes. Null uses fallback color.</param>
        /// <param name="baseSlabHeight">Height of the 3D base slab beneath the slopes. 0 disables the slab.</param>
        /// <returns>The roof container GameObject.</returns>
        public GameObject AddHipRoof(
            float ridgeHeight = Constants.Roof.DefaultRidgeHeight,
            float overhang = Constants.Roof.DefaultOverhang,
            bool? ridgeAlongX = null,
            Color? roofColor = null,
            Material? roofMaterial = null,
            float baseSlabHeight = Constants.Roof.DefaultBaseSlabHeight)
        {
            bool alongX = ridgeAlongX ?? (_roomSize.x >= _roomSize.z);
            GameObject container = BuildingUtilities.CreateFolder("HipRoof", _parent);

            Material? foundHipMat = MaterialPresets.FindExistingMaterial(Constants.Roof.RoofSlopeMaterialName);
            if (foundHipMat == null)
            {
                DebugLog.Warning($"[RoofBuilder] Material '{Constants.Roof.RoofSlopeMaterialName}' not found. Using fallback color.");
            }

            Material slopeMat = roofMaterial
                ?? foundHipMat
                ?? MaterialPresets.Opaque(roofColor ?? new Color(
                    Constants.Roof.DefaultRoofColorR,
                    Constants.Roof.DefaultRoofColorG,
                    Constants.Roof.DefaultRoofColorB));

            float ceilingY = _roomSize.y;
            float eaveY = ceilingY + baseSlabHeight; // slopes start on top of the slab
            float ridgeY = ceilingY + ridgeHeight;

            // Base slab gives the roof visible thickness from below
            if (baseSlabHeight > 0f)
            {
                Material slabMat = _palette.WallMaterial
                    ?? MaterialPresets.Opaque(_palette.WallColor);
                CreateRoofBaseSlab(container.transform, ceilingY, baseSlabHeight, overhang, slabMat);
            }

            if (alongX)
                CreateHipAlongX(container.transform, eaveY, ridgeY, overhang, slopeMat);
            else
                CreateHipAlongZ(container.transform, eaveY, ridgeY, overhang, slopeMat);

            return container;
        }

        #endregion

        #region Private Methods

        private static void CreateSlopeQuad(
            string name, Transform parent,
            Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3,
            float uvWidth, float uvHeight, Material material)
        {
            new CustomMeshBuilder(name)
                .AddVertices(v0, v1, v2, v3)       // front face: 0-3
                .AddVertices(v0, v1, v2, v3)       // back face:  4-7
                .AddQuad(0, 1, 2, 3)               // front
                .AddQuad(7, 6, 5, 4)               // back (reversed winding)
                .SetUVs(
                    new Vector2(0f, 0f),
                    new Vector2(uvWidth, 0f),
                    new Vector2(uvWidth, uvHeight),
                    new Vector2(0f, uvHeight),
                    new Vector2(0f, 0f),
                    new Vector2(uvWidth, 0f),
                    new Vector2(uvWidth, uvHeight),
                    new Vector2(0f, uvHeight))
                .SetMaterial(material)
                .Build(parent);
        }

        private void CreateHipAlongX(
            Transform container, float ceilingY, float ridgeY, float overhang,
            Material slopeMat)
        {
            float oh = overhang;
            float rise = ridgeY - ceilingY;
            float ridgeZ = _roomSize.z / 2f;

            // Ridge inset from each end — equal to half the short axis for equal pitch on all sides
            float ridgeInset = _roomSize.z / 2f;
            float ridgeMinX = ridgeInset;
            float ridgeMaxX = _roomSize.x - ridgeInset;

            // Eave corners
            Vector3 sw = new Vector3(-oh, ceilingY, -oh);
            Vector3 se = new Vector3(_roomSize.x + oh, ceilingY, -oh);
            Vector3 ne = new Vector3(_roomSize.x + oh, ceilingY, _roomSize.z + oh);
            Vector3 nw = new Vector3(-oh, ceilingY, _roomSize.z + oh);

            float sideRun = ridgeZ + oh;
            float sideSlopeLen = Mathf.Sqrt(rise * rise + sideRun * sideRun);

            if (ridgeMaxX > ridgeMinX)
            {
                // Standard hip: ridge line with positive length
                Vector3 ridgeW = new Vector3(ridgeMinX, ridgeY, ridgeZ);
                Vector3 ridgeE = new Vector3(ridgeMaxX, ridgeY, ridgeZ);

                float eaveLen = _roomSize.x + 2f * oh;

                // South slope (trapezoid)
                CreateSlopeQuad("Slope_South", container,
                    se, sw, ridgeW, ridgeE,
                    eaveLen, sideSlopeLen, slopeMat);

                // North slope (trapezoid)
                CreateSlopeQuad("Slope_North", container,
                    nw, ne, ridgeE, ridgeW,
                    eaveLen, sideSlopeLen, slopeMat);

                // Hip end slopes
                float hipBaseLen = _roomSize.z + 2f * oh;
                float hipRun = ridgeInset + oh;
                float hipSlopeLen = Mathf.Sqrt(rise * rise + hipRun * hipRun);

                // East hip (triangle)
                CreateSlopeTriangle("Hip_East", container,
                    ne, se, ridgeE,
                    hipBaseLen, hipSlopeLen, slopeMat);

                // West hip (triangle)
                CreateSlopeTriangle("Hip_West", container,
                    sw, nw, ridgeW,
                    hipBaseLen, hipSlopeLen, slopeMat);
            }
            else
            {
                // Pyramid: ridge collapsed to a point
                Vector3 apex = new Vector3(_roomSize.x / 2f, ridgeY, ridgeZ);

                float sideBaseLen = _roomSize.x + 2f * oh;
                float hipBaseLen = _roomSize.z + 2f * oh;
                float hipRun = _roomSize.x / 2f + oh;
                float hipSlopeLen = Mathf.Sqrt(rise * rise + hipRun * hipRun);

                CreateSlopeTriangle("Slope_South", container,
                    se, sw, apex, sideBaseLen, sideSlopeLen, slopeMat);
                CreateSlopeTriangle("Slope_North", container,
                    nw, ne, apex, sideBaseLen, sideSlopeLen, slopeMat);
                CreateSlopeTriangle("Hip_East", container,
                    ne, se, apex, hipBaseLen, hipSlopeLen, slopeMat);
                CreateSlopeTriangle("Hip_West", container,
                    sw, nw, apex, hipBaseLen, hipSlopeLen, slopeMat);
            }
        }

        private void CreateHipAlongZ(
            Transform container, float ceilingY, float ridgeY, float overhang,
            Material slopeMat)
        {
            float oh = overhang;
            float rise = ridgeY - ceilingY;
            float ridgeX = _roomSize.x / 2f;

            // Ridge inset from each end — equal to half the short axis for equal pitch on all sides
            float ridgeInset = _roomSize.x / 2f;
            float ridgeMinZ = ridgeInset;
            float ridgeMaxZ = _roomSize.z - ridgeInset;

            // Eave corners
            Vector3 sw = new Vector3(-oh, ceilingY, -oh);
            Vector3 se = new Vector3(_roomSize.x + oh, ceilingY, -oh);
            Vector3 ne = new Vector3(_roomSize.x + oh, ceilingY, _roomSize.z + oh);
            Vector3 nw = new Vector3(-oh, ceilingY, _roomSize.z + oh);

            float sideRun = ridgeX + oh;
            float sideSlopeLen = Mathf.Sqrt(rise * rise + sideRun * sideRun);

            if (ridgeMaxZ > ridgeMinZ)
            {
                // Standard hip: ridge line with positive length
                Vector3 ridgeS = new Vector3(ridgeX, ridgeY, ridgeMinZ);
                Vector3 ridgeN = new Vector3(ridgeX, ridgeY, ridgeMaxZ);

                float eaveLen = _roomSize.z + 2f * oh;

                // West slope (trapezoid)
                CreateSlopeQuad("Slope_West", container,
                    sw, nw, ridgeN, ridgeS,
                    eaveLen, sideSlopeLen, slopeMat);

                // East slope (trapezoid)
                CreateSlopeQuad("Slope_East", container,
                    ne, se, ridgeS, ridgeN,
                    eaveLen, sideSlopeLen, slopeMat);

                // Hip end slopes
                float hipBaseLen = _roomSize.x + 2f * oh;
                float hipRun = ridgeInset + oh;
                float hipSlopeLen = Mathf.Sqrt(rise * rise + hipRun * hipRun);

                // North hip (triangle)
                CreateSlopeTriangle("Hip_North", container,
                    nw, ne, ridgeN,
                    hipBaseLen, hipSlopeLen, slopeMat);

                // South hip (triangle)
                CreateSlopeTriangle("Hip_South", container,
                    se, sw, ridgeS,
                    hipBaseLen, hipSlopeLen, slopeMat);
            }
            else
            {
                // Pyramid: ridge collapsed to a point
                Vector3 apex = new Vector3(ridgeX, ridgeY, _roomSize.z / 2f);

                float sideBaseLen = _roomSize.z + 2f * oh;
                float hipBaseLen = _roomSize.x + 2f * oh;
                float hipRun = _roomSize.z / 2f + oh;
                float hipSlopeLen = Mathf.Sqrt(rise * rise + hipRun * hipRun);

                CreateSlopeTriangle("Slope_West", container,
                    sw, nw, apex, sideBaseLen, sideSlopeLen, slopeMat);
                CreateSlopeTriangle("Slope_East", container,
                    ne, se, apex, sideBaseLen, sideSlopeLen, slopeMat);
                CreateSlopeTriangle("Hip_North", container,
                    nw, ne, apex, hipBaseLen, hipSlopeLen, slopeMat);
                CreateSlopeTriangle("Hip_South", container,
                    se, sw, apex, hipBaseLen, hipSlopeLen, slopeMat);
            }
        }

        private static void CreateSlopeTriangle(
            string name, Transform parent,
            Vector3 v0, Vector3 v1, Vector3 v2,
            float uvWidth, float uvHeight, Material material)
        {
            new CustomMeshBuilder(name)
                .AddVertices(v0, v1, v2)       // front face: 0-2
                .AddVertices(v0, v1, v2)       // back face:  3-5
                .AddTriangle(0, 1, 2)          // front
                .AddTriangle(5, 4, 3)          // back (reversed winding)
                .SetUVs(
                    new Vector2(0f, 0f),
                    new Vector2(uvWidth, 0f),
                    new Vector2(uvWidth / 2f, uvHeight),
                    new Vector2(0f, 0f),
                    new Vector2(uvWidth, 0f),
                    new Vector2(uvWidth / 2f, uvHeight))
                .SetMaterial(material)
                .Build(parent);
        }

        private void CreateRoofBaseSlab(
            Transform container, float ceilingY, float slabHeight, float overhang, Material material)
        {
            float oh = overhang;
            float slabWidth = _roomSize.x + 2f * oh;
            float slabDepth = _roomSize.z + 2f * oh;
            float slabCenterY = ceilingY + slabHeight / 2f;

            GameObject slab = PrimitiveBuilder.CreateBox("RoofBaseSlab",
                new Vector3(_roomSize.x / 2f, slabCenterY, _roomSize.z / 2f),
                new Vector3(slabWidth, slabHeight, slabDepth),
                material.color, container);

            ApplyMaterial(slab, material);
        }

        private static void ApplyMaterial(GameObject obj, Material material)
        {
            Renderer r = obj.GetComponent<Renderer>();
            if (r != null) r.material = material;
        }

        #endregion
    }
}
