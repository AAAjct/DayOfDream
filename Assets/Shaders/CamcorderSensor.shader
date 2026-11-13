Shader "Hidden/DayOfDream/CamcorderSensor"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Camcorder Sensor"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment SensorFragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _CamcorderSensor;

            float Grain(float2 pixel)
            {
                float3 hash = frac(float3(pixel.xyx) * 0.1031);
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z) - 0.5;
            }

            half4 SensorFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float2 centered = uv * 2 - 1;
                float radial = dot(centered, centered);
                float ir = _CamcorderSensor.y;
                float3 scene = max(0, SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb);
                const float3 lumaWeights = float3(.2126, .7152, .0722);
                float luma = dot(scene, lumaWeights);
                float3 colorVideo = scene * .82 + float3(-.001, .001, .002);
                float exposed = pow(luma, .79) * 1.08;
                float signal = exposed / (1 + 1.25 * exposed);
                float3 phosphor = lerp(float3(.32, .95, .43), float3(.68, .96, .70), smoothstep(.25, .58, signal));
                // Limit sensor visibility as well as the lamp so ambient light cannot reveal the distance in IR mode.
                float depth = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float range = _CamcorderSensor.z;
                float visibility = 1 - smoothstep(range * .55, range, depth);
                float3 night = signal * phosphor * visibility;
                // A rounded optical gate keeps the center legible and darkens the perimeter.
                float edge = smoothstep(.25, 1.44, radial);
                float gate = smoothstep(.81, 1.32, max(abs(centered.x), abs(centered.y)) + radial * .22);
                float vignette = (1 - edge * lerp(.58, .79, ir)) * (1 - gate * .65);
                float3 video = lerp(colorVideo, night, ir);
                float tick = _CamcorderSensor.w;
                float2 pixel = floor(input.positionCS.xy);
                float grain = Grain(pixel + float2(tick * 21.7, tick * 17.1));
                float sensorGrain = Grain(floor(pixel * .5) + float2(tick * 13.1, tick * 19.7));
                float row = Grain(float2(pixel.y, tick * 3.7));
                float shadows = 1 - smoothstep(.015, .36, luma * visibility);
                float noise = lerp(.0018, .022 + .020 * shadows, ir);
                grain = lerp(grain, grain * .3 + sensorGrain * .7, ir);
                float scanline = 1 - frac(pixel.y * .5) * lerp(.018, .032, ir);
                video = video * scanline + (grain + row * .16) * noise *
                    lerp(float3(1, 1, 1), float3(.55, 1, .60), ir);
                return half4(saturate(video * vignette), 1);
            }
            ENDHLSL
        }
    }
}
