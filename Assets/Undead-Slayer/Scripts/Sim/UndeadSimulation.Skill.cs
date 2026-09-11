using System;
using System.Collections.Generic;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 액티브 스킬 — <b>과제 보상으로 얻고, 쿨다운을 두고 쓴다</b> [소스 <c>gameplaySkillsController</c>].
    ///
    /// <para>
    /// ★ <b>「가지고 있나 · 지금 쓸 수 있나 · 지금 도는 중인가」 셋이 다른 상태다.</b>
    /// 하나로 합치면 쿨다운 중에 다시 발동되거나, 효과가 도는 중에 겹쳐 걸린다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>[사고 · 재발방지 #174]</b> 이 시스템이 <b>통째로 없었다</b>. 문구 60키에 스킬 이름이 있고
    /// 배경 아트도 구워져 있어서 <b>자산 감사·문구 감사가 전부 통과</b>했다 —
    /// 「자산은 다 있는데 읽는 곳이 없는」 상태는 어느 검사에도 안 걸린다.
    /// </para>
    /// </summary>
    public sealed partial class UndeadSimulation
    {
        public const int SkillCount = 4;

        private readonly bool[] _skillOwned = new bool[SkillCount];
        private readonly double[] _skillCooldown = new double[SkillCount];
        private readonly double[] _skillActive = new double[SkillCount];

        /// <summary>돌진이 지나가며 «이미 때린» 적 — 한 번씩만 때린다 [소스 <c>hitTargets</c>].</summary>
        private readonly HashSet<int> _dashHits = new HashSet<int>();

        private UndeadVec2 _dashDirection;

        /// <summary>화염 자취 조각 — 지나온 자리에 남는다 [소스 <c>segments</c>].</summary>
        public struct TrailSegment
        {
            public bool Active;
            public UndeadVec2 Position;
            public double AgeSeconds;
        }

        private readonly TrailSegment[] _trail = new TrailSegment[MaxTrailSegments];
        private const int MaxTrailSegments = 128;

        private UndeadVec2 _trailLastHeroPosition;
        private double _trailDistanceSinceSegment;

        /// <summary>적마다 «다시 데일 때까지» 남은 시간 [소스 <c>targetThrottleRemainingMs</c> 500ms].</summary>
        private readonly double[] _trailThrottle = new double[MaxEnemies];

        // ══════════════════════════════ 바깥이 보는 것

        public IReadOnlyList<TrailSegment> TrailSegments
        {
            get { return _trail; }
        }

        /// <summary>조각 하나의 수명 (초) — 뷰가 «옅어지는 정도»를 이 값으로 낸다.</summary>
        public double TrailSegmentLifeSeconds
        {
            get { return _config.TrailSegmentLifeSeconds; }
        }

        /// <summary>지금 시뮬이 도는 배율 [소스 <c>simulationScale</c>] — 섬광 이동이 2 로 올린다.</summary>
        public double SimulationScale { get; private set; } = 1.0;

        /// <summary>돌진 중인가 — 이 동안 <b>접촉·투사체 피해를 안 받는다</b> [소스 <c>isDashProtected</c>].</summary>
        public bool HeroDashProtected
        {
            get { return _skillActive[(int)EUndeadSkill.Dash] > 0.0; }
        }

        /// <summary>적이 «얼어» 있나 — 겨울 파동이 도는 동안 적·투사체가 통째로 멈춘다 [소스 <c>beginFreeze</c>].</summary>
        public bool EnemiesFrozen
        {
            get { return _skillActive[(int)EUndeadSkill.WinterPulse] > 0.0; }
        }

        /// <summary>가로 입력이 마지막으로 «0 이 아니었을 때»의 방향 [소스 <c>getLastNonZeroDirection</c>].</summary>
        public UndeadVec2 HeroLastDirection { get; private set; } = new UndeadVec2(1.0, 0.0);

        public bool OwnsSkill(EUndeadSkill skill)
        {
            return IsSkill(skill) && _skillOwned[(int)skill];
        }

        public double SkillCooldownRemaining(EUndeadSkill skill)
        {
            return IsSkill(skill) ? _skillCooldown[(int)skill] : 0.0;
        }

        public double SkillActiveRemaining(EUndeadSkill skill)
        {
            return IsSkill(skill) ? _skillActive[(int)skill] : 0.0;
        }

        /// <summary>지금 누르면 도나 [소스 <c>activate</c> 의 앞 세 조건].</summary>
        public bool SkillReady(EUndeadSkill skill)
        {
            return OwnsSkill(skill) && _skillCooldown[(int)skill] <= 0.0 && _skillActive[(int)skill] <= 0.0;
        }

        public event Action<EUndeadSkill> OnSkillGranted;

        public event Action<EUndeadSkill> OnSkillActivated;

        /// <summary>보상으로 준다 [소스 <c>ownedSkills[id] = true</c>].</summary>
        public void GrantSkill(EUndeadSkill skill)
        {
            if (IsSkill(skill) == false || _skillOwned[(int)skill])
                return;

            _skillOwned[(int)skill] = true;
            OnSkillGranted?.Invoke(skill);
        }

        /// <summary>
        /// 발동한다 — 못 쓰면 <c>false</c> 다 [소스 <c>activate</c>].
        ///
        /// <para>
        /// ⚠ <b>쿨다운은 «발동에 성공했을 때만» 돈다</b> [소스 — 실패하면 <c>cooldownRemainingMs</c> 를 안 건드린다].
        /// 눌렀다고 도는 게 아니다.
        /// </para>
        /// </summary>
        public bool ActivateSkill(EUndeadSkill skill)
        {
            if (SkillReady(skill) == false || HeroDead)
                return false;

            if (skill == EUndeadSkill.Dash && BeginDash() == false)
                return false;

            int index = (int)skill;
            _skillActive[index] = ActiveSecondsOf(skill);
            _skillCooldown[index] = _config.SkillCooldownSeconds[index];

            switch (skill)
            {
                case EUndeadSkill.BlazingTrail:
                    BeginTrail();
                    break;

                case EUndeadSkill.FlashMove:
                    SimulationScale = _config.FlashMoveSimulationScale;
                    break;
            }

            OnSkillActivated?.Invoke(skill);
            return true;
        }

        // ══════════════════════════════ 한 프레임

        /// <summary>
        /// ⚠ <b>이 함수에 들어오는 <c>dt</c> 는 «배율이 실린» 값이다.</b>
        /// 섬광 이동이 배율을 2 로 올리면 세계가 두 배로 빨리 돈다 [소스 <c>simulationScale</c>].
        /// </summary>
        private void StepSkills(double dt)
        {
            for (int i = 0; i < SkillCount; i++)
            {
                if (_skillCooldown[i] > 0.0)
                    _skillCooldown[i] = Math.Max(0.0, _skillCooldown[i] - dt);

                if (_skillActive[i] <= 0.0)
                    continue;

                double step = Math.Min(dt, _skillActive[i]);
                _skillActive[i] = Math.Max(0.0, _skillActive[i] - dt);

                switch ((EUndeadSkill)i)
                {
                    case EUndeadSkill.Dash:
                        StepDash(step);

                        if (_skillActive[i] <= 0.0)
                            EndDash();

                        break;

                    case EUndeadSkill.BlazingTrail:
                        StepTrail(step);
                        break;

                    case EUndeadSkill.FlashMove:
                        if (_skillActive[i] <= 0.0)
                            SimulationScale = 1.0;

                        break;
                }
            }

            // 자취는 스킬이 끝난 뒤에도 «남은 조각»이 사라질 때까지 돈다
            StepTrailSegments(dt);
        }

        // ══════════════════════════════ 돌진

        /// <summary>
        /// [소스 <c>canActivate</c>] — <b>방향이 0 이면 못 쓴다.</b> 멈춘 채로는 돌진이 안 나간다.
        /// </summary>
        private bool BeginDash()
        {
            UndeadVec2 dir = HeroLastDirection.Normalized;

            if (dir.X == 0.0 && dir.Y == 0.0)
                return false;

            _dashDirection = dir;
            _dashHits.Clear();
            DashHitTargets();
            return true;
        }

        /// <summary>
        /// [소스 <c>onActiveUpdate</c>] — 전체 <c>300</c> 을 지속시간에 나눠 가고,
        /// <b>4 씩 쪼개 옮기며</b> 매 조각마다 겹친 적을 때린다. 벽에 막히면 <b>거기서 끝난다</b>.
        /// </summary>
        private void StepDash(double dt)
        {
            double active = ActiveSecondsOf(EUndeadSkill.Dash);
            double travel = active > 0.0 ? _config.DashDistance * dt / active : 0.0;

            double totalX = _dashDirection.X * travel;
            double totalY = _dashDirection.Y * travel;
            double length = Math.Sqrt(totalX * totalX + totalY * totalY);
            int steps = Math.Max(1, (int)Math.Ceiling(length / _config.DashStepUnits));
            double sx = totalX / steps;
            double sy = totalY / steps;

            for (int i = 0; i < steps; i++)
            {
                UndeadVec2 from = HeroPosition;
                UndeadVec2 next = new UndeadVec2(from.X + sx, from.Y + sy);
                HeroPosition = ClampToPassable(from, next);
                DashHitTargets();

                // 막혔으면 «그 자리에서» 끝난다 [소스 — 이동량이 0 이면 finishActiveEffect]
                if (Math.Abs(HeroPosition.X - next.X) > 1e-6 || Math.Abs(HeroPosition.Y - next.Y) > 1e-6)
                {
                    _skillActive[(int)EUndeadSkill.Dash] = 0.0;
                    return;
                }
            }
        }

        /// <summary>지나치는 적을 <b>한 번씩</b> 때린다 — 피해는 총알의 2배 [소스].</summary>
        private void DashHitTargets()
        {
            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false || _dashHits.Contains(i))
                    continue;

                if (Overlaps(_enemies[i].Position, HeroPosition) == false)
                    continue;

                _dashHits.Add(i);
                bool wasAlive = _enemies[i].Health > 0;
                DamageEnemy(i, (int)Math.Round(DamageStat * _config.DashDamageMultiplier), _config.ProjectileKnockback);

                // ★ 과제 지표 — <b>돌진으로</b> 잡은 것만 센다 [소스 <c>enemies_killed_with_dash</c>]
                if (wasAlive && _enemies[i].Active == false)
                    RecordTaskProgress(MetricEnemiesKilledWithDash, 1);
            }
        }

        /// <summary>[소스 <c>onDeactivated</c>] — 끝나는 자리에서 <b>반경 안의 적을 민다</b>.</summary>
        private void EndDash()
        {
            double radiusSq = _config.DashEndKnockbackRadius * _config.DashEndKnockbackRadius;

            for (int i = 0; i < _enemies.Length; i++)
            {
                if (_enemies[i].Active == false)
                    continue;

                UndeadVec2 away = _enemies[i].Position - HeroPosition;
                double distanceSq = away.X * away.X + away.Y * away.Y;

                if (distanceSq > radiusSq || distanceSq <= 0.0)
                    continue;

                double length = Math.Sqrt(distanceSq);
                _enemies[i].Knockback = new UndeadVec2(away.X / length * _config.DashEndKnockbackForce,
                                                       away.Y / length * _config.DashEndKnockbackForce);
            }

            _dashHits.Clear();
        }

        // ══════════════════════════════ 화염 자취

        private void BeginTrail()
        {
            _trailLastHeroPosition = HeroPosition;
            _trailDistanceSinceSegment = 0.0;
            AddTrailSegment(HeroPosition);
        }

        /// <summary>[소스 <c>sampleHeroMovement</c>] — <b>24 만큼 갈 때마다</b> 조각을 하나 남긴다.</summary>
        private void StepTrail(double dt)
        {
            UndeadVec2 now = HeroPosition;
            double dx = now.X - _trailLastHeroPosition.X;
            double dy = now.Y - _trailLastHeroPosition.Y;
            double moved = Math.Sqrt(dx * dx + dy * dy);

            if (moved <= 0.0)
                return;

            double ux = dx / moved;
            double uy = dy / moved;

            for (double at = _config.TrailSegmentSpacing - _trailDistanceSinceSegment; at <= moved; at += _config.TrailSegmentSpacing)
            {
                AddTrailSegment(new UndeadVec2(_trailLastHeroPosition.X + ux * at,
                                               _trailLastHeroPosition.Y + uy * at));
                _trailDistanceSinceSegment = 0.0;
            }

            _trailDistanceSinceSegment += moved % _config.TrailSegmentSpacing;
            _trailLastHeroPosition = now;
        }

        private void AddTrailSegment(UndeadVec2 at)
        {
            for (int i = 0; i < _trail.Length; i++)
            {
                if (_trail[i].Active)
                    continue;

                _trail[i] = new TrailSegment { Active = true, Position = at, AgeSeconds = 0.0 };
                return;
            }
        }

        /// <summary>조각이 나이를 먹고, 닿은 적을 <b>0.5초에 한 번씩</b> 태운다 [소스].</summary>
        private void StepTrailSegments(double dt)
        {
            for (int i = 0; i < _trailThrottle.Length; i++)
            {
                if (_trailThrottle[i] > 0.0)
                    _trailThrottle[i] = Math.Max(0.0, _trailThrottle[i] - dt);
            }

            int damage = Math.Max(1, (int)Math.Round(DamageStat * _config.TrailDamageMultiplier));

            for (int i = 0; i < _trail.Length; i++)
            {
                if (_trail[i].Active == false)
                    continue;

                _trail[i].AgeSeconds += dt;

                if (_trail[i].AgeSeconds >= _config.TrailSegmentLifeSeconds)
                {
                    _trail[i].Active = false;
                    continue;
                }

                for (int e = 0; e < _enemies.Length; e++)
                {
                    if (_enemies[e].Active == false || _trailThrottle[e] > 0.0)
                        continue;

                    if (CircleHitsEnemy(_trail[i].Position, _config.TrailSegmentRadius, _enemies[e].Position) == false)
                        continue;

                    _trailThrottle[e] = _config.TrailTargetThrottleSeconds;
                    DamageEnemy(e, damage, 0.0);
                }
            }
        }

        // ══════════════════════════════ 거들

        private static bool IsSkill(EUndeadSkill skill)
        {
            return skill >= 0 && (int)skill < SkillCount;
        }

        private double ActiveSecondsOf(EUndeadSkill skill)
        {
            return IsSkill(skill) ? _config.SkillActiveSeconds[(int)skill] : 0.0;
        }

        private void ResetSkills()
        {
            Array.Clear(_skillCooldown, 0, _skillCooldown.Length);
            Array.Clear(_skillActive, 0, _skillActive.Length);
            Array.Clear(_trail, 0, _trail.Length);
            Array.Clear(_trailThrottle, 0, _trailThrottle.Length);
            _dashHits.Clear();
            SimulationScale = 1.0;

            // ⚠ «소유»는 지우지 않는다 — 과제 보상은 판을 넘어 남는다 [소스 <c>ownedSkills</c> 는 meta 상태다]
        }
    }
}
