namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 유닛 길찾기 한 걸음의 결과 — 원본 `{type, node}` 객체 `[소스] Cq3gr8XO.js:type:Y.NextTile,node:this.path[this.pathIndex++]`.
    /// <para>칸이 없는 결과(Pending · PathNotFound)는 <see cref="Tile"/> 이 <see cref="NoTile"/> 이다.</para>
    /// </summary>
    public readonly struct FrontWarsPathStep
    {
        /// <summary>칸 없음. 원본의 `node` 없음(undefined) 자리.</summary>
        public const int NoTile = -1;

        public readonly EFrontWarsPathStatus Status;
        public readonly int Tile;

        public FrontWarsPathStep(EFrontWarsPathStatus status, int tile = NoTile)
        {
            Status = status;
            Tile = tile;
        }

        public override string ToString()
        {
            return Tile == NoTile ? Status.ToString() : $"{Status}:{Tile}";
        }
    }
}
