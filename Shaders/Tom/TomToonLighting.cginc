#ifndef TOM_TOON_LIGHTING_INC
#define TOM_TOON_LIGHTING_INC

#include "TomToonBRDF.cginc"
#include "TomToonStyle.cginc"
#include "UnityGlobalIllumination.cginc"

#if defined(TOM_HAIR)
#include "TomHairBRDF.cginc"
#endif

#if defined(TOM_ALPHA)
#include "TomAlphaCommon.cginc"
#endif

struct TomMaterialData
{
#if defined(TOM_LIQUID)
	TomLiquidData liquid;
#endif
#if defined(TOM_SKIN)
	TomSkinData skin;
#endif
#if defined(TOM_EYE)
	float eyeCoverage;
	float4 eyeOverlay;
	float3 eyeDebug;
	float3 eyeInterfaceNormal;
#endif
#if defined(TOM_ALPHA)
	float coverage;
#endif
	float3 albedo;
	float3 geometricNormalWS;
	float3 shadingNormalWS;
	float3 mainNormalWS;
	float3 toonNormalWS;
	float3 matCapNormalWS;
#if !defined(TOM_HAIR)
	float3 specularNormalWS;
	float specularNormalVariance;
#endif
#if defined(TOM_HAIR)
	float3 strandWS;
	float3 environmentStrandWS;
	float3 hairNormalWS;
	float hairNormalVariance;
	float4 hairControls;
	float3 hairHeightAxisWS;
#endif
	float normalVariance;
	float4 layerMask;
	float secondaryToneMask;
	float metallic;
	float roughness;
	float occlusion;
	float3 f0;
	float specularMask;
	float reflectionMask;
	float3 emission;
};

struct TomShadowData
{
	float sampledAttenuation;
	float physicalAttenuation;
};

struct TomRawLightData
{
	float3 direction;
	float3 color;
	float distanceAttenuation;
	float cookieAttenuation;
	float sampledShadowAttenuation;
	float physicalShadowAttenuation;
};

struct TomSurfaceLightTerms
{
	float artisticShadowAttenuation;
	float diffuseShadowAttenuation;
	float shadingNdotL;
	float geometricNdotL;
	float horizon;
	float toonVisibility;
	float toonDarkWeight;
	float toonShadowedResponse;
};

struct TomLightingResult
{
	float3 diffuse;
	float3 specular;
#if defined(TOM_HAIR)
	float3 hairPrimary;
	float3 hairSecondary;
#endif
};

half4 TomVertexGI(
	TomVertexData v,
	float3 positionWS,
	half3 normalWS,
	out half3 vertexLightDiffuse)
{
	half4 ambientOrLightmapUV = 0;
	vertexLightDiffuse = 0;

#if defined(LIGHTMAP_ON)
	ambientOrLightmapUV.xy = v.uv1 * unity_LightmapST.xy + unity_LightmapST.zw;
#elif UNITY_SHOULD_SAMPLE_SH
	#if defined(VERTEXLIGHT_ON)
		vertexLightDiffuse = Shade4PointLights(
			unity_4LightPosX0,
			unity_4LightPosY0,
			unity_4LightPosZ0,
			unity_LightColor[0].rgb,
			unity_LightColor[1].rgb,
			unity_LightColor[2].rgb,
			unity_LightColor[3].rgb,
			unity_4LightAtten0,
			positionWS,
			normalWS);
	#endif
	ambientOrLightmapUV.rgb = ShadeSHPerVertex(normalWS, 0.0);
#endif

#if defined(DYNAMICLIGHTMAP_ON)
	ambientOrLightmapUV.zw = v.uv2 * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

	return ambientOrLightmapUV;
}

