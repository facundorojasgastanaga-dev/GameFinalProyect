// IRON REQUIEM hard-surface shader (07/11/2026). One URP forward shader for environment, player modules and robots.
// SRP Batcher compatible: every material property lives in UnityPerMaterial; per-renderer variation (damage) can be
// set with MaterialPropertyBlock or, preferably, with a few shared materials (keeps the SRP Batcher path).
// Features are shader_feature_local (only combinations used by materials are compiled):
//   _TRIPLANAR      base/normal mapped in world space (meshes whose UVs do not match the texture scale)
//   _NORMALMAP      tangent-space normal map (UV path) or triplanar normal (triplanar path)
//   _DIRT           world-space grime: triplanar noise, stronger near the floor and in cavities (AO)
//   _EDGEWEAR       worn bright metal on edges: screen-space curvature (normal derivatives) x noise, or a mask (G of _MaskMap)
//   _RIM            emissive fresnel rim, for reading silhouettes in the dark
//   _PANELS         emissive panels (mask R of _MaskMap or _EmissionMap) with a time pulse
// Damage (_Damage 0..1) is a uniform branch, not a keyword: darkens, burns (noise) and pulses red.
Shader "IronRequiem/HardSurface"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        _Metallic("Metallic", Range(0,1)) = 0.2
        _Smoothness("Smoothness", Range(0,1)) = 0.4
        [NoScaleOffset] _MetallicGlossMap("Metallic (R) Smoothness (A)", 2D) = "white" {}
        [Toggle(_NORMALMAP)] _UseNormalMap("Normal Map", Float) = 0
        [Normal][NoScaleOffset] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0,2)) = 1
        [Toggle(_TRIPLANAR)] _UseTriplanar("World Triplanar Mapping", Float) = 0
        _TriplanarScale("Triplanar Metres Per Tile", Float) = 3
        _TriplanarSharpness("Triplanar Blend Sharpness", Range(1,16)) = 6
        [NoScaleOffset] _MaskMap("Mask (R panels, G wear, B occlusion)", 2D) = "black" {}
        [NoScaleOffset] _NoiseMap("Tileable Noise (R)", 2D) = "gray" {}

        [Header(Dirt)]
        [Toggle(_DIRT)] _UseDirt("World Dirt", Float) = 0
        _DirtColor("Dirt Color", Color) = (0.12,0.1,0.08,1)
        _DirtAmount("Dirt Amount", Range(0,1)) = 0.45
        _DirtScale("Dirt Metres Per Tile", Float) = 2.5
        _DirtFloorHeight("Dirt Floor Fade Height (m above _DirtFloorY)", Float) = 1.2
        _DirtFloorY("Floor Height", Float) = 0

        [Header(Edge Wear)]
        [Toggle(_EDGEWEAR)] _UseEdgeWear("Edge Wear", Float) = 0
        _WearColor("Worn Metal Color", Color) = (0.62,0.6,0.56,1)
        _WearAmount("Wear Amount", Range(0,1)) = 0.6
        _WearCurvature("Curvature Sensitivity", Range(0,20)) = 6
        _WearFromMask("Wear From Mask G (0 curvature, 1 mask)", Range(0,1)) = 0

        [Header(Rim)]
        [Toggle(_RIM)] _UseRim("Emissive Rim", Float) = 0
        [HDR] _RimColor("Rim Color", Color) = (0.25,0.5,0.7,1)
        _RimPower("Rim Power", Range(0.5,8)) = 3.5

        [Header(Emissive Panels)]
        [Toggle(_PANELS)] _UsePanels("Emissive Panels", Float) = 0
        [NoScaleOffset] _EmissionMap("Emission Map (optional, multiplied by mask R)", 2D) = "white" {}
        [HDR] _EmissionColor("Panel Emission", Color) = (0,0.8,1.2,1)
        _PulseSpeed("Pulse Speed (Hz)", Float) = 0.6
        _PulseAmount("Pulse Amount", Range(0,1)) = 0.35
        _PanelBoost("Panel Boost (runtime, e.g. firing)", Range(0,8)) = 0

        [Header(Damage)]
        _Damage("Damage", Range(0,1)) = 0
        [HDR] _DamageColor("Damage Pulse Color", Color) = (2.4,0.12,0.04,1)
        _DamagePulseSpeed("Damage Pulse Speed (Hz)", Float) = 2.2

        [HideInInspector] _Cull("__cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _Metallic, _Smoothness, _BumpScale;
            float _TriplanarScale, _TriplanarSharpness;
            half4 _DirtColor; half _DirtAmount; float _DirtScale, _DirtFloorHeight, _DirtFloorY;
            half4 _WearColor; half _WearAmount, _WearCurvature, _WearFromMask;
            half4 _RimColor; half _RimPower;
            half4 _EmissionColor; float _PulseSpeed; half _PulseAmount, _PanelBoost;
            half _Damage; half4 _DamageColor; float _DamagePulseSpeed;
            half _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
        TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);
        TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
        TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _TRIPLANAR
            #pragma shader_feature_local_fragment _DIRT
            #pragma shader_feature_local_fragment _EDGEWEAR
            #pragma shader_feature_local_fragment _RIM
            #pragma shader_feature_local_fragment _PANELS

            // URP lighting actually used by this project (Forward, 1 cascade, soft shadows, SSAO, fog). No lightmaps,
            // light layers, cookies, Forward+, DBuffer decals (screen-space decals need no keyword) or debug display.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            // No lightmap variants: everything (static included) takes baked indirect light from light probes (SH);
            // the LIGHTMAP_ON path below is kept for a future lightmapped variant but is not compiled.

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                #if defined(_NORMALMAP) && !defined(_TRIPLANAR)
                half4 tangentWS : TEXCOORD3;
                #endif
                half fogFactor : TEXCOORD4;
                #if defined(LIGHTMAP_ON)
                float2 staticLightmapUV : TEXCOORD5;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = n.normalWS;
                #if defined(_NORMALMAP) && !defined(_TRIPLANAR)
                o.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                #endif
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                #if defined(LIGHTMAP_ON)
                o.staticLightmapUV = input.staticLightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
                #endif
                return o;
            }

            float3 TriWeights(float3 n)
            {
                float3 w = pow(abs(n), _TriplanarSharpness);
                return w / max(dot(w, 1.0), 1e-4);
            }

            half4 SampleTri(TEXTURE2D_PARAM(tex, smp), float3 pos, float3 w, float scale)
            {
                float3 q = pos / max(scale, 1e-3);
                return SAMPLE_TEXTURE2D(tex, smp, q.zy) * w.x + SAMPLE_TEXTURE2D(tex, smp, q.xz) * w.y + SAMPLE_TEXTURE2D(tex, smp, q.xy) * w.z;
            }

            half Noise(float3 pos, float3 w, float scale)
            {
                return SampleTri(TEXTURE2D_ARGS(_NoiseMap, sampler_NoiseMap), pos, w, scale).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 geomN = normalize(input.normalWS);
                float3 w = TriWeights(geomN);

                // ---- base colour and normal ----
                #if defined(_TRIPLANAR)
                    half4 baseSample = SampleTri(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap), input.positionWS, w, _TriplanarScale);
                    float3 normalWS = geomN;
                    #if defined(_NORMALMAP)
                        // Whiteout-style triplanar normal blend (cheap, keeps the geometric normal as the base).
                        float3 q = input.positionWS / max(_TriplanarScale, 1e-3);
                        half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, q.zy), _BumpScale);
                        half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, q.xz), _BumpScale);
                        half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, q.xy), _BumpScale);
                        float3 nx = float3(0, tx.y, tx.x) * sign(geomN.x);
                        float3 ny = float3(ty.x, 0, ty.y) * sign(geomN.y);
                        float3 nz = float3(tz.x, tz.y, 0) * sign(geomN.z);
                        normalWS = normalize(geomN + nx * w.x + ny * w.y + nz * w.z);
                    #endif
                    half4 mask = SampleTri(TEXTURE2D_ARGS(_MaskMap, sampler_MaskMap), input.positionWS, w, _TriplanarScale);
                #else
                    half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    float3 normalWS = geomN;
                    #if defined(_NORMALMAP)
                        half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                        float sgn = input.tangentWS.w;
                        float3 bitangent = sgn * cross(geomN, input.tangentWS.xyz);
                        normalWS = normalize(TransformTangentToWorld(nTS, half3x3(input.tangentWS.xyz, bitangent, geomN)));
                    #endif
                    half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                #endif

                half3 albedo = baseSample.rgb * _BaseColor.rgb;
                #if defined(_TRIPLANAR)
                half4 metalGloss = half4(1, 1, 1, 1);
                #else
                half4 metalGloss = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, input.uv);
                #endif
                half metallic = _Metallic * metalGloss.r;
                half smoothness = _Smoothness * metalGloss.a;
                half occlusion = 1.0h - mask.b * 0.6h;
                half3 emission = 0;
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                #if defined(_DIRT) || defined(_EDGEWEAR)
                    half n1 = Noise(input.positionWS, w, _DirtScale);
                #else
                    half n1 = 0.5h;
                #endif

                // ---- world grime: noise, floor band, cavities (mask B) ----
                #if defined(_DIRT)
                    half floorBand = saturate(1.0h - (input.positionWS.y - _DirtFloorY) / max(_DirtFloorHeight, 1e-3));
                    half upFacing = saturate(geomN.y) * 0.35h;
                    half dirt = saturate((n1 - (1.0h - _DirtAmount)) * 2.5h + floorBand * _DirtAmount * 0.8h + mask.b * 0.5h + upFacing * _DirtAmount);
                    albedo = lerp(albedo, _DirtColor.rgb, dirt * 0.85h);
                    smoothness *= 1.0h - dirt * 0.7h;
                    metallic *= 1.0h - dirt * 0.6h;
                #endif

                // ---- edge wear: curvature from normal derivatives, or mask G ----
                #if defined(_EDGEWEAR)
                    float3 dn = fwidth(geomN);
                    float dp = max(length(fwidth(input.positionWS)), 1e-4);
                    half curvature = saturate(length(dn) / dp * 0.02h * _WearCurvature);
                    half wearSource = lerp(curvature, mask.g, _WearFromMask);
                    half wear = saturate((wearSource * (0.6h + n1 * 0.8h) - (1.0h - _WearAmount)) * 3.0h);
                    albedo = lerp(albedo, _WearColor.rgb, wear);
                    metallic = lerp(metallic, 1.0h, wear * 0.8h);
                    smoothness = lerp(smoothness, 0.65h, wear);
                #endif

                // ---- emissive panels with a slow time pulse (+ runtime boost, e.g. while firing) ----
                #if defined(_PANELS)
                    half pulse = 1.0h - _PulseAmount * (0.5h + 0.5h * sin(_Time.y * _PulseSpeed * 6.2831853h));
                    half3 panelTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                    emission += _EmissionColor.rgb * panelTex * mask.r * pulse * (1.0h + _PanelBoost);
                #endif

                // ---- emissive fresnel rim ----
                #if defined(_RIM)
                    half fres = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _RimPower);
                    emission += _RimColor.rgb * fres;
                #endif

                // ---- damage: darken, burn patches, red pulse (uniform branch, no keyword) ----
                UNITY_BRANCH
                if (_Damage > 0.001h)
                {
                    half burnNoise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, input.positionWS.xz * 0.37 + input.positionWS.y * 0.21).r;
                    half burn = saturate((burnNoise - (1.0h - _Damage)) * 4.0h);
                    albedo *= lerp(1.0h, 0.45h, _Damage);
                    albedo = lerp(albedo, half3(0.03h, 0.025h, 0.02h), burn);
                    smoothness *= 1.0h - burn * 0.8h;
                    half beat = 0.5h + 0.5h * sin(_Time.y * _DamagePulseSpeed * 6.2831853h);
                    half ember = saturate(burn * (1.0h - burn) * 4.0h); // glowing rim of burnt patches
                    emission += _DamageColor.rgb * (ember * 0.6h + beat * beat * 0.25h * _Damage) * _Damage;
                    #if defined(_PANELS)
                    emission *= 1.0h - _Damage * 0.5h * (1.0h - beat); // panels flicker with the damage beat
                    #endif
                }

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = NormalizeNormalPerPixel(normalWS);
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = 0;
                #if defined(LIGHTMAP_ON)
                inputData.bakedGI = SampleLightmap(input.staticLightmapUV, inputData.normalWS);
                #else
                inputData.bakedGI = SampleSH(inputData.normalWS);
                #endif
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = metallic;
                surface.smoothness = smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.emission = emission;
                surface.occlusion = occlusion;
                surface.alpha = 1;
                surface.specular = 0;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                o.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
                return o;
            }
            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DepthVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }
            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            Varyings DNVert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 DNFrag(Varyings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
            }
            ENDHLSL
        }
        Pass
        {
            Name "Meta"
            Tags { "LightMode"="Meta" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex MetaVert
            #pragma fragment MetaFrag
            #pragma shader_feature_local_fragment _PANELS
            #pragma shader_feature EDITOR_VISUALIZATION
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/MetaInput.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv0 : TEXCOORD0; float2 uv1 : TEXCOORD1; float2 uv2 : TEXCOORD2; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings MetaVert(Attributes input)
            {
                Varyings o;
                o.positionCS = UnityMetaVertexPosition(input.positionOS.xyz, input.uv1, input.uv2);
                o.uv = TRANSFORM_TEX(input.uv0, _BaseMap);
                return o;
            }
            half4 MetaFrag(Varyings input) : SV_Target
            {
                UnityMetaInput m = (UnityMetaInput)0;
                m.Albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                #if defined(_PANELS)
                m.Emission = _EmissionColor.rgb * SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv).r;
                #endif
                return UnityMetaFragment(m);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
