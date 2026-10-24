#ifndef TOM_HAIR_BRDF_INC
#define TOM_HAIR_BRDF_INC

DECLARE_TEX2D_NOSAMPLER(_ColorMask);
float4 _ColorMask_ST;
float4 _Color, _Color2, _Color3;

float3 TomHairColor(TomVaryings i)
{
	float2 uv = i.uv0 * _ColorMask_ST.xy + _ColorMask_ST.zw;
	// Share MainTex filtering/wrap to stay within D3D11's 16-sampler limit.
	float3 mask = SAMPLE_TEX2D_SAMPLER(_ColorMask, _MainTex, uv).rgb;
	// KKS/xukmi sequential overrides; a black or unset mask leaves the texture unchanged.
	float3 tint = lerp(1.0.xxx, _Color.rgb, mask.r);
	tint = lerp(tint, _Color2.rgb, mask.g);
	return lerp(tint, _Color3.rgb, mask.b);
}

DECLARE_TEX2D_NOSAMPLER(_StrandDirectionMap);
float4 _StrandDirectionMap_ST;
float _StrandDirectionBlend;
float _StrandAngle;
float _HairAnisotropy;
float _StrandDirectionMode;
float _HairHighlightShift, _HairPrimaryLongitudinalWidth, _HairPrimaryAzimuthalWidth;
float _HairSecondaryWeight, _HairSecondaryShift, _HairSecondaryRoughnessOffset;
float _HairSecondaryToonBlend, _HairSecondarySize, _HairSecondaryThreshold, _HairSecondarySoftness;
float _HairSecondaryLongitudinalWidth, _HairSecondaryAzimuthalWidth;
float4 _HairSecondaryColor;
float _HairSecondaryBaseColorTint;
DECLARE_TEX2D_NOSAMPLER(_HairHighlightMap);
float4 _HairHighlightMap_ST;
float _UseHairHighlightMap, _HairPrimaryShiftMapScale, _HairSecondaryShiftMapScale;
float _HairSpecularNormalInfluence, _HairSpecularDetailNormalInfluence;
float _HairEnvironmentAnisotropy, _HairHighlightDebugView;
float _HairPrimaryHeight, _HairSecondaryHeight, _HairHeightAxisAngle;

int TomHairDebugView()
{
	int view = (int)floor(_HairHighlightDebugView + 0.5);
	// Retired head-space data views must not suppress final lighting, fog or Add.
	return view >= 0 && view <= 6 ? view : 0;
}

float4 TomHairControls(TomVaryings i)
{
	float4 controls = float4(0.5, 0.5, 1.0, 1.0);
	UNITY_BRANCH
	if (_UseHairHighlightMap > 0.5)
		controls = saturate(SAMPLE_TEX2D_SAMPLER(_HairHighlightMap, _MainTex,
			i.uv0 * _HairHighlightMap_ST.xy + _HairHighlightMap_ST.zw));
	return controls;
}

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

float3 TomHairHeightAxis(TomVaryings i, float3 shadingNormal)
{
	// Use the original mesh frame, not the strand-flow angle or the face-flipped normal.
	float3 meshNormal = normalize(i.normalWS);
	float3 meshU = TomHairTangent(meshNormal, i.tangentWS.xyz);
	float handedness = i.tangentWS.w < 0.0 ? -1.0 : 1.0;
	float3 meshV = cross(meshNormal, meshU) * handedness * unity_WorldTransformParams.w;
	float s, c; sincos(radians(_HairHeightAxisAngle), s, c);
	return TomHairTangent(shadingNormal, meshU * c + meshV * s);
}

void TomHairHeightFrame(inout float3 normal, inout float3 strand, float3 baseNormal, float3 heightAxis, float height)
{
	UNITY_BRANCH
	if (height != 0.0)
	{
		float bias = clamp(height, -1.0, 1.0);
		float c = rsqrt(1.0 + bias * bias), s = -bias * c;
		float3 axis = TomHairUnit(cross(baseNormal, heightAxis), cross(baseNormal, strand));
		// Rotate the entire reflection frame, preserving both endpoint shapes and their visibility terms.
		normal = normal * c + cross(axis, normal) * s + axis * dot(axis, normal) * (1.0 - c);
		strand = strand * c + cross(axis, strand) * s + axis * dot(axis, strand) * (1.0 - c);
	}
}

