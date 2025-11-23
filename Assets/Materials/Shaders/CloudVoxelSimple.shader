Shader "Custom/CloudVoxelSimple"
{
    Properties
    {
        _CloudColor ("Cloud Color", Color) = (1,1,1,1)
        _PuffHeight ("Max Puff Height", Float) = 2.0
        _ChangeSpeed ("Change Speed", Float) = 0.2
        _AlphaCutoff ("Alpha Cutoff", Range(0,1)) = 0.2
        _UVOffset   ("UV Offset", Vector) = (0,0,0,0)
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _CloudColor;
            float _PuffHeight;
            float _ChangeSpeed;
            float _AlphaCutoff;
            float4 _UVOffset;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color  : COLOR;   // a = base density
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;

                float baseDensity = saturate(v.color.a);

                // Time wobble so clouds shrink/grow
                float t = _Time.y * _ChangeSpeed + baseDensity * 10.0;
                float wobble = 0.5 + 0.5 * sin(t);
                float finalDensity = saturate(baseDensity * wobble);

                // Height change
                float3 offset = v.normal * (_PuffHeight * finalDensity);
                float4 objPos = v.vertex + float4(offset, 0.0);
                o.pos = UnityObjectToClipPos(objPos);

                o.color.rgb = _CloudColor.rgb;
                o.color.a   = _CloudColor.a * finalDensity;

                // UV scroll
                o.uv = v.uv + _UVOffset.xy;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // You could sample a detail noise texture here using i.uv if you want.
                // For now we just use the animated alpha:

                clip(i.color.a - _AlphaCutoff);
                return i.color;
            }
            ENDCG
        }
    }
}
