Shader "Hidden/TomX/HairFeather"
{
	Properties { [HideInInspector] _TomHairFieldVersion ("Field Contract Version", Float) = 2 }
	SubShader
	{
		Cull Off ZWrite Off ZTest Always
		CGINCLUDE
		#include "UnityCG.cginc"
		sampler2D _TomHairFieldInput;
		sampler2D _TomHairFieldMask;
		float4 _TomHairFieldSize; // field width, height, screen width, height
		float _TomHairJump;
		float _TomHairMaxDistance;
		v2f_img vertMask(appdata_img v)
		{
			v2f_img o;
			o.pos = float4(v.vertex.xy, 0, 1);
			o.uv = v.texcoord;
			return o;
		}
		float4 mask(v2f_img i) : SV_Target { return 1; }
		float4 seed(v2f_img i) : SV_Target
		{
			float2 uv = i.pos.xy / _TomHairFieldSize.xy;
			float inside = tex2D(_TomHairFieldInput, uv).r;
			return inside < 0.5 ? float4(uv, 0, 1) : float4(-1, -1, 0, 0);
		}
		float4 jump(v2f_img i) : SV_Target
		{
			float2 nearest = float2(-1, -1);
			float best = 1e20;
			float2 centerUV = i.pos.xy / _TomHairFieldSize.xy;
			[unroll] for (int y = -1; y <= 1; y++)
			[unroll] for (int x = -1; x <= 1; x++)
			{
				float2 uv = centerUV + float2(x, y) * _TomHairJump / _TomHairFieldSize.xy;
				float2 candidate = tex2D(_TomHairFieldInput, saturate(uv)).rg;
				float2 delta = (candidate - centerUV) * _TomHairFieldSize.zw;
				float distanceSq = dot(delta, delta);
				if (candidate.x >= 0 && candidate.y >= 0 && distanceSq < best)
				{
					best = distanceSq;
					nearest = candidate;
				}
			}
			return float4(nearest, 0, 1);
		}
		float4 distanceField(v2f_img i) : SV_Target
		{
			float2 uv = i.pos.xy / _TomHairFieldSize.xy;
			float2 nearest = tex2D(_TomHairFieldInput, uv).rg;
			float distance = nearest.x < 0 ? _TomHairMaxDistance : min(_TomHairMaxDistance, length((nearest - uv) * _TomHairFieldSize.zw));
			// Outside the registered proxy is unavailable, not fully opaque feather.
			distance = tex2D(_TomHairFieldMask, uv).r >= 0.5 ? distance : -0.01;
			return distance.xxxx;
		}
		ENDCG
		Pass
		{
			Name "EXTRACT_STENCIL"
			Stencil { Ref 2 Comp Equal Pass Keep }
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vertMask
			#pragma fragment mask
			ENDCG
		}
		Pass
		{
			Name "SEED"
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vertMask
			#pragma fragment seed
			ENDCG
		}
		Pass
		{
			Name "JUMP"
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vertMask
			#pragma fragment jump
			ENDCG
		}
		Pass
		{
			Name "DISTANCE"
			CGPROGRAM
			#pragma target 3.0
			#pragma vertex vertMask
			#pragma fragment distanceField
			ENDCG
		}
	}
	Fallback Off
}
