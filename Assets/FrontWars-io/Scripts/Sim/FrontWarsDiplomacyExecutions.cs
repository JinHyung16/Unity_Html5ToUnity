using System;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    /// <summary>
    /// 동맹 요청 보내기 `[소스] Cq3gr8XO.js:uu=class{`. 상대가 이미 나에게 요청했으면 그걸 수락한다.
    /// 요청이 수락 · 거절되면 끝나고, 요청 지속 시간이 지나면 거절하고 끝난다.
    /// </summary>
    public sealed class FrontWarsAllianceRequestExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _requestor;
        private readonly string _recipientId;
        private FrontWarsGame _game;
        private FrontWarsAllianceRequest _request;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsAllianceRequestExecution(FrontWarsPlayer requestor, string recipientId)
        {
            _requestor = requestor;
            _recipientId = recipientId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            _game = game;

            // 원본은 상대가 없으면 경고만 하고 활성으로 둔다 — 다음 틱에 시간 초과로 끝난다
            if (game.HasPlayer(_recipientId) == false)
                return;

            FrontWarsPlayer recipient = game.Player(_recipientId);

            if (_requestor.CanSendAllianceRequest(recipient) == false)
            {
                IsActive = false;
                return;
            }

            FrontWarsAllianceRequest reverse = recipient.OutgoingAllianceRequests().FirstOrDefault(r => r.Recipient == _requestor);

            if (reverse != null)
            {
                IsActive = false;
                reverse.Accept();
            }
            else
            {
                _request = _requestor.CreateAllianceRequest(recipient);
            }
        }

        public void Tick(int tick)
        {
            if (_request != null && _request.Status != EFrontWarsRequestStatus.Pending)
            {
                IsActive = false;
                return;
            }

            if (_game.Ticks - (_request?.CreatedAt ?? 0) > _game.Settings.Config.AllianceRequestDurationTicks)
            {
                _request?.Reject();
                IsActive = false;
            }
        }
    }

    /// <summary>동맹 요청 답하기 — 수락하면 서로 관계 +100 `[소스] Cq3gr8XO.js:du=class{`.</summary>
    public sealed class FrontWarsAllianceReplyExecution : IFrontWarsExecution
    {
        private const int AcceptRelationBonus = 100;

        private readonly string _requestorId;
        private readonly FrontWarsPlayer _recipient;
        private readonly bool _accept;
        private FrontWarsPlayer _requestor;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsAllianceReplyExecution(string requestorId, FrontWarsPlayer recipient, bool accept)
        {
            _requestorId = requestorId;
            _recipient = recipient;
            _accept = accept;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_requestorId) == false)
            {
                IsActive = false;
                return;
            }

            _requestor = game.Player(_requestorId);
        }

        public void Tick(int tick)
        {
            if (_requestor.IsFriendly(_recipient) == false)
            {
                FrontWarsAllianceRequest request = _requestor.OutgoingAllianceRequests().FirstOrDefault(r => r.Recipient == _recipient);

                if (request != null)
                {
                    if (_accept)
                    {
                        request.Accept();
                        _requestor.UpdateRelation(_recipient, AcceptRelationBonus);
                        _recipient.UpdateRelation(_requestor, AcceptRelationBonus);
                    }
                    else
                    {
                        request.Reject();
                    }
                }
            }

            IsActive = false;
        }
    }

    /// <summary>동맹 깨기 — 깨진 쪽 관계 −200, 나머지 모두(같은 팀 제외) −40 `[소스] Cq3gr8XO.js:fu=class{`.</summary>
    public sealed class FrontWarsBreakAllianceExecution : IFrontWarsExecution
    {
        private const int BetrayedRelationPenalty = -200;
        private const int OthersRelationPenalty = -40;

        private readonly FrontWarsPlayer _requestor;
        private readonly string _recipientId;
        private FrontWarsGame _game;
        private FrontWarsPlayer _recipient;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsBreakAllianceExecution(FrontWarsPlayer requestor, string recipientId)
        {
            _requestor = requestor;
            _recipientId = recipientId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_recipientId) == false)
            {
                IsActive = false;
                return;
            }

            _recipient = game.Player(_recipientId);
            _game = game;
        }

        public void Tick(int tick)
        {
            FrontWarsAlliance alliance = _requestor.AllianceWith(_recipient);

            if (alliance != null)
            {
                _requestor.BreakAlliance(alliance);
                _recipient.UpdateRelation(_requestor, BetrayedRelationPenalty);

                foreach (FrontWarsPlayer p in _game.Players())
                {
                    if (p != _requestor && p.IsOnSameTeam(_requestor) == false)
                        p.UpdateRelation(_requestor, OthersRelationPenalty);
                }
            }

            IsActive = false;
        }
    }

    /// <summary>동맹 연장 동의 — 둘 다 동의하면 지금부터 다시 센다. init 에서 다 하고 바로 끝난다 `[소스] Cq3gr8XO.js:lu=class{`.</summary>
    public sealed class FrontWarsAllianceExtensionExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _from;
        private readonly string _toId;

        public bool IsActive => false;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsAllianceExtensionExecution(FrontWarsPlayer from, string toId)
        {
            _from = from;
            _toId = toId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_toId) == false)
                return;

            FrontWarsPlayer to = game.Player(_toId);

            if (_from.IsAlive == false || to.IsAlive == false)
                return;

            FrontWarsAlliance alliance = _from.AllianceWith(to);
            if (alliance == null)
                return;

            alliance.AddExtensionRequest(_from);

            if (alliance.BothAgreedToExtend)
            {
                alliance.Extend(game.Ticks, game.Settings.Config.AllianceDurationTicks);
                game.DisplayMessage(EFrontWarsMessage.AllianceAccepted, _from, 0, to.Name);
                game.DisplayMessage(EFrontWarsMessage.AllianceAccepted, to, 0, _from.Name);
            }
        }

        public void Tick(int tick)
        {
        }
    }

    /// <summary>타겟 찍기 — 찍힌 쪽 관계 −40 `[소스] Cq3gr8XO.js:Sp=class{`.</summary>
    public sealed class FrontWarsTargetExecution : IFrontWarsExecution
    {
        private const int TargetedRelationPenalty = -40;

        private readonly FrontWarsPlayer _requestor;
        private readonly string _targetId;
        private FrontWarsPlayer _target;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsTargetExecution(FrontWarsPlayer requestor, string targetId)
        {
            _requestor = requestor;
            _targetId = targetId;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_targetId) == false)
            {
                IsActive = false;
                return;
            }

            _target = game.Player(_targetId);
        }

        public void Tick(int tick)
        {
            if (_requestor.CanTarget(_target))
            {
                _requestor.Target(_target);
                _target.UpdateRelation(_requestor, TargetedRelationPenalty);
            }

            IsActive = false;
        }
    }

    /// <summary>
    /// 병력 기부 — 양이 없으면 내 병력 1/3(버림), 받는 쪽 빈자리까지만. 성공하면 받는 쪽 관계 +50 `[소스] Cq3gr8XO.js:Sf=class{`.
    /// </summary>
    public sealed class FrontWarsDonateTroopsExecution : IFrontWarsExecution
    {
        private const int DonationRelationBonus = 50;

        private readonly FrontWarsPlayer _sender;
        private readonly string _recipientId;
        private double? _troops;
        private FrontWarsPlayer _recipient;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsDonateTroopsExecution(FrontWarsPlayer sender, string recipientId, double? troops)
        {
            _sender = sender;
            _recipientId = recipientId;
            _troops = troops;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_recipientId) == false)
            {
                IsActive = false;
                return;
            }

            _recipient = game.Player(_recipientId);
            _troops ??= Math.Floor(_sender.Troops / (double)game.Settings.Config.DonationDivisor);
            double room = game.Rules.MaxTroops(_recipient) - _recipient.Troops;
            _troops = Math.Min(_troops.Value, room);
        }

        public void Tick(int tick)
        {
            if (_sender.CanDonateTroops(_recipient) && _sender.DonateTroops(_recipient, _troops.Value))
                _recipient.UpdateRelation(_sender, DonationRelationBonus);

            IsActive = false;
        }
    }

    /// <summary>
    /// 골드 기부 — 성공하면 받는 쪽 관계 +50 `[소스] Cq3gr8XO.js:xf=class{`.
    /// <para>⚠ 원본은 양이 없으면 0 으로 담아 1/3 기본값이 절대 안 붙는다 (<c>Lt(n??0)</c> 뒤 <c>??=</c>). 그래서 양 없는 기부는 실패한다 — 그대로 둔다.</para>
    /// </summary>
    public sealed class FrontWarsDonateGoldExecution : IFrontWarsExecution
    {
        private const int DonationRelationBonus = 50;

        private readonly FrontWarsPlayer _sender;
        private readonly string _recipientId;
        private readonly long _gold;
        private FrontWarsPlayer _recipient;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsDonateGoldExecution(FrontWarsPlayer sender, string recipientId, double? gold)
        {
            _sender = sender;
            _recipientId = recipientId;
            _gold = FrontWarsPlayer.ToStoredTroops(gold ?? 0);
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_recipientId) == false)
            {
                IsActive = false;
                return;
            }

            _recipient = game.Player(_recipientId);
        }

        public void Tick(int tick)
        {
            if (_sender.CanDonateGold(_recipient) && _sender.DonateGold(_recipient, _gold))
                _recipient.UpdateRelation(_sender, DonationRelationBonus);

            IsActive = false;
        }
    }

    /// <summary>금수 걸기 · 풀기 `[소스] Cq3gr8XO.js:Cf=class{`.</summary>
    public sealed class FrontWarsEmbargoExecution : IFrontWarsExecution
    {
        private readonly FrontWarsPlayer _player;
        private readonly string _targetId;
        private readonly bool _start;
        private FrontWarsPlayer _target;

        public bool IsActive { get; private set; } = true;
        public bool ActiveDuringSpawnPhase => false;

        public FrontWarsEmbargoExecution(FrontWarsPlayer player, string targetId, bool start)
        {
            _player = player;
            _targetId = targetId;
            _start = start;
        }

        public void Init(FrontWarsGame game, int tick)
        {
            if (game.HasPlayer(_targetId) == false)
            {
                IsActive = false;
                return;
            }

            _target = game.Player(_targetId);
        }

        public void Tick(int tick)
        {
            if (_start)
                _player.AddEmbargo(_target, false);
            else
                _player.StopEmbargo(_target);

            IsActive = false;
        }
    }
}
