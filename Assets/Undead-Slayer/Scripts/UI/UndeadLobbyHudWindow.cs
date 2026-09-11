using System;
using JinHyung.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 로비 위에 얹히는 것 — <b>과제 말풍선 둘 · 포털 말풍선 둘 · 수령 링 · 보상 팝업 · 진입 페이드</b>.
    ///
    /// <para>
    /// ★ 원본은 이것들을 <c>cameraTopContainer</c>(카메라 위 층)에 두고 <b>주인을 따라다니게</b> 한다 —
    /// 그래서 세계가 흔들려도 글자는 또렷하다. 우리도 <b>UI 층</b>에 두고 월드 좌표를 화면으로 옮긴다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>보이는 조건이 넷 다 다르다</b> — 과제 말풍선은 «과제를 들고 · 반경 안 · 모달 아님»,
    /// 수령 링은 거기에 «완료»까지, 포털 말풍선은 «근접», 잠김 문구는 «잠긴 포털만».
    /// 하나로 합치면 원본과 다른 자리에서 뜬다.
    /// </para>
    /// </summary>
    public sealed class UndeadLobbyHudWindow : BaseWindow
    {
        public static readonly WindowKey<UndeadLobbyHudWindow> Key =
            new WindowKey<UndeadLobbyHudWindow>("UI/Window/UndeadLobbyHudWindow");

        // ★ <b>평평한 배열</b>이다 — 중첩 직렬화 클래스는 굽는 쪽에서 «참조 주입»이 안 된다.
        //   ⚠ 배열들의 «같은 자리»가 한 말풍선이다. 길이가 어긋나면 다른 NPC 것이 섞인다.

        [Header("Task")]
        [SerializeField] private RectTransform[] _taskRoots;
        [SerializeField] private TMP_Text[] _taskTitles;
        [SerializeField] private TMP_Text[] _taskProgress;
        [SerializeField] private RectTransform[] _taskBarFills;
        [SerializeField] private TMP_Text[] _taskRewardTitles;
        [SerializeField] private Image[] _taskRewardIcons;
        [SerializeField] private TMP_Text[] _taskRewardNames;

        [Header("Portal")]
        [SerializeField] private RectTransform[] _portalRoots;
        [SerializeField] private TMP_Text[] _portalNames;
        [SerializeField] private TMP_Text[] _portalRequirements;
        [SerializeField] private TMP_Text[] _portalProgress;

        [Header("Claim")]
        [SerializeField] private Image _claimRing;

        [Header("Reward")]
        [SerializeField] private RectTransform _rewardPopup;
        [SerializeField] private TMP_Text _rewardSkillName;
        [SerializeField] private Image _rewardSkillIcon;
        [SerializeField] private Button _rewardConfirm;

        [Header("Fade")]
        [SerializeField] private Image _fade;

        /// <summary>진행 바의 전체 폭 [소스 <c>roundRect(−96, r, 192, 10, 5)</c>].</summary>
        private const float BarWidth = 192f;

        /// <summary>과제 말풍선이 NPC 위로 얼마나 뜨나 [소스 <c>−nativeHeight·anchorY − 20 + 4</c> = −96].</summary>
        private const float TaskBubbleOffsetY = -96f;

        /// <summary>포털 말풍선 오프셋 [소스 <c>ownerOffset = (0, −156)</c>].</summary>
        private const float PortalBubbleOffsetY = -156f;

        /// <summary>수령 링이 히어로에서 얼마나 떨어지나 [소스 <c>followOffset {x:0, y:75}</c>].</summary>
        private const float ClaimRingOffsetY = 75f;

        /// <summary>진입 페이드 — <b>1초</b>에 꽉 찬다 [소스 <c>alpha += dt/1000</c>].</summary>
        public const float FadeSeconds = 1f;

        private Camera _camera;
        private float _fadeElapsed = -1f;

        /// <summary>페이드가 다 찼다 — 그때 바이옴으로 넘어간다.</summary>
        public event Action OnFadeCompleted;

        public event Action OnRewardConfirmed;

        public void BindCamera(Camera camera)
        {
            _camera = camera;
        }

        protected override void OnOpened()
        {
            base.OnOpened();

            if (_rewardConfirm != null)
            {
                _rewardConfirm.onClick.RemoveAllListeners();
                _rewardConfirm.onClick.AddListener(() => OnRewardConfirmed?.Invoke());
            }

            HideReward();
            _fadeElapsed = -1f;

            if (_fade != null)
                _fade.enabled = false;
        }

        protected override void OnClosing()
        {
            if (_rewardConfirm != null)
                _rewardConfirm.onClick.RemoveAllListeners();

            base.OnClosing();
        }

        /// <summary>
        /// 매 프레임 — 로비 상태를 얹는다.
        /// <para>⚠ <paramref name="texts"/> 는 <b>문구 표에서 온 것</b>이다. 여기서 문자열을 짓지 않는다.</para>
        /// </summary>
        public void Apply(UndeadLobbySimulation lobby, UndeadSimulation run, ILobbyTexts texts)
        {
            if (lobby == null || run == null || texts == null)
                return;

            ApplyTaskBubbles(lobby, run, texts);
            ApplyPortalBubbles(lobby, run, texts);
            ApplyClaimRing(lobby);
        }

        private void ApplyTaskBubbles(UndeadLobbySimulation lobby, UndeadSimulation run, ILobbyTexts texts)
        {
            for (int i = 0; i < _taskRoots.Length && i < lobby.Npcs.Count; i++)
            {
                UndeadLobbySimulation.Npc npc = lobby.Npcs[i];

                // [소스] 과제를 들고 있고 · 반경 안이고 · 모달이 아니어야 뜬다
                bool show = npc.TaskIndex >= 0 && npc.InRange;

                if (_taskRoots[i].gameObject.activeSelf != show)
                    _taskRoots[i].gameObject.SetActive(show);

                if (show == false)
                    continue;

                UndeadSimulation.UndeadTaskDefinition def = run.TaskDefinitions[npc.TaskIndex];
                UndeadSimulation.TaskRecord record = run.TaskRecords[npc.TaskIndex];

                int goal = record.Goal > 0 ? record.Goal : def.GoalOffset;
                int current = Mathf.Min(run.TaskMetric(def.MetricId), goal);
                float ratio = goal > 0 ? Mathf.Clamp01((float)current / goal) : 0f;

                _taskTitles[i].text = texts.Requirement(npc.TaskIndex);
                _taskProgress[i].text = $"{current}/{goal}";
                _taskRewardTitles[i].text = texts.RewardTitle;
                _taskRewardNames[i].text = texts.SkillName(def.RewardSkill);
                _taskRewardIcons[i].sprite = texts.SkillIcon(def.RewardSkill);

                // 채움은 «폭이 자란다» — 그림을 자르지 않는다 (재발방지 #166 곁가지)
                _taskBarFills[i].sizeDelta =
                    new Vector2(BarWidth * ratio * CanvasScale, _taskBarFills[i].sizeDelta.y);

                Place(_taskRoots[i], npc.Position, TaskBubbleOffsetY);
            }
        }

        private void ApplyPortalBubbles(UndeadLobbySimulation lobby, UndeadSimulation run, ILobbyTexts texts)
        {
            for (int i = 0; i < _portalRoots.Length && i < lobby.Portals.Count; i++)
            {
                UndeadLobbySimulation.Portal portal = lobby.Portals[i];
                bool show = portal.InProximity;

                if (_portalRoots[i].gameObject.activeSelf != show)
                    _portalRoots[i].gameObject.SetActive(show);

                if (show == false)
                    continue;

                _portalNames[i].text = texts.PortalName(portal.Biome);

                // ★ 조건·진행은 «잠긴» 포털에만 뜬다 [소스 — 열려 있으면 이름만]
                bool locked = portal.Open == false;
                _portalRequirements[i].enabled = locked;
                _portalProgress[i].enabled = locked;

                if (locked)
                {
                    _portalRequirements[i].text = texts.PortalRequirement(portal.Biome);
                    int claimed = Mathf.Min(run.ClaimedTaskCount, UndeadLobbySimulation.Biome2ClaimedTasks);
                    _portalProgress[i].text = $"{claimed}/{UndeadLobbySimulation.Biome2ClaimedTasks}";
                }

                Place(_portalRoots[i], portal.Position, PortalBubbleOffsetY);
            }
        }

        private void ApplyClaimRing(UndeadLobbySimulation lobby)
        {
            if (_claimRing == null)
                return;

            double progress = 0.0;
            bool show = false;

            for (int i = 0; i < lobby.Npcs.Count; i++)
            {
                if (lobby.Npcs[i].ClaimProgress <= 0.0)
                    continue;

                progress = lobby.Npcs[i].ClaimProgress;
                show = true;
                break;
            }

            if (_claimRing.enabled != show)
                _claimRing.enabled = show;

            if (show == false)
                return;

            _claimRing.fillAmount = (float)progress;
            Place(_claimRing.rectTransform, lobby.HeroPosition, ClaimRingOffsetY);
        }

        /// <summary>월드 좌표를 UI 자리로 옮긴다 — 원본이 <c>toGlobal → toLocal</c> 로 하는 것과 같다.</summary>
        private void Place(RectTransform target, UndeadVec2 world, float offsetY)
        {
            if (_camera == null || target == null)
                return;

            Vector3 point = UndeadUnits.ToPosition(world.X, world.Y + offsetY);
            Vector3 screen = _camera.WorldToScreenPoint(point);
            var parent = target.parent as RectTransform;

            if (parent == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, screen, target.GetComponentInParent<Canvas>()?.worldCamera, out Vector2 local);

            target.anchoredPosition = local;
        }

        // ══════════════════════════════ 보상 팝업 · 페이드

        public void ShowReward(string skillName, Sprite icon)
        {
            if (_rewardPopup == null)
                return;

            _rewardPopup.gameObject.SetActive(true);

            if (_rewardSkillName != null)
                _rewardSkillName.text = skillName;

            if (_rewardSkillIcon != null)
                _rewardSkillIcon.sprite = icon;
        }

        public void HideReward()
        {
            if (_rewardPopup != null)
                _rewardPopup.gameObject.SetActive(false);
        }

        public bool IsRewardOpen
        {
            get { return _rewardPopup != null && _rewardPopup.gameObject.activeSelf; }
        }

        /// <summary>진입 페이드를 시작한다 — <b>다 차면</b> <see cref="OnFadeCompleted"/> 다.</summary>
        public void BeginFade()
        {
            if (_fadeElapsed >= 0f)
                return;

            _fadeElapsed = 0f;

            if (_fade != null)
            {
                _fade.enabled = true;
                _fade.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        public void StepFade(float deltaTime)
        {
            if (_fadeElapsed < 0f || _fade == null)
                return;

            _fadeElapsed += deltaTime;
            float alpha = Mathf.Clamp01(_fadeElapsed / FadeSeconds);
            _fade.color = new Color(0f, 0f, 0f, alpha);

            if (alpha < 1f)
                return;

            _fadeElapsed = -1f;
            OnFadeCompleted?.Invoke();
        }

        private const float CanvasScale = 1080f / 580f;
    }

    /// <summary>
    /// 로비 UI 가 쓰는 «문구·아이콘» 창구 — <b>창이 표를 직접 안 본다</b>.
    /// <para>⚠ 창이 표를 보면 화면 코드가 데이터 계층에 묶여, 검사할 때 표를 통째로 세워야 한다.</para>
    /// </summary>
    public interface ILobbyTexts
    {
        string Requirement(int taskIndex);

        string RewardTitle { get; }

        string SkillName(EUndeadSkill skill);

        Sprite SkillIcon(EUndeadSkill skill);

        string PortalName(int biome);

        string PortalRequirement(int biome);
    }
}
