#version 450

layout(binding = 1, std430) readonly buffer type_ByteAddressBuffer
{
    uint _m0[];
} tints;

uniform sampler2DArray SPIRV_Cross_CombinedtextureArraytextureSampler;

layout(location = 0) in vec2 in_var_TEXCOORD0;
layout(location = 1) flat in vec2 in_var_TEXCOORD3;
layout(location = 2) flat in int in_var_TEXCOORD1;
layout(location = 3) flat in int in_var_TEXCOORD2;
layout(location = 0) out vec4 out_var_SV_TARGET;

void main()
{
    vec4 _55 = texture(SPIRV_Cross_CombinedtextureArraytextureSampler, vec3(in_var_TEXCOORD0, float(in_var_TEXCOORD1)));
    float _56 = _55.w;
    if (_56 < 0.00999999977648258209228515625)
    {
        discard;
    }
    ivec2 _65 = ivec2(clamp(vec2(ivec2(floor(in_var_TEXCOORD0))), vec2(0.0), in_var_TEXCOORD3 - vec2(1.0)));
    uint _78 = uint(int((float(in_var_TEXCOORD2) + (float(_65.y) * in_var_TEXCOORD3.x)) + float(_65.x)) * 12) >> 2u;
    out_var_SV_TARGET = vec4(_55.xyz * uintBitsToFloat(uvec3(tints._m0[_78], tints._m0[_78 + 1u], tints._m0[_78 + 2u])), _56);
}

