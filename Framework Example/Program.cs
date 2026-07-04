using System.Numerics;
using GalensUnified.CubicGrid.Core;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Microsoft.DotNet.PlatformAbstractions;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

// ChunkDims can be switched out for other sized chunks.
// Core.ChunkDims are chunks of length 16 (there for volume of 4096).
// Others exist for 8(Core.HalfChunkDims), 32(Core.DoubleChunkDims), 64 and 128.
using ChunkDimensions = GalensUnified.CubicGrid.Core.ChunkDims;

using static BlockIDs;
using GalensUnified.CubicGrid.Framework.Player;
using System.Collections.Concurrent;
using GalensUnified.CubicGrid.Renderer.NET.Shapes;

static class Program
{
    // Startup Values
    const int renderDistance = 32;
    const int renderHeight = 4;
    public const bool lockGenerationHeight = true; // Disable for infinite Downward generation
    public const int WorldHeightInChunks = renderHeight * 2 + 1;
    const int WorldLengthInChunks = renderDistance * 2 + 1;
    public const int seed = 1337;
    public const float worldScale = 0.01f;
    public const int mountainHeight = 50;
    public static Vector3 camStartPos = new(8, mountainHeight + 8, 8);
    const float InteractionRange = 10f;
    public const int targetFrameRate = 60;
    public static readonly TimeSpan targetFrameTime = new(0, 0, 0, 0, 1000 / targetFrameRate);
    // Runtime
    public static bool cursorVisible = false;
    public static float moveSpeed = 2f;
    public static Vector2 previousMousePosition;
    public static DateTime frameStart = DateTime.Now;
    // Use this over Task.Run instead use ThreadBatch.EnqueueJob for better performance.
    public static readonly ThreadBatch backgroundThreadBatch = new(Environment.ProcessorCount - 1, ThreadPriority.Normal);

    static void Main(string[] args)
    {
        WindowOptions options = WindowOptions.Default;
        options.Title = "Cubic-Grid Voxel Rendering Example";
        options.PreferredDepthBufferBits = 32;
        options.Samples = 8;
        IWindow window = Window.Create(options);
        window.Load += () => Load(window);
        window.Run();
    }

