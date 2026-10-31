#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_3
    #define PS_SHADERMODEL ps_4_0_level_9_3
#endif

#define WAVE_COUNT 4

matrix WorldViewProjection;
float3 CameraPosition;
float  WaveTime;
float  WaveIntensity;

float4 W0A, W0B;
float4 W1A, W1B;
float4 W2A, W2B;
float4 W3A, W3B;

float FoamSlope;
float FoamSoftness;
float FoamCrestMin;
float FoamCrestSoft;
float FoamBreakup;
float FoamNoiseScale;
float FoamWhiteness;
float ShoreFoamWidth;     // blocks from the shore where foam ends completely
float ShoreFoamSoftness;  // blocks of fade at the outer edge (0 = hard edge)
float ShoreFoamRagged;    // 0..1, how much noise varies the width

static const float AlphaMin = 0.60;
static const float AlphaMax = 0.90;

Texture2D SpriteTexture;
sampler2D SpriteTextureSampler = sampler_state
{
    Texture = <SpriteTexture>;
};

struct VertexShaderInput
{
    float3 Position : POSITION0;
    float4 Color    : COLOR0;
    float2 Uv       : TEXCOORD0;
    float  Water    : TEXCOORD1;   // float weight matching float[] WaterW
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color    : COLOR0;
    float2 Uv       : TEXCOORD0;
    float4 Data     : TEXCOORD1;   // xy = world xz, z = weight, w = covered flag
    float  Shore    : TEXCOORD2;   // 0 = no shore foam, else 1 + 8-bit mask of solid neighbors (top faces only)
};

float3 WaveSample(float2 xz)
{
    float3 r = float3(0, 0, 0);

    float p0 = dot(W0A.xy, xz) + W0A.z * WaveTime + W0A.w;
    float a0 = W0B.x;
    r.x += a0 * sin(p0);
    float c0 = a0 * cos(p0);
    r.y += c0 * W0A.x;
    r.z += c0 * W0A.y;

    float p1 = dot(W1A.xy, xz) + W1A.z * WaveTime + W1A.w;
    float a1 = W1B.x;
    r.x += a1 * sin(p1);
    float c1 = a1 * cos(p1);
    r.y += c1 * W1A.x;
    r.z += c1 * W1A.y;

    float p2 = dot(W2A.xy, xz) + W2A.z * WaveTime + W2A.w;
    float a2 = W2B.x;
    r.x += a2 * sin(p2);
    float c2 = a2 * cos(p2);
    r.y += c2 * W2A.x;
    r.z += c2 * W2A.y;

    float p3 = dot(W3A.xy, xz) + W3A.z * WaveTime + W3A.w;
    float a3 = W3B.x;
    r.x += a3 * sin(p3);
    float c3 = a3 * cos(p3);
    r.y += c3 * W3A.x;
    r.z += c3 * W3A.y;

    return r * WaveIntensity;
}

VertexShaderOutput WaterVS(VertexShaderInput input)
{
    VertexShaderOutput output;

    // The mesher packs two things into this one float: wave weight (0, 1 or 2) in the low part and,
    // for top faces, (1 + shoreline neighbor mask) * 4 above it.
    float packedWater = input.Water;
    float shoreCode = floor(packedWater * 0.25);
    float wType = packedWater - 4.0 * shoreCode;

    float4 pos = float4(input.Position, 1.0);
    float rise = WaveSample(pos.xz).x;
    pos.y += 2.0 / 16.0;

    if (wType < 0.5)
    {
        rise = min(0.0, rise);
    }
    else if (wType > 1.5)
    {
        rise = min(0.0, rise);
    }

    pos.y += rise;

    output.Position = mul(pos, WorldViewProjection);
    output.Uv = input.Uv;

    float3 viewDir = CameraPosition - pos.xyz;
    float dist = max(length(viewDir), 0.01);
    float cosAngle = saturate(abs(viewDir.y) / dist);

    float k = 1.0 - cosAngle;
    k = k * k * k;
    float alpha = clamp(AlphaMin + (AlphaMax - AlphaMin) * k, AlphaMin, AlphaMax);

    output.Color = float4(input.Color.rgb, alpha);
    output.Data = float4(input.Position.x, input.Position.z, wType, wType > 1.5 ? 1.0 : 0.0);
    output.Shore = shoreCode;
    return output;
}

float2 WrapCell(float2 p) { return p - 61.0 * floor(p / 61.0); }

float Hash21(float2 p)
{
    return frac(sin(dot(WrapCell(p), float2(127.1, 311.7))) * 43758.5453);
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);

    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));

    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Bit of an 8-bit mask: div is 1, 2, 4, ... 128. Adding 0.5 before dividing keeps the result away from
