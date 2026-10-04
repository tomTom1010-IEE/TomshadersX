#ifndef TOM_ALPHA_DEPTH_INC
#define TOM_ALPHA_DEPTH_INC

#include "UnityCG.cginc"
sampler2D _MainTex;
sampler2D _AlphaMask;
float4 _MainTex_ST;
float4 _AlphaMask_ST;
float _Cutoff;
#include "TomAlphaCommon.cginc"
#if !defined(TOM_ALPHA_PREPASS)
float _DepthPrepass;
#endif

struct depthVaryings
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

depthVaryings vertDepth(appdata_base v)
{
    depthVaryings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(depthVaryings, o);
    UNITY_TRANSFER_INSTANCE_ID(v, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos = UnityObjectToClipPos(v.vertex);
    o.uv = v.texcoord.xy;
    return o;
}

float4 fragDepth(depthVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    // The legacy shader keeps its original prepass switch and material defaults.
#if defined(TOM_ALPHA_PREPASS)
    clip(_AlphaOptionZWrite - 0.5);
#else
    clip(_DepthPrepass - 0.5);
#endif
    float a = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).a;
    float mask = tex2D(_AlphaMask, i.uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw).r;
    TomAlphaClipDepthShadow(TomAlphaCoverage(a, mask));
    return 0;
}

#endif
