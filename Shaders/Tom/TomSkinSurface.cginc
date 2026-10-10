#ifndef TOM_SKIN_SURFACE_INC
#define TOM_SKIN_SURFACE_INC

#include "TomSkinCoverage.cginc"

float3 TomSkinColor(TomVaryings i, float3 mainColor)
{
    float3 mask = SAMPLE_TEX2D_SAMPLER(_ColMask, _MainTex, i.uv0 * _ColMask_ST.xy + _ColMask_ST.zw).rgb;
    float3 tint = lerp(_Col0.rgb, _Col1.rgb, mask.r);
    tint = lerp(tint, _Col2.rgb, mask.g);
    tint = lerp(tint, _Col3.rgb, mask.b);
    float3 color = mainColor * tint;
    float2 uv1 = i.skinOverlayUV12.xy;
    float radial = saturate(length(uv1 - 0.5) * 16.6666698 - 1.0);
    float2 scale = _nipsize * float2(-1.4, 0.7) + float2(2.0, -0.5);
    float2 shaped = lerp(uv1, uv1 * scale.x + scale.y, radial);
    float2 overUV = lerp(uv1, shaped, _nip) * i.skinOverlayUV3Color.z;
    float4 over1 = SAMPLE_TEX2D(_overtex1, overUV * _overtex1_ST.xy + _overtex1_ST.zw);
    float3 over1Color = lerp(over1.rgb * _overcolor1.rgb,
        over1.r * (_overcolor1.rgb + over1.g * _nip_specular * 0.33), saturate(_tex1mask));
    color = lerp(color, over1Color, saturate(over1.a * _overcolor1.a));
    overUV = i.skinOverlayUV12.zw * i.skinOverlayUV3Color.w;
    float4 over2 = SAMPLE_TEX2D(_overtex2, overUV * _overtex2_ST.xy + _overtex2_ST.zw);
    color = lerp(color, over2.rgb * _overcolor2.rgb, saturate(over2.a * _overcolor2.a));
    overUV = i.skinOverlayUV3Color.xy;
    float4 over3 = SAMPLE_TEX2D(_overtex3, overUV * _overtex3_ST.xy + _overtex3_ST.zw);
    return lerp(color, over3.rgb * _overcolor3.rgb, saturate(over3.a * _overcolor3.a));
}

void TomSkinApplySurface(TomVaryings i, float orientation, inout TomMaterialData m)
{
    m.skin.detail = SAMPLE_TEX2D(_DetailMask, i.uv0 * _DetailMask_ST.xy + _DetailMask_ST.zw);
    m.skin.lines = SAMPLE_TEX2D_SAMPLER(_LineMask, _DetailMask, i.uv0 * _LineMask_ST.xy + _LineMask_ST.zw);
    m.skin.control = SAMPLE_TEX2D_SAMPLER(_SkinControlMap, _DetailMask, i.uv0 * _SkinControlMap_ST.xy + _SkinControlMap_ST.zw);
    m.skin.diffuseNormalWS = normalize(lerp(m.mainNormalWS, m.shadingNormalWS, saturate(_SkinDiffuseNormalDetail)));
    UNITY_BRANCH
    if (_SkinFaceNormalStrength > 0.0)
    {
        float face = SAMPLE_TEX2D_SAMPLER(_NormalMask, _DetailMask, i.uv0 * _NormalMask_ST.xy + _NormalMask_ST.zw).g;
        m.skin.diffuseNormalWS = normalize(lerp(m.skin.diffuseNormalWS, m.geometricNormalWS,
            saturate(face * _SkinFaceNormalStrength)));
        m.toonNormalWS = normalize(lerp(m.toonNormalWS, m.geometricNormalWS,
            saturate(face * _SkinFaceNormalStrength)));
    }
    float detail = max(_DetailNormalMapScale, 0.0);
    float shade = saturate(min(1.0 - m.skin.detail.g, 1.0 - m.skin.lines.b * detail));
    float lineBase = max(0.8 * (1.0 - saturate(_linewidthG)) + 0.2, 0.0001);
    float lineFactor = saturate(min(pow(lineBase, max(m.skin.lines.g, 0.0)), 1.0 - 0.5 * m.skin.lines.r * detail));
    float3 lineColor = lerp(_SkinLineColor.rgb, _LineColorG.rgb, saturate(_SkinGameLineColor));
    m.skin.diffuseTint = lerp(1.0.xxx, _ShadowColor.rgb, saturate((1.0 - shade) * _SkinShadeStrength))
        * lerp(1.0.xxx, lineColor, saturate((1.0 - lineFactor) * _linetexon * _SkinLineStrength));
    float regionGloss = max(saturate(m.skin.detail.a) * max(_SpecularPower, 0.0),
        (1.0 - saturate(m.skin.detail.a)) * max(_SpecularPowerNail, 0.0));
    m.skin.gloss = lerp(1.0, regionGloss, saturate(_SkinGameGloss));
    TomLiquidApplySurface(i, orientation, m);
    m.skin.liquidCoverage = m.liquid.coverage;
    m.skin.liquidNormalWS = m.liquid.normalWS;
    float wet = saturate(m.skin.control.b * _SkinWetness);
    m.skin.coatCoverage = _SkinCoatCoverage < 0.5 ? 1.0 :
        (_SkinCoatCoverage < 1.5 ? m.skin.liquidCoverage :
        (_SkinCoatCoverage < 2.5 ? wet : max(wet, m.skin.liquidCoverage)));
}

