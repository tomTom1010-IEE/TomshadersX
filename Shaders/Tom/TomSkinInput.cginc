#ifndef TOM_SKIN_INPUT_INC
#define TOM_SKIN_INPUT_INC

DECLARE_TEX2D(_DetailMask);
DECLARE_TEX2D_NOSAMPLER(_LineMask);
DECLARE_TEX2D_NOSAMPLER(_NormalMask);
DECLARE_TEX2D_NOSAMPLER(_SkinControlMap);
DECLARE_TEX2D_NOSAMPLER(_ColMask);
DECLARE_TEX2D(_overtex1);
DECLARE_TEX2D(_overtex2);
DECLARE_TEX2D(_overtex3);
float4 _DetailMask_ST, _LineMask_ST, _NormalMask_ST, _SkinControlMap_ST, _ColMask_ST;
float4 _overtex1_ST, _overtex2_ST, _overtex3_ST;
float4 _overcolor1, _overcolor2, _overcolor3, _Col0, _Col1, _Col2, _Col3;
float _nip, _nipsize, _nip_specular, _tex1mask;
float _alpha_a, _alpha_b, _SkinMainAlphaClip;
float _SpecularPower, _SpecularPowerNail, _SpeclarHeight, _notusetexspecular;
float _UseDetailRAsSpecularMap, _SkinPatternStrength, _SkinGameGloss;
float _linetexon, _linewidthG, _SkinLineStrength, _SkinShadeStrength, _SkinGameLineColor;
float4 _LineColorG, _SkinLineColor;
float _SkinDiffuseNormalDetail, _SkinFaceNormalStrength;
float _SkinStrength, _SkinWrap, _SkinWarmth, _SkinWarmWidth;
float4 _SkinWarmColor;
float _SkinWetness, _SkinCoatCoverage, _SkinLiquidCoatNormal, _SkinDebugView;

struct TomSkinData
{
    float4 detail, lines, control;
    float3 diffuseNormalWS, liquidNormalWS, diffuseTint;
    float liquidCoverage, gloss, coatCoverage;
    float3 debug;
};
#endif
