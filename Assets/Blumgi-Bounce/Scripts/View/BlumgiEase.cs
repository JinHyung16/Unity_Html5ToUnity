using UnityEngine;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 원본 트윈의 이징 — <b>8회차가 «채록으로» 확정한 세 개만 있다</b>.
    ///
    /// <para>
    /// ★★ <b>원본 시트의 <c>_easeIndex</c> 를 C3 문서의 콤보 순서로 옮기면 틀린다</b> [8회차 §3-h].
    /// 인덱스 3·6·9 가 각각 어떤 곡선인지는 <b>트윈 시계열을 떠서 맞춰</b> 확정한 것이다:
    /// </para>
    ///
    /// <list type="table">
    /// <item><term><c>ease[3]</c></term><description><b>OutSine</b> — 머리공 1.5 s 트윈 8점이 sin(pπ/2) 와 ±0.016 이내 [실측]</description></item>
    /// <item><term><c>ease[6]</c></term><description><b>OutElastic</b> — 줌 0.8→1.0 · 2 s 의 극값 4개가 예측과 일치 [실측]</description></item>
    /// <item><term><c>ease[9]</c></term><description><b>OutBack</b> — 블롭 0.25 s 복귀가 오버슛 +9.3 % [실측]</description></item>
    /// </list>
    ///
    /// <para>
    /// ⚠ <b>여기에 「아마 이럴 것」인 곡선을 더하지 마라.</b> 인덱스 2·4 는 미측정이다
    /// (3 과 같은 계열이라는 정합만 있다). 「6 = OutQuad」로 옮겼으면 <b>탄성 진동이 통째로 사라졌을 것</b>이다.
    /// </para>
    /// </summary>
    public static class BlumgiEase
    {
        /// <summary>
        /// <c>ease[3]</c> — <b>OutSine</b>. 머리공 크기·블롭 스쿼시·<b>발사점</b> 트윈(1.5 s)이 이것이다.
        ///
        /// <para>
        /// ⚠ <b>홀드 스쿼시에는 이것을 직접 부르지 마라 — <see cref="BlumgiSquashTween"/> 를 부른다.</b>
        /// 12회차가 <b>발사점도 같은 트윈으로 돈다</b>는 것을 실측했고(패스 ①-h),
        /// 그때부터 이 곡선은 «시계(1.5 s)와 짝»이어야 뜻이 있다. 곡선만 따로 부르면
        /// <b>지속을 부르는 쪽마다 적게 되고 그 순간 진실이 갈린다.</b>
        /// 여기 남겨 두는 것은 <b>다른 1.5 s 아닌 트윈</b>이 생겼을 때를 위해서다.
        /// </para>
        /// </summary>
        public static float OutSine(float p)
        {
            return Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI * 0.5f);
        }

        /// <summary>
        /// <c>ease[9]</c> — <b>OutBack</b>. 발사 직후 블롭 복귀(0.25 s)가 이것이다.
        ///
        /// <para>
        /// ⚠ 표준 상수(<c>c1 = 1.70158</c>)의 오버슛은 <b>+10.0 %</b> 인데 실측은 <b>+9.3 %</b> 다 —
        /// <b>0.7 pt 차이가 남아 있다</b>. C3 가 쓰는 back 상수를 못 읽었기 때문이고,
        /// 실측에 맞춰 상수를 «고르지» 않았다 (없는 실측을 지어내는 것이라). 곡선의 «정체»가 실측이다.
        /// </para>
        /// </summary>
        public static float OutBack(float p)
        {
            const float C1 = 1.70158f;
            const float C3 = C1 + 1f;

            float u = Mathf.Clamp01(p) - 1f;
            return 1f + C3 * u * u * u + C1 * u * u;
        }

        /// <summary>
        /// <c>ease[6]</c> — <b>OutElastic</b>. 레벨 시작 줌 펀치(0.8 → 1.0 · 2 s)가 이것이다.
        ///
        /// <para>
        /// 주기 <c>p = 0.3</c>(정규화) = <b>0.6 s</b> [8회차 실측 · 극값 4개 일치].
        /// 검산 — 이 곡선에 <c>0.8 + 0.2·f</c> 를 얹으면 최대 <b>1.074</b>(t≈264 ms) ·
        /// 최소 <b>0.974</b>(t≈570 ms) · <b>1.0093</b>(t≈880 ms) 이 나오고,
        /// 실측은 <b>1.075 / 0.974 / 1.009</b> 였다.
        /// </para>
        /// </summary>
        public static float OutElastic(float p)
        {
            const float Period = 0.3f;
            const float Shift = Period * 0.25f;

            p = Mathf.Clamp01(p);

            if (p <= 0f)
                return 0f;

            if (p >= 1f)
                return 1f;

            return Mathf.Pow(2f, -10f * p) * Mathf.Sin((p - Shift) * (2f * Mathf.PI / Period)) + 1f;
        }
    }
}
