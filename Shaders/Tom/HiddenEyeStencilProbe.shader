Shader "Hidden/TomX/EyeStencilProbe"
{
    SubShader
    {
        Tags { "Queue" = "AlphaTest+26" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Stencil { Ref 2 Comp Equal Pass Keep }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 vert(float4 p : POSITION) : SV_POSITION { return UnityObjectToClipPos(p); }
            float4 frag() : SV_Target { return float4(1,1,1,1); }
            ENDCG
        }
    }
    Fallback Off
}
