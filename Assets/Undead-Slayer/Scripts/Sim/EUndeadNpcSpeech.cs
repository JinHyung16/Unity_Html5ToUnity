namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// NPC 가 지금 하는 말 [소스 — <c>showBubble</c> · <c>introductionText</c>].
    ///
    /// <para>
    /// ★ 전사(<see cref="EUndeadWarriorSpeech"/>)와 <b>따로 둔다</b> — 전사는 말풍선이 «상시»고
    /// 이쪽은 <b>사건에 잠깐</b> 뜬다. 한 열거형으로 합치면 뜨는 조건이 섞인다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>[사고 · 재발방지 #174]</b> 문구 표에는 여섯 줄이 다 있었는데 <b>읽는 곳이 없었다</b> —
    /// 마법사·가족·농부가 원본에서는 말을 하는데 우리 화면에서는 <b>아무 말도 안 했다</b>.
    /// 문구 감사는 「키가 다 있나」만 봐서 통과했다.
    /// </para>
    /// </summary>
    public enum EUndeadNpcSpeech
    {
        None = 0,

        /// <summary>마법사 — 「비전 파편 2개를 잃어버렸어」. 만나기 «전»에 떠 있다.</summary>
        MageIntro,

        /// <summary>마법사 — 「날 구해줬어, 이건 네 보상이야!」. 조각을 다 꽂으면 <b>2초</b>.</summary>
        MageReward,

        /// <summary>가족 — 「이 어두운 영혼을 통과해서 지나갈 수 없어!」. 소환이 시작될 때 <b>5초</b>.</summary>
        FamilyWarning,

        /// <summary>가족 — 「고마워! 이제 우리도 계속 갈 수 있어」. 보상이 끝나면 <b>3초</b>.</summary>
        FamilyThanks,

        /// <summary>농부 — 「내 양들이 위험해, 제발 다시 데려와 줘!」. 만나기 «전»에 떠 있다.</summary>
        FarmerIntro,

        /// <summary>농부 — 「나의 영웅! 고마워!」. 양을 다 모으면 <b>3초</b>.</summary>
        FarmerThanks,
    }
}
