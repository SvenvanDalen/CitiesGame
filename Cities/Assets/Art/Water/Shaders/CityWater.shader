// Watershader (E03-05b, E19-02): kleur en doorzichtigheid naar de echte dikte van de waterlaag (scènediepte min
// oppervlak, dus ook correct langs steile oevers en onder bruggen), een gebroken beeld van de bodem (opaque texture),
// rimpels die met de stroom meebewegen, schuim langs de oever en in snel water, en een Fresnel-reflectie van de
// omgeving. De mesh (WaterMeshBuilder) levert per punt de waterdiepte en de stroomrichting; verder is alles
// procedureel, dus er zijn geen texturen nodig. Vereist depth en opaque texture in de URP-asset. Het oppervlak
// ontvangt geen schaduw en gooit er geen. Kosten: één pass, 2 extra texture-lezingen en 8 ruisberekeningen per pixel.
Shader "Cities/Water"
{
    Properties
    {
        _ShallowColor("Kleur ondiep", Color) = (0.22, 0.52, 0.55, 1)
        _DeepColor("Kleur diep", Color) = (0.02, 0.12, 0.2, 1)
        _ColorDepth("Diepte (m) waarop het water diep gekleurd is", Float) = 6
        _Absorption("Absorptie per meter (rood, groen, blauw)", Color) = (0.45, 0.12, 0.07, 1)
        _Refraction("Sterkte breking", Range(0, 0.2)) = 0.06
        _FoamColor("Kleur schuim", Color) = (0.92, 0.96, 0.98, 1)
        _FoamDepth("Diepte (m) van het schuim langs de oever", Float) = 0.35
        _FlowFoam("Stroomsnelheid (0-1) vanaf waar het schuimt", Range(0, 1)) = 0.95
        _WaveScale("Meters per rimpel", Float) = 6
        _WaveStrength("Sterkte rimpels", Range(0, 2)) = 0.7
        _FlowSpeed("Meters per seconde bij volle stroom", Float) = 2.5
        _Drift("Rimpelsnelheid in stilstaand water (m/s)", Float) = 0.15
        _Smoothness("Gladheid", Range(0, 1)) = 0.92
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                float _ColorDepth;
                half4 _Absorption;
                float _Refraction;
                half4 _FoamColor;
                float _FoamDepth;
                float _FlowFoam;
                float _WaveScale;
                float _WaveStrength;
                float _FlowSpeed;
                float _Drift;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 data : TEXCOORD0; // diepte, stroom x, stroom z
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 data : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.data = input.data;
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            float Hash(float2 p)
            {
                float3 q = frac(float3(p.xyx) * 0.1031);
                q += dot(q, q.yzx + 33.33);
                return frac((q.x + q.y) * q.z);
            }

            // Waarderuis met vloeiende interpolatie, 0 tot 1.
            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // Golfhoogte op twee schalen, bewogen met de stroom.
            float Height(float2 p)
            {
                return Noise(p) * 0.65 + Noise(p * 2.3 + 17.0) * 0.35;
            }

            // Normaal uit de golfhoogte; twee fases die elkaar afwisselen laten de rimpels meestromen zonder dat ze
            // uitrekken (flow mapping): elke fase loopt maar een halve cyclus mee en vervaagt dan.
            float3 WaveNormal(float2 xz, float2 flow, float time)
            {
                float2 velocity = flow * _FlowSpeed;
                float drift = _Drift * (1.0 - saturate(length(flow) * 4.0));
                velocity += float2(drift, drift * 0.4);
                float phaseA = frac(time * 0.5);
                float phaseB = frac(time * 0.5 + 0.5);
                float weightA = abs(1.0 - 2.0 * phaseA);
                float weightB = 1.0 - weightA;
                float2 uvA = xz / _WaveScale - velocity * (phaseA - 0.5) * 2.0 / _WaveScale;
                float2 uvB = xz / _WaveScale - velocity * (phaseB - 0.5) * 2.0 / _WaveScale + 0.43;

                const float e = 0.08;
                float hA = Height(uvA);
                float hB = Height(uvB);
                float dxA = Height(uvA + float2(e, 0)) - hA;
                float dzA = Height(uvA + float2(0, e)) - hA;
                float dxB = Height(uvB + float2(e, 0)) - hB;
                float dzB = Height(uvB + float2(0, e)) - hB;
                float2 slope = (float2(dxA, dzA) * weightA + float2(dxB, dzB) * weightB) / e;
                return normalize(float3(-slope.x * _WaveStrength, 1.0, -slope.y * _WaveStrength));
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float depth = input.data.x;
                float2 flow = input.data.yz;
                float speed = saturate(length(flow));
                float time = _Time.y;

                float3 normal = WaveNormal(input.positionWS.xz, flow, time);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Dikte van de waterlaag langs de kijkrichting: scènediepte min oppervlak.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float surfaceEye = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());
                float thickness = max(LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - surfaceEye, 0.0);

                // Gebroken beeld van de bodem; blijft het gebroken punt voor het water liggen (een paal, een brug), dan geen breking.
                float2 refractedUV = screenUV + normal.xz * _Refraction * saturate(thickness * 0.5) / max(surfaceEye * 0.05, 1.0);
                float refractedEye = LinearEyeDepth(SampleSceneDepth(refractedUV), _ZBufferParams);
                refractedUV = refractedEye < surfaceEye ? screenUV : refractedUV;
                half3 ground = SampleSceneColor(refractedUV);

                // Absorptie per kleurkanaal: rood verdwijnt het eerst, diep water wordt blauwgroen en donker.
                Light light = GetMainLight();
                float day = lerp(0.35, 1.0, saturate(light.direction.y * 3.0 + 0.2));
                half3 transmittance = exp(-_Absorption.rgb * thickness);
                half3 tint = lerp(_ShallowColor.rgb, _DeepColor.rgb, saturate(max(thickness, depth) / _ColorDepth)) * day;
                half3 color = ground * transmittance + tint * (1.0 - transmittance);

                // Fresnel-reflectie van de omgeving (lucht en reflection probes) en zonglans.
                float fresnel = pow(1.0 - saturate(dot(normal, view)), 4.0);
                float3 reflected = reflect(-view, normal);
                half3 environment = GlossyEnvironmentReflection(reflected, input.positionWS, 1.0 - _Smoothness, 1.0, float2(0, 0));
                color = lerp(color, environment, saturate(fresnel * 0.85 + 0.04));

                float3 halfway = normalize(light.direction + view);
                float glint = pow(saturate(dot(normal, halfway)), lerp(60.0, 400.0, _Smoothness));
                color += light.color * glint * 1.5;

                // Schuim: smalle rand langs de oever en turbulent in snel water, beide met een rafelige rand.
                float noise = Noise(input.positionWS.xz * 0.9 - flow * time * _FlowSpeed * 0.5) * 0.6
                    + Noise(input.positionWS.xz * 2.7 + 5.0) * 0.4;
                float shore = 1.0 - smoothstep(0.0, _FoamDepth, depth + (noise - 0.5) * _FoamDepth * 0.8);
                float rapids = smoothstep(_FlowFoam, 1.0, speed) * smoothstep(0.5, 0.75, noise) * 0.7;
                float foam = saturate(max(shore, rapids));
                color = lerp(color, _FoamColor.rgb * saturate(light.direction.y * 2.0 + 0.4), foam);

                color = MixFog(color, input.fogFactor);
                // Op de rand zelf ligt het vlak op het maaiveld; daar moet het volledig wegvallen.
                return half4(color, smoothstep(0.0, 0.05, depth));
            }
            ENDHLSL
        }
    }
}
