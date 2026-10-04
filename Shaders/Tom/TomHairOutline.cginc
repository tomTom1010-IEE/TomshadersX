#include "UnityCG.cginc"
sampler2D _MainTex;
sampler2D _AlphaMask;
sampler2D _OutlineWidthMask;
float4 _MainTex_ST;
float4 _AlphaMask_ST;
float4 _OutlineWidthMask_ST;
float _Cutoff;
float _CullOption;
float _OutlineOn;
float _OutlineWidth;
float _OutlineScreenSpace;
float _OutlineDepthOffset;
float _OutlineNormalSource;
float _DebugView;
float4 _OutlineColor;
#include "TomHairCoverage.cginc"
struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	float3 smoothNormalOS : TEXCOORD3;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct v2f
{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD0;
	UNITY_FOG_COORDS(1)
	float4 fieldPosition : TEXCOORD2;
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};
v2f vertOutline(appdata v)
{
	v2f o;
	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_OUTPUT(v2f, o);
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
	float4 basePositionCS = UnityObjectToClipPos(v.vertex);
	float3 positionWS = mul(unity_ObjectToWorld, v.vertex).xyz;
	float3 normalOS = v.normal;
	if (_OutlineNormalSource > 0.5 && dot(v.smoothNormalOS, v.smoothNormalOS) > 0.0001)
		normalOS = normalize(v.smoothNormalOS);
	float3 normalWS = normalize(UnityObjectToWorldNormal(normalOS));
	float2 widthUV = v.uv * _OutlineWidthMask_ST.xy + _OutlineWidthMask_ST.zw;
	float width = _OutlineWidth * tex2Dlod(_OutlineWidthMask, float4(widthUV, 0, 0)).r;
	float3 expandedPositionWS = positionWS + normalWS * (width * 0.01);
	float4 worldSpacePositionCS = UnityWorldToClipPos(expandedPositionWS);

	float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
	float2 projectedNormal = mul((float2x2)UNITY_MATRIX_P, normalVS.xy);
	projectedNormal /= max(length(projectedNormal), 0.0001);
	float2 pixelSizeCS = 2.0 / _ScreenParams.xy;
	float4 screenSpacePositionCS = basePositionCS;
	screenSpacePositionCS.xy += projectedNormal * pixelSizeCS * width * basePositionCS.w;
	o.pos = lerp(worldSpacePositionCS, screenSpacePositionCS, saturate(_OutlineScreenSpace));
	// Use the expanded shell's screen position, with the underlying surface depth for width.
	o.fieldPosition = float4(o.pos.xy, o.pos.w, -mul(UNITY_MATRIX_V, float4(positionWS, 1.0)).z);
	#if defined(UNITY_REVERSED_Z)
	o.pos.z -= _OutlineDepthOffset * 0.001 * o.pos.w;
	#else
	o.pos.z += _OutlineDepthOffset * 0.001 * o.pos.w;
	#endif
	o.uv = v.uv;
	UNITY_TRANSFER_FOG(o, o.pos);
	return o;
}
fixed4 fragOutline(v2f i, fixed faceSign : VFACE) : SV_Target
{
	UNITY_SETUP_INSTANCE_ID(i);
	UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
	clip(_OutlineOn - 0.5);
	clip(0.5 - _DebugView);
	if (_CullOption > 0.5 && _CullOption < 1.5)
		clip(faceSign);
	else
		clip(-faceSign);
	float mainAlpha = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw).a;
	float mask = tex2D(_AlphaMask, i.uv * _AlphaMask_ST.xy + _AlphaMask_ST.zw).r;
	float coverage = TomHairCoverage(mainAlpha * mask, i.pos.xy, i.fieldPosition);
	fixed4 color = _OutlineColor;
	color.a *= coverage;
	clip(color.a - 0.00001);
	UNITY_APPLY_FOG(i.fogCoord, color);
	return color;
}
