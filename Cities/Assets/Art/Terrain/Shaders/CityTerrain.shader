// Terreinshader (E03-02): mengt gras, zand, rots, akker en bebouwde grond volgens de splatmap (_CitySplatMap, één
// pixel per cel van 8 m). Overgangen zijn zacht door bilineair filteren en hoogte-blending (de hoogte staat in de
// alfa van de albedo). Rots wordt triplanar geprojecteerd zodat steile wanden niet uitrekken; de andere lagen liggen
// plat in wereldcoördinaten. Twee schalen en een macrovariatie verbergen herhaling.
Shader "Cities/Terrain"
{
    Properties
    {
        [NoScaleOffset] _Albedo("Albedo (RGB) en hoogte (A), 5 lagen", 2DArray) = "" {}
        [NoScaleOffset] _Normals("Normalen, 5 lagen", 2DArray) = "" {}
        _LayerMeters("Meters per tegel: gras, zand, rots, akker", Vector) = (4, 5, 10, 8)
        _BuiltMeters("Meters per tegel: bebouwd", Float) = 4
        _Smoothness("Gladheid: gras, zand, rots, akker", Vector) = (0.12, 0.22, 0.28, 0.08)
        _BuiltSmoothness("Gladheid: bebouwd", Float) = 0.18
        _BlendDepth("Scherpte van hoogte-blending", Range(0.02, 1)) = 0.2
        _MacroScale("Schaal macrovariatie (m)", Float) = 420
        _MacroStrength("Sterkte macrovariatie", Range(0, 0.5)) = 0.18
        _NormalStrength("Sterkte normalen", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _LayerMeters;
            float _BuiltMeters;
            float4 _Smoothness;
            float _BuiltSmoothness;
            float _BlendDepth;
            float _MacroScale;
            float _MacroStrength;
            float _NormalStrength;
        CBUFFER_END

        TEXTURE2D_ARRAY(_Albedo);
        SAMPLER(sampler_Albedo);
        TEXTURE2D_ARRAY(_Normals);
        SAMPLER(sampler_Normals);
        TEXTURE2D(_CitySplatMap);
        SAMPLER(sampler_CitySplatMap);
        float4 _CityWorldSize;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Albedo en hoogte van een laag op twee schalen: de grove kopie verbergt het herhaalpatroon van de fijne.
            float4 SampleLayer(float2 uv, float layer)
            {
                float4 fine = SAMPLE_TEXTURE2D_ARRAY(_Albedo, sampler_Albedo, uv, layer);
                float4 coarse = SAMPLE_TEXTURE2D_ARRAY(_Albedo, sampler_Albedo, uv * 0.243 + 0.37, layer);
                return float4(fine.rgb * lerp(0.8, 1.2, coarse.a) * 0.92, fine.a * 0.75 + coarse.a * 0.25);
            }

            float3 SampleNormal(float2 uv, float layer)
            {
                float3 n = SAMPLE_TEXTURE2D_ARRAY(_Normals, sampler_Normals, uv, layer).rgb * 2.0 - 1.0;
                n.xy *= _NormalStrength;
                return normalize(n);
            }

            // Whiteout-blend van een raaklijnnormaal (x langs u, y langs v) op een projectievlak.
            float3 TopNormal(float3 n, float3 geometric)
            {
                return normalize(float3(n.x + geometric.x, n.z * geometric.y, n.y + geometric.z));
            }

            void Rock(float3 positionWS, float3 geometric, out float4 albedo, out float3 normalWS)
            {
                float3 blend = pow(abs(geometric), 4.0);
                blend /= dot(blend, 1.0);
                float scale = 1.0 / _LayerMeters.z;
                float2 uvX = positionWS.zy * scale;
                float2 uvY = positionWS.xz * scale;
                float2 uvZ = positionWS.xy * scale;
                albedo = SampleLayer(uvX, 2) * blend.x + SampleLayer(uvY, 2) * blend.y + SampleLayer(uvZ, 2) * blend.z;
                float3 nX = SampleNormal(uvX, 2);
                float3 nY = SampleNormal(uvY, 2);
                float3 nZ = SampleNormal(uvZ, 2);
                float3 worldX = float3(nX.z * sign(geometric.x), nX.y, nX.x) + float3(geometric.x, 0, 0);
                float3 worldY = TopNormal(nY, geometric);
                float3 worldZ = float3(nZ.x, nZ.y, nZ.z * sign(geometric.z)) + float3(0, 0, geometric.z);
                normalWS = normalize(worldX * blend.x + worldY * blend.y + worldZ * blend.z);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 geometric = normalize(input.normalWS);
                float2 xz = input.positionWS.xz;

                float4 splat = SAMPLE_TEXTURE2D(_CitySplatMap, sampler_CitySplatMap, xz * _CityWorldSize.zw);
                float weights[5];
                weights[0] = saturate(1.0 - splat.r - splat.g - splat.b - splat.a);
                weights[1] = splat.r;
                weights[2] = splat.g;
                weights[3] = splat.b;
                weights[4] = splat.a;

                float meters[5] = { _LayerMeters.x, _LayerMeters.y, _LayerMeters.z, _LayerMeters.w, _BuiltMeters };
                float smooth[5] = { _Smoothness.x, _Smoothness.y, _Smoothness.z, _Smoothness.w, _BuiltSmoothness };

                float4 albedo[5];
                float3 normals[5];
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    if (i == 2)
                    {
                        Rock(input.positionWS, geometric, albedo[i], normals[i]);
                    }
                    else
                    {
                        float2 uv = xz / meters[i];
                        albedo[i] = SampleLayer(uv, i);
                        normals[i] = TopNormal(SampleNormal(uv, i), geometric);
                    }
                }

                // Hoogte-blending: waar twee lagen elkaar raken, wint lokaal de hoogste (graspollen in zand, stenen in aarde).
                float top = 0;
                [unroll]
                for (int j = 0; j < 5; j++)
                {
                    top = max(top, albedo[j].a + weights[j]);
                }

                float total = 0;
                float3 color = 0;
                float3 normal = 0;
                float smoothness = 0;
                [unroll]
                for (int k = 0; k < 5; k++)
                {
                    float w = max(albedo[k].a + weights[k] - top + _BlendDepth, 0) * step(0.001, weights[k]);
                    total += w;
                    color += albedo[k].rgb * w;
                    normal += normals[k] * w;
                    smoothness += smooth[k] * w;
                }

                total = max(total, 1e-4);
                color /= total;
                smoothness /= total;

                // Grootschalige kleurverschillen, zoals vochtige en droge stukken land.
                float macro = SAMPLE_TEXTURE2D_ARRAY(_Albedo, sampler_Albedo, xz / _MacroScale, 0).a;
                color *= 1.0 + (macro - 0.5) * 2.0 * _MacroStrength;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalize(normal);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color;
                surface.smoothness = smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 result = UniversalFragmentPBR(inputData, surface);
                result.rgb = MixFog(result.rgb, inputData.fogCoord);
                return result;
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
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection));
                return ApplyShadowClamping(positionCS);
            }

            half4 ShadowFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex NormalsVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings NormalsVertex(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 NormalsFragment(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }
}
