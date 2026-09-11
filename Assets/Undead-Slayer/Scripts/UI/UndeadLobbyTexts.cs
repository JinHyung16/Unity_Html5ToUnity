using System.Collections.Generic;
using JinHyung.Core;
using JinHyung.Data;
using UnityEngine;

namespace JinHyung.UndeadSlayer
{
    /// <summary>
    /// 로비 UI 가 쓰는 <b>문구·아이콘 창구</b> — 표를 여기서만 본다.
    ///
    /// <para>
    /// ★ <b>화면 코드가 표를 직접 안 보게</b> 하는 것이 목적이다. 창이 표를 보면
    /// 그 창을 검사할 때 표를 통째로 세워야 하고, 문구 키가 <b>여러 창에 흩어진다</b>.
    /// </para>
    /// </summary>
    public sealed class UndeadLobbyTexts : ILobbyTexts
    {
        private readonly UndeadTextDataContainer _texts;
        private readonly UndeadTaskDataContainer _tasks;
        private readonly UndeadSkillDataContainer _skills;
        private readonly IReadOnlyDictionary<string, Sprite> _icons;

        public UndeadLobbyTexts(UndeadTextDataContainer texts, UndeadTaskDataContainer tasks,
                                UndeadSkillDataContainer skills, IReadOnlyDictionary<string, Sprite> icons)
        {
            _texts = texts;
            _tasks = tasks;
            _skills = skills;
            _icons = icons;
        }

        /// <summary>과제 조건 문구 — 인덱스는 <b>표의 줄 순서</b>다 (시뮬의 과제 인덱스와 같다).</summary>
        public string Requirement(int taskIndex)
        {
            if (_tasks == null || taskIndex < 0 || taskIndex >= _tasks.AllValues.Count)
                return string.Empty;

            return _texts.Ko(_tasks.AllValues[taskIndex].RequirementKey);
        }

        /// <summary>「보상:」 [소스 <c>`${i18n.get("reward")}:`</c>] — 콜론까지가 원본이다.</summary>
        public string RewardTitle
        {
            get { return _texts.Ko("reward") + ":"; }
        }

        public string SkillName(EUndeadSkill skill)
        {
            UndeadSkillData data = SkillOf(skill);
            return data == null ? string.Empty : _texts.Ko(data.NameKey);
        }

        public Sprite SkillIcon(EUndeadSkill skill)
        {
            UndeadSkillData data = SkillOf(skill);

            if (data == null || _icons == null)
                return null;

            return _icons.TryGetValue(data.IconAddress, out Sprite sprite) ? sprite : null;
        }

        /// <summary>포털 이름 [소스 <c>proximityCopyKey</c>].</summary>
        public string PortalName(int biome)
        {
            return _texts.Ko(biome == 1 ? "portalGraveyardName" : "portalWinterName");
        }

        /// <summary>잠긴 포털의 조건 문구 [소스 <c>lockedRequirementKey</c>] — 바이옴 1 에는 없다.</summary>
        public string PortalRequirement(int biome)
        {
            return biome == 1 ? string.Empty : _texts.Ko("portalWinterUnlockRequirement");
        }

        private UndeadSkillData SkillOf(EUndeadSkill skill)
        {
            if (_skills == null)
                return null;

            IReadOnlyList<UndeadSkillData> all = _skills.AllValues;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Order == (int)skill)
                    return all[i];
            }

            Log.Error($"스킬 슬롯 {skill} 에 해당하는 표 줄이 없다");
            return null;
        }
    }
}
