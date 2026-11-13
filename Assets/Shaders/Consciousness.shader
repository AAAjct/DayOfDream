Shader "Hidden/DayOfDream/Consciousness"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float4 _Consciousness;
            half4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float blur = _Consciousness.x;
                float2 uv = input.texcoord;
                float2 radius = float2(1, _ScreenParams.x / _ScreenParams.y) * .012 * blur;
                half3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb * .2;
                [unroll] for (int i = 0; i < 12; i++)
                {
                    float angle = i * 2.399963;
                    float2 offset = float2(cos(angle), sin(angle)) * sqrt((i + .5) / 12.0) * radius;
                    color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).rgb / 15;
                }
                // Brief binocular misalignment resolves as the eyes focus.
                half3 ghost = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + radius * .6).rgb;
                color = lerp(color, ghost, blur * .16);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
}