TomVaryings TomVert(TomVertexData v)
{
	TomVaryings o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(TomVaryings, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	o.posWS = mul(unity_ObjectToWorld, v.vertex).xyz;
	o.posCS = UnityObjectToClipPos(v.vertex);
	o.normalWS = UnityObjectToWorldNormal(v.normal);
	o.tangentWS = float4(UnityObjectToWorldDir(v.tangent.xyz), v.tangent.w);
#if defined(TOM_HAIR)
	o.tangentWS.xyz = TomHairTangent(normalize(o.normalWS), mul((float3x3)unity_ObjectToWorld, v.tangent.xyz));
	o.tangentWS.w = v.tangent.w < 0.0 ? -1.0 : 1.0;
#endif
	o.bitangentWS = normalize(cross(o.normalWS, o.tangentWS.xyz) * (v.tangent.w * unity_WorldTransformParams.w));
#if defined(TOM_HAIR)
	o.bitangentWS = cross(normalize(o.normalWS), o.tangentWS.xyz) * o.tangentWS.w * unity_WorldTransformParams.w;
#endif
	o.uv0 = v.uv0;
#if defined(TOM_EYE_IRIS)
	o.eyeOverlayUV = float4(v.uv1, v.uv2);
#endif
#if defined(TOM_SKIN)
	o.skinOverlayUV12 = float4(v.uv1, v.uv2);
	o.skinOverlayUV3Color = float4(v.uv3, v.color.r, v.color.b);
#endif
	o.ambientOrLightmapUV = TomVertexGI(
		v,
		o.posWS,
		normalize(o.normalWS),
		o.vertexLightDiffuse);

	#define pos posCS
	UNITY_TRANSFER_LIGHTING(o, v.uv1);
	#undef pos
	UNITY_TRANSFER_FOG(o, o.posCS);
	return o;
}

float TomFaceOrientation(fixed faceSign)
{
	return faceSign >= 0.0 ? 1.0 : -1.0;
}

float3 TomSafeDirection(float3 direction)
{
	// A scene without a main light may supply a zero vector.
	return direction * rsqrt(max(dot(direction, direction), 0.000001));
}

float3 TomToonNormalWS(float3 baseNormal, float3 detailNormal,
	float3 tangentWS, float3 bitangentWS, float3 geometricNormalWS)
{
	// Reuse the samples, but keep diffuse normal controls out of every specular lobe.
	float3 toonBase = lerp(float3(0, 0, 1), baseNormal, saturate(_ToonNormalInfluence));
	float3 toonCombined = BlendNormals(toonBase, detailNormal);
	float3 toonMainWS = normalize(toonBase.x * tangentWS + toonBase.y * bitangentWS + toonBase.z * geometricNormalWS);
	float3 toonCombinedWS = normalize(toonCombined.x * tangentWS + toonCombined.y * bitangentWS + toonCombined.z * geometricNormalWS);
#if defined(TOM_SKIN)
	float detailInfluence = saturate(_SkinDiffuseNormalDetail);
#else
	float detailInfluence = saturate(_ToonDetailNormalInfluence);
#endif
	return normalize(lerp(toonMainWS, toonCombinedWS, detailInfluence));
}

float3 TomLayerNormalWS(float3 baseNormal, float3 detailNormal, float mainInfluence, float detailInfluence,
	float3 tangentWS, float3 bitangentWS, float3 geometricNormalWS)
{
	float3 base = lerp(float3(0,0,1), baseNormal, saturate(mainInfluence));
	float3 detail = lerp(float3(0,0,1), detailNormal, saturate(detailInfluence));
	float3 combined = BlendNormals(base, detail);
	return normalize(combined.x * tangentWS + combined.y * bitangentWS + combined.z * geometricNormalWS);
}

float3 TomSelectMatCapNormalWS(float3 geometric, float3 mainNormal, float3 combined)
{
	if (_MatCapNormalSource < 0.5) return geometric;
	if (_MatCapNormalSource < 1.5) return mainNormal;
	return combined;
}

float3 TomMatCapNormalWS(float3 baseNormal, float3 detailNormal,
	float3 tangentWS, float3 bitangentWS, float3 geometric, float3 mainNormal, float3 combined)
{
	if (_MatCapNormalSource < 0.5) return geometric;
	if (_MatCapNormalInfluence == 1.0 && _MatCapDetailNormalInfluence == 1.0)
		return TomSelectMatCapNormalWS(geometric, mainNormal, combined);
	float detailInfluence = _MatCapNormalSource < 1.5 ? 0.0 : _MatCapDetailNormalInfluence;
	return TomLayerNormalWS(baseNormal, detailNormal, _MatCapNormalInfluence, detailInfluence,
		tangentWS, bitangentWS, geometric);
}

float3 TomNormalWS(TomVaryings i, float faceOrientation, float3 geometricNormalWS,
	out float3 mainNormalWS, out float3 toonNormalWS, out float3 matCapNormalWS
#if defined(TOM_HAIR)
	, out float3 hairNormalWS
#else
	, out float3 specularNormalWS
#endif
)
{
	float2 normalUV = i.uv0 * _NormalMap_ST.xy + _NormalMap_ST.zw;
	float2 detailUV = i.uv0 * _NormalMapDetail_ST.xy + _NormalMapDetail_ST.zw;
	float3 baseNormal = UnpackScaleNormal(SAMPLE_TEX2D(_NormalMap, normalUV), _NormalMapScale);
	float3 detailNormal = UnpackScaleNormal(SAMPLE_TEX2D_SAMPLER(_NormalMapDetail, _NormalMap, detailUV), _DetailNormalMapScale);
	float3 normalTS = BlendNormals(baseNormal, detailNormal);
	float3 tangentWS = normalize(i.tangentWS.xyz);
	float3 bitangentWS = normalize(i.bitangentWS) * faceOrientation;
	mainNormalWS = normalize(baseNormal.x * tangentWS + baseNormal.y * bitangentWS + baseNormal.z * geometricNormalWS);
	toonNormalWS = TomToonNormalWS(baseNormal, detailNormal, tangentWS, bitangentWS, geometricNormalWS);
	float3 normalWS = normalize(
		normalTS.x * tangentWS
		+ normalTS.y * bitangentWS
		+ normalTS.z * geometricNormalWS);
	matCapNormalWS = TomMatCapNormalWS(baseNormal, detailNormal, tangentWS, bitangentWS,
		geometricNormalWS, mainNormalWS, normalWS);
#if defined(TOM_HAIR)
	hairNormalWS = normalWS;
	UNITY_BRANCH
	if (_HairSpecularNormalInfluence != 1.0 || _HairSpecularDetailNormalInfluence != 1.0)
	{
		hairNormalWS = TomLayerNormalWS(baseNormal, detailNormal,
			_HairSpecularNormalInfluence, _HairSpecularDetailNormalInfluence,
			tangentWS, bitangentWS, geometricNormalWS);
	}
#else
	specularNormalWS = normalWS;
	UNITY_BRANCH
	if (_SpecularNormalInfluence != 1.0 || _SpecularDetailNormalInfluence != 1.0)
		specularNormalWS = TomLayerNormalWS(baseNormal, detailNormal,
			_SpecularNormalInfluence, _SpecularDetailNormalInfluence,
			tangentWS, bitangentWS, geometricNormalWS);
#endif
	return normalWS;
}

float TomSurfaceAlpha(TomVaryings i, float mainAlpha)
{
	float2 alphaUV = i.uv0 * _AlphaMask_ST.xy + _AlphaMask_ST.zw;
	return mainAlpha * SAMPLE_TEX2D(_AlphaMask, alphaUV).r;
}

#include "TomClearcoat.cginc"

#if defined(TOM_LIQUID)
#include "TomLiquid.cginc"
#endif

#if defined(TOM_SKIN)
#include "TomSkinSurface.cginc"
#endif

#if defined(TOM_EYE)
#include "TomEyeCoverage.cginc"
#include "TomEyeOptics.cginc"
#endif

TomMaterialData TomGetDirectMaterialData(TomVaryings i, fixed faceSign)
{
	TomMaterialData material;
#if defined(TOM_LIQUID)
	UNITY_INITIALIZE_OUTPUT(TomMaterialData, material);
#endif
#if defined(TOM_EYE)
	UNITY_INITIALIZE_OUTPUT(TomMaterialData, material);
	TomEyeLayers eye = TomEyeSource(i);
	float4 mainTex = eye.iris;
	material.eyeCoverage = eye.coverage;
	material.eyeOverlay = eye.overlay;
	TomEyeClipColor(eye.coverage);
#else
	float4 mainTex = SAMPLE_TEX2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw);
#if defined(TOM_SKIN)
	TomSkinClip(i.uv0, mainTex.a);
#elif defined(TOM_ALPHA)
	material.coverage = saturate(TomSurfaceAlpha(i, mainTex.a) * _Alpha);
	TomAlphaClipColor(material.coverage);
#else
	clip(TomSurfaceAlpha(i, mainTex.a) - _Cutoff);
#endif
#endif
	float faceOrientation = TomFaceOrientation(faceSign);
	material.albedo = mainTex.rgb * _BaseColor.rgb;
#if defined(TOM_HAIR)
	material.albedo *= TomHairColor(i);
#endif
#if defined(TOM_SKIN)
	material.albedo = TomSkinColor(i, mainTex.rgb) * _BaseColor.rgb;
#endif
	material.geometricNormalWS = normalize(i.normalWS) * faceOrientation;
	material.shadingNormalWS = TomNormalWS(i, faceOrientation, material.geometricNormalWS,
		material.mainNormalWS, material.toonNormalWS, material.matCapNormalWS
#if defined(TOM_HAIR)
		, material.hairNormalWS
#else
		, material.specularNormalWS
#endif
	);
#if defined(TOM_SKIN)
	TomSkinApplySurface(i, faceOrientation, material);
#elif defined(TOM_LIQUID)
	TomLiquidApplySurface(i, faceOrientation, material);
#endif
#if defined(TOM_EYE)
	material.eyeInterfaceNormal = TomResolveCoatNormal(i, material.geometricNormalWS,
		material.mainNormalWS, material.shadingNormalWS, faceOrientation);
#endif
#if defined(TOM_HAIR)
	float3 sourceStrand;
	material.strandWS = TomHairStrand(i, material.geometricNormalWS, material.hairNormalWS, faceOrientation, sourceStrand);
	material.environmentStrandWS = TomHairTangent(material.shadingNormalWS, sourceStrand);
	material.hairControls = TomHairControls(i);
	material.hairHeightAxisWS = 0.0;
	UNITY_BRANCH
	if (_HairPrimaryHeight != 0.0 || _HairSecondaryHeight != 0.0)
		material.hairHeightAxisWS = TomHairHeightAxis(i, material.hairNormalWS);
	float3 hdx = ddx(material.hairNormalWS), hdy = ddy(material.hairNormalWS);
	material.hairNormalVariance = 0.5 * (dot(hdx, hdx) + dot(hdy, hdy));
#endif
	float3 dx = ddx(material.shadingNormalWS);
	float3 dy = ddy(material.shadingNormalWS);
	material.normalVariance = 0.5 * (dot(dx, dx) + dot(dy, dy));
#if !defined(TOM_HAIR)
	material.specularNormalVariance = material.normalVariance;
	UNITY_BRANCH
	if (_SpecularNormalInfluence != 1.0 || _SpecularDetailNormalInfluence != 1.0)
	{
		float3 sx = ddx(material.specularNormalWS), sy = ddy(material.specularNormalWS);
		material.specularNormalVariance = 0.5 * (dot(sx, sx) + dot(sy, sy));
	}
#endif
#if defined(TOM_EYE)
	// Use surface-normal derivatives for AA, not derivatives of the derivative-built optical hit.
	TomEyeApplyOptics(i, eye, faceOrientation, material);
#endif
	float metallicMap;
	float roughnessMap;
	material.occlusion = 1.0;
	UNITY_BRANCH
	if (_UsePackedMaterialMap > 0.5)
	{
		float4 packed = SAMPLE_TEX2D_SAMPLER(_MaterialMap, _MainTex, i.uv0 * _MaterialMap_ST.xy + _MaterialMap_ST.zw);
		metallicMap = packed.r;
		roughnessMap = packed.g;
		material.occlusion = lerp(1.0, saturate(packed.b), saturate(_OcclusionStrength));
		material.specularMask = packed.a;
	}
	else
	{
		metallicMap = SAMPLE_TEX2D(_MetallicMap, i.uv0 * _MetallicMap_ST.xy + _MetallicMap_ST.zw).r;
		roughnessMap = SAMPLE_TEX2D(_RoughnessMap, i.uv0 * _RoughnessMap_ST.xy + _RoughnessMap_ST.zw).r;
		material.specularMask = SAMPLE_TEX2D(_SpecularMask, i.uv0 * _SpecularMask_ST.xy + _SpecularMask_ST.zw).r;
	}
	material.metallic = saturate(_Metallic * metallicMap);
	material.roughness = max(saturate(_Roughness * roughnessMap + _RoughnessBias), 0.04);
	float dielectricF0 = TomIorToF0(_SpecularIOR);
	material.f0 = lerp(dielectricF0.xxx, material.albedo, material.metallic);
	material.layerMask = 1.0;
	float reflectionMode = floor(_ReflectionMode + 0.5);
	bool matcapMultiply = (reflectionMode == 1.0 || reflectionMode == 3.0)
		&& _MatCapIntensity > 0.0 && _MatCapColor.a > 0.0 && saturate(_MatCapBlendMode) < 1.0;
	bool needsLayers = _SecondaryToneStrength > 0.0 || matcapMultiply;
#if defined(UNITY_PASS_FORWARDBASE)
	needsLayers = needsLayers || _RimStrength > 0.0 || reflectionMode > 0.0;
#endif
	UNITY_BRANCH
	if (needsLayers)
		material.layerMask = SAMPLE_TEX2D_SAMPLER(_LayerMask, _MainTex, i.uv0 * _LayerMask_ST.xy + _LayerMask_ST.zw);
	material.secondaryToneMask = material.layerMask.r;
	material.reflectionMask = 0.0;
	bool needsReflectionMask = matcapMultiply;
#if defined(UNITY_PASS_FORWARDBASE)
	needsReflectionMask = reflectionMode > 0.0;
#endif
	UNITY_BRANCH
	if (needsReflectionMask)
		material.reflectionMask = SAMPLE_TEX2D(_ReflectionMask, i.uv0 * _ReflectionMask_ST.xy + _ReflectionMask_ST.zw).r;
	material.emission = 0.0;
#if defined(TOM_SKIN)
	TomSkinFinishMaterial(i, material);
#endif
	return material;
}

