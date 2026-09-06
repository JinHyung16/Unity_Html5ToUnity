using System;
using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// ★★★ <b>전투 시뮬</b> — 유니티 타입도 물리 엔진도 안 쓴다. 원본 루프를 <b>그대로</b> 옮긴 것이다.
    ///
    /// <para>
    /// ★ 회차 9 에 원본 <b>번들 소스를 직독</b>해 규칙을 통째로 바꿔 썼다. 관측으로 못 잡았던 것들 —
    /// <b>위협 예산 스폰</b> · <b>조준 대상 유지</b> · <b>덧셈 누적 업그레이드</b> · <b>AABB 접촉</b> ·
    /// <b>레벨 목표 «식»</b> · <b>적 종류별 스탯</b> — 이 전부 여기 들어 있다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>원본에 «없는» 것을 넣지 않는다.</b> 회차 1~8 에 있던 «적끼리 밀어내기»는
    /// 원본에 없어서 <b>지웠다</b> — 관측된 「최소 간격 19.3px」은 충돌 상자가 만든 착시였다.
    /// </para>
    ///
    /// <para>
    /// 단위는 <b>원본 좌표계 · 초</b>다. 원본이 ms 로 쓰는 식(<c>예산 · 스폰 간격 · 넉백 감쇠</c>)은
    /// <c>dtMs</c> 로 환산해 <b>같은 식 그대로</b> 계산한다 — 재유도하면 반드시 어긋난다.
    /// </para>
    /// </summary>
    public sealed partial class UndeadSimulation
    {
        /// <summary>원본 한 프레임 (ms). 넉백 감쇠가 «프레임당»이라 필요하다 [소스].</summary>
        private const double FrameMs = 1000.0 / 60.0;

        private const int MaxEnemies = 2048;
        private const int MaxProjectiles = 256;
        private const int MaxGems = 2048;

        /// <summary>구슬 상한 — 원본은 획득 수만큼 늘지만 한 판에서 이보다 많아지지 않는다.</summary>
        private const int MaxOrbs = 16;

        /// <summary>적 «종류» 한 줄 — 표에서 옮겨 담는다.</summary>
        public struct EnemyType
        {
            public int Id;
            public string Code;
            public double Speed;
            public int MaxHpBase;
            public int MaxHpPerWave;
            public int UnlockWave;
            public int CostBase;
            public int CostPerWave;
            public double BudgetEarly;
            public double BudgetLate;
            public int BudgetSwitchWave;
            public int InitialBudget;
            public bool SpawnOnUnlock;

            /// <summary>그림 배율 — <b>스폰 당시 웨이브</b>로 정해지고 그 뒤로 안 바뀐다 [소스 <c>Ml</c> 생성자].</summary>
            public double ScaleBase;

            public double ScalePerWave;

            public double ScaleMax;

            /// <summary>[소스 <c>min(9, 4 + .35·wave)</c>] — 박쥐만 자란다. 나머지는 <c>PerWave = 0</c> 이라 상수다.</summary>
            public double ScaleAt(int wave)
            {
                double scale = ScaleBase + ScalePerWave * wave;
                return ScaleMax > 0.0 ? Math.Min(ScaleMax, scale) : scale;
            }

            public int MaxHpAt(int wave)
            {
                return MaxHpBase + MaxHpPerWave * wave;
            }

            public int CostAt(int wave)
            {
                return CostBase + CostPerWave * wave;
            }

            public double BudgetMultiplierAt(int wave)
            {
                return wave < BudgetSwitchWave ? BudgetEarly : BudgetLate;
            }
        }

        public struct Enemy
        {
            public bool Active;
            public UndeadVec2 Position;

            /// <summary><see cref="Types"/> 의 인덱스. ⚠ 표의 <c>Id</c> 가 아니다.</summary>
            public int TypeId;

            public double Speed;
            public int Health;
            public int MaxHealth;
            public double AnimFrame;
            public UndeadVec2 Knockback;
            public double AttackCooldown;

            /// <summary>이미 겹쳐 있었나 — <b>첫 겹침에는 짧게 기다린다</b> [소스].</summary>
            public bool Engaged;

            public double OutOfViewSeconds;

            /// <summary>태어난 웨이브. 박쥐의 체력·크기가 여기서 나온다.</summary>
            public int Wave;

            /// <summary>그림 배율 — 스폰 때 정해진다 [소스].</summary>
            public double SpriteScale;
        }

        /// <summary>
        /// <b>번개 구슬</b> — 히어로 둘레를 돈다 [소스 <c>ql</c>].
        /// <para>★ 한 바퀴를 돌 때마다 «맞힌 적 기록»이 지워져 같은 적을 다시 때린다.</para>
        /// </summary>
        public struct LightningOrb
        {
            public bool Active;
            public UndeadVec2 Position;
            public double Angle;
            public double AnimFrame;
        }

        public struct Projectile
        {
            public bool Active;
            public UndeadVec2 Position;
            public UndeadVec2 Velocity;
            public double AnimFrame;
        }

        public struct Gem
        {
            public bool Active;
            public UndeadVec2 Position;
            public double AnimFrame;
            public int Xp;
            public bool Falling;
            public double VelocityY;
            public double TargetY;
            public int Bounces;
            public bool Flying;
            public double FlyElapsed;
            public UndeadVec2 FlyStart;
        }

        private readonly UndeadSimConfig _config;
        private readonly Enemy[] _enemies = new Enemy[MaxEnemies];
        private readonly Projectile[] _projectiles = new Projectile[MaxProjectiles];
        private readonly Gem[] _gems = new Gem[MaxGems];
        private readonly LightningOrb[] _orbs = new LightningOrb[MaxOrbs];

        /// <summary>구슬마다 «이번 바퀴에 때린 적». 인덱스는 <c>구슬 × 적</c> 이다.</summary>
        private readonly bool[] _orbHits = new bool[MaxOrbs * MaxEnemies];

        private EnemyType[] _types = Array.Empty<EnemyType>();
        private double[] _budgets = Array.Empty<double>();

        private double _fireTimer;
        private double _spawnTimer;
        private double _spawnNextStep;
        private int _lastWave;
        private bool _budgetsSeeded;
        private int _targetIndex = -1;

        public UndeadSimulation(UndeadSimConfig config)
        {
            _config = config;
        }

        // ══════════════════════════════ 상태

        public UndeadVec2 HeroPosition { get; private set; }

        /// <summary>카메라가 보는 «월드 점». 히어로를 <b>지수적으로 뒤따른다</b> [소스].</summary>
        public UndeadVec2 CameraPivot { get; private set; }

        public bool HeroMoving { get; private set; }

        public double HeroAnimFrame { get; private set; }

        public double ElapsedSeconds { get; private set; }

        public int Level { get; private set; }

        public int Gauge { get; private set; }

        public int GaugeGoal { get; set; }

        public int KillCount { get; private set; }

        public int HeroHp { get; private set; }

        /// <summary>맞은 횟수 — <c>MaxHp − Hp</c> 다. 검사와 옛 호출부가 이 이름을 쓴다.</summary>
        public int HeroHitsTaken
        {
            get { return _config.HeroMaxHp - HeroHp; }
        }

        public double HeroInvincibleRemaining { get; private set; }

        /// <summary>피격 «빨강 물듦»이 남은 시간 (초) [소스 — 200ms].</summary>
        public double HeroHitTintRemaining { get; private set; }

        /// <summary>
        /// 지금 히어로를 그릴 알파 — 무적 동안 <b>0.1초마다 1 ↔ 0.3</b> 으로 깜빡인다 [소스].
        /// <para>★ 시뮬이 준다 — 뷰가 자기 시계로 깜빡이면 «정지» 중에도 깜빡인다.</para>
        /// </summary>
        public double HeroAlpha
        {
            get
            {
                if (HeroInvincibleRemaining <= 0.0 || _config.HeroBlinkIntervalSeconds <= 0.0)
                    return 1.0;

                double elapsed = _config.HeroInvincibleSeconds - HeroInvincibleRemaining;
                int phase = (int)Math.Floor(elapsed / _config.HeroBlinkIntervalSeconds);
                return phase % 2 == 0 ? 1.0 : _config.HeroBlinkAlpha;
            }
        }

        public bool HeroDead { get; private set; }

        // ── 업그레이드가 «덧셈으로» 쌓는 수치 [소스]
        public double FireRateStat { get; private set; }

        public double MoveSpeedStat { get; private set; }

        public int DamageStat { get; private set; }

        public double CollectRadiusStat { get; private set; }

        public int LightningCount { get; private set; }

        public int LightningDamageStat { get; private set; }

        public double LightningSpeedStat { get; private set; }

        public double LightningRadiusStat { get; private set; }

        public IReadOnlyList<EnemyType> Types
        {
            get { return _types; }
        }

        public IReadOnlyList<Enemy> Enemies
        {
            get { return _enemies; }
        }

        public IReadOnlyList<Projectile> Projectiles
        {
            get { return _projectiles; }
        }

        public IReadOnlyList<Gem> Gems
        {
            get { return _gems; }
        }

        public IReadOnlyList<LightningOrb> LightningOrbs
        {
            get { return _orbs; }
        }

        public int ActiveOrbCount { get; private set; }

        public int ActiveEnemyCount { get; private set; }

        public int ActiveProjectileCount { get; private set; }

        public int ActiveGemCount { get; private set; }

        /// <summary>지금 난이도 웨이브 [소스 — <c>getDifficultyWave</c>].</summary>
        public int DifficultyWave
        {
            get
            {
                int raw = (int)Math.Floor(ElapsedSeconds * 1000.0 / _config.MsPerWave);
                return raw <= _config.OnboardingWaveCount ? raw : raw - _config.OnboardingWaveCount;
            }
        }

        public event Action<int> OnLevelUp;

        public event Action<UndeadVec2, int> OnGemCollected;

        public event Action<UndeadVec2> OnEnemyKilled;

        /// <summary>
        /// 적이 <b>맞았다</b> (죽지 않아도) — 팝업이 뜬다 [소스 <c>$l</c>: 총알·번개 «명중마다» 하나].
        /// 인자는 <b>맞은 자리</b> · 피해 · 번개인가(분홍) · 보스인가(글자 ×3).
        /// ⚠ 유성·부활 몰살은 팝업이 «없다» [소스 — 그 자리에서 <c>$l</c> 을 안 만든다].
        /// </summary>
        public event Action<UndeadVec2, int, bool, bool> OnEnemyDamaged;

        /// <summary>히어로가 맞았다 — 큰 빨강 <c>-N</c> + 하트 [소스 <c>Ul</c>].</summary>
        public event Action<int> OnHeroDamaged;

        public event Action OnHeroHit;

        public event Action OnHeroDied;

        // ══════════════════════════════ 과제 포인터

        public UndeadVec2 QuestOffset
        {
            get { return QuestTarget - HeroPosition; }
        }

        public double QuestAngleDegrees
        {
            get
            {
                UndeadVec2 d = QuestOffset;
                return Math.Atan2(d.Y, d.X) * 180.0 / Math.PI;
            }
        }

        /// <summary>HUD 거리 (m). <b>1 m = 지형 타일 한 칸</b> · <b>반올림</b> [소스 — <c>Math.round</c>].</summary>
        public int QuestDistanceMeters
        {
            get { return (int)Math.Round(QuestOffset.Magnitude / _config.QuestMeterPixels, MidpointRounding.AwayFromZero); }
        }

        // ══════════════════════════════ 준비

        /// <summary>적 종류를 담는다. <b>스폰 우선순위 순으로</b> 넣어야 한다.</summary>
        public void SetEnemyTypes(IReadOnlyList<EnemyType> types)
        {
            _types = new EnemyType[types.Count];

            for (int i = 0; i < types.Count; i++)
                _types[i] = types[i];

            _budgets = new double[_types.Length];
            _budgetsSeeded = false;
        }

        public void Reset(UndeadVec2 heroPosition, int firstGoal)
        {
            Array.Clear(_enemies, 0, _enemies.Length);
            Array.Clear(_projectiles, 0, _projectiles.Length);
            Array.Clear(_gems, 0, _gems.Length);
            Array.Clear(_orbs, 0, _orbs.Length);
            Array.Clear(_orbHits, 0, _orbHits.Length);

            if (_budgets.Length > 0)
                Array.Clear(_budgets, 0, _budgets.Length);

            HeroPosition = heroPosition;
            CameraPivot = heroPosition;
            HeroMoving = false;
            HeroAnimFrame = 0.0;
            ElapsedSeconds = 0.0;
            Level = 1;
            Gauge = 0;
            GaugeGoal = firstGoal;
            KillCount = 0;
            HeroHp = _config.HeroMaxHp;
            HeroInvincibleRemaining = 0.0;
            HeroHitTintRemaining = 0.0;
            HeroDead = false;
            HasRevivedThisRun = false;
            ReviveDifficultyMultiplier = 1.0;
            _reviveRecoveryElapsed = double.MaxValue;

            FireRateStat = 1.0;
            MoveSpeedStat = 1.0;
            DamageStat = _config.ProjectileDamage;
            CollectRadiusStat = _config.CollectRadius;
            LightningCount = 0;
            LightningDamageStat = 2;
            LightningSpeedStat = 0.0025;
            LightningRadiusStat = 70.0;

            ResetQuest(heroPosition);
            ResetWorld();

            _fireTimer = 0.0;
            _spawnTimer = 0.0;
            _spawnNextStep = 100.0;
            _lastWave = 0;
            _budgetsSeeded = false;
            _targetIndex = -1;

            ActiveEnemyCount = 0;
            ActiveProjectileCount = 0;
            ActiveGemCount = 0;
            ActiveOrbCount = 0;
        }

        /// <summary>이 판에서 한 번 살아났나 — 두 번째 죽음엔 「부활?」이 안 뜬다 [소스 <c>meta_hasRevivedThisRun</c>].</summary>
        public bool HasRevivedThisRun { get; private set; }

        /// <summary>부활 뒤 스폰 예산 배율 — .5 에서 회복 시간 동안 1 로 [소스 <c>.5 + .5·e</c>].</summary>
        public double ReviveDifficultyMultiplier { get; private set; } = 1.0;

        private double _reviveRecoveryElapsed = double.MaxValue;

        public UndeadSimConfig Config { get { return _config; } }

        /// <summary>
        /// 부활 [소스 <c>applyRevive</c>] — 그 자리에 체력 <b>4</b> 로 · 무적 · <b>반경 200 안의 적은 전부, 밖의 적은 80%</b> 를
        /// 죽이고(보석은 떨어진다) · 스폰 예산을 .5 배로 낮춰 30초에 걸쳐 되돌린다. ⚠ 원본의 «대가»(광고)는 이관 밖이다.
        /// </summary>
        public void Revive()
        {
            HeroDead = false;
            HasRevivedThisRun = true;
            HeroHp = _config.ReviveHp;
            HeroInvincibleRemaining = _config.HeroInvincibleSeconds;

            double radiusSq = _config.ReviveKillRadius * _config.ReviveKillRadius;

            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false)
                    continue;

                UndeadVec2 d = _enemies[i].Position - HeroPosition;
                bool inside = d.X * d.X + d.Y * d.Y <= radiusSq;

                if (inside || RandomUtil.Value() < _config.ReviveKillOthersChance)
                    DamageEnemy(i, _enemies[i].MaxHealth, 0.0);
            }

            ReviveDifficultyMultiplier = _config.ReviveDifficultyMultiplier;
            _reviveRecoveryElapsed = 0.0;
        }

        // ══════════════════════════════ 한 프레임

        public void Step(double dt, UndeadVec2 moveInput)
        {
            if (dt <= 0.0)
                return;

            double dtMs = dt * 1000.0;

            ElapsedSeconds += dt;

            StepHero(dt, moveInput);
            StepCamera(dtMs);
            StepEnemies(dt, dtMs);
            StepSpawn(dtMs);
            StepFire(dt);
            StepProjectiles(dt);
            StepLightning(dt, dtMs);
            StepGems(dt);
            StepQuest(dt);
            StepWorld(dt);

            if (HeroInvincibleRemaining > 0.0)
                HeroInvincibleRemaining -= dt;

            if (HeroHitTintRemaining > 0.0)
                HeroHitTintRemaining -= dt;

            // 부활 뒤 난이도 회복 [소스 updateReviveDifficulty — .5 + .5·(경과/회복시간)]
            if (_reviveRecoveryElapsed < _config.ReviveDifficultyRecoverySeconds)
            {
                _reviveRecoveryElapsed = Math.Min(_config.ReviveDifficultyRecoverySeconds, _reviveRecoveryElapsed + dt);
                double e = _config.ReviveDifficultyRecoverySeconds > 0.0 ? _reviveRecoveryElapsed / _config.ReviveDifficultyRecoverySeconds : 1.0;
                ReviveDifficultyMultiplier = _config.ReviveDifficultyMultiplier + (1.0 - _config.ReviveDifficultyMultiplier) * e;
            }
            else
            {
                ReviveDifficultyMultiplier = 1.0;
            }
        }

        private void StepHero(double dt, UndeadVec2 moveInput)
        {
            UndeadVec2 dir = moveInput.Normalized;
            HeroMoving = dir.X != 0.0 || dir.Y != 0.0;

            if (HeroMoving && HeroDead == false)
            {
                double speed = _config.HeroMoveSpeed * MoveSpeedStat;
                var next = new UndeadVec2(HeroPosition.X + dir.X * speed * dt,
                                          HeroPosition.Y + dir.Y * speed * dt);

                // ★ 나무 발자국은 못 지난다 — 막힌 축만 되돌려 벽을 따라 미끄러진다 [소스 clampToPassablePosition]
                HeroPosition = ClampToPassable(HeroPosition, next);
            }

            HeroAnimFrame += _config.AnimationFps * dt;
        }

        /// <summary>
        /// 카메라는 히어로를 <b>뒤따른다</b> — <c>n = 1 − (1 − smoothing)^(dtMs/16.667)</c> [소스].
        /// <para>⚠ 「앞서 본다(리드)」가 아니다. 회차 1~8 이 그렇게 적었고 틀렸다.</para>
        /// </summary>
        private void StepCamera(double dtMs)
        {
            double n = 1.0 - Math.Pow(1.0 - _config.CameraSmoothing, dtMs / FrameMs);
            CameraPivot = new UndeadVec2(CameraPivot.X + (HeroPosition.X - CameraPivot.X) * n,
                                         CameraPivot.Y + (HeroPosition.Y - CameraPivot.Y) * n);
        }

        // ══════════════════════════════ 적

        private void StepEnemies(double dt, double dtMs)
        {
            int active = 0;

            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false)
                    continue;

                active++;

                UndeadVec2 toHero = HeroPosition - _enemies[i].Position;
                double distance = toHero.Magnitude;

                // ── 화면 밖에 오래 있으면 «다시 배치»된다 [소스 — 5초]
                if (IsInView(_enemies[i].Position))
                {
                    _enemies[i].OutOfViewSeconds = 0.0;
                }
                else
                {
                    _enemies[i].OutOfViewSeconds += dt;

                    if (_enemies[i].OutOfViewSeconds >= _config.EnemyRespawnOutOfViewSeconds)
                    {
                        _enemies[i].Position = RandomSpawnPoint();
                        _enemies[i].OutOfViewSeconds = 0.0;
                        continue;
                    }
                }

                if (_enemies[i].AttackCooldown > 0.0)
                    _enemies[i].AttackCooldown -= dt;

                if (distance > _config.EnemyChaseDeadZone)
                {
                    double step = _enemies[i].Speed * dt;
                    _enemies[i].Position = new UndeadVec2(_enemies[i].Position.X + toHero.X / distance * step,
                                                          _enemies[i].Position.Y + toHero.Y / distance * step);
                }

                AdvanceKnockback(ref _enemies[i], dtMs);

                _enemies[i].AnimFrame += _config.AnimationFps * dt;

                // ── 접촉 공격 — <b>AABB 겹침</b>이다 [소스]. 원 판정이 아니다.
                if (Overlaps(_enemies[i].Position, HeroPosition))
                {
                    if (_enemies[i].Engaged == false)
                    {
                        _enemies[i].Engaged = true;
                        _enemies[i].AttackCooldown = _config.EnemyFirstAttackWait;
                    }
                    else if (_enemies[i].AttackCooldown <= 0.0)
                    {
                        HitHero(_config.EnemyContactDamage);
                        _enemies[i].AttackCooldown = _config.EnemyAttackInterval;
                    }
                }
                else
                {
                    _enemies[i].Engaged = false;
                }
            }

            ActiveEnemyCount = active;
        }

        /// <summary>넉백은 «프레임당 감쇠»다 [소스 — <c>0.92 ^ (dtMs/16.667)</c>].</summary>
        private void AdvanceKnockback(ref Enemy enemy, double dtMs)
        {
            double magnitude = enemy.Knockback.Magnitude;

            if (magnitude <= 0.01)
            {
                enemy.Knockback = UndeadVec2.Zero;
                return;
            }

            double frames = dtMs / FrameMs;
            double decay = _config.EnemyKnockbackDecay;
            double damping = Math.Pow(decay, frames);
            double displacement = decay == 1.0 ? frames : (1.0 - damping) / (1.0 - decay);

            enemy.Position = new UndeadVec2(enemy.Position.X + enemy.Knockback.X * displacement,
                                            enemy.Position.Y + enemy.Knockback.Y * displacement);
            enemy.Knockback = enemy.Knockback * damping;
        }

        private void HitHero(int damage)
        {
            if (HeroDead || HeroInvincibleRemaining > 0.0)
                return;

            HeroHp -= damage;
            HeroInvincibleRemaining = _config.HeroInvincibleSeconds;
            HeroHitTintRemaining = _config.HeroHitTintSeconds;
            OnHeroHit?.Invoke();
            OnHeroDamaged?.Invoke(damage);

            if (HeroHp > 0)
                return;

            HeroHp = 0;
            HeroDead = true;
            OnHeroDied?.Invoke();
        }

        // ══════════════════════════════ 스폰 — 위협 예산 [소스]

        private void StepSpawn(double dtMs)
        {
            // ★ 보스전 동안 «적 생성이 멈춘다» [소스 — enemyGenerator.setPaused(true)]
            if (_types.Length == 0 || HeroDead || SpawnPaused)
                return;

            if (_budgetsSeeded == false)
            {
                _budgetsSeeded = true;

                for (int i = 0; i < _types.Length; i++)
                    _budgets[i] = _types[i].InitialBudget;
            }

            int wave = DifficultyWave;

            // 웨이브가 오를수록 예산이 «빨리» 찬다 [소스]
            double boost = 1.0
                           + wave * _config.BaseWaveBoost
                           + Math.Floor(wave / (double)_config.SteepWaveInterval) * _config.SteepWaveBoost;

            double baseGain = 16.0 * _config.ThreatBudgetPerFrame * (wave + 4) * 0.01 * (dtMs / 6.0) * boost * ReviveDifficultyMultiplier;

            for (int i = 0; i < _types.Length; i++)
            {
                if (IsAvailable(i, wave) == false)
                    continue;

                _budgets[i] += baseGain * _types[i].BudgetMultiplierAt(wave);
            }

            _spawnTimer += dtMs;

            if (_spawnTimer < _spawnNextStep)
                return;

            _spawnTimer = 0.0;
            _spawnNextStep += _config.BaseSpawnIntervalMs;

            if (_spawnNextStep > wave * _config.MaxSpawnIntervalMultiplier)
                _spawnNextStep = _config.BaseSpawnIntervalMs;

            CreateAffordableEnemies(wave);
            SpawnOnUnlock(wave);
            _lastWave = wave;
        }

        private bool IsAvailable(int typeIndex, int wave)
        {
            return wave >= _types[typeIndex].UnlockWave;
        }

        /// <summary>낼 수 있는 만큼 «우선순위 순으로» 낸다 [소스].</summary>
        private void CreateAffordableEnemies(int wave)
        {
            while (true)
            {
                bool spawned = false;

                for (int i = 0; i < _types.Length; i++)
                {
                    if (IsAvailable(i, wave) == false)
                        continue;

                    int cost = _types[i].CostAt(wave);

                    if (_budgets[i] < cost)
                        continue;

                    SpawnEnemy(i, wave);
                    _budgets[i] -= cost;
                    spawned = true;
                    break;
                }

                if (spawned == false)
                    return;
            }
        }

        /// <summary>해금되는 그 순간 한 마리를 «공짜로» 낸다 [소스 — 박쥐].</summary>
        private void SpawnOnUnlock(int wave)
        {
            for (int i = 0; i < _types.Length; i++)
            {
                if (_types[i].SpawnOnUnlock == false)
                    continue;

                if (_lastWave < _types[i].UnlockWave && wave >= _types[i].UnlockWave)
                    SpawnEnemy(i, wave);
            }
        }

        private void SpawnEnemy(int typeIndex, int wave)
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active)
                    continue;

                int hp = _types[typeIndex].MaxHpAt(wave);
                double jitter = 1.0 + _config.EnemySpeedJitter * 2.0 * RandomUtil.Value() - _config.EnemySpeedJitter;

                _enemies[i] = new Enemy
                {
                    Active = true,
                    Position = RandomSpawnPoint(),
                    TypeId = typeIndex,
                    Speed = _types[typeIndex].Speed * jitter,
                    Health = hp,
                    MaxHealth = hp,
                    AnimFrame = 0.0,
                    Wave = wave,
                    SpriteScale = _types[typeIndex].ScaleAt(wave),
                };

                return;
            }
        }

        /// <summary>화면 사각형 «바깥» 네 변 중 하나 [소스].</summary>
        private UndeadVec2 RandomSpawnPoint()
        {
            double left = CameraPivot.X - _config.ViewportWidth * 0.5;
            double right = CameraPivot.X + _config.ViewportWidth * 0.5;
            double top = CameraPivot.Y - _config.CameraOffsetY - _config.ViewportHeight * 0.5;
            double bottom = CameraPivot.Y - _config.CameraOffsetY + _config.ViewportHeight * 0.5;
            double margin = _config.SpawnMargin;

            switch (RandomUtil.Range(4))
            {
                case 0: return new UndeadVec2(left + RandomUtil.Value() * (right - left), top - margin);
                case 1: return new UndeadVec2(right + margin, top + RandomUtil.Value() * (bottom - top));
                case 2: return new UndeadVec2(left + RandomUtil.Value() * (right - left), bottom + margin);
                default: return new UndeadVec2(left - margin, top + RandomUtil.Value() * (bottom - top));
            }
        }

        private bool IsInView(UndeadVec2 position)
        {
            double halfW = _config.ViewportWidth * 0.5;
            double halfH = _config.ViewportHeight * 0.5;
            double centerY = CameraPivot.Y - _config.CameraOffsetY;

            return position.X >= CameraPivot.X - halfW && position.X <= CameraPivot.X + halfW
                   && position.Y >= centerY - halfH && position.Y <= centerY + halfH;
        }

        // ══════════════════════════════ 사격

        /// <summary>지금 발사 간격 — <c>0.35초 / 발사속도 수치</c> [소스].</summary>
        public double CurrentFireInterval
        {
            get { return _config.FireInterval / FireRateStat; }
        }

        private void StepFire(double dt)
        {
            if (HeroDead)
                return;

            _fireTimer += dt;

            if (_fireTimer < CurrentFireInterval)
                return;

            _fireTimer = 0.0;
            Fire();
        }

        /// <summary>
        /// ★★ <b>조준 대상 규칙</b> [소스] — ① <b>마지막에 맞힌 적</b>을 계속 쏜다.
        /// ② 그 적이 죽었으면 <b>화면 «안»의 가장 가까운 적</b>을 새로 고른다.
        /// <para>⚠ 「항상 최근접」이 아니다 — 회차 1~8 의 가설이 그랬다.</para>
        /// </summary>
        private void Fire()
        {
            if (_targetIndex >= 0 && _enemies[_targetIndex].Active == false)
                _targetIndex = -1;

            if (_targetIndex < 0)
                _targetIndex = FindNearestInView();

            if (_targetIndex < 0)
                return;

            UndeadVec2 muzzle = new UndeadVec2(HeroPosition.X, HeroPosition.Y + _config.MuzzleOffsetY);
            UndeadVec2 aim = EnemyColliderCenter(_enemies[_targetIndex].Position);
            UndeadVec2 delta = aim - muzzle;
            double distance = delta.Magnitude;

            if (distance <= 0.0)
                return;

            for (int i = 0; i < _projectiles.Length; i++)
            {
                if (_projectiles[i].Active)
                    continue;

                _projectiles[i] = new Projectile
                {
                    Active = true,
                    Position = muzzle,
                    Velocity = new UndeadVec2(delta.X / distance * _config.ProjectileSpeed,
                                              delta.Y / distance * _config.ProjectileSpeed),
                };

                return;
            }
        }

        private int FindNearestInView()
        {
            int best = -1;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false || IsInView(_enemies[i].Position) == false)
                    continue;

                double d = (_enemies[i].Position - HeroPosition).SqrMagnitude;

                if (d >= bestDistance)
                    continue;

                bestDistance = d;
                best = i;
            }

            return best;
        }

        private void StepProjectiles(double dt)
        {
            int active = 0;
            double range = _config.ProjectileRangeScreens
                           * Math.Max(_config.ViewportWidth, _config.ViewportHeight);

            for (int i = 0; i < _projectiles.Length; i++)
            {
                if (_projectiles[i].Active == false)
                    continue;

                _projectiles[i].Position = new UndeadVec2(
                    _projectiles[i].Position.X + _projectiles[i].Velocity.X * dt,
                    _projectiles[i].Position.Y + _projectiles[i].Velocity.Y * dt);

                _projectiles[i].AnimFrame += _config.AnimationFps * dt;

                // ★ 보스도 총알에 맞는다 — 적 배열 «밖»의 개체라 따로 본다.
                if (BossActive && CircleHitsBoss(_projectiles[i].Position, _config.ProjectileRadius))
                {
                    OnEnemyDamaged?.Invoke(BossPosition, DamageStat, false, true);
                    DamageBoss(DamageStat);
                    _projectiles[i].Active = false;
                    continue;
                }

                int hit = FindHitEnemy(_projectiles[i].Position);

                if (hit >= 0)
                {
                    OnEnemyDamaged?.Invoke(_enemies[hit].Position, DamageStat, false, false);
                    DamageEnemy(hit, DamageStat);
                    _projectiles[i].Active = false;
                    continue;
                }

                if ((_projectiles[i].Position - HeroPosition).Magnitude > range)
                {
                    _projectiles[i].Active = false;
                    continue;
                }

                active++;
            }

            ActiveProjectileCount = active;
        }

        private int FindHitEnemy(UndeadVec2 bullet)
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false)
                    continue;

                if (CircleHitsEnemy(bullet, _config.ProjectileRadius, _enemies[i].Position))
                    return i;
            }

            return -1;
        }

        private void DamageEnemy(int index, int damage)
        {
            DamageEnemy(index, damage, _config.ProjectileKnockback);
        }

        /// <summary>⚠ 넉백 세기는 <b>무기마다 다르다</b> [소스 — 총알 2 · 번개 1].</summary>
        private void DamageEnemy(int index, int damage, double knockback)
        {
            _enemies[index].Health -= damage;

            // 총알은 «미는 힘»도 준다 [소스 — 세기 2]
            UndeadVec2 away = _enemies[index].Position - HeroPosition;
            double length = away.Magnitude;

            if (length > 0.0)
            {
                _enemies[index].Knockback = new UndeadVec2(away.X / length * knockback,
                                                           away.Y / length * knockback);
            }

            if (_enemies[index].Health > 0)
            {
                // 살아 있으면 «이 적»을 계속 쏜다 [소스 — bulletHitsEnemy]
                _targetIndex = index;
                return;
            }

            UndeadVec2 at = _enemies[index].Position;
            int xp = _enemies[index].MaxHealth;

            _enemies[index].Active = false;
            KillCount++;

            if (_targetIndex == index)
                _targetIndex = -1;

            SpawnGem(at, xp);
            OnEnemyKilled?.Invoke(at);
        }

        // ══════════════════════════════ 번개 — 궤도 무기 [소스]

        /// <summary>
        /// 구슬을 돌리고 스치는 적을 때린다 [소스 <c>ql.update</c>].
        ///
        /// <para>
        /// ★ 각속도는 <c>heroLightningSpeed × dtMs</c> (라디안/ms) 이고, <b>한 바퀴마다 기록이 지워진다</b> —
        /// 그래서 같은 적을 계속 때린다. 기록을 안 지우면 «한 번만 때리는» 무기가 된다.
        /// </para>
        ///
        /// <para>⚠ 구슬 «개수»는 업그레이드가 늘린다. 각도는 항상 <c>i/n × 2π</c> 로 균등 분배다.</para>
        /// </summary>
        private void StepLightning(double dt, double dtMs)
        {
            int want = LightningCount > MaxOrbs ? MaxOrbs : LightningCount;
            int active = 0;

            for (int i = 0; i < _orbs.Length; i++)
            {
                bool shouldBeActive = i < want;

                if (shouldBeActive && _orbs[i].Active == false)
                {
                    // 새로 생기면 «전체를» 다시 균등 분배한다 [소스 — syncBulbsWithState]
                    _orbs[i].Active = true;

                    for (int k = 0; k < want; k++)
                        _orbs[k].Angle = k / (double)want * Math.PI * 2.0;
                }
                else if (shouldBeActive == false && _orbs[i].Active)
                {
                    _orbs[i].Active = false;
                }

                if (_orbs[i].Active == false)
                    continue;

                active++;

                double before = _orbs[i].Angle;
                _orbs[i].Angle += LightningSpeedStat * dtMs;
                _orbs[i].AnimFrame += _config.AnimationFps * dt;

                // 한 바퀴를 넘었으면 «이번 바퀴 기록»을 지운다
                if (Math.Floor(before / (Math.PI * 2.0)) != Math.Floor(_orbs[i].Angle / (Math.PI * 2.0)))
                    Array.Clear(_orbHits, i * MaxEnemies, MaxEnemies);

                double cx = HeroPosition.X + _config.LightningCenterX;
                double cy = HeroPosition.Y + _config.LightningCenterY;
                _orbs[i].Position = new UndeadVec2(cx + Math.Cos(_orbs[i].Angle) * LightningRadiusStat,
                                                   cy + Math.Sin(_orbs[i].Angle) * LightningRadiusStat);

                StepOrbHits(i);
            }

            ActiveOrbCount = active;
        }

        private void StepOrbHits(int orb)
        {
            int baseIndex = orb * MaxEnemies;

            for (int e = 0; e < _enemies.Length; e++)
            {
                if (_enemies[e].Active == false || _orbHits[baseIndex + e])
                    continue;

                if (CircleHitsEnemy(_orbs[orb].Position, _config.LightningOrbRadius, _enemies[e].Position) == false)
                    continue;

                _orbHits[baseIndex + e] = true;
                OnEnemyDamaged?.Invoke(_enemies[e].Position, LightningDamageStat, true, false);
                DamageEnemy(e, LightningDamageStat, _config.LightningKnockback);
            }

            // 보스는 «한 바퀴에 한 번» 맞는다 — 배열 밖이라 마지막 칸을 기록으로 쓴다.
            if (BossActive == false || _orbHits[baseIndex + MaxEnemies - 1])
                return;

            if (CircleHitsBoss(_orbs[orb].Position, _config.LightningOrbRadius) == false)
                return;

            _orbHits[baseIndex + MaxEnemies - 1] = true;
            OnEnemyDamaged?.Invoke(BossPosition, LightningDamageStat, true, true);
            DamageBoss(LightningDamageStat);
        }

        /// <summary>보스 충돌 상자 — 적과 크기가 다르다 [소스 — (−52,−180,104,100)].</summary>
        private bool CircleHitsBoss(UndeadVec2 center, double radius)
        {
            double bx = BossPosition.X + _config.BossColliderX;
            double by = BossPosition.Y + _config.BossColliderY;
            double cx = Clamp(center.X, bx, bx + _config.BossColliderW);
            double cy = Clamp(center.Y, by, by + _config.BossColliderH);
            double dx = center.X - cx;
            double dy = center.Y - cy;

            return dx * dx + dy * dy <= radius * radius;
        }

        // ══════════════════════════════ 보석

        /// <summary>★ <b>보석의 XP 는 «그 적의 최대 체력»</b>이다 [소스] — 고정값이 아니다.</summary>
        private void SpawnGem(UndeadVec2 at, int xp)
        {
            for (int i = 0; i < _gems.Length; i++)
            {
                if (_gems[i].Active)
                    continue;

                _gems[i] = new Gem
                {
                    Active = true,
                    Position = at,
                    Xp = xp,
                    Falling = true,
                    TargetY = at.Y + _config.GemFallOffsetY,
                };

                return;
            }
        }

        private void StepGems(double dt)
        {
            int active = 0;

            for (int i = 0; i < _gems.Length; i++)
            {
                if (_gems[i].Active == false)
                    continue;

                _gems[i].AnimFrame += _config.AnimationFps * dt;

                if (_gems[i].Falling)
                    StepGemFall(ref _gems[i], dt);

                if (_gems[i].Flying)
                {
                    StepGemFly(ref _gems[i], dt, i);
                }
                else if ((_gems[i].Position - HeroPosition).Magnitude <= CollectRadiusStat)
                {
                    _gems[i].Flying = true;
                    _gems[i].FlyElapsed = 0.0;
                    _gems[i].FlyStart = _gems[i].Position;
                }

                if (_gems[i].Active)
                    active++;
            }

            ActiveGemCount = active;
        }

        private void StepGemFall(ref Gem gem, double dt)
        {
            gem.VelocityY += _config.GemGravity * dt;
            gem.Position = new UndeadVec2(gem.Position.X, gem.Position.Y + gem.VelocityY * dt);

            if (gem.Position.Y < gem.TargetY || gem.VelocityY <= 0.0)
                return;

            gem.Position = new UndeadVec2(gem.Position.X, gem.TargetY);
            gem.Bounces++;

            if (gem.Bounces <= _config.GemMaxBounces)
            {
                gem.VelocityY = -Math.Abs(gem.VelocityY) * _config.GemBounceDamping;
                return;
            }

            gem.VelocityY = 0.0;
            gem.Falling = false;
        }

        private void StepGemFly(ref Gem gem, double dt, int index)
        {
            gem.FlyElapsed += dt;
            double t = gem.FlyElapsed / _config.GemFlySeconds;

            if (t >= 1.0)
            {
                gem.Active = false;
                AddGauge(gem.Xp);
                OnGemCollected?.Invoke(HeroPosition, gem.Xp);
                return;
            }

            gem.Position = new UndeadVec2(gem.FlyStart.X + (HeroPosition.X - gem.FlyStart.X) * t,
                                          gem.FlyStart.Y + (HeroPosition.Y - gem.FlyStart.Y) * t);
        }

        // ══════════════════════════════ 과제 — 쓰러진 전사

        // ══════════════════════════════ 게이지 · 업그레이드

        /// <summary>게이지를 올린다. 넘친 만큼은 <b>다음 레벨로 이월</b>된다 [실측].</summary>
        public void AddGauge(int amount)
        {
            Gauge += amount;

            while (GaugeGoal > 0 && Gauge >= GaugeGoal)
            {
                Gauge -= GaugeGoal;
                Level++;
                OnLevelUp?.Invoke(Level);

                if (GaugeGoal <= 0)
                    return;
            }
        }

        /// <summary>
        /// 업그레이드를 붙인다 [소스 — <c>applyReward</c>].
        /// <para>⚠ <b>덧셈 누적</b>이다. 「발사 속도 +30%」를 두 번 먹으면 1 → 1.3 → <b>1.6</b> 이다.</para>
        /// </summary>
        public void ApplyUpgrade(string code, EUndeadApplyKind kind, double amount)
        {
            switch (code)
            {
                case "bulletsFireRate":
                    FireRateStat += amount;
                    break;

                case "bulletDamage":
                    DamageStat += DamageStat;
                    break;

                case "heroMoveSpeed":
                    MoveSpeedStat += amount;
                    break;

                case "heroCollectRadius":
                    CollectRadiusStat += amount;
                    break;

                case "lightning":
                    LightningCount += (int)amount;
                    break;

                case "lightningSpeed":
                    LightningSpeedStat += amount;
                    break;

                case "lightningRadius":
                    LightningRadiusStat += amount;
                    break;

                case "lightningDamage":
                    LightningDamageStat += (int)Math.Round(0.5 * LightningDamageStat, MidpointRounding.AwayFromZero);
                    break;

                default:
                    Log.Warning($"모르는 업그레이드 코드다 — {code} (표와 시뮬 중 한쪽이 낡았다)");
                    break;
            }
        }

        // ══════════════════════════════ 충돌 — 원본은 AABB 다

        private UndeadVec2 EnemyColliderCenter(UndeadVec2 enemy)
        {
            return new UndeadVec2(enemy.X + _config.EnemyColliderX + _config.EnemyColliderW * 0.5,
                                  enemy.Y + _config.EnemyColliderY + _config.EnemyColliderH * 0.5);
        }

        private bool Overlaps(UndeadVec2 enemy, UndeadVec2 hero)
        {
            double ex = enemy.X + _config.EnemyColliderX;
            double ey = enemy.Y + _config.EnemyColliderY;
            double hx = hero.X + _config.HeroColliderX;
            double hy = hero.Y + _config.HeroColliderY;

            return ex < hx + _config.HeroColliderW && ex + _config.EnemyColliderW > hx
                   && ey < hy + _config.HeroColliderH && ey + _config.EnemyColliderH > hy;
        }

        /// <summary>총알은 <b>원</b>이고 적은 <b>사각형</b>이다 [소스].</summary>
        private bool CircleHitsEnemy(UndeadVec2 center, double radius, UndeadVec2 enemy)
        {
            double ex = enemy.X + _config.EnemyColliderX;
            double ey = enemy.Y + _config.EnemyColliderY;
            double cx = Clamp(center.X, ex, ex + _config.EnemyColliderW);
            double cy = Clamp(center.Y, ey, ey + _config.EnemyColliderH);
            double dx = center.X - cx;
            double dy = center.Y - cy;

            return dx * dx + dy * dy <= radius * radius;
        }

        private static double Clamp(double value, double min, double max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
