namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 플레이어 명령 — 화면 입력이 턴에 실려 시뮬로 가는 모양 `[소스] Cq3gr8XO.js:createExec(e){switch(e.type){`.
    /// <para>싱글도 같은 길이다. 명령은 다음 턴 시작에 실행으로 바뀌어 그 틱 끝에 init 된다.</para>
    /// </summary>
    public abstract class FrontWarsIntent
    {
        /// <summary>보낸 사람 — 싱글에서는 로컬 사람.</summary>
        public string ClientId { get; set; }
    }

    public sealed class FrontWarsAttackIntent : FrontWarsIntent
    {
        /// <summary>null = 주인 없는 땅.</summary>
        public string TargetId;

        /// <summary>null = 설정 비율.</summary>
        public double? Troops;
    }

    public sealed class FrontWarsCancelAttackIntent : FrontWarsIntent
    {
        public string AttackId;
    }

    public sealed class FrontWarsCancelBoatIntent : FrontWarsIntent
    {
        public int UnitId;
    }

    public sealed class FrontWarsMoveWarshipIntent : FrontWarsIntent
    {
        public int UnitId;
        public int Tile;
    }

    public sealed class FrontWarsSpawnIntent : FrontWarsIntent
    {
        public int Tile;
    }

    public sealed class FrontWarsBoatIntent : FrontWarsIntent
    {
        public string TargetId;
        public int Destination;
        public double? Troops;

        /// <summary>−1 = 알아서.</summary>
        public int Source = -1;
    }

    public sealed class FrontWarsAllianceRequestIntent : FrontWarsIntent
    {
        public string RecipientId;
    }

    public sealed class FrontWarsAllianceReplyIntent : FrontWarsIntent
    {
        public string RequestorId;
        public bool Accept;
    }

    public sealed class FrontWarsBreakAllianceIntent : FrontWarsIntent
    {
        public string RecipientId;
    }

    public sealed class FrontWarsTargetPlayerIntent : FrontWarsIntent
    {
        public string TargetId;
    }

    public sealed class FrontWarsEmojiIntent : FrontWarsIntent
    {
        /// <summary>null = 모두.</summary>
        public string RecipientId;

        public int Emoji;
    }

    public sealed class FrontWarsDonateTroopsIntent : FrontWarsIntent
    {
        public string RecipientId;
        public double? Troops;
    }

    public sealed class FrontWarsDonateGoldIntent : FrontWarsIntent
    {
        public string RecipientId;
        public double? Gold;
    }

    public sealed class FrontWarsEmbargoIntent : FrontWarsIntent
    {
        public string TargetId;
        public bool Start;
    }

    public sealed class FrontWarsBuildUnitIntent : FrontWarsIntent
    {
        public EFrontWarsUnit Unit;
        public int Tile;
    }

    public sealed class FrontWarsAllianceExtensionIntent : FrontWarsIntent
    {
        public string RecipientId;
    }

    public sealed class FrontWarsUpgradeStructureIntent : FrontWarsIntent
    {
        public int UnitId;
    }

    public sealed class FrontWarsDeleteUnitIntent : FrontWarsIntent
    {
        public int UnitId;
    }

    public sealed class FrontWarsQuickChatIntent : FrontWarsIntent
    {
        public string RecipientId;
        public string Key;
        public string TargetId;
    }

    public sealed class FrontWarsMarkDisconnectedIntent : FrontWarsIntent
    {
        public bool IsDisconnected;
    }

    /// <summary>아무것도 안 하는 실행 `[소스] Cq3gr8XO.js:bp=class{`.</summary>
    public sealed class FrontWarsNoOpExecution : IFrontWarsExecution
    {
        public bool IsActive => false;
        public bool ActiveDuringSpawnPhase => false;

        public void Init(FrontWarsGame game, int tick)
        {
        }

        public void Tick(int tick)
        {
        }
    }

    /// <summary>명령 → 실행 `[소스] Cq3gr8XO.js:Cp=class{…createExec(e){`. 보낸 사람을 못 찾으면 아무것도 안 하는 실행.</summary>
    public static class FrontWarsExecutionFactory
    {
        public static IFrontWarsExecution Create(FrontWarsGame game, FrontWarsIntent intent)
        {
            FrontWarsPlayer p = game.PlayerByClientId(intent.ClientId);

            if (p == null)
                return new FrontWarsNoOpExecution();

            switch (intent)
            {
                case FrontWarsAttackIntent i: return new FrontWarsAttackExecution(i.Troops, p, i.TargetId);
                case FrontWarsCancelAttackIntent i: return new FrontWarsCancelAttackExecution(p, i.AttackId);
                case FrontWarsCancelBoatIntent i: return new FrontWarsCancelBoatExecution(p, i.UnitId);
                case FrontWarsMoveWarshipIntent i: return new FrontWarsMoveWarshipExecution(p, i.UnitId, i.Tile);
                case FrontWarsSpawnIntent i: return game.Settings.RandomSpawn ? (IFrontWarsExecution)new FrontWarsNoOpExecution() : new FrontWarsSpawnExecution(p.Info, i.Tile);
                case FrontWarsBoatIntent i: return new FrontWarsTransportShipExecution(p, i.TargetId, i.Destination, i.Troops, i.Source);
                case FrontWarsAllianceRequestIntent i: return new FrontWarsAllianceRequestExecution(p, i.RecipientId);
                case FrontWarsAllianceReplyIntent i: return new FrontWarsAllianceReplyExecution(i.RequestorId, p, i.Accept);
                case FrontWarsBreakAllianceIntent i: return new FrontWarsBreakAllianceExecution(p, i.RecipientId);
                case FrontWarsTargetPlayerIntent i: return new FrontWarsTargetExecution(p, i.TargetId);
                case FrontWarsEmojiIntent i: return new FrontWarsEmojiExecution(p, i.RecipientId, i.Emoji);
                case FrontWarsDonateTroopsIntent i: return new FrontWarsDonateTroopsExecution(p, i.RecipientId, i.Troops);
                case FrontWarsDonateGoldIntent i: return new FrontWarsDonateGoldExecution(p, i.RecipientId, i.Gold);
                case FrontWarsEmbargoIntent i: return new FrontWarsEmbargoExecution(p, i.TargetId, i.Start);
                case FrontWarsBuildUnitIntent i: return new FrontWarsConstructionExecution(p, i.Unit, i.Tile);
                case FrontWarsAllianceExtensionIntent i: return new FrontWarsAllianceExtensionExecution(p, i.RecipientId);
                case FrontWarsUpgradeStructureIntent i: return new FrontWarsUpgradeStructureExecution(p, i.UnitId);
                case FrontWarsDeleteUnitIntent i: return new FrontWarsDeleteUnitExecution(p, i.UnitId);
                case FrontWarsQuickChatIntent i: return new FrontWarsQuickChatExecution(p, i.RecipientId, i.Key, i.TargetId);
                case FrontWarsMarkDisconnectedIntent i: return new FrontWarsMarkDisconnectedExecution(p, i.IsDisconnected);
                default: return new FrontWarsNoOpExecution();
            }
        }
    }

    /// <summary>끊김 표시 — init 에서 바로 `[소스] Cq3gr8XO.js:_p=class{`.</summary>
    public sealed class FrontWarsMarkDisconnectedExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly bool _disconnected;

        public bool IsActive => false;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsMarkDisconnectedExecution(FrontWarsPlayer player, bool disconnected)
        {
            _player = player;
            _disconnected = disconnected;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _player.MarkDisconnected(_disconnected);
        }

        public void Tick(int tick)
        {
        }
    }
}
