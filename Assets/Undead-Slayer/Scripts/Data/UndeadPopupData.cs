namespace JinHyung.Data
{
    /// <summary>
    /// 월드에 <b>떠오르는 문구</b> 한 갈래 [소스 직독 — <c>xu</c>(XP) · <c>$l</c>(적 피해) ·
    /// <c>Yl</c>(회복) · <c>Ul</c>(피격) · <c>Dl</c>(최대 체력) · <c>Ol</c>(화염 소진)].
    ///
    /// <para>
    /// ★ <b>여섯 갈래가 서로 다른 값</b>이다 — 수명·속도·글자 크기·색·흔들림·페이드 시작이 전부 다르다.
    /// 하나로 뭉뚱그리면 XP 는 너무 느리고 피격은 너무 작아진다. <b>표로 나눠 두는 이유가 이것이다.</b>
    /// </para>
    ///
    /// <para>
    /// ⚠ 값을 <b>코드 <c>const</c> 로 되돌리지 않는다</b> — 연출 수치는 눈으로 맞추는 값이라
    /// 표(또는 프리팹)가 자리다 (<c>SKILL.md</c> 「옮기지 않고 코드에 남은 것」).
    /// </para>
    /// </summary>
    public class UndeadPopupData : IData, IDataKey<string>
    {
        public int Id { get; set; }

        /// <summary>갈래 (<c>xp</c> · <c>enemyDamage</c> · <c>lightningDamage</c> · <c>heroHeal</c> · <c>heroDamage</c> · <c>fullHealth</c> · <c>fireOut</c>).</summary>
        public string Code { get; set; }

        /// <summary>수명 (ms) [소스 <c>maxLifetime</c>].</summary>
        public double LifetimeMs { get; set; }

        /// <summary>세로 속도 (원본 px / ms · <b>음수가 위</b>) [소스 <c>velocityY</c>].</summary>
        public double VelocityYPerMs { get; set; }

        /// <summary>글자 크기 (원본 px) [소스 <c>style.fontSize</c>].</summary>
        public double FontSize { get; set; }

        public string FillHex { get; set; }

        public string StrokeHex { get; set; }

        /// <summary>테두리 두께 (원본 px) [소스 <c>stroke.width</c>].</summary>
        public double StrokeWidth { get; set; }

        /// <summary>좌우 흔들림 폭 (원본 px) — <c>x = x₀ + A·sin(진행·π·주기)·(1 − 감쇠·진행)</c>.</summary>
        public double WobbleAmplitude { get; set; }

        /// <summary>흔들림 주기 — <c>π</c> 에 곱하는 수 (2 = 한 바퀴 · 1.5 = 3/4 바퀴).</summary>
        public double WobbleHalfCycles { get; set; }

        /// <summary>흔들림 감쇠 — 진행에 곱해 1 에서 뺀다.</summary>
        public double WobbleDecay { get; set; }

        /// <summary>이 진행도부터 알파가 선형으로 0 이 된다 [소스 <c>e &gt; .5</c> 등].</summary>
        public double FadeStart { get; set; }

        /// <summary>튀어나오는 시작 배율 (1 = 없음) [소스 XP <c>1.2 − .2t</c>].</summary>
        public double PopFromScale { get; set; }

        /// <summary>그 배율이 1 로 돌아오는 진행도.</summary>
        public double PopUntil { get; set; }

        /// <summary>숨쉬는 배율 폭 — <c>1 + A·sin(진행·2π)</c> [소스 회복·피격 .08 · 모닥불 .06].</summary>
        public double BreathAmplitude { get; set; }

        /// <summary>시작 자리 무작위 범위 (원본 px · x·y 각각 ±) [소스 <c>randomOffsetRange</c>].</summary>
        public double RandomOffset { get; set; }

        /// <summary>글자 오른쪽에 <b>하트</b>를 붙인다 [소스 — 회복·피격만].</summary>
        public bool HeartIcon { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
