// CORDON — one shader for every model of the game (characters, props, pickups, decor).
// Albedo comes from the shared palette texture (T_Palette): RGB = color, A = 1 - emission mask.
// Vertex colors carry the AO / grime / flood mud baked in Blender (Tools/Blender/cordon_kit.py).
// Extras for the game: a hit flash, an extra glow (elites, Cristallisés), a cold rim that lifts
// silhouettes off the dark ground, and a faint wet sheen (Univers.md 9.5: everything is wet).
Shader "Cordon/Character"
{
    Properties
    {
        [MainTexture] _BaseMap ("Palette", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _EmissionStrength ("Emission (palette alpha)", Float) = 1.6
        _EmissionTint ("Emission Tint", Color) = (1, 1, 1, 1)
        _Glow ("Extra Glow", Color) = (0, 0, 0, 0)
        _GlowRim ("Glow on the rim only", Range(0, 1)) = 0.6
        _Flash ("Flash", Range(0, 1)) = 0
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _RimColor ("Rim (cold)", Color) = (0.32, 0.4, 0.5, 1)
        _RimPower ("Rim Power", Float) = 3.5
        _Ambient ("Ambient", Range(0, 2)) = 1
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
        _VertexAO ("Vertex AO", Range(0, 1)) = 1
        _Smoothness ("Wet Sheen", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _EmissionStrength;
            half4 _EmissionTint;
            half4 _Glow;
            half _GlowRim;
            half _Flash;
            half4 _FlashColor;
            half4 _RimColor;
            half _RimPower;
            half _Ambient;
            half _Wrap;
            half _VertexAO;
            half _Smoothness;
        CBUFFER_END

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 color : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.color = v.color;
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                half3 ao = lerp(half3(1, 1, 1), i.color.rgb, _VertexAO);
                half3 albedo = tex.rgb * _BaseColor.rgb * ao;
                half emissionMask = 1.0h - tex.a;

                half3 n = normalize(i.normalWS);
                half3 view = SafeNormalize(GetWorldSpaceViewDir(i.positionWS));

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(i.positionWS));
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                #endif
                Light light = GetMainLight(shadowCoord);
                half atten = light.shadowAttenuation * light.distanceAttenuation;
                half ndl = saturate((dot(n, light.direction) + _Wrap) / (1.0h + _Wrap));
                half3 diffuse = light.color * (ndl * atten);
                half3 ambient = SampleSH(n) * _Ambient;

                half3 h = SafeNormalize(light.direction + view);
                half spec = pow(saturate(dot(n, h)), lerp(8.0h, 64.0h, _Smoothness)) * _Smoothness * atten;

                half rim = pow(1.0h - saturate(dot(n, view)), _RimPower);
                half3 color = albedo * (diffuse + ambient) + light.color * spec * 0.35h * ao;
                color += _RimColor.rgb * rim * (0.35h + 0.65h * ndl) * ao.g;
                color += tex.rgb * emissionMask * _EmissionStrength * _EmissionTint.rgb;
                color += _Glow.rgb * lerp(1.0h, rim * 2.0h, _GlowRim);
                color = lerp(color, _FlashColor.rgb * 1.6h, _Flash);

                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
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
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 lightDir = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half DepthFrag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
