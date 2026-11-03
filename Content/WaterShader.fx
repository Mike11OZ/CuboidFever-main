#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

// Plain Minecraft-style water: flat geometry, one constant translucency, lit like a solid block.
// The wave / foam / fresnel version is saved as WaterShader.waves.fx.txt (and in git history).

matrix WorldViewProjection;
float  SkyBrightness;   // 1 = full daylight, DayNightCycle.MinSkyBrightness at night

static const float WaterAlpha = 0.65;

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

// Matches VertexPositionColorTextureWater. 'Water' (wave weight, face index) is unused while waves are off.
struct VertexShaderInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 Uv       : TEXCOORD0;
    float2 Water    : TEXCOORD1;
    float4 Sky      : COLOR1;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 Uv       : TEXCOORD0;
};

VertexShaderOutput WaterVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(float4(input.Position, 1.0), WorldViewProjection);
    output.Uv = input.Uv;

    // Block light vs. day-scaled sky light, same rule as SolidShader.fx
    output.Color = float4(max(input.Color.rgb, input.Sky.rgb * SkyBrightness), 1.0);
    return output;
}

float4 WaterPS(VertexShaderOutput input) : COLOR0
{
    float4 tex = tex2D(SpriteTextureSampler, input.Uv);

    // One brightness value: the water texture already carries the colour, so the block tint is ignored.
    float lit = max(input.Color.r, max(input.Color.g, input.Color.b));

    // Premultiplied alpha, to match BlendState.AlphaBlend
    return float4(tex.rgb * lit * WaterAlpha, WaterAlpha);
}

technique WaterTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL WaterVS();
        PixelShader = compile PS_SHADERMODEL WaterPS();
    }
}