TomMaterialData TomGetBaseMaterialData(TomVaryings i, fixed faceSign)
{
	TomMaterialData material = TomGetDirectMaterialData(i, faceSign);
	UNITY_BRANCH
	if (_UsePackedMaterialMap < 0.5 && _OcclusionStrength > 0.0)
	{
		float occlusionMap = SAMPLE_TEX2D(_OcclusionMap, i.uv0 * _OcclusionMap_ST.xy + _OcclusionMap_ST.zw).r;
		material.occlusion = lerp(1.0, saturate(occlusionMap), saturate(_OcclusionStrength));
	}
	UNITY_BRANCH
	if (_EmissionIntensity > 0.0)
	{
		float3 emissionMask = SAMPLE_TEX2D(_EmissionMask, i.uv0 * _EmissionMask_ST.xy + _EmissionMask_ST.zw).rgb;
		material.emission = emissionMask * _EmissionColor.rgb * _EmissionIntensity
			* lerp(1.0.xxx, material.albedo, saturate(_EmissionKeepCol));
	}
	return material;
}

float TomSampleRamp(float value, float secondaryMask, float aa)
{
	float width = max(_ToonSoftness, fwidth(value) * max(aa, 0.0));
	float ramp = lerp(_ToonShadeLevel, 1.0, TomSoftBand(value, _ToonThreshold, width));
	UNITY_BRANCH
	if (_RampMode > 0.5)
	{
		float2 uv = float2(saturate(value), 0.5) * _RampTex_ST.xy + _RampTex_ST.zw;
		ramp = SAMPLE_TEX2D(_RampTex, uv).r;
	}
	UNITY_BRANCH
	if (_SecondaryToneStrength > 0.0 && secondaryMask > 0.0)
	{
		float2 uv = float2(saturate(value), 0.5) * _SecondaryRamp_ST.xy + _SecondaryRamp_ST.zw;
		float secondary = SAMPLE_TEX2D_SAMPLER(_SecondaryRamp, _MainTex, uv).r;
		ramp = lerp(ramp, secondary, saturate(_SecondaryToneStrength * secondaryMask));
	}
	return ramp;
}

float TomShapeShadow(float physicalShadow)
{
	physicalShadow = saturate(physicalShadow);
	float halfSoftness = max(_ShadowSoftness, 0.001) * 0.5;
	float stylizedShadow = smoothstep(
		_ShadowThreshold - halfSoftness,
		_ShadowThreshold + halfSoftness,
		physicalShadow);
	return lerp(physicalShadow, stylizedShadow, saturate(_ShadowRemapStrength));
}

void TomSampleLightDistanceAndCookie(
	float3 positionWS,
	out float distanceAttenuation,
	out float cookieAttenuation)
{
	distanceAttenuation = 1.0;
	cookieAttenuation = 1.0;

#if defined(POINT)
	float3 lightCoord = mul(unity_WorldToLight, float4(positionWS, 1.0)).xyz;
	distanceAttenuation = tex2D(_LightTexture0, dot(lightCoord, lightCoord).rr).r;
#elif defined(SPOT)
	float4 lightCoord = mul(unity_WorldToLight, float4(positionWS, 1.0));
	distanceAttenuation = UnitySpotAttenuate(lightCoord.xyz);
	cookieAttenuation = (lightCoord.z > 0.0) * UnitySpotCookie(lightCoord);
#elif defined(POINT_COOKIE)
	float3 lightCoord = mul(unity_WorldToLight, float4(positionWS, 1.0)).xyz;
	distanceAttenuation = tex2D(_LightTextureB0, dot(lightCoord, lightCoord).rr).r;
	cookieAttenuation = texCUBE(_LightTexture0, lightCoord).w;
#elif defined(DIRECTIONAL_COOKIE)
	float2 lightCoord = mul(unity_WorldToLight, float4(positionWS, 1.0)).xy;
	cookieAttenuation = tex2D(_LightTexture0, lightCoord).w;
#endif

	distanceAttenuation = saturate(distanceAttenuation);
	cookieAttenuation = saturate(cookieAttenuation);
}

TomShadowData TomGetShadowData(TomVaryings i)
{
	TomShadowData shadow;
	shadow.sampledAttenuation = saturate(UNITY_SHADOW_ATTENUATION(i, i.posWS));
	shadow.physicalAttenuation = shadow.sampledAttenuation;

#if defined(HANDLE_SHADOWS_BLENDING_IN_GI)
	half bakedAttenuation = UnitySampleBakedOcclusion(i.ambientOrLightmapUV.xy, i.posWS);
	float zDistance = dot(_WorldSpaceCameraPos - i.posWS, UNITY_MATRIX_V[2].xyz);
	float fadeDistance = UnityComputeShadowFadeDistance(i.posWS, zDistance);
	shadow.physicalAttenuation = UnityMixRealtimeAndBakedShadows(
		shadow.sampledAttenuation,
		bakedAttenuation,
		UnityComputeShadowFade(fadeDistance));
#endif

	shadow.physicalAttenuation = saturate(shadow.physicalAttenuation);
	return shadow;
}

TomRawLightData TomBuildRawLightData(
	float3 lightDirection,
	float3 lightColor,
	float distanceAttenuation,
	float cookieAttenuation,
	TomShadowData shadow,
	float intensity)
{
	TomRawLightData light;
	light.direction = TomSafeDirection(lightDirection);
	light.color = lightColor * intensity;
	light.distanceAttenuation = distanceAttenuation;
	light.cookieAttenuation = cookieAttenuation;
	light.sampledShadowAttenuation = shadow.sampledAttenuation;
	light.physicalShadowAttenuation = shadow.physicalAttenuation;
	return light;
}

#if defined(TOM_SKIN)
#include "TomSkinLighting.cginc"
#endif