float3 TomHairStrand(TomVaryings i, float3 geometricNormal, float3 shadingNormal, float faceOrientation,
	out float3 sourceStrand)
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
		if (_StrandDirectionMode < 0.5)
		{
			flow *= dot(flow, strand) < 0.0 ? -1.0 : 1.0;
			strand = lerp(strand, flow, saturate(_StrandDirectionBlend));
		}
		else
		{
			// Directed interpolation stays finite even for opposite root-to-tip arrows.
			float cross2 = strand.x * flow.y - strand.y * flow.x;
			float delta = atan2(abs(cross2) < 0.000001 ? 0.0 : cross2, dot(strand, flow));
			float a = angle + delta * saturate(_StrandDirectionBlend);
			strand = float2(cos(a), sin(a));
		}
	}
	float3 direction = tangent * strand.x + bitangent * strand.y;
	sourceStrand = direction;
	return TomHairTangent(shadingNormal, direction);
}

float TomHairFrameVariance(float3 normal)
{
	float3 dx = ddx(normal), dy = ddy(normal);
	return 0.5 * (dot(dx, dx) + dot(dy, dy));
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

void TomHairShiftFrame(inout float3 normal, inout float3 strand, float shiftDegrees)
{
	if (shiftDegrees != 0.0)
	{
		float s, c; sincos(radians(clamp(shiftDegrees, -40.0, 40.0)), s, c);
		float3 n = normal;
		normal = n * c + strand * s;
		strand = strand * c - n * s;
	}
}

float TomHairToonLobe(float3 normal, float3 strand, float3 halfDir, float variance,
	float size, float threshold, float softness, float2 widthScale)
{
	float roughness = TomFilteredRoughness(lerp(0.08, 1.0, saturate(size)), variance);
	float2 axes = max(TomHairAxes(roughness) * clamp(widthScale, 0.25, 4.0), 0.002);
	float distribution = TomHairNormalizedDistribution(normal, strand, halfDir, axes);
	float width = max(softness, fwidth(distribution) * _SpecularAA);
	float band = TomSoftBand(distribution, threshold, width);
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
	float3 viewDir, float3 halfDir, float roughness, float3 fresnel, float2 widthScale)
{
	float2 axes = max(TomHairAxes(roughness) * clamp(widthScale, 0.25, 4.0), 0.002);
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

float3 TomHairLobe(float3 normal, float3 strand, float3 lightDir, float3 viewDir,
	float3 halfDir, float roughness, float3 fresnel, float2 widthScale, float toonBlend, float toonLobe)
{
	float3 specular = 0.0;
	UNITY_BRANCH
	if (toonBlend < 0.999)
		specular = TomHairProductionSpecular(normal, strand, lightDir, viewDir, halfDir,
			roughness, fresnel, widthScale);
	float3 shaped = toonLobe * fresnel * saturate(dot(normal, lightDir));
	// The original surface still owns light/view visibility; the shifted frame only places the lobe.
	float visible = step(0.00001, dot(normal, lightDir)) * step(0.00001, dot(normal, viewDir));
	return lerp(specular, shaped, saturate(toonBlend)) * visible;
}

float3 TomHairEnvironmentNormal(float3 normal, float3 strand, float3 viewDir)
{
	float amount = saturate(_HairEnvironmentAnisotropy) * saturate(_HairAnisotropy);
	if (amount <= 0.0) return normal;
	// Bent reflection approximation; Unity's cubemap prefilter remains isotropic.
	// Project away the narrow axis: HairX is narrow along T and broad across T.
	float3 projected = viewDir - strand * dot(strand, viewDir);
	float len2 = dot(projected, projected);
	float3 bent = TomHairUnit(projected, normal);
	return TomHairUnit(lerp(normal, bent, amount * smoothstep(0.000001, 0.0001, len2)), normal);
}

#endif
