#ifndef TOM_ALPHA_COMMON_INC
#define TOM_ALPHA_COMMON_INC

float _Alpha;
float _AlphaBlendMode;
float _AlphaOptionCutoff;
float _AlphaOptionZWrite;
float _DepthShadowCutoff;
float _CastShadows;
#if defined(TOM_ALPHA_BACK_PASS)
float _BackfaceZWrite;
#endif

float TomAlphaCoverage(float mainAlpha, float mask)
{
	return saturate(mainAlpha * mask * _Alpha);
}

void TomAlphaClipGlobal(float coverage)
{
	// Zero coverage must not draw color, write depth or cast shadows, even at cutoff zero.
	clip(coverage > 0.0 ? 1.0 : -1.0);
	if (_AlphaOptionCutoff > 0.5)
		clip(coverage - _Cutoff);
}

void TomAlphaClipDepthShadow(float coverage)
{
	TomAlphaClipGlobal(coverage);
	clip(coverage - saturate(_DepthShadowCutoff));
}

void TomAlphaClipColor(float coverage)
{
	TomAlphaClipGlobal(coverage);
#if !defined(TOM_ALPHA_PREPASS)
	// A one-pass depth-writing fragment cannot keep color while declining its depth write.
	#if defined(TOM_ALPHA_BACK_PASS)
	if (_BackfaceZWrite > 0.5)
	#else
	if (_AlphaOptionZWrite > 0.5)
	#endif
		TomAlphaClipDepthShadow(coverage);
#endif
}

float4 TomAlphaOutput(float3 color, float coverage)
{
	// Blend source factor is either SrcAlpha (5) or One (1). Textures remain straight RGB.
	if (_AlphaBlendMode < 2.0)
		color *= coverage;
	return float4(color, coverage);
}

#endif
