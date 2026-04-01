Shader "Operation Marigold/URP/BlockWater"
{
    Properties
    {
        [Header(Color)]
        _DeepColor("Deep Color", Color) = (0.03, 0.16, 0.32, 1)
        _ShallowColor("Shallow Color", Color) = (0.20, 0.54, 0.82, 1)
        _SpecColor("Specular Color", Color) = (0.88, 0.96, 1.00, 1)
        _Alpha("Alpha", Range(0, 1)) = 0.88

        [Header(Fresnel)]
        _FresnelPower("Fresnel Power", Range(0.1, 12)) = 5
        _FresnelStrength("Fresnel Strength", Range(0, 2)) = 1

        [Header(Waves)]
        _WaveScale("Wave Scale", Range(0.1, 8)) = 1.4
        _WaveAmplitude("Wave Amplitude", Range(0, 1)) = 0.16
        _WaveNormalStrength("Wave Normal Strength", Range(0, 4)) = 1.2
        _WaveTopBlend("Top Face Wave Blend", Range(0, 1)) = 0.95
        _WaveSpeed1("Wave Speed 1", Vector) = (0.04, 0.03, 0, 0)
        _WaveSpeed2("Wave Speed 2", Vector) = (-0.03, 0.02, 0, 0)

        [Header(Lighting)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.92
        _DiffuseBoost("Diffuse Boost", Range(0, 2)) = 0.95

        [Header(Sparkle)]
        _SparkleScale("Sparkle Scale", Range(1, 200)) = 72
        _SparkleSpeed("Sparkle Speed", Range(0, 10)) = 1.8
        _SparkleIntensity("Sparkle Intensity", Range(0, 2)) = 0.55
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DeepColor;
                half4 _ShallowColor;
                half4 _SpecColor;
                half _Alpha;

                half _FresnelPower;
                half _FresnelStrength;

                half _WaveScale;
                half _WaveAmplitude;
                half _WaveNormalStrength;
                half _WaveTopBlend;
                float4 _WaveSpeed1;
                float4 _WaveSpeed2;

                half _Smoothness;
                half _DiffuseBoost;

                half _SparkleScale;
                half _SparkleSpeed;
                half _SparkleIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            float Hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            void BuildWave(float2 worldXZ, out float height, out float2 grad)
            {
                float t = _Time.y;

                float2 uv1 = worldXZ * _WaveScale + _WaveSpeed1.xy * t;
                float2 uv2 = worldXZ * (_WaveScale * 1.73) + _WaveSpeed2.xy * t;

                float s1 = sin(uv1.x);
                float c1x = cos(uv1.x);
                float s1y = sin(uv1.y);
                float c1y = cos(uv1.y);

                float s2 = sin(uv2.x + 0.7);
                float c2x = cos(uv2.x + 0.7);
                float s2y = sin(uv2.y + 1.4);
                float c2y = cos(uv2.y + 1.4);

                float h1 = s1 * c1y;
                float h2 = s2 * c2y;

                height = (h1 + h2 * 0.65) * 0.5;

                float dHdx = (c1x * c1y + c2x * c2y * 0.65) * 0.5 * _WaveScale;
                float dHdz = (-s1 * s1y + -s2 * s2y * 0.65) * 0.5 * _WaveScale;
                grad = float2(dHdx, dHdz);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs nrmInputs = GetVertexNormalInputs(v.normalOS);

                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS = NormalizeNormalPerVertex(nrmInputs.normalWS);
                o.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                o.shadowCoord = GetShadowCoord(posInputs);
                o.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float waveH;
                float2 waveGrad;
                BuildWave(i.positionWS.xz, waveH, waveGrad);

                float3 waveNormalWS = normalize(float3(
                    -waveGrad.x * _WaveAmplitude * _WaveNormalStrength,
                    1.0,
                    -waveGrad.y * _WaveAmplitude * _WaveNormalStrength));

                half upMask = saturate(i.normalWS.y);
                half blend = upMask * _WaveTopBlend;
                half3 normalWS = normalize(lerp(i.normalWS, waveNormalWS, blend));
                half3 viewDirWS = normalize(i.viewDirWS);

                Light mainLight = GetMainLight(i.shadowCoord);
                half3 lightDirWS = normalize(mainLight.direction);
                half NdotL = saturate(dot(normalWS, lightDirWS));
                half NdotV = saturate(dot(normalWS, viewDirWS));

                half fresnel = pow(1.0h - NdotV, _FresnelPower) * _FresnelStrength;
                half waveTint = saturate(0.5h + waveH * 0.9h);
                half3 waterColor = lerp(_DeepColor.rgb, _ShallowColor.rgb, waveTint);
                waterColor = lerp(waterColor, _SpecColor.rgb, fresnel * 0.25h);

                half3 ambient = SampleSH(normalWS) * waterColor * 0.45h;
                half3 diffuse = waterColor * (0.2h + NdotL * _DiffuseBoost) * mainLight.color * mainLight.shadowAttenuation;

                half3 halfDir = normalize(lightDirWS + viewDirWS);
                half specPow = lerp(12.0h, 256.0h, _Smoothness);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPow) * (0.35h + fresnel) * NdotL;
                half3 specular = _SpecColor.rgb * spec * mainLight.color * mainLight.shadowAttenuation;

                float2 sparkleUV = i.positionWS.xz * _SparkleScale + _Time.y * _SparkleSpeed;
                float sparkleNoise = Hash12(floor(sparkleUV) + frac(sparkleUV) * 0.37);
                half sparkleMask = smoothstep(0.88h, 1.0h, sparkleNoise);
                half sparkle = sparkleMask * _SparkleIntensity * (0.35h + fresnel) * NdotL;
                half3 sparkleCol = _SpecColor.rgb * sparkle;

                #ifdef _ADDITIONAL_LIGHTS
                    uint pixelLightCount = GetAdditionalLightsCount();
                    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                    {
                        Light addLight = GetAdditionalLight(lightIndex, i.positionWS);
                        half3 addDir = normalize(addLight.direction);
                        half addNdotL = saturate(dot(normalWS, addDir));
                        half3 addHalf = normalize(addDir + viewDirWS);
                        half addSpec = pow(saturate(dot(normalWS, addHalf)), specPow) * addNdotL;

                        diffuse += waterColor * addNdotL * addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation * 0.35h;
                        specular += _SpecColor.rgb * addSpec * addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation * 0.35h;
                    }
                #endif

                half3 finalColor = ambient + diffuse + specular + sparkleCol;
                finalColor = MixFog(finalColor, i.fogFactor);
                return half4(finalColor, _Alpha);
            }
            ENDHLSL
        }
    }
}
