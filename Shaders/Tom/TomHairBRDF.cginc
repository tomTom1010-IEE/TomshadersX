#ifndef TOM_HAIR_BRDF_INC
#define TOM_HAIR_BRDF_INC

DECLARE_TEX2D_NOSAMPLER(_StrandDirectionMap);
float4 _StrandDirectionMap_ST;
float _StrandDirectionBlend;
float _StrandAngle;
float _HairAnisotropy;

float3 TomHairUnit(float3 value, float3 fallback)
{
	float lengthSq = dot(value, value);
	return lengthSq > 0.000001 ? value * rsqrt(max(lengthSq, 0.000001)) : fallback;
}

float3 TomHairTangent(float3 normalWS, float3 tangentWS)
{
	float3 axis = abs(normalWS.z) < 0.999 ? float3(0,0,1) : float3(0,1,0);
	float3 fallback = normalize(cross(axis, normalWS));
	return TomHairUnit(tangentWS - normalWS * dot(normalWS, tangentWS), fallback);
}

float3 TomHairStrand(TomVaryings i, float3 geometricNormal, float3 shadingNormal, float faceOrientation)
{
	float3 tangent = TomHairTangent(geometricNormal, i.tangentWS.xyz);
	float handedness = i.tangentWS.w < 0.0 ? -1.0 : 1.0;
	float3 bitangent = cross(geometricNormal, tangent) * handedness * unity_WorldTransformParams.w;
	float angle = radians(_StrandAngle);
	float2 strand = float2(cos(angle), sin(angle));
	UNITY_BRANCH
	if (_StrandDirectionBlend > 0.0)
	{
		float2 uv = i.uv0 * _StrandDirectionMap_ST.xy + _StrandDirectionMap_ST.zw;
		// Linear RG flow data, NOT a packed Unity/KK normal texture.
		float2 flow = SAMPLE_TEX2D_SAMPLER(_StrandDirectionMap, _MainTex, uv).rg * 2.0 - 1.0;
		float len2 = dot(flow, flow);
		flow = len2 > 0.0001 ? flow * rsqrt(max(len2, 0.0001)) : strand;
		// A strand is an unoriented axis: align signs before interpolating.
		flow *= dot(flow, strand) < 0.0 ? -1.0 : 1.0;
		strand = lerp(strand, flow, saturate(_StrandDirectionBlend));
	}
	float3 direction = tangent * strand.x + bitangent * strand.y;
	return TomHairTangent(shadingNormal, direction);
}

float2 TomHairAxes(float roughness)
{
	float alpha = max(roughness * roughness, 0.002);
	float aspect = sqrt(1.0 - 0.9 * saturate(_HairAnisotropy));
	// Narrow along the strand, broad across it: the highlight forms a hair band.
	return max(float2(alpha * aspect, alpha / aspect), 0.002);
}

float TomHairNormalizedDistribution(float3 normal, float3 strand, float3 halfDir, float2 axes)
{
	float3 across = cross(normal, strand);
	float3 projected = float3(dot(strand, halfDir) / axes.x,
		dot(across, halfDir) / axes.y, saturate(dot(normal, halfDir)));
	float denominator = max(dot(projected, projected), 0.00001);
	return rcp(denominator * denominator);
}

float TomHairToonLobe(float3 normal, float3 strand, float3 halfDir, float variance)
{
	float roughness = TomFilteredRoughness(lerp(0.08, 1.0, saturate(_SpecularSize)), variance);
	float distribution = TomHairNormalizedDistribution(normal, strand, halfDir, TomHairAxes(roughness));
	float width = max(_SpecularSoftness, fwidth(distribution) * _SpecularAA);
	float band = TomSoftBand(distribution, _SpecularThreshold, width);
	int levels = (int)clamp(floor(_SpecularBands + 0.5), 1.0, 4.0);
	if (levels > 1)
	{
		float quantized = 0.0;
		for (int index = 1; index < 4; index++)
			if (index < levels)
				quantized += TomSoftBand(band, (float)index / levels, max(fwidth(band), 0.01));
		band = quantized / (levels - 1);
	}
	return band;
}

float3 TomHairProductionSpecular(float3 normal, float3 strand, float3 lightDir,
	float3 viewDir, float3 halfDir, float roughness, float3 fresnel)
{
	float2 axes = TomHairAxes(roughness);
	float3 across = cross(normal, strand);
	float noV = saturate(dot(normal, viewDir));
	float noL = saturate(dot(normal, lightDir));
	// Anisotropic GGX D and correlated Smith V (Filament's anisotropic BRDF).
	float distribution = TomHairNormalizedDistribution(normal, strand, halfDir, axes)
		/ (UNITY_PI * axes.x * axes.y);
	float lambdaV = noL * length(float3(axes.x * dot(strand, viewDir), axes.y * dot(across, viewDir), noV));
	float lambdaL = noV * length(float3(axes.x * dot(strand, lightDir), axes.y * dot(across, lightDir), noL));
	float visibility = 0.5 / max(lambdaV + lambdaL, 0.00001);
	float term = max(distribution * visibility * UNITY_PI, 0.0);
#if defined(UNITY_COLORSPACE_GAMMA)
	term = sqrt(max(term, 0.0001));
#endif
	return term * noL * fresnel;
}

#endif
