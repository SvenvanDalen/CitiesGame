// Standaardshader voor wegen en gebouwen (E19-01). Doet wat URP Lit doet (albedo, normal, gladheid, metaal), met twee
// stadsbrede extra's die de dag-nachtcyclus en het weer (E19-03, E19-04) via globale waarden aansturen:
//   _CityWetness (0-1): nat wegdek en natte daken worden donkerder en glanzen, met plekken die langer nat blijven.
//   _CityNight   (0-1): de emissiemap (ramen, lantaarns) licht op naarmate het donkerder wordt.
// Zonder die globals (0) is het gewoon droog en overdag. Eigenschapsnamen zijn dezelfde als bij URP Lit, zodat
// bestaande materialen hun waarden houden. De normal map wordt zonder tangenten gelezen (schermafgeleiden), omdat de
// procedurele meshes geen tangenten hebben.
Shader "Cities/Surface"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Kleur", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal map", 2D) = "bump" {}
        _BumpScale("Sterkte normal map", Float) = 1
        _Metallic("Metaal", Range(0, 1)) = 0
        _Smoothness("Gladheid", Range(0, 1)) = 0.2
        _WetSmoothness("Gladheid als het nat is", Range(0, 1)) = 0.9
        _WetDarken("Donkerder als het nat is", Range(0, 1)) = 0.35
        [HDR] _EmissionColor("Licht in het donker", Color) = (0, 0, 0, 1)
        _EmissionMap("Lichtmasker", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _BumpScale;
            half _Metallic;
            half _Smoothness;
            half _WetSmoothness;
            half _WetDarken;
            half4 _EmissionColor;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);
        SAMPLER(sampler_BumpMap);
        TEXTURE2D(_EmissionMap);
        SAMPLER(sampler_EmissionMap);

        // Stadsbrede waarden uit de dag-nachtcyclus en het weer; 0 als niemand ze zet.
        float _CityWetness;
        float _CityNight;
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
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            // Normal map zonder tangenten: het raakvlak komt uit de afgeleiden van positie en uv (Schueler).
            float3 PerturbNormal(float3 normalWS, float3 positionWS, float2 uv)
            {
                float3 n = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                float3 dp1 = ddx(positionWS);
                float3 dp2 = ddy(positionWS);
                float2 duv1 = ddx(uv);
                float2 duv2 = ddy(uv);
                float3 dp2perp = cross(dp2, normalWS);
                float3 dp1perp = cross(normalWS, dp1);
                float3 tangent = dp2perp * duv1.x + dp1perp * duv2.x;
                float3 bitangent = dp2perp * duv1.y + dp1perp * duv2.y;
                float invmax = rsqrt(max(dot(tangent, tangent), dot(bitangent, bitangent)) + 1e-20);
                return normalize(tangent * (n.x * invmax) + bitangent * (n.y * invmax) + normalWS * n.z);
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 geometric = normalize(input.normalWS);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                float3 normal = PerturbNormal(geometric, input.positionWS, input.uv);

                // Nat: plassen volgen een zachte ruis in wereldcoordinaten; ze worden eerder nat en drogen later op.
                float puddle = smoothstep(0.35, 0.65, Noise(input.positionWS.xz * 0.35));
                float wet = saturate(_CityWetness * (0.55 + 0.45 * puddle)) * (1.0 - _Metallic);
                half3 color = albedo.rgb * lerp(1.0, 1.0 - _WetDarken, wet);
                half smoothness = lerp(_Smoothness, _WetSmoothness, wet);
                // Een waterfilm maakt de plek vlak: de normal map verdwijnt er deels onder.
                normal = normalize(lerp(normal, geometric, wet * puddle * 0.7));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normal;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(normal);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = color;
                surface.metallic = _Metallic;
                surface.smoothness = smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                surface.emission = _EmissionColor.rgb * SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * saturate(_CityNight);

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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
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

            float4 DepthVertex(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFragment() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
