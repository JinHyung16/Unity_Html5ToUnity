using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 레벨 진행 도트.
    ///
    /// <para>
    /// ★ <b>점을 하나씩 «켜는» 것이 아니다.</b> 채움은 <b>폭이 58 world 단위로 자라는 바</b>다 [UIUX 2-b 실측] —
    /// 빈칸 바 290 ÷ 5 = 58 이고, 채움 폭이 58 / 116 / 174 / 232 / 290 로 관측됐다.
    /// <c>SetActive</c> 로 옮기면 원본과 다른 구조가 된다.
    /// </para>
    ///
    /// <para>
    /// 빈칸·채움이 <b>별개 스프라이트</b>(<c>UI_ProgressionDot_Empty</c> / <c>_Full</c>)이고
    /// 둘 다 <b>58×48</b> 짜리 타일이다 [런타임 실측].
    /// </para>
    /// </summary>
    public sealed class BlumgiLevelDotBar : MonoBehaviour
    {
        /// <summary>도트 한 칸의 폭 (원본 world) [실측 — 채움 폭이 정확히 58 의 정수배다].</summary>
        public const float CellWorldWidth = 58f;

        [SerializeField] private Image _empty;
        [SerializeField] private Image _fill;

        [SerializeField] private RectTransform _fillRect;

        /// <summary>
        /// 채운 칸 수를 넣는다. 원본은 <b>월드당 5칸</b> [실측 — 진행률 20 % 로 교차 확인].
        ///
        /// <para>
        /// ★★ <b>여기서 재는 단위는 «원본 world px» 다</b> — 캔버스 픽셀이 아니다.
        /// 도트 바 뿌리(<c>LevelDots</c>)에 <c>localScale = 1080/1280 = 0.84375</c> 가 걸려 있어
        /// <b>world→캔버스 환산이 그 한 곳에서 «한 번»만</b> 일어난다.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>[정정 2026-08-30] 예전에는 여기서 폭에 0.84375 를 곱했다.</b> 그러면 <c>Image.Type.Tiled</c> 의
        /// <b>타일 폭은 안 줄어든다</b> — 타일 크기는 «스프라이트 px ÷ PPU × 캔버스 refPPU» 라 <b>58 캔버스 px</b> 로
        /// 고정인데 바 폭만 245 로 줄어, 245 ÷ 58 = <b>4.22 칸</b>이 되어 <b>도트가 4개 + 잘린 조각</b>으로 보였다.
        /// 「폭은 맞는데 그림이 안 맞는」 결함이라 수치 검사로는 안 걸리고 <b>캡처로만 보였다</b> (재발방지 #60).
        /// ⇒ 바를 <b>world px 로 재고 뿌리에서 한 번 스케일</b>하면 타일 58 world 와 칸 58 world 가 «같은 자»가 된다.
        /// </para>
        /// </summary>
        public void SetStep(int filled, int total)
        {
            if (_empty == null || _fillRect == null)
                return;

            const float Cell = CellWorldWidth;   // 58 world — 뿌리 스케일이 캔버스 px 로 바꾼다

            filled = Mathf.Clamp(filled, 0, Mathf.Max(total, 0));

            var emptyRect = _empty.rectTransform;
            emptyRect.sizeDelta = new Vector2(Cell * total, emptyRect.sizeDelta.y);

            _fillRect.sizeDelta = new Vector2(Cell * filled, _fillRect.sizeDelta.y);

            if (_fill != null)
                _fill.enabled = filled > 0;
        }
    }
}
