namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 길찾기 한 번의 결과 종류 `[소스] Cq3gr8XO.js:e[e.NextTile=0]=`NextTile``.
    /// <para>숫자 값도 원본과 같다 — 원본이 이 값을 비교·해시에 쓰지는 않지만 로그 대조 때 헷갈리지 않게 맞춘다.</para>
    /// </summary>
    public enum EFrontWarsPathStatus
    {
        NextTile = 0,     // 다음 칸이 있다 — Tile 로 옮긴다
        Pending = 1,      // 아직 계산 중 — 다음 틱에 다시 부른다
        Completed = 2,    // 도착했다 (또는 탐색이 끝났다)
        PathNotFound = 3  // 길이 없다
    }
}
