#ifndef TOM_SKIN_DEPTH_INC
#define TOM_SKIN_DEPTH_INC
#include "../KKPDeclarations.cginc"
DECLARE_TEX2D(_MainTex);
DECLARE_TEX2D(_AlphaMask);
DECLARE_TEX2D(_DetailMask);
float4 _MainTex_ST, _AlphaMask_ST, _DetailMask_ST;
float _Cutoff, _alpha_a, _alpha_b, _SkinMainAlphaClip, _SkinDebugView, _ClearCoatDebugView;
#include "TomSkinCoverage.cginc"
float TomSkinMainAlpha(float2 uv)
{
    float alpha = 1.0;
    if (_SkinMainAlphaClip > 0.5)
        alpha = SAMPLE_TEX2D(_MainTex, uv * _MainTex_ST.xy + _MainTex_ST.zw).a;
    return alpha;
}
#endif
