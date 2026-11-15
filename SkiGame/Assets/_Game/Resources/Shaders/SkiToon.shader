// The one lit shader of the game: two-band cel shading with palette-tinted shadows,
// an inked inverted-hull outline, rim light, fog, vertex colours and optional snow sparkle.
Shader "Ski/Toon"
{
    Properties
    {
        _BaseColor ("Base colour", Color) = (1,1,1,1)
        _BaseMap ("Texture", 2D) = "white" {}
        _ShadowTint ("Shadow tint", Color) = (0.49,0.56,0.76,1)
        _BandEdge ("Band edge", Range(-0.5,0.8)) = 0.08
        _BandSoft ("Band softness", Range(0.001,0.3)) = 0.02
        _MidBand ("Mid band strength", Range(0,1)) = 0.35
        _RimColor ("Rim colour", Color) = (1,0.93,0.8,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3
        _RimStrength ("Rim strength", Range(0,1)) = 0.25
        _Emission ("Emission", Color) = (0,0,0,1)
        _OutlineColor ("Outline colour", Color) = (0.106,0.078,0.188,1)
        _OutlineWidth ("Outline width (px)", Range(0,8)) = 2
        _Snow ("Snow detail", Range(0,1)) = 0
        _Wind ("Wind sway", Range(0,1)) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

    TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

    CBUFFER_START(UnityPerMaterial)
        float4 _BaseMap_ST;
        half4 _BaseColor;
        half4 _ShadowTint;
        half _BandEdge;
        half _BandSoft;
        half _MidBand;
        half4 _RimColor;
        half _RimPower;
        half _RimStrength;
        half4 _Emission;
        half4 _OutlineColor;
        half _OutlineWidth;
        half _Snow;
        half _Wind;
    CBUFFER_END

    // Trees sway a little: displacement grows with height above the object's origin.
    float3 ApplyWind(float3 positionOS)
    {
        if (_Wind <= 0.0) return positionOS;
        float3 ws = TransformObjectToWorld(positionOS);
        float h = max(positionOS.y, 0.0);
        float sway = sin(_Time.y * 1.3 + ws.x * 0.21 + ws.z * 0.17) * 0.035 * _Wind * h;
        positionOS.x += sway;
        positionOS.z += sway * 0.6;
        return positionOS;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 pos = ApplyWind(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(pos);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 n = normalize(IN.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * IN.color.rgb;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = dot(n, mainLight.direction);
                half shade = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half lit = smoothstep(_BandEdge - _BandSoft, _BandEdge + _BandSoft, ndl) * smoothstep(0.35, 0.55, shade);
                half bright = smoothstep(0.62 - _BandSoft, 0.62 + _BandSoft, ndl) * lit;
                half band = lit * lerp(1.0 - _MidBand, 1.0, bright);

                half3 lightCol = mainLight.color;
                half3 shadowCol = albedo * _ShadowTint.rgb;
                half3 col = lerp(shadowCol, albedo * lightCol, band);

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint i = 0u; i < count; ++i)
                {
                    Light l = GetAdditionalLight(i, IN.positionWS);
                    // Lamps: two soft bands, with the light colour compressed so bright lamps tint instead of bleaching.
                    half reach = saturate(dot(n, l.direction) * 0.6 + 0.4) * l.distanceAttenuation;
                    half a = smoothstep(0.015, 0.06, reach) * 0.4 + smoothstep(0.18, 0.3, reach) * 0.6;
                    half3 lc = l.color / (1.0 + dot(l.color, half3(0.3, 0.5, 0.2))) * 1.5;
                    col += albedo * lc * a;
                }
                #endif

                // Rim on the lit side.
                half rim = pow(saturate(1.0 - dot(n, v)), _RimPower);
                col += _RimColor.rgb * smoothstep(0.45, 0.55, rim) * _RimStrength * lit;

                // Snow: groomed corduroy lines near the camera and a few sparkles.
                if (_Snow > 0.0)
                {
                    float dist = length(_WorldSpaceCameraPos - IN.positionWS);
                    half near = saturate(1.0 - dist / 45.0) * _Snow;
                    half groove = smoothstep(0.38, 0.5, abs(frac(IN.positionWS.x * 4.0) - 0.5));
                    col *= 1.0 - 0.045 * groove * near * IN.color.a;
                    half spark = step(0.9988, Hash21(floor(IN.positionWS.xz * 42.0)));
                    col += spark * saturate(1.0 - dist / 12.0) * lit * 0.45;
                }

                col += _Emission.rgb;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float3 smoothNormalOS : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 pos = ApplyWind(IN.positionOS.xyz);
                // Smoothed normals are baked into UV3 so hard-edged meshes get a closed hull.
                float3 nOS = dot(IN.smoothNormalOS, IN.smoothNormalOS) > 0.01 ? IN.smoothNormalOS : IN.normalOS;
                float4 cs = TransformObjectToHClip(pos);
                float3 nWS = TransformObjectToWorldNormal(nOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = max(length(nCS), 1e-5);
                // Constant width on screen, thinning far away so distant objects do not turn to blobs.
                float px = _OutlineWidth * saturate(22.0 / max(cs.w, 0.01)) ;
                px = max(px, _OutlineWidth * 0.35);
                cs.xy += (nCS / len) * px * 2.0 / _ScreenParams.xy * cs.w;
                OUT.positionCS = cs;
                OUT.fogFactor = ComputeFogFactor(cs.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, IN.fogFactor), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 positionWS = TransformObjectToWorld(ApplyWind(IN.positionOS.xyz));
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 cs = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = cs;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(ApplyWind(IN.positionOS.xyz));
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
