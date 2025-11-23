Shader "Custom/ScrollingCloudNoise"
{
    Properties
    {
        _CloudColor   ("Cloud Color", Color) = (1,1,1,1)
        _BasePuff     ("Base Puff Height", Float) = 1.0
        _MaxExtraPuff ("Max Extra Puff Height", Float) = 2.0

        _NoiseFreq    ("Noise Frequency", Float) = 1.0
        _CoverageBias ("Coverage Bias", Range(0,1)) = 0.4

        _Wind         ("Wind Direction (XZ)", Vector) = (0.2, 0.0, 0.0, 0.0)
        _NoiseSpeed   ("Noise Scroll Speed", Float) = 0.1

        _AlphaCutoff  ("Alpha Cutoff", Range(0,1)) = 0.2
        _PlanetCenter ("Planet Center (world)", Vector) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="TransparentCutout" }
        Cull Back
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CloudColor;
            float  _BasePuff;
            float  _MaxExtraPuff;

            float  _NoiseFreq;
            float  _CoverageBias;

            float4 _Wind;       // xz used
            float  _NoiseSpeed;

            float  _AlphaCutoff;
            float4 _PlanetCenter;

            // --- simple hash-based "value noise" ---
            float hash3(float3 p)
            {
                // cheap but fine for clouds
                float h = dot(p, float3(12.9898, 78.233, 37.719));
                return frac(sin(h) * 43758.5453);
            }

            float noise3(float3 p)
            {
                // could do more fancy stuff, but single-sample is ok for big blobs
                return hash3(p);
            }

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos     : SV_POSITION;
                float  density : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;

                // world position of base vertex (before puff)
                float4 worldPos4 = mul(unity_ObjectToWorld, v.vertex);
                float3 worldPos  = worldPos4.xyz;

                // direction from planet center
                float3 dir = normalize(worldPos - _PlanetCenter.xyz);

                // build a 3D noise coordinate:
                // - scale by frequency
                // - scroll over time using wind + time
                float t = _Time.y * _NoiseSpeed;
                float3 windOffset = float3(_Wind.x, 0.0, _Wind.z) * t;

                float3 noiseP = dir * _NoiseFreq + windOffset;

                // noise in [0,1]
                float n = noise3(noiseP);

                // apply coverage bias
                float density;
                if (n < _CoverageBias)
                    density = 0.0;
                else
                    density = saturate((n - _CoverageBias) / (1.0 - _CoverageBias));

                // puff height based on density
                float puff = _BasePuff + _MaxExtraPuff * density;

                // offset vertex along its normal in OBJECT space
                float3 objOffset = v.normal * puff;
                float4 objPos    = v.vertex + float4(objOffset, 0.0);

                o.pos     = UnityObjectToClipPos(objPos);
                o.density = density;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float alpha = i.density;

                // discard very thin clouds
                clip(alpha - _AlphaCutoff);

                fixed4 col = _CloudColor;
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}
