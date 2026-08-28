namespace JinHyung.Core
{
    /// <summary>
    /// 매 프레임 갱신이 필요한 <see cref="BaseManager"/> 만 구현한다.
    ///
    /// <para>
    /// <c>BaseGameManager</c> 하나가 구현체 목록을 <b>등록 순서대로</b> 돌린다.
    /// 그래서 원본 메인 루프의 호출 순서를 그대로 재현할 수 있다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>원본 메인 루프의 정지 게이트를 여기 진입부에 그대로 옮긴다.</b>
    /// 원본이 「창이 열려 있으면 루프를 멈춘다」면 그 <c>if</c> 한 줄이 여기 와야 한다 —
    /// 함수 단위로만 이식하면 그 줄이 통째로 사라진다.
    /// </para>
    ///
    /// <para>
    /// <c>OnFixedUpdate</c> 는 두지 않았다. 물리를 쓰는 게임이 오면 그때 추가한다
    /// (안 쓰는 인터페이스 멤버는 구현체마다 빈 메서드만 남긴다).
    /// </para>
    /// </summary>
    public interface IGameUpdate
    {
        void OnUpdate(float deltaTime);
    }
}
