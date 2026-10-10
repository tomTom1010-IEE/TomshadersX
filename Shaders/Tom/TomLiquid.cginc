#ifndef TOM_LIQUID_INC
#define TOM_LIQUID_INC
float3 TomLiquidNormal(float4 packed)
{
    float3 n;
    if (_LiquidNormalEncoding > 1.5)
        n = UnpackNormal(packed);
    else
    {
        // Exported PNG RGB can be sRGB encoded while alpha remains numerical.
        // Mode 1 expects data sampling; never combine it with automatic sRGB decode.
        float g = packed.g;
        if (_LiquidNormalEncoding > 0.5)
            g = g <= 0.04045 ? g / 12.92 : pow((g + 0.055) / 1.055, 2.4);
        float2 xy = float2(packed.a, g) * 2.0 - 1.0;
        n = float3(xy, sqrt(max(1.0 - dot(xy, xy), 0.0001)));
    }
    float2 slope = n.xy * max(_SkinLiquidNormalScale, 0.0) / max(n.z, 0.05);
    float limit = tan(radians(clamp(_LiquidMaxNormalAngle, 5.0, 85.0)));
    slope *= rsqrt(1.0 + dot(slope, slope) / (limit * limit));
    return normalize(float3(slope, 1.0));
}

float TomLiquidShape(float value)
{
    float cutoff = clamp(_LiquidCutoff, 0.0, 0.999);
    float softness = max(max(_LiquidEdgeSoftness, fwidth(value)), 0.0001);
    return saturate((value - cutoff) / (1.0 - cutoff))
        * smoothstep(cutoff, cutoff + softness, value);
}

float TomLiquidPattern(float amount, float2 pattern)
{
    return max(saturate(amount) * pattern.r, saturate(amount - 1.0) * pattern.g);
}

float TomLiquidRegions(float3 regions, float2 pattern)
{
    float3 single = regions - max(regions.zzy, regions.yxx);
    float2 combined = (min(regions.yz, regions.xy) - 0.1) / 0.9;
    float coverage = min(single.r, TomLiquidPattern(_liquidftop, pattern));
    coverage = max(coverage, min(single.g, TomLiquidPattern(_liquidfbot, pattern)));
    coverage = max(coverage, min(single.b, TomLiquidPattern(_liquidbtop, pattern)));
    coverage = max(coverage, min(combined.x, TomLiquidPattern(_liquidbbot, pattern)));
    coverage = max(coverage, min(combined.y, TomLiquidPattern(_liquidface, pattern)));
    return saturate(coverage);
}

float TomLiquidPigment()
{
    return saturate(_SkinLiquidColorStrength * saturate(_SkinLiquidColor.a));
}

