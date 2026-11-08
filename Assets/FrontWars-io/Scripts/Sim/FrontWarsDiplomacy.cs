using System;
using System.Collections.Generic;
using System.Linq;

namespace JinHyung.FrontWarsIo
{
    public enum EFrontWarsRequestStatus
    {
        Pending,
        Accepted,
        Rejected
    }

    /// <summary>동맹 요청 `[소스] Cq3gr8XO.js:kp=class{`.</summary>
    public sealed class FrontWarsAllianceRequest
    {
        private readonly FrontWarsDiplomacy _diplomacy;

        public FrontWarsPlayer Requestor { get; }
        public FrontWarsPlayer Recipient { get; }
        public int CreatedAt { get; }
        public EFrontWarsRequestStatus Status { get; private set; } = EFrontWarsRequestStatus.Pending;

        public FrontWarsAllianceRequest(FrontWarsPlayer requestor, FrontWarsPlayer recipient, int createdAt, FrontWarsDiplomacy diplomacy)
        {
            Requestor = requestor;
            Recipient = recipient;
            CreatedAt = createdAt;
            _diplomacy = diplomacy;
        }

        public void Accept()
        {
            Status = EFrontWarsRequestStatus.Accepted;
            _diplomacy.AcceptAllianceRequest(this);
        }

        public void Reject()
        {
            Status = EFrontWarsRequestStatus.Rejected;
            _diplomacy.RejectAllianceRequest(this);
        }
    }

    /// <summary>동맹 — 만든 틱 + 지속 시간에 끝난다. 둘 다 연장에 동의하면 지금부터 다시 센다 `[소스] Cq3gr8XO.js:Op=class{`.</summary>
    public sealed class FrontWarsAlliance
    {
        private readonly FrontWarsDiplomacy _diplomacy;
        private bool _extensionRequestor;
        private bool _extensionRecipient;

        public FrontWarsPlayer Requestor { get; }
        public FrontWarsPlayer Recipient { get; }
        public int CreatedAt { get; }
        public int Id { get; }
        public int ExpiresAt { get; private set; }

        public FrontWarsAlliance(FrontWarsDiplomacy diplomacy, FrontWarsPlayer requestor, FrontWarsPlayer recipient, int createdAt, int id, int duration)
        {
            _diplomacy = diplomacy;
            Requestor = requestor;
            Recipient = recipient;
            CreatedAt = createdAt;
            Id = id;
            ExpiresAt = createdAt + duration;
        }

        public FrontWarsPlayer Other(FrontWarsPlayer player)
        {
            return Requestor == player ? Recipient : Requestor;
        }

        public void Expire()
        {
            _diplomacy.ExpireAlliance(this);
        }

        public void AddExtensionRequest(FrontWarsPlayer player)
        {
            if (Requestor == player)
                _extensionRequestor = true;
            else if (Recipient == player)
                _extensionRecipient = true;
        }

        public bool BothAgreedToExtend
        {
            get { return _extensionRequestor && _extensionRecipient; }
        }

        public bool OnlyOneAgreedToExtend
        {
            get { return _extensionRequestor != _extensionRecipient; }
        }

        public void Extend(int ticks, int duration)
        {
            _extensionRequestor = false;
            _extensionRecipient = false;
            ExpiresAt = ticks + duration;
        }
    }

    /// <summary>
    /// 동맹 요청 · 동맹 목록을 가진 게임 쪽 관리자 `[소스] Cq3gr8XO.js:createAllianceRequest(e,t){ · acceptAllianceRequest(e){ · breakAlliance(e,t){ · expireAlliance(e){`.
    /// </summary>
    public sealed class FrontWarsDiplomacy
    {
        private readonly FrontWarsGame _game;
        private int _nextAllianceId;

        public List<FrontWarsAllianceRequest> Requests { get; private set; } = new List<FrontWarsAllianceRequest>();
        public List<FrontWarsAlliance> Alliances { get; private set; } = new List<FrontWarsAlliance>();

        public event Action<FrontWarsAllianceRequest> OnRequest;
        public event Action<FrontWarsAllianceRequest, bool> OnRequestReply;
        public event Action<FrontWarsPlayer, FrontWarsPlayer> OnBroken;
        public event Action<FrontWarsAlliance> OnExpired;

        public FrontWarsDiplomacy(FrontWarsGame game)
        {
            _game = game;
        }

