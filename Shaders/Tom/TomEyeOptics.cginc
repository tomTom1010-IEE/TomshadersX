#ifndef TOM_EYE_OPTICS_INC
#define TOM_EYE_OPTICS_INC

#if defined(TOM_EYE_IRIS)
float4 _MainTex_TexelSize;
#include "TomEyeSurface.cginc"

struct TomEyeChart
{
    float3 ju, jv;
    float3 gramInverse;
    float depth;
    float valid;
};

TomEyeChart TomEyeBuildChart(float3 px, float3 py, float2 qx, float2 qy, float3 normalWS)
{
    TomEyeChart chart;
    float determinant = qx.x * qy.y - qx.y * qy.x;
    float relativeDet = abs(determinant) / max(length(qx) * length(qy), 1e-30);
    float inverseDet = (determinant < 0.0 ? -1.0 : 1.0) / max(abs(determinant), 1e-30);
    float3 ju = (px * qy.y - py * qx.y) * inverseDet;
    float3 jv = (py * qx.x - px * qy.x) * inverseDet;
    ju -= normalWS * dot(ju, normalWS);
    jv -= normalWS * dot(jv, normalWS);
    float scale = max(max(length(ju), length(jv)), 1e-15);
    chart.ju = ju / scale;
    chart.jv = jv / scale;
    float guu = dot(chart.ju, chart.ju), gvv = dot(chart.jv, chart.jv), guv = dot(chart.ju, chart.jv);
    float gd = guu * gvv - guv * guv;
    chart.gramInverse = float3(gvv, -guv, guu) / max(gd, 1e-12);
    chart.depth = max(_IrisDepth, 0.0) * sqrt(length(chart.ju) * length(chart.jv));
    chart.valid = relativeDet > 0.0001 && gd > 0.0001 * max(guu * gvv, 1e-12)
        && min(_IrisRadiusX, _IrisRadiusY) > 0.0
        && min(abs(_MainTex_ST.x), abs(_MainTex_ST.y)) > 0.000001 ? 1.0 : 0.0;
    return chart;
}

float2 TomEyeHit(TomEyeChart chart, float2 uv, float2 ux, float2 uy, float3 viewDir, float3 interfaceNormal,
    float3 geometric, float ior, out float valid)
{
    float3 ray = refract(-normalize(viewDir), normalize(interfaceNormal), 1.0 / max(ior, 1.0));
    float inward = -dot(ray, geometric);
    float t = chart.depth / max(inward, 0.025);
    float3 delta = t * (ray - geometric * dot(ray, geometric));
    float2 rhs = float2(dot(chart.ju, delta), dot(chart.jv, delta));
    float2 dq = float2(dot(chart.gramInverse.xy, rhs), dot(chart.gramInverse.yz, rhs));
    dq *= min(1.0, max(_EyeOpticsMaxOffset, 0.0) / max(length(dq), 0.000001));
    float2 radius = max(float2(_IrisRadiusX, _IrisRadiusY), 0.001);
    float grazing = smoothstep(0.025, 0.12, inward) * smoothstep(0.01, 0.10, dot(viewDir, geometric));
    valid = chart.valid * (inward > 0.025 ? 1.0 : 0.0);
    float2 hit = uv;
    UNITY_BRANCH
    if (_UseEyeSurfaceMask > 0.5 || _IrisDepthShape >= 0.5)
    {
        float surfaceValid = 1.0;
        hit = TomEyeSurfaceHit(uv, radius * dq * saturate(_EyeOpticsStrength) * grazing * valid,
            ux, uy, surfaceValid);
        valid *= surfaceValid;
    }
    else
    {
        // Preserve the original flat/ellipse path, including its edge and aperture guards.
        float2 q = (uv - float2(_IrisCenterX, _IrisCenterY)) / radius;
        dq *= min(1.0, 0.8 * max(0.0, 1.0 - length(q)) / max(length(dq), 0.000001));
        float edge = 1.0 - smoothstep(1.0 - max(_EyeOpticsEdgeFade, 0.01), 1.0, length(q));
        hit = uv + radius * dq * edge * saturate(_EyeOpticsStrength) * grazing * valid;
    }
    return hit;
}

float2 TomEyeToRaw(float2 uv)
{
    float2 st = _MainTex_ST.xy;
    st = float2(st.x < 0.0 ? -1.0 : 1.0, st.y < 0.0 ? -1.0 : 1.0) * max(abs(st), 0.000001);
    return TomEyeRotate((uv - _MainTex_ST.zw) / st, _rotation);
}

float2 TomEyeRawGradient(float2 gradient)
{
    return TomEyeToRaw(_MainTex_ST.zw + gradient) - TomEyeToRaw(_MainTex_ST.zw);
}

float TomEyeGradientRadius(float2 dx, float2 dy)
{
    float xx = dot(dx, dx), yy = dot(dy, dy), xy = dot(dx, dy);
    return sqrt(max(0.0, 0.5 * (xx + yy + sqrt((xx - yy) * (xx - yy) + 4.0 * xy * xy))));
}

