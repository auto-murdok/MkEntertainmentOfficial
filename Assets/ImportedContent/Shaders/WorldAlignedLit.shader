Shader "UEI/WorldAlignedLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        [Toggle(_NORMALMAP)] _EnableNormalMap("Enable Normal Map", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Scale", Float) = 1.0

        _MetallicGlossMap("Metallic (R) / Occlusion (G) / Smoothness (A)", 2D) = "white" {}
        _Metallic("Metallic", Range(0.0, 1.0)) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _OcclusionStrength("Occlusion Strength", Range(0.0, 1.0)) = 1.0

        [Header(World Alignment)]
        _TextureSize("World Texture Size (cm)", Float) = 300.0
        _BlendSharpness("Triplanar Blend Sharpness", Range(1.0, 32.0)) = 4.0

        [HideInInspector] _Cull("__cull", Float) = 0.0
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

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            // -------------------------------------
            // Material keywords
            #pragma shader_feature_local _NORMALMAP

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _BumpScale;
                half _Metallic;
                half _Smoothness;
                half _GlossMapScale;
                half _OcclusionStrength;
                float _TextureSize;
                float _BlendSharpness;
                half _Cull;
            CBUFFER_END

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            TEXTURE2D(_MetallicGlossMap);
            SAMPLER(sampler_MetallicGlossMap);

            Varyings LitPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;

                return output;
            }

            // World-aligned triplanar projection matching UE MF_WorldAligned_BaseMAterial
            // In UE, 1 unit = 1 cm. In Unity, 1 unit = 1 m (100 cm).
            // _TextureSize is given in cm (e.g. 300 cm = 3 meters).
            void SampleTriplanar(float3 positionWS, float3 normalWS,
                out half4 outAlbedo, out half3 outNormalWS, out half outMetallic, out half outSmoothness, out half outOcclusion)
            {
                float scale = 1.0 / max(0.01, (_TextureSize * 0.01)); // cm -> Unity meters

                float2 uvX = positionWS.zy * float2(-1.0, 1.0) * scale;
                float2 uvY = positionWS.xz * scale;
                float2 uvZ = positionWS.xy * scale;

                // Blend weights based on normal
                float3 blend = pow(abs(normalWS), _BlendSharpness);
                blend /= max(0.0001, (blend.x + blend.y + blend.z));

                // Albedo
                half4 colX = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX);
                half4 colY = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY);
                half4 colZ = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ);
                outAlbedo = (colX * blend.x + colY * blend.y + colZ * blend.z) * _BaseColor;

                // Packed ORM: R=Metallic, G=Occlusion, A=Smoothness
                half4 ormX = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvX);
                half4 ormY = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvY);
                half4 ormZ = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uvZ);
                half4 orm = ormX * blend.x + ormY * blend.y + ormZ * blend.z;

                outMetallic = orm.r * _Metallic;
                outSmoothness = orm.a * _GlossMapScale * _Smoothness;
                outOcclusion = lerp(1.0, orm.g, _OcclusionStrength);

                // Normal Maps
                #if defined(_NORMALMAP)
                half4 nX = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX);
                half4 nY = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY);
                half4 nZ = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ);

                half3 tnormalX = UnpackNormalScale(nX, _BumpScale);
                half3 tnormalY = UnpackNormalScale(nY, _BumpScale);
                half3 tnormalZ = UnpackNormalScale(nZ, _BumpScale);

                // Project tangents to world space for each projection plane
                half3 worldNormalX = half3(0, tnormalX.y, tnormalX.x * (normalWS.x < 0 ? -1 : 1));
                half3 worldNormalY = half3(tnormalY.x, 0, tnormalY.y * (normalWS.y < 0 ? -1 : 1));
                half3 worldNormalZ = half3(tnormalZ.x * (normalWS.z < 0 ? -1 : 1), tnormalZ.y, 0);

                outNormalWS = normalize(normalWS + (worldNormalX * blend.x + worldNormalY * blend.y + worldNormalZ * blend.z));
                #else
                outNormalWS = normalize(normalWS);
                #endif
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                float3 normalWS = normalize(input.normalWS);

                half4 albedo;
                half3 sampledNormalWS;
                half metallic;
                half smoothness;
                half occlusion;

                SampleTriplanar(positionWS, normalWS, albedo, sampledNormalWS, metallic, smoothness, occlusion);

                InputData inputData = (InputData)0;
                inputData.positionWS = positionWS;
                inputData.normalWS = sampledNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(positionWS, 1.0), input.positionCS.z);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.bakedGI = SampleSH(sampledNormalWS);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = albedo.a;
                surfaceData.metallic = metallic;
                surfaceData.specular = half3(0.04, 0.04, 0.04);
                surfaceData.smoothness = smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = occlusion;
                surfaceData.emission = half3(0, 0, 0);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 _LightDirection;

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
