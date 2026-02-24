Shader "Universal Render Pipeline/Lit (World Space)"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // ---- World-Space Triplanar Settings ----
        [Header(World Space Triplanar)]
        _WorldScale("World Texture Scale", Float) = 1.0
        _TriplanarSharpness("Triplanar Blend Sharpness", Range(1.0, 16.0)) = 4.0

        // SRP batching compatibility for Clear Coat (Not used in Lit)
        [HideInInspector] _ClearCoatMask("_ClearCoatMask", Float) = 0.0
        [HideInInspector] _ClearCoatSmoothness("_ClearCoatSmoothness", Float) = 0.0

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _BlendModePreserveSpecular("_BlendModePreserveSpecular", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0
        [HideInInspector] _AddPrecomputedVelocity("_AddPrecomputedVelocity", Float) = 0.0
        [HideInInspector] _XRMotionVectorsPass("_XRMotionVectorsPass", Float) = 1.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0

        // ObsoleteProperties
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _GlossMapScale("Smoothness", Float) = 0.0
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.0
        [HideInInspector] _GlossyReflections("EnvironmentReflections", Float) = 0.0

        [HideInInspector][NoScaleOffset]unity_Lightmaps("unity_Lightmaps", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_LightmapsInd("unity_LightmapsInd", 2DArray) = "" {}
        [HideInInspector][NoScaleOffset]unity_ShadowMasks("unity_ShadowMasks", 2DArray) = "" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        // ------------------------------------------------------------------
        //  Forward pass — world-space triplanar UV
        // ------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend[_SrcBlend][_DstBlend], [_SrcBlendAlpha][_DstBlendAlpha]
            ZWrite[_ZWrite]
            Cull[_Cull]
            AlphaToMask[_AlphaToMask]

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex   LitPassVertex
            #pragma fragment LitPassFragment

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _ALPHAPREMULTIPLY_ON _ALPHAMODULATE_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            // -------------------------------------
            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // LitInput.hlsl declares all textures/samplers and the UnityPerMaterial CBUFFER.
            // We do NOT include LitForwardPass.hlsl; we write our own vertex/fragment below.
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // _WorldScale and _TriplanarSharpness must live outside UnityPerMaterial to avoid
            // redefining the CBUFFER that LitInput.hlsl already closed.  This means they are
            // not SRP-Batcher compatible, but every other property remains compatible.
            float _WorldScale;
            float _TriplanarSharpness;

            // ================================================================
            // Triplanar helpers
            // ================================================================

            // Blend weights from the absolute world normal raised to _TriplanarSharpness.
            float3 TriplanarWeights(float3 worldNormal)
            {
                float3 w = pow(abs(worldNormal), max(_TriplanarSharpness, 0.001));
                return w / (w.x + w.y + w.z + 1e-5);
            }

            // Axis-aligned UVs with correct mirroring so seams line up at negative faces.
            //   X face  → YZ plane  : uv = (worldPos.z * signX, worldPos.y)
            //   Y face  → XZ plane  : uv = (worldPos.x * signY, worldPos.z)
            //   Z face  → XY plane  : uv = (worldPos.x * -signZ, worldPos.y)
            float2 TriUV_X(float3 p, float3 s) { return float2(p.z * s.x,  p.y      ); }
            float2 TriUV_Y(float3 p, float3 s) { return float2(p.x * s.y,  p.z      ); }
            float2 TriUV_Z(float3 p, float3 s) { return float2(p.x * -s.z, p.y      ); }

            // Sample any texture with triplanar projection.
            half4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 worldPos, float3 weights, float3 axisSign)
            {
                half4 xS = SAMPLE_TEXTURE2D(tex, samp, TriUV_X(worldPos, axisSign) * _WorldScale);
                half4 yS = SAMPLE_TEXTURE2D(tex, samp, TriUV_Y(worldPos, axisSign) * _WorldScale);
                half4 zS = SAMPLE_TEXTURE2D(tex, samp, TriUV_Z(worldPos, axisSign) * _WorldScale);
                return xS * weights.x + yS * weights.y + zS * weights.z;
            }

            // Sample the normal map with triplanar projection.
            // Uses Ben Golus' "Whiteout Blend" technique to produce a world-space normal.
            #ifdef _NORMALMAP
            half3 SampleNormalTriplanar(float3 worldPos, float3 worldNormal, float3 weights, float3 axisSign, float bumpScale)
            {
                float4 nX = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TriUV_X(worldPos, axisSign) * _WorldScale);
                float4 nY = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TriUV_Y(worldPos, axisSign) * _WorldScale);
                float4 nZ = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, TriUV_Z(worldPos, axisSign) * _WorldScale);

                float3 tnX = UnpackNormalScale(nX, bumpScale);
                float3 tnY = UnpackNormalScale(nY, bumpScale);
                float3 tnZ = UnpackNormalScale(nZ, bumpScale);

                // Reorient each tangent-space sample into world space (Whiteout blend).
                tnX = float3(tnX.xy * axisSign.x + worldNormal.zy, abs(tnX.z) * worldNormal.x);
                tnY = float3(tnY.xy * axisSign.y + worldNormal.xz, abs(tnY.z) * worldNormal.y);
                tnZ = float3(tnZ.xy * -axisSign.z + worldNormal.xy, abs(tnZ.z) * worldNormal.z);

                return normalize(tnX.zyx * weights.x + tnY.xzy * weights.y + tnZ.xyz * weights.z);
            }
            #endif // _NORMALMAP

            // ================================================================
            // Surface data initialisation (replaces InitializeStandardLitSurfaceData)
            // ================================================================

            void InitializeSurfaceDataWorldSpace(float3 worldPos, float3 worldNormal, out SurfaceData s)
            {
                s = (SurfaceData)0;

                float3 weights  = TriplanarWeights(worldNormal);
                float3 axisSign = sign(worldNormal);

                // --- Albedo / Alpha ---
                half4 albedoAlpha = SampleTriplanar(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), worldPos, weights, axisSign) * _BaseColor;
                s.alpha = albedoAlpha.a;

                #ifdef _ALPHATEST_ON
                    clip(s.alpha - _Cutoff);
                #endif

                s.albedo = albedoAlpha.rgb;
                #if defined(_ALPHAPREMULTIPLY_ON)
                    s.albedo *= s.alpha;
                #elif defined(_ALPHAMODULATE_ON)
                    s.albedo = lerp(half3(1.0h, 1.0h, 1.0h), s.albedo, s.alpha);
                #endif

                // --- Metallic / Specular + Smoothness ---
                #if defined(_SPECULAR_SETUP)
                    s.metallic = half(1.0);
                    #if defined(_METALLICSPECGLOSSMAP)
                        half4 specGloss = SampleTriplanar(TEXTURE2D_ARGS(_SpecGlossMap, sampler_SpecGlossMap), worldPos, weights, axisSign);
                        s.specular = specGloss.rgb;
                        #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                            s.smoothness = albedoAlpha.a * _Smoothness;
                        #else
                            s.smoothness = specGloss.a * _Smoothness;
                        #endif
                    #else
                        s.specular = _SpecColor.rgb;
                        #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                            s.smoothness = albedoAlpha.a * _Smoothness;
                        #else
                            s.smoothness = _Smoothness;
                        #endif
                    #endif
                #else  // metallic workflow
                    s.specular = half3(0, 0, 0);
                    #if defined(_METALLICSPECGLOSSMAP)
                        half4 metallicGloss = SampleTriplanar(TEXTURE2D_ARGS(_MetallicGlossMap, sampler_MetallicGlossMap), worldPos, weights, axisSign);
                        s.metallic = metallicGloss.r * _Metallic;
                        #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                            s.smoothness = albedoAlpha.a * _Smoothness;
                        #else
                            s.smoothness = metallicGloss.a * _Smoothness;
                        #endif
                    #else
                        s.metallic = _Metallic;
                        #if defined(_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A)
                            s.smoothness = albedoAlpha.a * _Smoothness;
                        #else
                            s.smoothness = _Smoothness;
                        #endif
                    #endif
                #endif

                // normalTS is not used directly for world-space normals;
                // we compute the final world-space normal separately in the fragment.
                s.normalTS = half3(0.0h, 0.0h, 1.0h);

                // --- Occlusion ---
                #if defined(_OCCLUSIONMAP)
                    half4 occ = SampleTriplanar(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), worldPos, weights, axisSign);
                    s.occlusion = LerpWhiteTo(occ.g, _OcclusionStrength);
                #else
                    s.occlusion = half(1.0);
                #endif

                // --- Emission ---
                #if defined(_EMISSION)
                    s.emission = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), worldPos, weights, axisSign).rgb * _EmissionColor.rgb;
                #else
                    s.emission = half3(0, 0, 0);
                #endif

                // --- Detail maps (world-space triplanar) ---
                #if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
                    half  detailMask   = SampleTriplanar(TEXTURE2D_ARGS(_DetailMask,       sampler_DetailMask),       worldPos, weights, axisSign).a;
                    half4 detailAlbedo = SampleTriplanar(TEXTURE2D_ARGS(_DetailAlbedoMap,  sampler_DetailAlbedoMap),  worldPos, weights, axisSign);

                    #if defined(_DETAIL_MULX2)
                        s.albedo = s.albedo * LerpWhiteTo(detailAlbedo.rgb * unity_ColorSpaceDouble.rgb, detailMask * _DetailAlbedoMapScale);
                    #elif defined(_DETAIL_SCALED)
                        s.albedo = lerp(s.albedo, s.albedo * detailAlbedo.rgb * unity_ColorSpaceDouble.rgb, detailMask * _DetailAlbedoMapScale);
                    #endif
                    // Detail normal handled below via the _NORMALMAP path
                #endif
            }

            // ================================================================
            // Vertex / Fragment structs
            // ================================================================

            struct Attributes
            {
                float4 positionOS         : POSITION;
                float3 normalOS           : NORMAL;
                float4 tangentOS          : TANGENT;
                float2 texcoord           : TEXCOORD0;
                float2 staticLightmapUV   : TEXCOORD1;
                float2 dynamicLightmapUV  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv                      : TEXCOORD0;
                float3 positionWS              : TEXCOORD1;
                half3  normalWS                : TEXCOORD2;
            #ifdef _NORMALMAP
                half4  tangentWS               : TEXCOORD3; // xyz tangent, w sign
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3  vertexLighting          : TEXCOORD4;
            #endif
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord             : TEXCOORD5;
            #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV       : TEXCOORD9;
            #endif
            #if USE_APV_PROBE_OCCLUSION
                float4 probeOcclusion          : TEXCOORD10;
            #endif
                half4  fogFactorAndVertexLight : TEXCOORD11; // x: fog, yzw: vertex light
                float4 positionCS              : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ================================================================
            // Vertex shader
            // ================================================================

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs   normalInput  = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                    fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif

                // Standard mesh UV is still passed (used only for lightmaps)
                output.uv         = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.normalWS   = normalInput.normalWS;
                output.positionWS = vertexInput.positionWS;

            #ifdef _NORMALMAP
                real sign = input.tangentOS.w * GetOddNegativeScale();
                output.tangentWS = half4(normalInput.tangentWS.xyz, sign);
            #endif

                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif

                // OUTPUT_SH4 writes spherical harmonics (and probe occlusion when APV is active).
                // When USE_APV_PROBE_OCCLUSION is false the macro ignores output.probeOcclusion,
                // so passing it is always safe even though the field is conditionally declared.
                OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz,
                           GetWorldSpaceNormalizeViewDir(vertexInput.positionWS),
                           output.vertexSH, output.probeOcclusion);

                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
            #endif

                output.positionCS = vertexInput.positionCS;
                return output;
            }

            // ================================================================
            // Fragment shader
            // ================================================================

            void LitPassFragment(
                Varyings input,
                out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out float4 outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(LOD_FADE_CROSSFADE)
                LODFadeCrossFade(input.positionCS);
            #endif

                float3 positionWS    = input.positionWS;
                half3  geometryNorm  = NormalizeNormalPerPixel(input.normalWS);

                // ---- Surface data (triplanar world-space) ----
                SurfaceData surfaceData;
                InitializeSurfaceDataWorldSpace(positionWS, geometryNorm, surfaceData);

                // ---- World-space normal (geometry or normal-mapped) ----
                half3 finalNormalWS;
            #ifdef _NORMALMAP
                {
                    float3 weights  = TriplanarWeights(geometryNorm);
                    float3 axisSign = sign(geometryNorm);
                    finalNormalWS = SampleNormalTriplanar(positionWS, geometryNorm, weights, axisSign, _BumpScale);

                    // Blend in detail normal if requested
                    #if defined(_DETAIL_MULX2) || defined(_DETAIL_SCALED)
                    {
                        half4 dn = SampleTriplanar(TEXTURE2D_ARGS(_DetailNormalMap, sampler_DetailNormalMap), positionWS, weights, axisSign);
                        half3 detailNorm = UnpackNormalScale(dn, _DetailNormalMapScale);
                        // Re-orient detail normal the same way and blend
                        detailNorm = float3(detailNorm.xy * axisSign.x + geometryNorm.zy, abs(detailNorm.z) * geometryNorm.x);
                        finalNormalWS = NormalizeNormalPerPixel(half3(finalNormalWS.xy + detailNorm.xy, finalNormalWS.z));
                    }
                    #endif
                }
            #else
                finalNormalWS = geometryNorm;
            #endif

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);

                // ---- Build a TBN for inputData.tangentToWorld ----
                // Required for debug views and some reflection calculations.
                // Derived from the geometry normal so it is always valid.
                half3 tAlt = (abs(geometryNorm.y) < 0.999h) ? half3(0,1,0) : half3(1,0,0);
                half3 tWS  = normalize(cross(tAlt, (half3)geometryNorm));
                half3 bWS  = cross((half3)geometryNorm, tWS);
                half3x3 tangentToWorld = half3x3(tWS, bWS, geometryNorm);

                // ---- InputData ----
                InputData inputData = (InputData)0;
                inputData.positionWS              = positionWS;
                inputData.positionCS              = input.positionCS;
                inputData.normalWS                = NormalizeNormalPerPixel(finalNormalWS);
                inputData.viewDirectionWS         = viewDirWS;
                inputData.tangentToWorld          = tangentToWorld;

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
            #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
            #endif

                inputData.fogCoord        = InitializeInputDataFog(float4(positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting  = input.fogFactorAndVertexLight.yzw;

            #if defined(DYNAMICLIGHTMAP_ON)
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
            #endif

                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask              = SAMPLE_SHADOWMASK(input.staticLightmapUV);

            #if defined(DEBUG_DISPLAY)
                #ifdef DYNAMICLIGHTMAP_ON
                    inputData.dynamicLightmapUV = input.dynamicLightmapUV;
                #endif
                #ifdef LIGHTMAP_ON
                    inputData.staticLightmapUV = input.staticLightmapUV;
                #else
                    inputData.vertexSH = input.vertexSH;
                #endif
                inputData.geomNormalWS = geometryNorm;
            #endif

                // ---- Lighting ----
                half4 color = UniversalFragmentPBR(inputData, surfaceData);

                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a   = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));

                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                uint renderingLayers   = GetMeshRenderingLayer();
                outRenderingLayers     = float4(EncodeMeshRenderingLayer(renderingLayers), 0, 0, 0);
            #endif
            }

            ENDHLSL
        }

        // ------------------------------------------------------------------
        //  All passes below are unchanged from the stock URP Lit shader.
        //  They use mesh UVs for alpha-test, which is fine for depth/shadow.
        // ------------------------------------------------------------------

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "GBuffer"
            Tags { "LightMode" = "UniversalGBuffer" }

            ZWrite[_ZWrite]
            ZTest LEqual
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 4.5
            #pragma exclude_renderers gles3 glcore

            #pragma vertex LitGBufferPassVertex
            #pragma fragment LitGBufferPassFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _RENDER_PASS_ENABLED
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fragment _ LIGHTMAP_BICUBIC_SAMPLING
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitGBufferPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitDepthNormalsPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMetaLit

            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _SPECGLOSSMAP
            #pragma shader_feature EDITOR_VISUALIZATION

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitMetaPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }

            Blend[_SrcBlend][_DstBlend]
            ZWrite[_ZWrite]
            Cull[_Cull]

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Universal2D.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }
            ColorMask RG

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "XRMotionVectors"
            Tags { "LightMode" = "XRMotionVectors" }
            ColorMask RGBA

            Stencil
            {
                WriteMask 1
                Ref 1
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma shader_feature_local_vertex _ADD_PRECOMPUTED_VELOCITY
            #define APPLICATION_SPACE_WARP_MOTION 1

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ObjectMotionVectors.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
