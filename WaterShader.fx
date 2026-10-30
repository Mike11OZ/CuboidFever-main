#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

matrix WorldViewProjection;
float Time;
float WaveIntensity;
float WaveSpeed;
float3 CameraPosition;

Texture2D SpriteTexture;
sampler TextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = Point;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
    float IsTopFace : TEXCOORD1;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinate : TEXCOORD0;
};

VertexShaderOutput WaterVertexShader(VertexShaderInput input)
{
    VertexShaderOutput output;
    float4 pos = input.Position;

    // 1. GPU Vertex Wave Displacement (using sine/cosine waves combined)
    if (input.IsTopFace > 0.5f && WaveIntensity > 0.0001f)
    {
        float wave = sin((pos.x * 0.4f) + (Time * WaveSpeed)) * cos((pos.z * 0.4f) + (Time * WaveSpeed));
        pos.y += wave * WaveIntensity;
    }

    output.Position = mul(pos, WorldViewProjection);
    output.TextureCoordinate = input.TextureCoordinate;

    // 2. GPU Fresnel-style Alpha Calculation
    float3 viewDir = CameraPosition - pos.xyz;
    float distSq = dot(viewDir, viewDir);
    float dist = sqrt(distSq);
    float along = input.IsTopFace > 0.5f ? viewDir.y : viewDir.x;
    float cosAngle = dist > 0.001f ? abs(along) / dist : 1.0f;

    float k = 1.0f - cosAngle;
    k *= k * k; // (1 - cos)^3
    float alpha = 0.45f + (0.95f - 0.45f) * k;

    // Apply color and premultiplied alpha
    float3 baseColor = input.Color.rgb;
    output.Color = float4(baseColor * alpha, alpha);

    return output;
}

float4 WaterPixelShader(VertexShaderOutput input) : COLOR0
{
    float4 texColor = tex2D(TextureSampler, input.TextureCoordinate);
    return texColor * input.Color;
}

technique WaterTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL WaterVertexShader();
        PixelShader = compile PS_SHADERMODEL WaterPixelShader();
    }
}