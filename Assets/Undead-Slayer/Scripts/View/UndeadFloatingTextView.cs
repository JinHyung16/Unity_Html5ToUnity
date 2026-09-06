using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using TMPro;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 월드에 <b>뜨는 문구</b> — 원본의 <c>+3 XP</c> · <c>-2</c> · <c>+1</c>(하트) · 「최대 체력」 · 「화염 소진!」 [소스 직독].
    ///
    /// <para>
    /// ★ <b>풀링</b>이다. 개체당 컴포넌트를 안 붙이는 것과 같은 이유 —
    /// 보석은 초당 여러 개가 들어오고, 그때마다 <c>GameObject</c> 를 만들면 GC 가 화면을 끊는다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>값은 <see cref="UndeadPopupDataContainer"/> 가 들고 있다</b> — 갈래마다 수명·속도·크기·색·흔들림이
    /// 전부 다르다. 회차 11 까지는 한 벌(0.35초 · 17.5px)로 뭉뚱그려 <b>XP·피격·회복이 전부 원본과 달랐다</b>.
    /// </para>
    /// </summary>
    public sealed class UndeadFloatingTextView : MonoBehaviour
    {
        private struct Item
        {
            public TMP_Text Text;
            public SpriteRenderer Heart;
            public UndeadPopupData Spec;
            public double AgeMs;
            public double StartX;
            public double StartY;
            public Color Fill;
        }

        [SerializeField] private TMP_FontAsset _font;

        private readonly List<Item> _items = new List<Item>(64);
        private readonly Stack<TMP_Text> _pool = new Stack<TMP_Text>(64);
        private readonly Stack<SpriteRenderer> _heartPool = new Stack<SpriteRenderer>(8);
        private UndeadPopupDataContainer _table;
        private UndeadSpriteSet _heartSet;

        /// <summary>지금 떠 있는 문구 수 — <b>검사가 이 값을 센다</b>.</summary>
        public int ActiveCount
        {
            get { return _items.Count; }
        }

        public int PooledCount
        {
            get { return _pool.Count + _items.Count; }
        }

        /// <summary>갈래별 값을 들고 있는 표를 넣는다 — <b>없으면 아무것도 안 띄운다</b>(조용히 넘기지 않고 오류).</summary>
        public void SetTable(UndeadPopupDataContainer table)
        {
            _table = table;
        }

        /// <summary>회복·피격 팝업 오른쪽에 붙는 하트 [소스 <c>Yl</c> · <c>Ul</c> — <c>scale 2.5</c>].</summary>
        public void SetHeart(UndeadSpriteSet heart)
        {
            _heartSet = heart;
        }

        public void SetFont(TMP_FontAsset font)
        {
            _font = font;
        }

        /// <summary>
        /// 문구 하나를 띄운다. <paramref name="world"/> 는 <b>원본 world 좌표의 «글자 자리»</b>다 —
        /// 개체 자리에서 얼마를 올릴지는 <b>부르는 쪽</b>이 정한다 (원본도 개체마다 다르다).
        /// </summary>
        public void Spawn(string code, UndeadVec2 world, string value)
        {
            if (_table == null)
            {
                Core.Log.Error("팝업 표가 없다 — UndeadPopupTable 등록을 본다");
                return;
            }

            UndeadPopupData spec = _table.Get(code);

            if (spec == null)
            {
                Core.Log.Error($"팝업 갈래가 표에 없다 — {code}");
                return;
            }

            TMP_Text text = _pool.Count > 0 ? _pool.Pop() : Create();

            text.text = value;
            text.fontSize = (float)spec.FontSize / UndeadUnits.WorldPixelsPerUnit * PointsPerWorldUnit;
            text.outlineWidth = OutlineWidth(spec);
            text.gameObject.SetActive(true);

            Color fill = Hex(spec.FillHex);
            text.color = fill;
            text.outlineColor = Hex(spec.StrokeHex);

            double offset = spec.RandomOffset;
            double x = world.X + (offset > 0.0 ? (RandomUtil.Value() - 0.5) * offset * 2.0 : 0.0);
            double y = world.Y + (offset > 0.0 ? (RandomUtil.Value() - 0.5) * offset * 2.0 : 0.0);

            SpriteRenderer heart = null;

            if (spec.HeartIcon && _heartSet != null)
            {
                heart = _heartPool.Count > 0 ? _heartPool.Pop() : CreateHeart();
                heart.gameObject.SetActive(true);
            }

            var item = new Item { Text = text, Heart = heart, Spec = spec, AgeMs = 0.0, StartX = x, StartY = y, Fill = fill };
            _items.Add(item);
            Apply(item, 0.0);
        }

        private void LateUpdate()
        {
            double dtMs = Time.deltaTime * 1000.0;

            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Item item = _items[i];
                item.AgeMs += dtMs;

                if (item.AgeMs >= item.Spec.LifetimeMs)
                {
                    item.Text.gameObject.SetActive(false);
                    _pool.Push(item.Text);

                    if (item.Heart != null)
                    {
                        item.Heart.gameObject.SetActive(false);
                        _heartPool.Push(item.Heart);
                    }

                    _items.RemoveAt(i);
                    continue;
                }

                Apply(item, item.AgeMs / item.Spec.LifetimeMs);
                _items[i] = item;
            }
        }

        /// <summary>한 프레임 값 — 전부 <b>소스 식 그대로</b>다 (표의 계수만 갈래마다 다르다).</summary>
        private void Apply(Item item, double progress)
        {
            UndeadPopupData s = item.Spec;

            // y += velocityY·dt  ⇒  y₀ + velocityY·경과
            double y = item.StartY + s.VelocityYPerMs * item.AgeMs;
            double wobble = s.WobbleAmplitude * Mathf.Sin((float)(progress * Mathf.PI * s.WobbleHalfCycles)) * (1.0 - s.WobbleDecay * progress);
            double x = item.StartX + wobble;

            item.Text.transform.localPosition = UndeadUnits.ToPosition(x, y);

            float alpha = progress > s.FadeStart && s.FadeStart < 1.0
                ? 1f - (float)((progress - s.FadeStart) / (1.0 - s.FadeStart))
                : 1f;

            Color color = item.Fill;
            color.a = alpha;
            item.Text.color = color;
            item.Text.outlineColor = WithAlpha(item.Text.outlineColor, alpha);

            float scale = 1f;

            if (s.PopFromScale > 1.0 && s.PopUntil > 0.0 && progress < s.PopUntil)
                scale = (float)(s.PopFromScale - (s.PopFromScale - 1.0) * (progress / s.PopUntil));

            if (s.BreathAmplitude > 0.0)
                scale *= (float)(1.0 + s.BreathAmplitude * Mathf.Sin((float)(progress * Mathf.PI * 2.0)));

            item.Text.transform.localScale = new Vector3(scale, scale, 1f);

            if (item.Heart == null)
                return;

            // 하트는 «글자 오른쪽 끝 + 30» 에 선다 [소스 — text.width/2 + 30 · y −3 · scale 2.5]
            item.Text.ForceMeshUpdate();
            double halfWidth = UndeadUnits.ToWorldLength(item.Text.textBounds.size.x) * 0.5;
            item.Heart.transform.localPosition = UndeadUnits.ToPosition(x + halfWidth + HeartGap, y - 3.0);
            item.Heart.transform.localScale = new Vector3(HeartScale * scale, HeartScale * scale, 1f);
            item.Heart.color = new Color(1f, 1f, 1f, alpha);
        }

        /// <summary>
        /// TMP 3D 텍스트의 <c>fontSize</c> 는 <b>유닛이 아니라 포인트</b>다 — 유닛으로 넣으면 글자가 10배 작다.
        /// <b>[실측 보정]</b> 원본 14px 글자의 글리프 상자 높이 18px 에 맞춘 값 (회차 12 덤프).
        /// </summary>
        private const float PointsPerWorldUnit = 10.6f;

        private const double HeartGap = 30.0;    // [소스 heart.x = text.width/2 + 30]
        private const float HeartScale = 2.5f;   // [소스 heart.scale.set(2.5)]

        /// <summary>TMP 외곽선은 «글자 크기 대비 0~1» 이라 원본 px 두께를 그 비율로 바꾼다.</summary>
        private static float OutlineWidth(UndeadPopupData spec)
        {
            return Mathf.Clamp01((float)(spec.StrokeWidth / Mathf.Max(1.0f, (float)spec.FontSize)));
        }

        private static Color Hex(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out Color color) ? color : Color.white;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private TMP_Text Create()
        {
            var go = new GameObject("FloatingText");
            go.transform.SetParent(transform, false);

            var text = go.AddComponent<TextMeshPro>();
            text.alignment = TextAlignmentOptions.Center;
            text.sortingOrder = 20000;   // 개체보다 위
            text.enableWordWrapping = false;
            text.fontMaterial.EnableKeyword("OUTLINE_ON");

            if (_font != null)
                text.font = _font;

            return text;
        }

        private SpriteRenderer CreateHeart()
        {
            var go = new GameObject("PopupHeart");
            go.transform.SetParent(transform, false);

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _heartSet.Get(0, 0.0);
            renderer.sortingOrder = 20000;
            return renderer;
        }
    }
}