        /// <summary>
        /// 이미 동맹 · 중복 요청이면 null. 상대가 나에게 보낸 요청이 있으면 그걸 수락하고 null.
        /// </summary>
        public FrontWarsAllianceRequest CreateAllianceRequest(FrontWarsPlayer requestor, FrontWarsPlayer recipient)
        {
            if (requestor.IsAlliedWith(recipient))
                return null;

            if (recipient.IncomingAllianceRequests().Any(r => r.Requestor == requestor))
                return null;

            FrontWarsAllianceRequest reverse = requestor.IncomingAllianceRequests().FirstOrDefault(r => r.Requestor == recipient);
            if (reverse != null)
            {
                reverse.Accept();
                return null;
            }

            var request = new FrontWarsAllianceRequest(requestor, recipient, _game.Ticks, this);
            Requests.Add(request);
            OnRequest?.Invoke(request);
            return request;
        }

        /// <summary>요청을 지우고 동맹을 만든다. 서로의 임시 금수를 푼다.</summary>
        public void AcceptAllianceRequest(FrontWarsAllianceRequest request)
        {
            Requests = Requests.FindAll(r => r != request);
            FrontWarsPlayer a = request.Requestor, b = request.Recipient;

            if (a.AllianceWith(b) != null)
                throw new InvalidOperationException($"cannot accept alliance request, already allied with {b.Name}");

            var alliance = new FrontWarsAlliance(this, a, b, _game.Ticks, _nextAllianceId++, _game.Settings.Config.AllianceDurationTicks);
            Alliances.Add(alliance);
            a.AddAllianceRef(alliance);
            b.AddAllianceRef(alliance);
            a.RecordPastOutgoingAllianceRequest(request);

            if (a.HasEmbargoAgainst(b))
                a.EndTemporaryEmbargo(b);

            if (b.HasEmbargoAgainst(a))
                b.EndTemporaryEmbargo(a);

            OnRequestReply?.Invoke(request, true);
        }

        public void RejectAllianceRequest(FrontWarsAllianceRequest request)
        {
            Requests = Requests.FindAll(r => r != request);
            request.Requestor.RecordPastOutgoingAllianceRequest(request);
            OnRequestReply?.Invoke(request, false);
        }

        /// <summary>owner 가 target 에게서 받은 요청이 있으면 거절한다 `[소스] rejectIncomingAllianceRequests(e){`.</summary>
        public void RejectIncomingRequest(FrontWarsPlayer owner, FrontWarsPlayer from)
        {
            owner.IncomingAllianceRequests().FirstOrDefault(r => r.Requestor == from)?.Reject();
        }

        /// <summary>깬 쪽은 상대가 배신자 · 끊김이 아니면 배신자가 된다.</summary>
        public void BreakAlliance(FrontWarsPlayer breaker, FrontWarsAlliance alliance)
        {
            FrontWarsPlayer other = alliance.Requestor == breaker ? alliance.Recipient : alliance.Requestor;

            if (breaker.IsAlliedWith(other) == false)
                throw new InvalidOperationException($"{breaker.Name} not allied with {other.Name}, cannot break alliance");

            if (other.IsTraitor == false && other.IsDisconnected == false)
                breaker.MarkTraitor();

            FrontWarsAlliance shared = SingleShared(breaker, other, "must have exactly one alliance");
            Remove(shared);
            OnBroken?.Invoke(breaker, other);
        }

        public void ExpireAlliance(FrontWarsAlliance alliance)
        {
            FrontWarsAlliance shared = SingleShared(alliance.Requestor, alliance.Recipient, "cannot expire alliance: must have exactly one alliance");
            Remove(shared);
            OnExpired?.Invoke(alliance);
        }

        private static FrontWarsAlliance SingleShared(FrontWarsPlayer a, FrontWarsPlayer b, string error)
        {
            var mine = new HashSet<FrontWarsAlliance>(a.Alliances());
            List<FrontWarsAlliance> shared = b.Alliances().FindAll(mine.Contains);

            if (shared.Count != 1)
                throw new InvalidOperationException($"{error}, have {shared.Count}");

            return shared[0];
        }

        private void Remove(FrontWarsAlliance alliance)
        {
            Alliances = Alliances.FindAll(x => x != alliance);
            alliance.Requestor.RemoveAllianceRef(alliance);
            alliance.Recipient.RemoveAllianceRef(alliance);
        }
    }
}
