using System;
using System.Collections.Generic;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 과제 — <b>NPC 슬롯 둘이 나눠 주고, 끝내면 스킬을 보상으로 준다</b> [소스 <c>Nh</c>].
    ///
    /// <para>
    /// ★★ <b>목표는 «절대값»이 아니라 «받을 때의 지표 + 증분»이다</b>
    /// [소스 <c>goal = metrics[metricId] + goalOffset</c>].
    /// 절대값으로 옮기면 이미 나무를 켠 사람은 <b>받자마자 완료</b>가 된다.
    /// </para>
    /// </summary>
    public sealed partial class UndeadSimulation
    {
        public const string MetricTreesActivated = "graveyard_evil_trees_activated";
        public const string MetricFireplaceHpRestored = "fireplace_hp_restored";
        public const string MetricEnemiesKilledWithDash = "enemies_killed_with_dash";
        public const string MetricSheepReturned = "sheep_returned_to_farmers";

        /// <summary>과제를 나눠 주는 NPC 슬롯 수 [소스 <c>task_npc_1</c> · <c>task_npc_2</c>].</summary>
        public const int TaskNpcSlots = 2;

        /// <summary>과제 하나의 진행 상태.</summary>
        public struct TaskRecord
        {
            public EUndeadTaskStatus Status;

            /// <summary>받을 때 «고정된» 목표 — 지표가 이 값을 넘으면 완료다.</summary>
            public int Goal;
        }

        private TaskRecord[] _taskRecords = Array.Empty<TaskRecord>();
        private readonly Dictionary<string, int> _taskMetrics = new Dictionary<string, int>();
        private readonly int[] _taskNpcSlot = new int[TaskNpcSlots];
        private UndeadTaskDefinition[] _taskDefinitions = Array.Empty<UndeadTaskDefinition>();

        /// <summary>표에서 온 과제 정의 — 시뮬은 <c>JinHyung.Data</c> 를 안 본다.</summary>
        public struct UndeadTaskDefinition
        {
            public string Code;
            public int Order;
            public string MetricId;
            public int GoalOffset;
            public EUndeadSkill RewardSkill;
            public EUndeadSkill[] PrerequisiteSkills;
        }

        public IReadOnlyList<TaskRecord> TaskRecords
        {
            get { return _taskRecords; }
        }

        public IReadOnlyList<UndeadTaskDefinition> TaskDefinitions
        {
            get { return _taskDefinitions; }
        }

        /// <summary>슬롯이 들고 있는 과제의 인덱스 — <c>-1</c> 이면 비었다.</summary>
        public int TaskInSlot(int slot)
        {
            return slot >= 0 && slot < _taskNpcSlot.Length ? _taskNpcSlot[slot] : -1;
        }

        public int TaskMetric(string metricId)
        {
            return _taskMetrics.TryGetValue(metricId, out int value) ? value : 0;
        }

        /// <summary>과제가 «완료」됐다 — 아직 보상은 안 받았다.</summary>
        public event Action<int> OnTaskCompleted;

        /// <summary>보상을 받았다 — 스킬 하나가 새로 생겼다.</summary>
        public event Action<int, EUndeadSkill> OnTaskRewardClaimed;

        /// <summary>
        /// 표를 받아 과제를 세운다. <b>배선이 판을 시작할 때 한 번</b> 부른다.
        /// <para>⚠ 안 부르면 과제가 <b>0개</b>인 채로 돈다 — 그런데 아무 오류도 안 난다.</para>
        /// </summary>
        public void SetupTasks(UndeadTaskDefinition[] definitions)
        {
            _taskDefinitions = definitions ?? Array.Empty<UndeadTaskDefinition>();
            _taskRecords = new TaskRecord[_taskDefinitions.Length];
            _taskMetrics.Clear();

            for (int i = 0; i < _taskNpcSlot.Length; i++)
                _taskNpcSlot[i] = -1;

            for (int i = 0; i < _taskDefinitions.Length; i++)
            {
                _taskRecords[i] = new TaskRecord { Status = EUndeadTaskStatus.Unassigned, Goal = 0 };

                if (_taskMetrics.ContainsKey(_taskDefinitions[i].MetricId) == false)
                    _taskMetrics[_taskDefinitions[i].MetricId] = 0;
            }

            AssignEligibleTasks();
        }

        /// <summary>
        /// 빈 슬롯에 «자격이 되는» 과제를 채운다 [소스 <c>assignEligibleTasks</c>].
        ///
        /// <para>
        /// 자격 = <b>아직 안 붙었고</b> · <b>다른 슬롯이 안 들고 있고</b> · <b>선행 스킬을 다 가졌다</b>.
        /// </para>
        /// </summary>
        public void AssignEligibleTasks()
        {
            for (int slot = 0; slot < _taskNpcSlot.Length; slot++)
            {
                int held = _taskNpcSlot[slot];

                // 들고 있는 과제가 아직 «수령 전»이면 그대로 둔다
                if (held >= 0 && _taskRecords[held].Status != EUndeadTaskStatus.Claimed)
                    continue;

                _taskNpcSlot[slot] = -1;
                int pick = FindEligibleTask();

                if (pick < 0)
                    continue;

                _taskRecords[pick] = new TaskRecord
                {
                    Status = EUndeadTaskStatus.Active,
                    Goal = TaskMetric(_taskDefinitions[pick].MetricId) + _taskDefinitions[pick].GoalOffset,
                };

                _taskNpcSlot[slot] = pick;
            }
        }

        private int FindEligibleTask()
        {
            int best = -1;

            for (int i = 0; i < _taskDefinitions.Length; i++)
            {
                if (_taskRecords[i].Status != EUndeadTaskStatus.Unassigned)
                    continue;

                if (IsHeldBySlot(i))
                    continue;

                if (HasPrerequisites(_taskDefinitions[i]) == false)
                    continue;

                if (best < 0 || _taskDefinitions[i].Order < _taskDefinitions[best].Order)
                    best = i;
            }

            return best;
        }

        private bool IsHeldBySlot(int taskIndex)
        {
            for (int s = 0; s < _taskNpcSlot.Length; s++)
            {
                if (_taskNpcSlot[s] == taskIndex)
                    return true;
            }

            return false;
        }

        private bool HasPrerequisites(UndeadTaskDefinition definition)
        {
            if (definition.PrerequisiteSkills == null)
                return true;

            for (int i = 0; i < definition.PrerequisiteSkills.Length; i++)
            {
                if (OwnsSkill(definition.PrerequisiteSkills[i]) == false)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 지표를 올린다 — <b>목표를 넘긴 «진행 중» 과제가 완료된다</b> [소스 <c>recordProgress</c>].
        ///
        /// <para>⚠ 진행 중이 아닌 과제도 지표는 «쌓인다» — 나중에 받을 때 목표가 그만큼 올라간다.</para>
        /// </summary>
        public void RecordTaskProgress(string metricId, int amount)
        {
            if (amount <= 0 || _taskDefinitions.Length == 0)
                return;

            int total = TaskMetric(metricId) + amount;
            _taskMetrics[metricId] = total;

            for (int i = 0; i < _taskDefinitions.Length; i++)
            {
                if (_taskDefinitions[i].MetricId != metricId)
                    continue;

                if (_taskRecords[i].Status != EUndeadTaskStatus.Active || total < _taskRecords[i].Goal)
                    continue;

                _taskRecords[i].Status = EUndeadTaskStatus.Completed;
                OnTaskCompleted?.Invoke(i);
            }
        }

        /// <summary>
        /// 보상을 받는다 [소스 <c>claimTaskReward</c>] — <b>완료 상태에서만</b> 된다.
        /// <para>받으면 슬롯이 비고 <b>다음 과제가 곧바로 배정된다</b>.</para>
        /// </summary>
        public bool ClaimTaskReward(int taskIndex)
        {
            if (taskIndex < 0 || taskIndex >= _taskRecords.Length)
                return false;

            if (_taskRecords[taskIndex].Status != EUndeadTaskStatus.Completed)
                return false;

            _taskRecords[taskIndex].Status = EUndeadTaskStatus.Claimed;
            EUndeadSkill reward = _taskDefinitions[taskIndex].RewardSkill;
            GrantSkill(reward);

            for (int s = 0; s < _taskNpcSlot.Length; s++)
            {
                if (_taskNpcSlot[s] == taskIndex)
                    _taskNpcSlot[s] = -1;
            }

            AssignEligibleTasks();
            OnTaskRewardClaimed?.Invoke(taskIndex, reward);
            return true;
        }

        /// <summary>수령까지 끝난 과제 수 — 둘을 넘기면 원본은 <b>두 번째 바이옴</b>을 연다 [소스].</summary>
        public int ClaimedTaskCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < _taskRecords.Length; i++)
                {
                    if (_taskRecords[i].Status == EUndeadTaskStatus.Claimed)
                        count++;
                }

                return count;
            }
        }
    }
}
