// FrontWars 지형 + 영토 한 장 그리기 (URP Unlit, HLSL).
//
// 원본은 WebGL2 전체 화면 삼각형 하나로 칸마다 지형 텍스처 · 칸 값 텍스처(R16UI) · 팔레트(8×2048)를 읽어 섞는다
// [소스] BMmXchgl.js:uniform highp usampler2D uTiles; · vec4 overlayAt(ivec2 tc) · vec4 compositeAt(ivec2 tc)
// 규칙을 읽고 HLSL 로 다시 짰다 (R11). 여기서는 지도 크기 사각형 하나에 그린다 (칸 1개 = 1 월드 단위, 칸 (0,0) = 왼쪽 위).
//
// 색공간: 원본은 sRGB 바이트 그대로 섞는다. 텍스처는 모두 linear(무변환)로 만들어 바이트/255 를 읽고,
// 섞은 뒤 Linear 프로젝트면 SRGBToLinear 로 바꿔 내보낸다 → 화면에 원본 바이트가 그대로 찍힌다.
Shader "JinHyung/FrontWars/Territory"
{
    Properties
    {
        [NoScaleOffset] _TerrainTex ("Terrain RGBA8 (linear)", 2D) = "black" {}
        [NoScaleOffset] _OwnerTex ("Tile code RG8 = low/high byte (linear)", 2D) = "black" {}
        [NoScaleOffset] _PaletteTex ("Palette 8x2048 RGBA8 (linear)", 2D) = "black" {}
        _MapSize ("Map size in tiles", Vector) = (2000, 1500, 0, 0)
        // Color 가 아니라 Vector — Linear 프로젝트에서 Color 속성은 자동 변환되어 바이트가 달라진다
        _CanvasBg ("Canvas background (sRGB byte / 255)", Vector) = (0.105882, 0.137255, 0.184314, 1)
        _Tactical ("Tactical view (0/1)", Float) = 0
        _Smooth ("Bilinear composite when zoom < 1 (0/1)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            // LightMode 없음 = SRPDefaultUnlit — Universal · 2D 렌더러 둘 다 그린다
            Name "FrontWarsTerritory"
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_TerrainTex);
            TEXTURE2D(_OwnerTex);
            TEXTURE2D(_PaletteTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MapSize;
                float4 _CanvasBg;
                float _Tactical;
                float _Smooth;
            CBUFFER_END

            // 칸 값 비트 [소스] BMmXchgl.js:po=2047,mo=2048,ho=4096,go=8192
            #define ID_MASK      2047u
            #define BORDER_BIT   2048u
            #define DEFENDED_BIT 4096u
            #define ALT_BIT      8192u

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 world : TEXCOORD0; // 칸 좌표 (x 오른쪽 · y 아래쪽)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                // 사각형은 로컬 x 0..W, y 0..-H — 칸 y 는 아래로 자란다
                output.world = float2(input.positionOS.x, -input.positionOS.y);
                return output;
            }

            uint TileCode(int2 tc)
            {
                float4 o = LOAD_TEXTURE2D(_OwnerTex, tc);
                uint lo = (uint)(o.r * 255.0 + 0.5);
                uint hi = (uint)(o.g * 255.0 + 0.5);
                return lo | (hi << 8);
            }

            float4 Palette(int slot, int row)
            {
                return LOAD_TEXTURE2D(_PaletteTex, int2(slot, row));
            }

            // [소스] BMmXchgl.js:vec4 overlayAt(ivec2 tc) {
            float4 OverlayAt(int2 tc)
            {
                uint v = TileCode(tc);
                uint id = v & ID_MASK;
                int row = (int)id;
                bool border = (v & BORDER_BIT) != 0u;

                if (_Tactical > 0.5)
                {
                    if (!border || id == 0u)
                        return float4(0, 0, 0, 0);

                    return Palette(5, row);
                }

                if (id == 0u)
                {
                    if ((v & ALT_BIT) != 0u)
                    {
                        // 낙진 점무늬 — 칸 좌표 해시로 0행 1 · 4~7 칸 중 하나 [소스] Fallout speckle … falloutPaletteIndex()
                        uint h = ((uint)tc.x * 0x9e3779b1u) ^ ((uint)tc.y * 0x85ebca6bu);
                        h ^= h >> 13u;
                        h *= 0xc2b2ae35u;
                        h ^= h >> 16u;
                        uint shade = h % 5u;
                        int slot = shade == 0u ? 1 : (int)shade + 3;
                        return Palette(slot, 0);
                    }

                    return float4(0, 0, 0, 0);
                }

                if (border)
                {
                    uint atk = (v >> 14) & 3u;

                    if (atk == 1u)
                        return Palette(2, 0);

                    if (atk == 2u)
                        return Palette(3, 0);

                    if ((v & DEFENDED_BIT) != 0u)
                    {
                        // 방어초소 국경 체크 무늬 [소스] bool light = ((tc.x ^ tc.y) & 1) == 0;
                        bool lightCell = ((tc.x ^ tc.y) & 1) == 0;
                        return Palette(lightCell ? 3 : 4, row);
                    }

                    return Palette(2, row);
                }

                bool alt = (v & ALT_BIT) != 0u;
                return Palette(alt ? 1 : 0, row);
            }

            // [소스] BMmXchgl.js:vec4 compositeAt(ivec2 tc) {
            float4 CompositeAt(int2 tc)
            {
                if (tc.x < 0 || tc.y < 0 || tc.x >= (int)_MapSize.x || tc.y >= (int)_MapSize.y)
                    return float4(_CanvasBg.rgb, 1.0);

                float4 terr = LOAD_TEXTURE2D(_TerrainTex, tc);
                float4 ov = OverlayAt(tc);
                return float4(lerp(terr.rgb, ov.rgb, ov.a), ov.a + terr.a * (1.0 - ov.a));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 world = input.world;
                float4 c;

                if (_Smooth > 0.5)
                {
                    // 줌 1 미만일 때 네 칸 합성 결과를 쌍선형으로 [소스] if (uSmooth == 1) { vec2 wf = world - 0.5;
                    float2 wf = world - 0.5;
                    float2 baseCell = floor(wf);
                    float2 f = wf - baseCell;
                    int2 b = (int2)baseCell;
                    float4 c00 = CompositeAt(b);
                    float4 c10 = CompositeAt(b + int2(1, 0));
                    float4 c01 = CompositeAt(b + int2(0, 1));
                    float4 c11 = CompositeAt(b + int2(1, 1));
                    c = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
                }
                else
                {
                    c = CompositeAt((int2)floor(world));
                }

                // 배경 위에 합성 [소스] outColor = vec4(mix(uBg, c.rgb, c.a), 1.0);
                float3 rgb = lerp(_CanvasBg.rgb, c.rgb, c.a);

                #if !defined(UNITY_COLORSPACE_GAMMA)
                    rgb = SRGBToLinear(rgb);
                #endif

                return half4(rgb, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
