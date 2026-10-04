#ifndef TOM_SKIN_LIGHTING_INC
#define TOM_SKIN_LIGHTING_INC

float TomSkinDiffuseVisibility(TomMaterialData m, TomRawLightData light, float shadow)
{
    float nl = dot(m.skin.diffuseNormalWS, light.direction);
    float amount = saturate(_SkinStrength * m.skin.control.r);
    float wrapped = saturate((nl + max(_SkinWrap, 0.0)) / (1.0 + max(_SkinWrap, 0.0)));
    float diffuseNL = lerp(saturate(nl), wrapped, amount);
    float horizon = smoothstep(0.0, max(_NormalHorizonFade, 0.001), diffuseNL)
        * smoothstep(0.0, max(_NormalHorizonFade, 0.001), saturate(dot(m.geometricNormalWS, light.direction)));
    float rampInput = diffuseNL * lerp(1.0, shadow, saturate(_UseRampForShadows));
    float ramp = light.physicalShadowAttenuation;
    if (_UseRamp > 0.0) ramp = TomSampleRamp(rampInput, m.secondaryToneMask);
    float response = lerp(diffuseNL, ramp, saturate(_UseRamp));
    float shadowOutside = lerp(shadow, 1.0, saturate(_UseRampForShadows) * saturate(_UseRamp));
    return response * horizon * light.distanceAttenuation * light.cookieAttenuation * min(shadowOutside, shadow);
}

float3 TomSkinWarmTint(TomMaterialData m, float3 lightDir)
{
    float nl = saturate(dot(m.skin.diffuseNormalWS, lightDir));
    float transition = 1.0 - smoothstep(0.0, max(_SkinWarmWidth, 0.001), nl);
    float amount = saturate(_SkinStrength * _SkinWarmth * m.skin.control.r * m.skin.control.g * transition);
    float3 tint = max(_SkinWarmColor.rgb, 0.0);
    tint /= max(TomLuminance(tint), 0.05);
    return lerp(1.0.xxx, tint, amount);
}
#endif
