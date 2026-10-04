#ifndef TOM_EYE_COVERAGE_INC
#define TOM_EYE_COVERAGE_INC

struct TomEyeLayers
{
    float4 iris;
    float4 expression;
    float4 overlay;
    float coverage;
};

TomEyeLayers TomEyeSource(TomVaryings i)
{
    TomEyeLayers eye;
    eye.iris = SAMPLE_TEX2D(_MainTex, TomEyeMainUV(i.uv0));
    eye.expression = eye.overlay = 0.0;
#if defined(TOM_EYE_IRIS)
    float3 v = normalize(lerp(_WorldSpaceCameraPos.xyz - i.posWS, UNITY_MATRIX_V[2].xyz, unity_OrthoParams.w));
    float2 expressionUV = i.uv0 - 0.06 * _ExpressionDepth
        * float2(dot(i.tangentWS.xyz, v), dot(i.bitangentWS, v));
    expressionUV = expressionUV * _MainTex_ST.xy + _MainTex_ST.zw;
    expressionUV = (expressionUV - 0.5) / max(0.1, _ExpressionSize) + float2(0.5, 0.6);
    // Legacy expression mapping intentionally does not consume _expression_ST.
    eye.expression = SAMPLE_TEX2D_SAMPLER(_expression, _MainTex, expressionUV);
    eye.expression.a *= _exppower;
    float4 a = SAMPLE_TEX2D_SAMPLER(_overtex1, _MainTex, i.eyeOverlayUV.xy * _overtex1_ST.xy + _overtex1_ST.zw).a * _overcolor1;
    float4 b = SAMPLE_TEX2D_SAMPLER(_overtex2, _MainTex, i.eyeOverlayUV.zw * _overtex2_ST.xy + _overtex2_ST.zw).a * _overcolor2;
    eye.overlay = 1.0 - (1.0 - a) * (1.0 - b);
    eye.overlay.a = saturate(eye.overlay.a * _isHighLight);
#endif
    float mask = SAMPLE_TEX2D(_AlphaMask, i.uv0 * _AlphaMask_ST.xy + _AlphaMask_ST.zw).r;
    eye.coverage = saturate(saturate(eye.iris.a + eye.expression.a + eye.overlay.a) * mask * _Alpha);
    return eye;
}

void TomEyeClipColor(float coverage)
{
    clip(coverage > 0.000001 ? 1.0 : -1.0);
    clip(coverage - _Cutoff);
}

float TomEyeStencilSurvival(float coverage)
{
    return coverage > 0.000001 && coverage >= _Cutoff && coverage >= _StencilCutoff ? 1.0 : 0.0;
}

#endif
