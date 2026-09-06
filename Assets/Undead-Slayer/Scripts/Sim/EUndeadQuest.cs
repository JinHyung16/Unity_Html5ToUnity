namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 바이옴 1 의 퀘스트 순서 [소스 직독 · 회차 9].
    ///
    /// <para>
    /// 원본의 순서는 <b>전사 → 마법사 → 첫 보스 → 농부/양</b> 넷이고 <b>하나씩 순차</b>로 돈다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>넷 다 이관한다</b> — 사람 확정(「원본이랑 같으면 되잖아」). 「한 판의 클리어」는
    /// <b>농부 퀘스트까지 끝내는 것</b>이다.
    /// </para>
    /// </summary>
    public enum EUndeadQuest
    {
        /// <summary>아직 아무 퀘스트도 안 켜졌다 — 전투 시작 뒤 «시계»가 전사 퀘스트를 켠다 [소스 <c>currentQuest: void 0</c>].</summary>
        None,

        /// <summary>쓰러진 전사를 구한다 — 반경 안에 4초 머물기.</summary>
        Warrior,

        /// <summary>마법사의 아케인 조각 둘을 찾아 준다 — 보상은 <b>번개 2개</b>.</summary>
        Mage,

        /// <summary>가족을 막아선 <b>darkSoul</b> 을 잡는다 — 보상은 보석 분수 100개.</summary>
        FirstBoss,

        /// <summary>농부의 양 5마리를 데려다 준다 — 보상은 보석 분수 200개.</summary>
        FarmerSheep,

        /// <summary>이관 범위의 퀘스트를 모두 마쳤다.</summary>
        Done,
    }
}
