namespace JinHyung.Data
{
    /// <summary>
    /// 적 한 종류 [소스 직독 · 회차 9].
    ///
    /// <para>
    /// ★★ 원본의 스폰은 <b>«위협 예산»</b>이다 — 종류마다 예산이 «차오르고», 비용을 낼 수 있으면 나온다.
    /// 「몇 초에 한 마리」도 「가중치 뽑기」도 아니다(회차 1~8 의 우리 구현이 그랬고, 틀렸다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>박쥐만 웨이브를 탄다</b> — 체력·비용이 <c>23 + 23×웨이브</c> 로 같이 오르고 크기도 커진다.
    /// 그래서 체력·비용·크기가 「기본 + 웨이브당」 두 칸으로 되어 있다.
    /// </para>
    /// </summary>
    public class UndeadEnemyData : IData, IDataKey<int>
    {
        public int Id { get; set; }

        /// <summary>원본의 개체 id (<c>enemy1</c> · <c>enemyBat</c> …).</summary>
        public string Code { get; set; }

        /// <summary>아트 표의 코드 — 원본 <c>textureName</c> 그대로다.</summary>
        public string TextureCode { get; set; }

        public int FrameWidth { get; set; }

        public int FrameHeight { get; set; }

        public int FrameCount { get; set; }

        public double SpriteScaleBase { get; set; }

        /// <summary>웨이브마다 커지는 양 [소스 — 박쥐만 0.35].</summary>
        public double SpriteScalePerWave { get; set; }

        /// <summary>커지는 상한 [소스 — 박쥐 9].</summary>
        public double SpriteScaleMax { get; set; }

        /// <summary>기본 이동 속도 (world px/s). ⚠ 개체마다 <b>±10%</b> 가 곱해진다 [소스].</summary>
        public double BaseMoveSpeedWorld { get; set; }

        /// <summary>최대 체력 [소스].</summary>
        public int MaxHpBase { get; set; }

        /// <summary>웨이브마다 더해지는 체력 [소스 — 박쥐만 23].</summary>
        public int MaxHpPerWave { get; set; }

        /// <summary>이 웨이브부터 나온다 [소스].</summary>
        public int UnlockWave { get; set; }

        /// <summary>한 마리를 내보내는 데 드는 위협 예산 [소스].</summary>
        public int SpawnCostBase { get; set; }

        /// <summary>웨이브마다 더해지는 비용 [소스 — 박쥐만 23].</summary>
        public int SpawnCostPerWave { get; set; }

        /// <summary>
        /// 예산을 쓰는 순서 (작을수록 먼저) [소스 — <c>박쥐 · 망령 · 눈 · 좀비2 · 좀비1</c>].
        /// <para>⚠ 순서가 바뀌면 «비싼 적이 영영 안 나오는» 판이 된다.</para>
        /// </summary>
        public int SpawnPriority { get; set; }

        /// <summary><see cref="BudgetSwitchWave"/> «전»에 기본 예산에 곱하는 값 [소스].</summary>
        public double BudgetMultiplierEarly { get; set; }

        /// <summary>그 웨이브부터 곱하는 값 [소스 — 좀비1 이 12 웨이브에 3배 → 2배로 준다].</summary>
        public double BudgetMultiplierLate { get; set; }

        public int BudgetSwitchWave { get; set; }

        /// <summary>판이 시작할 때 이미 차 있는 예산 [소스 — 좀비1 만 15].</summary>
        public int InitialThreatBudget { get; set; }

        /// <summary>해금되는 그 순간 «한 마리»가 공짜로 나온다 [소스 — 박쥐].</summary>
        public bool SpawnOnUnlock { get; set; }

        public int Key
        {
            get { return Id; }
        }

    }
}
