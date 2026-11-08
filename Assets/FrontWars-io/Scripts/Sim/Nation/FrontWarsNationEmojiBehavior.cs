using System.Collections.Generic;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 네이션 이모지 · 빠른 채팅 도우미 `[소스] Cq3gr8XO.js:Vu=class{`.
    ///
    /// <para>
    /// 공격 틱마다 8가지 가벼운 반응을 차례로 굴리고, 다른 도우미가 공격 · 동맹 · 핵 · 전함 때 부른다.
    /// 받는 쪽이 사람이 아니면(모두에게 제외) 보내지 않는다. 한 사람에게는 300틱에 한 번(이모지) · 900틱에 한 번(채팅)만.
    /// </para>
    /// <para>난수는 네이션 실행의 난수를 같이 쓴다. 받는 쪽 <c>null</c> = 모두 (원본 <c>AllPlayers</c>).</para>
    /// </summary>
    public sealed class FrontWarsNationEmojiBehavior
    {
        /// <summary>도움 요청 — 1/16 확률 · 받는 공격 합 ≥ 내 병력×3 · 가장 큰 공격자에게 1/4 채팅 `[소스] cryForHelpWhenOverwhelmed(){if(!this.random.chance(16))`.</summary>
        private const int HelpChance = 16;
        private const double HelpTroopsMul = 3;
        private const int HelpChatChance = 4;

        /// <summary>작은 공격 조롱 — 1/8 · 공격 병력 &lt; 내 병력×0.1 · 1/2 로 목록 고르기 `[소스] mockTinyAttacks(){if(!this.random.chance(8))`.</summary>
        private const int MockChance = 8;
        private const double MockTroopShare = 0.1;
        private const int MockListChance = 2;

        /// <summary>왕관 자랑 1/300 `[소스] flexCrown(){if(!this.random.chance(300))`.</summary>
        private const int CrownChance = 300;

        /// <summary>동맹 아부 1/250 · 사랑 목록 1/3 `[소스] flatterAllies(){if(!this.random.chance(250))`.</summary>
        private const int FlatterChance = 250;
        private const int FlatterLoveChance = 3;

        /// <summary>배신자 야유 1/40 `[소스] heckleTraitors(){if(!this.random.chance(40))`.</summary>
        private const int HeckleChance = 40;

        /// <summary>쥐 지목 — 6000틱 뒤 · 1/10000 · 맵 땅의 1% 미만 `[소스] callOutRats(){if(this.game.ticks()<6e3||!this.random.chance(1e4))`.</summary>
        private const int RatMinTicks = 6000;
        private const int RatChance = 10000;
        private const double RatLandShare = 0.01;

        /// <summary>이웃 인사 — 600틱 전 · 1/250 · 채팅 1/3 · 인사말 1/2 `[소스] greetNeighbors(){if(this.game.ticks()>600||!this.random.chance(250))`.</summary>
        private const int GreetMaxTicks = 600;
        private const int GreetChance = 250;
        private const int GreetChatChance = 3;
        private const int GreetHelloChance = 2;

        /// <summary>공격 이모지 — 중립 이상 1/2 😈 · 아니면 1/4 😡 + 1/6 채팅 `[소스] maybeSendAttackEmoji(e){`.</summary>
        private const int AttackDevilChance = 2;
        private const int AttackAngryChance = 4;
        private const int AttackChatChance = 6;

        /// <summary>MIRV 맞은 네이션의 도움 요청 1/8 `[소스] respondToMIRV(e){Fu.length!==0&&this.random.chance(8)`.</summary>
        private const int MirvHelpChance = 8;

        /// <summary>같은 사람에게 다시 보내기까지 `[소스] this.emojiMutedUntil.set(e.smallID(),t+300) · this.chatMutedUntil.set(e.smallID(),r+900)`.</summary>
        private const int EmojiMuteTicks = 300;
        private const int ChatMuteTicks = 900;

        private const string ChatHelpNoAttack = "help.no_attack";
        private const string ChatGreetGg = "greet.gg";
        private const string ChatGreetHello = "greet.hello";
        private const string ChatGreetNiceToMeet = "greet.nice_to_meet";
        private const string ChatGreetDontDoThat = "greet.dont_do_that";

        private readonly FrontWarsRandom _random;
        private readonly FrontWarsGame _game;
        private readonly FrontWarsPlayer _player;
        private readonly Dictionary<int, int> _emojiMutedUntil = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _chatMutedUntil = new Dictionary<int, int>();
        private bool _matchOver;

        public FrontWarsNationEmojiBehavior(FrontWarsRandom random, FrontWarsGame game, FrontWarsPlayer player)
        {
            _random = random;
            _game = game;
            _player = player;
        }

        /// <summary>판이 끝나지 않았으면 8가지를 순서대로 `[소스] maybeSendCasualEmoji(){`.</summary>
        public void MaybeSendCasualEmoji()
        {
            if (_matchOver)
                return;

            CryForHelpWhenOverwhelmed();
            MockTinyAttacks();
            ApplaudWinner();
            FlexCrown();
            FlatterAllies();
            HeckleTraitors();
            CallOutRats();
            GreetNeighbors();
        }

        private void CryForHelpWhenOverwhelmed()
        {
            if (_random.Chance(HelpChance) == false)
                return;

            List<FrontWarsAttack> incoming = _player.IncomingAttacks;
            if (incoming.Count == 0)
                return;

            double total = 0;
            double biggest = 0;
            FrontWarsPlayer biggestAttacker = null;

            foreach (FrontWarsAttack attack in incoming)
            {
                double troops = attack.Troops;
                total += troops;

                if (troops > biggest)
                {
                    biggest = troops;
                    biggestAttacker = attack.Attacker;
                }
            }

            if (total < _player.Troops * HelpTroopsMul)
                return;

            SendEmoji(null, FrontWarsEmojis.Help);

            if (biggestAttacker != null && _random.Chance(HelpChatChance))
                MaybeSendQuickChat(biggestAttacker, ChatHelpNoAttack, null);
        }

        private void MockTinyAttacks()
        {
            if (_random.Chance(MockChance) == false)
                return;

            double troops = _player.Troops;
            if (troops <= 0)
                return;

            foreach (FrontWarsAttack attack in _player.IncomingAttacks.ToArray())
            {
                FrontWarsPlayer attacker = attack.Attacker;

                if (attacker.Type != EFrontWarsPlayerType.Human)
                    continue;

                if (attack.Troops >= troops * MockTroopShare)
                    continue;

                // 목록 고르기 난수는 음소거 칸 확인보다 먼저 굴린다 (인자 평가 순서)
                IReadOnlyList<int> list = _random.Chance(MockListChance) ? FrontWarsEmojis.Confused : FrontWarsEmojis.Yawn;
                MaybeSendEmoji(attacker, list);
            }
        }

        /// <summary>
        /// 승자가 나오면 판 끝 `[소스] applaudWinner(){`.
        /// <para>팀전: 내 팀이 이겼으면 가만히, 아니면 모두에게 박수. 개인전: 가장 큰 네이션만 승자에게 박수 · gg.</para>
        /// </summary>
        private void ApplaudWinner()
        {
            if (_game.HasWinner == false)
                return;

            _matchOver = true;

            if (_game.Settings.IsTeamMode)
            {
                if (_game.WinnerTeam == _player.Team)
                    return;

                SendEmoji(null, FrontWarsEmojis.Clap);
                return;
            }

            // 개인전 승자는 늘 플레이어다 `[소스] typeof e!=`string``
            FrontWarsPlayer winner = _game.Winner;
            if (winner == null || LargestNation() != _player)
                return;

            SendEmoji(winner, FrontWarsEmojis.Clap);
            MaybeSendQuickChat(winner, ChatGreetGg, null);
        }

        /// <summary>땅이 가장 큰 네이션 (같으면 먼저 나온 쪽) `[소스] largestNation(){`.</summary>
        private FrontWarsPlayer LargestNation()
        {
            FrontWarsPlayer largest = null;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Type == EFrontWarsPlayerType.FakeHuman && (largest == null || p.NumTilesOwned > largest.NumTilesOwned))
                    largest = p;
            }

            return largest;
        }

        private void FlexCrown()
        {
            if (_random.Chance(CrownChance) == false)
                return;

            int mine = _player.NumTilesOwned;

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p != _player && p.NumTilesOwned > mine)
                    return;
            }

            SendEmoji(null, FrontWarsEmojis.Crown);
        }

        private void FlatterAllies()
        {
            if (_random.Chance(FlatterChance) == false)
                return;

            var humans = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer ally in _player.Allies())
            {
                if (ally.Type == EFrontWarsPlayerType.Human)
                    humans.Add(ally);
            }

            if (humans.Count == 0)
                return;

            FrontWarsPlayer target = _random.RandElement(humans);
            IReadOnlyList<int> list = _random.Chance(FlatterLoveChance) ? FrontWarsEmojis.Love : FrontWarsEmojis.Handshake;
            SendEmoji(target, list);
        }

        private void HeckleTraitors()
        {
            if (_random.Chance(HeckleChance) == false)
                return;

            var traitors = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Type == EFrontWarsPlayerType.Human && p.IsTraitor && p.IsFriendly(_player) == false)
                    traitors.Add(p);
            }

            if (traitors.Count == 0)
                return;

            SendEmoji(_random.RandElement(traitors), FrontWarsEmojis.Mock);
        }

        private void CallOutRats()
        {
            if (_game.Ticks < RatMinTicks || _random.Chance(RatChance) == false)
                return;

            double limit = _game.NumLandTiles * RatLandShare;
            var rats = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer p in _game.Players())
            {
                if (p.Type != EFrontWarsPlayerType.Human)
                    continue;

                int tiles = p.NumTilesOwned;
                if (tiles > 0 && tiles < limit)
                    rats.Add(p);
            }

            if (rats.Count == 0)
                return;

            SendEmoji(_random.RandElement(rats), FrontWarsEmojis.Rat);
        }

        private void GreetNeighbors()
        {
            if (_game.Ticks > GreetMaxTicks || _random.Chance(GreetChance) == false)
                return;

            var humans = new List<FrontWarsPlayer>();

            foreach (FrontWarsPlayer n in _player.Neighbors())
            {
                if (n != null && n.Type == EFrontWarsPlayerType.Human)
                    humans.Add(n);
            }

            if (humans.Count == 0)
                return;

            FrontWarsPlayer target = _random.RandElement(humans);
            SendEmoji(target, FrontWarsEmojis.Wave);

            if (_random.Chance(GreetChatChance))
                MaybeSendQuickChat(target, _random.Chance(GreetHelloChance) ? ChatGreetHello : ChatGreetNiceToMeet, null);
        }

        // ── 다른 도우미가 부르는 것

        /// <summary>공격을 보낼 때 — 음소거 칸을 먼저 잡는다 `[소스] maybeSendAttackEmoji(e){`.</summary>
        public void MaybeSendAttackEmoji(FrontWarsPlayer target)
        {
            if (ClaimEmojiSlot(target) == false)
                return;

            if (_player.Relation(target) >= EFrontWarsRelation.Neutral)
            {
                if (_random.Chance(AttackDevilChance) == false)
                    return;

                SendEmoji(target, FrontWarsEmojis.Devil);
                return;
            }

            if (_random.Chance(AttackAngryChance) == false)
                return;

            SendEmoji(target, FrontWarsEmojis.Angry);

            if (_random.Chance(AttackChatChance))
                MaybeSendQuickChat(target, ChatGreetDontDoThat, null);
        }

        public void SendAssistEmoji(FrontWarsPlayer target, EFrontWarsNationAssistEmoji kind)
        {
            switch (kind)
            {
                case EFrontWarsNationAssistEmoji.Accept: SendEmoji(target, FrontWarsEmojis.AssistAccept); break;
                case EFrontWarsNationAssistEmoji.RelationTooLow: SendEmoji(target, FrontWarsEmojis.AssistRelationTooLow); break;
                case EFrontWarsNationAssistEmoji.TargetMe: SendEmoji(target, FrontWarsEmojis.AssistTargetMe); break;
                default: SendEmoji(target, FrontWarsEmojis.AssistTargetAlly); break;
            }
        }

        public void SendAllianceEmoji(FrontWarsPlayer target, EFrontWarsNationAllianceEmoji kind)
        {
            switch (kind)
            {
                case EFrontWarsNationAllianceEmoji.Confused: SendEmoji(target, FrontWarsEmojis.Confused); break;
                case EFrontWarsNationAllianceEmoji.ScaredOfThreat: SendEmoji(target, FrontWarsEmojis.Beg); break;
                case EFrontWarsNationAllianceEmoji.Love: SendEmoji(target, FrontWarsEmojis.Love); break;
                default: SendEmoji(target, FrontWarsEmojis.HandshakeOnly); break;
            }
        }

        public void MaybeSendNukeEmoji(FrontWarsPlayer target)
        {
            MaybeSendEmoji(target, FrontWarsEmojis.Nuke);
        }

        public void SendNukeEmojiToAll()
        {
            SendEmoji(null, FrontWarsEmojis.Nuke);
        }

        /// <summary>MIRV 를 맞는 쪽이 네이션이면 1/8 확률로 그 네이션이 모두에게 도움 요청 `[소스] respondToMIRV(e){`.</summary>
        public void RespondToMirv(FrontWarsPlayer target)
        {
            if (FrontWarsEmojis.Help.Count == 0 || _random.Chance(MirvHelpChance) == false)
                return;

            if (target.Type != EFrontWarsPlayerType.FakeHuman || target.CanSendEmoji(null) == false)
                return;

            _game.AddExecution(new FrontWarsEmojiExecution(target, null, _random.RandElement(FrontWarsEmojis.Help)));
        }

        public void MaybeSendWarshipRetaliationEmoji(FrontWarsPlayer target)
        {
            MaybeSendEmoji(target, FrontWarsEmojis.Boat);
        }

        public void SendWarshipRetaliationEmojiToAll()
        {
            SendEmoji(null, FrontWarsEmojis.Boat);
        }

        // ── 보내기

        private void MaybeSendEmoji(FrontWarsPlayer target, IReadOnlyList<int> list)
        {
            if (ClaimEmojiSlot(target))
                SendEmoji(target, list);
        }

        /// <summary>목록이 있고 · 받는 쪽이 모두 또는 사람이고 · 쿨다운이 아니면 목록에서 하나 `[소스] sendEmoji(e,t){`.</summary>
        private void SendEmoji(FrontWarsPlayer recipient, IReadOnlyList<int> list)
        {
            if (list.Count == 0)
                return;

            if (recipient != null && recipient.Type != EFrontWarsPlayerType.Human)
                return;

            if (_player.CanSendEmoji(recipient) == false)
                return;

            _game.AddExecution(new FrontWarsEmojiExecution(_player, recipient?.Id, _random.RandElement(list)));
        }

        /// <summary>모두에게는 늘 참 · 사람 아니면 거짓 · 음소거 중이면 거짓, 아니면 300틱 음소거를 걸고 참 `[소스] claimEmojiSlot(e){`.</summary>
        private bool ClaimEmojiSlot(FrontWarsPlayer recipient)
        {
            if (recipient == null)
                return true;

            if (recipient.Type != EFrontWarsPlayerType.Human)
                return false;

            int ticks = _game.Ticks;

            if (ticks <= (_emojiMutedUntil.TryGetValue(recipient.SmallId, out int until) ? until : 0))
                return false;

            _emojiMutedUntil[recipient.SmallId] = ticks + EmojiMuteTicks;
            return true;
        }

        /// <summary>사람에게만 · 900틱에 한 번 `[소스] maybeSendQuickChat(e,t,n){`.</summary>
        private void MaybeSendQuickChat(FrontWarsPlayer recipient, string key, FrontWarsPlayer target)
        {
            if (recipient.Type != EFrontWarsPlayerType.Human)
                return;

            int ticks = _game.Ticks;

            if (ticks <= (_chatMutedUntil.TryGetValue(recipient.SmallId, out int until) ? until : 0))
                return;

            _chatMutedUntil[recipient.SmallId] = ticks + ChatMuteTicks;
            _game.AddExecution(new FrontWarsQuickChatExecution(_player, recipient.Id, key, target?.Id));
        }
    }
}