    static void Load(IWindow window)
    {
        // Camera
        Vector3 camPosition = camStartPos;
        Vector2 camRotation = Vector2.Zero; // Pitch, Yaw
        float mouseSensitivity = 0.0025f;
        float camFov = MathF.PI * (120f / 360f);
        float camAspectRatio = (float)window.Size.X / window.Size.Y;
        float camNearPlane = 0.1f;
        float camFarPlane = 2000f;

        // Inputs
        IInputContext input = window.CreateInput();
        input.Mice[0].Cursor.CursorMode = CursorMode.Raw;
        input.Keyboards[0].KeyDown += (keboard, key, num) =>
        {
            if (key == Key.Escape)
                Environment.Exit(0);
            if (key == Key.E)
            {
                cursorVisible = !cursorVisible;
                input.Mice[0].Cursor.CursorMode = cursorVisible ? CursorMode.Normal : CursorMode.Raw;
            }
            if (key == Key.F3)
                DebugRenderer.showDebugInfo = !DebugRenderer.showDebugInfo;
        };
        previousMousePosition = input.Mice[0].Position;
        input.Mice[0].MouseMove += (mouse, pos) => camRotation += GetCameraRotationDelta(mouse, pos, mouseSensitivity);
        window.Update += delta => camPosition += GetCameraPositionDelta(delta, input, camRotation.Y);

        // Load assets
        DirectoryInfo assets = Directory.CreateDirectory(Path.Combine(ApplicationEnvironment.ApplicationBasePath, "Assets"));
        FileInfo[] textureFiles = Directory.CreateDirectory(Path.Combine(assets.FullName, "Textures")).GetFiles();
        TextureLoader.Texture[] textures = TextureLoader.LoadImages(textureFiles);
        BlockRenderData.Factory BRDFactory = new(textures);
        // Create Blocks
        // Shapes
        List<Shape> shapes = [];
        Cube cube = new();
        shapes.AddRange(cube.Create(shapes.Count));
        shapes.AddRange(WaterRendering.CreatShapes(cube.shapeIDByFace[(int)Direction.Top], cube.shapeIDByFace[(int)Direction.Bottom], shapes.Count));
        // Faces are named by the Assets/Textures file name.
        BlockRenderData.renderDataByBlock =
        [
            // Air
            new(0, 0, 0, 0, 0, 0, cube),
            // Grass
            BRDFactory.CreateWithNames("Grass_Side", "Grass_Side", "Grass", "Dirt", "Grass_Side", "Grass_Side", cube),
            // GrassSideDirt
            BRDFactory.CreateWithNames("Grass_Side_Dirt", "Grass_Side_Dirt", "Dirt", "Dirt", "Grass_Side_Dirt", "Grass_Side_Dirt", cube),
            // Dirt
            BRDFactory.CreateWithName("Dirt", cube),
            // Stone
            BRDFactory.CreateWithName("Stone", cube),
            // OakLog
            BRDFactory.CreateWithName("oak_log", cube),
            // OakLeaves
            BRDFactory.CreateWithName("oak_leaves", cube),
            // WaterFull
            BRDFactory.CreateWithName("Water", cube),
            // Water Levels
            .. WaterRendering.shapeByWaterLevel.Select(shape => BRDFactory.CreateWithName("Water", shape))
        ];
        for (ushort i = 0; i < BlockRenderData.renderDataByBlock.Length; i++)
            BlockCulling.transparencyModeByBlock.TryAdd(i, BlockCulling.TransparencyMode.Opaque);
        BlockCulling.transparencyModeByBlock[OakLeaves] = BlockCulling.TransparencyMode.RenderOnTransparent;
        for (ushort i = 0; i <= WaterLevels; i++)
            BlockCulling.transparencyModeByBlock[WaterRendering.GetBlock(i)] = BlockCulling.TransparencyMode.RenderOnTransparent;

        // Create Graphics and Shader
        long worldVolume = checked(WorldLengthInChunks * WorldLengthInChunks * WorldHeightInChunks * ChunkDimensions.Volume);
        if (worldVolume > int.MaxValue)
            throw new IndexOutOfRangeException($"{typeof(ChunkCluster<ChunkDimensions>).Name} does not allow world volume > {int.MaxValue:N0}. Current: {worldVolume:N0}");
        GL graphics = window.CreateOpenGL();
        graphics.Enable(EnableCap.DepthTest);
        graphics.DepthFunc(DepthFunction.Less);
        graphics.Enable(EnableCap.CullFace);
        graphics.CullFace(GLEnum.Back); // Back face culling doesn't seem to effect performance but looks better for transparent blocks.
        graphics.Enable(EnableCap.Multisample);
        graphics.Enable(EnableCap.SampleAlphaToCoverage);
        graphics.ClearColor(System.Drawing.Color.CornflowerBlue);
        window.Resize += size => graphics.Viewport(0, 0, (uint)size.X, (uint)size.Y);
        window.Update += delta => graphics.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        // Ambiguous between mine and Silk.NET.OpenGL.Shader :sob:
        GalensUnified.CubicGrid.Renderer.NET.Shader shader = new
        (
            graphics,
            Path.Combine(assets.FullName, "GLSL"),
            ChunkDimensions.Length,
            ChunkDimensions.Volume  * ShapeInstance.MemorySize * 32, // chunkVolume * sizeof(BlockInstance) * vram batch size in chunks
            camNearPlane,
            [.. textures.Select(t => t.Image)],
            [.. shapes],
            messageErr => Console.WriteLine(messageErr),
            messageLog => Console.WriteLine(messageLog)
        );
        window.Render += dt => shader.Render
        (
            CameraMatrices.CreateProjectionMatrix(camFov, camAspectRatio, camNearPlane, camFarPlane),
            CameraMatrices.CreateViewMatrix(camPosition, camRotation.X, camRotation.Y, 0)
        );
        // Sun
        Vector3 sunColor = new(1f, 0.9f, 1f);
        Vector3 sunDirection = Vector3.Normalize(new // Each axis rotation is scaled -1 to +1 (pos = -direction * distance)
            (
                (float)Random.Shared.NextDouble() * 2.0f - 1.0f,
                (float)Random.Shared.NextDouble() * -.8f - 0.1f, // Puts height between -0.1 and -0.9
                (float)Random.Shared.NextDouble() * 2.0f - 1.0f
            ));
        float sunScale = 250f;
        float sunDistance = 1000f;
        Sun.Load(graphics, sunColor, sunDirection, sunScale, sunDistance);
        window.Render += dt => Sun.Draw
        (
            graphics,
            CameraMatrices.CreateProjectionMatrix(camFov, camAspectRatio, camNearPlane, camFarPlane),
            CameraMatrices.CreateViewMatrix(camPosition, camRotation.X, camRotation.Y, 0)
        );
        // Block Behaviors
        int behaviorCount = WaterFull + WaterLevels + 1;
        ChunkCluster<ChunkDimensions>.IBlockBehavior?[] blockBehaviors = new ChunkCluster<ChunkDimensions>.IBlockBehavior?[behaviorCount];
        for (int i = 0; i < behaviorCount; i++)
            blockBehaviors[i] = null;
        for (int i = 0; i < WaterLevels; i++)
            blockBehaviors[WaterFull + i] = new WaterBlockData<ChunkDimensions>();
        // Chunk Management
        ChunkCluster<ChunkDimensions> chunkCluster = new(WorldLengthInChunks, WorldHeightInChunks, blockBehaviors);
        ChunkProcessor<ChunkDimensions> processor = new(chunkCluster, shader, sunDirection, 0.6f, 0.05f);
        ChunkGenerationPipeline<Vector3D<int>> generationPipeline = new(processor, backgroundThreadBatch);
        IChunkClusterDirector clusterRegistry = lockGenerationHeight
            ? new ChunkClusterDirectorFlat(generationPipeline, ChunkDimensions.Length, renderDistance, camStartPos.Floor(), 32)
            : new ChunkClusterDirector(generationPipeline, ChunkDimensions.Length, renderDistance, renderHeight, camStartPos.Floor(), 32);

        WaterBlockData<ChunkDimensions>.cluster = chunkCluster;
        WaterBlockData<ChunkDimensions>.processor = processor;

        ChunkDirectorHandler<ChunkDimensions> registryHandler = new(chunkCluster, processor);
        Action<Vector3D<int>> chunkUpdate = processor.RedrawInstant;
        // Render Loop
        bool LMBHeld = false;
        window.Render += dt =>
        {
            frameStart = DateTime.Now;

            bool ctrl = input.Keyboards[0].IsKeyPressed(Key.ControlLeft);
            bool LMB = input.Mice[0].IsButtonPressed(MouseButton.Left);
            if (LMB && (!LMBHeld || ctrl))
            {
                Interactions.AttemptBreak
                (
                    chunkCluster,
                    camPosition,
                    Vector3.Transform(-Vector3.UnitZ, Quaternion.CreateFromYawPitchRoll(camRotation.Y, camRotation.X, 0)),
                    ctrl ? float.MaxValue : InteractionRange,
                    chunkUpdate
                );
            }
            LMBHeld = LMB;

            clusterRegistry.SetCentrePosition(camPosition.Floor());
            if (OverTargtetFrameTime())
                return;

            Vector3D<int>[] chunkProcesses = [.. processor.NeedsProcessingByChunk.Keys];
            foreach (Vector3D<int> chunk in chunkProcesses)
                if (processor.NeedsProcessingByChunk.TryRemove(chunk, out ConcurrentQueue<Action>? processes))
                    backgroundThreadBatch.EnqueueJob(() =>
                    {
                        while (processes.TryDequeue(out Action process))
                            process();
                        processor.Redraw(chunk);
                    });

            while (processor.NeedRendering.TryDequeue(out var result))
            {
                if (shader.chunkByPos.ContainsKey(result.Position))
                    shader.DeactivateChunk(result.Position);
                if (chunkCluster.IsActive(result.Position.Floor()) && result.Shapes.Length > 0)
                    shader.RenderChunk(result.Position, result.Shapes);
                if (OverTargtetFrameTime())
                    return;
            }

            clusterRegistry.ProcessChunks(registryHandler);
        };
        // Debug Info
        ImGuiController guiController = new(graphics, window, input);
        DebugRenderer.Load();
        window.Render += delta =>
        {
            guiController.Update((float)delta);
            DebugRenderer.OnRender(delta, generationPipeline.ChunksInPipeline.Count());
            guiController.Render();
        };
        // On Quite Cleanup
        window.Closing += backgroundThreadBatch.Dispose;
    }

