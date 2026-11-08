using System;
using System.Collections.Generic;
using System.Diagnostics;
using JinHyung.Core;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 판 하나를 소유한다. 화면 프레임 시간을 모아 <b>100ms 마다 한 턴</b>을 돌린다.
    ///
    /// <para>
    /// 한 턴: 이번 턴에 모인 명령을 실행으로 바꿔 넣고 → 시뮬 한 틱 `[소스] Cq3gr8XO.js:ug … executeNextTick(){…addExecution(...createExecs(turn))`.
    /// 원본 워커처럼 밀린 턴은 한 프레임에 8ms 안에서만 몰아 돌리고 나머지는 다음 프레임에 돈다 `[소스] performance.now()-t<8`.
    /// </para>
    /// </summary>
    public class FrontWarsGameManager : BaseManager, IGameUpdate
    {
        /// <summary>원본 턴 간격 — 사양이라 코드 상수다 `[소스] Cq3gr8XO.js:turnIntervalMs(){return 100}`.</summary>
        public const double TurnIntervalSeconds = 0.1;

        /// <summary>한 프레임에 밀린 턴을 몰아 돌리는 시간 한도 (ms) `[소스] performance.now()-t<8`.</summary>
        private const double DrainBudgetMs = 8;

        private readonly List<FrontWarsIntent> _pending = new List<FrontWarsIntent>(8);
        private readonly Stopwatch _drainWatch = new Stopwatch();
        private readonly Stopwatch _tickWatch = new Stopwatch();

        /// <summary>시뮬 틱 시간 · 흐름 — 성능 창(Shift+D)의 「Core Worker (sim)」 줄이 읽는다.</summary>
        public FrontWarsSimStats Stats { get; } = new FrontWarsSimStats();

        /// <summary>다음 턴에 실을 명령 수 (성능 창 「queue」).</summary>
        public int PendingCount => _pending.Count;
        private double _accumulated;

        public FrontWarsGame Game { get; private set; }

        /// <summary>로컬 사람의 클라이언트 id.</summary>
        public string LocalClientId { get; private set; }

        public FrontWarsPlayer LocalPlayer
        {
            get { return Game?.PlayerByClientId(LocalClientId); }
        }

        /// <summary>판이 도나. 메뉴 · 설정 창에서는 멈춰 있다.</summary>
        public bool Running { get; private set; }

        /// <summary>싱글 일시정지 `[소스] ko.json:instructions.pause_unpause`.</summary>
        public bool Paused { get; set; }

        /// <summary>싱글 속도 배율 — 턴 간격에 곱한다 `[소스] BMmXchgl.js:turnIntervalMs()*this.speedMultiplier`.</summary>
        public double SpeedMultiplier { get; set; } = 1.0;

        /// <summary>한 턴이 끝날 때마다 (틱 번호).</summary>
        public event Action<int> OnTick;

        public event Action<FrontWarsGame> OnGameStarted;
        public event Action OnGameStopped;

        /// <summary>판을 시작한다. 이전 판은 버린다.</summary>
        public void StartGame(FrontWarsGame game, string localClientId)
        {
            StopGame();
            Game = game;
            LocalClientId = localClientId;
            Running = true;
            Paused = false;
            _accumulated = 0;
            OnGameStarted?.Invoke(game);
        }

        public void StopGame()
        {
            if (Game == null)
                return;

            Running = false;
            Game = null;
            _pending.Clear();
            OnGameStopped?.Invoke();
        }

        /// <summary>명령을 다음 턴에 싣는다. 보낸 사람은 로컬 사람으로 채운다.</summary>
        public void Submit(FrontWarsIntent intent)
        {
            if (Game == null || intent == null)
                return;

            intent.ClientId ??= LocalClientId;
            _pending.Add(intent);
        }

        public void OnUpdate(float deltaTime)
        {
            if (Running == false || Paused || Game == null)
                return;

            _accumulated += deltaTime;
            double interval = TurnIntervalSeconds * SpeedMultiplier;
            _drainWatch.Restart();

            while (_accumulated >= interval)
            {
                _accumulated -= interval;
                RunTurn();

                if (_drainWatch.Elapsed.TotalMilliseconds >= DrainBudgetMs || Game == null)
                    break;
            }
        }

        private void RunTurn()
        {
            if (_pending.Count > 0)
            {
                foreach (FrontWarsIntent intent in _pending)
                    Game.AddExecution(FrontWarsExecutionFactory.Create(Game, intent));

                _pending.Clear();
            }

            _tickWatch.Restart();
            Game.ExecuteNextTick();
            Stats.RecordTick(_tickWatch.Elapsed.TotalMilliseconds);
            OnTick?.Invoke(Game.Ticks);
        }

        protected override void OnDispose()
        {
            OnTick = null;
            OnGameStarted = null;
            OnGameStopped = null;
        }
    }

    /// <summary>
    /// 최근 1초 창의 틱 시간(마지막 · 평균 · 최대) · 초당 틱 수. 원본은 워커가 보고하는 값이다 — 우리 시뮬은 같은 스레드에서 돈다.
    /// </summary>
    public sealed class FrontWarsSimStats
    {
        private const double WindowSeconds = 1.0;

        private readonly Queue<(double At, double Ms)> _recent = new Queue<(double, double)>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();

        public double LastMs { get; private set; }

        public double AverageMs
        {
            get
            {
                Trim();
                if (_recent.Count == 0)
                    return 0;

                double sum = 0;
                foreach (var entry in _recent)
                    sum += entry.Ms;
                return sum / _recent.Count;
            }
        }

        public double MaxMs
        {
            get
            {
                Trim();
                double max = 0;
                foreach (var entry in _recent)
                    max = Math.Max(max, entry.Ms);
                return max;
            }
        }

        public int TicksPerSecond
        {
            get
            {
                Trim();
                return _recent.Count;
            }
        }

        public void RecordTick(double ms)
        {
            LastMs = ms;
            _recent.Enqueue((_clock.Elapsed.TotalSeconds, ms));
            Trim();
        }

        private void Trim()
        {
            double now = _clock.Elapsed.TotalSeconds;
            while (_recent.Count > 0 && now - _recent.Peek().At > WindowSeconds)
                _recent.Dequeue();
        }
    }
}
