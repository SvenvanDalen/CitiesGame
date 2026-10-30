// Impostors voor bomen in de verte (E03-07). Getekend met Graphics.RenderMeshPrimitives: elke instance leest zijn
// plek uit _TreeInstances (x, y, z en in w de schaal in het gehele deel en de draaiing als fractie). Bomen dichter
// bij de camera dan _TreeImpostorStart vallen weg; die tekent de renderer als volledig model.
Shader "Cities/TreeImpostor"
{
    Properties
    {
        [MainTexture] _BaseMap("Foto's (links opzij, rechts boven)", 2D) = "white" {}
        _Cutoff("Alfagrens", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half _Cutoff;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        StructuredBuffer<float4> _TreeInstances;
        float _TreeImpostorStart;

        // Plaatst een hoekpunt van de impostor in de wereld; buiten bereik valt de hele boom weg.
        float3 TreeWorld(float3 positionOS, uint instance, out float3 rotationX, out float3 rotationZ, out bool hidden)
        {
            float4 data = _TreeInstances[instance];
            float scale = floor(data.w) / 1000.0;
            float yaw = frac(data.w) / 0.999 * 6.2831853;
            rotationX = float3(cos(yaw), 0, -sin(yaw));
            rotationZ = float3(sin(yaw), 0, cos(yaw));
            hidden = distance(data.xyz, GetCameraPositionWS()) < _TreeImpostorStart;
            return data.xyz + (rotationX * positionOS.x + float3(0, positionOS.y, 0) + rotationZ * positionOS.z) * scale;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                uint instance : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 rotationX;
                float3 rotationZ;
                bool hidden;
                float3 positionWS = TreeWorld(input.positionOS.xyz, input.instance, rotationX, rotationZ, hidden);
                output.positionWS = positionWS;
                output.positionCS = hidden ? float4(0, 0, -1, 1) : TransformWorldToHClip(positionWS);
                output.normalWS = normalize(rotationX * input.normalOS.x + float3(0, input.normalOS.y, 0) + rotationZ * input.normalOS.z);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                clip(albedo.a - _Cutoff);

                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normal = normalize(input.normalWS);
                half diffuse = saturate(dot(normal, light.direction)) * light.shadowAttenuation;
                half3 color = albedo.rgb * (light.color * diffuse + SampleSH(normal));
                return half4(MixFog(color, input.fogFactor), 1);
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
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instance : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float3 rotationX;
                float3 rotationZ;
                bool hidden;
                float3 positionWS = TreeWorld(input.positionOS.xyz, input.instance, rotationX, rotationZ, hidden);
                output.positionCS = hidden ? float4(0, 0, -1, 1) : TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
