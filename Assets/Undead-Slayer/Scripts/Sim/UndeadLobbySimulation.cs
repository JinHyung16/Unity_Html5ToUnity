using System;
using System.Collections.Generic;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// <b>로비</b> — 전투 밖의 작은 방. 히어로가 걸어 다니며 <b>과제 NPC 둘</b>에게서 보상을 받고
    /// <b>포털</b>로 바이옴에 들어간다 [소스 <c>lobby</c> 씬].
    ///
    /// <para>
    /// ★ <b>전투 시뮬과 «다른 시뮬»이다.</b> 적도 총알도 없고, 막힘은 TMX <c>$colliders</c> 레이어가 준다.
    /// 한 파일에 합치면 전투 루프가 로비 상태를 들고 다니게 된다.
    /// </para>
    ///
    /// <para>
    /// ★ 로비는 <b>처음부터 열리지 않는다</b> [소스 <c>isLobbyUnlocked</c>] —
    /// <b>부활 없이 한 번 죽고 나서야</b> 열린다. 그전에는 바로 바이옴 1 로 들어간다.
    /// </para>
    /// </summary>
    public sealed class UndeadLobbySimulation
    {
        /// <summary>NPC 앞에 머물러 보상을 받기까지 (초) [소스 <c>Yu = 2000</c>].</summary>
        public const double ClaimSeconds = 2.0;

        /// <summary>NPC 상호작용 반경 [소스 <c>interactionRadius: 72</c>].</summary>
        public const double NpcRadius = 72.0;

        /// <summary>포털 근접 반경 [소스 <c>interactionRadius: 64</c>].</summary>
        public const double PortalRadius = 64.0;

        /// <summary>포털 «문간» — 이 안에 들어가야 진입이다 [소스 <c>Du</c>].</summary>
        public static readonly UndeadRect Doorway = new UndeadRect(-28.0, -40.0, 56.0, 44.0);

        /// <summary>포털 앞을 막는 띠 [소스 <c>Lu</c>] — 문간 말고는 못 지나간다.</summary>
        public static readonly UndeadRect PortalSolid = new UndeadRect(-56.0, -50.0, 112.0, 20.0);

        /// <summary>두 번째 바이옴이 열리는 조건 [소스 <c>getClaimedTaskCount() &gt;= 2</c>].</summary>
        public const int Biome2ClaimedTasks = 2;

        /// <summary>포털이 <b>열릴 때</b> 한 번 도는 연출 길이 (ms) [소스 <c>Pu = 800</c>].</summary>
        public const double PortalOpenFeedbackMs = 800.0;

        public struct Npc
        {
            public UndeadVec2 Position;

            /// <summary>이 NPC 가 들고 있는 과제 — <c>-1</c> 이면 없다.</summary>
            public int TaskIndex;

            public bool InRange;

            /// <summary>수령 링이 얼마나 찼나 0~1 — <b>완료 상태에서 머무는 동안만</b> 찬다.</summary>
            public double ClaimProgress;

            public bool ClaimTriggered;
        }

        public struct Portal
        {
            public UndeadVec2 Position;
            public int Biome;
            public bool Open;
            public bool InProximity;
            public bool InDoorway;

            /// <summary>
            /// 열리는 연출이 얼마나 지났나 (ms) — <b><see cref="PortalOpenFeedbackMs"/> 면 «끝난 것»</b>이다
            /// [소스 <c>openingFeedbackElapsedMs</c>].
            /// <para>⚠ 처음 세울 때부터 «끝난 값»으로 둔다 — 안 그러면 <b>로비에 들어서자마자</b> 연출이 돈다.</para>
            /// </summary>
            public double OpeningFeedbackMs;

            /// <summary>보상 팝업이 떠 있어 <b>연출을 미뤄 둔</b> 상태 [소스 <c>hasPendingOpenFeedback</c>].</summary>
            public bool PendingOpenFeedback;

            /// <summary>연출 진행 0~1.</summary>
            public double OpeningProgress
            {
                get { return Math.Min(1.0, OpeningFeedbackMs / PortalOpenFeedbackMs); }
            }

            /// <summary>지금 연출이 도는 중인가 [소스 <c>isOpeningFeedbackActive</c>].</summary>
            public bool IsOpeningFeedbackActive
            {
                get { return OpeningFeedbackMs < PortalOpenFeedbackMs; }
            }
        }

        private readonly Npc[] _npcs = new Npc[UndeadSimulation.TaskNpcSlots];
        private readonly Portal[] _portals = new Portal[2];
        private readonly List<UndeadRect> _blocked = new List<UndeadRect>(128);
        private readonly UndeadSimulation _run;

        private double _moveSpeed;
        private bool _entered;

        public UndeadLobbySimulation(UndeadSimulation run)
        {
            _run = run;
        }

        public UndeadVec2 HeroPosition { get; private set; }

        public bool HeroFacingLeft { get; private set; }

        public bool HeroMoving { get; private set; }

        public double HeroAnimFrame { get; private set; }

        public IReadOnlyList<Npc> Npcs
        {
            get { return _npcs; }
        }

        public IReadOnlyList<Portal> Portals
        {
            get { return _portals; }
        }

        /// <summary>포털에 들어갔다 — 인자는 바이옴 번호 [소스 <c>lobbyExitFade.start(biome)</c>].</summary>
        public event Action<int> OnEnterBiome;

        /// <summary>NPC 앞에서 링이 다 찼다 — 과제 인덱스.</summary>
        public event Action<int> OnClaim;

        /// <summary>
        /// 맵을 받아 로비를 세운다.
        /// <para>⚠ <paramref name="blocked"/> 는 <b>월드 좌표 사각형</b>이다 — 타일 좌표가 아니다.</para>
        /// </summary>
        public void Setup(UndeadVec2 heroStart, UndeadVec2 npc1, UndeadVec2 npc2,
                          UndeadVec2 portal1, UndeadVec2 portal2,
                          IReadOnlyList<UndeadRect> blocked, double moveSpeed)
        {
            HeroPosition = heroStart;
            HeroFacingLeft = false;
            HeroMoving = false;
            HeroAnimFrame = 0.0;
            _moveSpeed = moveSpeed;
            _entered = false;

            _npcs[0] = new Npc { Position = npc1, TaskIndex = -1 };
            _npcs[1] = new Npc { Position = npc2, TaskIndex = -1 };

            // ⚠ 처음 여는 것은 «연출 없이»다 [소스 setOpen(..., false)] — 연출을 끝난 값으로 둔다
            _portals[0] = new Portal
            {
                Position = portal1, Biome = 1, Open = true, OpeningFeedbackMs = PortalOpenFeedbackMs,
            };

            _portals[1] = new Portal
            {
                Position = portal2, Biome = 2, Open = _run != null && _run.ClaimedTaskCount >= Biome2ClaimedTasks,
                OpeningFeedbackMs = PortalOpenFeedbackMs,
            };

            _blocked.Clear();

            for (int i = 0; blocked != null && i < blocked.Count; i++)
                _blocked.Add(blocked[i]);

            // 포털 앞 띠도 막힘이다 — 문간으로만 지난다
            for (int i = 0; i < _portals.Length; i++)
                _blocked.Add(PortalSolid.Offset(_portals[i].Position));
        }

        /// <summary>
        /// 한 프레임.
        /// <para>⚠ <paramref name="modalOpen"/> 이면 <b>수령이 안 돈다</b> [소스 <c>!e.isModalOpen</c>].</para>
        /// </summary>
        public void Step(double dt, UndeadVec2 moveInput, bool modalOpen)
        {
            if (dt <= 0.0)
                return;

            StepHero(dt, moveInput);
            SyncTasks();
            StepNpcs(dt, modalOpen);
            StepPortals(dt, modalOpen);
        }

        private void StepHero(double dt, UndeadVec2 moveInput)
        {
            UndeadVec2 dir = moveInput.Normalized;
            HeroMoving = dir.X != 0.0 || dir.Y != 0.0;

            if (dir.X < 0.0)
                HeroFacingLeft = true;
            else if (dir.X > 0.0)
                HeroFacingLeft = false;

            HeroAnimFrame += UndeadLobbyAnimationFps * dt;

            if (HeroMoving == false)
                return;

            // ★ 막힌 축만 되돌린다 — 벽을 따라 미끄러진다 (전투와 같은 규칙)
            double nx = HeroPosition.X + dir.X * _moveSpeed * dt;
            double ny = HeroPosition.Y + dir.Y * _moveSpeed * dt;

            if (IsBlocked(new UndeadVec2(nx, HeroPosition.Y)) == false)
                HeroPosition = new UndeadVec2(nx, HeroPosition.Y);

            if (IsBlocked(new UndeadVec2(HeroPosition.X, ny)) == false)
                HeroPosition = new UndeadVec2(HeroPosition.X, ny);
        }

        /// <summary>히어로 애니 속도 — 전투와 같다 [소스 12fps].</summary>
        private const double UndeadLobbyAnimationFps = 12.0;

        private bool IsBlocked(UndeadVec2 at)
        {
            for (int i = 0; i < _blocked.Count; i++)
            {
                if (_blocked[i].Contains(at))
                    return true;
            }

            return false;
        }

        /// <summary>NPC 가 들고 있는 과제를 <b>전투 시뮬의 슬롯</b>에서 가져온다 — 두 벌로 두지 않는다.</summary>
        private void SyncTasks()
        {
            for (int i = 0; i < _npcs.Length; i++)
                _npcs[i].TaskIndex = _run != null ? _run.TaskInSlot(i) : -1;
        }

        private void StepNpcs(double dt, bool modalOpen)
        {
            for (int i = 0; i < _npcs.Length; i++)
            {
                double dx = HeroPosition.X - _npcs[i].Position.X;
                double dy = HeroPosition.Y - _npcs[i].Position.Y;
                _npcs[i].InRange = dx * dx + dy * dy <= NpcRadius * NpcRadius;

                bool completed = _npcs[i].TaskIndex >= 0 && _run != null
                                 && _run.TaskRecords[_npcs[i].TaskIndex].Status == EUndeadTaskStatus.Completed;

                // ⚠ 완료가 «아니면» 링을 통째로 되돌린다 [소스 — 진행이 남지 않는다]
                if (completed == false || _npcs[i].InRange == false || modalOpen)
                {
                    _npcs[i].ClaimProgress = 0.0;
                    _npcs[i].ClaimTriggered = false;
                    continue;
                }

                if (_npcs[i].ClaimTriggered)
                    continue;

                _npcs[i].ClaimProgress = Math.Min(1.0, _npcs[i].ClaimProgress + dt / ClaimSeconds);

                if (_npcs[i].ClaimProgress < 1.0)
                    continue;

                _npcs[i].ClaimTriggered = true;
                OnClaim?.Invoke(_npcs[i].TaskIndex);
            }
        }

        /// <summary>
        /// 포털.
        ///
        /// <para>
        /// ★ <b>「열렸다」와 「열리는 연출」은 다른 것</b>이다 [소스 <c>setOpen(open, withFeedback)</c>] —
        /// 로비에 들어설 때 이미 열려 있던 포털은 <b>연출 없이</b> 열린 그림으로 서 있고,
        /// 눈앞에서 조건이 채워졌을 때만 800ms 짜리 연출이 돈다.
        /// </para>
        ///
        /// <para>
        /// ⚠ 조건이 채워지는 순간은 <b>보상 팝업이 떠 있는 순간</b>이다 — 그 뒤에서 연출을 돌리면 아무도 못 본다.
        /// 그래서 <b>팝업이 닫힐 때까지 미뤄 둔다</b> [소스 <c>hasPendingOpenFeedback</c>].
        /// </para>
        /// </summary>
        private void StepPortals(double dt, bool modalOpen)
        {
            int claimed = _run != null ? _run.ClaimedTaskCount : 0;

            for (int i = 0; i < _portals.Length; i++)
            {
                bool shouldOpen = _portals[i].Biome == 1 || claimed >= Biome2ClaimedTasks;

                if (shouldOpen && _portals[i].Open == false)
                {
                    _portals[i].Open = true;

                    if (modalOpen)
                        _portals[i].PendingOpenFeedback = true;
                    else
                        _portals[i].OpeningFeedbackMs = 0.0;
                }
                else if (_portals[i].PendingOpenFeedback && modalOpen == false)
                {
                    _portals[i].PendingOpenFeedback = false;
                    _portals[i].OpeningFeedbackMs = 0.0;
                }

                if (_portals[i].OpeningFeedbackMs < PortalOpenFeedbackMs)
                {
                    _portals[i].OpeningFeedbackMs =
                        Math.Min(PortalOpenFeedbackMs, _portals[i].OpeningFeedbackMs + dt * 1000.0);
                }

                double dx = HeroPosition.X - _portals[i].Position.X;
                double dy = HeroPosition.Y - _portals[i].Position.Y;
                _portals[i].InProximity = dx * dx + dy * dy <= PortalRadius * PortalRadius;
                _portals[i].InDoorway = Doorway.Offset(_portals[i].Position).Contains(HeroPosition);

                // ★ 한 번 들어가면 «잠긴다» [소스 isActivationLocked] — 페이드 중에 두 번 안 들어간다
                if (_entered || _portals[i].Open == false || _portals[i].InDoorway == false)
                    continue;

                _entered = true;
                OnEnterBiome?.Invoke(_portals[i].Biome);
            }
        }
    }

    /// <summary>월드 좌표 사각형 — 로비의 막힘·문간에 쓴다.</summary>
    public readonly struct UndeadRect
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Width;
        public readonly double Height;

        public UndeadRect(double x, double y, double width, double height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public UndeadRect Offset(UndeadVec2 by)
        {
            return new UndeadRect(X + by.X, Y + by.Y, Width, Height);
        }

        public bool Contains(UndeadVec2 point)
        {
            return point.X >= X && point.X <= X + Width && point.Y >= Y && point.Y <= Y + Height;
        }
    }
}
