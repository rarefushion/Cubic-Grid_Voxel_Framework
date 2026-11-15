#version 450
#ifdef GL_ARB_shader_draw_parameters
#extension GL_ARB_shader_draw_parameters : enable
#endif

struct QuadInstance
{
    uint posXYZAndNormal;
    uint textureAndScale;
    uint tintIndex;
};

const int _84[6] = int[](0, 1, 2, 2, 1, 3);
const int _85[24] = int[](0, 3, 1, 2, 5, 6, 4, 7, 3, 7, 2, 6, 1, 5, 0, 4, 4, 7, 0, 3, 1, 2, 5, 6);
const vec3 _86[8] = vec3[](vec3(0.0), vec3(1.0, 0.0, 0.0), vec3(1.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(1.0, 0.0, 1.0), vec3(1.0), vec3(0.0, 1.0, 1.0));
const vec2 _87[4] = vec2[](vec2(0.0), vec2(0.0, 1.0), vec2(1.0, 0.0), vec2(1.0));
const vec3 _88[6] = vec3[](vec3(1.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 0.0, 1.0));
const vec3 _89[6] = vec3[](vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0));
const vec3 _90[6] = vec3[](vec3(0.0, 0.0, 1.0), vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), vec3(0.0, 1.0, 0.0), vec3(1.0, 0.0, 0.0), vec3(1.0, 0.0, 0.0));

struct type_Uniforms
{
    mat4 view;
    mat4 projection;
    vec3 chunkPos;
};

uniform type_Uniforms Uniforms;

layout(binding = 0, std430) readonly buffer type_StructuredBuffer_QuadInstance
{
    QuadInstance _m0[];
} QuadInstances;

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
    vec2 _114 = vec2(float((QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndScale >> 16u) & 255u), float((QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndScale >> 24u) & 255u)) + vec2(1.0);
    uint _120 = QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posXYZAndNormal >> 24u;
    gl_Position = spvWorkaroundRowMajor(Uniforms.projection) * (spvWorkaroundRowMajor(Uniforms.view) * vec4((Uniforms.chunkPos + vec3(uvec4(QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posXYZAndNormal & 255u, (QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posXYZAndNormal >> 8u) & 255u, (QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].posXYZAndNormal >> 16u) & 255u, _120).xyz)) + (_86[_85[(4u * _120) + uint(_84[uint(gl_VertexID)])]] * (((_88[_120] * _114.x) + (_89[_120] * _114.y)) + _90[_120])), 1.0));
    out_var_TEXCOORD0 = _87[_84[uint(gl_VertexID)]] * _114;
    out_var_TEXCOORD3 = _114;
    out_var_TEXCOORD1 = int(QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].textureAndScale & 65535u);
    out_var_TEXCOORD2 = int(QuadInstances._m0[uint((gl_InstanceID + SPIRV_Cross_BaseInstance))].tintIndex);
}