    public static bool OverTargtetFrameTime() => DateTime.Now - frameStart > targetFrameTime;

    /// <summary>Calculates the camera rotation every frame.</summary>
    /// <returns>Distance to rotate the camera.</returns>
    static Vector2 GetCameraRotationDelta(IMouse mouse, Vector2 pos, float sensitivity)
    {
        if (mouse.Cursor.CursorMode != CursorMode.Raw)
            return Vector2.Zero;

        Vector2 delta = pos - previousMousePosition;
        previousMousePosition = pos;

        float Yaw = delta.X * sensitivity;
        float Pitch = delta.Y * sensitivity;

        // clamp pitch to avoid flipping
        float limit = MathF.PI / 2f - 0.01f;
        Pitch = Math.Clamp(Pitch, -limit, limit);
        return new(-Pitch, -Yaw);
    }

    /// <summary>Calculates the distance the camera needs to move every frame.</summary>
    /// <returns>Distance to move the camera.</returns>
    static Vector3 GetCameraPositionDelta(double deltaTime, IInputContext input, float camYaw)
    {
        IKeyboard keyboard = input.Keyboards[0];
        Vector3 dir = new(-MathF.Sin(camYaw), 0, -(float)Math.Cos(camYaw));
        Vector3 toMove = Vector3.Zero;
        if (keyboard.IsKeyPressed(Key.A))
            toMove = new Vector3(-dir.Z, 0, dir.X) * -1;
        else if (keyboard.IsKeyPressed(Key.D))
            toMove = new Vector3(-dir.Z, 0, dir.X) * 1;

        if (keyboard.IsKeyPressed(Key.S))
            toMove += dir * -1;
        else if (keyboard.IsKeyPressed(Key.W))
            toMove += dir * 1;

        if (keyboard.IsKeyPressed(Key.Space))
            toMove.Y = 1;
        else if (keyboard.IsKeyPressed(Key.ShiftLeft))
            toMove.Y = -1;

        float speedMult = input.Mice[0].ScrollWheels[0].Y;
        speedMult = (speedMult > 0) ? 1.25f : (speedMult < 0) ? 0.75f : 0;
        if (speedMult != 0)
            moveSpeed *= speedMult;

        return toMove * (float)deltaTime * moveSpeed;
    }
}