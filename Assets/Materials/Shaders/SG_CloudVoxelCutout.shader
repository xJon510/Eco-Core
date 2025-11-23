Shader "Unlit/CloudVoxelCutout"
{
    Properties
    {
        _MainTex           ("Texture", 2D) = "white" {}
        _CloudColor        ("Cloud Color", Color) = (0.9, 0.95, 1, 1)
        _AlphaClipThreshold("Alpha Clip Threshold", Range(0,1)) = 0.2
        _DensityContrast   ("Density Contrast", Range(0.1,4)) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="TransparentCutout"
        }
        LOD 100

        // Cutout-style transparency
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            fixed4 _CloudColor;
            float  _AlphaClipThreshold;
            float  _DensityContrast;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;     // from mesh: a = density
            };

            struct v2f
            {
                float2 uv      : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex  : SV_POSITION;
                fixed4 color   : COLOR;    // carry vertex color to fragment
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Base texture (can just be white)
                fixed4 texCol = tex2D(_MainTex, i.uv);

                // Density from vertex color alpha
                // (your script sets this per-column)
                float density = saturate(i.color.a);

                // Optional contrast shaping
                density = pow(density, _DensityContrast);

                // Start from cloud color * texture
                fixed4 col = _CloudColor * texCol;

                // Apply density to RGB for softer edges
                col.rgb *= density;

                // Final alpha from color alpha * density
                col.a *= density;

                // Hard cutout based on threshold
                clip(col.a - _AlphaClipThreshold);

                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
