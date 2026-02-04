Shader "Universal Render Pipeline/Lit Triplanar WS"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)

        _Metallic("Metallic", Range(0,1)) = 0
        _MetallicGlossMap("Metallic Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0,1)) = 1

        _BumpScale("Normal Strength", Float) = 1
        _BumpMap("Normal Map", 2D) = "bump" {}

        _OcclusionStrength("Occlusion Strength", Range(0,1)) = 1
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        // Triplanar - ddoubled scale (was 1, now 0.5)
        _TriplanarScale("Triplanar Scale", Float) = 0.5
        _TriplanarBlendSharpness("Blend Sharpness", Range(1,8)) = 4

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Opaque"
            "UniversalMaterialType"="Lit"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _EMISSION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _GlossMapScale;
                half _BumpScale;
                half _OcclusionStrength;
                half4 _EmissionColor;
                float _TriplanarScale;
                float _TriplanarBlendSharpness;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_OcclusionMap); SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            half4 TriplanarSample(TEXTURE2D_PARAM(tex,samp), float3 wp, float3 wn)
            {
                float3 w = abs(wn);
                w = pow(w, _TriplanarBlendSharpness);
                w /= dot(w, 1);

                return
                    SAMPLE_TEXTURE2D(tex, samp, wp.zy * _TriplanarScale) * w.x +
                    SAMPLE_TEXTURE2D(tex, samp, wp.xz * _TriplanarScale) * w.y +
                    SAMPLE_TEXTURE2D(tex, samp, wp.xy * _TriplanarScale) * w.z;
            }

            half3 TriplanarNormalWS(float3 wp, float3 wn)
            {
                float3 w = abs(wn);
                w = pow(w, _TriplanarBlendSharpness);
                w /= dot(w, 1);

                half3 nx = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wp.zy * _TriplanarScale));
                half3 ny = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wp.xz * _TriplanarScale));
                half3 nz = UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, wp.xy * _TriplanarScale));

                // Reorient normals for each projection axis
                nx = half3(nx.z, nx.x, nx.y);
                ny = half3(ny.x, ny.z, ny.y);

                return normalize(nx * w.x + ny * w.y + nz * w.z);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewDirWS = GetWorldSpaceViewDir(p.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = TriplanarSample(_BaseMap, sampler_BaseMap, i.positionWS, i.normalWS) * _BaseColor;

                half metallic = _Metallic;
                half smoothness = _Smoothness;

                #ifdef _METALLICSPECGLOSSMAP
                    half4 mg = TriplanarSample(_MetallicGlossMap, sampler_MetallicGlossMap, i.positionWS, i.normalWS);
                    metallic = mg.r;
                    smoothness = mg.a * _GlossMapScale;
                #endif

                half3 normalWS = i.normalWS;
                #ifdef _NORMALMAP
                    normalWS = TriplanarNormalWS(i.positionWS, i.normalWS);
                    normalWS = normalize(lerp(i.normalWS, normalWS, _BumpScale));
                #endif

                half occlusion = 1;
                #ifdef _OCCLUSIONMAP
                    occlusion = TriplanarSample(_OcclusionMap, sampler_OcclusionMap, i.positionWS, i.normalWS).g;
                    occlusion = lerp(1, occlusion, _OcclusionStrength);
                #endif

                half3 emission = 0;
                #ifdef _EMISSION
                    emission = TriplanarSample(_EmissionMap, sampler_EmissionMap, i.positionWS, i.normalWS).rgb * _EmissionColor.rgb;
                #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(i.viewDirWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo.rgb;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.alpha = albedo.a;
                surface.occlusion = occlusion;
                surface.emission = emission;

                return UniversalFragmentPBR(inputData, surface);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
    }
}
