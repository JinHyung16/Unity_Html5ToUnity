namespace JinHyung.Data
{
    /// <summary>
    /// 과제 하나 [소스 <c>Vh</c> 정의 배열].
    ///
    /// <para>
    /// ★ <b>목표는 «절대값»이 아니라 «지금 지표에 더하는 값»이다</b> [소스 <c>goal = metrics[metricId] + goalOffset</c>].
    /// 그래서 이미 나무를 몇 그루 켠 뒤에 과제를 받아도 <b>거기서부터 10 그루</b>다.
    /// 절대값으로 옮기면 <b>받자마자 완료</b>가 되어 버린다.
    /// </para>
    /// </summary>
    public class UndeadTaskData : IData, IDataKey<string>
    {
        public int Id { get; set; }

        /// <summary>원본 과제 id (<c>activate_trees</c> …).</summary>
        public string Code { get; set; }

        /// <summary>배정 우선순위 [소스 <c>order</c>] — 낮은 것부터 NPC 에게 붙는다.</summary>
        public int Order { get; set; }

        /// <summary>제목 문구 키.</summary>
        public string TitleKey { get; set; }

        /// <summary>조건 문구 키 — <c>{goal}</c> 을 치환한다.</summary>
        public string RequirementKey { get; set; }

        /// <summary>어떤 지표를 세나 [소스 <c>metricId</c>].</summary>
        public string MetricId { get; set; }

        /// <summary><b>지금 지표에 더할</b> 목표치 [소스 <c>goalOffset</c>].</summary>
        public int GoalOffset { get; set; }

        /// <summary>다 하면 주는 스킬 [소스 <c>rewardSkillId</c>].</summary>
        public string RewardSkill { get; set; }

        /// <summary>
        /// 이 과제를 받으려면 «먼저 가지고 있어야 하는» 스킬 [소스 <c>prerequisiteSkillIds</c>].
        /// <para>비었으면 조건 없음. 세미콜론으로 여럿을 적는다.</para>
        /// </summary>
        public string PrerequisiteSkills { get; set; }

        public string Key
        {
            get { return Code; }
        }
    }
}
