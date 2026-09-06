using System;
using System.Collections.Generic;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 월드 오브젝트 — <b>사악한 나무(중형 오브젝트)</b> 와 <b>모닥불(회복 오브젝트)</b> [소스 직독 · 회차 11].
    ///
    /// <para>
    /// ★ 둘 다 <b>지형이 놓는다</b> — 24×24 «칸» 단위 청크마다 노이즈가 문턱을 넘으면 그 청크에 하나가
    /// 시드 난수 자리에 선다. 나무는 <c>noise(chunk) &gt; .4</c> · 발자국 4×2 칸 · <b>통행 불가</b>,
    /// 모닥불은 <c>noise(chunk) &gt; .3</c> · 1칸.
    /// </para>
    ///
    /// <para>
    /// ★★ 나무는 «충전 폭탄»이다 — 히어로가 150 안에 있으면 1250ms 동안 차오르고(밖이면 1.5배로 식는다),
    /// 다 차면 <b>유성 6개</b>를 원형으로 뿜는다(속도 0.5px/ms · 반지름 40 · 피해 = 총알 피해 ×2 · 넉백 2 ·
    /// 적마다 한 번). 한 번 터진 나무는 비활성 그림이 되고 다시 안 터진다. 카메라가 300ms 흔들린다.
    /// </para>
    ///
    /// <para>⚠ 자리의 «난수원»은 우리 것이라 원본과 다른 곳에 선다 — 규칙·밀도는 같다 (의도된 차이 #6).</para>
    /// </summary>
    public sealed partial class UndeadSimulation
    {
        private const int MaxTrees = 64;
        private const int MaxMeteors = 64;
        private const int MaxFireplaces = 64;

        public struct Tree
        {
            public bool Active;
            public int TileX;
            public int TileY;
            public UndeadVec2 Position;
            public bool Bursted;

            /// <summary>히어로가 반경 안에 있어 «켜진» 그림인가.</summary>
            public bool Activated;

            public double ChargeMs;

            /// <summary>충전 진행(0~1)을 부드럽게 따라가는 값 — 그림의 «떨림»이 여기서 나온다 [소스].</summary>
            public double ChargeSmoothed;

            /// <summary>대기 흔들림 위상 [소스 <c>pulsePhase</c>].</summary>
            public double PulsePhase;

            public double IdleMs;
        }

        public struct Meteor
        {
            public bool Active;
            public UndeadVec2 Position;
            public UndeadVec2 Velocity;
            public double AnimFrame;

            /// <summary>이미 맞힌 적 — 적 하나에 한 번만 [소스 <c>hitEnemies</c>].</summary>
            public bool[] Hit;
            public bool HitBoss;
        }

        public struct Fireplace
        {
            public bool Active;
            public int TileX;
            public int TileY;
            public UndeadVec2 Position;
            public int Lives;
            public double CooldownSeconds;

            /// <summary>「화염 소진!」을 다시 띄우기까지 [소스 <c>fireOutCooldownDurationMs 900</c>].</summary>
            public double FireOutCooldownSeconds;
        }

        private readonly Tree[] _trees = new Tree[MaxTrees];
        private readonly Meteor[] _meteors = new Meteor[MaxMeteors];
        private readonly Fireplace[] _fireplaces = new Fireplace[MaxFireplaces];

        // 청크를 벗어났다 돌아와도 «터진 나무»·«남은 재고»는 그대로다 [소스 markBursted · livesInStock]
        private readonly HashSet<long> _burstedTrees = new HashSet<long>();
        private readonly Dictionary<long, int> _fireplaceLives = new Dictionary<long, int>();

        private UndeadTerrainNoise _noiseMediumObject;
        private UndeadTerrainNoise _noiseHealingObject;
        private int _lastChunkX = int.MinValue;
        private int _lastChunkY = int.MinValue;

        public IReadOnlyList<Tree> Trees { get { return _trees; } }
        public IReadOnlyList<Meteor> Meteors { get { return _meteors; } }
        public IReadOnlyList<Fireplace> Fireplaces { get { return _fireplaces; } }

        public int ActiveTreeCount { get; private set; }
        public int ActiveMeteorCount { get; private set; }
        public int ActiveFireplaceCount { get; private set; }

        /// <summary>나무가 터졌다 — 카메라 흔들림(10 · 300ms) [소스 <c>triggerShake</c>].</summary>
        public event Action<UndeadVec2> OnTreeBurst;

        private void ResetWorld()
        {
            Array.Clear(_trees, 0, _trees.Length);
            Array.Clear(_fireplaces, 0, _fireplaces.Length);

            for (int i = 0; i < _meteors.Length; i++)
            {
                _meteors[i].Active = false;
                _meteors[i].HitBoss = false;

                if (_meteors[i].Hit != null)
                    Array.Clear(_meteors[i].Hit, 0, _meteors[i].Hit.Length);
            }

            _burstedTrees.Clear();
            _fireplaceLives.Clear();
            _lastChunkX = int.MinValue;
            _lastChunkY = int.MinValue;

            if (_noiseMediumObject == null)
            {
                _noiseMediumObject = new UndeadTerrainNoise(UndeadTerrainSeeds.MediumObject);
                _noiseHealingObject = new UndeadTerrainNoise(UndeadTerrainSeeds.HealingObject);
            }

            ActiveTreeCount = 0;
            ActiveMeteorCount = 0;
            ActiveFireplaceCount = 0;
            SyncWorldObjects(true);
        }

        private void StepWorld(double dt)
        {
            SyncWorldObjects(false);
            StepTrees(dt);
            StepMeteors(dt);
            StepFireplaces(dt);
        }

        // ══════════════════════════════ 청크 동기화 [소스 updateMediumObjects · updateHealingObjects]

        private static long ChunkKey(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private int ChunkOf(double world)
        {
            return (int)Math.Floor(world / (UndeadTerrainSeeds.TilePixels * (double)_config.ObjectChunkTiles));
        }

        /// <summary>카메라 둘레 청크의 나무·모닥불을 세운다. 청크가 바뀔 때만 다시 센다.</summary>
        private void SyncWorldObjects(bool force)
        {
            int cx = ChunkOf(CameraPivot.X);
            int cy = ChunkOf(CameraPivot.Y);

            if (force == false && cx == _lastChunkX && cy == _lastChunkY)
                return;

            _lastChunkX = cx;
            _lastChunkY = cy;

            int chunkPx = UndeadTerrainSeeds.TilePixels * _config.ObjectChunkTiles;
            int reachX = (int)Math.Ceiling(_config.ViewportWidth / chunkPx) + 1;
            int reachY = (int)Math.Ceiling(_config.ViewportHeight / chunkPx) + 1;

            int trees = 0;
            int fires = 0;

            for (int y = cy - reachY; y <= cy + reachY; y++)
            {
                for (int x = cx - reachX; x <= cx + reachX; x++)
                {
                    if (trees < _trees.Length && TryPlaceTree(x, y, out Tree tree))
                        _trees[trees++] = tree;

                    if (fires < _fireplaces.Length && TryPlaceFireplace(x, y, out Fireplace fire))
                        _fireplaces[fires++] = fire;
                }
            }

            for (int i = trees; i < _trees.Length; i++)
                _trees[i].Active = false;

            for (int i = fires; i < _fireplaces.Length; i++)
                _fireplaces[i].Active = false;

            ActiveTreeCount = trees;
            ActiveFireplaceCount = fires;
        }

        private bool TryPlaceTree(int chunkX, int chunkY, out Tree tree)
        {
            tree = default;
            int tilesPerChunk = _config.ObjectChunkTiles;
            int originX = chunkX * tilesPerChunk;
            int originY = chunkY * tilesPerChunk;

            if (_noiseMediumObject.Sample(originX * UndeadTerrainSeeds.ObjectFrequency,
                                          originY * UndeadTerrainSeeds.ObjectFrequency) <= _config.TreeThreshold)
                return false;

            var rng = new UndeadSeededRandom($"{chunkX},{chunkY}");
            int tileX = originX + (int)Math.Floor(rng.Next() * (tilesPerChunk - _config.TreeWidthTiles + 1));
            int tileY = originY + (int)Math.Floor(rng.Next() * (tilesPerChunk - _config.TreeHeightTiles + 1));
            long key = ChunkKey(tileX, tileY);

            // 이미 세워 둔 나무면 «충전·위상»을 잃지 않는다
            for (int i = 0; i < _trees.Length; i++)
            {
                if (_trees[i].Active && _trees[i].TileX == tileX && _trees[i].TileY == tileY)
                {
                    tree = _trees[i];
                    return true;
                }
            }

            var idleRng = new UndeadSeededRandom($"{tileX}_{tileY}");
            double idleMs = 10000.0 * idleRng.Next();

            tree = new Tree
            {
                Active = true,
                TileX = tileX,
                TileY = tileY,
                Position = new UndeadVec2(tileX * UndeadTerrainSeeds.TilePixels + _config.TreeAnchorOffsetX,
                                          tileY * UndeadTerrainSeeds.TilePixels),
                Bursted = _burstedTrees.Contains(key),
                IdleMs = idleMs,
                PulsePhase = idleMs * _config.TreeIdleSpeed * 1.35,
            };
            return true;
        }

        private bool TryPlaceFireplace(int chunkX, int chunkY, out Fireplace fire)
        {
            fire = default;
            int tilesPerChunk = _config.ObjectChunkTiles;
            int originX = chunkX * tilesPerChunk;
            int originY = chunkY * tilesPerChunk;

            if (_noiseHealingObject.Sample(originX * UndeadTerrainSeeds.ObjectFrequency,
                                           originY * UndeadTerrainSeeds.ObjectFrequency) <= _config.FireplaceThreshold)
                return false;

            var rng = new UndeadSeededRandom($"healing_{chunkX},{chunkY}");
            int tileX = originX + (int)Math.Floor(rng.Next() * tilesPerChunk);
            int tileY = originY + (int)Math.Floor(rng.Next() * tilesPerChunk);
            long key = ChunkKey(tileX, tileY);

            for (int i = 0; i < _fireplaces.Length; i++)
            {
                if (_fireplaces[i].Active && _fireplaces[i].TileX == tileX && _fireplaces[i].TileY == tileY)
                {
                    fire = _fireplaces[i];
                    return true;
                }
            }

            fire = new Fireplace
            {
                Active = true,
                TileX = tileX,
                TileY = tileY,
                Position = new UndeadVec2(tileX * UndeadTerrainSeeds.TilePixels + _config.FireplaceAnchorOffset,
                                          tileY * UndeadTerrainSeeds.TilePixels + _config.FireplaceAnchorOffset),
                Lives = _fireplaceLives.TryGetValue(key, out int lives) ? lives : _config.FireplaceLives,
            };
            return true;
        }

        // ══════════════════════════════ 통행 [소스 isTilePassable · clampToPassablePosition]

        /// <summary>나무 발자국(4×2 칸) 위인가.</summary>
        public bool IsTileBlocked(int tileX, int tileY)
        {
            for (int i = 0; i < _trees.Length; i++)
            {
                if (_trees[i].Active == false)
                    continue;

                if (tileX >= _trees[i].TileX && tileX < _trees[i].TileX + _config.TreeWidthTiles
                    && tileY <= _trees[i].TileY && tileY > _trees[i].TileY - _config.TreeHeightTiles)
                    return true;
            }

            return false;
        }

        private bool IsBlocked(UndeadVec2 world)
        {
            int tileX = (int)Math.Floor(world.X / UndeadTerrainSeeds.TilePixels);
            int tileY = (int)Math.Floor(world.Y / UndeadTerrainSeeds.TilePixels);
            return IsTileBlocked(tileX, tileY);
        }

        /// <summary>막힌 칸에 들어갔으면 «한 축씩» 되돌린다 — 벽을 따라 미끄러진다 [소스].</summary>
        private UndeadVec2 ClampToPassable(UndeadVec2 from, UndeadVec2 to)
        {
            if (IsBlocked(to) == false)
                return to;

            var keepX = new UndeadVec2(to.X, from.Y);

            if (IsBlocked(keepX) == false)
                return keepX;

            var keepY = new UndeadVec2(from.X, to.Y);

            if (IsBlocked(keepY) == false)
                return keepY;

            return from;
        }

        // ══════════════════════════════ 나무 [소스 class ld]

        private void StepTrees(double dt)
        {
            double dtMs = dt * 1000.0;
            double chargeMs = _config.TreeChargeSeconds * 1000.0;

            for (int i = 0; i < _trees.Length; i++)
            {
                if (_trees[i].Active == false || _trees[i].Bursted)
                    continue;

                double distance = (HeroPosition - _trees[i].Position).Magnitude;
                bool near = HeroDead == false && distance <= _config.TreeTriggerRadius;

                _trees[i].Activated = near;
                _trees[i].ChargeMs = near
                    ? Math.Min(chargeMs, _trees[i].ChargeMs + dtMs)
                    : Math.Max(0.0, _trees[i].ChargeMs - _config.TreeChargeDecayMultiplier * dtMs);

                _trees[i].IdleMs += dtMs;

                // 진행을 부드럽게 따라간다 — 1 − 0.002^dt [소스]
                double target = Math.Min(_trees[i].ChargeMs / chargeMs, 1.0);
                double blend = 1.0 - Math.Pow(0.002, dtMs);
                _trees[i].ChargeSmoothed += (target - _trees[i].ChargeSmoothed) * blend;

                double s = _trees[i].ChargeSmoothed;

                if (s > 0.0)
                    s += 0.5;

                _trees[i].PulsePhase += _config.TreeIdleSpeed * (1.35 + 1.5 * s) * dtMs;

                if (_trees[i].ChargeMs >= chargeMs)
                    BurstTree(i);
            }
        }

        private void BurstTree(int index)
        {
            _trees[index].Bursted = true;
            _trees[index].Activated = false;
            _trees[index].ChargeMs = 0.0;
            _burstedTrees.Add(ChunkKey(_trees[index].TileX, _trees[index].TileY));

            var origin = new UndeadVec2(_trees[index].Position.X,
                                        _trees[index].Position.Y - UndeadTerrainSeeds.TilePixels);
            int count = Math.Max(0, _config.TreeBurstMeteorCount);

            for (int k = 0; k < count; k++)
            {
                double angle = k / (double)count * Math.PI * 2.0;
                SpawnMeteor(origin, new UndeadVec2(Math.Cos(angle), Math.Sin(angle)));
            }

            OnTreeBurst?.Invoke(origin);
        }

        private void SpawnMeteor(UndeadVec2 origin, UndeadVec2 direction)
        {
            for (int i = 0; i < _meteors.Length; i++)
            {
                if (_meteors[i].Active)
                    continue;

                if (_meteors[i].Hit == null)
                    _meteors[i].Hit = new bool[MaxEnemies];
                else
                    Array.Clear(_meteors[i].Hit, 0, _meteors[i].Hit.Length);

                _meteors[i].Active = true;
                _meteors[i].HitBoss = false;
                _meteors[i].Position = origin;
                _meteors[i].Velocity = new UndeadVec2(direction.X * _config.MeteorSpeed, direction.Y * _config.MeteorSpeed);
                _meteors[i].AnimFrame = 0.0;
                return;
            }
        }

        /// <summary>유성 — 적을 «관통»하며 하나에 한 번씩 때린다. 화면 두 배 밖으로 나가면 사라진다 [소스 class rd].</summary>
        private void StepMeteors(double dt)
        {
            int active = 0;
            int damage = (int)Math.Round(_config.MeteorDamageMultiplier * DamageStat);

            for (int i = 0; i < _meteors.Length; i++)
            {
                if (_meteors[i].Active == false)
                    continue;

                _meteors[i].Position = new UndeadVec2(
                    _meteors[i].Position.X + _meteors[i].Velocity.X * dt,
                    _meteors[i].Position.Y + _meteors[i].Velocity.Y * dt);
                _meteors[i].AnimFrame += _config.AnimationFps * dt;

                for (int e = 0; e < _enemies.Length; e++)
                {
                    if (_enemies[e].Active == false || _meteors[i].Hit[e])
                        continue;

                    if (CircleHitsEnemy(_meteors[i].Position, _config.MeteorRadius, _enemies[e].Position) == false)
                        continue;

                    _meteors[i].Hit[e] = true;
                    DamageEnemy(e, damage, _config.MeteorKnockback);
                }

                if (BossActive && _meteors[i].HitBoss == false
                    && CircleHitsBoss(_meteors[i].Position, _config.MeteorRadius))
                {
                    _meteors[i].HitBoss = true;
                    DamageBoss(damage);
                }

                double limitX = _config.ViewportWidth;
                double limitY = _config.ViewportHeight;

                if (Math.Abs(_meteors[i].Position.X - CameraPivot.X) > limitX
                    || Math.Abs(_meteors[i].Position.Y - CameraPivot.Y) > limitY)
                {
                    _meteors[i].Active = false;
                    continue;
                }

                active++;
            }

            ActiveMeteorCount = active;
        }

        // ══════════════════════════════ 모닥불 [소스 class Wl]

        /// <summary>
        /// 다가가면 체력을 채워 준다. <b>재고가 있는 동안만</b>, 한 번 채우면 700ms 쉰다.
        ///
        /// <para>
        /// ★ 접촉의 결과는 <b>세 갈래</b>다 [소스 <c>Wl.update</c>] — 재고가 0 이면 <b>「화염 소진!」</b>(900ms 쿨),
        /// 체력이 만땅이면 <b>「최대 체력」</b>(회복 쿨을 그대로 먹는다), 그 밖이면 회복이다.
        /// 회차 11 은 앞의 둘을 «그냥 넘겨» 두 문구가 화면에 영영 안 떴다.
        /// </para>
        /// </summary>
        private void StepFireplaces(double dt)
        {
            for (int i = 0; i < _fireplaces.Length; i++)
            {
                if (_fireplaces[i].Active == false)
                    continue;

                if (_fireplaces[i].CooldownSeconds > 0.0)
                    _fireplaces[i].CooldownSeconds -= dt;

                if (_fireplaces[i].FireOutCooldownSeconds > 0.0)
                    _fireplaces[i].FireOutCooldownSeconds -= dt;

                if (HeroDead)
                    continue;

                // 판정 원은 «불보다 조금 아래»다 [소스 collider (0, −8, 55)]
                var center = new UndeadVec2(_fireplaces[i].Position.X, _fireplaces[i].Position.Y + _config.FireplaceColliderOffsetY);

                if ((center - HeroPosition).Magnitude > _config.FireplaceRadius)
                    continue;

                if (_fireplaces[i].Lives <= 0)
                {
                    if (_fireplaces[i].FireOutCooldownSeconds <= 0.0)
                    {
                        _fireplaces[i].FireOutCooldownSeconds = _config.FireOutCooldownSeconds;
                        OnFireplaceOut?.Invoke(_fireplaces[i].Position);
                    }

                    continue;
                }

                if (_fireplaces[i].CooldownSeconds > 0.0)
                    continue;

                if (HeroHp >= _config.HeroMaxHp)
                {
                    _fireplaces[i].CooldownSeconds = _config.FireplaceHealCooldown;
                    OnFireplaceFullHealth?.Invoke(_fireplaces[i].Position);
                    continue;
                }

                int healed = Math.Min(_config.FireplaceHealAmount, _config.HeroMaxHp - HeroHp);

                if (healed <= 0)
                    continue;

                HeroHp += healed;
                _fireplaces[i].Lives--;
                _fireplaces[i].CooldownSeconds = _config.FireplaceHealCooldown;
                _fireplaceLives[ChunkKey(_fireplaces[i].TileX, _fireplaces[i].TileY)] = _fireplaces[i].Lives;
                OnHeroHealed?.Invoke(HeroPosition, healed);
            }
        }
    }
}
