// Banded dusk sky with a sun, far mountain ridges and stars. Blends to flat grey in a storm.
Shader "Ski/Sky"
{
    Properties
    {
        _Top ("Top", Color) = (0.17,0.18,0.37,1)
        _Mid ("Mid", Color) = (0.55,0.45,0.62,1)
        _Horizon ("Horizon", Color) = (0.95,0.62,0.48,1)
        _Bottom ("Below horizon", Color) = (0.49,0.56,0.76,1)
        _SunDir ("Sun direction", Vector) = (0.3,0.25,0.9,0)
        _SunColor ("Sun colour", Color) = (1,0.85,0.6,1)
        _RidgeFar ("Far ridge", Color) = (0.42,0.42,0.62,1)
        _RidgeNear ("Near ridge", Color) = (0.28,0.3,0.5,1)
        _Stars ("Stars", Range(0,1)) = 0.3
        _Storm ("Storm", Range(0,1)) = 0
        _StormColor ("Storm colour", Color) = (0.75,0.78,0.85,1)
        _Bands ("Bands", Range(2,24)) = 9
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Mid, _Horizon, _Bottom, _SunColor, _RidgeFar, _RidgeNear, _StormColor;
                float4 _SunDir;
                half _Stars, _Storm, _Bands;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise1(float x)
            {
                float i = floor(x), f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash(float2(i, 7.0)), Hash(float2(i + 1.0, 7.0)), f);
            }

            float Ridge(float az, float seed, float freq)
            {
                float x = az * freq + seed;
                return Noise1(x) * 0.55 + Noise1(x * 2.7 + 3.1) * 0.3 + Noise1(x * 7.3 + 1.7) * 0.15;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float t = d.y;
                // Banded vertical gradient.
                float g = saturate(t * 1.15);
                g = floor(pow(g, 0.55) * _Bands + 0.5) / _Bands;
                half3 col = g < 0.4 ? lerp(_Horizon.rgb, _Mid.rgb, g / 0.4) : lerp(_Mid.rgb, _Top.rgb, (g - 0.4) / 0.6);

                // Stars high up.
                float2 sp = d.xz / (abs(d.y) + 0.35) * 42.0;
                float star = step(0.992, Hash(floor(sp))) * smoothstep(0.25, 0.6, t);
                col += star * _Stars;

                // Sun: hard disc with two halo bands.
                float3 sun = normalize(_SunDir.xyz);
                float sd = dot(d, sun);
                col += _SunColor.rgb * (step(0.9965, sd) * 0.16 + step(0.989, sd) * 0.07);
                col = lerp(col, _SunColor.rgb, step(0.9990, sd));

                // Mountain ridges with snow caps.
                float az = atan2(d.x, d.z);
                float far = 0.05 + 0.13 * Ridge(az, 11.0, 4.0);
                float near = 0.015 + 0.085 * Ridge(az, 37.0, 6.5);
                if (t < far) col = _RidgeFar.rgb;
                if (t < near) col = _RidgeNear.rgb;
                if (t < 0.0) col = lerp(_RidgeNear.rgb, _Bottom.rgb, saturate(-t * 6.0));

                col = lerp(col, _StormColor.rgb, _Storm);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
