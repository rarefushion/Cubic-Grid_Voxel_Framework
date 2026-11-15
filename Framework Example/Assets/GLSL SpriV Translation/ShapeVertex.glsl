#version 450
#ifdef GL_ARB_shader_draw_parameters
#extension GL_ARB_shader_draw_parameters : enable
#endif

struct ShapeVertex
{
    vec3 position;
    float _pad0;
    vec2 uv;
    float _pad2;
    float _pad1;
};

struct ShapeInstance
{
    float posX;
    float posY;
    float posZ;
    uint textureAndShapeAndRotation;
};

const mat3 _89[24] = mat3[](mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, -1.0), vec3(0.0, 1.0, 0.0)), mat3(vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, -1.0), vec3(-1.0, 0.0, 0.0)), mat3(vec3(-1.0, 0.0, 0.0), vec3(0.0, 0.0, -1.0), vec3(0.0, -1.0, 0.0)), mat3(vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, -1.0), vec3(1.0, 0.0, 0.0)), mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, -1.0, 0.0)), mat3(vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(-1.0, 0.0, 0.0)), mat3(vec3(-1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0)), mat3(vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(1.0, 0.0, 0.0)), mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0)), mat3(vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), vec3(-1.0, 0.0, 0.0)), mat3(vec3(-1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, -1.0)), mat3(vec3(0.0, 0.0, -1.0), vec3(0.0, 1.0, 0.0), vec3(1.0, 0.0, 0.0)), mat3(vec3(-1.0, 0.0, 0.0), vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, 1.0)), mat3(vec3(0.0, 0.0, -1.0), vec3(0.0, -1.0, 0.0), vec3(-1.0, 0.0, 0.0)), mat3(vec3(1.0, 0.0, 0.0), vec3(0.0, -1.0, 0.0), vec3(0.0, 0.0, -1.0)), mat3(vec3(0.0, 0.0, 1.0), vec3(0.0, -1.0, 0.0), vec3(1.0, 0.0, 0.0)), mat3(vec3(0.0, 0.0, 1.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, -1.0, 0.0)), mat3(vec3(0.0, 1.0, 0.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0)), mat3(vec3(0.0, 0.0, -1.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0)), mat3(vec3(0.0, -1.0, 0.0), vec3(-1.0, 0.0, 0.0), vec3(0.0, 0.0, -1.0)), mat3(vec3(0.0, 0.0, 1.0), vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0)), mat3(vec3(0.0, 1.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, -1.0)), mat3(vec3(0.0, 0.0, -1.0), vec3(1.0, 0.0, 0.0), vec3(0.0, -1.0, 0.0)), mat3(vec3(0.0, -1.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0)));

struct type_Uniforms
{
    mat4 view;
    mat4 projection;
    vec3 chunkPos;
    int verticesPerShape;
};

uniform type_Uniforms Uniforms;

layout(binding = 3, std430) readonly buffer type_StructuredBuffer_ShapeVertex
{
    ShapeVertex _m0[];
} ShapeVertices;

layout(binding = 0, std430) readonly buffer type_StructuredBuffer_ShapeInstance
{
    ShapeInstance _m0[];
} ShapeInstances;

#ifdef GL_ARB_shader_draw_parameters
#define SPIRV_Cross_BaseInstance gl_BaseInstanceARB
#else
uniform int SPIRV_Cross_BaseInstance;
#endif
layout(location = 0) out vec2 out_var_TEXCOORD0;
layout(location = 1) flat out vec2 out_var_TEXCOORD3;
layout(location = 2) flat out int out_var_TEXCOORD1;
layout(location = 3) flat out int out_var_TEXCOORD2;

mat4 spvWorkaroundRowMajor(mat4 wrap) { return wrap; }

void main()
{
    uint _116 = uint(gl_VertexID) + (((ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndShapeAndRotation >> 16u) & 255u) * uint(Uniforms.verticesPerShape));
    gl_Position = spvWorkaroundRowMajor(Uniforms.projection) * (spvWorkaroundRowMajor(Uniforms.view) * vec4((((_89[(ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndShapeAndRotation >> 24u) & 255u] * (ShapeVertices._m0[_116].position - vec3(0.5))) + vec3(0.5)) + Uniforms.chunkPos) + vec3(ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posX, ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posY, ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posZ), 1.0));
    out_var_TEXCOORD0 = ShapeVertices._m0[_116].uv;
    out_var_TEXCOORD3 = vec2(1.0);
    out_var_TEXCOORD1 = int(ShapeInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndShapeAndRotation & 65535u);
    out_var_TEXCOORD2 = int(uint((gl_InstanceID + SPIRV_Cross_BaseInstance)));
}

