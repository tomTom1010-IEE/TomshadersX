#ifndef TOM_HAIR_FORWARD_INC
#define TOM_HAIR_FORWARD_INC

#include "TomHairCoverage.cginc"

float TomHairFragmentCoverage(TomVaryings i)
{
	float alpha = SAMPLE_TEX2D(_MainTex, i.uv0 * _MainTex_ST.xy + _MainTex_ST.zw).a;
	float4 clipPosition = UnityWorldToClipPos(i.posWS);
	float depth = -mul(UNITY_MATRIX_V, float4(i.posWS, 1.0)).z;
	return TomHairCoverage(TomSurfaceAlpha(i, alpha), i.posCS.xy, float4(clipPosition.xy, clipPosition.w, depth));
}

float4 TomHairFragBase(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
	float coverage = TomHairFragmentCoverage(i);
	float4 color = TomFragBase(i, faceSign);
	color.a = coverage;
	return color;
}

float4 TomHairFragAdd(TomVaryings i, fixed faceSign : VFACE) : SV_Target
{
	float coverage = TomHairFragmentCoverage(i);
	float4 color = TomFragAdd(i, faceSign);
	// Add uses One/One. Only new light is attenuated; background is not blended twice.
	return float4(color.rgb * coverage, 0.0);
}

#endif
