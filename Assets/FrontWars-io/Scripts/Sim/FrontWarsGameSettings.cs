using System;
using System.Collections.Generic;
using System.Linq;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>난이도 `[소스] Cq3gr8XO.js:d.Easy · d.Medium · d.Hard · d.Impossible`.</summary>
    public enum EFrontWarsDifficulty
    {
        Easy,
        Medium,
        Hard,
        Impossible
    }

    /// <summary>경제 방식 `[소스] Ca.Classic · Ca.Fast`.</summary>
    public enum EFrontWarsEconomyMode
    {
        Classic,
        Fast
    }

    /// <summary>게임 방식 — 개인전 · 팀전 `[소스] Cq3gr8XO.js:e.FFA=`Free For All`,e.Team=`Team``.</summary>
    public enum EFrontWarsGameMode
    {
        FFA,
        Team
    }

    /// <summary>
    /// 한 판의 설정 — 원본 싱글 시작 설정(<c>gameConfig</c>)에 밸런스 표 행을 붙인 것.
    ///
    /// <para>
    /// 시뮬은 DataManager 를 모른다. 부르는 쪽(게임 흐름 · 검사기)이 표 행을 꽂아 넘긴다 — 그래서 재생 없이 검사에서 돈다.
    /// 설정값 기본은 원본 싱글 기본값이다 (`00_게임개요.md` 「싱글 기본값」).
    /// </para>
    /// </summary>
    public sealed class FrontWarsGameSettings
    {
        /// <summary>게임 id — 게임 시드 난수 · 봇 · 네이션 난수의 시드.</summary>
        public string GameId { get; set; }

        public EFrontWarsDifficulty Difficulty { get; set; } = EFrontWarsDifficulty.Medium;

        public EFrontWarsEconomyMode EconomyMode { get; set; } = EFrontWarsEconomyMode.Classic;

        /// <summary>개인전 · 팀전 `[소스] gameMode:b.FFA`.</summary>
        public EFrontWarsGameMode GameMode { get; set; } = EFrontWarsGameMode.FFA;

        /// <summary>
        /// 팀 나누기 — 개인전이면 null. 팀전이면 팀 수 "2"~"7"(더 큰 수도 된다) · "Duos" · "Trios" · "Quads" · "HumansVsNations" `[소스] playerTeams(){return this._gameConfig.playerTeams??0}`.
        /// </summary>
        public string PlayerTeams { get; set; }

        /// <summary>팀전인가.</summary>
        public bool IsTeamMode
        {
            get { return GameMode == EFrontWarsGameMode.Team; }
        }

        /// <summary>사람 대 네이션 팀전인가 `[소스] playerTeams===`HumansVsNations``.</summary>
        public bool IsHumansVsNations
        {
            get { return IsTeamMode && PlayerTeams == FrontWarsGame.HumansVsNations; }
        }

        /// <summary>봇 수 `[소스] bots:400`.</summary>
        public int Bots { get; set; } = 400;

        /// <summary>네이션 수. null = 맵 기본(전부) `[소스] nationsFollowsDefault`. 0 = 끔.</summary>
        public int? Nations { get; set; }

        public bool RandomSpawn { get; set; }

        /// <summary>튜토리얼 판 — 튜토리얼 도우미 실행이 붙는다.</summary>
        public bool Tutorial { get; set; }
        public bool DonateGold { get; set; } = true;
        public bool DonateTroops { get; set; } = true;
        public bool DisableAlliances { get; set; }
        public bool InstantBuild { get; set; }
        public bool WaterNukes { get; set; }

        /// <summary>끈 유닛 종류 (설정 창 「유닛 끄기」).</summary>
        public HashSet<EFrontWarsUnit> DisabledUnits { get; set; } = new HashSet<EFrontWarsUnit>();
        public bool InfiniteTroops { get; set; }
        public bool InfiniteGold { get; set; }
        public bool CompactMap { get; set; }

        /// <summary>시작 병력 설정 (켰을 때만). null = 기본.</summary>
        public int? StartingTroops { get; set; }

        /// <summary>골드 배율 (켰을 때만). null = 1 `[소스] goldMultiplier(){return this._gameConfig.goldMultiplier??1}`.</summary>
        public double? GoldMultiplier { get; set; }

        /// <summary>시작 골드 백만 단위 (켰을 때만). null = 0.</summary>
        public long? StartingGoldMillions { get; set; }

        /// <summary>제한 시간(분, 켰을 때만). null = 없음.</summary>
        public int? GameLengthMinutes { get; set; }

        public FrontWarsConfigData Config { get; set; }
        public FrontWarsDifficultyData DifficultyRow { get; set; }

        /// <summary>유닛 표 — 종류 순서(<see cref="EFrontWarsUnit"/>)대로.</summary>
        public IReadOnlyList<FrontWarsUnitData> UnitRows { get; set; }

        public FrontWarsUnitData Unit(EFrontWarsUnit type)
        {
            return UnitRows[(int)type];
        }

        /// <summary>핵 반경 표 — 코드(AtomBomb · HydrogenBomb · MIRVWarhead)로 찾는다.</summary>
        public IReadOnlyDictionary<EFrontWarsUnit, FrontWarsNukeData> NukeRows { get; set; }

        public FrontWarsNukeData Nuke(EFrontWarsUnit type)
        {
            if (NukeRows.TryGetValue(type, out FrontWarsNukeData row) == false)
                throw new InvalidOperationException($"핵 표에 {type} 행이 없다");

            return row;
        }

        /// <summary>땅 지형 공격 값 — Plains · Highland · Mountain.</summary>
        public FrontWarsTerrainData Plains { get; set; }
        public FrontWarsTerrainData Highland { get; set; }
        public FrontWarsTerrainData Mountain { get; set; }

        /// <summary>성격 표 — 뽑기 문턱(PickBelow) 오름차순으로 넣는다.</summary>
        public IReadOnlyList<FrontWarsPersonalityData> Personalities { get; set; }

        /// <summary>스폰 단계 턴 수를 바꾼다 (튜토리얼 70). null = 표의 싱글 값.</summary>
        public int? SpawnPhaseTurnsOverride { get; set; }

        public int SpawnPhaseTurns
        {
            get { return SpawnPhaseTurnsOverride ?? Config.SpawnPhaseTurnsSingleplayer; }
        }

        /// <summary>이기는 땅 % — 팀전 95 · 개인전 80 `[소스] percentageTilesOwnedToWin(){return this._gameConfig.gameMode===b.Team?95:80}`.</summary>
        public double WinPercent
        {
            get { return IsTeamMode ? Config.WinPercentTeam : Config.WinPercentFFA; }
        }

        /// <summary>표 컨테이너에서 한 번에 꽂는다.</summary>
        public static FrontWarsGameSettings FromTables(
            FrontWarsConfigData config,
            IEnumerable<FrontWarsDifficultyData> difficulties,
            IEnumerable<FrontWarsPersonalityData> personalities,
            IEnumerable<FrontWarsTerrainData> terrains,
            IEnumerable<FrontWarsUnitData> units,
            IEnumerable<FrontWarsNukeData> nukes,
            EFrontWarsDifficulty difficulty,
            string gameId)
        {
            var nukeRows = new Dictionary<EFrontWarsUnit, FrontWarsNukeData>();

            foreach (FrontWarsNukeData n in nukes)
                nukeRows[(EFrontWarsUnit)Enum.Parse(typeof(EFrontWarsUnit), n.Code)] = n;

            var unitRows = new FrontWarsUnitData[Enum.GetValues(typeof(EFrontWarsUnit)).Length];

            foreach (FrontWarsUnitData u in units)
            {
                if (Enum.TryParse(u.Code, out EFrontWarsUnit t) == false)
                    throw new InvalidOperationException($"유닛 코드가 enum 에 없다: {u.Code}");

                unitRows[(int)t] = u;
            }

            if (unitRows.Any(u => u == null))
                throw new InvalidOperationException("유닛 표에 빠진 종류가 있다");

            string code = difficulty.ToString();
            FrontWarsDifficultyData row = difficulties.FirstOrDefault(d => d.Code == code);

            if (row == null)
                throw new InvalidOperationException($"난이도 행이 없다: {code}");

            List<FrontWarsTerrainData> terrainList = terrains.ToList();

            FrontWarsTerrainData Terrain(string c)
            {
                return terrainList.FirstOrDefault(t => t.Code == c) ?? throw new InvalidOperationException($"지형 행이 없다: {c}");
            }

            return new FrontWarsGameSettings
            {
                GameId = gameId,
                Difficulty = difficulty,
                Config = config,
                DifficultyRow = row,
                Personalities = personalities.OrderBy(p => p.PickBelow).ToList(),
                UnitRows = unitRows,
                NukeRows = nukeRows,
                Plains = Terrain("Plains"),
                Highland = Terrain("Highland"),
                Mountain = Terrain("Mountain")
            };
        }
    }
}
