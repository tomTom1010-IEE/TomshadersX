#ifndef TOM_SKIN_SDF_INC
#define TOM_SKIN_SDF_INC

void TomSkinFaceFrame(out float3 forward, out float3 right)
{
    float3 localForward = _FaceSDFForward.xyz;
    if (dot(localForward, localForward) < 0.000001) localForward = float3(0, 0, 1);
    localForward = normalize(localForward);
    float3 localUp = _FaceSDFUp.xyz - localForward * dot(_FaceSDFUp.xyz, localForward);
    if (dot(localUp, localUp) < 0.000001)
    {
        float3 fallback = abs(localForward.y) < 0.99 ? float3(0, 1, 0) : float3(1, 0, 0);
        localUp = fallback - localForward * dot(fallback, localForward);
    }
    localUp = normalize(localUp);
    float3 up;
    if (_FaceSDFDirectionSource > 0.5 && _TomFaceSDFHeadValid > 0.5)
    {
        forward = mul((float3x3)_TomFaceSDFHeadToWorld, localForward);
        up = mul((float3x3)_TomFaceSDFHeadToWorld, localUp);
    }
    else
    {
        forward = mul((float3x3)unity_ObjectToWorld, localForward);
        up = mul((float3x3)unity_ObjectToWorld, localUp);
    }
    forward = TomSafeDirection(forward);
    up -= forward * dot(up, forward);
    right = TomSafeDirection(cross(up, forward));
}

// X: lit region, Y: replacement weight, Z: selected map value, W: light threshold.
float4 TomSkinFaceSDF(TomMaterialData m, float3 lightDir)
{
    float4 result = 0.0;
    UNITY_BRANCH
    if (_UseFaceSDF > 0.5 && _UseRamp > 0.0)
    {
        float3 forward, right;
        TomSkinFaceFrame(forward, right);
        float2 projected = float2(dot(lightDir, right), dot(lightDir, forward));
        float lengthSq = dot(projected, projected);
        float2 direction = projected * rsqrt(max(lengthSq, 0.000001));
        float threshold = (1.0 - clamp(direction.y, -1.0, 1.0)) * 0.5;
        if (_FaceSDFAngleMode > 0.5)
            threshold = atan2(abs(direction.x), direction.y) / UNITY_PI;
        threshold = saturate(threshold + _FaceSDFOffset);
        float2 uv = m.skin.sdfUV;
        if (_FaceSDFSideMode > 0.5 && _FaceSDFSideMode < 1.5 && direction.x < 0.0)
            uv.x = 2.0 * _FaceSDFMirrorU - uv.x;
        float4 map = _FaceSDFTex.Sample(sampler_tom_liquid_linear_clamp, uv);
        int channel = (int)clamp(floor(_FaceSDFChannel + 0.5), 0.0, 3.0);
        float value = map[channel];
        if (_FaceSDFSideMode > 1.5) value = direction.x < 0.0 ? map.g : map.r;
        value = lerp(value, 1.0 - value, saturate(_FaceSDFInvert));
        float width = max(_FaceSDFSoftness, fwidth(value - threshold) * max(_ToonAA, 0.0));
        result.x = TomSoftBand(value, threshold, width);
        // An axial light has no left/right direction. Fall back smoothly to normal shading.
        result.y = m.skin.sdfWeight * smoothstep(0.000001, 0.0001, lengthSq);
        result.z = value;
        result.w = threshold;
    }
    return result;
}

float TomSkinSDFResponse(TomMaterialData m, float3 lightDir, float diffuseShadow, float normalResponse)
{
    UNITY_BRANCH
    if (_UseFaceSDF > 0.5 && _UseRamp > 0.0)
    {
        float4 sdf = TomSkinFaceSDF(m, lightDir);
        float lit = sdf.x * lerp(1.0, diffuseShadow, saturate(_UseRampForShadows));
        float shade = TomSampleRamp(0.0, m.secondaryToneMask, 0.0);
        float bright = TomSampleRamp(1.0, m.secondaryToneMask, 0.0);
        return lerp(normalResponse, lerp(shade, bright, lit), sdf.y);
    }
    return normalResponse;
}

#endif
