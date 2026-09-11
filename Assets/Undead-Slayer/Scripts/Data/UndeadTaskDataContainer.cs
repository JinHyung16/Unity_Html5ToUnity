using System.Text;
using JinHyung.Extensions;

namespace JinHyung.Data
{
    /// <summary>
    /// 과제 표 — 원본 정의 배열 <b>4종 전수</b> [소스 <c>Vh</c>].
    ///
    /// <para>
    /// ⚠ 보상 스킬이 <b>스킬 표에 실제로 있는지</b>를 여기서 대조한다 —
    /// 「보상 코드만 적고 그 스킬은 없는」 상태가 조용히 통과하지 않게 한다.
    /// </para>
    /// </summary>
    public class UndeadTaskDataContainer : DictionaryContainer<string, UndeadTaskData>
    {
        /// <summary>원본 과제 수 [소스 — 정의 배열 길이].</summary>
        public const int OriginRowCount = 4;

        /// <summary>과제를 나눠 주는 NPC 슬롯 수 [소스 <c>jh = ["task_npc_1","task_npc_2"]</c>].</summary>
        public const int NpcSlotCount = 2;

        public override string Name
        {
            get { return "UndeadTaskTable"; }
        }

        public override bool Validate(out string errorMessage)
        {
            var sb = new StringBuilder();

            if (base.Validate(out string baseError) == false)
                sb.AppendLine(baseError);

            if (Count != OriginRowCount)
                sb.AppendLine($"행 수 {Count} — 원본 과제 {OriginRowCount}종");

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadTaskData v = AllValues[i];

                if (v.Code.IsNullOrEmpty())
                    sb.AppendLine($"Id {v.Id}: Code 가 비었다");

                if (v.MetricId.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 지표 id 가 비었다");

                if (v.GoalOffset <= 0)
                    sb.AppendLine($"{v.Code}: 목표 증분이 0 이다");

                if (v.RewardSkill.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 보상 스킬이 비었다");

                if (v.TitleKey.IsNullOrEmpty() || v.RequirementKey.IsNullOrEmpty())
                    sb.AppendLine($"{v.Code}: 문구 키가 비었다");
            }

            errorMessage = sb.ToString();
            return errorMessage.Length == 0;
        }

        /// <summary>
        /// 표끼리 맞물리는지 본다 — <b>보상 스킬과 선행 스킬이 스킬 표에 실재하나</b>.
        /// <para>⚠ 「코드만 적힌 유령 참조」는 여기서만 잡힌다.</para>
        /// </summary>
        public override void AfterAllTableLoaded()
        {
            base.AfterAllTableLoaded();

            var skills = DataManager.Instance.GetContainer<UndeadSkillDataContainer>();

            if (skills == null)
                return;

            for (int i = 0; i < AllValues.Count; i++)
            {
                UndeadTaskData v = AllValues[i];

                if (skills.Get(v.RewardSkill) == null)
                    Core.Log.Error($"{Name}: {v.Code} 의 보상 스킬 «{v.RewardSkill}» 이 스킬 표에 없다");

                if (v.PrerequisiteSkills.IsNullOrEmpty())
                    continue;

                foreach (string code in v.PrerequisiteSkills.Split(';'))
                {
                    if (code.IsNullOrEmpty() == false && skills.Get(code.Trim()) == null)
                        Core.Log.Error($"{Name}: {v.Code} 의 선행 스킬 «{code}» 이 스킬 표에 없다");
                }
            }
        }
    }
}
