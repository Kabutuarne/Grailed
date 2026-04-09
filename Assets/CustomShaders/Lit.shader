Shader "Universal Render Pipeline/Lit (Local Space Triplanar)"
{
    Properties
    {
        _WorkflowMode("WorkflowMode", Float) = 1.0
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _BumpScale("Normal Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0)
        _EmissionMap("Emission Map", 2D) = "white" {}

        [Header(Triplanar Settings)]
        _WorldScale("Texture Scale (tiles per unit)", Float) = 1.0
        _TriplanarSharpness("Triplanar Blend Sharpness", Range(1.0, 16.0)) = 4.0

        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "UniversalMaterialType" = "Lit" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            // Universal Pipeline keywords for lighting and shadows
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ LIGHTMAP_ON
            
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _OCCLUSIONMAP

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _WorldScale;
            float _TriplanarSharpness;

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 texcoord   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                float3 positionScaledLS : TEXCOORD3; // Scaled Local Pos
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Calculate object scale from the world matrix
            float3 GetObjectScale() {
                return float3(
                    length(float3(unity_ObjectToWorld[0].x, unity_ObjectToWorld[1].x, unity_ObjectToWorld[2].x)),
                    length(float3(unity_ObjectToWorld[0].y, unity_ObjectToWorld[1].y, unity_ObjectToWorld[2].y)),
                    length(float3(unity_ObjectToWorld[0].z, unity_ObjectToWorld[1].z, unity_ObjectToWorld[2].z))
                );
            }

            Varyings LitPassVertex(Attributes input) {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vInput.positionCS;
                output.positionWS = vInput.positionWS;
                output.normalWS   = GetVertexNormalInputs(input.normalOS, input.tangentOS).normalWS;

                // "Glue" logic: Use local position multiplied by current scale
                output.positionScaledLS = input.positionOS.xyz * GetObjectScale();

                return output;
            }

            // Helper for sampling textures triplanar-style
            half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 p, float3 w, float4 st) {
                float2 uvX = (p.zy * _WorldScale) * st.xy + st.zw;
                float2 uvY = (p.xz * _WorldScale) * st.xy + st.zw;
                float2 uvZ = (p.xy * _WorldScale) * st.xy + st.zw;
                return SAMPLE_TEXTURE2D(tex, samp, uvX) * w.x + SAMPLE_TEXTURE2D(tex, samp, uvY) * w.y + SAMPLE_TEXTURE2D(tex, samp, uvZ) * w.z;
            }

            void LitPassFragment(Varyings input, out half4 outColor : SV_Target0) {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 p = input.positionScaledLS;
                half3 normalWS = normalize(input.normalWS);
                
                // Calculate blending weights based on the normal [cite: 22]
                float3 w = pow(abs(normalWS), max(_TriplanarSharpness, 0.01));
                w /= (w.x + w.y + w.z);

                // Initialize SurfaceData for URP Lighting 
                SurfaceData s = (SurfaceData)0;
                half4 albedo = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), p, w, _BaseMap_ST) * _BaseColor;
                s.albedo = albedo.rgb;
                s.alpha = albedo.a;
                s.metallic = _Metallic;
                s.smoothness = _Smoothness;
                s.normalTS = half3(0, 0, 1); // Normal mapping would go here
                
                #if defined(_OCCLUSIONMAP)
                    s.occlusion = SampleTriplanar(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), p, w, _BaseMap_ST).g * _OcclusionStrength;
                #else
                    s.occlusion = 1.0;
                #endif

                #if defined(_EMISSION)
                    s.emission = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), p, w, _BaseMap_ST).rgb * _EmissionColor.rgb;
                #endif

                // Initialize InputData for lighting calculation [cite: 132, 134]
                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // Apply URP PBR Lighting [cite: 150]
                outColor = UniversalFragmentPBR(inputData, s);
            }
            ENDHLSL
        }
        
        // Use the original ShadowCaster pass so the object casts shadows [cite: 155]
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}