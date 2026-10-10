#ifndef TOM_LIQUID_OVERLAY_INC
#define TOM_LIQUID_OVERLAY_INC
TomMaterialData TomLiquidOverlaySurface(TomVaryings i, fixed face)
{
    TomMaterialData m;
    UNITY_INITIALIZE_OUTPUT(TomMaterialData, m);
    float a = SAMPLE_TEX2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
    clip(TomSurfaceAlpha(i, a) - _Cutoff);
    float orientation = TomFaceOrientation(face);
    m.geometricNormalWS = normalize(i.normalWS) * orientation;
    float3 ts = UnpackScaleNormal(SAMPLE_TEX2D(_NormalMap, i.uv0 * _NormalMap_ST.xy + _NormalMap_ST.zw), _NormalMapScale);
    m.mainNormalWS = normalize(ts.x * normalize(i.tangentWS.xyz)
        + ts.y * normalize(i.bitangentWS) * orientation + ts.z * m.geometricNormalWS);
    m.shadingNormalWS = m.mainNormalWS;
    m.occlusion = 1.0;
    TomLiquidApplySurface(i, orientation, m);
    return m;
}

TomRawLightData TomLiquidOverlayLight(TomVaryings i, float intensity)
{
    float distance, cookie;
    TomSampleLightDistanceAndCookie(i.posWS, distance, cookie);
    return TomBuildRawLightData(TomSafeDirection(UnityWorldSpaceLightDir(i.posWS)), _LightColor0.rgb,
        distance, cookie, TomGetShadowData(i), intensity);
}

float TomLiquidOverlayToonVisibility(TomMaterialData m, TomRawLightData light)
{
    // The accepted helper pigment response has no substrate Toon-normal/fill controls.
    float nl = saturate(dot(m.shadingNormalWS, light.direction));
    float gn = saturate(dot(m.geometricNormalWS, light.direction));
    float horizon = smoothstep(0.0, max(_NormalHorizonFade, 0.001), nl)
        * smoothstep(0.0, max(_NormalHorizonFade, 0.001), gn);
    float shadow = lerp(1.0, TomShapeShadow(light.physicalShadowAttenuation), saturate(_ShadowStrength));
    float input = nl * lerp(1.0, shadow, saturate(_UseRampForShadows));
    float ramp = light.physicalShadowAttenuation;
    if (_UseRamp > 0.0) ramp = TomSampleRamp(input, m.secondaryToneMask, 0.0);
    float outside = lerp(shadow, 1.0, saturate(_UseRampForShadows) * saturate(_UseRamp));
    return lerp(nl, ramp, saturate(_UseRamp)) * horizon * light.distanceAttenuation
        * light.cookieAttenuation * min(outside, shadow);
}

float4 TomLiquidOverlayBase(TomVaryings i, fixed face : VFACE) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    TomMaterialData m = TomLiquidOverlaySurface(i, face);
    float w = TomLiquidWeight(m);
    float3 view = TomInterfaceView(i.posWS);
    TomRawLightData light = TomLiquidOverlayLight(i, _MainLightIntensity);
    float toonVisibility = TomLiquidOverlayToonVisibility(m, light);
    UnityGIInput gi = TomBuildGIInput(i, m, view, light.direction,
        light.distanceAttenuation * light.cookieAttenuation * light.sampledShadowAttenuation);
    float ownership = TomGetMainDirectOwnership();
    float3 direct = TomLiquidDirect(m, light, view) * ownership;
    float3 environment = TomLiquidEnvironment(m, gi, view);
    float pigment = TomLiquidPigment();
    float3 pigmentTint = TomLiquidPigmentShadowTint(lerp(1.0,
        TomLiquidPigmentVisibility(m, light, toonVisibility), ownership));
    float3 color = (TomLiquidPigmentDirect(m, light, toonVisibility)
        * TomLiquidDirectRetention(m, view, light.direction) * ownership
        + TomLiquidPigmentIndirect(i, m, gi) * pigmentTint * TomLiquidIndirectRetention(m, view)) * pigment;
    color = (color + direct + environment) * w;
    // No GrabPass: approximate transmission of the already rendered substrate.
    float alpha = w * (pigment + (1.0 - pigment) * (1.0 - TomLiquidViewRetention(m, view)));
    float4 result = float4(max(color, 0.0), saturate(alpha));
    if (_LiquidDebugView > 0.5)
        result = float4(TomLiquidDebug(m, direct, environment), 1.0);
    else { UNITY_APPLY_FOG_COLOR(i.fogCoord, result, float4(0,0,0,0)); }
    return result;
}

float4 TomLiquidOverlayAdd(TomVaryings i, fixed face : VFACE) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    if (_LiquidDebugView > 0.5 && abs(_LiquidDebugView - 3.0) > 0.5) return 0.0;
    TomMaterialData m = TomLiquidOverlaySurface(i, face);
    float3 view = TomInterfaceView(i.posWS);
    TomRawLightData light = TomLiquidOverlayLight(i, _AdditionalLightIntensity);
    float toonVisibility = TomLiquidOverlayToonVisibility(m, light);
    float3 direct = TomLiquidDirect(m, light, view);
    float3 color = direct + TomLiquidPigmentDirect(m, light, toonVisibility)
        * TomLiquidPigment() * TomLiquidDirectRetention(m, view, light.direction);
    float4 result = float4(max(color, 0.0) * TomLiquidWeight(m), 0.0);
    if (_LiquidDebugView > 0.5) result.rgb = TomLiquidDebug(m, direct, 0.0);
    else { UNITY_APPLY_FOG_COLOR(i.fogCoord, result, float4(0,0,0,0)); }
    return result;
}
#endif
