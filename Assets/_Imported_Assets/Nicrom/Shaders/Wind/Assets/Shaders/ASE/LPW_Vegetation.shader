Shader "Nicrom/LPW/URP/Low Poly Vegetation"
{
    Properties
    {
        _Color              ("Color",               Color)          = (1,1,1,1)
        [NoScaleOffset]
        _MainTex            ("Main Tex",            2D)             = "white" {}

        [Space]
        _Metallic           ("Metallic",            Range(0,1))     = 0
        _Smoothness         ("Smoothness",          Range(0,1))     = 0

        [Header(Main Bending)][Space]
        _MBDefaultBending   ("MB Default Bending",  Float)          = 0
        [Space]
        _MBAmplitude        ("MB Amplitude",        Float)          = 1.5
        _MBAmplitudeOffset  ("MB Amplitude Offset", Float)          = 2
        [Space]
        _MBFrequency        ("MB Frequency",        Float)          = 1.11
        _MBFrequencyOffset  ("MB Frequency Offset", Float)          = 0
        [Space]
        _MBPhase            ("MB Phase",            Float)          = 1
        [Space]
        _MBWindDir          ("MB Wind Dir",         Range(0,360))   = 0
        _MBWindDirOffset    ("MB Wind Dir Offset",  Range(0,180))   = 20
        [Space]
        _MBMaxHeight        ("MB Max Height",       Float)          = 10

        [NoScaleOffset][Header(World Space Noise)][Space]
        _NoiseTexture       ("Noise Texture",       2D)             = "bump" {}
        _NoiseTextureTilling("Noise Tilling - Static (XY) Animated (ZW)", Vector) = (1,1,1,1)
        _NoisePannerSpeed   ("Noise Panner Speed",  Vector)         = (0.05,0.03,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType"            = "Opaque"
            "RenderPipeline"        = "UniversalPipeline"
            "Queue"                 = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex   VegetationVert
            #pragma fragment VegetationFrag

            // URP lighting keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ----------------------------------------------------------------
            // Textures
            // ----------------------------------------------------------------
            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTexture); SAMPLER(sampler_NoiseTexture);

            // ----------------------------------------------------------------
            // Constant buffer
            // ----------------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4  _Color;
                float   _Metallic;
                float   _Smoothness;

                float   _MBDefaultBending;
                float   _MBAmplitude;
                float   _MBAmplitudeOffset;
                float   _MBFrequency;
                float   _MBFrequencyOffset;
                float   _MBPhase;
                float   _MBWindDir;
                float   _MBWindDirOffset;
                float   _MBMaxHeight;

                float4  _NoiseTextureTilling;
                float2  _NoisePannerSpeed;
            CBUFFER_END

            // ----------------------------------------------------------------
            // Structs
            // ----------------------------------------------------------------
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
                float2 lightmapUV   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 3);
                float  fogCoord     : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ----------------------------------------------------------------
            // Rotate around axis helper
            // ----------------------------------------------------------------
            float3 RotateAroundAxis(float3 center, float3 original, float3 u, float angle)
            {
                original -= center;
                float C  = cos(angle), S = sin(angle), t = 1.0 - C;
                float3x3 m = float3x3(
                    t*u.x*u.x + C,       t*u.x*u.y - S*u.z,  t*u.x*u.z + S*u.y,
                    t*u.x*u.y + S*u.z,   t*u.y*u.y + C,       t*u.y*u.z - S*u.x,
                    t*u.x*u.z - S*u.y,   t*u.y*u.z + S*u.x,  t*u.z*u.z + C
                );
                return mul(m, original) + center;
            }

            // ----------------------------------------------------------------
            // Vertex
            // ----------------------------------------------------------------
            Varyings VegetationVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS = IN.positionOS.xyz;

                // ---- Wind bending ----
                float3 objOriginWS = TransformObjectToWorld(float3(0,0,0));
                float2 wsUV        = objOriginWS.xz;

                // Animated noise
                float2 animTiling  = _NoiseTextureTilling.zw;
                float2 panner      = 0.1 * _Time.y * _NoisePannerSpeed;
                float  animNoise   = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture,
                                        wsUV * animTiling + panner, 0).r;

                // Wind direction axis
                float windDirRad   = radians((_MBWindDir +
                    _MBWindDirOffset * (-1.0 + animNoise * 2.0)) * -1.0);
                float3 windDirWS   = float3(cos(windDirRad), 0.0, sin(windDirRad));
                // Transform to object space for rotation axis
                float3 windDirOS   = normalize(
                    TransformWorldToObject(windDirWS) - TransformWorldToObject(float3(0,0,0)));

                // Static noise for amplitude/frequency variation
                float2 staticTiling = _NoiseTextureTilling.xy;
                float  staticNoise  = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture,
                                        wsUV * staticTiling, 0).r;

                float amp  = _MBAmplitude + _MBAmplitudeOffset * staticNoise;
                float freq = _MBFrequency + _MBFrequencyOffset * staticNoise;
                float sineInput = ((objOriginWS.x + objOriginWS.z)
                                  + (_Time.y * freq)) * _MBPhase;

                float angle = radians((amp * sin(sineInput) + _MBDefaultBending)
                              * (posOS.y / max(_MBMaxHeight, 0.001)));

                // Only bend above ground
                float mask = step(0.01, posOS.y);

                float3 pivot      = float3(0.0, posOS.y, 0.0);
                float3 rotated1   = RotateAroundAxis(pivot,    posOS,    windDirOS, angle);
                float3 rotated2   = RotateAroundAxis(float3(0,0,0), rotated1, windDirOS, angle);
                posOS += (rotated2 - posOS) * mask;

                // ---- Output ----
                VertexPositionInputs posInputs = GetVertexPositionInputs(posOS);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionCS = posInputs.positionCS;
                OUT.positionWS = posInputs.positionWS;
                OUT.normalWS   = nrmInputs.normalWS;
                OUT.uv         = IN.uv;
                OUT.fogCoord   = ComputeFogFactor(posInputs.positionCS.z);

                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(OUT.normalWS, OUT.vertexSH);

                return OUT;
            }

            // ----------------------------------------------------------------
            // Fragment
            // ----------------------------------------------------------------
            half4 VegetationFrag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float4 albedoTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float3 albedo    = (albedoTex * _Color).rgb;

                float3 normalWS  = normalize(IN.normalWS);

                // URP PBR lighting
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS        = IN.positionWS;
                lightingInput.normalWS          = normalWS;
                lightingInput.viewDirectionWS   = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                lightingInput.fogCoord          = IN.fogCoord;
                lightingInput.bakedGI           = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, normalWS);
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionCS);
                lightingInput.shadowMask        = SAMPLE_SHADOWMASK(IN.lightmapUV);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo      = albedo;
                surface.metallic    = _Metallic;
                surface.smoothness  = _Smoothness;
                surface.occlusion   = 1.0;
                surface.alpha       = 1.0;

                half4 color = UniversalFragmentPBR(lightingInput, surface);
                color.rgb   = MixFog(color.rgb, IN.fogCoord);
                return color;
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_NoiseTexture); SAMPLER(sampler_NoiseTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Metallic; float _Smoothness;
                float  _MBDefaultBending; float _MBAmplitude; float _MBAmplitudeOffset;
                float  _MBFrequency; float _MBFrequencyOffset; float _MBPhase;
                float  _MBWindDir; float _MBWindDirOffset; float _MBMaxHeight;
                float4 _NoiseTextureTilling;
                float2 _NoisePannerSpeed;
            CBUFFER_END

            float3 RotateAroundAxis(float3 center, float3 original, float3 u, float angle)
            {
                original -= center;
                float C = cos(angle), S = sin(angle), t = 1.0-C;
                float3x3 m = float3x3(
                    t*u.x*u.x+C,     t*u.x*u.y-S*u.z, t*u.x*u.z+S*u.y,
                    t*u.x*u.y+S*u.z, t*u.y*u.y+C,     t*u.y*u.z-S*u.x,
                    t*u.x*u.z-S*u.y, t*u.y*u.z+S*u.x, t*u.z*u.z+C);
                return mul(m, original) + center;
            }

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes IN)
            {
                float3 posOS = IN.positionOS.xyz;
                float3 objOriginWS = TransformObjectToWorld(float3(0,0,0));
                float2 wsUV = objOriginWS.xz;

                float2 animTiling = _NoiseTextureTilling.zw;
                float2 panner     = 0.1 * _Time.y * _NoisePannerSpeed;
                float  animNoise  = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture,
                                       wsUV * animTiling + panner, 0).r;

                float windDirRad  = radians((_MBWindDir +
                    _MBWindDirOffset * (-1.0 + animNoise * 2.0)) * -1.0);
                float3 windDirWS  = float3(cos(windDirRad), 0.0, sin(windDirRad));
                float3 windDirOS  = normalize(
                    TransformWorldToObject(windDirWS) - TransformWorldToObject(float3(0,0,0)));

                float2 staticTiling = _NoiseTextureTilling.xy;
                float  staticNoise  = SAMPLE_TEXTURE2D_LOD(_NoiseTexture, sampler_NoiseTexture,
                                        wsUV * staticTiling, 0).r;

                float amp  = _MBAmplitude + _MBAmplitudeOffset * staticNoise;
                float freq = _MBFrequency + _MBFrequencyOffset * staticNoise;
                float sineInput = ((objOriginWS.x + objOriginWS.z) + (_Time.y * freq)) * _MBPhase;
                float angle = radians((amp * sin(sineInput) + _MBDefaultBending)
                              * (posOS.y / max(_MBMaxHeight, 0.001)));
                float mask = step(0.01, posOS.y);

                float3 pivot    = float3(0.0, posOS.y, 0.0);
                float3 rot1     = RotateAroundAxis(pivot, posOS, windDirOS, angle);
                float3 rot2     = RotateAroundAxis(float3(0,0,0), rot1, windDirOS, angle);
                posOS += (rot2 - posOS) * mask;

                Varyings OUT;
                float3 posWS  = TransformObjectToWorld(posOS);
                float3 nrmWS  = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nrmWS, _MainLightPosition.xyz));
                return OUT;
            }

            half4 ShadowFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        // Depth-only pass (for depth prepass / SSAO etc.)
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Metallic; float _Smoothness;
                float _MBDefaultBending; float _MBAmplitude; float _MBAmplitudeOffset;
                float _MBFrequency; float _MBFrequencyOffset; float _MBPhase;
                float _MBWindDir; float _MBWindDirOffset; float _MBMaxHeight;
                float4 _NoiseTextureTilling; float2 _NoisePannerSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings   { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
            half4 DepthFrag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }
    }
}