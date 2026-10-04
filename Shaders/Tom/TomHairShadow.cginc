#include "UnityCG.cginc"
sampler2D _MainTex;
sampler2D _AlphaMask;
float4 _MainTex_ST;
float4 _AlphaMask_ST;
float _Cutoff;
float _CullOption;
struct v2f { float2 uv : TEXCOORD1; V2F_SHADOW_CASTER; };
v2f vertShadow(appdata_base v)
{
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	o.uv = v.texcoord.xy;
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
	return o;
}
float4 fragShadow(v2f i, fixed faceSign : VFACE) : SV_Target
{
	if (_CullOption > 1.5)
		clip(faceSign);
	else if (_CullOption > 0.5)
		clip(-faceSign);
	float mainAlpha = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).a;
	float mask = tex2D(_AlphaMask, i.uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw).r;
	clip(mainAlpha * mask - max(_Cutoff, 0.00001));
	SHADOW_CASTER_FRAGMENT(i)
}

