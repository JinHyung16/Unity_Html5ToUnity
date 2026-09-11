using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 로비를 돌린다 — <b>시뮬은 <see cref="UndeadLobbySimulation"/>, 그림은 <see cref="UndeadLobbyView"/></b>.
    ///
    /// <para>
    /// ★ 로비는 <b>전투와 «같은 씬»</b>에 있다. 들어가면 전투를 멈추고 로비를 켠다.
    /// ⚠ 안 멈추면 <b>보이지 않는 전투가 계속 돈다</b> — 적이 쌓이고 시간이 흐른다.
    /// </para>
    /// </summary>
    public sealed class UndeadLobbyManager : BaseManager, IGameUpdate
    {
        private UndeadLobbySimulation _simulation;
        private UndeadLobbyView _view;
        private UndeadSimulation _run;

        /// <summary>지금 로비에 있나.</summary>
        public bool IsInside { get; private set; }

        public UndeadLobbySimulation Simulation
        {
            get { return _simulation; }
        }

        /// <summary>이동 입력을 어디서 받나 — 전투와 <b>같은 소스</b>를 쓴다.</summary>
        public Func<UndeadVec2> MoveInputSource { get; set; }

        /// <summary>모달이 떠 있나 — 떠 있으면 <b>수령이 안 돈다</b> [소스 <c>isModalOpen</c>].</summary>
        public Func<bool> IsModalOpen { get; set; }

        /// <summary>포털에 들어갔다 — 인자는 바이옴 번호.</summary>
        public event Action<int> OnEnterBiome;

        /// <summary>보상을 받았다 — 과제 인덱스와 받은 스킬.</summary>
        public event Action<int, EUndeadSkill> OnRewardClaimed;

        /// <summary>
        /// 맵·아트를 받아 로비를 세운다. <b>판이 시작할 때 한 번</b> 부른다.
        /// </summary>
        public void Setup(UndeadSimulation run, UndeadLobbyView view, UndeadLobbyMapDataContainer maps,
                          double heroMoveSpeed)
        {
            _run = run;
            _view = view;

            if (maps == null || maps.Map == null)
            {
                Log.Error("로비 맵이 없다 — 로비를 못 세운다");
                return;
            }

            _simulation = new UndeadLobbySimulation(run);
            _simulation.OnEnterBiome += HandleEnterBiome;
            _simulation.OnClaim += HandleClaim;

            UndeadLobbyMapData map = maps.Map;

            _simulation.Setup(PointOf(maps, "hero"),
                              PointOf(maps, "lobbyNpcTask1Anchor"),
                              PointOf(maps, "lobbyNpcTask2Anchor"),
                              PointOf(maps, "lobbyPortalBiome1Anchor"),
                              PointOf(maps, "lobbyPortalBiome2Anchor"),
                              BuildBlocked(maps, map),
                              heroMoveSpeed);
        }

        private static UndeadVec2 PointOf(UndeadLobbyMapDataContainer maps, string name)
        {
            UndeadLobbyPoint point = maps.Point(name);
            return point == null ? UndeadVec2.Zero : new UndeadVec2(point.X, point.Y);
        }

        /// <summary>
        /// 막힘 사각형 — <c>$colliders</c> 레이어의 <b>칸마다 한 개</b>다.
        /// <para>⚠ 칸 좌표는 «타일»이고 시뮬은 «월드 px» 를 쓴다 — 타일 크기를 곱한다.</para>
        /// </summary>
        private static List<UndeadRect> BuildBlocked(UndeadLobbyMapDataContainer maps, UndeadLobbyMapData map)
        {
            var list = new List<UndeadRect>(96);
            UndeadLobbyLayer layer = maps.Layer(UndeadLobbyMapData.ColliderLayer);

            if (layer == null || layer.Cells == null)
            {
                Log.Error("로비에 막힘 레이어가 없다 — 벽을 통과한다");
                return list;
            }

            foreach (KeyValuePair<string, int> pair in layer.Cells)
            {
                if (int.TryParse(pair.Key, out int index) == false)
                    continue;

                int tx = index % map.Width;
                int ty = index / map.Width;
                list.Add(new UndeadRect(tx * map.TileWidth, ty * map.TileHeight, map.TileWidth, map.TileHeight));
            }

            return list;
        }

        /// <summary>로비로 들어간다 — 화면 전이가 부른다.</summary>
        public void Enter()
        {
            IsInside = true;
            _view?.SetVisible(true);
        }

        /// <summary>로비에서 나간다.</summary>
        public void Leave()
        {
            IsInside = false;
            _view?.SetVisible(false);
        }

        public void OnUpdate(float deltaTime)
        {
            if (IsInside == false || _simulation == null)
                return;

            UndeadVec2 input = MoveInputSource != null ? MoveInputSource() : UndeadVec2.Zero;
            bool modal = IsModalOpen != null && IsModalOpen();

            _simulation.Step(deltaTime, input, modal);
            _view?.Render();
        }

        private void HandleEnterBiome(int biome)
        {
            OnEnterBiome?.Invoke(biome);
        }

        private void HandleClaim(int taskIndex)
        {
            if (_run == null || taskIndex < 0)
                return;

            EUndeadSkill reward = _run.TaskDefinitions[taskIndex].RewardSkill;

            if (_run.ClaimTaskReward(taskIndex))
                OnRewardClaimed?.Invoke(taskIndex, reward);
        }

        protected override void OnDispose()
        {
            if (_simulation != null)
            {
                _simulation.OnEnterBiome -= HandleEnterBiome;
                _simulation.OnClaim -= HandleClaim;
            }

            OnEnterBiome = null;
            OnRewardClaimed = null;
        }
    }
}