void TomLiquidApplySurface(TomVaryings i, float orientation, inout TomMaterialData m)
{
    m.liquid.rawCoverage = 0.0;
    m.liquid.coverage = 0.0;
    m.liquid.normalWS = m.mainNormalWS;
    m.liquid.diffuseNormalWS = m.mainNormalWS;
    m.liquid.roughness = clamp(_SkinLiquidRoughness, 0.04, 1.0);
    // Uniform early-out: dry materials avoid all liquid texture/gradient work.
    if (_SkinLiquidMaterial <= 0.0 || (_LiquidCoverageMode < 0.5
        && max(max(max(_liquidftop, _liquidfbot), max(_liquidbtop, _liquidbbot)), _liquidface) <= 0.0))
        return;
    float2 uv = i.uv0 * _LiquidTiling.zw + _LiquidTiling.xy;
    float2 pattern = _Texture2.Sample(sampler_tom_liquid_trilinear_repeat_aniso8,
        uv * _Texture2_ST.xy + _Texture2_ST.zw).rg;
    float custom = _LiquidCoverageMap.Sample(sampler_tom_liquid_trilinear_repeat_aniso8,
        uv * _LiquidCoverageMap_ST.xy + _LiquidCoverageMap_ST.zw).r;
    float3 regions = _liquidmask.Sample(sampler_tom_liquid_linear_clamp,
        i.uv0 * _liquidmask_ST.xy + _liquidmask_ST.zw).rgb;
    // Filter before the region amounts so animating an amount stays continuous.
    float2 shaped = float2(TomLiquidShape(pattern.r), TomLiquidShape(pattern.g));
    float customShaped = TomLiquidShape(custom);
    m.liquid.rawCoverage = _LiquidCoverageMode < 0.5 ? TomLiquidRegions(regions, pattern)
        : (_LiquidCoverageMode < 1.5 ? saturate(custom) : 1.0);
    m.liquid.coverage = _LiquidCoverageMode < 0.5 ? TomLiquidRegions(regions, shaped)
        : (_LiquidCoverageMode < 1.5 ? customShaped : 1.0);
    float3 detail = TomLiquidNormal(_Texture3.Sample(sampler_tom_liquid_trilinear_repeat_aniso8,
        uv * _Texture3_ST.xy + _Texture3_ST.zw));
    float3 n = m.geometricNormalWS;
    float3 t = TomSafeDirection(i.tangentWS.xyz - n * dot(n, i.tangentWS.xyz));
    float3 b = TomSafeDirection(cross(n, t)) * (i.tangentWS.w < 0.0 ? -1.0 : 1.0) * unity_WorldTransformParams.w;
    float3 baseTS = float3(dot(m.mainNormalWS, t), dot(m.mainNormalWS, b), dot(m.mainNormalWS, n));
    // Reoriented normal mapping keeps the substrate's large-scale shape.
    float3 r = baseTS + float3(0, 0, 1);
    float3 u = detail * float3(-1, -1, 1);
    float3 ts = normalize(r * dot(r, u) / max(r.z, 0.0001) - u);
    m.liquid.normalWS = normalize(ts.x * t + ts.y * b + ts.z * n);
    m.liquid.diffuseNormalWS = normalize(lerp(m.mainNormalWS, m.liquid.normalWS, saturate(_LiquidDiffuseNormal)));
    // Unconditional derivatives avoid undefined gradients at coverage boundaries.
    float3 dx = ddx(m.liquid.normalWS), dy = ddy(m.liquid.normalWS);
    float variance = 0.5 * (dot(dx, dx) + dot(dy, dy));
    float roughness = clamp(_SkinLiquidRoughness, 0.04, 1.0);
    m.liquid.roughness = pow(saturate(pow(roughness, 4.0) + min(variance * max(_LiquidNormalAA, 0.0), 0.25)), 0.25);
}

float TomLiquidWeight(TomMaterialData m)
{
    return saturate(m.liquid.coverage * _SkinLiquidMaterial);
}

float TomLiquidFresnel(float cosine)
{
    float f0 = TomIorToF0(max(_LiquidIOR, 1.0));
    // An index-matched interface has no reflection, including at grazing angles.
    return f0 > 0.0 ? f0 + (1.0 - f0) * pow(1.0 - saturate(cosine), 5.0) : 0.0;
}

float3 TomLiquidTransmissionNormal(TomMaterialData m)
{
    // Reflection keeps the full detail normal; substrate dimming is independently shaped.
    return normalize(lerp(m.mainNormalWS, m.liquid.normalWS, saturate(_LiquidAttenuationNormal)));
}

float TomLiquidEnergyRetention(float retention)
{
    return lerp(1.0, saturate(retention), saturate(_LiquidEnergyBlend));
}

float TomLiquidViewRetention(TomMaterialData m, float3 view)
{
    return TomLiquidEnergyRetention(1.0 - TomLiquidFresnel(saturate(dot(TomLiquidTransmissionNormal(m), view))));
}

float TomLiquidDirectRetention(TomMaterialData m, float3 view, float3 light)
{
    float3 n = TomLiquidTransmissionNormal(m);
    float retention = (1.0 - TomLiquidFresnel(saturate(dot(n, view))))
        * (1.0 - TomLiquidFresnel(saturate(dot(n, light))));
    // Apply the artistic energy blend once, after both interface crossings.
    return TomLiquidEnergyRetention(retention);
}

float TomLiquidIndirectRetention(TomMaterialData m, float3 view)
{
    float f0 = TomIorToF0(max(_LiquidIOR, 1.0));
    float averageF = f0 > 0.0 ? f0 + (1.0 - f0) / 21.0 : 0.0;
    float viewRetention = 1.0 - TomLiquidFresnel(saturate(dot(TomLiquidTransmissionNormal(m), view)));
    return TomLiquidEnergyRetention(viewRetention * (1.0 - averageF));
}

float3 TomLiquidSubstrateSpecular(TomMaterialData m)
{
    float dryF0 = TomIorToF0(_SpecularIOR);
    float ratio = (_SpecularIOR - max(_LiquidIOR, 1.0)) / max(_SpecularIOR + max(_LiquidIOR, 1.0), 0.001);
    float indexMatch = saturate(ratio * ratio / max(dryF0, 0.00001));
    float3 interfaceMatch = lerp(indexMatch.xxx, 1.0.xxx, m.metallic);
    return lerp(1.0.xxx, interfaceMatch, saturate(_LiquidEnergyBlend)) * (1.0 - TomLiquidPigment());
}

