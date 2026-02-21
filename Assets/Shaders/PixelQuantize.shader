Shader "PostProcess/PixelQuantize"
{
    Properties
    {
        [HideInInspector] _BlitTexture  ("", 2D)     = "white" {}
        [HideInInspector] _BlitScaleBias("", Vector) = (1,1,0,0)

        _PixelSize   ("Pixel Size",        Int)         = 4
        _Intensity   ("Intensity",         Range(0,1))  = 1.0

        [Space(10)]
        [Toggle(_USE_PALETTE)] _UsePalette ("Use Custom Palette", Int) = 0
        _PaletteTex  ("Palette Texture",   2D)          = "white" {}
        _PaletteCount("Palette Color Count", Int)        = 8

        [Space(5)]
        // Fallback LAB quantisation used when palette is off
        _LabSteps    ("LAB Steps (no palette)", Int)    = 8
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "PixelQuantize"
            HLSLPROGRAM
            #pragma vertex   PQ_Vert
            #pragma fragment PQ_Frag
            #pragma target   3.0
            #pragma shader_feature_local _USE_PALETTE

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"

            TEXTURE2D(_BlitTexture);
            TEXTURE2D(_PaletteTex);
            SamplerState pq_linear_clamp_sampler;
            SamplerState pq_point_clamp_sampler;

            float4 _BlitTexture_TexelSize;
            float4 _BlitScaleBias;

            float _Intensity;
                            float _PixelSize;
            int   _LabSteps;
            int   _PaletteCount;

            struct PQ_Attr { uint id : SV_VertexID; };
            struct PQ_Vary { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            PQ_Vary PQ_Vert(PQ_Attr IN)
            {
                PQ_Vary OUT;
                float2 uv = float2((IN.id << 1) & 2, IN.id & 2);
                OUT.pos   = float4(uv * 2.0 - 1.0, 0.0, 1.0);
                OUT.uv    = uv * _BlitScaleBias.xy + _BlitScaleBias.zw;
                #if UNITY_UV_STARTS_AT_TOP
                    OUT.uv.y = 1.0 - OUT.uv.y;
                #endif
                return OUT;
            }

            // ------------------------------------------------------------------
            // Colour space helpers
            // ------------------------------------------------------------------
            float3 PQ_ToSRGB  (float3 c){ return pow(max(c,0.0001), 1.0/2.2); }
            float3 PQ_ToLinear(float3 c){ return pow(max(c,0.0001), 2.2);      }

            float3 PQ_RGBToXYZ(float3 rgb)
            {
                float3 c = PQ_ToLinear(rgb);
                return float3(
                    dot(c, float3(0.4124564,0.3575761,0.1804375)),
                    dot(c, float3(0.2126729,0.7151522,0.0721750)),
                    dot(c, float3(0.0193339,0.1191920,0.9503041)));
            }

            float PQ_LabF(float t)
            {
                return (t > 0.008856) ? pow(t,1.0/3.0) : 7.787*t+16.0/116.0;
            }
            float3 PQ_XYZToLab(float3 xyz)
            {
                float fx = PQ_LabF(xyz.x/0.95047);
                float fy = PQ_LabF(xyz.y/1.00000);
                float fz = PQ_LabF(xyz.z/1.08883);
                return float3(116.0*fy-16.0, 500.0*(fx-fy), 200.0*(fy-fz));
            }

            float PQ_LabFInv(float t)
            {
                return (t > 0.20690) ? t*t*t : (t-16.0/116.0)/7.787;
            }
            float3 PQ_LabToXYZ(float3 lab)
            {
                float fy = (lab.x+16.0)/116.0;
                return float3(
                    PQ_LabFInv(lab.y/500.0+fy)*0.95047,
                    PQ_LabFInv(fy)            *1.00000,
                    PQ_LabFInv(fy-lab.z/200.0)*1.08883);
            }

            float3 PQ_XYZToRGB(float3 xyz)
            {
                return clamp(float3(
                    dot(xyz, float3( 3.2404542,-1.5371385,-0.4985314)),
                    dot(xyz, float3(-0.9692660, 1.8760108, 0.0415560)),
                    dot(xyz, float3( 0.0556434,-0.2040259, 1.0572252))),
                    0.0, 1.0);
            }

            float PQ_Quant(float v, float lo, float hi, int n)
            {
                float t = clamp((v-lo)/(hi-lo), 0.0, 1.0);
                return floor(t*float(n-1)+0.5)/float(n-1)*(hi-lo)+lo;
            }

            // ------------------------------------------------------------------
            // LAB distance between two linear-sRGB colours
            // ------------------------------------------------------------------
            float PQ_LabDist(float3 a, float3 b)
            {
                float3 diff = PQ_XYZToLab(PQ_RGBToXYZ(a))
                            - PQ_XYZToLab(PQ_RGBToXYZ(b));
                return dot(diff, diff); // squared distance is fine for comparison
            }

            // ------------------------------------------------------------------
            // Snap linear colour to nearest palette entry (sampled in sRGB)
            // Palette texture: horizontal strip, any height, point-sampled
            // ------------------------------------------------------------------
            float3 PQ_NearestPalette(float3 linearCol)
            {
                int   count   = max(1, _PaletteCount);
                float best    = 1e9;
                float3 result = linearCol;

                for (int i = 0; i < count; i++)
                {
                    // Sample palette at centre of each texel
                    float  u       = (float(i) + 0.5) / float(count);
                    float3 entry   = SAMPLE_TEXTURE2D(_PaletteTex,
                                        pq_point_clamp_sampler,
                                        float2(u, 0.5)).rgb;
                    // Palette is typically sRGB — convert to linear for distance
                    float3 entryLin = PQ_ToLinear(entry);
                    float  d        = PQ_LabDist(linearCol, entryLin);
                    if (d < best)
                    {
                        best   = d;
                        result = entryLin;
                    }
                }
                return result;
            }

            // ------------------------------------------------------------------
            // Fragment
            // ------------------------------------------------------------------
            float4 PQ_Frag(PQ_Vary IN) : SV_Target
            {
                float2 uv  = IN.uv;
                float2 res = _BlitTexture_TexelSize.zw;

                // 1. Point filter / pixelation
                int    ps      = max(1, _PixelSize);
                float2 snapped = (floor(uv*res/float(ps))*float(ps)+float(ps)*0.5)/res;
                float4 col     = SAMPLE_TEXTURE2D(_BlitTexture, pq_linear_clamp_sampler, snapped);

                float3 result;

                #if _USE_PALETTE
                    // 2a. Snap to nearest palette colour in LAB space
                    result = PQ_NearestPalette(col.rgb);
                #else
                    // 2b. Uniform LAB quantisation fallback
                    float3 srgb = PQ_ToSRGB(col.rgb);
                    float3 lab  = PQ_XYZToLab(PQ_RGBToXYZ(srgb));
                    int s = max(2, _LabSteps);
                    lab.x = PQ_Quant(lab.x,  0.0,   100.0, s);
                    lab.y = PQ_Quant(lab.y, -128.0, 127.0, s);
                    lab.z = PQ_Quant(lab.z, -128.0, 127.0, s);
                    result = PQ_ToLinear(PQ_XYZToRGB(PQ_LabToXYZ(lab)));
                #endif

                // 3. Blend
                return float4(lerp(col.rgb, result, _Intensity), col.a);
            }
            ENDHLSL
        }
    }
}