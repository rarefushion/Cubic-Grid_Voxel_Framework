using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using GalensUnified.Debugging.ImGui;
using Silk.NET.Maths;
using static Program;

// Manages chunk meshing and remeshing
public class ChunkMeshManager<TChunkDims>
(
    ChunkCluster<TChunkDims> cluster,
    ShapeShader shapeShader,
    GridAlignedQuadShader quadShader,
    Vector3 sunDirection,
    float sunOccludedShade,
    float minBrightness
)
where TChunkDims : IChunkDims
{

    public readonly Vector3 sunDirection = sunDirection;
    public readonly float sunOccludedShade = sunOccludedShade;
    public readonly float minBrightness = minBrightness;

    private ulong nextTicket = 0;

    public readonly DeltaLogs meshGenerationTimes = new("Instance/Cull T1K", 1000);

    private record ChunkMesh
    (
        Vector3 Position,
        ulong Ticket,
        ShapeInstance[] ShapeInstances, Vector3[] ShapeTints,
        GridAlignedQuadInstance[] QuadInstances, Vector3[] QuadTints
    );
    private readonly ConcurrentQueue<ChunkMesh> NeedRendering = [];
    private readonly ConcurrentDictionary<Vector3D<int>, ulong> chunkCounters = [];

    public void Update()
    {
        while (NeedRendering.TryDequeue(out var result))
        {
            RenderMesh(result);
            if (OverTargtetFrameTime())
                return;
        }
    }

    public void RenderAsync(Vector3D<int> chunk) =>
        backgroundThreadBatch.EnqueueJob(() => NeedRendering.Enqueue(CreateMesh(chunk)));

    public void RenderSynchronous(Vector3D<int> chunk)
    {
        DeactivateChunk((Vector3)chunk);
        if (cluster.IsActive(chunk))
            RenderMesh(CreateMesh(chunk));
    }

    private void RenderMesh(ChunkMesh mesh)
    {
        if (!chunkCounters.TryGetValue(mesh.Position.Floor(), out ulong ticket) || mesh.Ticket != ticket)
            return;
        DeactivateChunk(mesh.Position);
        if (!cluster.IsActive(mesh.Position.Floor()))
            return;
        if (mesh.ShapeInstances.Length > 0)
            shapeShader.RenderChunk
            (
                mesh.Position,
                mesh.ShapeInstances,
                mesh.ShapeTints
            );
        if (mesh.QuadInstances.Length > 0)
            quadShader.RenderChunk
            (
                mesh.Position,
                mesh.QuadInstances,
                mesh.QuadTints
            );
    }

    private ChunkMesh CreateMesh(Vector3D<int> chunk)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        ulong ticket = Interlocked.Increment(ref nextTicket);
        chunkCounters.AddOrUpdate(chunk, ticket, (_, _) => ticket);
        ShapeInstancer<TChunkDims> cullingHandler = new((Vector3)chunk, sunDirection, sunOccludedShade, minBrightness, cluster);
        cullingHandler = cluster.CullChunk(chunk, cullingHandler);
        ChunkMesh toReturn = new
        (
            (Vector3)chunk,
            ticket,
            [.. cullingHandler.shapeInstances.Select(i => i.ShapeInstance)],
            [.. cullingHandler.shapeInstances.Select(i => i.Tint)],
            [.. cullingHandler.quadInstances.Select(i => i.QuadInstance)],
            [.. cullingHandler.quadInstances.SelectMany(i => i.Tints)]
        );
        stopwatch.Stop();
        meshGenerationTimes.LogDelta(stopwatch.Elapsed.Milliseconds);
        return toReturn;
    }

    public void DeactivateChunk(Vector3 chunk)
    {
        chunkCounters.Remove(chunk.Floor(), out _);
        shapeShader.DeactivateChunk(chunk);
        quadShader.DeactivateChunk(chunk);
    }
}