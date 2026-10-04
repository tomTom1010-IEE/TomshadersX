#ifndef TOM_CLEARCOAT_INC
#define TOM_CLEARCOAT_INC

// Extra maps share the existing main/normal sampler states, with independent ST.
DECLARE_TEX2D_NOSAMPLER(_ClearCoatMap);
DECLARE_TEX2D_NOSAMPLER(_ClearCoatNormalMap);
float4 _ClearCoatMap_ST, _ClearCoatNormalMap_ST;
float _ClearCoat, _ClearCoatRoughness, _ClearCoatIOR;
float _ClearCoatNormalSource, _ClearCoatNormalScale;
float _ClearCoatEnvironmentStrength, _ClearCoatEnergyBlend, _ClearCoatDebugView;

struct TomCoatData
{
    float weight;
    float roughness;
    float f0;
    float3 normalWS;
    float viewRetention;
    float indirectRetention;
};

float3 TomInterfaceView(float3 positionWS)
{
    float3 perspective = _WorldSpaceCameraPos.xyz - positionWS;
    float3 orthographic = UNITY_MATRIX_V[2].xyz;
    return normalize(lerp(perspective, orthographic, unity_OrthoParams.w));
}

float3 TomResolveCoatNormal(TomVaryings i, float3 geometric, float3 mainNormal, float3 combined, float orientation)
{
    if (_ClearCoatNormalSource < 0.5) return geometric;
    if (_ClearCoatNormalSource < 1.5) return mainNormal;
    if (_ClearCoatNormalSource < 2.5) return combined;
    float2 uv = i.uv0 * _ClearCoatNormalMap_ST.xy + _ClearCoatNormalMap_ST.zw;
    float3 ts = UnpackScaleNormal(SAMPLE_TEX2D_SAMPLER(_ClearCoatNormalMap, _NormalMap, uv), _ClearCoatNormalScale);
    return normalize(ts.x * normalize(i.tangentWS.xyz)
        + ts.y * normalize(i.bitangentWS) * orientation + ts.z * geometric);
}

float TomCoatFresnel(float cosine, float f0)
{
    // An index-matched interface has no reflection, including at grazing angles.
    return _ClearCoatIOR <= 1.0 ? 0.0 : f0 + (1.0 - f0) * Pow5(1.0 - saturate(cosine));
}

TomCoatData TomGetCoat(TomVaryings i, TomMaterialData material, float3 viewDir, float orientation)
{
    TomCoatData coat;
    coat.weight = 0.0;
    coat.roughness = 0.1;
    coat.f0 = TomIorToF0(_ClearCoatIOR);
    coat.normalWS = material.geometricNormalWS;
    coat.viewRetention = coat.indirectRetention = 1.0;
    UNITY_BRANCH
    if (_ClearCoat > 0.0 || _ClearCoatDebugView > 0.5)
    {
        float2 map = SAMPLE_TEX2D_SAMPLER(_ClearCoatMap, _MainTex,
            i.uv0 * _ClearCoatMap_ST.xy + _ClearCoatMap_ST.zw).rg;
        coat.weight = saturate(_ClearCoat * map.r);
        coat.roughness = clamp(_ClearCoatRoughness * map.g, 0.04, 1.0);
        coat.normalWS = TomResolveCoatNormal(i, material.geometricNormalWS,
            material.mainNormalWS, material.shadingNormalWS, orientation);
#if defined(TOM_EYE)
        coat.normalWS = material.eyeInterfaceNormal;
#endif
#if defined(TOM_SKIN)
        coat.weight *= material.skin.coatCoverage;
        coat.normalWS = normalize(lerp(coat.normalWS, material.skin.liquidNormalWS,
            saturate(_SkinLiquidCoatNormal * material.skin.liquidCoverage)));
#endif
        float3 dx = ddx(coat.normalWS), dy = ddy(coat.normalWS);
        coat.roughness = TomFilteredRoughness(coat.roughness, 0.5 * (dot(dx, dx) + dot(dy, dy)));
        coat.viewRetention = 1.0 - coat.weight * TomCoatFresnel(dot(coat.normalWS, viewDir), coat.f0);
        float averageF = _ClearCoatIOR <= 1.0 ? 0.0 : coat.f0 + (1.0 - coat.f0) / 21.0;
        coat.indirectRetention = coat.viewRetention * (1.0 - coat.weight * averageF);
    }
    return coat;
}

float TomCoatDirectRetention(TomCoatData coat, float3 lightDir)
{
    return coat.viewRetention * (1.0 - coat.weight * TomCoatFresnel(dot(coat.normalWS, lightDir), coat.f0));
}

float TomCoatBodyRetention(float retention)
{
    return lerp(1.0, retention, saturate(_ClearCoatEnergyBlend));
}

float3 TomCoatDirect(TomCoatData coat, TomRawLightData light, float3 geometric, float3 viewDir)
{
    float3 h = light.direction + viewDir;
    float h2 = dot(h, h);
    float nv = saturate(dot(coat.normalWS, viewDir));
    float nl = saturate(dot(coat.normalWS, light.direction));
    float horizon = smoothstep(0.0, max(_NormalHorizonFade, 0.001), nl)
        * smoothstep(0.0, max(_NormalHorizonFade, 0.001), saturate(dot(geometric, light.direction)));
    float3 result = 0.0;
    UNITY_BRANCH
    if (coat.weight > 0.0 && _ClearCoatIOR > 1.0 && h2 >= 0.000001 && nv > 0.00001 && nl > 0.00001)
    {
        h *= rsqrt(h2);
        result = coat.weight * TomEvaluateProductionGGX(nv, nl, saturate(dot(coat.normalWS, h)),
            saturate(dot(viewDir, h)), coat.roughness, coat.f0.xxx, 1.0)
            * horizon * light.color * light.distanceAttenuation * light.cookieAttenuation * light.physicalShadowAttenuation;
    }
    return result;
}

float3 TomSampleProbe(UnityGIInput giInput, float3 viewDir, float3 normalWS, float roughness, float3 f0)
{
    Unity_GlossyEnvironmentData glossy = UnityGlossyEnvironmentSetup(1.0 - roughness, viewDir, normalWS, f0);
    return UnityGI_IndirectSpecular(giInput, 1.0, glossy);
}

float3 TomCoatEnvironment(TomCoatData coat, UnityGIInput giInput, float3 viewDir, float occlusion)
{
    float3 result = 0.0;
    UNITY_BRANCH
    if (coat.weight > 0.0 && _ClearCoatIOR > 1.0 && _ClearCoatEnvironmentStrength > 0.0)
    {
    float3 environment = TomSampleProbe(giInput, viewDir, coat.normalWS, coat.roughness, coat.f0.xxx);
    float realRoughness = PerceptualRoughnessToRoughness(coat.roughness);
#if defined(UNITY_COLORSPACE_GAMMA)
    float reduction = 1.0 - 0.28 * realRoughness * coat.roughness;
#else
    float reduction = 1.0 / (realRoughness * realRoughness + 1.0);
#endif
    float grazing = saturate(1.0 - coat.roughness + coat.f0);
    float3 fresnel = FresnelLerp(coat.f0.xxx, grazing.xxx, saturate(dot(coat.normalWS, viewDir)));
    result = environment * reduction * fresnel * coat.weight * _ClearCoatEnvironmentStrength
        * lerp(1.0, occlusion, saturate(_SpecularOcclusionStrength));
    }
    return result;
}

#endif
