using System.Collections.Generic;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 원본이 <b><c>Graphics</c> 로 그 자리에서 그리는 도형</b>을 스프라이트로 굽는다 —
    /// 포털 테두리 원 · 조각 후광 · 마법사 앞 조각 자리.
    ///
    /// <para>
    /// ★★ <b>에셋으로 만들지 않는다.</b> 원본에도 그런 «컷»이 없다 — 아트 표에 올리면
    /// 「측정한 원본 컷」이 아닌 것이 표에 섞인다. 그래서 <b>런타임에 굽고 반지름으로 캐시</b>한다.
    /// </para>
    ///
    /// <para>
    /// ★★ <b>선 굵기는 호출부에 적힌 <c>width</c> 가 «아니다».</b> 원본 도형에는 전부
    /// <c>pixelLine: true</c> 가 걸려 있고, 그러면 렌더러는 그 도형을 <b>GL 라인 목록</b>으로 넘긴다 —
    /// 그리는 굵기는 언제나 <b>기기 픽셀 1</b> 이고 <c>width</c> 는 <b>버려진다</b>
    /// [번들 직독 <c>s.pixelLine ? (라인 목록) : (삼각형 띠)</c>]. <c>width: 5</c> 를 그대로 옮기면
    /// <b>원본의 9배 굵은 띠</b>가 나온다 (재발방지 #179).
    /// </para>
    ///
    /// <para>
    /// ⚠ 기기 픽셀 1 은 «원본 world px 로 몇»인가 — 원본 루트 배율이 <c>화면세로 / 580</c> 이므로
    /// 기준 1080p 에서 <b>580 / 1080 = 0.537 world px</b> 다. 화면이 커지면 원본은 계속 1픽셀이지만
    /// 우리 스프라이트는 같이 굵어진다 — <b>기준 해상도에서 맞춘 «의도된 차이»</b>다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>채움과 테두리는 색도 알파도 다르다</b> — 한 장에 같이 구우면 따로 물들일 수 없다.
    /// 그래서 <see cref="Disc"/> 와 <see cref="Ring"/> 두 장이고, 쓰는 쪽이 겹쳐 놓는다.
    /// </para>
    /// </summary>
    public static class UndeadShapes
    {
        /// <summary>기준 화면 세로 — 기기 픽셀 하나가 world px 로 몇인지 정하는 값이다.</summary>
        public const float ReferenceScreenHeight = 1080f;

        /// <summary>한 world px 를 <b>몇 텍셀</b>로 굽나 — 선이 반 픽셀보다 가늘어 결이 필요하다.</summary>
        private const int TexelsPerWorldPixel = 2;

        /// <summary>반지름 밖 여백 (world px) — 선이 가장자리에 잘리지 않게 둔다.</summary>
        public const float Margin = 2f;

        private static readonly Dictionary<float, Sprite> Rings = new Dictionary<float, Sprite>(4);
        private static readonly Dictionary<float, Sprite> Discs = new Dictionary<float, Sprite>(4);

        /// <summary>기기 픽셀 1 이 원본 world px 로 몇인가 [= 580 / 1080].</summary>
        public static float StrokeWorldPixels
        {
            get { return UndeadUnits.DesignViewportHeight / ReferenceScreenHeight; }
        }

        /// <summary>
        /// <b>테두리 원</b> — 배율 1 이면 반지름이 정확히 <paramref name="radius"/> world px 다.
        /// </summary>
        public static Sprite Ring(float radius)
        {
            if (Rings.TryGetValue(radius, out Sprite cached) && cached != null)
                return cached;

            Sprite baked = Bake(radius, true);
            Rings[radius] = baked;
            return baked;
        }

        /// <summary><b>채운 원</b> — 테두리와 <b>같은 크기·같은 중심</b>이라 그대로 겹쳐 놓으면 된다.</summary>
        public static Sprite Disc(float radius)
        {
            if (Discs.TryGetValue(radius, out Sprite cached) && cached != null)
                return cached;

            Sprite baked = Bake(radius, false);
            Discs[radius] = baked;
            return baked;
        }

        private static Sprite Bake(float radius, bool stroke)
        {
            float half = radius + Margin;
            int size = Mathf.CeilToInt(half * 2f * TexelsPerWorldPixel);
            float texelWorld = 1f / TexelsPerWorldPixel;
            float halfStroke = StrokeWorldPixels * 0.5f;

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = stroke ? $"UndeadRing{radius}" : $"UndeadDisc{radius}",
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            float center = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 텍셀 «가운데»까지의 거리를 world px 로 잰다
                    float dx = (x + 0.5f - center) * texelWorld;
                    float dy = (y + 0.5f - center) * texelWorld;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);

                    // 안쪽이면 음수 — 텍셀 하나 폭으로 나눠 «덮은 비율»로 바꾼다 (계단이 안 진다)
                    float signed = stroke ? Mathf.Abs(distance - radius) - halfStroke : distance - radius;
                    float coverage = Mathf.Clamp01(0.5f - signed / texelWorld);

                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            // PPU 를 «텍셀 결»만큼 올려야 배율 1 에서 반지름이 원본 값 그대로다
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                                 UndeadUnits.WorldPixelsPerUnit * TexelsPerWorldPixel);
        }
    }
}
