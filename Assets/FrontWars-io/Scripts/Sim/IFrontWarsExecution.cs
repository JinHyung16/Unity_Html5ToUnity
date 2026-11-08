namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 시뮬 한 틱에 도는 실행 하나 — 공격 · 스폰 · 플레이어 경제 · AI · 승리 판정 …
    ///
    /// <para>
    /// 원본 실행 객체의 계약 그대로다 `[소스] Cq3gr8XO.js:executeNextTick` —
    /// 게임에 넣으면 먼저 「아직 시작 안 한」 목록에 들어가고, 다음 틱 끝에서 <see cref="Init"/> 되며,
    /// 그다음 틱부터 <see cref="Tick"/> 이 불린다. <see cref="IsActive"/> 가 거짓이 되면 정리된다.
    /// </para>
    /// </summary>
    public interface IFrontWarsExecution
    {
        /// <summary>시작할 때 한 번. 틱 번호는 init 되는 틱이다.</summary>
        void Init(FrontWarsGame game, int tick);

        /// <summary>매 틱.</summary>
        void Tick(int tick);

        bool IsActive { get; }

        /// <summary>스폰 단계에도 도는가 (스폰 · 승리 판정 전 대기 · 튜토리얼 워커 …).</summary>
        bool ActiveDuringSpawnPhase { get; }
    }
}
