using System;

namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// ★★★ <b>홀드 스쿼시의 «시계» — 이 게임에 하나뿐이다.</b>
    ///
    /// <para>
    /// 8회차가 원본 시트에서 읽은 트윈 셋이 <b>전부 같은 시계</b>로 돈다 [실측]:
    /// </para>
    ///
    /// <list type="bullet">
    /// <item>블롭 몸(<c>spr_SlimePhysics</c>) <b>87.14×50 → 113.28×35</b> · Tween <c>"slimeSquash"</c></item>
    /// <item>머리 위 공(<c>spr_BallVisu</c>) <b>94×93 → 112.8×111.6</b> · Tween <c>"sizeBall"</c></item>
    /// <item>★ <b>발사점</b> — 12회차가 찾았다. 머리공이 눌려 내려간 만큼 <b>발사 위치가 따라 내려간다</b></item>
    /// </list>
    ///
    /// <code>
    /// 진행도 p = min(1, 홀드초 / 1.5)        ← 1.5 s 포화
    /// 이징    s = sin(π/2 · p)               ← ease[3] = OutSine [8회차 실측 · 8점 ±0.016]
    /// </code>
    ///
    /// <para>
    /// ★★ <b>이 상수를 다른 파일에 다시 적지 마라.</b> 같은 시계가 세 곳(블롭 · 머리공 · 발사점)에
    /// 흩어져 있으면 한 곳만 고쳐졌을 때 <b>「블롭은 눌렸는데 발사점은 안 내려간」</b> 상태가 되고,
    /// 그 어긋남은 <b>골든 점 몇 개로는 안 잡힌다</b> — 창 하한에서만 보인다 (12회차가 그것을 겪었다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>힘 포화(1.633 s)와 «다른 시간»이다.</b> 진행도(0~1)를 힘 쪽과 공유하면 8 % 어긋난다 —
    /// 그래서 이 클래스는 <b>«초»를 받는다</b>. force 를 갖고 있으면
    /// <see cref="BlumgiPowerModel.HoldSecondsAtForce"/> 로 초를 먼저 만든다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>트윈이 «끝난 뒤»의 한 단계 더 눌림</b>(블롭 118.04×32.27 [8회차 실측])은 여기 없다.
    /// 발사점 쪽에서 그것이 어떻게 실리는지는 <b>미측정</b>이다 — 12회차 표본이 force 59.5(홀드 0.9 s)까지라
    /// 포화 구간을 안 지났다. 지어내지 않고 <b>재측정 대기</b>에 올렸다.
    /// </para>
    /// </summary>
    public static class BlumgiSquashTween
    {
        /// <summary>
        /// 스쿼시 트윈 지속(초) — <b>1.5</b> [8회차 실측 · <c>eControls</c> EVENT#45 원문].
        /// <b>블롭 · 머리공 · 발사점이 전부 이 값을 읽는다.</b>
        /// </summary>
        public const double HoldSeconds = 1.5;

        /// <summary>유니티 뷰가 쓰는 float 사본. <b>값은 <see cref="HoldSeconds"/> 하나다.</b></summary>
        public const float HoldSecondsFloat = (float)HoldSeconds;

        /// <summary>홀드 진행도 0~1. 포화 뒤에도 1 을 넘지 않는다.</summary>
        public static double Progress(double holdSeconds)
        {
            if (holdSeconds <= 0.0)
                return 0.0;

            double p = holdSeconds / HoldSeconds;
            return p >= 1.0 ? 1.0 : p;
        }

        /// <summary>
        /// 이징이 적용된 진행도 <c>s = sin(π/2 · p)</c> — <b>ease[3] = OutSine</b> [8회차 실측].
        /// 발사점·크기 트윈이 전부 이 값에 선형으로 얹힌다.
        /// </summary>
        public static double Ease(double holdSeconds)
        {
            return Math.Sin(Progress(holdSeconds) * Math.PI * 0.5);
        }

        /// <summary>뷰용 float 판. <b>계산은 double 로 하고 마지막에만 내린다</b>.</summary>
        public static float EaseFloat(float holdSeconds)
        {
            return (float)Ease(holdSeconds);
        }
    }
}
