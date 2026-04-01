Shader "PathPreview/LineGlow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.2, 0.9, 0.3, 0.9)
        _SparkleSpeed ("Sparkle Speed", Range(0.5, 4)) = 2
        _SparkleIntensity ("Sparkle Intensity", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _SparkleSpeed;
            float _SparkleIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _Color;
                output.uv = input.positionOS.x + input.positionOS.y + input.positionOS.z;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float pulse = sin(_Time.y * _SparkleSpeed + input.uv * 3.14) * 0.5 + 0.5;
                float sparkle = 1 + pulse * _SparkleIntensity;
                half4 baseColor = (input.color.a > 0.01) ? (input.color * _Color) : _Color;
                half4 c = baseColor * sparkle;
                c.rgb *= c.a;
                return c;
            }
            ENDHLSL
        }
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _SparkleSpeed;
            float _SparkleIntensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Color;
                o.uv = v.vertex.x + v.vertex.y + v.vertex.z;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float pulse = sin(_Time.y * _SparkleSpeed + i.uv * 3.14) * 0.5 + 0.5;
                float sparkle = 1 + pulse * _SparkleIntensity;
                fixed4 baseColor = (i.color.a > 0.01) ? (i.color * _Color) : _Color;
                fixed4 c = baseColor * sparkle;
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
