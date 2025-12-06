Shader "EcoCore/CloudShell"
{
    Properties
    {
        _CloudColor   ("Cloud Color", Color) = (1,1,1,1)
        _CloudOpacity ("Cloud Opacity", Range(0,1)) = 0.9

        _CloudFrequency ("Cloud Frequency", Float) = 0.25
        _CoverageBias   ("Coverage Bias", Range(0,1)) = 0.4
        _CloudSeed      ("Cloud Seed", Float) = 42

        _NoiseOffset ("Noise Offset", Vector) = (0,0,0,0)
        _PlanetCenter ("Planet Center", Vector) = (0,0,0,0)

        _ClearanceRadius ("Clearance Radius", Float) = 15
        _ClearanceFade   ("Clearance Fade", Float) = 5

        _SoftBottom ("Bottom Softness", Range(0,1)) = 0.2
        _SoftTop    ("Top Softness", Range(0,1)) = 0.2

        _CloudInnerRadius ("Cloud Inner Radius", Float) = 51
        _CloudOuterRadius ("Cloud Outer Radius", Float) = 55

        // Latitudinal banding (|sin(lat)|)
        _EqBandLimit   ("Equator Band Limit |sin(lat)|", Range(0,1)) = 0.25
        _MidBandLimit  ("Mid Band Limit |sin(lat)|",     Range(0,1)) = 0.6

        _EqBandSpeed    ("Equator Band Speed", Float) = 1.0
        _MidBandSpeed   ("Mid Band Speed",    Float) = 0.7
        _PolarBandSpeed ("Polar Band Speed",  Float) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1; // y = height factor 0..1 (if you want)
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  height      : TEXCOORD3;
            };

            float4 _CloudColor;
            float  _CloudOpacity;

            float  _CloudFrequency;
            float  _CoverageBias;
            float  _CloudSeed;

            float4 _NoiseOffset;
            float4 _PlanetCenter;

            float  _ClearanceRadius;
            float  _ClearanceFade;

            float  _SoftBottom;
            float  _SoftTop;

            float  _CloudInnerRadius;
            float  _CloudOuterRadius;

            float  _EqBandLimit;
            float  _MidBandLimit;

            float  _EqBandSpeed;
            float  _MidBandSpeed;
            float  _PolarBandSpeed;

            // --- simple 2D value noise (0..1) -------------------------

            float hash21(float2 p)
            {
                p = frac(p * 0.3183099 + float2(0.71, 0.113));
                return frac(23.17 * dot(p, float2(p.x + 0.11, p.y + 0.17)));
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Compute band-specific offset from global _NoiseOffset and latitude
            float3 ComputeBandFlow(float3 dir)
            {
                float lat = dir.y;          // sin(latitude), -1..1
                float s   = abs(lat);       // 0 at equator, 1 at poles
            
                // Smooth band weights
                float wEq  = 1.0 - s;              // strongest near equator
                float wMid = 4.0 * s * (1.0 - s);  // strongest mid-lats
                float wPol = s;                    // strongest near poles
            
                float sum = max(wEq + wMid + wPol, 1e-4);
                wEq  /= sum;
                wMid /= sum;
                wPol /= sum;
            
                // Blend band speeds into a scalar “zonal” speed at this latitude
                // (you can set _EqBandSpeed/_MidBandSpeed/_PolarBandSpeed positive/negative
                // to make some bands run opposite directions)
                float zonalSpeed = wEq * _EqBandSpeed
                                 + wMid * _MidBandSpeed
                                 + wPol * _PolarBandSpeed;
            
                // East: tangent around spin axis (Y)
                float3 east = normalize(float3(-dir.z, 0.0001, dir.x));
            
                // For now: ONLY zonal flow (no meridional poleward source),
                // so we don’t “spawn” everything at the equator.
                return east * zonalSpeed;
            }

            // --- 3D value noise (0..1) -----------------------------------------
            float hash31(float3 p)
            {
                // simple, cheap hash; good enough for clouds
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            
            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
            
                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));
            
                float3 u = f * f * (3.0 - 2.0 * f);
            
                float n00 = lerp(n000, n100, u.x);
                float n10 = lerp(n010, n110, u.x);
                float n01 = lerp(n001, n101, u.x);
                float n11 = lerp(n011, n111, u.x);
            
                float n0 = lerp(n00, n10, u.y);
                float n1 = lerp(n01, n11, u.y);
            
                return lerp(n0, n1, u.z);
            }

            float sampleSphereNoiseScrolled(float3 dir, float3 flowDir)
            {
                float freq = _CloudFrequency;
                float t    = _NoiseOffset.x;  // driven by CloudManager (simulated time)
            
                // Base position on a sphere
                float3 baseP = dir * freq;
            
                // Advect along local wind field
                float3 advected = baseP + flowDir * t;
            
                // Use t as a 3rd dimension so clouds morph (appear/disappear)
                // rather than just sliding in from “offscreen”.
                float timeWarp = t * 0.25;   // how quickly shapes change; tweak to taste
            
                float3 p = float3(advected.xy, advected.z + timeWarp) + _CloudSeed;
            
                return noise3D(p);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                float3 worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                float3 worldNormal = normalize(worldPos - _PlanetCenter.xyz);

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.worldPos    = worldPos;
                OUT.worldNormal = worldNormal;
                OUT.uv          = IN.uv;
                OUT.height      = saturate(IN.uv2.y);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Direction from planet center (for sphere noise)
                float3 dir  = normalize(IN.worldPos - _PlanetCenter.xyz);
                float3 flow = ComputeBandFlow(dir);
                float n = sampleSphereNoiseScrolled(dir, flow);

                float density = 0.0;
                if (n >= _CoverageBias)
                {
                    density = saturate((n - _CoverageBias) / max(1.0 - _CoverageBias, 1e-4));
                }

                // 2. Radial soft fade across the whole shell
                float r      = distance(IN.worldPos, _PlanetCenter.xyz);
                float innerR = _CloudInnerRadius;
                float outerR = _CloudOuterRadius;
                float shellT = max(outerR - innerR, 1e-4);

                float radialT = saturate((r - innerR) / shellT);

                float bottomFade = smoothstep(0.0, _SoftBottom, radialT);
                float topFade    = smoothstep(1.0, 1.0 - _SoftTop, radialT);

                density *= bottomFade * topFade;

                // 3. Camera clearance bubble (_WorldSpaceCameraPos is provided by Unity)
                float inner = _ClearanceRadius;
                float outer = _ClearanceRadius + _ClearanceFade;

                if (outer > inner + 1e-4)
                {
                    float3 camPosWS = GetCameraPositionWS();
                    float dist = distance(camPosWS, IN.worldPos);

                    if (dist <= inner)
                    {
                        density = 0.0;
                    }
                    else if (dist <= outer)
                    {
                        float t = saturate((dist - inner) / (outer - inner));
                        density *= t;
                    }
                }

                density = saturate(density);
                float alpha = density * _CloudOpacity;

                if (alpha <= 0.001)
                    discard;

                float3 col = _CloudColor.rgb;
                return half4(col, alpha);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
