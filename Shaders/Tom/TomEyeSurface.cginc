#ifndef TOM_EYE_SURFACE_INC
#define TOM_EYE_SURFACE_INC

// Depth is positive into the eye. Coverage here controls optics, never alpha/stencil.
float2 TomEyeSurface(float2 uv, float2 dx, float2 dy)
{
    float2 surface = 0.0;
    UNITY_BRANCH
    if (_UseEyeSurfaceMask > 0.5)
    {
        // Final iris UV, including its rotation and MainTex ST. No second map transform.
        float4 map = saturate(_EyeSurfaceMap.SampleGrad(sampler_MainTex, uv, dx, dy));
        float depth = 1.0 - map.r;
        surface = _EyeSurfaceMapMode > 0.5 ? float2(map.r * map.a, map.a)
            : float2(depth, saturate(depth / 0.0001));
    }
    else
    {
        float2 q = (uv - float2(_IrisCenterX, _IrisCenterY)) / max(float2(_IrisRadiusX, _IrisRadiusY), 0.001);
        float r = length(q);
        float coverage = 1.0 - smoothstep(1.0 - max(_EyeOpticsEdgeFade, 0.01), 1.0, r);
        float depth = _IrisDepthShape < 0.5 ? 1.0 : (_IrisDepthShape < 1.5 ? saturate(1.0 - r) : saturate(1.0 - r * r));
        surface = float2(r < 1.0 ? depth : 0.0, coverage);
    }
    return surface;
}

float2 TomEyeSurfaceHit(float2 uv, float2 travel, float2 dx, float2 dy, out float valid)
{
    float2 origin = TomEyeSurface(uv, dx, dy);
    travel *= origin.y;
    valid = 1.0;
    if (origin.x <= 0.000001) return uv;

    // A shallow smooth field has one local root t = depth(uv + travel*t).
    // Bounded secant refinement is not an occlusion/first-hit ray march.
    float lo = 0.0, hi = 1.0;
    float flo = -origin.x, fhi = 1.0 - TomEyeSurface(uv + travel, dx, dy).x;
    float t = origin.x, residual = 0.0;
    [unroll]
    for (int iteration = 0; iteration < 6; iteration++)
    {
        t = lerp(lo, hi, saturate(-flo / max(fhi - flo, 0.000001)));
        residual = t - TomEyeSurface(uv + travel * t, dx, dy).x;
        if (residual < 0.0) { lo = t; flo = residual; }
        else { hi = t; fhi = residual; }
    }
    // Unsupported steep/noisy inputs fade back instead of leaving unstable hits.
    valid = 1.0 - smoothstep(0.002, 0.02, abs(residual));
    return uv + travel * t;
}

#endif
