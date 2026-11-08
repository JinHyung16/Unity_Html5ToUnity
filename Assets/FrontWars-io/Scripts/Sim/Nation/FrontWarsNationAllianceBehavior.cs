using System.Collections.Generic;
using JinHyung.Data;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 동맹 도우미 — 요청 받기 · 연장 · 요청 보내기 · 배신 `[소스] Cq3gr8XO.js:Ef=class{`.
    ///
    /// <para>
    /// 「동맹을 원하나」 는 점수를 더해 0 보다 크면 원한다. 정책 값은 난이도 표의 Alliance* 열 `[소스] wf={`.
    /// 난수 순서: (팀전 거부권 — 팀전일 때만) → 동맹 상한 → 잡음 → 반응 이모지.
    /// </para>
    /// </summary>
    public sealed class FrontWarsNationAllianceBehavior
    {
        /// <summary>점수 조정 `[소스] e.isTraitor()&&(r+=-.85) · (r+=-1.2) · (r+=-.8) · (r+=.35)`.</summary>
        private const double TraitorPenalty = -0.85;
        private const double OverCapPenalty = -1.2;
        private const double OverAlliedPenalty = -0.8;
        private const double ComparableStrengthBonus = 0.35;

        /// <summary>팀전 거부권 — 난수 0~99 가 teamVeto×100 미만이면 −2 `[소스] this.random.nextInt(0,100)<n.teamVeto*100&&(r-=2)`.</summary>
        private const int TeamVetoRoll = 100;
        private const double TeamVetoPenalty = -2;

        /// <summary>
        /// 관계 이름 가중치 `[소스] Tf={[w.Hostile]:-.6,[w.Distrustful]:-.3,[w.Neutral]:0,[w.Friendly]:.45}`.
        /// <para>표 FrontWarsAttitudeTable.AllianceWeight 와 같은 값 — 시뮬 설정이 그 표를 들고 있지 않아 상수로 둔다.</para>
        /// </summary>
        private const double HostileWeight = -0.6;
        private const double DistrustfulWeight = -0.3;
        private const double NeutralWeight = 0;
        private const double FriendlyWeight = 0.45;

        /// <summary>잡음 = 난수 −100~100 / 100 × noise `[소스] this.random.nextInt(-100,101)/100*n.noise`.</summary>
        private const int NoiseMin = -100;
        private const int NoiseMaxExclusive = 101;
        private const double NoiseScale = 100;

        /// <summary>스폰 단계 + 1틱 안에 만든 요청은 거절 `[소스] e.createdAt()<=this.game.config().numSpawnPhaseTurns()+1`.</summary>
        private const int RequestGraceTicks = 1;

        /// <summary>적마다 요청을 보낼 확률 1/30 `[소스] maybeSendAllianceRequests(e){…this.random.chance(30)`.</summary>
        private const int RequestChance = 30;

        /// <summary>반응 이모지 확률 `[소스] reactWithEmoji(e,t,n,r){`.</summary>
        private const int OverpoweredEmojiChance = 6;
        private const int HandshakeEmojiChance = 4;
        private const int ConfusedEmojiChance = 4;

        /// <summary>압도당함 `[소스] isOverpoweredBy(e){return n>t*2||n>t&&(…>…*1.75||…>…*1.5)}`.</summary>
        private const double OverpowerTroopsMul = 2;
        private const double OverpowerMaxTroopsMul = 1.75;
        private const double OverpowerTilesMul = 1.5;

        /// <summary>힘이 비슷함 `[소스] isComparableStrength(e){…(t+.1)&&r>n*.5}`.</summary>
        private const double StrengthTilesBarOffset = 0.1;
        private const double StrengthHalf = 0.5;

        /// <summary>배신 `[소스] maybeBetray(e,t){`.</summary>
        private const double BetrayTroopsMul = 10;
        private const double BetrayCrippledShare = 0.25;
        private const double BetrayTraitorTroopsMul = 1.2;
        private const int BetrayLoneNeighborCount = 1;
        private const double BetrayLoneTroopsMul = 3;

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly FrontWarsDifficultyData _policy;
        private readonly FrontWarsNationEmojiBehavior _emoji;

        public FrontWarsNationAllianceBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player, FrontWarsNationEmojiBehavior emoji)
        {
            _random = random;
            _game = game;
            _player = player;
            _emoji = emoji;
            _policy = game.Settings.DifficultyRow;
        }

        /// <summary>받은 요청마다 — 스폰 직후 요청은 거절, 아니면 원하면 수락 `[소스] handleAllianceRequests(){`.</summary>
        public void HandleAllianceRequests()
        {
            if (_game.Settings.DisableAlliances)
                return;

            foreach (FrontWarsAllianceRequest request in _player.IncomingAllianceRequests())
            {
                if (request.CreatedAt <= _game.Settings.SpawnPhaseTurns + RequestGraceTicks)
                {
                    request.Reject();
                    continue;
                }

                if (WantsAllianceWith(request.Requestor, true))
                    request.Accept();
                else
                    request.Reject();
            }
        }

        /// <summary>한쪽만 연장에 동의한 동맹이면 원할 때 연장 `[소스] handleAllianceExtensionRequests(){`.</summary>
        public void HandleAllianceExtensionRequests()
        {
            if (_game.Settings.DisableAlliances)
                return;

            foreach (FrontWarsAlliance alliance in _player.Alliances())
            {
                if (alliance.OnlyOneAgreedToExtend == false)
                    continue;

                FrontWarsPlayer other = alliance.Other(_player);

                if (WantsAllianceWith(other, true))
                    _game.AddExecution(new FrontWarsAllianceExtensionExecution(_player, other.Id));
            }
        }

        /// <summary>적마다 1/30 — 봇은 쉬움일 때만, 보낼 수 있고 원하면 요청 `[소스] maybeSendAllianceRequests(e){`.</summary>
        public void MaybeSendAllianceRequests(IReadOnlyList<FrontWarsPlayer> candidates)
        {
            if (_game.Settings.DisableAlliances)
                return;

            bool easy = _game.Settings.Difficulty == EFrontWarsDifficulty.Easy;

            foreach (FrontWarsPlayer candidate in candidates)
            {
                if (_random.Chance(RequestChance) == false)
                    continue;

                if (candidate.Type == EFrontWarsPlayerType.Bot && easy == false)
                    continue;

                if (_player.CanSendAllianceRequest(candidate) && WantsAllianceWith(candidate, false))
                    _game.AddExecution(new FrontWarsAllianceRequestExecution(_player, candidate.Id));
            }
        }

        /// <param name="incomingRequest">받은 요청(연장 포함)에 답하는 중이면 참, 보내려는 중이면 거짓 — 반응 이모지가 갈린다.</param>
        public bool WantsAllianceWith(FrontWarsPlayer other, bool incomingRequest)
        {
            double score = _policy.AllianceBaseline;

            if (other.IsTraitor)
                score += TraitorPenalty;

            score += AttitudeWeight(_player.Relation(other));

            bool overpowered = _policy.AllianceFear > 0 && IsOverpoweredBy(other);
            if (overpowered)
                score += _policy.AllianceFear;

            // 팀전 거부권 — 팀전일 때만 난수를 굴린다 `[소스] gameMode===b.Team&&this.random.nextInt(0,100)<n.teamVeto*100&&(r-=2)`
            if (_game.Settings.IsTeamMode && _random.NextInt(0, TeamVetoRoll) < _policy.AllianceTeamVeto * TeamVetoRoll)
                score += TeamVetoPenalty;

            if (_game.Ticks - _game.Settings.SpawnPhaseTurns < _policy.AllianceEarlyWindowTicks)
                score += _policy.AllianceEarlyBonus;

            int cap = _random.NextInt(_policy.AllianceCapMin, _policy.AllianceCapMaxExclusive);
            if (_player.Alliances().Count >= cap)
                score += OverCapPenalty;

            if (_policy.AlliancePartnerShare > 0 && IsOverAllied(other))
                score += OverAlliedPenalty;

            if (IsComparableStrength(other))
                score += ComparableStrengthBonus;

            if (_policy.AllianceNoise > 0)
                score += _random.NextInt(NoiseMin, NoiseMaxExclusive) / NoiseScale * _policy.AllianceNoise;

            bool wants = score > 0;
            ReactWithEmoji(other, wants, overpowered, incomingRequest);
            return wants;
        }

        private static double AttitudeWeight(EFrontWarsRelation relation)
        {
            switch (relation)
            {
                case EFrontWarsRelation.Hostile: return HostileWeight;
                case EFrontWarsRelation.Distrustful: return DistrustfulWeight;
                case EFrontWarsRelation.Neutral: return NeutralWeight;
                default: return FriendlyWeight;
            }
        }

        /// <summary>
        /// 원함 + 압도당함 → 받은 요청이면 1/6 사랑 · 보낼 때면 1/6 겁먹음. 원함 → 1/4 악수. 받은 요청 거절 → 1/4 혼란 `[소스] reactWithEmoji(e,t,n,r){`.
        /// </summary>
        private void ReactWithEmoji(FrontWarsPlayer other, bool wants, bool overpowered, bool incomingRequest)
        {
            if (_emoji == null)
                return;

            if (wants && overpowered)
            {
                if (_random.Chance(OverpoweredEmojiChance) == false)
                    return;

                _emoji.SendAllianceEmoji(other, incomingRequest ? EFrontWarsNationAllianceEmoji.Love : EFrontWarsNationAllianceEmoji.ScaredOfThreat);
                return;
            }

            if (wants)
            {
                if (_random.Chance(HandshakeEmojiChance))
                    _emoji.SendAllianceEmoji(other, EFrontWarsNationAllianceEmoji.Handshake);

                return;
            }

            if (incomingRequest && _random.Chance(ConfusedEmojiChance))
                _emoji.SendAllianceEmoji(other, EFrontWarsNationAllianceEmoji.Confused);
        }

        /// <summary>병력 + 나가는 공격 병력 `[소스] strengthOf(e){`.</summary>
        public static double StrengthOf(FrontWarsPlayer player)
        {
            double strength = player.Troops;

            foreach (FrontWarsAttack attack in player.OutgoingAttacks)
                strength += attack.Troops;

            return strength;
        }

        private bool IsOverpoweredBy(FrontWarsPlayer other)
        {
            double mine = _player.Troops;
            double theirs = other.Troops;

            return theirs > mine * OverpowerTroopsMul
                   || (theirs > mine
                       && (_game.Rules.MaxTroops(other) > _game.Rules.MaxTroops(_player) * OverpowerMaxTroopsMul
                           || other.NumTilesOwned > _player.NumTilesOwned * OverpowerTilesMul));
        }

        /// <summary>상대 동맹 수 ≥ 봇 아닌 플레이어 수 × 비율 `[소스] isOverAllied(e){`.</summary>
        private bool IsOverAllied(FrontWarsPlayer other)
        {
            int nonBots = 0;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Type != EFrontWarsPlayerType.Bot)
                    nonBots++;
            }

            return other.Alliances().Count >= nonBots * _policy.AlliancePartnerShare;
        }

        private bool IsComparableStrength(FrontWarsPlayer other)
        {
            double bar = _policy.AllianceStrengthBar;
            double mine = StrengthOf(_player);
            double theirs = StrengthOf(other);

            return theirs > mine * bar
                   || (other.NumTilesOwned > _player.NumTilesOwned * (bar + StrengthTilesBarOffset) && theirs > mine * StrengthHalf);
        }

        /// <summary>
        /// 동맹인 상대를 배신할까 — 참이면 이미 동맹을 깼다 `[소스] maybeBetray(e,t){`.
        /// </summary>
        /// <param name="neighborCount">국경 이웃 플레이어 수 (우호 + 적).</param>
        public bool MaybeBetray(FrontWarsPlayer other, int neighborCount)
        {
            if (_player.IsAlliedWith(other) == false)
                return false;

            EFrontWarsDifficulty difficulty = _game.Settings.Difficulty;
            bool low = difficulty == EFrontWarsDifficulty.Easy || difficulty == EFrontWarsDifficulty.Medium;
            double myStrength = StrengthOf(_player);

            if (low)
            {
                if ((difficulty != EFrontWarsDifficulty.Easy || other.Type != EFrontWarsPlayerType.Human)
                    && _player.Troops >= other.Troops * BetrayTroopsMul)
                    return Betray(other);
            }
            else
            {
                double otherMax = _game.Rules.MaxTroops(other);

                if (StrengthOf(other) < otherMax * BetrayCrippledShare && other.Troops < _player.Troops)
                    return Betray(other);
            }

            if (difficulty != EFrontWarsDifficulty.Easy
                && ((other.IsTraitor && other.Troops < _player.Troops * BetrayTraitorTroopsMul)
                    || (neighborCount == BetrayLoneNeighborCount && other.Troops * BetrayLoneTroopsMul < myStrength)))
                return Betray(other);

            return false;
        }

        private bool Betray(FrontWarsPlayer other)
        {
            FrontWarsAlliance alliance = _player.AllianceWith(other);
            if (alliance == null)
                return false;

            _player.BreakAlliance(alliance);
            return true;
        }
    }
}