TomSurfaceLightTerms TomBuildSurfaceLightTerms(
	TomMaterialData material,
	TomRawLightData light)
{
	TomSurfaceLightTerms surface;
	surface.artisticShadowAttenuation = TomShapeShadow(light.physicalShadowAttenuation);
	surface.diffuseShadowAttenuation = lerp(
		1.0,
		surface.artisticShadowAttenuation,
		saturate(_ShadowStrength));

	surface.shadingNdotL = saturate(dot(material.shadingNormalWS, light.direction));
	surface.geometricNdotL = saturate(dot(material.geometricNormalWS, light.direction));
	float horizonFade = max(_NormalHorizonFade, 0.001);
	float shadingHorizon = smoothstep(0.0, horizonFade, surface.shadingNdotL);
	float geometricHorizon = smoothstep(0.0, horizonFade, surface.geometricNdotL);
	surface.horizon = shadingHorizon * geometricHorizon;

	float continuous = surface.shadingNdotL * surface.horizon;
	float toonNL = saturate(dot(material.toonNormalWS, light.direction));
#if defined(TOM_SKIN)
	continuous = TomSkinContinuousVisibility(material, light.direction);
	toonNL = TomSkinDiffuseNL(material, material.toonNormalWS, light.direction);
#endif
	float rampInput = toonNL * lerp(
		1.0,
		surface.diffuseShadowAttenuation,
		saturate(_UseRampForShadows));
	float ramp = light.physicalShadowAttenuation;
	UNITY_BRANCH
	if (_UseRamp > 0.0)
		ramp = TomSampleRamp(rampInput, material.secondaryToneMask, _ToonAA);
	// A single diffuse macro-horizon, using Toon softness rather than the specular guard.
	float geometricNL = dot(material.geometricNormalWS, light.direction);
	float toonWidth = max(max(_ToonSoftness, 0.001), fwidth(geometricNL) * max(_ToonAA, 0.0));
	float toonHorizon = smoothstep(0.0, toonWidth, geometricNL);
	float toonResponse = ramp * toonHorizon;
#if defined(TOM_SKIN)
	toonResponse = TomSkinSDFResponse(material, light.direction, surface.diffuseShadowAttenuation, toonResponse);
#endif
	surface.toonDarkWeight = saturate(1.0 - toonResponse) * saturate(_UseRamp);
	float toonNdotL = lerp(continuous, toonResponse, saturate(_UseRamp));
	float shadowOutsideRamp = lerp(
		surface.diffuseShadowAttenuation,
		1.0,
		saturate(_UseRampForShadows) * saturate(_UseRamp));
	surface.toonShadowedResponse = saturate(toonResponse
		* min(shadowOutsideRamp, surface.diffuseShadowAttenuation));
	surface.toonVisibility = toonNdotL
		* light.distanceAttenuation
		* light.cookieAttenuation
		* min(shadowOutsideRamp, surface.diffuseShadowAttenuation);
	return surface;
}

float3 TomEvaluateToonDarkFill(TomMaterialData material, TomRawLightData light,
	TomSurfaceLightTerms surface, float3 viewDir)
{
	float3 fresnel = TomFresnelSchlick(saturate(dot(material.shadingNormalWS, viewDir)), material.f0, _FresnelStrength);
	return material.albedo * TomDiffuseWeight(fresnel, material.metallic)
#if defined(TOM_SKIN)
		* material.skin.diffuseTint
#endif
		* max(_ToonShadeColor.rgb, 0.0) * saturate(_ToonShadeColor.a)
		* surface.toonDarkWeight * light.color * light.distanceAttenuation
		* light.cookieAttenuation * light.physicalShadowAttenuation;
}

float3 TomEvaluateToonMinimum(TomMaterialData material, TomRawLightData light,
	TomSurfaceLightTerms surface, float3 viewDir)
{
	// Remap the shadowed Toon response, not light intensity or final pixel RGB.
	float lift = saturate(_ToonMinLighting) * saturate(_UseRamp)
		* (1.0 - surface.toonShadowedResponse);
	float3 fresnel = TomFresnelSchlick(saturate(dot(material.shadingNormalWS, viewDir)), material.f0, _FresnelStrength);
	return material.albedo * TomDiffuseWeight(fresnel, material.metallic)
#if defined(TOM_SKIN)
		* material.skin.diffuseTint
#endif
		* lift * light.color * light.distanceAttenuation * light.cookieAttenuation;
}

TomLightingResult TomEvaluateDirectLight(
	TomMaterialData material,
	TomRawLightData light,
	TomSurfaceLightTerms surface,
	float3 viewDir)
{
	TomLightingResult result;
	float3 halfVector = light.direction + viewDir;
	float halfLengthSq = dot(halfVector, halfVector);
	float halfIsValid = step(0.000001, halfLengthSq);
	float3 halfDir = halfVector * rsqrt(max(halfLengthSq, 0.000001));
	float3 specularNormal = material.shadingNormalWS;
	float specularVariance = material.normalVariance;
	float specularHorizon = surface.horizon;
	float nDotL = surface.shadingNdotL;
#if !defined(TOM_HAIR)
	specularNormal = material.specularNormalWS;
	specularVariance = material.specularNormalVariance;
	UNITY_BRANCH
	if (_SpecularNormalInfluence != 1.0 || _SpecularDetailNormalInfluence != 1.0)
	{
		nDotL = saturate(dot(specularNormal, light.direction));
		float fade = max(_NormalHorizonFade, 0.001);
		specularHorizon = smoothstep(0.0, fade, nDotL) * smoothstep(0.0, fade, surface.geometricNdotL);
	}
#endif
	float nDotV = saturate(dot(specularNormal, viewDir));
	float nDotH = saturate(dot(specularNormal, halfDir)) * halfIsValid;
	float vDotH = saturate(dot(viewDir, halfDir)) * halfIsValid;
	float3 fresnel = TomFresnelSchlick(vDotH, material.f0, _FresnelStrength);
	result.diffuse = material.albedo * TomDiffuseWeight(fresnel, material.metallic)
		* light.color * surface.toonVisibility;
#if defined(TOM_SKIN)
	result.diffuse *= material.skin.diffuseTint * TomSkinWarmTint(material, light.direction);
#endif
	result.specular = 0.0;
	// Derivatives are evaluated outside per-pixel visibility branches.
#if defined(TOM_HAIR)
	result.hairPrimary = 0.0;
	result.hairSecondary = 0.0;
	float3 primaryN = material.hairNormalWS, primaryT = material.strandWS;
	float primaryShift = _HairHighlightShift + (material.hairControls.r * 2.0 - 1.0) * _HairPrimaryShiftMapScale;
	TomHairShiftFrame(primaryN, primaryT, primaryShift);
	TomHairHeightFrame(primaryN, primaryT, material.hairNormalWS, material.hairHeightAxisWS, _HairPrimaryHeight);
	float primaryVariance = material.hairNormalVariance;
	if (_HairHighlightShift != 0.0 || _HairPrimaryShiftMapScale > 0.0
		|| _HairPrimaryHeight != 0.0)
		primaryVariance = TomHairFrameVariance(primaryN);
	float2 primaryWidth = float2(_HairPrimaryLongitudinalWidth, _HairPrimaryAzimuthalWidth);
	float toonLobe = TomHairToonLobe(primaryN, primaryT, halfDir, primaryVariance,
		_SpecularSize, _SpecularThreshold, _SpecularSoftness, primaryWidth);
	float secondaryShare = saturate(_HairSecondaryWeight);
	float3 secondaryN = material.hairNormalWS, secondaryT = material.strandWS;
	float2 secondaryWidth = float2(_HairSecondaryLongitudinalWidth, _HairSecondaryAzimuthalWidth);
	float secondaryLobe = 0.0;
	float secondaryVariance = material.hairNormalVariance;
	// Material-uniform branch: derivatives never enter per-fragment visibility branches.
	UNITY_BRANCH
	if (secondaryShare > 0.0)
	{
		float shift = _HairSecondaryShift + (material.hairControls.g * 2.0 - 1.0) * _HairSecondaryShiftMapScale;
		TomHairShiftFrame(secondaryN, secondaryT, shift);
		TomHairHeightFrame(secondaryN, secondaryT, material.hairNormalWS, material.hairHeightAxisWS, _HairSecondaryHeight);
		secondaryVariance = TomHairFrameVariance(secondaryN);
		secondaryLobe = TomHairToonLobe(secondaryN, secondaryT, halfDir, secondaryVariance,
			_HairSecondarySize, _HairSecondaryThreshold, _HairSecondarySoftness, secondaryWidth);
	}
	nDotV = saturate(dot(material.hairNormalWS, viewDir));
	nDotL = saturate(dot(material.hairNormalWS, light.direction));
	UNITY_BRANCH
	if (_HairSpecularNormalInfluence != 1.0 || _HairSpecularDetailNormalInfluence != 1.0)
	{
		float fade = max(_NormalHorizonFade, 0.001);
		specularHorizon = smoothstep(0.0, fade, nDotL) * smoothstep(0.0, fade, surface.geometricNdotL);
	}
#else
	float toonLobe = TomStylizedGGXLobe(nDotH, specularVariance);
#endif
	UNITY_BRANCH
	if (_SpecularStrength > 0.0 && halfIsValid > 0.5 && specularHorizon > 0.0
		&& nDotV > 0.00001 && nDotL > 0.00001)
	{
		float3 specular = 0.0;
#if defined(TOM_HAIR)
		float3 primary = TomHairLobe(primaryN, primaryT, light.direction, viewDir, halfDir,
			TomFilteredRoughness(material.roughness, primaryVariance), fresnel,
			primaryWidth, _SpecularToonBlend, toonLobe);
		float3 secondary = 0.0;
		UNITY_BRANCH
		if (secondaryShare > 0.0)
		{
			float secondaryRoughness = TomFilteredRoughness(max(saturate(material.roughness + _HairSecondaryRoughnessOffset), 0.04), secondaryVariance);
			secondary = TomHairLobe(secondaryN, secondaryT, light.direction, viewDir, halfDir,
				secondaryRoughness, fresnel, secondaryWidth, _HairSecondaryToonBlend, secondaryLobe);
			secondary *= _HairSecondaryColor.rgb * lerp(1.0.xxx, material.albedo, saturate(_HairSecondaryBaseColorTint));
		}
		float shadow = lerp(1.0, surface.artisticShadowAttenuation, saturate(_SpecularShadowStrength));
		float3 common = _SpecularColor.rgb * specularHorizon * material.specularMask * _SpecularStrength
			* light.color * light.distanceAttenuation * light.cookieAttenuation * shadow;
		result.hairPrimary = primary * ((1.0 - secondaryShare) * material.hairControls.b) * common;
		result.hairSecondary = secondary * (secondaryShare * material.hairControls.a) * common;
		result.specular = result.hairPrimary + result.hairSecondary;
#else
		UNITY_BRANCH
		if (_SpecularToonBlend < 0.999)
			specular = TomEvaluateProductionGGX(nDotV, nDotL, nDotH, vDotH,
			TomFilteredRoughness(material.roughness, specularVariance), material.f0, _FresnelStrength);
		float3 shaped = toonLobe * fresnel * nDotL;
		specular = lerp(specular, shaped, saturate(_SpecularToonBlend));
		float shadow = lerp(1.0, surface.artisticShadowAttenuation, saturate(_SpecularShadowStrength));
		result.specular = specular * _SpecularColor.rgb * specularHorizon
			* material.specularMask * _SpecularStrength * light.color
			* light.distanceAttenuation * light.cookieAttenuation * shadow;
#endif
	}
	return result;
}

