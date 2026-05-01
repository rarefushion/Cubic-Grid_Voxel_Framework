using System.Numerics;
using System.Runtime.InteropServices;
using GalensUnified.CubicGrid.Renderer.NET;
using Silk.NET.OpenGL;

public static class Sun
{
    // Creates it's own shader. This will be offloaded once there's a standared place for generic meshes.
    private static readonly string _vertexShaderSunSource = @"
        #version 330 core

        layout (location = 0) in vec3 aPos;
        layout (location = 1) in vec3 aColor;

        out vec3 vColor;

        uniform mat4 view;
        uniform mat4 projection;

        void main()
        {
            vColor = aColor;
            gl_Position = projection * view * vec4(aPos, 1.0);
        }
    ";

    private static readonly string _fragmentShaderSunSource = @"
        #version 330 core

        in vec3 vColor;
        out vec4 FragColor;

        void main()
        {
            FragColor = vec4(vColor, 1.0);
        }
    ";
    public static uint shaderProgram;
    public static int viewShaderLocation;
    public static int projectionShaderLocation;

    public record VertexArrayObject(uint Vao, uint IndexCount);
    public static List<VertexArrayObject> drawObjects = [];

    public static uint CreateShaders(GL GL, string vertexShaderCode, string fragmentShaderCode)
    {
        uint vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexShaderCode);
        GL.CompileShader(vertexShader);

        uint fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentShaderCode);
        GL.CompileShader(fragmentShader);

        uint shaderProgram = GL.CreateProgram();
        GL.AttachShader(shaderProgram, vertexShader);
        GL.AttachShader(shaderProgram, fragmentShader);
        GL.LinkProgram(shaderProgram);

        GL.DetachShader(shaderProgram, vertexShader);
        GL.DetachShader(shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        return shaderProgram;
    }

    public unsafe static void Load(GL GL, Vector3 color, Vector3 direction, float scale, float distance)
    {
        shaderProgram = CreateShaders(GL, _vertexShaderSunSource, _fragmentShaderSunSource);
        projectionShaderLocation = GL.GetUniformLocation(shaderProgram, "projection");
        viewShaderLocation = GL.GetUniformLocation(shaderProgram, "view");
        // Create Sun
        Vertex[] verts = new Vertex[6 * 4];
        uint[] indices = new uint[6 * 6];
        for (int face = 0; face < 6; face++)
        {
            for (int vert = 0; vert < 4; vert++)
                verts[vert + (face * 4)] =
                    new Vertex()
                    {
                        Position = (CubeMesh.vertices[CubeMesh.quads[4 * face + vert]] * scale) + (-direction * distance),
                        Color = color
                    };

            for (int indice = 0; indice < 6; indice++)
                indices[indice + (face * 6)] = (uint)(CubeMesh.quadsOffsetForTris[indice] + (face * 4));
        }
        // Upload Vertices
        GL.UseProgram(shaderProgram);
        uint _vao = GL.GenVertexArray();
        GL.BindVertexArray(_vao);
        uint _vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        fixed (Vertex* buf = verts)
        {
            nuint size = (nuint)(verts.Length * sizeof(Vertex));
            GL.BufferData(BufferTargetARB.ArrayBuffer, size, buf, BufferUsageARB.StaticDraw);
            GL.BufferSubData(BufferTargetARB.ArrayBuffer, 0, size, buf);
        }
        // Upload Indices
        uint ebo = GL.GenBuffer();
        GL.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        fixed (uint* buf = indices)
        {
            nuint size = (nuint)(indices.Length * sizeof(uint));
            GL.BufferData(BufferTargetARB.ElementArrayBuffer, size, null, BufferUsageARB.StaticDraw);
            GL.BufferSubData(BufferTargetARB.ElementArrayBuffer, 0, size, buf);
        }
        // Link vertex attributes
        uint stride = (uint)sizeof(Vertex);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, (void*)0); // Pos
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, (void*)sizeof(Vector3)); // Color
        GL.EnableVertexAttribArray(1);
        GL.BindVertexArray(0);
        // Add to draw group
        VertexArrayObject VAO = new(_vao, (uint)indices.Length);
        drawObjects.Add(VAO);
    }

    public unsafe static void Draw(GL GL, Matrix4x4 projectionMatrix, Matrix4x4 viewMatrix)
    {
        GL.UseProgram(shaderProgram);

        // Camera
        GL.UniformMatrix4(projectionShaderLocation, 1, false, (float*)&projectionMatrix);
        GL.UniformMatrix4(viewShaderLocation, 1, false, (float*)&viewMatrix);
        foreach (VertexArrayObject mesh in drawObjects)
        {
            GL.BindVertexArray(mesh.Vao);
            GL.DrawElements(PrimitiveType.Triangles, mesh.IndexCount, DrawElementsType.UnsignedInt, (void*)0);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Color;
    }

}