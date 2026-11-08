using System;
using System.Collections.Generic;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.FrontWarsIo
{
    /// <summary>정보 판 버튼 · 외교 동작 — 원형 메뉴 밖에서도 같은 동작을 쓴다.</summary>
    public enum EFrontWarsRadialAction
    {
        Target,
        QuickChat,
        Embargo,
        StopEmbargo
    }

    /// <summary>
    /// 원형 메뉴 조각 하나 `[소스] QPQdQ6Sy.js:oa(){ … g({id,name,icon,color,disabled,cooldown,counter,subMenu,onClick,tooltip}) }`.
    /// </summary>
    public sealed class FrontWarsRadialItem
    {
        public string Id;
        public Color Color;

        /// <summary>그림 표식 (null 이면 <see cref="Glyph"/> 글자를 쓴다).</summary>
        public Sprite Icon;
        public string Glyph;

        public bool Disabled;

        /// <summary>남은 재사용 대기 비율 0~1 — 조각 위에 어두운 부채꼴로 덮는다.</summary>
        public float Cooldown;

        /// <summary>아이콘 아래 숫자 (배 「남은 수/최대」) — null 이면 없음.</summary>
        public string Counter;

        public bool HasSubMenu;

        public string Title;
        public string Description;
        public string Cost;
        public string CounterLine;
        public string CooldownLine;
    }

    /// <summary>
    /// 오른쪽 클릭 원형 메뉴 `[소스] QPQdQ6Sy.js:function ua(e){ · la=Y.memo(… · Q={colors,dims}` · `[실측] probe/radial_probe.json`.
    /// <list type="bullet">
    /// <item>SVG 한 변 290(하위 메뉴가 열리면 400)을 0.6 배로 그린다. 가운데 원 반지름 55, 첫 겹 고리 68~143, 둘째 겹 90~175.</item>
    /// <item>지금 겹의 조각만 그린다. 조각 i 는 [i·d + f + p/2, (i+1)·d + f − p/2] (d = 2π/n · f = −π/2 − d/2 · 틈 p = 0.05 라디안) — 첫 조각이 위 가운데.</item>
    /// <item>가운데 원: 하위 메뉴면 뒤로, 내 땅이면 방어 초소 짓기, 남의 땅이면 공격.</item>
    /// <item>조각에 올리면 커서 오른쪽 아래(+15)에 툴팁 — 이름 · 설명 · 값 · 배 수 · 대기 · 「사용 불가」.</item>
    /// </list>
    /// </summary>
    public sealed class FrontWarsRadialMenuWindow : BaseWindow
    {
        public static readonly WindowKey<FrontWarsRadialMenuWindow> Key = new WindowKey<FrontWarsRadialMenuWindow>("UI/Window/FrontWarsRadialMenuWindow");

        /// <summary>원본 SVG 배율 [실측 scale(0.6)].</summary>
        public const float SvgScale = 0.6f;

        public const float CenterRadius = 55f * SvgScale;
        public const float InnerRadius = 68f * SvgScale;
        public const float OuterRadius = 143f * SvgScale;
        public const float InnerRadiusSub = 90f * SvgScale;
        public const float OuterRadiusSub = 175f * SvgScale;

        /// <summary>조각 사이 틈 (라디안) `[소스] p=.05`.</summary>
        public const float GapRadians = 0.05f;

        public const float IconSize = 44f * SvgScale;
        public const float CenterIconSize = 64f * SvgScale;
        public const int MaxSlices = 8;

        /// <summary>SVG 한 변 — 첫 겹 290 · 하위 메뉴 400 `[소스] dims.baseSize · expandedSize`.</summary>
        public const float BaseSize = 290f * SvgScale;
        public const float ExpandedSize = 400f * SvgScale;

        /// <summary>툴팁 — 커서에서 오른쪽 아래로 [소스 left:x+15, top:y+15].</summary>
        public static readonly Vector2 TooltipOffset = new Vector2(15f, 15f);

        private const int RingTextureSize = 256;

        /// <summary>조각 색 `[소스] Q.colors`.</summary>
        public static readonly Color InfoColor = new Color(145f / 255f, 153f / 255f, 161f / 255f, 0.95f);
        public static readonly Color BuildColor = new Color(234f / 255f, 179f / 255f, 8f / 255f, 0.8f);
        public static readonly Color BuildingColor = new Color(17f / 255f, 24f / 255f, 39f / 255f, 0.95f);
        public static readonly Color BoatColor = new Color(59f / 255f, 130f / 255f, 246f / 255f, 0.8f);
        public static readonly Color AllyColor = new Color(34f / 255f, 197f / 255f, 94f / 255f, 0.8f);
        public static readonly Color RedColor = new Color(239f / 255f, 68f / 255f, 68f / 255f, 0.8f);
        public static readonly Color DisabledColor = new Color(75f / 255f, 85f / 255f, 99f / 255f, 0.6f);
        private static readonly Color CooldownShade = new Color(0f, 0f, 0f, 0.55f);

        [SerializeField] private RectTransform _center;
        [SerializeField] private Image _centerIcon;
        [SerializeField] private Image[] _slices;
        [SerializeField] private Image[] _cooldowns;
        [SerializeField] private Image[] _sliceIcons;
        [SerializeField] private TMP_Text[] _sliceGlyphs;
        [SerializeField] private TMP_Text[] _sliceCounters;
        [SerializeField] private RectTransform _tooltip;
        [SerializeField] private TMP_Text _tooltipTitle;
        [SerializeField] private TMP_Text _tooltipBody;
        [SerializeField] private FrontWarsRadialHitArea _hitArea;
        [SerializeField] private Button _dismiss;

        private readonly List<FrontWarsRadialItem> _items = new List<FrontWarsRadialItem>();
        private int _level;
        private string _unavailable = string.Empty;
        private static Sprite _ringTop;
        private static Sprite _ringSub;

        public event Action<string> OnItem;
        public event Action OnCenter;
        public event Action OnDismiss;

        public int Level => _level;
        public IReadOnlyList<FrontWarsRadialItem> Items => _items;

        /// <summary>화면 좌표(왼쪽 위 기준 px)에 메뉴 가운데를 둔다.</summary>
        public void Place(Vector2 screenTopLeft)
        {
            var canvas = GetComponentInParent<Canvas>();
            var root = (RectTransform)transform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(screenTopLeft.x, Screen.height - screenTopLeft.y),
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null, out Vector2 local);
            _center.anchoredPosition = local;
        }

        /// <summary>한 겹을 편다 — 0 = 첫 겹, 1 = 하위 메뉴.</summary>
        public void Show(IReadOnlyList<FrontWarsRadialItem> items, int level, Sprite centerIcon, string unavailable)
        {
            FrontWarsFonts.ApplyTo(gameObject);
            _items.Clear();
            _level = level;
            _unavailable = unavailable ?? string.Empty;
            _centerIcon.sprite = centerIcon;
            _centerIcon.enabled = centerIcon != null;

            int count = Mathf.Min(items.Count, _slices.Length);
            float inner = level == 0 ? InnerRadius : InnerRadiusSub;
            float outer = level == 0 ? OuterRadius : OuterRadiusSub;
            float span = count > 0 ? 360f / count : 360f;
            float gap = GapRadians * Mathf.Rad2Deg;

            for (int i = 0; i < _slices.Length; i++)
            {
                bool used = i < count;
                _slices[i].gameObject.SetActive(used);
                _sliceIcons[i].gameObject.SetActive(false);
                _sliceGlyphs[i].gameObject.SetActive(false);
                _sliceCounters[i].gameObject.SetActive(false);

                if (used == false)
                    continue;

                FrontWarsRadialItem item = items[i];
                _items.Add(item);

                Image slice = _slices[i];
                slice.sprite = level == 0 ? RingTop() : RingSub();
                slice.rectTransform.sizeDelta = Vector2.one * outer * 2f;
                PrepareFill(slice, span, gap, i);
                slice.color = item.Disabled ? DisabledColor : item.Color;

                Image shade = _cooldowns[i];
                shade.sprite = slice.sprite;
                shade.rectTransform.sizeDelta = slice.rectTransform.sizeDelta;
                PrepareFill(shade, span, gap, i);
                shade.fillAmount = Mathf.Max(0f, span - gap) / 360f * Mathf.Clamp01(item.Cooldown);
                shade.color = CooldownShade;
                shade.gameObject.SetActive(item.Cooldown > 0f);

                // 아이콘은 고리 한가운데 (안쪽 + 폭/2). 숫자가 있으면 6 올린다 `[소스] w=C?-6:0`
                float theta = i * span * Mathf.Deg2Rad;
                float radius = (inner + outer) * 0.5f;
                Vector2 at = new Vector2(Mathf.Sin(theta), Mathf.Cos(theta)) * radius;
                float lift = item.Counter != null ? 6f * SvgScale : 0f;
                float alpha = item.Disabled ? 0.6f : 1f;

                if (item.Icon != null)
                {
                    Image icon = _sliceIcons[i];
                    icon.gameObject.SetActive(true);
                    icon.sprite = item.Icon;
                    icon.rectTransform.anchoredPosition = at + new Vector2(0f, lift);
                    icon.rectTransform.sizeDelta = Vector2.one * IconSize;
                    icon.color = new Color(1f, 1f, 1f, alpha);
                }
                else if (string.IsNullOrEmpty(item.Glyph) == false)
                {
                    TMP_Text glyph = _sliceGlyphs[i];
                    glyph.gameObject.SetActive(true);
                    glyph.text = item.Glyph;
                    glyph.rectTransform.anchoredPosition = at + new Vector2(0f, lift);
                    glyph.alpha = alpha;
                }

                if (item.Counter != null)
                {
                    // 아이콘 아래 숫자 [소스 y: S.y + iconSize/2 + 4 · 16px 700]
                    TMP_Text counter = _sliceCounters[i];
                    counter.gameObject.SetActive(true);
                    counter.text = item.Counter;
                    counter.rectTransform.anchoredPosition = at - new Vector2(0f, (IconSize * 0.5f + 4f * SvgScale) - lift);
                    counter.alpha = item.Disabled ? 0.7f : 1f;
                }
            }

            _tooltip.gameObject.SetActive(false);
        }

        private static void PrepareFill(Image image, float span, float gap, int index)
        {
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = Mathf.Max(0f, span - gap) / 360f;

            // 채우기는 위에서 시계 방향으로 자란다 — 조각 가운데가 i×span 에 오게 돌린다 (유니티 z 는 반시계가 +)
            image.rectTransform.localEulerAngles = new Vector3(0f, 0f, (span - gap) * 0.5f - index * span);
        }

        /// <summary>
        /// 조각 id 의 테두리 상자 네 귀퉁이(월드) — 튜토리얼이 그 조각을 비춘다 `[소스] document.getElementById(`cm-slice-${id}`).getBoundingClientRect()`.
        /// </summary>
        public bool TryGetSliceCorners(string id, Vector3[] corners)
        {
            int index = _items.FindIndex(item => item.Id == id);

            if (index < 0)
                return false;

            float inner = _level == 0 ? InnerRadius : InnerRadiusSub;
            float outer = _level == 0 ? OuterRadius : OuterRadiusSub;
            float span = 360f / _items.Count;
            float half = (span - GapRadians * Mathf.Rad2Deg) * 0.5f;
            float from = index * span - half;
            float to = index * span + half;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            const int Steps = 24;

            // 부채꼴 둘레를 촘촘히 따라가며 상자를 넓힌다 (안쪽 · 바깥 호 둘 다)
            for (int i = 0; i <= Steps; i++)
            {
                float theta = Mathf.Lerp(from, to, i / (float)Steps) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(theta), Mathf.Cos(theta));
                min = Vector2.Min(min, Vector2.Min(dir * inner, dir * outer));
                max = Vector2.Max(max, Vector2.Max(dir * inner, dir * outer));
            }

            SetCorners(corners, min, max);
            return true;
        }

        /// <summary>메뉴 SVG 전체 상자 네 귀퉁이(월드) — 튜토리얼 카드가 이 상자 밖에 붙는다 `[소스] cardBoundary:`svg``.</summary>
        public void GetMenuCorners(Vector3[] corners)
        {
            float half = (_level == 0 ? BaseSize : ExpandedSize) * 0.5f;
            SetCorners(corners, new Vector2(-half, -half), new Vector2(half, half));
        }

        private void SetCorners(Vector3[] corners, Vector2 min, Vector2 max)
        {
            corners[0] = _center.TransformPoint(new Vector3(min.x, min.y));
            corners[1] = _center.TransformPoint(new Vector3(min.x, max.y));
            corners[2] = _center.TransformPoint(new Vector3(max.x, max.y));
            corners[3] = _center.TransformPoint(new Vector3(max.x, min.y));
        }

        /// <summary>가운데 기준 좌표 → 조각 번호. 가운데 원이면 −2, 조각 밖(틈 · 고리 밖)이면 −1.</summary>
        public int SliceAt(Vector2 local)
        {
            float r = local.magnitude;

            if (r <= CenterRadius)
                return -2;

            float inner = _level == 0 ? InnerRadius : InnerRadiusSub;
            float outer = _level == 0 ? OuterRadius : OuterRadiusSub;

            if (r < inner || r > outer || _items.Count == 0)
                return -1;

            float span = 360f / _items.Count;
            float theta = Mathf.Atan2(local.x, local.y) * Mathf.Rad2Deg;
            if (theta < 0f)
                theta += 360f;

            int index = Mathf.RoundToInt(theta / span) % _items.Count;
            float offset = Mathf.DeltaAngle(theta, index * span);

            return Mathf.Abs(offset) <= (span - GapRadians * Mathf.Rad2Deg) * 0.5f ? index : -1;
        }

        protected override void OnOpened()
        {
            _hitArea.OnClickLocal += HandleClick;
            _hitArea.OnHoverLocal += HandleHover;
            _hitArea.OnExit += HandleExit;
            _dismiss.onClick.AddListener(HandleDismiss);
        }

        protected override void OnClosing()
        {
            _hitArea.OnClickLocal -= HandleClick;
            _hitArea.OnHoverLocal -= HandleHover;
            _hitArea.OnExit -= HandleExit;
            _dismiss.onClick.RemoveListener(HandleDismiss);
        }

        private void HandleClick(Vector2 local)
        {
            int index = SliceAt(local);

            if (index >= 0)
            {
                // 못 누르는 조각은 아무 일도 없다 [소스 else if(t)return]
                if (_items[index].Disabled == false)
                    OnItem?.Invoke(_items[index].Id);

                return;
            }

            if (index == -2)
                OnCenter?.Invoke();
        }

        private void HandleHover(Vector2 local)
        {
            int index = SliceAt(local);
            bool show = index >= 0;
            _tooltip.gameObject.SetActive(show);

            if (show == false)
                return;

            FrontWarsRadialItem item = _items[index];
            _tooltipTitle.text = item.Title ?? item.Id;
            _tooltipBody.text = TooltipBody(item);
            _tooltip.anchoredPosition = _center.anchoredPosition + local + new Vector2(TooltipOffset.x, -TooltipOffset.y);
        }

        /// <summary>
        /// 툴팁 본문 `[소스] pa.tooltipDesc #d1d5db 13px · tooltipCost #fbbf24 13px 500 · tooltipCounter #7dd3fc 13px 600 · tooltipCooldown #93c5fd 13px 600 · tooltipError #ef4444 12px 기울임`.
        /// </summary>
        private string TooltipBody(FrontWarsRadialItem item)
        {
            var sb = new System.Text.StringBuilder();

            if (string.IsNullOrEmpty(item.Description) == false)
                sb.Append("<size=13><color=#D1D5DB>").Append(item.Description).Append("</color></size>");

            if (string.IsNullOrEmpty(item.Cost) == false)
                sb.Append("\n<size=13><color=#FBBF24>").Append(item.Cost).Append("</color></size>");

            if (string.IsNullOrEmpty(item.CounterLine) == false)
                sb.Append("\n<size=13><b><color=#7DD3FC>").Append(item.CounterLine).Append("</color></b></size>");

            if (string.IsNullOrEmpty(item.CooldownLine) == false)
                sb.Append("\n<size=13><b><color=#93C5FD>").Append(item.CooldownLine).Append("</color></b></size>");

            if (item.Disabled)
                sb.Append("\n<size=12><i><color=#EF4444>").Append(_unavailable).Append("</color></i></size>");

            return sb.ToString();
        }

        private void HandleExit() => _tooltip.gameObject.SetActive(false);
        private void HandleDismiss() => OnDismiss?.Invoke();

        private static Sprite RingTop() => _ringTop != null ? _ringTop : _ringTop = Ring(InnerRadius / OuterRadius);
        private static Sprite RingSub() => _ringSub != null ? _ringSub : _ringSub = Ring(InnerRadiusSub / OuterRadiusSub);

        /// <summary>조각 고리 그림 — 한 변 = 바깥 지름. 안쪽 반지름 비율만큼 비운다.</summary>
        private static Sprite Ring(float innerRatio)
        {
            var tex = new Texture2D(RingTextureSize, RingTextureSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            float half = RingTextureSize * 0.5f;
            float inner = half * innerRatio;
            var pixels = new Color32[RingTextureSize * RingTextureSize];

            for (int y = 0; y < RingTextureSize; y++)
            {
                for (int x = 0; x < RingTextureSize; x++)
                {
                    float d = Mathf.Sqrt((x + 0.5f - half) * (x + 0.5f - half) + (y + 0.5f - half) * (y + 0.5f - half));
                    float a = Mathf.Clamp01(half - d) * Mathf.Clamp01(d - inner);
                    pixels[y * RingTextureSize + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, RingTextureSize, RingTextureSize), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