UnityGIInput TomBuildGIInput(
	TomVaryings i,
	TomMaterialData material,
	float3 viewDir,
	float3 lightDir,
	float engineAttenuation)
{
	UnityGIInput data;
	data.light.color = _LightColor0.rgb;
	data.light.dir = lightDir;
	data.light.ndotl = saturate(dot(material.shadingNormalWS, lightDir));
	data.worldPos = i.posWS;
	data.worldViewDir = viewDir;
	data.atten = engineAttenuation;

#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
	data.ambient = 0;
	data.lightmapUV = i.ambientOrLightmapUV;
#else
	data.ambient = i.ambientOrLightmapUV.rgb;
	data.lightmapUV = 0;
#endif

	data.probeHDR[0] = unity_SpecCube0_HDR;
	data.probeHDR[1] = unity_SpecCube1_HDR;
#if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
	data.boxMin[0] = unity_SpecCube0_BoxMin;
#endif
#if defined(UNITY_SPECCUBE_BOX_PROJECTION)
	data.boxMax[0] = unity_SpecCube0_BoxMax;
	data.probePosition[0] = unity_SpecCube0_ProbePosition;
	data.boxMax[1] = unity_SpecCube1_BoxMax;
	data.boxMin[1] = unity_SpecCube1_BoxMin;
	data.probePosition[1] = unity_SpecCube1_ProbePosition;
#endif
	return data;
}

float3 TomSHAtlasBlockUV(float3 volumeUV, float blockIndex)
{
	float3 grid = max(_TomSHVolumeGrid.xyz, 2.0);
	float atlasWidth = max(_TomSHVolumeGrid.w, grid.x * 7.0);
	return float3(
		(blockIndex * grid.x + 0.5 + volumeUV.x * (grid.x - 1.0)) / atlasWidth,
		(0.5 + volumeUV.y * (grid.y - 1.0)) / grid.y,
		(0.5 + volumeUV.z * (grid.z - 1.0)) / grid.z);
}

float4 TomEvaluateCustomSHVolume(float3 positionWS, float3 normalWS)
{
	float4 result = float4(0.0, 0.0, 0.0, 0.0);

	UNITY_BRANCH
	if (_TomSHVolumeParams.x >= 0.5)
	{
		float3 localPosition = mul(_TomSHWorldToLocal, float4(positionWS, 1.0)).xyz;
		float3 volumeUV = (localPosition - _TomSHBoundsMin.xyz) * _TomSHBoundsInvSize.xyz;
		float3 inside = step(0.0, volumeUV) * step(volumeUV, 1.0);
		float insideWeight = inside.x * inside.y * inside.z;
		float3 edgeDistance3 = min(volumeUV, 1.0 - volumeUV);
		float edgeDistance = min(edgeDistance3.x, min(edgeDistance3.y, edgeDistance3.z));
		float edgeFade = max(_TomSHVolumeParams.z, 0.0);
		float smoothWeight = smoothstep(0.0, max(edgeFade, 0.0001), edgeDistance);
		float hardWeight = 1.0 - step(0.0001, edgeFade);
		float weight = insideWeight * lerp(smoothWeight, 1.0, hardWeight);

		UNITY_BRANCH
		if (weight > 0.0)
		{
			float4 shAr = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 0.0));
			float4 shAg = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 1.0));
			float4 shAb = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 2.0));
			float4 shBr = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 3.0));
			float4 shBg = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 4.0));
			float4 shBb = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 5.0));
			float3 shC = TOM_SAMPLE_SH(TomSHAtlasBlockUV(volumeUV, 6.0)).rgb;

			float4 normal4 = float4(normalWS, 1.0);
			float4 quadratic = normalWS.xyzz * normalWS.yzzx;
			float3 irradiance = 0.0;
			irradiance.r = dot(shAr, normal4) + dot(shBr, quadratic);
			irradiance.g = dot(shAg, normal4) + dot(shBg, quadratic);
			irradiance.b = dot(shAb, normal4) + dot(shBb, quadratic);
			irradiance += shC * (normalWS.x * normalWS.x - normalWS.y * normalWS.y);
			result = float4(max(irradiance, 0.0) * _TomSHVolumeParams.y, weight);
		}
	}

	return result;
}

float3 TomEvaluateIndirectDiffuse(
	TomVaryings i,
	TomMaterialData material,
	float3 viewDir,
	UnityGI gi)
{
#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
	float3 irradiance = max(gi.indirect.diffuse, 0.0);
#else
	float3 irradiance = lerp(
		_AmbientColor.rgb,
		max(gi.indirect.diffuse, 0.0),
		saturate(_LightProbeBlend));
	float4 customSH = TomEvaluateCustomSHVolume(
		i.posWS,
#if defined(TOM_SKIN)
		material.skin.diffuseNormalWS);
#else
		material.shadingNormalWS);
#endif
	irradiance = lerp(
		irradiance,
		customSH.rgb,
		saturate(_CustomSHVolumeBlend) * customSH.a);
