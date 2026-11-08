using System;
using System.Collections.Generic;
using System.Text;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 한 판을 차린다 — 플레이어 id · 네이션 고르기 · 게임 생성 · 시작 실행 넣기 `[소스] Cq3gr8XO.js:async function lg( · ug init(){`.
    ///
    /// <list type="number">
    /// <item>게임 id 해시 시드 난수 하나로 사람 id → 네이션 id 순으로 뽑는다.</item>
    /// <item>게임을 만들면 사람 → 네이션 순으로 플레이어가 생긴다 (작은 id 1 부터).</item>
    /// <item>봇 스폰 실행들 → 네이션 AI 실행들 → (튜토리얼) → 승리 판정 실행 순으로 넣는다.</item>
    /// </list>
    /// </summary>
    public static class FrontWarsGameSetup
    {
        /// <summary>사람 이름에서 원본 1차 거르기(<c>Ft</c>)가 지우고 2차(<c>ym</c>)는 남기는 글자 — 미리 지운다.</summary>
        private const string ClientNameRemovedSymbols = ".-+,^";

        public static FrontWarsGame Create(
            FrontWarsTerrainMap map,
            FrontWarsTerrainMap miniMap,
            FrontWarsGameSettings settings,
            string humanName,
            string humanClientId,
            IReadOnlyList<FrontWarsNationData> manifestNations,
            IReadOnlyList<string> botNamePrefixes,
            IReadOnlyList<string> botNameSuffixes)
        {
            var random = new FrontWarsRandom(FrontWarsHash.Of(settings.GameId));

            var humans = new List<FrontWarsPlayerInfo>
            {
                new FrontWarsPlayerInfo(CleanClientName(humanName), EFrontWarsPlayerType.Human, humanClientId, random.NextId())
            };

            // 사람 대 네이션이면 네이션 수 = 사람 수로 고른다 (시작 설정은 그대로라 네이션 AI 를 켜는 판단은 원래 값) `[소스] e.config.gameMode===b.Team&&e.config.playerTeams===`HumansVsNations`?{...e.config,nations:s.length,disableNPCs:s.length===0}:e.config`
            int? nationCount = settings.IsHumansVsNations ? humans.Count : settings.Nations;
            List<FrontWarsNation> nations = SelectNations(nationCount, manifestNations, random);
            // 축소 맵 설정이면 원본은 축소 맵으로 판을 한다 `[소스] l=e.config.gameMapSize===ee.Compact?a.miniGameMap:a.gameMap`
            FrontWarsTerrainMap playMap = settings.CompactMap ? miniMap : map;
            var game = new FrontWarsGame(playMap, miniMap, settings, humans, nations);
            game.Paths = new FrontWarsPathFactory(game);

            if (settings.Bots > 0)
                game.AddExecutions(new FrontWarsBotSpawner(game, settings.GameId, botNamePrefixes, botNameSuffixes).SpawnBots(settings.Bots));

            if (settings.Nations != 0)
            {
                // 원본은 한 판에 공유 상태를 하나만 만들어 모든 네이션에 넘긴다 `[소스] fakeHumanExecutions(){let e=[],t=new yp;…}`
                var nationShared = new FrontWarsNationSharedState();

                foreach (FrontWarsNation n in game.Nations)
                    game.AddExecution(new FrontWarsNationExecution(settings.GameId, n, settings, nationShared));
            }

            if (settings.Tutorial)
                game.AddExecution(new FrontWarsTutorialExecution(settings.GameId));

            game.AddExecution(new FrontWarsWinCheckExecution());
            return game;
        }

        /// <summary>
        /// 네이션 고르기 `[소스] function sh(e,t,n){`.
        /// <para>null = 맵 네이션 전부(섞지 않음) · 0 = 없음 · 맵 수 이하 = 섞어서 앞에서부터.</para>
        /// <para>맵 수보다 많이 고르면 원본은 국가 목록에서 더 뽑는다 — 그 목록은 아직 안 옮겼다 (단위 14 · 15).</para>
        /// </summary>
        private static List<FrontWarsNation> SelectNations(int? nations, IReadOnlyList<FrontWarsNationData> manifest, FrontWarsRandom random)
        {
            var result = new List<FrontWarsNation>();

            if (nations == 0)
                return result;

            IReadOnlyList<FrontWarsNationData> picked;

            if (nations == null)
            {
                picked = manifest;
            }
            else if (nations.Value <= manifest.Count)
            {
                FrontWarsNationData[] shuffled = random.Shuffle(manifest);
                picked = new ArraySegment<FrontWarsNationData>(shuffled, 0, nations.Value);
            }
            else
            {
                throw new NotSupportedException($"맵 네이션 수({manifest.Count})보다 많은 네이션({nations}) — 추가 국가 목록은 아직 없다");
            }

            foreach (FrontWarsNationData n in picked)
            {
                // ⚠ 원본은 플레이어 정보에 네이션을 안 넘긴다 `[소스] new oe(e.name,E.FakeHuman,null,n.nextID())` —
                // 그래서 시작 병력의 강도 배율(nation?.strength??1)은 늘 1 이다. 강도는 네이션 쪽에만 둔다 [원본 해시 대조로 확인]
                var info = new FrontWarsPlayerInfo(n.Name, EFrontWarsPlayerType.FakeHuman, null, random.NextId());
                result.Add(new FrontWarsNation(info, n.X, n.Y, n.Strength));
            }

            return result;
        }

        /// <summary>
        /// 사람 이름 1차 거르기 `[소스] function Ft(e){…replace(/[^\p{L}\p{N}\s\p{Emoji}\p{Emoji_Component}[\]_]/gu,``)}`.
        /// <para>
        /// 원본은 글자 · 숫자 · 공백 · 이모지 · []_ 만 남긴다. 뒤이어 플레이어 이름 거르기가 좁은 글자만 남기므로,
        /// 그 좁은 글자 중 1차에서 지워지는 <c>. - + , ^</c> 만 지우면 결과가 같다.
        /// </para>
        /// </summary>
        public static string CleanClientName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            var sb = new StringBuilder(name.Length);

            foreach (char c in name)
            {
                if (ClientNameRemovedSymbols.IndexOf(c) < 0)
                    sb.Append(c);
            }

            return sb.ToString();
        }
    }

    /// <summary>
    /// 봇 자리 뽑기 `[소스] Cq3gr8XO.js:ud=class{`. 게임 id 해시 시드 난수로
    /// 이름(앞말 · 뒷말) → 칸(x · y) → 조건 맞으면 id 순으로 뽑는다.
    /// <para>물이거나 이미 뽑은 봇과 맨해튼 거리 30 미만이면 버리고, 1만 번 넘게 버리면 그만둔다.</para>
    /// </summary>
    public sealed class FrontWarsBotSpawner
    {
        private const int MaxRetries = 10000;

        private readonly FrontWarsGame _game;
        private readonly FrontWarsRandom _random;
        private readonly IReadOnlyList<string> _prefixes;
        private readonly IReadOnlyList<string> _suffixes;
        private readonly int _minDistance;
        private readonly List<FrontWarsSpawnExecution> _bots = new List<FrontWarsSpawnExecution>();
        private readonly List<int> _botTiles = new List<int>();

        public FrontWarsBotSpawner(FrontWarsGame game, string gameId, IReadOnlyList<string> prefixes, IReadOnlyList<string> suffixes)
        {
            _game = game;
            _random = new FrontWarsRandom(FrontWarsHash.Of(gameId));
            _prefixes = prefixes;
            _suffixes = suffixes;
            _minDistance = game.Settings.Config.BotSpawnMinDistance;
        }

        public List<FrontWarsSpawnExecution> SpawnBots(int count)
        {
            int retries = 0;

            while (_bots.Count < count)
            {
                if (retries > MaxRetries)
                    return _bots;

                string name = RandomBotName();
                FrontWarsSpawnExecution bot = SpawnBot(name, out int tile);

                if (bot == null)
                {
                    retries++;
                }
                else
                {
                    _bots.Add(bot);
                    _botTiles.Add(tile);
                }
            }

            return _bots;
        }

        private FrontWarsSpawnExecution SpawnBot(string name, out int tile)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = _random.NextInt(0, map.Width);
            int y = _random.NextInt(0, map.Height);
            tile = map.Ref(x, y);

            if (map.IsLand(tile) == false)
                return null;

            foreach (int t in _botTiles)
            {
                if (map.ManhattanDistance(t, tile) < _minDistance)
                    return null;
            }

            var info = new FrontWarsPlayerInfo(name, EFrontWarsPlayerType.Bot, null, _random.NextId());
            return new FrontWarsSpawnExecution(info, tile);
        }

        private string RandomBotName()
        {
            int a = _random.NextInt(0, _prefixes.Count);
            int b = _random.NextInt(0, _suffixes.Count);
            return $"{_prefixes[a]} {_suffixes[b]}";
        }
    }
}
