namespace JinHyung.FrontWarsIo
{
    /// <summary>화면 이벤트 종류 — 이벤트 패널 분류가 이것으로 갈린다 `[소스] Cq3gr8XO.js:let D=function(e){`.</summary>
    public enum EFrontWarsMessage
    {
        AttackFailed,
        AttackCancelled,
        AttackRequest,
        ConqueredPlayer,
        MirvInbound,
        NukeInbound,
        HydrogenBombInbound,
        NavalInvasionInbound,
        SamMiss,
        SamHit,
        CapturedEnemyUnit,
        UnitCapturedByEnemy,
        UnitDestroyed,
        AllianceAccepted,
        AllianceRejected,
        AllianceRequest,
        AllianceBroken,
        AllianceExpired,
        SentGoldToPlayer,
        ReceivedGoldFromPlayer,
        ReceivedGoldFromTrade,
        SentTroopsToPlayer,
        ReceivedTroopsFromPlayer,
        Chat,
        RenewAlliance
    }

    /// <summary>두 플레이어 사이 관계 종류 — 기차 골드가 이것으로 갈린다 `[소스] Cq3gr8XO.js:re=function(e){return e.Self=`self`… · function vd(e,t){`.</summary>
    public enum EFrontWarsRelationKind
    {
        Self,
        Team,
        Ally,
        Other
    }

    public static class FrontWarsRelationKinds
    {
        /// <summary>같으면 자기 · 같은 팀 · 동맹 · 남 순서로 가른다.</summary>
        public static EFrontWarsRelationKind Of(FrontWarsPlayer a, FrontWarsPlayer b)
        {
            if (a == b)
                return EFrontWarsRelationKind.Self;

            if (a.IsOnSameTeam(b))
                return EFrontWarsRelationKind.Team;

            return a.IsAlliedWith(b) ? EFrontWarsRelationKind.Ally : EFrontWarsRelationKind.Other;
        }
    }

    /// <summary>화면 이벤트 하나 — 받는 플레이어 · 양 · 상대 이름.</summary>
    public sealed class FrontWarsMessage
    {
        public EFrontWarsMessage Type { get; }

        /// <summary>받는 플레이어. null = 모두.</summary>
        public FrontWarsPlayer Recipient { get; }

        /// <summary>병력 · 골드 양 (없으면 0).</summary>
        public double Amount { get; }

        public string OtherName { get; }
        public int Tick { get; }

        public FrontWarsMessage(EFrontWarsMessage type, FrontWarsPlayer recipient, double amount, string otherName, int tick)
        {
            Type = type;
            Recipient = recipient;
            Amount = amount;
            OtherName = otherName;
            Tick = tick;
        }
    }
}
