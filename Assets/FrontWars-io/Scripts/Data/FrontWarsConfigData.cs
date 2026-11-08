namespace JinHyung.Data
{
    /// <summary>
    /// FrontWars 밸런스 상수 — 행 1개짜리 표. 틱 = 100ms.
    /// <para>
    /// 값은 전부 원본 설정 클래스 `[소스] Cq3gr8XO.js` 에서 읽었다 (`03_데이터.md` 「게임 상수」).
    /// 원본 계산식의 고정 계수(병력 식의 0.6 · 1000 · 50000 등)는 여기 두지 않는다 — 사양이라 시뮬 코드 상수다.
    /// </para>
    /// </summary>
    public class FrontWarsConfigData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        public int SpawnPhaseTurnsSingleplayer { get; set; }
        public int SpawnPhaseTurnsMultiplayer { get; set; }
        public int SpawnImmunityTicks { get; set; }
        public int AutoSpawnTicksBeforeEnd { get; set; }
        public int WinPercentFFA { get; set; }
        public int WinPercentTeam { get; set; }
        public int WinCheckIntervalTicks { get; set; }

        public int TraitorDurationTicks { get; set; }
        public double TraitorDefenseDebuff { get; set; }
        public double TraitorSpeedDebuff { get; set; }
        public int AllianceRequestDurationTicks { get; set; }
        public int AllianceRequestCooldownTicks { get; set; }
        public int AllianceDurationTicks { get; set; }
        public int AllianceExtensionPromptOffsetTicks { get; set; }
        public int TemporaryEmbargoDurationTicks { get; set; }
        public int DonateCooldownTicks { get; set; }
        public int DonationDivisor { get; set; }
        public int DeleteUnitCooldownTicks { get; set; }
        public int UpgradeCooldownTicks { get; set; }
        public int EmojiDurationTicks { get; set; }
        public int EmojiCooldownTicks { get; set; }
        public int TargetDurationTicks { get; set; }
        public int TargetCooldownTicks { get; set; }

        public int SamCooldownTicks { get; set; }
        public int SiloCooldownTicks { get; set; }
        public double SamHitChance { get; set; }
        public double SamWarheadHitChance { get; set; }
        public int SamRangeBase { get; set; }
        public int SamMissileSpeed { get; set; }
        public int NukeSpeed { get; set; }
        public int NukeTargetableRange { get; set; }
        public int NukeAllianceBreakTiles { get; set; }

        public int DefensePostRange { get; set; }
        public double DefensePostDefenseBonus { get; set; }
        public double DefensePostSpeedBonus { get; set; }
        public int CityTroopIncrease { get; set; }
        public int StructureMinDistance { get; set; }

        public int ShellLifetimeTicks { get; set; }
        public int WarshipShellLifetimeTicks { get; set; }
        public int WarshipPatrolRange { get; set; }
        public int WarshipTargetingRange { get; set; }
        public int WarshipShellIntervalTicks { get; set; }
        public int SafeFromPiratesTicks { get; set; }
        public int BoatMaxNumber { get; set; }
        public int BoatAttackDivisor { get; set; }
        public int PortSpawnRadius { get; set; }
        public int TrainStationMinRange { get; set; }
        public int TrainStationMaxRange { get; set; }
        public int RailroadMaxSize { get; set; }

        public int GoldPerTickHuman { get; set; }
        public int GoldPerTickBot { get; set; }
        public long StartingGoldUnit { get; set; }
        public int HumanStartTroops { get; set; }
        public int BotStartTroops { get; set; }
        public int AttackAmountDivisorHuman { get; set; }
        public int AttackAmountDivisorBot { get; set; }
        public int RetreatLossPercent { get; set; }
        public int CancelAttackDelayTicks { get; set; }
        public int DeadDefenderTiles { get; set; }
        public int ClusterCheckIntervalTicks { get; set; }
        public double RelationDecayPerTick { get; set; }
        public int BotSpawnMinDistance { get; set; }

        public int Key
        {
            get { return Id; }
        }
    }
}
