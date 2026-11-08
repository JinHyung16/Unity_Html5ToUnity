using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 진행 중인 공격 하나 — 병력 · 앞줄(국경) 칸 · 후퇴 상태 `[소스] Cq3gr8XO.js:bm=class{`.
    /// <para>공격 실행이 매 틱 병력을 깎고 칸을 먹는다. HUD 의 공격 목록 · 화살표 위치(<see cref="AveragePosition"/>)가 이것을 읽는다.</para>
    /// </summary>
    public sealed class FrontWarsAttack
    {
        private readonly HashSet<int> _border = new HashSet<int>();

        public string Id { get; }

        /// <summary>대상. null = 주인 없는 땅.</summary>
        public FrontWarsPlayer Target { get; }

        public FrontWarsPlayer Attacker { get; }

        /// <summary>남은 병력 — 원본이 숫자(double)로 들고 깎는다.</summary>
        public double Troops { get; set; }

        /// <summary>배로 내린 공격의 시작 칸. 땅 공격이면 −1.</summary>
        public int SourceTile { get; }

        public bool IsActive { get; private set; } = true;

        /// <summary>후퇴 명령을 받았다 — 공격이 멈춘다.</summary>
        public bool Retreating { get; private set; }

        /// <summary>후퇴를 실행한다 — 다음 틱에 병력을 돌려준다.</summary>
        public bool Retreated { get; private set; }

        public int BorderSize { get; private set; }

        public int TargetSmallId
        {
            get { return Target?.SmallId ?? 0; }
        }

        public FrontWarsAttack(string id, FrontWarsPlayer target, FrontWarsPlayer attacker, double troops, int sourceTile)
        {
            Id = id;
            Target = target;
            Attacker = attacker;
            Troops = troops;
            SourceTile = sourceTile;
        }

        /// <summary>양쪽 목록에서 빼고 끝낸다.</summary>
        public void Delete()
        {
            Target?.IncomingAttacks.Remove(this);
            Attacker.OutgoingAttacks.Remove(this);
            IsActive = false;
        }

        public void OrderRetreat()
        {
            Retreating = true;
        }

        public void ExecuteRetreat()
        {
            Retreated = true;
        }

        public void ClearBorder()
        {
            BorderSize = 0;
            _border.Clear();
        }

        public void AddBorderTile(int tile)
        {
            if (_border.Add(tile))
                BorderSize++;
        }

        public void RemoveBorderTile(int tile)
        {
            if (_border.Remove(tile))
                BorderSize--;
        }

        public IEnumerable<int> BorderTiles
        {
            get { return _border; }
        }

        /// <summary>앞줄 칸 평균 좌표. 앞줄이 비면 시작 칸, 그것도 없으면 false `[소스] averagePosition(){`.</summary>
        public bool AveragePosition(FrontWarsTerrainMap map, out double x, out double y)
        {
            x = 0;
            y = 0;

            if (BorderSize == 0)
            {
                if (SourceTile < 0)
                    return false;

                x = map.X(SourceTile);
                y = map.Y(SourceTile);
                return true;
            }

            foreach (int t in _border)
            {
                x += map.X(t);
                y += map.Y(t);
            }

            x /= BorderSize;
            y /= BorderSize;
            return true;
        }
    }
}
