// Geanimeerde voertuigen en burgers op de GPU (E21-05). Elke instance leest zijn plek uit _AgentPose (x, y, z en in w de
// draaiing in radialen) en zijn animatiestand uit _AgentAnim (x = wielhoek of looppas, y = schaal). Er zijn twee manieren:
//  - voertuigen: hoekpunten met UV2.x = 1 zijn wielhoekpunten en draaien om de X-as door scharnier (UV2.y, UV2.z) over de wielhoek;
//  - burgers (_AGENT_CLIP): de looppas is vooraf gebakken in _ClipMap (breedte = hoekpunten, hoogte = frames, hele floats als
//    verplaatsing t.o.v. de rustpose); de shader mengt twee frames op basis van de fase. Geen skinning, geen CPU-werk per frame.
Shader "Cities/CityAgent"
{
    Properties
    {
        [MainColor] _BaseColor("Kleur", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Textuur", 2D) = "white" {}
        _Smoothness("Gladheid", Range(0, 1)) = 0.4
        [NoScaleOffset] _ClipMap("Gebakken clip (positie per hoekpunt per frame)", 2D) = "black" {}
        _ClipFrames("Aantal frames", Float) = 1
        _ClipVertices("Aantal hoekpunten", Float) = 1
        [Toggle(_AGENT_CLIP)] _UseClip("Gebakken clip gebruiken", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Smoothness;
            float _ClipFrames;
            float _ClipVertices;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_ClipMap);
        SamplerState sampler_point_clamp;
        StructuredBuffer<float4> _AgentPose;
        StructuredBuffer<float4> _AgentAnim;

        // Draait rond de X-as door het scharnier (y, z), gelijk aan LimbPose.RotateAroundX in de simulatie.
        float3 RotateAroundX(float3 p, float2 pivot, float angle)
        {
            float s = sin(angle);
            float c = cos(angle);
            float y = p.y - pivot.x;
            float z = p.z - pivot.y;
            return float3(p.x, pivot.x + y * c - z * s, pivot.y + y * s + z * c);
        }

        float3 ClipOffset(uint vertex, float frame)
        {
            float u = (vertex + 0.5) / _ClipVertices;
            float v = (frame + 0.5) / _ClipFrames;
            return SAMPLE_TEXTURE2D_LOD(_ClipMap, sampler_point_clamp, float2(u, v), 0).xyz;
        }

        // Rustpositie na de animatie: een gebakken clip (burgers) of een draaiend wiel (voertuigen).
        float3 AnimatePosition(uint vertex, float3 wheel, float4 anim, float3 position)
        {
        #if defined(_AGENT_CLIP)
            float t = frac(anim.x) * _ClipFrames;
            float f0 = min(floor(t), _ClipFrames - 1);
            float f1 = fmod(f0 + 1, _ClipFrames);
            return position + lerp(ClipOffset(vertex, f0), ClipOffset(vertex, f1), t - f0);
        #else
            return wheel.x > 0.5 ? RotateAroundX(position, wheel.yz, anim.x) : position;
        #endif
        }

        float3 ToWorld(float3 positionOS, float4 pose, float scale)
        {
            float s = sin(pose.w);
            float c = cos(pose.w);
            float3 rotated = float3(c * positionOS.x + s * positionOS.z, positionOS.y, -s * positionOS.x + c * positionOS.z);
            return pose.xyz + rotated * scale;
        }

        float3 NormalToWorld(float3 normalOS, float yaw)
        {
            float s = sin(yaw);
            float c = cos(yaw);
            return float3(c * normalOS.x + s * normalOS.z, normalOS.y, -s * normalOS.x + c * normalOS.z);
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
            #pragma multi_compile_local _ _AGENT_CLIP
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float3 wheel : TEXCOORD1;
                uint vertexId : SV_VertexID;
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
                float4 pose = _AgentPose[input.instance];
                float4 anim = _AgentAnim[input.instance];
                float3 position = input.positionOS.xyz;
                float3 normal = input.normalOS;
            #if !defined(_AGENT_CLIP)
                if (input.wheel.x > 0.5)
                {
                    normal = RotateAroundX(normal, float2(0, 0), anim.x);
                }
            #endif
                position = AnimatePosition(input.vertexId, input.wheel, anim, position);
                float3 positionWS = ToWorld(position, pose, anim.y);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = NormalToWorld(normal, pose.w);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                Light light = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normal = normalize(input.normalWS);
                half diffuse = saturate(dot(normal, light.direction)) * light.shadowAttenuation;
                half3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                half3 halfDir = normalize(light.direction + viewDir);
                half specular = pow(saturate(dot(normal, halfDir)), lerp(8, 96, _Smoothness)) * _Smoothness * light.shadowAttenuation;
                half3 color = albedo.rgb * (light.color * diffuse + SampleSH(normal)) + light.color * specular * 0.25;
                return half4(MixFog(color, input.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_local _ _AGENT_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 wheel : TEXCOORD1;
                uint vertexId : SV_VertexID;
                uint instance : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float4 pose = _AgentPose[input.instance];
                float4 anim = _AgentAnim[input.instance];
                float3 position = input.positionOS.xyz;
                position = AnimatePosition(input.vertexId, input.wheel, anim, position);
                output.positionCS = TransformWorldToHClip(ToWorld(position, pose, anim.y));
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
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
            #pragma target 4.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_local _ _AGENT_CLIP

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 wheel : TEXCOORD1;
                uint vertexId : SV_VertexID;
                uint instance : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                float4 pose = _AgentPose[input.instance];
                float4 anim = _AgentAnim[input.instance];
                float3 position = input.positionOS.xyz;
                position = AnimatePosition(input.vertexId, input.wheel, anim, position);
                output.positionCS = TransformWorldToHClip(ToWorld(position, pose, anim.y));
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
