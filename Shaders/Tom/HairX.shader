Shader "tom/HairX"
{
	Properties
	{
		_MainTex ("Main Texture", 2D) = "white" {}
		_NormalMap ("Normal Map", 2D) = "bump" {}
		_NormalMapDetail ("Detail Normal Map", 2D) = "bump" {}
		_AlphaMask ("Alpha Mask", 2D) = "white" {}
		_RampTex ("Toon Ramp", 2D) = "white" {}
		_SpecularMask ("Specular Mask", 2D) = "white" {}
		_MetallicMap ("Metallic Map", 2D) = "white" {}
		_RoughnessMap ("Roughness Map", 2D) = "white" {}
		_OcclusionMap ("Occlusion Map", 2D) = "white" {}
		_ReflectionMask ("Reflection Mask", 2D) = "white" {}
		_MatCap ("MatCap", 2D) = "black" {}
		_EmissionMask ("Emission Mask", 2D) = "black" {}

		[Gamma] _BaseColor ("Base Color", Color) = (1,1,1,1)
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
		[Enum(Off,0,Front,1,Back,2)] _CullOption ("Cull", Float) = 0
		_NormalMapScale ("Normal Scale", Range(0,2)) = 1
		_DetailNormalMapScale ("Detail Normal Scale", Range(0,2)) = 1

		[Gamma] _AmbientColor ("Ambient Color", Color) = (0.35,0.35,0.35,1)
		[Gamma] _ShadowColor ("Shadow Color", Color) = (0.65,0.65,0.7,1)
		_ShadowStrength ("Shadow Strength", Range(0,1)) = 1
		[MaterialToggle] _UseRamp ("Use Toon Ramp", Float) = 1
		[MaterialToggle] _UseRampForShadows ("Put Shadows Into Ramp", Float) = 1
		_ShadowRemapStrength ("Shadow Remap Strength", Range(0,1)) = 0
		_ShadowThreshold ("Shadow Threshold", Range(0,1)) = 0.5
		_ShadowSoftness ("Shadow Softness", Range(0.001,1)) = 0.2
		_ShadowOffsetFactor ("Shadow Offset Factor", Range(-4,4)) = 0
		_ShadowOffsetUnits ("Shadow Offset Units", Range(-4,4)) = 0
		_MainLightIntensity ("Main Light Intensity", Range(0,4)) = 1
		_AdditionalLightIntensity ("Additional Light Intensity", Range(0,4)) = 1
		_VertexLightIntensity ("Vertex Light Fill Intensity", Range(0,1)) = 0.5
		_LightProbeBlend ("Light Probe Blend", Range(0,1)) = 1
		_CustomSHVolumeBlend ("Custom SH Volume Blend", Range(0,1)) = 1
		_IndirectDiffuseIntensity ("Indirect Diffuse Intensity", Range(0,4)) = 0.25

		[Gamma] _SpecularColor ("Direct Specular Color", Color) = (1,1,1,1)
		_SpecularStrength ("Direct Specular Strength", Range(0,4)) = 0.5
		_SpecularShadowStrength ("Specular Shadow Strength", Range(0,1)) = 1
		_Metallic ("Metallic", Range(0,1)) = 0
		_Roughness ("Roughness", Range(0.04,1)) = 0.5
		_RoughnessBias ("Roughness Bias", Range(-1,1)) = 0
		_SpecularIOR ("Specular IOR", Range(1,2.5)) = 1.5
		_FresnelStrength ("Fresnel Strength", Range(0,2)) = 1
		_NormalHorizonFade ("Normal Horizon Fade", Range(0.001,0.25)) = 0.05
		_OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
		_SpecularOcclusionStrength ("Specular Occlusion Strength", Range(0,1)) = 1

		[Gamma] _RimColor ("Rim Color", Color) = (1,1,1,1)
		_RimStrength ("Rim Strength", Range(0,4)) = 0
		_RimShadowStrength ("Rim Shadow Strength", Range(0,1)) = 0

		[Enum(Off,0,MatCap,1,Environment,2,MatCapAndEnvironment,3)] _ReflectionMode ("Reflection Mode", Float) = 0
		[Enum(Multiply,0,Add,1)] _MatCapBlendMode ("MatCap Blend", Float) = 1
		_MatCapIntensity ("MatCap Intensity", Range(0,2)) = 1
		_MatCapMip ("MatCap Mip", Range(0,8)) = 0
		_EnvironmentIntensity ("Environment Intensity", Range(0,4)) = 1
		_EnvironmentRoughness ("Environment Roughness", Range(0,1)) = 0.5
		[MaterialToggle] _UseMaterialRoughnessForEnvironment ("Use Material Roughness For Environment", Float) = 1
		_ReflectionShadowStrength ("Reflection Shadow Strength", Range(0,1)) = 0

		[MaterialToggle] _OutlineOn ("Outline", Float) = 0
		[Gamma] _OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth ("Outline Width", Range(0,8)) = 1
		[MaterialToggle] _OutlineScreenSpace ("Screen Space Outline", Float) = 1
		_OutlineDepthOffset ("Outline Depth Offset", Range(-1,1)) = 0

		[Gamma] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
		_EmissionIntensity ("Emission Intensity", Range(0,10)) = 0

		[MaterialToggle] _UsePackedMaterialMap ("Use Packed Material Map", Float) = 0
		_MaterialMap ("Material Map (Metal/Rough/AO/Spec)", 2D) = "white" {}
		_LayerMask ("Layer Mask (Tone/Rim/MatCap/Environment)", 2D) = "white" {}
		_SecondaryRamp ("Secondary Tone Ramp", 2D) = "white" {}
		_OutlineWidthMask ("Outline Width Mask", 2D) = "white" {}
		_DiffuseEnergyBlend ("Physical Diffuse Suppression", Range(0,1)) = 0
		[Enum(Procedural,0,Texture,1)] _RampMode ("Toon Ramp Source", Float) = 0
		_ToonThreshold ("Toon Threshold", Range(0,1)) = 0.5
		_ToonSoftness ("Toon Softness", Range(0.001,1)) = 0.1
		_ToonShadeLevel ("Toon Shade Level", Range(0,1)) = 0.15
		_SecondaryToneStrength ("Secondary Tone Strength", Range(0,1)) = 0
		_IndirectToonBlend ("Indirect Tone Blend", Range(0,1)) = 0
		_IndirectToonThreshold ("Indirect Tone Threshold", Range(0,2)) = 0.4
		_IndirectToonSoftness ("Indirect Tone Softness", Range(0.001,1)) = 0.2
		_IndirectShadeLevel ("Indirect Shade Level", Range(0,1)) = 0.3
		_SpecularToonBlend ("Direct Specular Toon Blend", Range(0,1)) = 1
		_SpecularSize ("Direct Highlight Size", Range(0,1)) = 0.5
		_SpecularThreshold ("Direct Highlight Threshold", Range(0,1)) = 0.5
		_SpecularSoftness ("Direct Highlight Softness", Range(0.001,1)) = 0.15
		[IntRange] _SpecularBands ("Direct Highlight Bands (1 = Smooth)", Range(1,4)) = 1
		_SpecularAA ("Specular Anti Aliasing", Range(0,1)) = 0.5
		_EnvironmentToonBlend ("Environment Toon Blend", Range(0,1)) = 1
		_EnvironmentThreshold ("Environment Highlight Threshold", Range(0,1)) = 0.6
		_EnvironmentSoftness ("Environment Highlight Softness", Range(0.001,1)) = 0.2
		_EnvironmentExposure ("Environment Selection Exposure", Range(0.01,8)) = 1
		_EnvironmentColorWeight ("Environment Color Weight", Range(0,1)) = 0.5
		_EnvironmentBaseColorTint ("Environment Base Color Tint", Range(0,1)) = 0
		_EnvironmentFresnelStrength ("Environment Fresnel Strength", Range(0,2)) = 1
		[Gamma] _EnvironmentColor ("Environment Tint", Color) = (1,1,1,1)
		_RimWidth ("Rim Width", Range(0,1)) = 0.3
		_RimSoftness ("Rim Softness", Range(0.001,1)) = 0.2
		_RimLightInfluence ("Rim Main Light Influence", Range(0,1)) = 0
		[Enum(Geometry,0,MainNormal,1,CombinedNormal,2)] _MatCapNormalSource ("MatCap Normal Source", Float) = 2
		[MaterialToggle] _EmissionKeepCol ("Emission Keep Base Color", Float) = 0
		[Enum(MeshNormal,0,UV4ObjectSpace,1)] _OutlineNormalSource ("Outline Normal Source", Float) = 0
		[Enum(Final,0,Body,1,DirectSpecular,2,Environment,3,MatCap,4,Rim,5,Emission,6)] _DebugView ("Lighting Debug View", Float) = 0
		_StrandDirectionMap ("Strand Direction (Linear RG)", 2D) = "gray" {}
		_StrandDirectionBlend ("Tangent / Direction Map Blend", Range(0,1)) = 0
		_StrandAngle ("Mesh Strand Angle (U=0, V=90)", Range(-180,180)) = 90
		_HairAnisotropy ("Hair Anisotropy", Range(0,1)) = 0.8
		[Enum(Off,0,Hard,1,Feather,2)] _HairFrontMode ("HairFront Mode", Float) = 0
		_HairFrontOpacity ("HairFront Opacity (1 = Opaque)", Range(0,1)) = 0.5
		_HairFeatherWidth ("HairFront Feather Width (Pixels)", Range(0,32)) = 8
		[Enum(Pixels,0,WorldUnits,1)] _HairFeatherWidthMode ("HairFront Feather Width Mode", Float) = 0
		_HairFeatherWorldWidth ("HairFront Feather Width (World Units)", Range(0,0.05)) = 0.005
		_HairFeatherThreshold ("HairFront Feather Midpoint", Range(0.05,0.95)) = 0.5
		_HairFeatherPower ("HairFront Feather Power", Range(0.5,4)) = 1
		[MaterialToggle] _HairFrontZWrite ("HairFront ZWrite", Float) = 1

		_ClearCoat ("Clearcoat Weight", Range(0,1)) = 0
		_ClearCoatRoughness ("Clearcoat Roughness", Range(0.04,1)) = 0.1
		_ClearCoatIOR ("Clearcoat IOR", Range(1,2.5)) = 1.5
		_ClearCoatMap ("Clearcoat Map (Weight/Roughness)", 2D) = "white" {}
		[Enum(Geometry,0,MainNormal,1,CombinedNormal,2,CoatNormal,3)] _ClearCoatNormalSource ("Clearcoat Normal Source", Float) = 0
		_ClearCoatNormalMap ("Clearcoat Normal Map", 2D) = "bump" {}
		_ClearCoatNormalScale ("Clearcoat Normal Scale", Range(0,2)) = 1
		_ClearCoatEnvironmentStrength ("Clearcoat Environment Strength", Range(0,2)) = 1
		_ClearCoatEnergyBlend ("Clearcoat Body Energy Blend", Range(0,1)) = 1
		[Enum(Off,0,Direct,1,Environment,2,Normal,3,Weight,4,BodyRetention,5)] _ClearCoatDebugView ("Clearcoat Debug View", Float) = 0
	}

	SubShader
	{
		Tags { "Queue" = "AlphaTest+25" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
		LOD 600

		Pass
		{
			Name "OUTLINE_BODY"
			Tags { "LightMode" = "Always" }
			Stencil { Ref 2 Comp NotEqual Pass Keep }
			Cull Off
			ZWrite On
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex vertOutline
			#pragma fragment fragOutline
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			
			#include "TomHairOutline.cginc"
			ENDCG
		}

		Pass
		{
			Name "FORWARD_BODY"
			Tags { "LightMode" = "ForwardBase" }
			Stencil { Ref 2 Comp NotEqual Pass Keep }
			Cull [_CullOption]
			ZWrite On
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex TomVert
			#pragma fragment TomHairFragBase
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile __ UNITY_SPECCUBE_BOX_PROJECTION
			#pragma multi_compile __ UNITY_SPECCUBE_BLENDING
			#define TOM_HAIR 1
			
			#include "UnityCG.cginc"
			#include "UnityStandardUtils.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			#include "TomToonInput.cginc"
			#include "TomToonLighting.cginc"
			#include "TomHairForward.cginc"
			ENDCG
		}

		Pass
		{
			Name "FORWARDADD_BODY"
			Tags { "LightMode" = "ForwardAdd" }
			Stencil { Ref 2 Comp NotEqual Pass Keep }
			Cull [_CullOption]
			ZWrite Off
			Blend One One
			ColorMask RGB
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex TomVert
			#pragma fragment TomHairFragAdd
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#define TOM_HAIR 1
			
			#include "UnityCG.cginc"
			#include "UnityStandardUtils.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			#include "TomToonInput.cginc"
			#include "TomToonLighting.cginc"
			#include "TomHairForward.cginc"
			ENDCG
		}

		Pass
		{
			Name "OUTLINE_FRONT"
			Tags { "LightMode" = "Always" }
			Stencil { Ref 2 Comp Equal Pass Keep }
			Cull Off
			ZWrite [_HairFrontZWrite]
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex vertOutline
			#pragma fragment fragOutline
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#define TOM_HAIR_FRONT 1
			#include "TomHairOutline.cginc"
			ENDCG
		}

		Pass
		{
			Name "FORWARD_FRONT"
			Tags { "LightMode" = "ForwardBase" }
			Stencil { Ref 2 Comp Equal Pass Keep }
			Cull [_CullOption]
			ZWrite [_HairFrontZWrite]
			Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex TomVert
			#pragma fragment TomHairFragBase
			#pragma multi_compile_fwdbase
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile __ UNITY_SPECCUBE_BOX_PROJECTION
			#pragma multi_compile __ UNITY_SPECCUBE_BLENDING
			#define TOM_HAIR 1
			#define TOM_HAIR_FRONT 1
			#include "UnityCG.cginc"
			#include "UnityStandardUtils.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			#include "TomToonInput.cginc"
			#include "TomToonLighting.cginc"
			#include "TomHairForward.cginc"
			ENDCG
		}

		Pass
		{
			Name "FORWARDADD_FRONT"
			Tags { "LightMode" = "ForwardAdd" }
			Stencil { Ref 2 Comp Equal Pass Keep }
			Cull [_CullOption]
			ZWrite Off
			Blend One One
			ColorMask RGB
			CGPROGRAM
			#pragma target 4.0
			#pragma vertex TomVert
			#pragma fragment TomHairFragAdd
			#pragma multi_compile_fwdadd_fullshadows
			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#define TOM_HAIR 1
			#define TOM_HAIR_FRONT 1
			#include "UnityCG.cginc"
			#include "UnityStandardUtils.cginc"
			#include "AutoLight.cginc"
			#include "Lighting.cginc"
			#include "TomToonInput.cginc"
			#include "TomToonLighting.cginc"
			#include "TomHairForward.cginc"
			ENDCG
		}

		Pass
		{
			Name "SHADOWCASTER"
			Tags { "LightMode" = "ShadowCaster" }
			Cull Off
			ZWrite On
			Offset [_ShadowOffsetFactor], [_ShadowOffsetUnits]
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vertShadow
			#pragma fragment fragShadow
			#pragma multi_compile_shadowcaster
			#pragma multi_compile_instancing
			#include "TomHairShadow.cginc"
			ENDCG
		}
	}
	Fallback Off
}
