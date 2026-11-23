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
    public const int maxSkyShadeDisWithSun = 25;
    private const float skyOccludedShade = 0.2f;
    private const float abientOcclusionShade = 0.05f;

    public readonly List<ShapeInstanceAndTint> shapeInstances = [];
    public GridAlignedQuadInstanceAndTints[] quadInstances = [];
    private readonly List<Vector3> tintStorage = []; // We don't want to allocate this for every shape
    private readonly GreedyQuadInstancer greedyQuadInstancer = new();

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
        Vector3 dataTint = Vector3.One;
        if (cluster.TryGetBlockData(blockPos.Floor(), out ChunkCluster<TChunkDims>.IBlockData? blockdata))
        {
            if (blockdata is TintBlockData<TChunkDims> tintData)
                dataTint = tintData.tint;
            if (blockdata is WaterBlockData<TChunkDims> waterData)
                dataTint = waterData.tint;
        }
        for (int i = 0; i < facesVisible.Count; i++)
        {
            float shadow = GetShadow(blockPos, block, facesVisible[i]);
            if (block == 1 && facesVisible[i] is not (Direction.Top or Direction.Bottom))
            {
                // Create another face for the Grass Side to fill in the bottom with dirt with no tint.
                BlockRenderData grassSideDirtRD = BlockRenderData.renderDataByBlock[GrassSideDirt];
                BlockRenderInstance grassSideDirtInstance = grassSideDirtRD
                    .InstanceMixTints(localBlockPosition, [Vector3.One * shadow], [facesVisible[i]], Direction.Top, 0);
                greedyQuadInstancer.AddRange(grassSideDirtInstance.quadInstancesAndTints);
            }
            tintStorage.Add(dataTint * shadow);
        }
        // Rotation
        Direction up = Direction.Top;
        int forward = 0;

        BlockRenderInstance blockInstance = renderData.InstanceMixTints(localBlockPosition, tintStorage, facesVisible, up, forward);
        shapeInstances.AddRange(blockInstance.shapeInstancesAndTints);
        greedyQuadInstancer.AddRange(blockInstance.quadInstancesAndTints);
    }

    /// <summary>Tests if a face of a block is occluded from a light source.</summary>
    /// <param name="blockPos">The position of the block being tested.</param>
    /// <param name="faceNormal">The face of the block being tested. Position tests will get offset using this.</param>
    /// <param name="lightSourceBlock">The light source block, the block the raycast expects to hit.</param>
    /// <param name="chunkCluster">The chunk cluster to raycast with.</param>
    /// <param name="directionToLight">
    /// From the <paramref name="blockPos"/> the direction to the light. Must be normalized.
    /// The <paramref name="faceNormal"/> position offset does not modify this, in some cases leading to incorrect results.
    /// </param>
    /// <param name="distanceToLight">
    /// From the <paramref name="blockPos"/> the distance to the light.
    /// The <paramref name="faceNormal"/> position offset does not modify this, in some cases leading to incorrect results.
    /// </param>
    /// <param name="lightDotProduct">
    /// The dot product or how similar <paramref name="faceNormal"/> is to <paramref name="directionToLight"/>. <br/>
    /// Uses Vector3.Dot(<paramref name="directionToLight"/>, <paramref name="faceNormal"/>.ToVector()). <br/>
    /// If these two point in the exact same direction this will be 1f.
    /// If they point in opposite directions this will be -1f.
    /// </param>
    /// <param name="raycastResult">The raycast result provided by <paramref name="chunkCluster"/>.</param>
    /// <returns>
    /// True if the blocks face could not see the light. <br/>
    /// False if the face was facing the light, the light was closer than <paramref name="distanceToLight"/>
    /// and the path was not obstructed.
    /// </returns>
    public static bool IsLightOccluded
    (
        Vector3 blockPos, Direction faceNormal,
        ushort lightSourceBlock, ChunkCluster<TChunkDims> chunkCluster,
        Vector3 directionToLight, float distanceToLight,
        out float lightDotProduct,
        out RaycastResult? raycastResult
    )
    {
        Vector3 faceNormalVec = faceNormal.ToVector();
        lightDotProduct = Vector3.Dot(directionToLight, faceNormalVec);
        raycastResult = null;
        if (lightDotProduct < 0f)
            return true;
        Vector3 facePosition = blockPos + FaceCenters[(int)faceNormal];
        distanceToLight = MathF.Min(distanceToLight, MaxShadowTraceDistance);
        raycastResult = chunkCluster.Raycast(facePosition, directionToLight, distanceToLight);
        if (raycastResult.Distance > distanceToLight || raycastResult.Block != lightSourceBlock)
            return true;
        return false;
    }

    private readonly float GetShadow(Vector3 blockPos, ushort block, Direction faceNormal)
    {
        float brightness = 1;
        Vector3 directionVec = faceNormal.ToVector();
        Vector3 facePosition = blockPos + FaceCenters[(int)faceNormal];
        // Sun occlusion
        if (!IsLightOccluded(blockPos, faceNormal, 0, cluster, -sunDirection, MaxShadowTraceDistance, out float sunDot, out _))
            brightness -= sunOccludedShade * (1 - sunDot);
        else
            brightness -= sunOccludedShade;
        // Sky occlusion
        if
        (
            IsLightOccluded
            (
                blockPos, faceNormal,
                0, cluster,
                Vector3.UnitY, maxSkyShadeDisWithSun,
                out _,
                out RaycastResult? skyRayResult
            ) &&
            skyRayResult != null
        )
        {
            float distanceInBlocks = MathF.Min(MathF.Floor(skyRayResult!.Distance - 0.5f), maxSkyShadeDisWithSun);
            brightness -= skyOccludedShade * (1 - (distanceInBlocks / maxSkyShadeDisWithSun));
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

    public void CullFinalize() => quadInstances = greedyQuadInstancer.Merge();
}