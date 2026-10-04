#ifndef TOM_HAIR_COVERAGE_INC
#define TOM_HAIR_COVERAGE_INC

float _HairFrontMode;
float _HairFrontOpacity;
float _HairFeatherWidth;
float _HairFeatherWidthMode;
float _HairFeatherWorldWidth;
float _HairFeatherThreshold;
float _HairFeatherPower;
sampler2D _TomHairFeatherDistance;
float4 _TomHairFeatherParams; // available, width, height, maximum distance in screen pixels
float4x4 _TomHairFeatherView;
float4 _TomHairFeatherLayout; // partial viewport, provider version, reserved, reserved

void TomHairClipTexture(float coverage)
{
	clip(coverage - max(_Cutoff, 0.00001));
}

float TomHairFeatherWeight(float distance, float widthPixels)
{
	// Preserve the original curve and avoid the power operations at the defaults.
	float weight = smoothstep(0.0, widthPixels, distance);
	UNITY_BRANCH
	if (_HairFeatherThreshold != 0.5 || _HairFeatherPower != 1.0)
	{
		float u = saturate(distance / max(widthPixels, 0.00001));
		float threshold = clamp(_HairFeatherThreshold, 0.05, 0.95);
		float power = clamp(_HairFeatherPower, 0.5, 4.0);
		// Pin 50% progress to threshold; balanced powers change slope, not that pivot.
		float before = u * (1.0 - threshold);
		float after = (1.0 - u) * threshold;
		float biased = saturate(before / (before + after));
		float rising = pow(biased, power);
		float falling = pow(1.0 - biased, power);
		weight = smoothstep(0.0, 1.0, rising / max(rising + falling, 0.00001));
	}
	return weight;
}

// fieldPosition contains un-rasterized clip XY/W and positive view depth.
float TomHairCoverage(float textureCoverage, float2 pixelPosition, float4 fieldPosition)
{
	TomHairClipTexture(textureCoverage);
	float coverage = 1.0;
#if defined(TOM_HAIR_FRONT)
	UNITY_BRANCH
	if (_HairFrontMode > 0.5)
	{
		coverage = saturate(textureCoverage) * saturate(_HairFrontOpacity);
		UNITY_BRANCH
		float widthPixels = _HairFeatherWidth;
		if (_HairFeatherWidthMode > 0.5)
		{
			float depth = lerp(max(fieldPosition.w, _ProjectionParams.y), 1.0, unity_OrthoParams.w);
			widthPixels = max(_HairFeatherWorldWidth, 0.0) * 0.5 * _TomHairFeatherParams.z
				* abs(UNITY_MATRIX_P._m11) / max(depth, 0.00001);
			widthPixels = _TomHairFeatherLayout.y >= 2.0 ? min(widthPixels, _TomHairFeatherParams.w) : 0.0;
		}
		if (_HairFrontMode > 1.5 && widthPixels > 0.0 && _TomHairFeatherParams.x > 0.5)
		{
			// Never reuse another camera's field (e.g. a nested reflection capture).
			float4 viewDelta = abs(_TomHairFeatherView[0] - UNITY_MATRIX_V[0])
				+ abs(_TomHairFeatherView[1] - UNITY_MATRIX_V[1])
				+ abs(_TomHairFeatherView[2] - UNITY_MATRIX_V[2]);
			if (dot(viewDelta, 1.0.xxxx) < 0.0001
				&& (_TomHairFeatherLayout.x > 0.5 || all(abs(_TomHairFeatherParams.yz - _ScreenParams.xy) < 0.5)))
			{
				float2 uv = pixelPosition / _ScreenParams.xy;
				// D3D raster coordinates normalized to this camera's viewport, not the full target.
				if (_TomHairFeatherLayout.x > 0.5)
					uv = fieldPosition.xy / fieldPosition.z * float2(0.5, -0.5) + 0.5;
				float distance = tex2D(_TomHairFeatherDistance, saturate(uv)).r;
				if (distance >= 0.0)
				{
					float weight = TomHairFeatherWeight(distance, widthPixels);
					coverage = lerp(1.0, coverage, weight);
				}
			}
		}
	}
#endif
	// Never leave an invisible depth occluder, even when Cutoff is zero.
	clip(coverage - 0.00001);
	return coverage;
}

#endif
