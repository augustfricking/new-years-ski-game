// Full-screen comic inking: draws a dark line wherever depth or surface direction changes
// sharply, which adds the inner contour lines (jaw, folds, collars, edges of buildings) that an
// outline hull cannot. Lines fade with distance so far forests stay clean.
Shader "Ski/InkEdges"
{
    Properties
    {
        _InkColor ("Ink colour", Color) = (0.106,0.078,0.188,0.9)
        _Thickness ("Thickness (px at 720p)", Range(0.5,4)) = 1.25
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
            float _Thickness, _DepthSens, _NormalSens, _FadeStart, _FadeEnd, _InnerStart, _InnerEnd;

            float Eye(float2 uv) { return LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams); }

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

                float2 px = _Thickness * (_ScreenParams.y / 720.0) / _ScreenParams.xy;
                float2 uvA = uv + px * float2(1, 1), uvB = uv - px * float2(1, 1);
                float2 uvC = uv + px * float2(1, -1), uvD = uv - px * float2(1, -1);

                // Roberts cross on depth, relative to distance. Surfaces seen at a grazing angle
                // change depth quickly without being an edge, so the threshold rises there.
                float dA = Eye(uvA), dB = Eye(uvB), dC = Eye(uvC), dD = Eye(uvD);
                float dEdge = (abs(dA - dB) + abs(dC - dD)) / d0;
                float3 n0 = SampleSceneNormals(uv);
                float3 posWS = ComputeWorldSpacePosition(uv, raw, UNITY_MATRIX_I_VP);
                float ndv = saturate(dot(n0, normalize(_WorldSpaceCameraPos - posWS)));
                float thr = _DepthSens * (1.0 + 14.0 * (1.0 - ndv) * (1.0 - ndv));
                float e = smoothstep(thr, thr * 1.8, dEdge);

                // Roberts cross on normals.
                float3 nA = SampleSceneNormals(uvA), nB = SampleSceneNormals(uvB);
                float3 nC = SampleSceneNormals(uvC), nD = SampleSceneNormals(uvD);
                float nEdge = length(nA - nB) + length(nC - nD);
                // Inner lines only up close: on a small figure they would fill it with ink.
                float inner = 1.0 - smoothstep(_InnerStart, _InnerEnd, d0);
                e = max(e, smoothstep(_NormalSens, _NormalSens + 0.35, nEdge) * inner);

                col.rgb = lerp(col.rgb, _InkColor.rgb, e * fade * _InkColor.a);
                return col;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
