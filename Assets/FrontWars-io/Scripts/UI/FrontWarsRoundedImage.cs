using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 모서리를 둥글린다 — 원본 창 · 카드 · 버튼은 모두 둥근 모서리다 `[실측] probe/menu_modals.json border-radius 16px · 12px · 8px`.
    /// <para>둥근 사각형 그림을 코드로 만들어 9조각(sliced)으로 늘린다. 반지름마다 한 장만 만들어 나눠 쓴다.</para>
    /// <para>⚠ 그림을 «구울 때» 만들어 넣으면 프리팹에 안 남는다 (런타임에 만든 것은 저장이 안 된다). 그래서 컴포넌트가 «켜질 때» 넣는다.</para>
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class FrontWarsRoundedImage : MonoBehaviour
    {
        /// <summary>그림 한 변 = 반지름×2 + 가운데 2픽셀. 반지름이 클수록 그림이 커진다.</summary>
        private const int CenterPixels = 2;

        /// <summary>
        /// 그림 PPU — 캔버스 referencePixelsPerUnit(100)과 같아야 한다.
        /// <para>⚠ 1 로 두면 9조각 경계가 100 배로 잡혀 모서리가 판 높이 절반까지 부풀고(알약 모양), 배치 계산의 «선호 높이» 도 2400 처럼 튄다.</para>
        /// </summary>
        private const float CanvasPixelsPerUnit = 100f;

        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();

        [SerializeField] private float _radius = 12f;

        public float Radius
        {
            get { return _radius; }
            set
            {
                _radius = value;
                Apply();
            }
        }

        private void Awake() => Apply();

        private void OnEnable() => Apply();

        private void Apply()
        {
            var image = GetComponent<Image>();

            if (image == null)
                return;

            image.sprite = Get(Mathf.RoundToInt(_radius));
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }

        /// <summary>반지름만큼 둥근 사각형 그림 — 가장자리는 부드럽게(안티에일리어싱) 깎는다.</summary>
        public static Sprite Get(int radius)
        {
            radius = Mathf.Clamp(radius, 1, 64);

            if (Cache.TryGetValue(radius, out Sprite cached) && cached != null)
                return cached;

            int size = radius * 2 + CenterPixels;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = $"FrontWarsRounded{radius}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 네 모서리의 원 중심까지 거리로 안팎을 가른다 (가운데 2픽셀 띠는 언제나 안쪽이다)
                    float dx = x < radius ? radius - 0.5f - x : (x >= radius + CenterPixels ? x - (radius + CenterPixels) + 0.5f : 0f);
                    float dy = y < radius ? radius - 0.5f - y : (y >= radius + CenterPixels ? y - (radius + CenterPixels) + 0.5f : 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), CanvasPixelsPerUnit, 0u,
                                          SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            sprite.name = $"FrontWarsRounded{radius}";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[radius] = sprite;
            return sprite;
        }
    }
}
