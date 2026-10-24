#ifndef TOM_TESSELLATION_INC
#define TOM_TESSELLATION_INC

#include "UnityCG.cginc"

float _TessellationMode, _TessellationFactor, _TessellationEdgeLength, _TessellationSmooth;
float _UseGeometryMask;
Texture2D _GeometryMask, _DisplacementMap;
SamplerState sampler_GeometryMask, sampler_DisplacementMap;
float4 _GeometryMask_ST, _DisplacementMap_ST;
float _DisplacementStrength, _DisplacementMidlevel, _DisplacementMip, _DisplacementNormalStrength;

struct TomTessInput
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float4 uv3 : TEXCOORD3;
    float4 color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TomTessControlPoint
{
    float4 vertex : INTERNALTESSPOS;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float4 uv3 : TEXCOORD3;
    float4 color : COLOR;
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED) || defined(UNITY_STEREO_INSTANCING_ENABLED)
    uint instanceID : TEXCOORD4;
#endif
};

struct TomTessFactors
{
    float edge[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

TomTessControlPoint TomTessVertex(TomTessInput v)
{
    TomTessControlPoint o = (TomTessControlPoint)0;
    o.vertex = v.vertex; o.normal = v.normal; o.tangent = v.tangent;
    o.uv0 = v.uv0; o.uv1 = v.uv1; o.uv2 = v.uv2; o.uv3 = v.uv3; o.color = v.color;
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    return o;
}

float TomTessEdge(float3 a, float3 b)
{
    float cap = clamp(_TessellationFactor, 1.0, 16.0);
    if (_TessellationMode < 0.5) return cap;
    float3 aw = mul(unity_ObjectToWorld, float4(a, 1)).xyz;
    float3 bw = mul(unity_ObjectToWorld, float4(b, 1)).xyz;
    float4 ac = mul(unity_CameraProjection, mul(unity_WorldToCamera, float4(aw, 1)));
    float4 bc = mul(unity_CameraProjection, mul(unity_WorldToCamera, float4(bw, 1)));
    if (min(ac.w, bc.w) <= 0.01) return cap;
    float pixels = length((ac.xy / ac.w - bc.xy / bc.w) * _ScreenParams.xy * 0.5);
    // An edge depends on its endpoints only: adjacent patches agree, including reversed edges.
    return clamp(pixels / clamp(_TessellationEdgeLength, 4.0, 64.0), 1.0, cap);
}

TomTessFactors TomTessPatch(InputPatch<TomTessControlPoint, 3> p)
{
    UNITY_SETUP_INSTANCE_ID(p[0]);
    TomTessFactors o;
    o.edge[0] = TomTessEdge(p[1].vertex.xyz, p[2].vertex.xyz);
    o.edge[1] = TomTessEdge(p[2].vertex.xyz, p[0].vertex.xyz);
    o.edge[2] = TomTessEdge(p[0].vertex.xyz, p[1].vertex.xyz);
    o.inside = max(o.edge[0], max(o.edge[1], o.edge[2]));
    return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("TomTessPatch")]
[maxtessfactor(16.0)]
TomTessControlPoint TomTessHull(InputPatch<TomTessControlPoint, 3> p, uint id : SV_OutputControlPointID)
{
    return p[id];
}

float3 TomTessSafeNormal(float3 n)
{
    return dot(n, n) > 1e-12 ? normalize(n) : float3(0, 1, 0);
}

float2 TomTessMask(float2 uv)
{
    if (_UseGeometryMask < 0.5) return 1.0;
    return saturate(_GeometryMask.SampleLevel(sampler_GeometryMask,
        uv * _GeometryMask_ST.xy + _GeometryMask_ST.zw, clamp(_DisplacementMip, 0.0, 8.0)).rg);
}

float TomTessHeight(float2 uv, float weight)
{
    float h = saturate(_DisplacementMap.SampleLevel(sampler_DisplacementMap,
        uv * _DisplacementMap_ST.xy + _DisplacementMap_ST.zw, clamp(_DisplacementMip, 0.0, 8.0)).r);
    return (h - saturate(_DisplacementMidlevel)) * clamp(_DisplacementStrength, -0.05, 0.05) * weight;
}

float3 TomTessDryPosition(const OutputPatch<TomTessControlPoint, 3> p, float3 b, float weight)
{
    float3 pos = p[0].vertex.xyz * b.x + p[1].vertex.xyz * b.y + p[2].vertex.xyz * b.z;
    float smooth = saturate(_TessellationSmooth) * weight;
    if (smooth <= 0.0) return pos;
    float3 n0 = TomTessSafeNormal(p[0].normal), n1 = TomTessSafeNormal(p[1].normal), n2 = TomTessSafeNormal(p[2].normal);
    float3 q0 = pos - n0 * dot(pos - p[0].vertex.xyz, n0);
    float3 q1 = pos - n1 * dot(pos - p[1].vertex.xyz, n1);
    float3 q2 = pos - n2 * dot(pos - p[2].vertex.xyz, n2);
    return lerp(pos, q0 * b.x + q1 * b.y + q2 * b.z, smooth);
}

float2 TomTessUV(const OutputPatch<TomTessControlPoint, 3> p, float3 b)
{
    return p[0].uv0 * b.x + p[1].uv0 * b.y + p[2].uv0 * b.z;
}

void TomTessSurfaceSample(const OutputPatch<TomTessControlPoint, 3> p, float3 b, out float3 dryWS, out float h)
{
    float2 uv = TomTessUV(p, b), mask = TomTessMask(uv);
    dryWS = mul(unity_ObjectToWorld, float4(TomTessDryPosition(p, b, mask.r), 1)).xyz;
    h = TomTessHeight(uv, mask.g);
}

TomTessInput TomTessDomainVertex(const OutputPatch<TomTessControlPoint, 3> p, float3 b)
{
    UNITY_SETUP_INSTANCE_ID(p[0]);
    TomTessInput o = (TomTessInput)0;
    UNITY_TRANSFER_INSTANCE_ID(p[0], o);
    o.vertex = p[0].vertex * b.x + p[1].vertex * b.y + p[2].vertex * b.z;
    o.normal = p[0].normal * b.x + p[1].normal * b.y + p[2].normal * b.z;
    o.tangent = float4(p[0].tangent.xyz * b.x + p[1].tangent.xyz * b.y + p[2].tangent.xyz * b.z,
        p[0].tangent.w < 0 ? -1.0 : 1.0);
    o.uv0 = TomTessUV(p, b);
    o.uv1 = p[0].uv1 * b.x + p[1].uv1 * b.y + p[2].uv1 * b.z;
    o.uv2 = p[0].uv2 * b.x + p[1].uv2 * b.y + p[2].uv2 * b.z;
    o.uv3 = p[0].uv3 * b.x + p[1].uv3 * b.y + p[2].uv3 * b.z;
    o.color = p[0].color * b.x + p[1].color * b.y + p[2].color * b.z;
    if (_TessellationSmooth > 0.0 || _DisplacementStrength != 0.0)
    {
        float2 mask = TomTessMask(o.uv0);
        o.vertex.xyz = TomTessDryPosition(p, b, mask.r);
        if (_DisplacementStrength != 0.0)
        {
            float3 dryWS = mul(unity_ObjectToWorld, o.vertex).xyz;
            float3 normalWS = UnityObjectToWorldNormal(TomTessSafeNormal(o.normal));
            float h = TomTessHeight(o.uv0, mask.g);
            o.vertex = mul(unity_WorldToObject, float4(dryWS + normalWS * h, 1));
            if (_DisplacementNormalStrength > 0.0)
            {
                // Barycentric differences include height ST, mask gradients and world-space scale.
                const float epsilon = 0.001;
                float3 du = float3(-epsilon, epsilon, 0), dv = float3(-epsilon, 0, epsilon);
                float3 pu0 = 0, pu1 = 0, pv0 = 0, pv1 = 0;
                float hu0 = 0, hu1 = 0, hv0 = 0, hv1 = 0;
                TomTessSurfaceSample(p, b - du, pu0, hu0); TomTessSurfaceSample(p, b + du, pu1, hu1);
                TomTessSurfaceSample(p, b - dv, pv0, hv0); TomTessSurfaceSample(p, b + dv, pv1, hv1);
                float3 u = pu1 - pu0, v = pv1 - pv0;
                float uu = dot(u, u), vv = dot(v, v), uv = dot(u, v), determinant = uu * vv - uv * uv;
                if (determinant > 1e-5 * max(uu * vv, 1e-30))
                {
                    float dhdu = hu1 - hu0, dhdv = hv1 - hv0;
                    float3 gradient = (u * (vv * dhdu - uv * dhdv) + v * (uu * dhdv - uv * dhdu)) / determinant;
                    float3 corrected = TomTessSafeNormal(normalWS - gradient * saturate(_DisplacementNormalStrength));
                    o.normal = TomTessSafeNormal(mul(corrected, (float3x3)unity_ObjectToWorld));
                    float3 tangentWS = UnityObjectToWorldDir(o.tangent.xyz);
                    tangentWS -= corrected * dot(corrected, tangentWS);
                    if (dot(tangentWS, tangentWS) > 1e-10)
                        o.tangent.xyz = normalize(mul((float3x3)unity_WorldToObject, tangentWS));
                }
            }
        }
    }
    return o;
}
#endif
