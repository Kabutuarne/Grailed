Shader "Custom/URP Lit Triplanar Bricks"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)

        _Metallic("Metallic", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = 0.5

        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 1

        _TriplanarScale("Triplanar Scale", Float) = 1
        _TriplanarBlend("Blend Sharpness", Range(1,8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "UniversalMaterialType" = "Lit"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            // Lighting variants
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma multi_compile_fog

            #pragma shader_feature_local _NORMALMAP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _BumpScale;
                float _TriplanarScale;
                float _TriplanarBlend;
            CBUFFER_END

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 lightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 positionCSRaw : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                float fogCoord : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 7);
            };

            // ===============================
            // Triplanar
            // ===============================
            float3 TriplanarWeights(float3 n)
            {
                float3 w = pow(abs(n), _TriplanarBlend);
                return w / max(dot(w,1.0), 1e-4);
            }

            half4 TriplanarAlbedo(float3 worldPos, float3 worldNormal)
            {
                float3 w = TriplanarWeights(worldNormal);
                float2 uvX = worldPos.zy * _TriplanarScale;
                float2 uvY = worldPos.xz * _TriplanarScale;
                float2 uvZ = worldPos.xy * _TriplanarScale;

                half4 sampleX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX);
                half4 sampleY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY);
                half4 sampleZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ);

                return sampleX * w.x + sampleY * w.y + sampleZ * w.z;
            }

            half3 TriplanarNormalWS(float3 worldPos, float3 worldNormal)
            {
                float3 w = TriplanarWeights(worldNormal);
                float2 uvX = worldPos.zy * _TriplanarScale;
                float2 uvY = worldPos.xz * _TriplanarScale;
                float2 uvZ = worldPos.xy * _TriplanarScale;

                half3 nX = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 nY = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 nZ = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);

                half3 worldX = half3(nX.z, nX.x, nX.y);
                half3 worldY = half3(nY.x, nY.z, nY.y);
                half3 worldZ = nZ;

                return normalize(worldX*w.x + worldY*w.y + worldZ*w.z);
            }

            // ===============================
            // Vertex
            // ===============================
            Varyings vert(Attributes v)
            {
                Varyings o;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                VertexNormalInputs norm = GetVertexNormalInputs(v.normalOS);

                o.positionCS = pos.positionCS;
                o.positionCSRaw = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = NormalizeNormalPerVertex(norm.normalWS);
                o.viewDirWS = GetWorldSpaceViewDir(pos.positionWS);
                o.shadowCoord = GetShadowCoord(pos);
                o.fogCoord = ComputeFogFactor(pos.positionCS.z);

                OUTPUT_LIGHTMAP_UV(v.lightmapUV, unity_LightmapST, o.lightmapUV);
                OUTPUT_SH(o.normalWS, o.vertexSH);

                o.vertexLighting = half3(0,0,0);

                return o;
            }

            // ===============================
            // Fragment
            // ===============================
            half4 frag(Varyings i) : SV_Target
            {
                SurfaceData surface;
                ZERO_INITIALIZE(SurfaceData, surface);

                // Triplanar Albedo
                half4 albedoSample = TriplanarAlbedo(i.positionWS, i.normalWS);
                surface.albedo = albedoSample.rgb * _BaseColor.rgb;
                surface.metallic = _Metallic;
                surface.smoothness = _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;

                // Triplanar normal
                half3 normalWS = i.normalWS;
                #ifdef _NORMALMAP
                    normalWS = TriplanarNormalWS(i.positionWS, normalWS);
                #endif
                normalWS = NormalizeNormalPerPixel(normalWS);

                InputData inputData;
                ZERO_INITIALIZE(InputData, inputData);
                inputData.positionWS = i.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = SafeNormalize(i.viewDirWS);
                inputData.shadowCoord = i.shadowCoord;
                inputData.fogCoord = i.fogCoord;
                inputData.vertexLighting = i.vertexLighting;
                inputData.bakedGI = SAMPLE_GI(i.lightmapUV, i.vertexSH, normalWS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(i.lightmapUV);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCSRaw);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }

            ENDHLSL
        }
    }

    FallBack Off
}