#endif

	UNITY_BRANCH
	if (_IndirectToonBlend > 0.0)
	{
		float luma = TomLuminance(irradiance);
		float tone = lerp(_IndirectShadeLevel, 1.0,
			TomSoftBand(luma, _IndirectToonThreshold, _IndirectToonSoftness));
		float3 toned = irradiance / max(luma, 0.0001) * tone * saturate(luma / 0.05);
		irradiance = lerp(irradiance, toned, saturate(_IndirectToonBlend));
	}
	float nDotV = saturate(dot(material.shadingNormalWS, viewDir));
	float3 fresnel = TomFresnelSchlick(nDotV, material.f0, _FresnelStrength);
	float3 diffuseWeight = TomDiffuseWeight(fresnel, material.metallic);
	return material.albedo
#if defined(TOM_SKIN)
		* material.skin.diffuseTint
#endif
		* diffuseWeight
		* irradiance
		* material.occlusion
		* _IndirectDiffuseIntensity;
}

float3 TomEvaluateVertexLightDiffuse(
	TomMaterialData material,
	float3 viewDir,
	float3 vertexLightDiffuse)
{
#if defined(VERTEXLIGHT_ON) && !defined(LIGHTMAP_ON)
	float nDotV = saturate(dot(material.geometricNormalWS, viewDir));
	float3 fresnel = TomFresnelSchlick(nDotV, material.f0, _FresnelStrength);
	float3 diffuseWeight = TomDiffuseWeight(fresnel, material.metallic);
	return material.albedo
#if defined(TOM_SKIN)
		* material.skin.diffuseTint
#endif
		* diffuseWeight
		* max(vertexLightDiffuse, 0.0)
		* max(_VertexLightIntensity, 0.0);
#else
	return 0.0;
#endif
}

float TomGetMainDirectOwnership()
{
	// Static Subtractive lightmaps already contain the main light's direct term.
#if defined(LIGHTMAP_ON) && defined(LIGHTMAP_SHADOW_MIXING) && !defined(SHADOWS_SHADOWMASK)
	return 0.0;
#else
	return 1.0;
#endif
}

float3 TomEvaluateProbeSpecular(
	UnityGIInput giInput,
	TomMaterialData material,
	float3 viewDir)
{
	float perceptualRoughness = lerp(
		saturate(_EnvironmentRoughness),
		material.roughness,
		saturate(_UseMaterialRoughnessForEnvironment));
	float smoothness = 1.0 - perceptualRoughness;
	float3 probeNormal = material.shadingNormalWS;
#if defined(TOM_HAIR)
	probeNormal = TomHairEnvironmentNormal(probeNormal, material.environmentStrandWS, viewDir);
#endif
	float3 environment = TomSampleProbe(giInput, viewDir, probeNormal, perceptualRoughness, material.f0);

	float realRoughness = PerceptualRoughnessToRoughness(perceptualRoughness);
#if defined(UNITY_COLORSPACE_GAMMA)
	float surfaceReduction = 1.0 - 0.28 * realRoughness * perceptualRoughness;
#else
	float surfaceReduction = 1.0 / (realRoughness * realRoughness + 1.0);
#endif

	float reflectivity = max(max(material.f0.r, material.f0.g), material.f0.b);
	float grazingTerm = saturate(smoothness + reflectivity);
	float nDotV = saturate(dot(material.shadingNormalWS, viewDir));
	float3 unityFresnel = FresnelLerp(material.f0, grazingTerm.xxx, nDotV);
	float3 fresnel = saturate(lerp(material.f0, unityFresnel, _EnvironmentFresnelStrength));
	float specularOcclusion = lerp(
		1.0,
		material.occlusion,
		saturate(_SpecularOcclusionStrength));
	float3 shaped = TomShapeEnvironment(environment);
	float3 reflected = lerp(environment * surfaceReduction, shaped, saturate(_EnvironmentToonBlend));
	return reflected * fresnel * specularOcclusion * _EnvironmentColor.rgb
#if defined(TOM_SKIN)
		* material.skin.gloss
#endif
		* lerp(1.0.xxx, material.albedo, saturate(_EnvironmentBaseColorTint));
}

float3 TomSampleMatCap(TomVaryings i, TomMaterialData material)
{
	float3 normalWS = material.matCapNormalWS;
	float2 uv = mul((float3x3)UNITY_MATRIX_V, normalWS).xy * 0.5 + 0.5;
	uv = uv * _MatCap_ST.xy + _MatCap_ST.zw;
	return SAMPLE_TEX2D_LOD(_MatCap, float4(uv, 0, 0), _MatCapMip).rgb * _MatCapColor.rgb;
}

float3 TomMatCapSampleForPass(TomVaryings i, TomMaterialData material)
{
	float mode = floor(_ReflectionMode + 0.5);
	float3 result = 0.0;
	bool needsSample = (mode == 1.0 || mode == 3.0) && _MatCapIntensity > 0.0 && _MatCapColor.a > 0.0
		&& material.reflectionMask * material.layerMask.b != 0.0;
#if defined(UNITY_PASS_FORWARDADD)
	needsSample = needsSample && saturate(_MatCapBlendMode) < 1.0;
#endif
	UNITY_BRANCH
	if (needsSample) result = TomSampleMatCap(i, material);
	return result;
}

float3 TomMatCapMultiplier(float3 matcapRGB, TomMaterialData material)
{
	float3 result = 1.0;
	float mode = floor(_ReflectionMode + 0.5);
	UNITY_BRANCH
	if ((mode == 1.0 || mode == 3.0) && _MatCapIntensity > 0.0 && _MatCapColor.a > 0.0 && saturate(_MatCapBlendMode) < 1.0)
	{
		float amount = saturate(material.reflectionMask * material.layerMask.b * _MatCapIntensity);
		float weight = (1.0 - saturate(_MatCapBlendMode)) * saturate(_MatCapColor.a);
		// Retain the old saturated intensity before blending the whole layer.
		float3 endpoint = lerp(1.0.xxx, matcapRGB * 2.0, amount);
		result = weight == 1.0 ? endpoint : lerp(1.0.xxx, endpoint, weight);
	}
	return result;
}

float3 TomEnvironmentLayer(UnityGIInput giInput, TomMaterialData material, float3 viewDir, float mainShadow)
{
	float3 result = 0.0;
	float mode = floor(_ReflectionMode + 0.5);
	UNITY_BRANCH
	if ((mode == 2.0 || mode == 3.0) && _EnvironmentIntensity > 0.0)
		result = TomEvaluateProbeSpecular(giInput, material, viewDir) * material.reflectionMask
			* material.layerMask.a * _EnvironmentIntensity
			* lerp(1.0, mainShadow, saturate(_ReflectionShadowStrength));
	return result;
}

float3 TomMatCapAddLayer(float3 matcapRGB, TomMaterialData material, float mainShadow)
{
	float3 result = 0.0;
	float mode = floor(_ReflectionMode + 0.5);
	UNITY_BRANCH
	if ((mode == 1.0 || mode == 3.0) && _MatCapIntensity > 0.0 && _MatCapColor.a > 0.0 && saturate(_MatCapBlendMode) > 0.0)
		result = matcapRGB * material.reflectionMask * material.layerMask.b
			* _MatCapIntensity * lerp(1.0, mainShadow, saturate(_ReflectionShadowStrength))
			* (saturate(_MatCapBlendMode) * saturate(_MatCapColor.a));
	return result;
}

float3 TomEvaluateRim(TomMaterialData material, float3 viewDir, float3 mainLightDir, float mainShadow)
{
	float facing = saturate(dot(material.shadingNormalWS, viewDir));
	float rim = 1.0 - TomSoftBand(facing, _RimWidth, _RimSoftness);
	rim *= lerp(1.0, saturate(dot(material.geometricNormalWS, mainLightDir)), saturate(_RimLightInfluence));
	float shadow = lerp(1.0, mainShadow, saturate(_RimShadowStrength));
	return rim * _RimStrength * _RimColor.rgb * shadow * material.layerMask.g;
}