void TomSkinFinishMaterial(TomVaryings i, inout TomMaterialData m)
{
    float highlight = lerp(1.0, saturate(m.skin.detail.r), saturate(_UseDetailRAsSpecularMap));
    UNITY_BRANCH
    if (_SkinPatternStrength > 0.0 && _notusetexspecular < 0.999)
    {
        float3 view = TomInterfaceView(i.posWS);
        float2 shifted = i.uv0 + 0.8 * (_SpeclarHeight - 1.0) *
            float2(dot(normalize(i.tangentWS.xyz), view), dot(normalize(i.bitangentWS), view));
        float pattern = SAMPLE_TEX2D(_DetailMask, shifted * _DetailMask_ST.xy + _DetailMask_ST.zw).r;
        highlight *= lerp(1.0, saturate(pattern * 1.6666667), saturate(_SkinPatternStrength * (1.0 - _notusetexspecular)));
    }
    m.specularMask *= m.skin.gloss * highlight;
    m.layerMask.g *= saturate(1.0 - m.skin.detail.b);
    int debug = (int)floor(_SkinDebugView + 0.5);
    m.skin.debug = m.albedo;
    if (debug == 2) m.skin.debug = m.skin.detail.rgb;
    else if (debug == 3) m.skin.debug = m.skin.lines.rgb;
    else if (debug == 4) m.skin.debug = m.skin.liquidCoverage.xxx;
    else if (debug == 5) m.skin.debug = m.skin.diffuseNormalWS * 0.5 + 0.5;
    else if (debug == 6) m.skin.debug = m.skin.gloss.xxx;
    else if (debug == 7) m.skin.debug = float3(frac(i.skinOverlayUV12.xy), 0);
    else if (debug == 8) m.skin.debug = float3(frac(i.skinOverlayUV12.zw), 0);
    else if (debug == 9) m.skin.debug = float3(frac(i.skinOverlayUV3Color.xy), 0);
    else if (debug == 10) m.skin.debug = m.skin.control.rgb;
    else if (debug == 11) m.skin.debug = TomSkinCoverage(i.uv0).xxx;
    else if (debug == 12) m.skin.debug = m.skin.liquidNormalWS * 0.5 + 0.5;
    else if (debug == 13) m.skin.debug = m.skin.diffuseTint;
}
#endif
