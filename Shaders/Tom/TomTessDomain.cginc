#ifndef TOM_TESS_DOMAIN_INC
#define TOM_TESS_DOMAIN_INC
#include "TomTessellation.cginc"

#if defined(TOM_TESS_DEPTH)
[domain("tri")]
depthVaryings TomTessDomain(TomTessFactors factors, const OutputPatch<TomTessControlPoint, 3> p, float3 bary : SV_DomainLocation)
{
    TomTessInput source = TomTessDomainVertex(p, bary);
    appdata_base v;
    UNITY_INITIALIZE_OUTPUT(appdata_base, v);
    v.vertex = source.vertex; v.normal = source.normal; v.texcoord = float4(source.uv0, 0, 0);
    UNITY_TRANSFER_INSTANCE_ID(source, v);
    return vertDepth(v);
}
#elif defined(TOM_TESS_OUTLINE) || defined(TOM_TESS_SHADOW)
[domain("tri")]
v2f TomTessDomain(TomTessFactors factors, const OutputPatch<TomTessControlPoint, 3> p, float3 bary : SV_DomainLocation)
{
    TomTessInput source = TomTessDomainVertex(p, bary);
#if defined(TOM_TESS_OUTLINE)
    appdata v;
    UNITY_INITIALIZE_OUTPUT(appdata, v);
    v.vertex = source.vertex; v.normal = source.normal; v.uv = source.uv0;
#if !defined(TOM_TESS_SKIN)
    v.smoothNormalOS = source.uv3.xyz;
#endif
    UNITY_TRANSFER_INSTANCE_ID(source, v);
    return vertOutline(v);
#else
    appdata_base v;
    UNITY_INITIALIZE_OUTPUT(appdata_base, v);
    v.vertex = source.vertex; v.normal = source.normal; v.texcoord = float4(source.uv0, 0, 0);
    UNITY_TRANSFER_INSTANCE_ID(source, v);
    return vertShadow(v);
#endif
}
#else
[domain("tri")]
TomVaryings TomTessDomain(TomTessFactors factors, const OutputPatch<TomTessControlPoint, 3> p, float3 bary : SV_DomainLocation)
{
    TomTessInput source = TomTessDomainVertex(p, bary);
    TomVertexData v;
    UNITY_INITIALIZE_OUTPUT(TomVertexData, v);
    v.vertex = source.vertex; v.normal = source.normal; v.tangent = source.tangent;
    v.uv0 = source.uv0; v.uv1 = source.uv1; v.uv2 = source.uv2;
#if defined(TOM_SKIN)
    v.uv3 = source.uv3.xy; v.color = source.color;
#endif
    UNITY_TRANSFER_INSTANCE_ID(source, v);
    return TomVert(v);
}
#endif
#endif
