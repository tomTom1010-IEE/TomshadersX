#ifndef TOM_EYE_INPUT_INC
#define TOM_EYE_INPUT_INC

float _Alpha, _StencilCutoff, _EyeDebugView;
#if defined(TOM_EYE_IRIS)
DECLARE_TEX2D_NOSAMPLER(_overtex1);
DECLARE_TEX2D_NOSAMPLER(_overtex2);
DECLARE_TEX2D_NOSAMPLER(_expression);
float4 _overtex1_ST, _overtex2_ST;
float4 _overcolor1, _overcolor2;
float _rotation, _isHighLight, _exppower, _ExpressionSize, _ExpressionDepth;
float _EyeOpticsMode, _EyeOpticsStrength, _IrisDepth;
float _IrisCenterX, _IrisCenterY, _IrisRadiusX, _IrisRadiusY;
float _EyeOpticsEdgeFade, _EyeOpticsMaxOffset, _EyeDispersion;
float _EyeUVMinX, _EyeUVMinY, _EyeUVMaxX, _EyeUVMaxY;
DECLARE_TEX2D_NOSAMPLER(_EyeSurfaceMap);
float _EyeRefractionIOR, _IrisDepthShape, _UseEyeSurfaceMask, _EyeSurfaceMapMode;
#else
float4 _Color;
#endif

float2 TomEyeRotate(float2 uv, float turns)
{
    float s, c;
    sincos(turns * 6.28318530718, s, c);
    uv -= 0.5;
    return float2(c * uv.x - s * uv.y, s * uv.x + c * uv.y) + 0.5;
}

float2 TomEyeMainUV(float2 raw)
{
#if defined(TOM_EYE_IRIS)
    raw = TomEyeRotate(raw, -_rotation);
#endif
    return raw * _MainTex_ST.xy + _MainTex_ST.zw;
}

#endif
