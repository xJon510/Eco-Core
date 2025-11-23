Shader "Custom/CubeSphereGridLit"
{
    Properties
    {
        _Color        ("Base Color", Color) = (0.3,0.5,0.3,1)
        _GridColor    ("Grid Color", Color) = (0,0,0,1)
        _Thickness    ("Grid Thickness", Range(0.001, 0.1)) = 0.02
        _CellsPerFace ("Cells Per Face", Float) = 8

        _Metallic     ("Metallic", Range(0,1)) = 0.0
        _Smoothness   ("Smoothness", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalRenderPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
            "LightMode"      = "UniversalForward"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // enable URP lighting variants
            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct CubeSphereAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct CubeSphereVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            float4 _Color;
            float4 _GridColor;
            float  _Thickness;
            float  _CellsPerFace;
            float  _Metallic;
            float  _Smoothness;

            CubeSphereVaryings vert (CubeSphereAttributes v)
            {
                CubeSphereVaryings o;

                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 nrmWS = TransformObjectToWorldNormal(v.normalOS);

                o.positionWS  = posWS;
                o.normalWS    = normalize(nrmWS);
                o.positionHCS = TransformWorldToHClip(posWS);
                o.uv          = v.uv;

                // shadow coord for main light
                o.shadowCoord = TransformWorldToShadowCoord(posWS);

                return o;
            }

            float3 EvaluateOneLight(Light light, float3 albedo, float3 N, float3 V)
            {
                float3 L = normalize(light.direction);
                float  NdotL = saturate(dot(N, L));
                if (NdotL <= 0.0)
                    return 0.0;

                float3 H = normalize(L + V);
                float  NdotH = saturate(dot(N, H));

                // In URP, light.color already includes attenuation & shadows
                float3 radiance = light.color;

                float3 diffuse  = albedo * NdotL * radiance;

                float shininess = lerp(8.0, 128.0, _Smoothness);
                float  spec     = pow(NdotH, shininess) * _Metallic;
                float3 specular = spec * radiance;

                return diffuse + specular;
            }

            float3 ApplyLighting(float3 albedo, CubeSphereVaryings i)
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);

                float3 color = 0;

                // main light (directional)
                Light mainLight = GetMainLight(i.shadowCoord);
                color += EvaluateOneLight(mainLight, albedo, N, V);

                // additional lights: point / spot / extra directional
                #if defined(_ADDITIONAL_LIGHTS)
                int additionalCount = GetAdditionalLightsCount();
                for (int li = 0; li < additionalCount; li++)
                {
                    Light l = GetAdditionalLight(li, i.positionWS);
                    color += EvaluateOneLight(l, albedo, N, V);
                }
                #endif

                return color;
            }

            float4 frag (CubeSphereVaryings i) : SV_Target
            {
                // --- grid mask ---
                float2 uvFace = i.uv;
                float2 uv = frac(uvFace * _CellsPerFace);

                float d = min(
                    min(uv.x, 1.0 - uv.x),
                    min(uv.y, 1.0 - uv.y)
                );

                bool isGrid = (d < _Thickness);

                // base colors
                float3 baseCol = _Color.rgb;
                float3 gridCol = _GridColor.rgb;

                // lit colors
                float3 litBase = ApplyLighting(baseCol, i);
                float3 litGrid = ApplyLighting(gridCol, i);

                float3 finalRGB = isGrid ? litGrid : litBase;
                float  finalA   = isGrid ? _GridColor.a : _Color.a;

                return float4(finalRGB, finalA);
            }
            ENDHLSL
        }
    }
}
