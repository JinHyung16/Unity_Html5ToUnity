using System;
using System.Text;

namespace JinHyung.Data
{
    /// <summary>
    /// Undead Slayer 전역 설정 — 행 1개짜리 표.
    ///
    /// <para>
    /// ★★ <b>근거 등급을 검사가 안다.</b> <c>~Samples</c> 가
    /// <b><c>-1</c> 이면 «소스 직독»</b>(저작 상수 그대로 · 표본 검사 면제) ·
    /// <b>양수면 실측</b>(<c>n ≥ 3</c> 이어야 한다) · <b><c>0</c> 이면 미측정</b>이다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 어설션 값을 <b>여기 두 벌로 적지 않는다</b> — 표의 값과 대조할 «불변식»만 둔다
    /// (타일 한 칸에서 유도되는 것 · 0 이면 안 되는 것 · 서로 맞물려야 하는 것).
    /// </para>
    /// </summary>
    public class UndeadConfigDataContainer : DictionaryContainer<int, UndeadConfigData>
    {
        private const int OriginRowCount = 1;

        /// <summary><c>~Samples</c> 가 이 값이면 <b>원본 소스에서 직접 읽은 저작 상수</b>다.</summary>
        public const int SourceRead = -1;

        public override string Name
        {
            get { return "UndeadConfigTable"; }
        }

        /// <summary>이 게임의 유일한 설정 행. 없으면 <c>null</c> — <b>폴백을 만들지 않는다</b>.</summary>
        public UndeadConfigData Config
        {
            get { return Get(1); }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 {OriginRowCount}");

            UndeadConfigData config = Config;

            if (config == null)
            {
                errorMessage = sb.AppendLine("Id 1 행이 없다").ToString();
                return false;
            }

            // ── 근거 등급 : -1(소스 직독) 이거나 n≥3(실측) 이어야 한다
            AppendEvidenceError(sb, nameof(config.HeroMoveSpeedWorld), config.HeroMoveSpeedSamples);
            AppendEvidenceError(sb, nameof(config.FireIntervalSeconds), config.FireIntervalSamples);
            AppendEvidenceError(sb, nameof(config.ProjectileSpeedWorld), config.ProjectileSpeedSamples);
            AppendEvidenceError(sb, nameof(config.ProjectileDamage), config.ProjectileDamageSamples);

            // ── 0 이면 «안 옮긴 것»인 값들. 폴백을 깔지 않고 여기서 터뜨린다.
            AppendZeroError(sb, nameof(config.HeroMoveSpeedWorld), config.HeroMoveSpeedWorld);
            AppendZeroError(sb, nameof(config.FireIntervalSeconds), config.FireIntervalSeconds);
            AppendZeroError(sb, nameof(config.CollectRadiusWorld), config.CollectRadiusWorld);
            AppendZeroError(sb, nameof(config.HeroInvincibleSeconds), config.HeroInvincibleSeconds);
            AppendZeroError(sb, nameof(config.EnemyAttackIntervalSeconds), config.EnemyAttackIntervalSeconds);
            AppendZeroError(sb, nameof(config.CameraSmoothing), config.CameraSmoothing);
            AppendZeroError(sb, nameof(config.ThreatBudgetPerFrame), config.ThreatBudgetPerFrame);
            AppendZeroError(sb, nameof(config.QuestRescueRadius), config.QuestRescueRadius);

            if (config.HeroMaxHp <= 0)
                sb.AppendLine($"히어로 최대 체력 {config.HeroMaxHp} 가 0 이하다 — 죽지 않거나 즉사하는 게임이 된다");

            if (config.EnemyContactDamage <= 0)
                sb.AppendLine($"접촉 피해 {config.EnemyContactDamage} 가 0 이하다 — 적이 히어로를 못 죽인다");

            if (config.ProjectileDamage <= 0)
                sb.AppendLine($"투사체 피해 {config.ProjectileDamage} 가 0 이하다");

            // ── 서로 맞물려야 하는 것
            if (config.HeroColliderW <= 0.0 || config.HeroColliderH <= 0.0)
                sb.AppendLine("히어로 충돌 상자의 폭·높이가 0 이하다 — 접촉 판정이 영영 안 난다");

            if (config.EnemyColliderW <= 0.0 || config.EnemyColliderH <= 0.0)
                sb.AppendLine("적 충돌 상자의 폭·높이가 0 이하다");

            // ★★ 1 m 는 «타일 한 칸»에서 유도된다 — 자유 적합의 소수(23.75)를 박으면
            //   그 회차의 관측 잡음까지 굳는다(재발방지 #105). 둘이 갈리면 한쪽이 틀린 것이다.
            if (config.QuestMeterPixels != config.TerrainTileWorld)
                sb.AppendLine($"1m = {config.QuestMeterPixels}px 인데 타일 한 칸은 {config.TerrainTileWorld}px 다 — "
                              + "거리 표시는 «타일 한 칸 = 1m»에서 유도된다");

            if (config.WarriorOffsetX == 0.0 && config.WarriorOffsetY == 0.0)
                sb.AppendLine("전사 오프셋이 (0,0) 이다 — 히어로 발밑에 목표가 생긴다");

            if (config.MageMeetRadius <= 0.0)
                sb.AppendLine("마법사 대화 반경이 0 이하다 — 퀘스트가 영영 안 열린다");

            if (config.HeroStartX == 0.0 && config.HeroStartY == 0.0)
                sb.AppendLine("시작 좌표가 (0,0) 이다 — 원본은 바이옴 1 에서 (1620, 1010) 이다");

            // 지형 타일은 환산 상수의 뿌리다 — 흔들리면 Tilemap 셀이 1 유닛이 아니게 된다.
            if (config.TerrainTileWorld != 24)
                sb.AppendLine($"지형 타일 {config.TerrainTileWorld} — 원본 소스 24");

            if (config.DesignViewportHeight != 580)
                sb.AppendLine($"설계 세로 {config.DesignViewportHeight} — 원본 실측 580 (확정표 3-d)");

            if (config.FrameTicksPerSecond != 60)
                sb.AppendLine($"원본 틱 {config.FrameTicksPerSecond} — 실측 60");

            // 애니 fps 는 원본 재생 속도(0.2/틱)에서 «유도»된다 — 틱과 맞물려야 한다.
            double derivedFps = config.FrameTicksPerSecond * 0.2;

            if (Math.Abs(config.AnimationFps - derivedFps) > 0.01)
                sb.AppendLine($"애니 fps {config.AnimationFps} 가 «틱 × 0.2»({derivedFps}) 와 다르다");

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        /// <summary>근거 등급 검사 — <b>소스 직독(-1)</b> 이거나 <b>실측(n≥3)</b> 이어야 한다.</summary>
        private static void AppendEvidenceError(StringBuilder sb, string column, int samples)
        {
            if (samples == SourceRead || samples >= 3)
                return;

            sb.AppendLine($"{column}: 근거 {samples} — «소스 직독(-1)» 이거나 «실측(n≥3)» 이어야 한다");
        }

        private static void AppendZeroError(StringBuilder sb, string column, double value)
        {
            if (value != 0.0)
                return;

            sb.AppendLine($"{column} 이 0 이다 — 안 옮긴 값이면 그 계산이 통째로 죽는다");
        }
    }
}