float2 TomEyeGradientCoordinates(float2 value, float2 dx, float2 dy, float inverseDet)
{
    return float2(dy.y * value.x - dy.x * value.y, dx.x * value.y - dx.y * value.x) * inverseDet;
}

void TomEyeRefractedRGB(TomVaryings i, float3 geometric, float3 interfaceNormal, float orientation,
    inout float3 rgb, inout float3 mainNormal, inout float3 combinedNormal,
    inout float3 toonNormal, out float3 debug)
{
    float2 uv = TomEyeMainUV(i.uv0);
    float2 radius = max(float2(_IrisRadiusX, _IrisRadiusY), 0.001);
    float2 q = (uv - float2(_IrisCenterX, _IrisCenterY)) / radius;
    debug = float3(q * 0.5 + 0.5, 0.0);
    // Derivatives only depend on interpolants/texture normals, never on a derivative-built hit.
    float2 ux = ddx(uv), uy = ddy(uv);
    float3 px = ddx(i.posWS), py = ddy(i.posWS);
    float3 v = TomInterfaceView(i.posWS), vx = ddx(v), vy = ddy(v);
    float3 nx = ddx(interfaceNormal), ny = ddy(interfaceNormal);
    float3 gx = ddx(geometric), gy = ddy(geometric);
    if (_EyeDebugView > 6.5)
    {
        float2 surface = TomEyeSurface(uv, ux, uy);
        debug = (_EyeDebugView < 7.5 ? surface.x : surface.y).xxx;
        return;
    }
    UNITY_BRANCH
    if (_EyeOpticsMode < 0.5 || _EyeOpticsStrength <= 0.0 || _IrisDepth <= 0.0)
    {
        if (_EyeDebugView > 3.5 && _EyeDebugView < 4.5) debug = float3(0.5, 0.5, 0.0);
        if (_EyeDebugView > 5.5 && _EyeDebugView < 6.5) debug = 0.0;
        return;
    }

    TomEyeChart chart = TomEyeBuildChart(px, py, ux / radius, uy / radius, geometric);
    float validity, unused;
    float ior = max(_EyeRefractionIOR, 1.0);
    float2 hit = TomEyeHit(chart, uv, ux, uy, v, interfaceNormal, geometric, ior, validity);
    // Hold the chart constant and propagate view/normal/UV derivatives through the local ray.
    float2 hx = TomEyeHit(chart, uv + ux, ux, uy, v + vx, interfaceNormal + nx, normalize(geometric + gx), ior, unused) - hit;
    float2 hy = TomEyeHit(chart, uv + uy, ux, uy, v + vy, interfaceNormal + ny, normalize(geometric + gy), ior, unused) - hit;
    float maxFootprint = 4.0 * max(length(ux) + length(uy), 0.0000001);
    if (length(hx) + length(hy) > maxFootprint) { hx = ux * 2.0; hy = uy * 2.0; }
    float2 red = hit, blue = hit;
    UNITY_BRANCH
    if (_EyeDispersion > 0.0)
    {
        float spread = (ior - 1.0) * saturate(_EyeDispersion) / 40.0;
        float vr, vb;
        red = TomEyeHit(chart, uv, ux, uy, v, interfaceNormal, geometric, ior - spread, vr);
        blue = TomEyeHit(chart, uv, ux, uy, v, interfaceNormal, geometric, ior + spread, vb);
        validity *= vr * vb;
        float2 redX = TomEyeHit(chart, uv + ux, ux, uy, v + vx, interfaceNormal + nx, normalize(geometric + gx), ior - spread, unused) - red;
        float2 redY = TomEyeHit(chart, uv + uy, ux, uy, v + vy, interfaceNormal + ny, normalize(geometric + gy), ior - spread, unused) - red;
        float2 blueX = TomEyeHit(chart, uv + ux, ux, uy, v + vx, interfaceNormal + nx, normalize(geometric + gx), ior + spread, unused) - blue;
        float2 blueY = TomEyeHit(chart, uv + uy, ux, uy, v + vy, interfaceNormal + ny, normalize(geometric + gy), ior + spread, unused) - blue;
        float determinant = hx.x * hy.y - hx.y * hy.x;
        if (abs(determinant) > 0.0001 * max(length(hx) * length(hy), 1e-30))
        {
            // Inflate the green ellipse only enough to enclose both chromatic Jacobians.
            float inverseDet = 1.0 / determinant;
            float redScale = TomEyeGradientRadius(TomEyeGradientCoordinates(redX, hx, hy, inverseDet),
                TomEyeGradientCoordinates(redY, hx, hy, inverseDet));
            float blueScale = TomEyeGradientRadius(TomEyeGradientCoordinates(blueX, hx, hy, inverseDet),
                TomEyeGradientCoordinates(blueY, hx, hy, inverseDet));
            float scale = max(1.0, max(redScale, blueScale));
            hx *= scale;
            hy *= scale;
        }
        else
        {
            float2 texelDimensions = max(abs(_MainTex_TexelSize.zw), 1.0);
            float width = max(TomEyeGradientRadius(hx * texelDimensions, hy * texelDimensions),
                max(TomEyeGradientRadius(redX * texelDimensions, redY * texelDimensions),
                    TomEyeGradientRadius(blueX * texelDimensions, blueY * texelDimensions)));
            hx = float2(width / texelDimensions.x, 0.0);
            hy = float2(0.0, width / texelDimensions.y);
        }
    }
    float2 footprint = max(abs(hx) + abs(hy), abs(_MainTex_TexelSize.xy)) * 2.0;
    float2 lo = float2(_EyeUVMinX, _EyeUVMinY) + footprint;
    float2 hi = float2(_EyeUVMaxX, _EyeUVMaxY) - footprint;
    float2 minHit = min(hit, min(red, blue)), maxHit = max(hit, max(red, blue));
    float2 safeDistance = min(minHit - lo, hi - maxHit);
    float safeFade = saturate(min(safeDistance.x / max(footprint.x, 1e-7), safeDistance.y / max(footprint.y, 1e-7)));
    float validBounds = all(hi > lo) ? 1.0 : 0.0;
    validity *= safeFade * validBounds;
    float2 safeHi = max(hi, lo);
    float3 refracted = _MainTex.SampleGrad(sampler_MainTex, clamp(hit, lo, safeHi), hx, hy).rgb;
    UNITY_BRANCH
    if (_EyeDispersion > 0.0)
    {
        refracted.r = _MainTex.SampleGrad(sampler_MainTex, clamp(red, lo, safeHi), hx, hy).r;
        refracted.b = _MainTex.SampleGrad(sampler_MainTex, clamp(blue, lo, safeHi), hx, hy).b;
    }
    rgb = lerp(rgb, refracted, validity);

    float2 raw = TomEyeToRaw(clamp(hit, lo, safeHi));
    float2 rx = TomEyeRawGradient(hx), ry = TomEyeRawGradient(hy);
    float3 bn = UnpackScaleNormal(_NormalMap.SampleGrad(sampler_NormalMap,
        raw * _NormalMap_ST.xy + _NormalMap_ST.zw, rx * _NormalMap_ST.xy, ry * _NormalMap_ST.xy), _NormalMapScale);
    float3 dn = UnpackScaleNormal(_NormalMapDetail.SampleGrad(sampler_NormalMap,
        raw * _NormalMapDetail_ST.xy + _NormalMapDetail_ST.zw, rx * _NormalMapDetail_ST.xy, ry * _NormalMapDetail_ST.xy), _DetailNormalMapScale);
    float3 cn = BlendNormals(bn, dn);
    float3 tangent = normalize(i.tangentWS.xyz), bitangent = normalize(i.bitangentWS) * orientation;
    mainNormal = normalize(lerp(mainNormal, normalize(bn.x * tangent + bn.y * bitangent + bn.z * geometric), validity));
    combinedNormal = normalize(lerp(combinedNormal, normalize(cn.x * tangent + cn.y * bitangent + cn.z * geometric), validity));
    toonNormal = normalize(lerp(toonNormal, TomToonNormalWS(bn, dn, tangent, bitangent, geometric), validity));
    if (_EyeDebugView > 3.5 && _EyeDebugView < 4.5) debug = float3(0.5 + (hit - uv) / radius, 0.0);
    if (_EyeDebugView > 5.5 && _EyeDebugView < 6.5) debug = float3(1.0 - chart.valid, 1.0 - validity, 0.0);
}
#endif

void TomEyeApplyOptics(TomVaryings i, TomEyeLayers eye, float orientation, inout TomMaterialData material)
{
    float3 rgb = eye.iris.rgb;
    material.eyeDebug = 0.0;
#if defined(TOM_EYE_IRIS)
    TomEyeRefractedRGB(i, material.geometricNormalWS, material.eyeInterfaceNormal, orientation,
        rgb, material.mainNormalWS, material.shadingNormalWS, material.toonNormalWS, material.eyeDebug);
    rgb = lerp(rgb, eye.expression.rgb, saturate(eye.expression.a));
#else
    // Game EyeW tint has a neutral value of 0.5; X lighting owns light intensity.
    rgb *= _Color.rgb * 2.0;
#endif
    material.albedo = rgb * _BaseColor.rgb;
    if (_EyeDebugView > 0.5 && _EyeDebugView < 1.5) material.eyeDebug = eye.coverage.xxx;
    else if (_EyeDebugView > 1.5 && _EyeDebugView < 2.5) material.eyeDebug = TomEyeStencilSurvival(eye.coverage).xxx;
    else if (_EyeDebugView > 4.5 && _EyeDebugView < 5.5) material.eyeDebug = material.eyeInterfaceNormal * 0.5 + 0.5;
}

#endif
