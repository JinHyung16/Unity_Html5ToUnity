namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 영토 칸 값(16비트)과 팔레트 칸 번호 규칙 — 셰이더와 똑같은 규칙을 C# 으로도 둔다 (검사 · 한 칸 색 계산용).
    ///
    /// <para>
    /// 칸 값 `[소스] BMmXchgl.js:po=2047,mo=2048,ho=4096,go=8192,_o=16384,vo=32768`:
    /// 비트 0~10 주인 작은 id · 비트 11 국경 · 비트 12 방어초소 국경 · 비트 13 낙진(주인 없음) 또는 무늬 둘째 색 ·
    /// 비트 14 내가 거는 공격 국경 · 비트 15 나에게 오는 공격 국경.
    /// </para>
    ///
    /// <para>
    /// 팔레트 = 폭 8 × 높이 2048 RGBA8. 행 = 작은 id, 열 = 칸 `[소스] xo=0,So=1,Co=2,wo=3,To=4,Eo=5`:
    /// 0 영토(α150) · 1 둘째 색(α150) · 2 국경(α255) · 3 방어 국경 밝은 칸 · 4 방어 국경 어두운 칸 · 5 관계 색(전술 보기).
    /// 0행은 특수: 1 · 4~7 낙진 5색(α150) · 2 내 공격 국경 · 3 받는 공격 국경 `[소스] Ao=[Do,4,5,6,7]`.
    /// </para>
    /// </summary>
    public static class FrontWarsTerritoryCode
    {
        public const int IdMask = 2047;
        public const int BorderBit = 2048;
        public const int DefendedBit = 4096;
        public const int AltBit = 8192;
        public const int OutgoingAttackBit = 16384;
        public const int IncomingAttackBit = 32768;
        public const int AttackMask = 49152;

        public const int PaletteWidth = 8;

        /// <summary>팔레트 행 수 = 작은 id 상한 `[소스] bo=2048`.</summary>
        public const int PaletteHeight = 2048;

        public const int SlotTerritory = 0;
        public const int SlotAlt = 1;
        public const int SlotBorder = 2;
        public const int SlotDefendedLight = 3;
        public const int SlotDefendedDark = 4;
        public const int SlotRelation = 5;

        /// <summary>0행 — 공격 국경 색 칸 `[source] Oo=2,ko=3`.</summary>
        public const int SlotOutgoingAttack = 2;
        public const int SlotIncomingAttack = 3;

        /// <summary>영토 · 둘째 색 · 낙진 알파 `[소스] No=150,Po=150`. 국경 · 관계 · 공격은 255.</summary>
        public const byte TerritoryAlpha = 150;
        public const byte FalloutAlpha = 150;

        /// <summary>
        /// 칸 하나의 값을 만든다 `[소스] BMmXchgl.js:renderTile(e,t=!1){`.
        /// <para>주인 없음: 낙진이면 낙진 비트만, 아니면 0. 주인 있음: id | 국경이면 (국경 | 공격 방향 | 방어초소).</para>
        /// <para>국경이 아닐 때의 둘째 색 비트는 무늬(코스메틱)용이라 싱글에서는 늘 꺼진다.</para>
        /// </summary>
        public static ushort Encode(int ownerSmallId, bool hasFallout, bool isBorder, int attackDirection, bool defended)
        {
            if (ownerSmallId == 0)
                return (ushort)(hasFallout ? AltBit : 0);

            int value = ownerSmallId & IdMask;

            if (isBorder)
            {
                value |= BorderBit;

                if (attackDirection == 1)
                    value |= OutgoingAttackBit;
                else if (attackDirection == 2)
                    value |= IncomingAttackBit;

                if (defended)
                    value |= DefendedBit;
            }

            return (ushort)value;
        }

        /// <summary>
        /// 낙진 칸의 색 칸 번호 (1 또는 4~7) — 칸 좌표 해시 `[소스] BMmXchgl.js:function jo(e,t){` · 셰이더 `Fallout speckle`.
        /// </summary>
        public static int FalloutSlot(int x, int y)
        {
            uint h = unchecked(((uint)x * 2654435761u) ^ ((uint)y * 2246822507u));
            h ^= h >> 13;
            h = unchecked(h * 3266489909u);
            h ^= h >> 16;

            uint shade = h % 5u;
            return shade == 0 ? 1 : (int)shade + 3;
        }

        /// <summary>
        /// 칸 값 → 팔레트 칸 번호 (행×8 + 열). 투명이면 −1 `[소스] BMmXchgl.js:function Mo(e,t,n,r){` — 셰이더 overlayAt 과 같은 규칙.
        /// </summary>
        public static int PaletteIndex(int value, int x, int y, bool tactical)
        {
            int id = value & IdMask;

            if (tactical)
                return (value & BorderBit) != 0 && id != 0 ? id * PaletteWidth + SlotRelation : -1;

            if (id == 0)
                return (value & AltBit) != 0 ? FalloutSlot(x, y) : -1;

            if ((value & BorderBit) != 0)
            {
                int attack = value & AttackMask;

                if (attack == OutgoingAttackBit)
                    return SlotOutgoingAttack;

                if (attack == IncomingAttackBit)
                    return SlotIncomingAttack;

                if ((value & DefendedBit) != 0)
                    return id * PaletteWidth + (((x ^ y) & 1) != 0 ? SlotDefendedDark : SlotDefendedLight);

                return id * PaletteWidth + SlotBorder;
            }

            return id * PaletteWidth + ((value & AltBit) != 0 ? SlotAlt : SlotTerritory);
        }
    }
}