#if defined(TOM_LIQUID)
float3 TomLiquidPigmentIndirect(TomVaryings i, TomMaterialData m, UnityGIInput input)
{
    UnityGI gi = UnityGlobalIllumination(input, 1.0, m.liquid.diffuseNormalWS);
#if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
    float3 irradiance = max(gi.indirect.diffuse, 0.0);
#else
    float3 irradiance = lerp(_AmbientColor.rgb, max(gi.indirect.diffuse, 0.0), saturate(_LightProbeBlend));
    float4 sh = TomEvaluateCustomSHVolume(i.posWS, m.liquid.diffuseNormalWS);
    irradiance = lerp(irradiance, sh.rgb, saturate(_CustomSHVolumeBlend) * sh.a);
#endif
    float luma = TomLuminance(irradiance);
    float tone = lerp(_IndirectShadeLevel, 1.0, TomSoftBand(luma, _IndirectToonThreshold, _IndirectToonSoftness));
    float3 toned = irradiance / max(luma, 0.0001) * tone * saturate(luma / 0.05);
    irradiance = lerp(irradiance, toned, saturate(_IndirectToonBlend * _LiquidToonBlend));
    float3 vertex = 0.0;
#if defined(VERTEXLIGHT_ON) && !defined(LIGHTMAP_ON)
    vertex = max(i.vertexLightDiffuse, 0.0) * max(_VertexLightIntensity, 0.0);
#endif
    return max(_SkinLiquidColor.rgb, 0.0) * (irradiance * m.occlusion * _IndirectDiffuseIntensity + vertex);
}
#endif

float4 TomFragBase(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	TomMaterialData material = TomGetBaseMaterialData(i, faceSign);
	float3 viewDir = TomSafeDirection(_WorldSpaceCameraPos.xyz - i.posWS);
	float3 lightDir = TomSafeDirection(UnityWorldSpaceLightDir(i.posWS));
	float distanceAttenuation;
	float cookieAttenuation;
	TomSampleLightDistanceAndCookie(i.posWS, distanceAttenuation, cookieAttenuation);
	TomShadowData shadow = TomGetShadowData(i);
	TomRawLightData mainLight = TomBuildRawLightData(
		lightDir,
		_LightColor0.rgb,
		distanceAttenuation,
		cookieAttenuation,
		shadow,
		_MainLightIntensity);
	TomSurfaceLightTerms mainSurface = TomBuildSurfaceLightTerms(material, mainLight);
	TomLightingResult direct = TomEvaluateDirectLight(material, mainLight, mainSurface, viewDir);
	float3 toonDarkFill = 0.0;
	UNITY_BRANCH
	if (_ToonShadeColor.a > 0.0 && _UseRamp > 0.0)
		toonDarkFill = TomEvaluateToonDarkFill(material, mainLight, mainSurface, viewDir);
	UNITY_BRANCH
	if (_ToonMinLighting > 0.0 && _UseRamp > 0.0)
		toonDarkFill += TomEvaluateToonMinimum(material, mainLight, mainSurface, viewDir);

	float engineAttenuation = mainLight.distanceAttenuation
		* mainLight.cookieAttenuation
		* mainLight.sampledShadowAttenuation;
	UnityGIInput giInput = TomBuildGIInput(
		i,
		material,
		viewDir,
		lightDir,
		engineAttenuation);
#if defined(TOM_SKIN)
	UnityGI gi = UnityGlobalIllumination(giInput, 1.0, material.skin.diffuseNormalWS);
#else
	UnityGI gi = UnityGlobalIllumination(giInput, 1.0, material.shadingNormalWS);
#endif
	float3 indirectDiffuse = TomEvaluateIndirectDiffuse(i, material, viewDir, gi);
	float3 vertexLightDiffuse = TomEvaluateVertexLightDiffuse(
		material,
		viewDir,
		i.vertexLightDiffuse);
	float mainDirectOwnership = TomGetMainDirectOwnership();
	direct.diffuse *= mainDirectOwnership;
	direct.specular *= mainDirectOwnership;
	toonDarkFill *= mainDirectOwnership;

	float3 shadowTint = lerp(
		_ShadowColor.rgb,
		1.0,
		mainSurface.diffuseShadowAttenuation);
	shadowTint = lerp(1.0.xxx, shadowTint, mainDirectOwnership);
	float3 matcapSample = TomMatCapSampleForPass(i, material);
	float3 matcapMultiplier = TomMatCapMultiplier(matcapSample, material);
	float3 body = (indirectDiffuse * shadowTint + vertexLightDiffuse + direct.diffuse + toonDarkFill) * matcapMultiplier;
	float3 environment = TomEnvironmentLayer(giInput, material, viewDir, mainSurface.artisticShadowAttenuation);
	float3 matcap = TomMatCapAddLayer(matcapSample, material, mainSurface.artisticShadowAttenuation);
	float3 rim = TomEvaluateRim(material, viewDir, lightDir, mainSurface.artisticShadowAttenuation);
	float3 color = body + direct.specular + environment + matcap + rim + material.emission;
	float3 coatView = TomInterfaceView(i.posWS);
	TomCoatData coat = TomGetCoat(i, material, coatView, TomFaceOrientation(faceSign));
	float3 coatDirect = 0.0, coatEnvironment = 0.0;
	UNITY_BRANCH
	if (_ClearCoat > 0.0)
	{
		float retention = TomCoatDirectRetention(coat, lightDir);
		coatDirect = TomCoatDirect(coat, mainLight, material.geometricNormalWS, coatView) * mainDirectOwnership;
		coatEnvironment = TomCoatEnvironment(coat, giInput, coatView, material.occlusion);
		// Artistic fill represents broad illumination, not a back-facing specular ray.
		float3 coatedBody = ((indirectDiffuse * shadowTint + vertexLightDiffuse + toonDarkFill) * TomCoatBodyRetention(coat.indirectRetention)
			+ direct.diffuse * TomCoatBodyRetention(retention))
			* matcapMultiplier;
		color = coatedBody + direct.specular * retention + environment * coat.indirectRetention
			+ material.emission * coat.viewRetention + coatDirect + coatEnvironment + matcap + rim;
	}
#if defined(TOM_LIQUID)
	float3 liquidDirect = 0.0, liquidEnvironment = 0.0;
	UNITY_BRANCH
	if (TomLiquidWeight(material) > 0.0)
	{
		float weight = TomLiquidWeight(material), pigment = TomLiquidPigment();
		float dr = TomLiquidDirectRetention(material, coatView, lightDir);
		float ir = TomLiquidIndirectRetention(material, coatView);
		liquidDirect = TomLiquidDirect(material, mainLight, coatView) * mainDirectOwnership;
		liquidEnvironment = TomLiquidEnvironment(material, giInput, coatView);
		float3 wetDirect = lerp(direct.diffuse * matcapMultiplier,
			TomLiquidPigmentDirect(material, mainLight, mainSurface.toonVisibility) * mainDirectOwnership, pigment);
		float3 pigmentTint = TomLiquidPigmentShadowTint(lerp(1.0,
			TomLiquidPigmentVisibility(material, mainLight, mainSurface.toonVisibility), mainDirectOwnership));
		float3 wetIndirect = lerp((indirectDiffuse * shadowTint + vertexLightDiffuse + toonDarkFill)
			* matcapMultiplier,
			TomLiquidPigmentIndirect(i, material, giInput) * pigmentTint, pigment);
		float3 substrate = TomLiquidSubstrateSpecular(material);
		float3 wet = wetDirect * dr + wetIndirect * ir
			+ substrate * (direct.specular * dr + environment * ir)
			+ liquidDirect + liquidEnvironment + (matcap + rim + material.emission)
			* (1.0 - pigment) * TomLiquidViewRetention(material, coatView);
		// Wet coverage replaces the existing top coat, never adds a second interface.
		color = lerp(color, wet, weight);
		body = lerp(body, wetDirect * dr + wetIndirect * ir, weight);
		direct.specular = lerp(direct.specular, direct.specular * substrate * dr + liquidDirect, weight);
		environment = lerp(environment, environment * substrate * ir + liquidEnvironment, weight);
	}
#endif
	int debugView = (int)floor(_DebugView + 0.5);
	if (debugView == 1) color = body;
	else if (debugView == 2) color = direct.specular;
	else if (debugView == 3) color = environment;
	else if (debugView == 4) color = matcapMultiplier - saturate(_MatCapBlendMode) + matcap;
	else if (debugView == 5) color = rim;
	else if (debugView == 6) color = material.emission;
	int coatDebug = (int)floor(_ClearCoatDebugView + 0.5);
	if (coatDebug == 1) color = coatDirect;
	else if (coatDebug == 2) color = coatEnvironment;
	else if (coatDebug == 3) color = coat.normalWS * 0.5 + 0.5;
	else if (coatDebug == 4) color = coat.weight.xxx;
	else if (coatDebug == 5) color = TomCoatBodyRetention(coat.indirectRetention).xxx;
#if defined(TOM_EYE)
	if (debugView == 0 && coatDebug == 0) color = lerp(color, material.eyeOverlay.rgb, material.eyeOverlay.a);
	if (_EyeDebugView > 0.5) color = material.eyeDebug;
#endif
	float4 outputColor = float4(max(color, 0.0), 1.0);
	bool applyFog = debugView == 0 && coatDebug == 0;
#if defined(TOM_HAIR)
	int hairDebug = TomHairDebugView();
	if (hairDebug == 1) outputColor.rgb = direct.hairPrimary;
	else if (hairDebug == 2) outputColor.rgb = direct.hairSecondary;
	else if (hairDebug == 3) outputColor.rgb = material.strandWS * 0.5 + 0.5;
	else if (hairDebug == 4) outputColor.rgb = float3(
		0.5 + clamp((_HairHighlightShift + (material.hairControls.r * 2 - 1) * _HairPrimaryShiftMapScale) / 80.0, -0.5, 0.5),
		0.5 + clamp((_HairSecondaryShift + (material.hairControls.g * 2 - 1) * _HairSecondaryShiftMapScale) / 80.0, -0.5, 0.5), 0.0);
	else if (hairDebug == 5) outputColor.rgb = float3(material.hairControls.b, material.hairControls.a, 0.0);
	else if (hairDebug == 6) outputColor.rgb = material.hairNormalWS * 0.5 + 0.5;
	applyFog = applyFog && hairDebug == 0;
#endif
#if defined(TOM_SKIN)
	if (_SkinDebugView > 0.5) outputColor.rgb = material.skin.debug;
	applyFog = applyFog && _SkinDebugView < 0.5;
	if (_FaceSDFDebugView > 0.5)
	{
		float4 sdf = TomSkinFaceSDF(material, lightDir);
		outputColor.rgb = sdf.y.xxx;
		if (_FaceSDFDebugView > 1.5) outputColor.rgb = (sdf.z * sdf.y).xxx;
		if (_FaceSDFDebugView > 2.5) outputColor.rgb = (sdf.x * sdf.y).xxx;
		applyFog = false;
	}
#endif
#if defined(TOM_EYE)
	applyFog = applyFog && _EyeDebugView < 0.5;
	outputColor.a = material.eyeCoverage;
#endif
#if defined(TOM_LIQUID)
	if (_LiquidDebugView > 0.5) outputColor.rgb = TomLiquidDebug(material, liquidDirect, liquidEnvironment);
	applyFog = applyFog && _LiquidDebugView < 0.5;
#endif
	if (applyFog) { UNITY_APPLY_FOG(i.fogCoord, outputColor); }
#if defined(TOM_ALPHA)
	outputColor = TomAlphaOutput(outputColor.rgb, material.coverage);
#endif
	return outputColor;
}

