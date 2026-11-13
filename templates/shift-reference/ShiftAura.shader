// SHIFT/Aura: a thin additive shell pushed out along the normals, lit by fresnel, with red streaks flowing upward inside it.
// Drawn on a copy of each body renderer (same mesh, same bones), toggled with the gimmick.
Shader "SHIFT/Aura"
{
    Properties
    {
        _Color ("Color", Color) = (0.85, 0.04, 0.07, 1)
        _Strength ("Strength", Range(0, 2)) = 0.6
        _Width ("Shell width (m)", Float) = 0.003
        _Flow ("Flow lines", Range(0, 2)) = 1
        _FlowSpeed ("Flow speed (m/s)", Float) = 1.6
        _FlowFreq ("Flow lines per metre", Float) = 14
        _Power ("Fresnel power", Float) = 2.2
        _TimeOverride ("Time override (editor previews, <0 = off)", Float) = -1
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "VRCFallback"="Hidden" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Offset -1, -1
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wpos : TEXCOORD0; float3 wnrm : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };

            float4 _Color; float _Strength, _Width, _Flow, _FlowSpeed, _FlowFreq, _Power, _TimeOverride;

            float hash(float2 p) { float3 p3 = frac(float3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return frac((p3.x + p3.y) * p3.z); }
            float vnoise(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f); return lerp(lerp(hash(i), hash(i + float2(1, 0)), f.x), lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x), f.y); }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float t = _TimeOverride >= 0 ? _TimeOverride : _Time.y;
                float3 wn = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                // the shell hugs the surface (depth-tested against the body, so parts hidden under clothes stay hidden) and breathes a little
                float pulse = 1.0 + 0.25 * sin(t * 5.0 + wp.y * 9.0);
                wp += wn * _Width * pulse;
                o.wpos = wp; o.wnrm = wn;
                o.pos = UnityWorldToClipPos(wp);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _TimeOverride >= 0 ? _TimeOverride : _Time.y;
                float3 vdir = normalize(_WorldSpaceCameraPos - i.wpos);
                float fres = pow(1.0 - saturate(dot(normalize(i.wnrm), vdir)), _Power);
                // flow lines: thin bands travelling upward, broken up by noise so they look like energy streaks
                float n = vnoise(i.wpos.xz * 9.0 + i.wpos.y * 1.5);
                float band = frac(i.wpos.y * _FlowFreq - t * _FlowSpeed * _FlowFreq * 0.25 + n * 2.0);
                float lines = smoothstep(0.85, 0.97, band) * smoothstep(0.4, 0.8, n) * _Flow;
                // rim glow + a faint red wash over the whole body + the flowing lines, strongest near the silhouette
                float a = (fres * 0.7 + 0.05) * _Strength + lines * (0.12 + 0.5 * fres) * _Strength;
                float3 col = _Color.rgb * a + lines * fres * _Strength * float3(0.3, 0.03, 0.03);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
