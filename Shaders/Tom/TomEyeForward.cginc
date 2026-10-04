#ifndef TOM_EYE_FORWARD_INC
#define TOM_EYE_FORWARD_INC

#include "TomToonLighting.cginc"

float4 TomEyeStencil(TomVaryings i) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float coverage = TomEyeSource(i).coverage;
    TomEyeClipColor(coverage);
    clip(coverage - _StencilCutoff);
    return 0.0;
}

#endif
