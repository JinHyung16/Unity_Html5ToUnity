using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 모든 네이션이 같이 쓰는 상태 `[소스] Cq3gr8XO.js:yp=class{constructor(){this.recentMirvTargets=new Map`.
    ///
    /// <para>
    /// 원본은 네이션 실행을 만들 때 한 판에 하나만 만들어 모든 네이션에 넘긴다 `[소스] fakeHumanExecutions(){let e=[],t=new yp`.
    /// 그래서 한 네이션이 MIRV 를 쏜 대상은 다른 네이션도 300틱 동안 피한다.
    /// </para>
    /// </summary>
    public sealed class FrontWarsNationSharedState
    {
        private static readonly List<FrontWarsUnit> NoTransports = new List<FrontWarsUnit>();

        private readonly Dictionary<int, List<FrontWarsUnit>> _incomingTransportsByTarget = new Dictionary<int, List<FrontWarsUnit>>();
        private int _incomingTransportsTick = -1;

        /// <summary>플레이어 id → 마지막으로 MIRV 를 쏜 틱. 조회만 하므로 순서는 필요 없다.</summary>
        public Dictionary<string, int> RecentMirvTargets { get; } = new Dictionary<string, int>();

        /// <summary>
        /// 그 작은 id 의 땅으로 향하는 남의 수송선 `[소스] incomingTransportsFor(e,t){`.
        /// <para>틱마다 한 번만 다시 모은다 — 목표 칸이 있고 · 살아 있고 · 후퇴 중이 아니고 · 제 땅으로 가는 배가 아닌 것.</para>
        /// </summary>
        public IReadOnlyList<FrontWarsUnit> IncomingTransportsFor(FrontWarsGame game, int targetSmallId)
        {
            if (game.Ticks != _incomingTransportsTick)
            {
                _incomingTransportsTick = game.Ticks;
                _incomingTransportsByTarget.Clear();

                foreach (FrontWarsUnit ship in game.AllUnits(EFrontWarsUnit.TransportShip))
                {
                    int target = ship.TargetTile;
                    if (target < 0 || ship.IsActive == false || ship.Retreating)
                        continue;

                    int ownerId = game.OwnerSmallId(target);
                    if (ship.Owner.SmallId == ownerId)
                        continue;

                    if (_incomingTransportsByTarget.TryGetValue(ownerId, out List<FrontWarsUnit> list) == false)
                    {
                        list = new List<FrontWarsUnit>();
                        _incomingTransportsByTarget[ownerId] = list;
                    }

                    list.Add(ship);
                }
            }

            return _incomingTransportsByTarget.TryGetValue(targetSmallId, out List<FrontWarsUnit> found) ? found : NoTransports;
        }
    }
}
