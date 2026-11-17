// Full-screen comic inking: a crisp dark line wherever depth or surface direction changes
// sharply. Silhouettes (depth) get a bold line, inner forms (normals: jaw, folds, collars, the
// edges of buildings) a thinner one. Lines are hard-edged, as an inker draws them, and fade
// with distance so far forests stay clean.
Shader "Ski/InkEdges"
{
    Properties
    {
        _InkColor ("Ink colour", Color) = (0.106,0.078,0.188,0.95)
        _Thickness ("Silhouette half-width (px at 720p)", Range(0.5,4)) = 1.25
        _InnerThickness ("Inner line half-width (px at 720p)", Range(0.3,3)) = 0.8
        _DepthSens ("Depth sensitivity", Range(0.005,0.5)) = 0.06
        _NormalSens ("Normal sensitivity", Range(0.05,2)) = 0.55
        _FadeStart ("Fade start (m)", Float) = 30
        _FadeEnd ("Fade end (m)", Float) = 140
        _InnerStart ("Inner lines fade start (m)", Float) = 7
        _InnerEnd ("Inner lines fade end (m)", Float) = 24
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "InkEdges"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            half4 _InkColor;
            float _Thickness, _InnerThickness, _DepthSens, _NormalSens, _FadeStart, _FadeEnd, _InnerStart, _InnerEnd;

            float Eye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

            // Depth edge from a cross of four samples at a given radius (in uv). A flat surface, at
            // any angle, lies on a straight line between opposite samples; a real edge does not. The
            // line goes on the pixels in front of their surroundings, so a silhouette is one line,
            // drawn on the near object, and a long slope seen at a grazing angle stays clean.
            float DepthEdge(float2 uv, float2 r, float d0, float thr)
            {
                float dA = Eye(uv + r * float2(1, 1)), dB = Eye(uv - r * float2(1, 1));
                float dC = Eye(uv + r * float2(1, -1)), dD = Eye(uv - r * float2(1, -1));
                float lap = ((dA + dB) * 0.5 - d0) + ((dC + dD) * 0.5 - d0);
                return lap / d0 > thr ? 1.0 : 0.0;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                float raw = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    if (raw <= 0.00001) return col; // sky
                #else
                    if (raw >= 0.99999) return col;
                #endif
                float d0 = LinearEyeDepth(raw, _ZBufferParams);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, d0);
                if (fade <= 0.001) return col;

                float scale = _ScreenParams.y / 720.0;
                float2 px = 1.0 / _ScreenParams.xy;

                // Surfaces seen at a grazing angle change depth quickly without being an edge,
                // so the threshold rises there.
                float3 n0 = SampleSceneNormals(uv);
                float3 posWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float ndv = saturate(dot(n0, normalize(_WorldSpaceCameraPos - posWS)));
                float thr = _DepthSens * (1.0 + 14.0 * (1.0 - ndv) * (1.0 - ndv));
                float e = DepthEdge(uv, px * _Thickness * scale, d0, thr);

                // Inner lines from the surface direction, thinner, and only up close.
                float inner = 1.0 - smoothstep(_InnerStart, _InnerEnd, d0);
                if (inner > 0.01 && e < 0.5)
                {
                    float2 r = px * _InnerThickness * scale;
                    float3 nA = SampleSceneNormals(uv + r * float2(1, 1)), nB = SampleSceneNormals(uv - r * float2(1, 1));
                    float3 nC = SampleSceneNormals(uv + r * float2(1, -1)), nD = SampleSceneNormals(uv - r * float2(1, -1));
                    float nEdge = length(nA - nB) + length(nC - nD);
                    // Hard-edged, but the threshold itself thins out with distance (no grey halo).
                    e = nEdge > _NormalSens + (1.0 - inner) * 0.8 ? 1.0 : 0.0;
                }

                col.rgb = lerp(col.rgb, _InkColor.rgb, e * fade * _InkColor.a);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
