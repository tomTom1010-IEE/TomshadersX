#include "UnityCG.cginc"
sampler2D _MainTex;
sampler2D _AlphaMask;
float4 _MainTex_ST;
float4 _AlphaMask_ST;
float _Cutoff;
#include "TomAlphaCommon.cginc"
float _CullOption;
struct v2f { float2 uv : TEXCOORD1; V2F_SHADOW_CASTER; };
v2f vertShadow(appdata_base v)
{
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	o.uv = v.texcoord.xy;
	#if defined(TOM_ALPHA_BACKFRONT_SHADOW)
	// Both faces are visible across the color passes, so use the two-sided bias policy.
	TRANSFER_SHADOW_CASTER_NOPOS_LEGACY(o, o.pos)
	#else
	UNITY_BRANCH
	if (_CullOption < 0.5)
	{
		TRANSFER_SHADOW_CASTER_NOPOS_LEGACY(o, o.pos)
	}
	else
	{
		if (_CullOption < 1.5)
			v.normal = -v.normal;
		TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
	}
	#endif
	return o;
}
float4 fragShadow(v2f i, fixed faceSign : VFACE) : SV_Target
{
	#if !defined(TOM_ALPHA_BACKFRONT_SHADOW)
	if (_CullOption > 1.5)
		clip(faceSign);
	else if (_CullOption > 0.5)
		clip(-faceSign);
	#endif
	float mainAlpha = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).a;
	float mask = tex2D(_AlphaMask, i.uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw).r;
	clip(_CastShadows - 0.5);
	TomAlphaClipDepthShadow(TomAlphaCoverage(mainAlpha, mask));
	SHADOW_CASTER_FRAGMENT(i)
}