float3 TomLiquidDirect(TomMaterialData m, TomRawLightData light, float3 viewDir)
{
    float3 h = light.direction + viewDir;
    float h2 = dot(h, h);
    float nv = saturate(dot(m.liquid.normalWS, viewDir));
    float nl = saturate(dot(m.liquid.normalWS, light.direction));
    float horizon = smoothstep(0.0, max(_NormalHorizonFade, 0.001), nl)
        * smoothstep(0.0, max(_NormalHorizonFade, 0.001), saturate(dot(m.geometricNormalWS, light.direction)));
    float3 result = 0.0;
    float f0 = TomIorToF0(max(_LiquidIOR, 1.0));
    UNITY_BRANCH
    if (h2 >= 0.000001 && nv > 0.00001 && nl > 0.00001 && _LiquidSpecularStrength > 0.0 && f0 > 0.0)
    {
        h *= rsqrt(h2);
        result = TomEvaluateProductionGGX(nv, nl, saturate(dot(m.liquid.normalWS, h)),
            saturate(dot(viewDir, h)), m.liquid.roughness, f0.xxx, 1.0)
            * _LiquidSpecularStrength * horizon * light.color * light.distanceAttenuation
            * light.cookieAttenuation * light.physicalShadowAttenuation;
    }
    return result;
}

float TomLiquidPigmentVisibility(TomMaterialData m, TomRawLightData light, float toonVisibility)
{
    float soft = saturate(dot(m.liquid.diffuseNormalWS, light.direction)) * light.physicalShadowAttenuation;
    return lerp(soft, saturate(toonVisibility), saturate(_LiquidToonBlend));
}

float3 TomLiquidPigmentShadowTint(float visibility)
{
    // A multiplicative pigment tint, never an emissive fill or a shadow-map override.
    return lerp(1.0.xxx, saturate(_LiquidShadowColor.rgb),
        saturate(_LiquidShadowColor.a) * (1.0 - saturate(visibility)));
}

float3 TomLiquidPigmentDirect(TomMaterialData m, TomRawLightData light, float toonVisibility)
{
    float nl = saturate(dot(m.liquid.diffuseNormalWS, light.direction));
    float visibility = nl * light.distanceAttenuation * light.cookieAttenuation * light.physicalShadowAttenuation;
    float3 tint = TomLiquidPigmentShadowTint(TomLiquidPigmentVisibility(m, light, toonVisibility));
    return max(_SkinLiquidColor.rgb, 0.0) * tint * light.color * lerp(visibility, toonVisibility, saturate(_LiquidToonBlend));
}

float3 TomLiquidEnvironment(TomMaterialData m, UnityGIInput giInput, float3 viewDir)
{
    float f0 = TomIorToF0(max(_LiquidIOR, 1.0));
    if (f0 <= 0.0 || _LiquidEnvironmentStrength <= 0.0) return 0.0;
    float3 environment = TomSampleProbe(giInput, viewDir, m.liquid.normalWS, m.liquid.roughness, f0.xxx);
    float realRoughness = PerceptualRoughnessToRoughness(m.liquid.roughness);
#if defined(UNITY_COLORSPACE_GAMMA)
    float reduction = 1.0 - 0.28 * realRoughness * m.liquid.roughness;
#else
    float reduction = 1.0 / (realRoughness * realRoughness + 1.0);
#endif
    float grazing = saturate(1.0 + f0 - m.liquid.roughness);
    return environment * reduction * FresnelLerp(f0.xxx, grazing.xxx,
        saturate(dot(m.liquid.normalWS, viewDir))) * _LiquidSpecularStrength * _LiquidEnvironmentStrength
        * lerp(1.0, m.occlusion, saturate(_SpecularOcclusionStrength));
}

float3 TomLiquidDebug(TomMaterialData m, float3 direct, float3 environment)
{
    int mode = (int)floor(_LiquidDebugView + 0.5);
    float w = TomLiquidWeight(m);
    if (mode == 1) return w.xxx;
    if (mode == 2) return (m.liquid.normalWS * 0.5 + 0.5) * w;
    if (mode == 3) return direct * w;
    if (mode == 4) return environment * w;
    if (mode == 5) return (w * TomLiquidPigment()).xxx;
    return m.liquid.rawCoverage.xxx;
}
#endif
