Shader "CityBuilder/FlatShading"
{
    Properties
    {
        _BaseColor    ("Base Color",    Color)  = (1, 1, 1, 1)
        _SunColor     ("Sun Color",     Color)  = (1, 0.95, 0.8, 1)
        _AmbientColor ("Ambient Color", Color)  = (0.15, 0.2, 0.3, 1)
        _SunDirection ("Sun Direction", Vector) = (0.5, 1.0, 0.3, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType"     = "Opaque"
            "Queue"          = "Geometry"
        }

        // ── Depth pre-pass ────────────────────────────────────────────────────
        Pass
        {
            Name "DepthForwardOnly"
            Tags { "LightMode" = "DepthForwardOnly" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }
            void DepthFrag(Varyings input) {}
            ENDHLSL
        }

        // ── Forward lit pass with flat shading ────────────────────────────────
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SunColor;
                float4 _AmbientColor;
                float4 _SunDirection;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // Flat shading: face normal from screen-space position derivatives.
                // Each polygon gets one uniform normal → the characteristic faceted look.
                float3 flatNormal = normalize(cross(ddy(input.positionWS),
                                                    ddx(input.positionWS)));

                float3 sunDir = normalize(_SunDirection.xyz);
                float  ndotl  = saturate(dot(flatNormal, sunDir));

                float3 color = _BaseColor.rgb * (_AmbientColor.rgb + _SunColor.rgb * ndotl);
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        // ── Shadow caster ─────────────────────────────────────────────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes { float3 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                return output;
            }
            void ShadowFrag(Varyings input) {}
            ENDHLSL
        }
    }

    FallBack "Hidden/InternalErrorShader"
}
