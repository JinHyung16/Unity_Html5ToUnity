namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 전사가 지금 <b>무슨 말을 하고 있나</b> [소스 <c>Bd</c> — 말풍선 하나를 «갈아끼워» 쓴다].
    ///
    /// <para>
    /// ⚠ 원본은 말풍선이 <b>개체 하나</b>고 문구·그림만 바뀐다 — 그래서 «셋이 동시에» 뜨는 일이 없다.
    /// 뷰가 갈래별로 창을 따로 두면 겹쳐 뜬다.
    /// </para>
    /// </summary>
    public enum EUndeadWarriorSpeech
    {
        /// <summary>말풍선이 없다.</summary>
        None,

        /// <summary>구조 «전» — 「도와줘!」 [소스 <c>helpMe</c>].</summary>
        Help,

        /// <summary>구조 «직후» 2초 — 「고마워, 친구!」 [소스 <c>thanksPal</c>].</summary>
        Thanks,

        /// <summary>
        /// <b>다음 과제를 예고한다</b> — 큰 말풍선(<c>bubble_medium</c>)이다 [소스 <c>variant "medium"</c>].
        /// <para>문구는 <b>예고 중인 과제</b>가 정한다 — <c>PendingQuest</c> 를 본다.</para>
        /// </summary>
        QuestIntro,
    }
}