float4 TomFragAdd(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#if defined(TOM_HAIR)
	if (TomHairDebugView() > 2) return 0.0;
#endif
	if (_ClearCoatDebugView > 1.5 || (_ClearCoatDebugView < 0.5 && _DebugView > 2.5)) return 0.0;
#if defined(TOM_LIQUID)
	if (_LiquidDebugView > 0.5 && abs(_LiquidDebugView - 3.0) > 0.5) return 0.0;
#endif
#if defined(TOM_SKIN)
	if (_SkinDebugView > 0.5 || _FaceSDFDebugView > 0.5) return 0.0;
#endif
#if defined(TOM_EYE)
	if (_EyeDebugView > 0.5) return 0.0;
#endif
	TomMaterialData material = TomGetDirectMaterialData(i, faceSign);
	float3 viewDir = TomSafeDirection(_WorldSpaceCameraPos.xyz - i.posWS);
	float3 lightDir = TomSafeDirection(UnityWorldSpaceLightDir(i.posWS));
	float distanceAttenuation;
	float cookieAttenuation;
	TomSampleLightDistanceAndCookie(i.posWS, distanceAttenuation, cookieAttenuation);
	TomShadowData shadow = TomGetShadowData(i);
	TomRawLightData light = TomBuildRawLightData(
		lightDir,
		_LightColor0.rgb,
		distanceAttenuation,
		cookieAttenuation,
		shadow,
		_AdditionalLightIntensity);
	TomSurfaceLightTerms surface = TomBuildSurfaceLightTerms(material, light);
	TomLightingResult direct = TomEvaluateDirectLight(material, light, surface, viewDir);

	// Multiplicative MatCap modifies diffuse only, identically for every light.
	float3 matcapSample = TomMatCapSampleForPass(i, material);
	float3 body = direct.diffuse * TomMatCapMultiplier(matcapSample, material);
	float3 color = body + direct.specular;
	float3 coatView = TomInterfaceView(i.posWS);
	TomCoatData coat = TomGetCoat(i, material, coatView, TomFaceOrientation(faceSign));
	float3 coatDirect = 0.0;
	UNITY_BRANCH
	if (_ClearCoat > 0.0)
	{
		float retention = TomCoatDirectRetention(coat, lightDir);
		coatDirect = TomCoatDirect(coat, light, material.geometricNormalWS, coatView);
		color = body * TomCoatBodyRetention(retention) + direct.specular * retention + coatDirect;
	}
#if defined(TOM_LIQUID)
	float3 liquidDirect = 0.0;
	UNITY_BRANCH
	if (TomLiquidWeight(material) > 0.0)
	{
		float weight = TomLiquidWeight(material);
		float retention = TomLiquidDirectRetention(material, coatView, lightDir);
		liquidDirect = TomLiquidDirect(material, light, coatView);
		float3 wetBody = lerp(body, TomLiquidPigmentDirect(material, light, surface.toonVisibility), TomLiquidPigment()) * retention;
		float3 wetSpecular = direct.specular * TomLiquidSubstrateSpecular(material) * retention + liquidDirect;
		color = lerp(color, wetBody + wetSpecular, weight);
		body = lerp(body, wetBody, weight);
		direct.specular = lerp(direct.specular, wetSpecular, weight);
	}
#endif
	if (_DebugView > 0.5 && _DebugView < 1.5) color = body;
	else if (_DebugView >= 1.5) color = direct.specular;
	if (_ClearCoatDebugView > 0.5) color = coatDirect;
#if defined(TOM_HAIR)
	int hairDebug = TomHairDebugView();
	if (hairDebug == 1) color = direct.hairPrimary;
	else if (hairDebug == 2) color = direct.hairSecondary;
#endif
#if defined(TOM_LIQUID)
	if (_LiquidDebugView > 0.5) color = TomLiquidDebug(material, liquidDirect, 0.0);
#endif
#if defined(TOM_EYE)
	if (_DebugView < 0.5 && _ClearCoatDebugView < 0.5) color *= 1.0 - material.eyeOverlay.a;
#endif
	float4 outputColor = float4(max(color, 0.0), 0.0);
#if defined(TOM_EYE)
	outputColor.a = material.eyeCoverage;
#endif
	bool applyFog = _DebugView < 0.5 && _ClearCoatDebugView < 0.5;
#if defined(TOM_HAIR)
	applyFog = applyFog && hairDebug == 0;
#endif
#if defined(TOM_LIQUID)
	applyFog = applyFog && _LiquidDebugView < 0.5;
#endif
	if (applyFog) { UNITY_APPLY_FOG(i.fogCoord, outputColor); }
#if defined(TOM_ALPHA)
	outputColor = TomAlphaOutput(outputColor.rgb, material.coverage);
#endif
	return outputColor;
}

#endif
