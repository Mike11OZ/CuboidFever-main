#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

// Replaces AlphaTestEffect for solid blocks. Block light and sky light arrive in separate vertex
// colors so the sky can be dimmed by the time of day without remeshing anything.

matrix WorldViewProjection;
float  SkyBrightness;   // 1 = full daylight, DayNightCycle.MinSkyBrightness at night

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexShaderInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;      // block light * shade * tint * AO, plus alpha
    float2 Uv       : TEXCOORD0;
    float4 Sky      : COLOR1;      // sky light * shade * tint * AO
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 Uv       : TEXCOORD0;
    float3 Sky      : TEXCOORD1;
};

VertexShaderOutput SolidVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.Color = input.Color;
    output.Uv = input.Uv;
    output.Sky = input.Sky.rgb;
    return output;
}

float4 SolidPS(VertexShaderOutput input) : COLOR0
{
    float4 tex = tex2D(SpriteTextureSampler, input.Uv);

    // Same alpha cutoff AlphaTestEffect was using (ReferenceAlpha 128, GreaterEqual)
    clip(tex.a * input.Color.a - 0.5);

    float3 lit = max(input.Color.rgb, input.Sky * SkyBrightness);
    return float4(tex.rgb * lit, 1.0);
}

technique SolidTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SolidVS();
        PixelShader = compile PS_SHADERMODEL SolidPS();
    }
}