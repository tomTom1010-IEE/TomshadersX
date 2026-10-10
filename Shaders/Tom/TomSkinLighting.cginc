#ifndef TOM_SKIN_LIGHTING_INC
#define TOM_SKIN_LIGHTING_INC

float TomSkinDiffuseNL(TomMaterialData m, float3 normalWS, float3 lightDir)
{
    float nl = dot(normalWS, lightDir);
    float amount = saturate(_SkinStrength * m.skin.control.r);
    float wrapped = saturate((nl + max(_SkinWrap, 0.0)) / (1.0 + max(_SkinWrap, 0.0)));
    return lerp(saturate(nl), wrapped, amount);
}

float TomSkinContinuousVisibility(TomMaterialData m, float3 lightDir)
{
    float diffuseNL = TomSkinDiffuseNL(m, m.skin.diffuseNormalWS, lightDir);
    float horizon = smoothstep(0.0, max(_NormalHorizonFade, 0.001), diffuseNL)
        * smoothstep(0.0, max(_NormalHorizonFade, 0.001), saturate(dot(m.geometricNormalWS, lightDir)));
    return diffuseNL * horizon;
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
