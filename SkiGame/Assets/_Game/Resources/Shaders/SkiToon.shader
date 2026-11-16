// The one lit shader of the game, in a graphic-novel style: hard cel bands whose edges wobble
// like brush strokes, palette-tinted shadows, pools of ink in the deepest shade, a coloured rim
// from behind, a painted variation over flat colour, and an inverted-hull ink outline.
// A separate full-screen pass (Ski/InkEdges) draws the inner contour lines from depth and normals.
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
        _BackRimColor ("Back rim colour", Color) = (0.45,0.75,1,1)
        _BackRim ("Back rim strength", Range(0,1)) = 0
        _InkShadow ("Ink in deep shadow", Range(0,1)) = 0
        _Paint ("Painted variation", Range(0,1)) = 0.25
        _Wobble ("Brush wobble of band edges", Range(0,1)) = 0.3
        _Emission ("Emission", Color) = (0,0,0,1)
        _OutlineColor ("Outline colour", Color) = (0.106,0.078,0.188,1)
        _OutlineWidth ("Outline width (px)", Range(0,8)) = 2
        _Snow ("Snow detail", Range(0,1)) = 0
        _Wind ("Wind sway", Range(0,1)) = 0
        _FlatPrepass ("Flat normals for ink edges", Range(0,1)) = 0
        _InkNear ("Inner ink lines start to thin out at (m, 0 = never)", Float) = 0
        _InkFar ("Inner ink lines gone at (m)", Float) = 0
        _InkSoft ("Fewer inner ink lines, at any distance", Range(0,1)) = 0
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
        half4 _BackRimColor;
        half _BackRim;
        half _InkShadow;
        half _Paint;
        half _Wobble;
        half4 _Emission;
        half4 _OutlineColor;
        half _OutlineWidth;
        half _Snow;
        half _Wind;
        half _FlatPrepass;
        half _InkNear;
        half _InkFar;
        half _InkSoft;
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

    half Hash21(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    // Smooth 3D value noise, 0..1. Sampled in object space so it sticks to moving characters.
    half Noise3(float3 p)
    {
        float3 i = floor(p), f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        float n = i.x + i.y * 57.0 + i.z * 113.0;
        float4 a = frac(sin(float4(n, n + 1.0, n + 57.0, n + 58.0)) * 43758.5453);
        float4 b = frac(sin(float4(n + 113.0, n + 114.0, n + 170.0, n + 171.0)) * 43758.5453);
        float2 x0 = lerp(a.xz, a.yw, f.x), x1 = lerp(b.xz, b.yw, f.x);
        return lerp(lerp(x0.x, x0.y, f.y), lerp(x1.x, x1.y, f.y), f.z);
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
                float3 positionOS : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float3 pos = ApplyWind(IN.positionOS.xyz);
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionWS = TransformObjectToWorld(pos);
                OUT.positionCS = TransformWorldToHClip(OUT.positionWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half3 n = normalize(IN.normalWS);
                half3 v = normalize(GetWorldSpaceViewDir(IN.positionWS));
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 albedo = tex.rgb * _BaseColor.rgb * IN.color.rgb;

                // Painted look: broad, soft patches of slightly different value over the flat colour.
                half patch = Noise3(IN.positionOS * 3.1) * 0.6 + Noise3(IN.positionOS * 11.0) * 0.4;
                albedo *= 1.0 + (patch - 0.5) * 0.22 * _Paint;

                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                // Band edges wander a little, as if laid in with a brush.
                half wobble = (Noise3(IN.positionOS * 9.0 + 3.7) - 0.5) * 0.3 * _Wobble;
                half ndl = dot(n, mainLight.direction) + wobble;
                half shade = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half lit = smoothstep(_BandEdge - _BandSoft, _BandEdge + _BandSoft, ndl) * smoothstep(0.35, 0.55, shade);
                half bright = smoothstep(0.62 - _BandSoft, 0.62 + _BandSoft, ndl) * lit;
                half band = lit * lerp(1.0 - _MidBand, 1.0, bright);

                half3 lightCol = mainLight.color;
                half3 shadowCol = albedo * _ShadowTint.rgb;
                half3 col = lerp(shadowCol, albedo * lightCol, band);

                // The deepest shade fills in with ink, as on a comic page.
                half deep = (1.0 - smoothstep(-0.62, -0.5, ndl)) * _InkShadow;
                col = lerp(col, shadowCol * 0.42 + _OutlineColor.rgb * 0.5, deep);

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

                // Rim on the lit side, and a coloured rim from behind on the shadow side.
                half fres = saturate(1.0 - dot(n, v));
                half rim = pow(fres, _RimPower);
                col += _RimColor.rgb * smoothstep(0.45, 0.55, rim) * _RimStrength * lit;
                half back = smoothstep(0.5, 0.62, pow(fres, 2.2)) * (1.0 - lit) * saturate(0.4 - ndl);
                col += _BackRimColor.rgb * back * _BackRim;

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
                float px = _OutlineWidth * saturate(22.0 / max(cs.w, 0.01));
                px = max(px, _OutlineWidth * 0.35);
                cs.xy += (nCS / len) * px * 2.0 / _ScreenParams.xy * cs.w * (_ScreenParams.y / 720.0);
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

        // Depth and normals for the ink-edge pass. Materials with _FlatPrepass (snow, trees)
        // write a constant normal, so they only get silhouette lines, not a line on every facet.
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 pos = ApplyWind(IN.positionOS.xyz);
                OUT.positionCS = TransformObjectToHClip(pos);
                OUT.positionWS = TransformObjectToWorld(pos);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 n = lerp(normalize(IN.normalWS), float3(0.0, 1.0, 0.0), step(0.5, _FlatPrepass));
                // Finely modelled surfaces (faces, folds) seen from further off: the small forms are
                // smoothed away, so the artist draws the outline and leaves the inside clean.
                float3 toCam = _WorldSpaceCameraPos - IN.positionWS;
                float far = smoothstep(_InkNear, max(_InkFar, _InkNear + 0.01), length(toCam)) * step(0.001, _InkFar);
                n = normalize(lerp(n, normalize(toCam), max(_InkSoft, 0.88 * far)));
                return half4(n, 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
