#ifndef TOM_LIQUID_INPUT_INC
#define TOM_LIQUID_INPUT_INC
DECLARE_TEX2D_NOSAMPLER(_Texture2);
DECLARE_TEX2D_NOSAMPLER(_Texture3);
DECLARE_TEX2D_NOSAMPLER(_liquidmask);
DECLARE_TEX2D_NOSAMPLER(_LiquidCoverageMap);
SamplerState sampler_tom_liquid_trilinear_repeat_aniso8;
SamplerState sampler_tom_liquid_linear_clamp;
float4 _Texture2_ST, _Texture3_ST, _liquidmask_ST, _LiquidCoverageMap_ST;
float4 _LiquidTiling, _SkinLiquidColor, _LiquidShadowColor;
float _liquidftop, _liquidfbot, _liquidbtop, _liquidbbot, _liquidface;
float _SkinLiquidColorStrength, _SkinLiquidNormalScale, _SkinLiquidRoughness, _SkinLiquidMaterial;
float _LiquidDiffuseNormal, _LiquidSpecularStrength, _LiquidNormalEncoding;
float _LiquidCoverageMode, _LiquidCutoff, _LiquidEdgeSoftness;
float _LiquidMaxNormalAngle, _LiquidNormalAA, _LiquidIOR, _LiquidEnvironmentStrength;
float _LiquidToonBlend, _LiquidDebugView;
float _LiquidEnergyBlend, _LiquidAttenuationNormal;
struct TomLiquidData
{
    float rawCoverage;
    float coverage;
    float3 normalWS;
    float3 diffuseNormalWS;
    float roughness;
};
#endif
