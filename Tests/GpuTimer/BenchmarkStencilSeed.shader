Shader "Hidden/TomX/BenchmarkStencilSeed"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask 0
            Stencil { Ref 2 Comp Always Pass Replace }
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            float4 vert(uint id : SV_VertexID) : SV_POSITION
            {
                return float4(id == 1 ? 3 : -1, id == 2 ? 3 : -1, 0, 1);
            }
            float4 frag() : SV_Target { return 0; }
            ENDCG
        }
    }
}
