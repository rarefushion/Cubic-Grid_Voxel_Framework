using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;

using static BlockIDs;
using static GenerationValues;
using static GalensUnified.CubicGrid.Core.Raycasting;

/// <summary>
/// Handles shading, color tinting and creation of <see cref="ShapeInstance"/>s.<br/>
/// Called by <see cref="BlockCulling"/> through <see cref="IBlockCullingHandler"/> to know what shapes to instance.<br/>
/// This is created when a chunk is being culled/rendered and then scrapped.<br/>
/// </summary>
/// <param name="chunkPosition">The chunk position this is responsible for creating.</param>
/// <param name="sunDirection">The direction of the global light source (sun).</param>
/// <param name="sunOccludedShade">The amount to shade when the global light source(sun) is not visible to the shape.</param>
/// <param name="minBrightness">The minimum brightness a shape can be.</param>
/// <param name="cluster">The cluster to use for sun occlusion raycasts.</param>
struct ShapeInstancer<TChunkDims>
(
    Vector3 chunkPosition,
    Vector3 sunDirection,
    float sunOccludedShade,
    float minBrightness,
    ChunkCluster<TChunkDims> cluster
) : IBlockCullingHandler
where TChunkDims : IChunkDims
{
    private const int MaxShadowTraceDistance = 500;
    private const int maxSkyShadeDisWithSun = 25;
    private const float skyOccludedShade = 0.2f;
    private const float abientOcclusionShade = 0.05f;

    public readonly List<ShapeInstance> instances = [];
    private readonly List<Vector3> tintStorage = []; // We don't want to allocate this for every shape

    /// <summary>Gets the center of a face of a cube using the standardized order: -z, +z, +y, -y, -x then +x.</summary>
    public static readonly Vector3[] FaceCenters =
    [
        new( 0.50f,  0.50f, -0.01f),
        new( 0.50f,  0.50f,  1.01f),
        new( 0.50f,  1.01f,  0.50f),
        new( 0.50f, -0.01f,  0.50f),
        new(-0.01f,  0.50f,  0.50f),
        new( 1.01f,  0.50f,  0.50f),
    ];

    public readonly void CullBegan() { }

    public readonly void ShapeVisible(Vector3 localBlockPosition, ushort block, List<Direction> facesVisible)
    {
        Vector3 blockPos = chunkPosition + localBlockPosition;
        BlockRenderData renderData = BlockRenderData.renderDataByBlock[block];
        tintStorage.Clear();
        for (int i = 0; i < facesVisible.Count; i++)
        {
            if (block == 1 && facesVisible[i] is not (Direction.Top or Direction.Bottom))
            {
                // Create another face for the Grass Side to fill in the bottom with dirt with no tint.
                float shadow = GetShadow(blockPos, block, facesVisible[i]);
                BlockRenderData grassSideDirtRD = BlockRenderData.renderDataByBlock[GrassSideDirt];
                instances.AddRange(grassSideDirtRD.Instance(localBlockPosition, [Vector3.One * shadow], [facesVisible[i]], Direction.Top, 0));
            }
            tintStorage.Add(GetShadedTint(blockPos, block, facesVisible[i]));
        }
        // Rotation
        Direction up = Direction.Top;
        int forward = 0;
        instances.AddRange(renderData.Instance(localBlockPosition, tintStorage, facesVisible, up, forward));
    }

    private readonly float GetShadow(Vector3 blockPos, ushort block, Direction faceNormal)
    {
        float brightness = 1;
        Vector3 directionVec = faceNormal.ToVector();
        Vector3 facePosition = blockPos + FaceCenters[(int)faceNormal];
        // Sun occlusion
        float sunDot = Vector3.Dot(-sunDirection, directionVec);
        if (sunDot > 0f)
        {
            if (cluster.Raycast(facePosition, -sunDirection, MaxShadowTraceDistance).Block != Air)
                brightness -= sunOccludedShade;
            else
                brightness -= sunOccludedShade * (1 - sunDot);
        }
        else
            brightness -= sunOccludedShade;
        // Sky occlusion
        RaycastResult skyRayResult = cluster.Raycast(facePosition, Vector3.UnitY, maxSkyShadeDisWithSun);
        if (skyRayResult.Block != Air)
        {
            brightness -= skyOccludedShade * (1 - (MathF.Min(MathF.Floor(skyRayResult.Distance - 0.5f), maxSkyShadeDisWithSun) / maxSkyShadeDisWithSun));
        }
        // Ambient occlusion
        foreach (Direction testDirection in Enum.GetValues<Direction>())
        {
            Vector3 testVec = testDirection.ToVector();
            if (directionVec == testVec || directionVec == -testVec)
                continue;
            RaycastResult testRayResult = cluster.Raycast(facePosition, testVec, 1);
            if (testRayResult.Block != 0 && testRayResult.Distance < 1f)
                brightness -= abientOcclusionShade;
        }
        // Cave fog
        if (facePosition.Y < MinTerrainHeight)
            brightness *= float.Lerp(1, minBrightness, (MathF.Max(facePosition.Y, MinTerrainHeight - 32) - MinTerrainHeight) / -32);
        return MathF.Max(brightness, minBrightness);
    }

    private readonly Vector3 GetShadedTint(Vector3 blockPos, ushort block, Direction faceNormal)
    {
        float shadow = GetShadow(blockPos, block, faceNormal);

        Vector3 tint = Vector3.One;
        if (block == Grass && faceNormal != Direction.Bottom) // Only Grass, not bottoms
        {
            tint = Vector3.Lerp(AutumnColor, LushColor, GetTemperature(blockPos));
        }
        if (block == OakLeaves)
            tint = Vector3.Lerp(AutumnColor, LushColor, GetTemperature(blockPos));
        if (WaterRendering.IsWater(block))
            tint = Vector3.Lerp(AutumnWaterColor, LushWaterColor, GetTemperature(blockPos));
        return tint * shadow;
    }
}