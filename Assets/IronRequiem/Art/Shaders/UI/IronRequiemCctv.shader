// Iron Requiem — CCTV monitor look for a UI RawImage (12/11/2026).
// Monochrome green tint, scanlines, film noise, vignette and occasional horizontal tearing (_Glitch, driven by
// CctvMonitor). Only this UI element uses it, so the scene's post-processing is untouched.
Shader "IronRequiem/UI/CCTV"
{
    Properties
    {
        [PerRendererData] _MainTex ("Feed", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Phosphor ("Phosphor", Color) = (0.55, 1, 0.65, 1)
        _Lines ("Scanlines", Float) = 160
        _Noise ("Noise", Range(0,1)) = 0.12
        _Gain ("Gain", Range(1,8)) = 3.5
        _Glitch ("Glitch", Range(0,1)) = 0
        _NoSignal ("No signal (static only)", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex; fixed4 _Color, _Phosphor; float _Lines, _Noise, _Glitch, _NoSignal, _Gain;
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color * _Color; return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y; float2 uv = i.uv;
                float band = floor(uv.y * 48.0);
                uv.x += (hash(float2(band, floor(t * 24.0))) - 0.5) * 0.09 * _Glitch;
                uv.y += _Glitch * 0.015 * sin(t * 90.0);
                float3 feed = tex2D(_MainTex, uv).rgb;
                float lum = dot(feed, float3(0.299, 0.587, 0.114));
                lum = saturate(pow(saturate(lum * _Gain), 0.7)); // night-vision style gain: the hangar is dark
                float n = hash(i.uv * float2(320.0, 180.0) + frac(t * 13.0));
                lum = lerp(lum, n, _NoSignal);
                float scan = 0.72 + 0.28 * sin(i.uv.y * _Lines * 6.2831);
                float roll = 0.92 + 0.08 * sin((i.uv.y + t * 0.35) * 6.2831);
                float2 c = i.uv - 0.5; float vig = saturate(1.0 - dot(c, c) * 1.6);
                float3 col = _Phosphor.rgb * (lum * scan * roll + (n - 0.5) * _Noise) * vig;
                return fixed4(col, 1) * i.color;
            }
            ENDCG
        }
    }
}
