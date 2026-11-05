cbuffer PerFrameCB : register(b0)
{
    float4x4 WorldViewProj;
    float4x4 World;
    float3 CameraPos;
    float ColorMode;
    float Roughness;
    float Metallic;
    float2 Padding;
};

cbuffer PerNodeCB : register(b1)
{
    float4 LodColor;
};

Texture2D gDiffuseMap : register(t0);
Texture2D gNormalMap : register(t1);
SamplerState gSampler : register(s0);

struct VertexIn
{
    float3 Position : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD;
};

struct VertexOut
{
    float4 Position : SV_POSITION;
    float3 PosWorld : POSITION;
    float3 Normal : NORMAL;
    float2 TexCoord : TEXCOORD;
};

VertexOut VSMain(VertexIn vin)
{
    VertexOut vout;
    vout.PosWorld = vin.Position;
    vout.Normal = normalize(vin.Normal);
    vout.TexCoord = vin.TexCoord;
    vout.Position = mul(float4(vin.Position, 1.0f), WorldViewProj);
    return vout;
}

struct PSOutput
{
    float4 Position : SV_TARGET0;
    float4 Normal : SV_TARGET1;
    float4 Albedo : SV_TARGET2;
};

PSOutput PSMain(VertexOut pin)
{
    float3 N = normalize(pin.Normal);
    float3 albedo = float3(0.5f, 0.5f, 0.5f);

    if (ColorMode > 0.5f && ColorMode < 1.5f)
    {
        albedo = LodColor.rgb;
    }
    else if (ColorMode > 1.5f)
    {
        float slope = 1.0f - saturate(N.y);
        float h = saturate(pin.PosWorld.y / 70.0f);
        
        float3 grass = float3(0.28f, 0.52f, 0.25f);
        float3 rock = float3(0.45f, 0.44f, 0.42f);
        float3 snow = float3(0.95f, 0.96f, 0.98f);
        
        albedo = lerp(grass, rock, saturate(h * 1.5f));
        albedo = lerp(albedo, snow, saturate((h - 0.65f) * 3.0f));
        albedo = lerp(albedo, rock, saturate((slope - 0.25f) * 4.0f));
    }
    else
    {
        albedo = gDiffuseMap.Sample(gSampler, pin.TexCoord).rgb;
    }

    PSOutput output;
    output.Position = float4(pin.PosWorld, 1.0f);
    output.Normal = float4(N, Metallic);
    output.Albedo = float4(albedo, Roughness);
    return output;
}