// exact integer boundaries, so tiny GPU division/interpolation errors can never flip a bit.
float ShoreBit(float mask, float div)
{
    float k = floor((mask + 0.5) / div);
    return step(0.5, frac(k * 0.5));   // 1 when k is odd
}

// Shoreline foam for one water cell, from the distance to the nearest solid block.
// 'mask' bits mark which of the 8 neighboring cells are solid:
//   0:+x  1:-x  2:+z  3:-z  4:+x+z  5:+x-z  6:-x+z  7:-x-z
// 'local' is the position inside the cell (0..1). Distance is measured with max(dx, dz) (the
// "square" metric), so the foam is square around corners instead of round. Foam is full up to
// (width - softness) from the shore and fades to nothing at 'width'.
float ShoreFoam(float mask, float2 local, float width, float softness)
{
    float lx = local.x;
    float lz = local.y;

    float d = 10.0;
    d = min(d, lerp(10.0, 1.0 - lx, ShoreBit(mask,   1.0)));                    // solid on +x
    d = min(d, lerp(10.0, lx,       ShoreBit(mask,   2.0)));                    // solid on -x
    d = min(d, lerp(10.0, 1.0 - lz, ShoreBit(mask,   4.0)));                    // solid on +z
    d = min(d, lerp(10.0, lz,       ShoreBit(mask,   8.0)));                    // solid on -z
    d = min(d, lerp(10.0, max(1.0 - lx, 1.0 - lz), ShoreBit(mask,  16.0)));     // solid on +x+z corner
    d = min(d, lerp(10.0, max(1.0 - lx, lz),       ShoreBit(mask,  32.0)));     // +x-z
    d = min(d, lerp(10.0, max(lx, 1.0 - lz),       ShoreBit(mask,  64.0)));     // -x+z
    d = min(d, lerp(10.0, max(lx, lz),             ShoreBit(mask, 128.0)));     // -x-z

    float t = saturate((width - d) / max(softness, 0.0001));
    return t * t * (3.0 - 2.0 * t);   // smoothstep
}

float4 WaterPS(VertexShaderOutput input) : COLOR0
{
    float4 tex = tex2D(SpriteTextureSampler, input.Uv);

    float3 baseColor = input.Color.rgb;
    float waterAlpha = input.Color.a;

    float weight = input.Data.z;
    float isCovered = input.Data.w;
    float foam = 0.0;

    // 1. Open water whitecaps
    float allowed = (weight > 0.5 && weight < 1.5) ? 1.0 : 0.0;
    if (allowed > 0.001 && WaveIntensity > 0.0001)
    {
        float3 w = WaveSample(input.Data.xy);
        float steepness = length(w.yz);

        float n = ValueNoise(input.Data.xy * FoamNoiseScale + float2(WaveTime * 0.35, WaveTime * 0.22));
        steepness *= lerp(1.0 - FoamBreakup, 1.0 + FoamBreakup, n);

        float steep = smoothstep(FoamSlope, FoamSlope + max(FoamSoftness, 0.0001), steepness);
        float crest = smoothstep(FoamCrestMin, FoamCrestMin + max(FoamCrestSoft, 0.0001), w.x);

        foam = steep * crest * allowed;
    }

    // 2. Shoreline foam: squared bands along solid edges, squares in solid corners
    float shoreFoam = 0.0;
    if (isCovered < 0.5 && input.Shore > 0.5)
    {
        float shoreNoise = ValueNoise(input.Data.xy * (FoamNoiseScale * 1.5) + float2(WaveTime * 0.2, WaveTime * 0.15));
        float foamWidth = ShoreFoamWidth * lerp(1.0 - ShoreFoamRagged, 1.0 + ShoreFoamRagged, shoreNoise);

        float mask = floor(input.Shore + 0.5) - 1.0;   // round: the value is interpolated across the quad
        shoreFoam = ShoreFoam(mask, frac(input.Data.xy), foamWidth, ShoreFoamSoftness);
    }

    foam = max(foam, shoreFoam);

    float lit = max(baseColor.r, max(baseColor.g, baseColor.b));
    float3 waterRgb = tex.rgb * baseColor * waterAlpha;
    float3 foamTex = lerp(tex.rgb, float3(1, 1, 1), FoamWhiteness);
    float3 foamRgb = foamTex * float3(lit, lit, lit);

    float3 finalRgb = lerp(waterRgb, foamRgb, foam);
    float finalAlpha = lerp(waterAlpha, 1.0, foam);

    return float4(finalRgb, finalAlpha);
}

technique WaterTechnique
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL WaterVS();
        PixelShader = compile PS_SHADERMODEL WaterPS();
    }
}