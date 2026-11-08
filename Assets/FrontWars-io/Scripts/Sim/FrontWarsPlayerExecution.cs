using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 플레이어 한 명의 매 틱 — 관계 감소 · 병력 · 골드 · 고립 조각 흡수 `[소스] Cq3gr8XO.js:nd=class{`.
    /// <para>스폰 단계에는 안 돈다. 칸이 0 이 되면 골드를 비우고 끝난다.</para>
    /// </summary>
    public sealed class FrontWarsPlayerExecution : IFrontWarsExecution
    {
        /// <summary>조각 검사 주기 (틱) `[소스] ticksPerClusterCalc=20`.</summary>
        private const int TicksPerClusterCalc = 20;

        private readonly FrontWarsPlayer _player;
        private readonly int[] _neighborScratch = new int[4];
        private readonly int[] _diagScratch = new int[8];
        private FrontWarsGame _game;
        private int _lastCalc;

        /// <summary>조각 계산 표시 배열 — 원본처럼 모든 플레이어가 같이 쓴다. 원본 Uint8Array(127에서 되감기) 대신 int 로 계속 올린다 — 표시값이 겹치지 않아 결과는 같다.</summary>
        private static int[] s_marks = new int[0];
        private static int s_stamp;

        public bool IsActive { get; private set; } = true;

        public bool ActiveDuringSpawnPhase
        {
            get { return false; }
        }

        public FrontWarsPlayerExecution(FrontWarsPlayer player)
        {
            _player = player;
        }

        /// <summary>조각 검사 시작 틱을 이름 해시로 흩는다 `[소스] this.lastCalc=t+P(this.player.name())%this.ticksPerClusterCalc`.</summary>
        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;
            _lastCalc = tick + (int)(FrontWarsHash.Of(_player.Name) % TicksPerClusterCalc);
        }

        public void Tick(int tick)
        {
            _player.DecayRelations();

            // 땅에 묶인 건물 — 칸 주인이 없으면 삭제, 남이면 넘긴다(방어초소는 레벨을 하나 내리고 남으면 넘긴다)
            foreach (FrontWarsUnit unit in _player.Units())
            {
                if (unit.Info.TerritoryBound == false)
                    continue;

                FrontWarsPlayer tileOwner = _game.Owner(unit.Tile);

                if (tileOwner == null)
                {
                    unit.Delete();
                    continue;
                }

                if (tileOwner == _player)
                    continue;

                if (unit.Type == EFrontWarsUnit.DefensePost)
                {
                    unit.DecreaseLevel(tileOwner);
                    if (unit.IsActive)
                        tileOwner.CaptureUnit(unit);
                }
                else
                {
                    tileOwner.CaptureUnit(unit);
                }
            }

            if (_player.IsAlive == false)
            {
                _player.RemoveGold(_player.Gold);

                foreach (FrontWarsUnit unit in _player.Units())
                {
                    if (unit.Type != EFrontWarsUnit.AtomBomb && unit.Type != EFrontWarsUnit.HydrogenBomb
                        && unit.Type != EFrontWarsUnit.MIRVWarhead && unit.Type != EFrontWarsUnit.MIRV)
                        unit.Delete();
                }

                IsActive = false;
                return;
            }

            _player.AddTroops(_game.Rules.TroopIncreaseRate(_player));
            _player.AddGold(_game.Rules.GoldAdditionRate(_player));

            foreach (FrontWarsAlliance alliance in _player.Alliances())
            {
                if (alliance.ExpiresAt <= _game.Ticks)
                    alliance.Expire();
            }

            foreach (FrontWarsEmbargo embargo in _player.Embargoes.ToList())
            {
                if (embargo.IsTemporary && _game.Ticks - embargo.CreatedAt > _game.Settings.Config.TemporaryEmbargoDurationTicks)
                    _player.StopEmbargo(embargo.Target);
            }

            if (tick - _lastCalc > TicksPerClusterCalc && _player.LastTileChange > _lastCalc)
            {
                _lastCalc = tick;
                RemoveClusters();
            }
        }

        /// <summary>
        /// 가장 큰 조각은 같은 적에게 둘러싸였을 때만, 나머지 조각은 남에게 둘러싸였으면 흡수된다 `[소스] removeClusters(){`.
        /// </summary>
        private void RemoveClusters()
        {
            // 원본 sort 는 안정 정렬 — 같은 크기면 찾은 순서를 지킨다
            List<List<int>> clusters = CalculateClusters().OrderByDescending(c => c.Count).ToList();

            if (clusters.Count == 0)
                throw new System.InvalidOperationException("No clusters");

            List<int> largest = clusters[0];
            clusters.RemoveAt(0);
            _player.LargestClusterBounds = FrontWarsTileBounds.Of(_game.Map, largest);

            FrontWarsPlayer surrounder = SurroundedBySamePlayer(largest);
            if (surrounder != null && _player.IsFriendly(surrounder) == false)
                RemoveCluster(largest);

            foreach (List<int> cluster in clusters)
            {
                if (IsSurrounded(cluster))
                    RemoveCluster(cluster);
            }
        }

        /// <summary>
        /// 국경 칸을 8방향으로 이은 묶음들 `[소스] calculateClusters(){`. 국경 순서대로 시작하고, 묶음 안은 넓이 우선 순서.
        /// </summary>
        private List<List<int>> CalculateClusters()
        {
            IReadOnlyList<int> border = _player.BorderTiles.Items;
            int size = _game.Map.TileCount;

            if (s_marks.Length < size)
                s_marks = new int[size];

            s_stamp += 2;
            int isBorder = s_stamp;
            int visited = s_stamp + 1;

            for (int i = 0; i < border.Count; i++)
                s_marks[border[i]] = isBorder;

            var clusters = new List<List<int>>();

            for (int b = 0; b < border.Count; b++)
            {
                int start = border[b];
                if (s_marks[start] != isBorder)
                    continue;

                var cluster = new List<int>();
                s_marks[start] = visited;
                cluster.Add(start);

                for (int head = 0; head < cluster.Count; head++)
                {
                    int n = _game.Map.NeighborsWithDiagInto(cluster[head], _diagScratch);
                    for (int k = 0; k < n; k++)
                    {
                        int t = _diagScratch[k];
                        if (s_marks[t] == isBorder)
                        {
                            s_marks[t] = visited;
                            cluster.Add(t);
                        }
                    }
                }

                clusters.Add(cluster);
            }

            return clusters;
        }

        /// <summary>
        /// 조각 둘레가 전부 한 플레이어이고 그 플레이어 국경 범위가 조각 범위를 품으면 그 플레이어 `[소스] surroundedBySamePlayer(e){`.
        /// <para>바다 해안 · 맵 가장자리 · 주인 없는 땅에 닿으면 아니다.</para>
        /// </summary>
        private FrontWarsPlayer SurroundedBySamePlayer(List<int> cluster)
        {
            var owners = new HashSet<int>();
            int me = _player.SmallId;
            FrontWarsTerrainMap map = _game.Map;

            foreach (int tile in cluster)
            {
                if (map.IsOceanShore(tile) || IsOnEdgeOfMap(tile))
                    return null;

                int n = map.NeighborsInto(tile, _neighborScratch);
                for (int k = 0; k < n; k++)
                {
                    int owner = _game.OwnerSmallId(_neighborScratch[k]);
                    if (owner == 0)
                        return null;

                    if (owner != me)
                        owners.Add(owner);
                }

                if (owners.Count != 1)
                    return null;
            }

            if (owners.Count != 1)
                return null;

            FrontWarsPlayer other = _game.PlayerBySmallId(owners.First());
            FrontWarsTileBounds otherBounds = FrontWarsTileBounds.Of(map, other.BorderTiles.Items);

            return otherBounds.Contains(FrontWarsTileBounds.Of(map, cluster)) ? other : null;
        }

        /// <summary>남의 칸이 조각 둘레에 있고 그 칸들 범위가 조각 범위를 품으면 참 `[소스] isSurrounded(e){`. 해안 · 가장자리면 아니다.</summary>
        private bool IsSurrounded(List<int> cluster)
        {
            var enemyTiles = new List<int>();
            var seen = new HashSet<int>();
            int me = _player.SmallId;
            FrontWarsTerrainMap map = _game.Map;

            foreach (int tile in cluster)
            {
                if (map.IsShore(tile) || IsOnEdgeOfMap(tile))
                    return false;

                int n = map.NeighborsInto(tile, _neighborScratch);
                for (int k = 0; k < n; k++)
                {
                    int t = _neighborScratch[k];
                    int owner = _game.OwnerSmallId(t);
                    if (owner != 0 && owner != me && seen.Add(t))
                        enemyTiles.Add(t);
                }
            }

            return enemyTiles.Count != 0
                   && FrontWarsTileBounds.Of(map, enemyTiles).Contains(FrontWarsTileBounds.Of(map, cluster));
        }

        /// <summary>
        /// 조각에서 이어진 내 땅 전부를 가져갈 플레이어에게 넘긴다 `[소스] removeCluster(e){`.
        /// <para>그게 내 땅 전부면 골드도 넘긴다 (정복).</para>
        /// </summary>
        private void RemoveCluster(List<int> cluster)
        {
            int me = _player.SmallId;

            foreach (int t in cluster)
            {
                if (_game.OwnerSmallId(t) != me)
                    return;
            }

            FrontWarsPlayer capturer = GetCapturingPlayer(cluster);

            // 원본은 첫 칸이 0 이면(거짓 값) 그만둔다 `[소스] if(!r)return`
            if (capturer == null || cluster.Count == 0 || cluster[0] == 0)
                return;

            List<int> connected = _game.Bfs(cluster[0], t => _game.OwnerSmallId(t) == me);

            if (_player.NumTilesOwned == connected.Count)
                _game.ConquerPlayer(capturer, _player);

            foreach (int t in connected)
                capturer.Conquer(t);
        }

        /// <summary>
        /// 둘레의 우호 아닌 플레이어 중 나를 치는 공격 병력이 가장 큰 쪽 (먼저 만난 쪽 우선) `[소스] getCapturingPlayer(e){`.
        /// <para>나를 치는 공격이 없으면 아무도 안 가져간다.</para>
        /// </summary>
        private FrontWarsPlayer GetCapturingPlayer(List<int> cluster)
        {
            int me = _player.SmallId;
            var seenOwners = new HashSet<int>();
            var candidates = new List<FrontWarsPlayer>();

            foreach (int tile in cluster)
            {
                int n = _game.Map.NeighborsInto(tile, _neighborScratch);
                for (int k = 0; k < n; k++)
                {
                    int owner = _game.OwnerSmallId(_neighborScratch[k]);
                    if (owner == me || seenOwners.Add(owner) == false)
                        continue;

                    FrontWarsPlayer p = _game.PlayerBySmallId(owner);
                    if (p != null && p.IsFriendly(_player) == false && candidates.Contains(p) == false)
                        candidates.Add(p);
                }
            }

            FrontWarsPlayer best = null;
            double bestTroops = 0;

            foreach (FrontWarsPlayer p in candidates)
            {
                foreach (FrontWarsAttack attack in p.OutgoingAttacks)
                {
                    if (attack.TargetSmallId == me && attack.Troops > bestTroops)
                    {
                        bestTroops = attack.Troops;
                        best = p;
                    }
                }
            }

            return best;
        }

        private bool IsOnEdgeOfMap(int tile)
        {
            FrontWarsTerrainMap map = _game.Map;
            int x = map.X(tile), y = map.Y(tile);
            return x == 0 || x == map.Width - 1 || y == 0 || y == map.Height - 1;
        }
    }
}
