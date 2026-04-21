using System.Numerics;
using GalensUnified.CubicGrid.Framework;
using GalensUnified.CubicGrid.Renderer.NET;
using Microsoft.DotNet.PlatformAbstractions;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

using static BlockIDs;

static class Program
{
    // Startup Values
    const int chunkLength = 16;
    const int renderDistance = 24;
    const int WorldLengthInChunks = renderDistance * 2 + 1;
    public const int seed = 1337;
    public const float worldScale = 0.01f;
    public const int mountainHeight = 50;
    public static Vector3 camStartPos = new(8, mountainHeight + 8, 8);
    public const int targetFrameRate = 60;
    public static readonly TimeSpan targetFrameTime = new(0, 0, 0, 0, 1000 / targetFrameRate);
    // Runtime
    public static bool cursorVisible = false;
    public static float moveSpeed = 2f;
    public static Vector2 previousMousePosition;
    public static DateTime frameStart = DateTime.Now;

    static void Main(string[] args)
    {
        WindowOptions options = WindowOptions.Default;
        options.Title = "Cubic-Grid Voxel Rendering Example";
        options.PreferredDepthBufferBits = 32;
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

        // Create Blocks
        // Faces are named by the Assets/Textures file name.
        Dictionary<ushort, BlockRenderData> renderDataByBlock = new()
        {
            {Air, new("Null", "Null", "Null", "Null", "Null", "Null")},
            {Grass, new("Grass Side", "Grass Side", "Grass", "Dirt", "Grass Side", "Grass Side")},
            {Dirt, new("Dirt", "Dirt", "Dirt", "Dirt", "Dirt", "Dirt")},
            {Stone, new("Stone", "Stone", "Stone", "Stone", "Stone", "Stone")}
        };

        // Create Graphics and Shader
        GL graphics = window.CreateOpenGL();
        graphics.Enable(EnableCap.DepthTest);
        graphics.DepthFunc(DepthFunction.Less);
        graphics.ClearColor(System.Drawing.Color.CornflowerBlue);
        window.Resize += size => graphics.Viewport(0, 0, (uint)size.X, (uint)size.Y);
        window.Update += delta => graphics.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        DirectoryInfo assets = Directory.CreateDirectory(Path.Combine(ApplicationEnvironment.ApplicationBasePath, "Assets"));
        // Ambiguous between mine and Silk.NET.OpenGL.Shader :sob:
        GalensUnified.CubicGrid.Renderer.NET.Shader shader = new
        (
            graphics,
            Path.Combine(assets.FullName, "GLSL"),
            chunkLength,
            WorldLengthInChunks,
            camNearPlane,
            renderDataByBlock,
            TextureLoader.LoadImages(Directory.CreateDirectory(Path.Combine(assets.FullName, "Textures")).GetFiles()),
            messageErr => Console.WriteLine(messageErr),
            messageLog => Console.WriteLine(messageLog)
        );
        window.Render += dt => shader.Render
        (
            CameraMatrices.CreateProjectionMatrix(camFov, camAspectRatio, camNearPlane, camFarPlane),
            CameraMatrices.CreateViewMatrix(camPosition, camRotation.X, camRotation.Y, 0),
            (Vector2)window.Size
        );
        // Chunk Management
        ChunkCluster chunkCluster = new(chunkLength, WorldLengthInChunks);
        ChunkProcessor processor = new(chunkCluster, shader);
        ChunkGenerationPipeline<Vector3D<int>> generationPipeline = new(processor);
        ChunkClusterDirector clusterRegistry = new(generationPipeline, chunkLength, renderDistance, BlockPosByVector3(camStartPos), 32);
        static bool OverTargtetFrameTime() => DateTime.Now - frameStart > targetFrameTime;
        window.Render += dt =>
        {
            frameStart = DateTime.Now;

            clusterRegistry.SetCentrePosition(BlockPosByVector3(camPosition));
            if (OverTargtetFrameTime())
                return;

            foreach (ChunkDirectorUpdate chunk in clusterRegistry.ProcessChunks())
            {
                if (!chunk.IsActive)
                    shader.DeactivateChunk(chunkCluster.IndexByChunkCoord(chunkCluster.ChunkCoordByGlobalPos(chunk.Position)));
                if (OverTargtetFrameTime())
                    return;
            }
        };
        // Debug Info
        ImGuiController guiController = new(graphics, window, input);
        DebugRenderer.Load();
        window.Render += delta =>
        {
            guiController.Update((float)delta);
            DebugRenderer.OnRender(delta);
            guiController.Render();
        };

    }

    static Vector3D<int> BlockPosByVector3(Vector3 pos) =>
        new((int)pos.X, (int)pos.Y, (int)pos.Z);

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