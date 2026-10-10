#ifndef TOM_TOON_INPUT_INC
#define TOM_TOON_INPUT_INC

#include "../KKPDeclarations.cginc"

#if defined(TOM_SKIN)
#define TOM_LIQUID 1
#endif
#if defined(TOM_LIQUID)
#include "TomLiquidInput.cginc"
#endif

struct TomVertexData
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 uv0 : TEXCOORD0;
	float2 uv1 : TEXCOORD1;
	float2 uv2 : TEXCOORD2;
#if defined(TOM_SKIN)
	float2 uv3 : TEXCOORD3;
	float4 color : COLOR;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TomVaryings
{
	float4 posCS : SV_POSITION;
	float2 uv0 : TEXCOORD0;
	float3 posWS : TEXCOORD1;
	float3 normalWS : TEXCOORD2;
	float4 tangentWS : TEXCOORD3;
	float3 bitangentWS : TEXCOORD4;
	half4 ambientOrLightmapUV : TEXCOORD5;
	UNITY_LIGHTING_COORDS(6, 7)
	UNITY_FOG_COORDS(8)
	half3 vertexLightDiffuse : TEXCOORD9;
#if defined(TOM_EYE_IRIS)
	float4 eyeOverlayUV : TEXCOORD10;
#endif
#if defined(TOM_SKIN)
	float4 skinOverlayUV12 : TEXCOORD10;
	float4 skinOverlayUV3Color : TEXCOORD11;
#endif
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

DECLARE_TEX2D(_MainTex);
DECLARE_TEX2D(_AlphaMask);
DECLARE_TEX2D(_NormalMap);
DECLARE_TEX2D_NOSAMPLER(_NormalMapDetail);
DECLARE_TEX2D(_RampTex);
#if defined(TOM_SKIN)
// Skin's material masks are UV0 atlas data. Liquid/overlay samplers stay separate.
DECLARE_TEX2D_NOSAMPLER(_SpecularMask);
DECLARE_TEX2D_NOSAMPLER(_MetallicMap);
DECLARE_TEX2D_NOSAMPLER(_RoughnessMap);
DECLARE_TEX2D_NOSAMPLER(_OcclusionMap);
DECLARE_TEX2D_NOSAMPLER(_ReflectionMask);
DECLARE_TEX2D_NOSAMPLER(_EmissionMask);
#define sampler_SpecularMask sampler_MainTex
#define sampler_MetallicMap sampler_MainTex
#define sampler_RoughnessMap sampler_MainTex
#define sampler_OcclusionMap sampler_MainTex
#define sampler_ReflectionMask sampler_MainTex
#define sampler_EmissionMask sampler_MainTex
#else
DECLARE_TEX2D(_SpecularMask);
DECLARE_TEX2D(_MetallicMap);
DECLARE_TEX2D(_RoughnessMap);
DECLARE_TEX2D(_OcclusionMap);
DECLARE_TEX2D(_ReflectionMask);
DECLARE_TEX2D(_EmissionMask);
#endif
DECLARE_TEX2D(_MatCap);

float4 _MainTex_ST;
float4 _AlphaMask_ST;
float4 _NormalMap_ST;
float4 _NormalMapDetail_ST;
float4 _RampTex_ST;
float4 _SpecularMask_ST;
float4 _MetallicMap_ST;
float4 _RoughnessMap_ST;
float4 _OcclusionMap_ST;
float4 _ReflectionMask_ST;
float4 _MatCap_ST;
float4 _EmissionMask_ST;

float4 _BaseColor;
float _Cutoff;
float _CullOption;
float _NormalMapScale;
float _DetailNormalMapScale;

float4 _AmbientColor;
float4 _ShadowColor;
float _ShadowStrength;
float _UseRamp;
float _UseRampForShadows;
float _ShadowRemapStrength;
float _ShadowThreshold;
float _ShadowSoftness;
float _MainLightIntensity;
float _AdditionalLightIntensity;
float _VertexLightIntensity;
float _LightProbeBlend;
float _CustomSHVolumeBlend;
float _IndirectDiffuseIntensity;

#if defined(TOM_LIQUID)
// The SH atlas is linear/clamped without mipmaps, like the liquid region mask.
// Sharing this state preserves the 16-sampler limit in lightmapped variants.
Texture3D _TomSHVolumeTex;
#define TOM_SAMPLE_SH(uv) _TomSHVolumeTex.SampleLevel(sampler_tom_liquid_linear_clamp, uv, 0)
#else
sampler3D _TomSHVolumeTex;
#define TOM_SAMPLE_SH(uv) tex3D(_TomSHVolumeTex, uv)
#endif
float4x4 _TomSHWorldToLocal;
float4 _TomSHBoundsMin;
float4 _TomSHBoundsInvSize;
float4 _TomSHVolumeGrid;
float4 _TomSHVolumeParams;

float4 _SpecularColor;
float _SpecularStrength;
float _SpecularShadowStrength;
float _Metallic;
float _Roughness;
float _RoughnessBias;
float _SpecularIOR;
float _FresnelStrength;
float _NormalHorizonFade;
float _OcclusionStrength;
float _SpecularOcclusionStrength;

float4 _RimColor;
float _RimStrength;
float _RimShadowStrength;

float _ReflectionMode;
float _MatCapBlendMode;
float _MatCapIntensity;
float _MatCapMip;
float _EnvironmentIntensity;
float _EnvironmentRoughness;
float _UseMaterialRoughnessForEnvironment;
float _ReflectionShadowStrength;

float4 _EmissionColor;
float _EmissionIntensity;

float _OutlineOn;
float4 _OutlineColor;
float _OutlineWidth;
float _OutlineScreenSpace;
float _OutlineDepthOffset;

float _UsePackedMaterialMap;
DECLARE_TEX2D_NOSAMPLER(_MaterialMap);
float4 _MaterialMap_ST;
DECLARE_TEX2D_NOSAMPLER(_LayerMask);
float4 _LayerMask_ST;
DECLARE_TEX2D_NOSAMPLER(_SecondaryRamp);
float4 _SecondaryRamp_ST;
DECLARE_TEX2D_NOSAMPLER(_OutlineWidthMask);
float4 _OutlineWidthMask_ST;
float _DiffuseEnergyBlend;
float _RampMode;
float _ToonThreshold;
float _ToonSoftness;
float _ToonShadeLevel;
float _ToonNormalInfluence;
float _ToonDetailNormalInfluence;
float _ToonAA;
float4 _ToonShadeColor;
float _ToonMinLighting;
float _SecondaryToneStrength;
float _IndirectToonBlend;
float _IndirectToonThreshold;
float _IndirectToonSoftness;
float _IndirectShadeLevel;
float _SpecularToonBlend;
float _SpecularSize;
float _SpecularThreshold;
float _SpecularSoftness;
float _SpecularBands;
float _SpecularAA;
float _EnvironmentToonBlend;
float _EnvironmentThreshold;
float _EnvironmentSoftness;
float _EnvironmentExposure;
float _EnvironmentColorWeight;
float _EnvironmentBaseColorTint;
float _EnvironmentFresnelStrength;
float4 _EnvironmentColor;
float _RimWidth;
float _RimSoftness;
float _RimLightInfluence;
float _MatCapNormalSource;
float _EmissionKeepCol;
float _OutlineNormalSource;
float _DebugView;

#if defined(TOM_EYE)
#include "TomEyeInput.cginc"
#endif
#if defined(TOM_SKIN)
#include "TomSkinInput.cginc"
#endif

#endif
