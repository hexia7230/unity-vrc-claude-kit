// SHIFT/Streak: additive soft streak for the particle system (stretched billboards), vertex colour drives the colour and fade.
Shader "SHIFT/Streak"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+11" "RenderType"="Transparent" "IgnoreProjector"="True" "VRCFallback"="Hidden" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            float4 _Color;
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color * _Color; o.uv = v.uv; return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // soft across the width, fading tail along the length
                float w = 1.0 - abs(i.uv.y * 2.0 - 1.0); w = w * w;
                float l = smoothstep(0.0, 0.25, i.uv.x) * smoothstep(1.0, 0.6, i.uv.x);
                float a = w * l * i.color.a;
                return fixed4(i.color.rgb * a, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
