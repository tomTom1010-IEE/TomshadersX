#ifndef TOM_ALPHA_BACK_FRONT_INC
#define TOM_ALPHA_BACK_FRONT_INC

#include "TomToonLighting.cginc"

void TomClipBackPassFace(float faceSign)
{
    // The first color pass draws the complementary face. Cull Off uses only the main pass.
    clip(_CullOption - 0.5);
    if (_CullOption > 1.5) clip(-faceSign);
    else clip(faceSign);
}

float4 TomBackFaceBase(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
    TomClipBackPassFace(faceSign);
    return TomFragBase(i, faceSign);
}

float4 TomBackFaceAdd(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
    TomClipBackPassFace(faceSign);
    return TomFragAdd(i, faceSign);
}
#endif
