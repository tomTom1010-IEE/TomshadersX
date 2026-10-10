Shader "tom/LiquidX"
{
    Properties
    {
        _MainTex ("Surface Cutout (A)", 2D) = "white" {}
        _AlphaMask ("Surface Cutout Mask (R)", 2D) = "white" {}
        _Cutoff ("Surface Cutout", Range(0,1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _CullOption ("Cull", Float) = 2
        _OffsetFactor ("Depth Offset Factor", Range(-4,4)) = 0
        _OffsetUnits ("Depth Offset Units", Range(-4,4)) = -1
        _NormalMap ("Substrate Normal", 2D) = "bump" {}
        _NormalMapScale ("Substrate Normal Strength", Range(0,2)) = 1
        _Texture2 ("KKS Liquid Pattern (R G)", 2D) = "black" {}
        _Texture3 ("Liquid Normal", 2D) = "gray" {}
        _liquidmask ("KKS Liquid Regions", 2D) = "black" {}
        _LiquidTiling ("Liquid Offset XY Tiling ZW", Vector) = (0,0,1,1)
        _liquidftop ("Front Top Liquid", Range(0,2)) = 0
        _liquidfbot ("Front Bottom Liquid", Range(0,2)) = 0
        _liquidbtop ("Back Top Liquid", Range(0,2)) = 0
        _liquidbbot ("Back Bottom Liquid", Range(0,2)) = 0
        _liquidface ("Face Liquid", Range(0,2)) = 0
        [Gamma] _SkinLiquidColor ("Liquid Pigment Color", Color) = (0.85,0.85,0.8,0)
        _SkinLiquidColorStrength ("Liquid Pigment Strength", Range(0,1)) = 1
        _SkinLiquidMaterial ("Liquid Layer Strength", Range(0,1)) = 1
        _SkinLiquidNormalScale ("Liquid Normal Slope", Range(0,2)) = 1
        _SkinLiquidRoughness ("Liquid Roughness", Range(0.04,1)) = 0.25
        _LiquidDiffuseNormal ("Liquid Pigment Normal Influence", Range(0,1)) = 0
        _LiquidSpecularStrength ("Liquid Specular Strength", Range(0,2)) = 1
        [Enum(Raw AG,0,Exported AG PNG,1,Unity Normal,2)] _LiquidNormalEncoding ("Liquid Normal Encoding", Float) = 0
        [Enum(KKS Regions,0,Custom Mask,1,Full Surface,2)] _LiquidCoverageMode ("Liquid Coverage Source", Float) = 2
        _LiquidCoverageMap ("Liquid Custom Coverage (R)", 2D) = "white" {}
        _LiquidCutoff ("Liquid Coverage Cutoff", Range(0,1)) = 0.02
        _LiquidEdgeSoftness ("Liquid Edge Softness", Range(0,0.5)) = 0.04
        _LiquidMaxNormalAngle ("Liquid Maximum Normal Angle", Range(5,85)) = 60
        _LiquidNormalAA ("Liquid Normal Filtering", Range(0,2)) = 1
        _LiquidIOR ("Liquid IOR", Range(1,2)) = 1.33
        _LiquidEnvironmentStrength ("Liquid Environment Strength", Range(0,2)) = 1
        _LiquidToonBlend ("Liquid Pigment Toon Blend", Range(0,1)) = 0
        _LiquidEnergyBlend ("Liquid Substrate Attenuation", Range(0,1)) = 0.25
        _LiquidAttenuationNormal ("Liquid Detail in Attenuation", Range(0,1)) = 0
        _LiquidShadowColor ("Liquid Pigment Shadow (RGB tint / A strength)", Color) = (0.65,0.65,0.65,0)
        [Enum(Off,0,Coverage,1,Normal,2,Direct Specular,3,Environment,4,Pigment,5,Raw Coverage,6)] _LiquidDebugView ("Liquid Debug View", Float) = 0
		_MainLightIntensity ("Main Light Intensity", Range(0,4)) = 1
		_AdditionalLightIntensity ("Additional Light Intensity", Range(0,4)) = 1
		_VertexLightIntensity ("Vertex Light Fill Intensity", Range(0,1)) = 0.5
        [Gamma] _AmbientColor ("Ambient Color", Color) = (0.2,0.2,0.2,1)
        _LightProbeBlend ("Native SH Blend", Range(0,1)) = 1
        _CustomSHVolumeBlend ("Custom SH Volume Blend", Range(0,1)) = 1
		_IndirectDiffuseIntensity ("Indirect Diffuse Intensity", Range(0,4)) = 0.25
        _IndirectToonBlend ("Indirect Toon Blend", Range(0,1)) = 0
		_IndirectShadeLevel ("Indirect Shade Level", Range(0,1)) = 0.3
		_IndirectToonThreshold ("Indirect Tone Threshold", Range(0,2)) = 0.4
		_IndirectToonSoftness ("Indirect Tone Softness", Range(0.001,1)) = 0.2
        _UseRamp ("Use Toon Ramp", Range(0,1)) = 1
        _ToonThreshold ("Toon Threshold", Range(0,1)) = 0.5
        _ToonSoftness ("Toon Softness", Range(0,1)) = 0.2
        _ToonShadeLevel ("Toon Shade Level", Range(0,1)) = 0.3
        _RampTex ("Toon Ramp", 2D) = "white" {}
        [Enum(Procedural,0,Texture,1)] _RampMode ("Ramp Mode", Float) = 0
        _ShadowStrength ("Shadow Strength", Range(0,1)) = 1
		_NormalHorizonFade ("Normal Horizon Fade", Range(0.001,0.25)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+50" "IgnoreProjector"="True" }
        Cull [_CullOption]
        ZWrite Off
        ZTest LEqual
        Offset [_OffsetFactor], [_OffsetUnits]
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode"="ForwardBase" }
            Blend One OneMinusSrcAlpha, Zero One
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex TomVert
            #pragma fragment TomLiquidOverlayBase
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile __ UNITY_SPECCUBE_BOX_PROJECTION
            #pragma multi_compile __ UNITY_SPECCUBE_BLENDING
            #pragma only_renderers d3d11 glcore gles3 metal xboxone ps4
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #define TOM_LIQUID 1
            #include "TomToonInput.cginc"
            #include "TomToonLighting.cginc"
            #include "TomLiquidOverlay.cginc"
            ENDCG
        }
        Pass
        {
            Name "FORWARDADD"
            Tags { "LightMode"="ForwardAdd" }
            Blend One One
            ColorMask RGB
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex TomVert
            #pragma fragment TomLiquidOverlayAdd
            #pragma multi_compile_fwdadd_fullshadows
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma only_renderers d3d11 glcore gles3 metal xboxone ps4
            #include "UnityCG.cginc"
            #include "UnityStandardUtils.cginc"
            #include "AutoLight.cginc"
            #include "Lighting.cginc"
            #define TOM_LIQUID 1
            #include "TomToonInput.cginc"
            #include "TomToonLighting.cginc"
            #include "TomLiquidOverlay.cginc"
            ENDCG
        }
    }
    Fallback Off
}
