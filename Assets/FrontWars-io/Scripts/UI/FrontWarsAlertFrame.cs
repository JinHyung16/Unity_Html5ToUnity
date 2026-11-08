using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 경고 테두리 — 공격받는 중이거나 방금 배신당했을 때 화면 네 변 안쪽으로 붉은 빛이 번진다 `[소스] BMmXchgl.js:tn=class e{static{this.BETRAY_HOLD_MS=5e3}`.
    /// <list type="bullet">
    /// <item>두께 max(36, 화면 짧은 변 × 0.11) · 변마다 가장자리 불투명 → 안쪽 투명 선형 그라데이션 · 모서리는 겹친다.</item>
    /// <item>세기는 목표로 min(1, dt/150ms) 만큼 다가가고 0.004 미만이면 0. 깜빡임 0.72 + 0.28·sin(초·π·1.25).</item>
    /// <item>색은 공격 rgb(225,55,45) · 배신 rgb(205,45,140) 을 두 세기의 비율로 섞는다. 투명도 min(0.85, 세기·깜빡임·0.9), 0.003 이하면 안 그린다.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsAlertFrame : MonoBehaviour
    {
        public const float BetrayHoldSeconds = 5f;

        private const float MinThickness = 36f;
        private const float ThicknessRatio = 0.11f;
        private const float ApproachMs = 150f;
        private const float SnapToZero = 0.004f;
        private const float HideAlpha = 0.003f;
        private const float MaxAlpha = 0.85f;

        private static readonly Color32 AttackColor = new Color32(225, 55, 45, 255);
        private static readonly Color32 BetrayColor = new Color32(205, 45, 140, 255);

        /// <summary>위 · 아래 · 왼쪽 · 오른쪽 띠.</summary>
        [SerializeField] private Image[] _edges;

        private float _attackTarget;
        private float _betrayTarget;
        private float _attack;
        private float _betray;
        private float _pulse;

        private static Sprite _vertical;
        private static Sprite _horizontal;

        /// <summary>지금 보이는 투명도 (0 = 안 그림).</summary>
        public float Alpha { get; private set; }

        public void SetTargets(bool attack, bool betray)
        {
            _attackTarget = attack ? 1f : 0f;
            _betrayTarget = betray ? 1f : 0f;
        }

        public void ResetState()
        {
            _attackTarget = _betrayTarget = _attack = _betray = _pulse = 0f;
            Alpha = 0f;
            Show(false);
        }

        private void Awake()
        {
            _vertical ??= Gradient(true);
            _horizontal ??= Gradient(false);

            // 위 · 아래는 세로로 번지고, 아래 · 오른쪽은 그림을 뒤집는다
            for (int i = 0; i < _edges.Length; i++)
            {
                _edges[i].sprite = i < 2 ? _vertical : _horizontal;
                _edges[i].raycastTarget = false;
                _edges[i].rectTransform.localScale = new Vector3(i == 3 ? -1f : 1f, i == 1 ? -1f : 1f, 1f);
            }
        }

        private void Update()
        {
            float dtMs = Time.unscaledDeltaTime * 1000f;
            float k = Mathf.Min(1f, dtMs / ApproachMs);
            _attack += (_attackTarget - _attack) * k;
            _betray += (_betrayTarget - _betray) * k;

            if (_attack < SnapToZero)
                _attack = 0f;

            if (_betray < SnapToZero)
                _betray = 0f;

            float strength = Mathf.Max(_attack, _betray);

            if (strength <= 0f)
            {
                Alpha = 0f;
                Show(false);
                return;
            }

            _pulse += dtMs / 1000f;
            float flicker = 0.72f + 0.28f * Mathf.Sin(_pulse * Mathf.PI * 1.25f);
            float sum = _attack + _betray;
            float mix = sum > 0f ? _betray / sum : 0f;
            Color color = Color.Lerp(AttackColor, BetrayColor, mix);
            Alpha = Mathf.Min(MaxAlpha, strength * flicker * 0.9f);

            if (Alpha <= HideAlpha)
            {
                Show(false);
                return;
            }

            color.a = Alpha;
            Vector2 size = ((RectTransform)transform).rect.size;
            float thickness = Mathf.Max(MinThickness, Mathf.Min(size.x, size.y) * ThicknessRatio);
            Show(true);

            for (int i = 0; i < _edges.Length; i++)
            {
                _edges[i].color = color;
                RectTransform rect = _edges[i].rectTransform;
                rect.sizeDelta = i < 2 ? new Vector2(0f, thickness) : new Vector2(thickness, 0f);

                // 피벗이 가운데라 뒤집어도 제자리 — 변에서 두께 절반만큼 안으로 둔다
                rect.anchoredPosition = i switch
                {
                    0 => new Vector2(0f, -thickness * 0.5f),
                    1 => new Vector2(0f, thickness * 0.5f),
                    2 => new Vector2(thickness * 0.5f, 0f),
                    _ => new Vector2(-thickness * 0.5f, 0f)
                };
            }
        }

        private void Show(bool on)
        {
            foreach (Image edge in _edges)
            {
                if (edge.enabled != on)
                    edge.enabled = on;
            }
        }

        /// <summary>한쪽 끝 불투명 → 반대쪽 투명. 세로면 위가 불투명, 가로면 왼쪽이 불투명.</summary>
        private static Sprite Gradient(bool vertical)
        {
            const int length = 64;
            int w = vertical ? 1 : length;
            int h = vertical ? length : 1;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = vertical ? "FrontWarsAlertVertical" : "FrontWarsAlertHorizontal",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new List<Color32>(length);

            for (int i = 0; i < length; i++)
            {
                // 텍스처 y 는 아래가 0 — 세로는 위(끝)가 불투명
                float t = vertical ? 1f - i / (length - 1f) : i / (length - 1f);
                pixels.Add(new Color32(255, 255, 255, (byte)Mathf.RoundToInt((1f - t) * 255f)));
            }

            texture.SetPixels32(pixels.ToArray());
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
