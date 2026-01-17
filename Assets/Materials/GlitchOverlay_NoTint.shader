Shader "UI/GlitchOverlay_NoTint"
{
    Properties
    {
        // UI expects _MainTex + _Color sometimes; we keep them for compatibility.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Glitch controls
        _Intensity ("Intensity", Range(0, 1)) = 0
        _Opacity ("Max Opacity", Range(0, 1)) = 0.35
        _LineDensity ("Scanline Density", Range(50, 1500)) = 450
        _LineSharpness ("Scanline Sharpness", Range(1, 20)) = 8
        _NoiseScale ("Noise Scale", Range(1, 200)) = 80
        _NoiseSpeed ("Noise Speed", Range(0, 10)) = 3
        _TearAmount ("Horizontal Tear", Range(0, 0.05)) = 0.012
        _TearBands ("Tear Bands", Range(1, 60)) = 18
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;

            float _Intensity;
            float _Opacity;
            float _LineDensity;
            float _LineSharpness;
            float _NoiseScale;
            float _NoiseSpeed;
            float _TearAmount;
            float _TearBands;

            // Simple hash noise (stable + cheap)
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // NOTE:
                // This overlay does NOT sample the scene (UI overlay cannot distort scene color directly).
                // It only draws neutral white with alpha patterns, so the scene's colors remain unchanged.

                float t = _Time.y;

                // Scanlines (alpha-only)
                float scan = sin((i.uv.y * _LineDensity) + (t * 60.0));
                scan = abs(scan);                         // 0..1-ish
                scan = pow(saturate(scan), _LineSharpness); // sharpen
                // invert so thin lines pop
                float scanAlpha = 1.0 - scan;

                // Noise (alpha-only)
                float2 np = i.uv * _NoiseScale;
                np.y += t * _NoiseSpeed;
                float n = hash21(floor(np));
                // Make noise “sparkly” rather than constant fog
                float noiseAlpha = step(0.75, n);         // 0 or 1 mostly

                // Horizontal tearing bands (still alpha-only)
                float band = floor(i.uv.y * _TearBands);
                float bandRand = hash21(float2(band, floor(t * 12.0)));
                float tearMask = step(0.85, bandRand);    // occasional band tears

                // Combine alpha sources
                float a = 0.0;
                a += scanAlpha * 0.55;
                a += noiseAlpha * 0.45;
                a = saturate(a);

                // Apply intensity + max opacity cap
                a *= _Intensity * _Opacity;

                // Also punch up tearing bands a bit (still no tint)
                a += tearMask * (_Intensity * _Opacity * 0.35);
                a = saturate(a);

                // Output neutral white (no color shift), alpha controls visibility
                fixed4 col = fixed4(1, 1, 1, a);

                // If your RawImage has a texture, you can keep it invisible by setting its texture to white.
                // We ignore _MainTex on purpose to avoid tint.
                return col;
            }
            ENDCG
        }
    }
}
