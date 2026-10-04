#ifndef TOM_SKIN_COVERAGE_INC
#define TOM_SKIN_COVERAGE_INC

float TomSkinCoverage(float2 uv)
{
    float2 mask = SAMPLE_TEX2D(_AlphaMask, uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw).rg;
    float2 enabledMask = max(1.0 - saturate(float2(_alpha_a, _alpha_b)), mask);
    return min(enabledMask.x, enabledMask.y);
}

void TomSkinClip(float2 uv, float mainAlpha)
{
    clip(TomSkinCoverage(uv) - 0.5);
    if (_SkinMainAlphaClip > 0.5) clip(mainAlpha - _Cutoff);
}
#endif
