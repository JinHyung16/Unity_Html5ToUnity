namespace JinHyung.Data
{
    /// <summary>
    /// 난이도 한 단계 — 네이션 AI 와 병력 식이 난이도로 갈리는 값 전부 (`03_데이터.md` 난이도 · 네이션 표 · `02_시스템_AI.md`).
    /// <para>원본에서는 여러 표로 흩어져 있다(`maxTroops(` · `attackRateRange` · `baseline:` · `hesitation:` · `strikeBack` · `mp=` · `up=` · `dp=` · `ap=`). 난이도를 한 줄에서 조정하게 한 표로 모았다.</para>
    /// </summary>
    public class FrontWarsDifficultyData : IData, IDataKey<string>
    {
        /// <summary>Easy · Medium · Hard · Impossible.</summary>
        public string Code { get; set; }

        public int Order { get; set; }

        // ── 병력 `[소스] startManpower( · maxTroops( · troopIncreaseRate(`
        public int NationStartTroops { get; set; }
        public double NationMaxTroopsMul { get; set; }
        public double NationTroopIncreaseMul { get; set; }
        public int DifficultyModifier { get; set; }

        // ── 네이션 행동 주기 `[소스] attackRateRange`
        public int NationAttackRateMin { get; set; }
        public int NationAttackRateMaxExclusive { get; set; }
        public double NationTriggerRatioDelta { get; set; }

        // ── 동맹 정책 `[소스] baseline:`
        public double AllianceBaseline { get; set; }
        public double AllianceNoise { get; set; }
        public double AllianceFear { get; set; }
        public int AllianceEarlyWindowTicks { get; set; }
        public double AllianceEarlyBonus { get; set; }
        public double AllianceTeamVeto { get; set; }
        public int AllianceCapMin { get; set; }
        public int AllianceCapMaxExclusive { get; set; }
        public double AlliancePartnerShare { get; set; }
        public double AllianceStrengthBar { get; set; }

        // ── MIRV 교리 `[소스] hesitation:`
        public int MirvHesitation { get; set; }
        public double MirvSoloDenialShare { get; set; }
        public double MirvTeamDenialShare { get; set; }
        public double MirvCityGap { get; set; }
        public int MirvCityFloor { get; set; }

        /// <summary>행동 목록 — 우선순위 순, `;` 로 나눈다 `[소스] strikeBack`.</summary>
        public string Plays { get; set; }

        /// <summary>금수를 풀어 주는 관계 이름 (Neutral · Friendly · 빈칸 = 안 풀어 줌) `[소스] EMBARGO_FORGIVE_AT`.</summary>
        public string EmbargoForgiveAt { get; set; }

        // ── 건물 · 핵 · 전함 · 공격 도우미
        public double SamPerCity { get; set; }
        public int RandomUpgradePercent { get; set; }
        public int RailScorePercent { get; set; }
        public int WarshipRetaliatePercent { get; set; }
        public double RunawayCrownShare { get; set; }

        /// <summary>보내기 상한 비율. 0 이면 상한 없음.</summary>
        public double TroopSendCapShare { get; set; }

        /// <summary>봇 소탕 폭 — 보통은 1 또는 2 를 1/2 확률로.</summary>
        public int BotSweepMin { get; set; }
        public int BotSweepMax { get; set; }

        public int NukeAimSamples { get; set; }

        /// <summary>SAM 이 요격 못 하는 조준을 찾을 앞쪽 후보 수. 0 이면 1등을 그냥 쓴다.</summary>
        public int NukeSamAvoidTop { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
