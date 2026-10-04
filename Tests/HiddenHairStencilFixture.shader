Shader "Hidden/TomX/HairStencilFixture"
{
	Properties { _MainTex ("Mask", 2D) = "white" {} _StencilCutoff ("Cutoff", Range(0,1)) = 0.5 }
	SubShader
	{
		Tags { "Queue" = "AlphaTest+10" }
		Pass
		{
			Name "StencilMask"
			Cull Off ZWrite Off ColorMask 0
			Stencil { Ref 2 Comp Always Pass Replace }
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			sampler2D _MainTex;
			float _StencilCutoff;
			struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
			v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
			float4 frag(v2f i) : SV_Target { clip(tex2D(_MainTex, i.uv).a - _StencilCutoff); return 0; }
			ENDCG
		}
	}
	Fallback Off
}
