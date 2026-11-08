namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 유닛 · 건물 종류 — 표 <c>FrontWarsUnitTable</c> 순서와 같다 `[소스] Cq3gr8XO.js:S.TransportShip=`Transport``.
    /// <para>원본 해시는 종류 «이름 문자열»(표 DisplayName)의 해시를 쓴다 — <see cref="FrontWarsUnitNames"/>.</para>
    /// </summary>
    public enum EFrontWarsUnit
    {
        TransportShip,
        Warship,
        Shell,
        SAMMissile,
        Port,
        AtomBomb,
        HydrogenBomb,
        TradeShip,
        MissileSilo,
        DefensePost,
        SAMLauncher,
        City,
        MIRV,
        MIRVWarhead,
        Construction,
        Train,
        Factory
    }

    /// <summary>원본 유닛 종류 문자열 — 해시 · 표 조회 키.</summary>
    public static class FrontWarsUnitNames
    {
        private static readonly string[] Names =
        {
            "Transport", "Warship", "Shell", "SAMMissile", "Port", "Atom Bomb", "Hydrogen Bomb", "Trade Ship", "Missile Silo",
            "Defense Post", "SAM Launcher", "City", "MIRV", "MIRV Warhead", "Construction", "Train", "Factory"
        };

        public static string Of(EFrontWarsUnit type)
        {
            return Names[(int)type];
        }
    }
}
