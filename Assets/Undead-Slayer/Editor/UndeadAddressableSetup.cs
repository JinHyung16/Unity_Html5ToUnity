using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Undead Slayer 의 <b>월드 아트를 어드레서블에 등록</b>한다 (확정표 10-c — UI 프리팹만 Resources).
    ///
    /// <para>
    /// ★★ <b>[사고] 이 도구가 «없어서» 새 아트가 조용히 안 보였다</b> (회차 9).
    /// 그림은 구워졌고 임포터 설정도 붙었는데 <b>주소가 없어</b> 로드가 <c>null</c> 이었고,
    /// 렌더러는 <b>아무 말 없이 안 그렸다</b> — 시뮬은 「적 27마리」라고 했고 화면엔 0마리였다.
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>아트를 «추가»했으면 이 도구를 돌린다.</b> 굽기 → 임포트 → <b>등록</b> → 씬 순서다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadAddressableSetup.Setup</c></para>
    /// </summary>
    public static class UndeadAddressableSetup
    {
        private const string GameArtFolder = "Assets/Undead-Slayer/Art/Game";

        /// <summary>UI 아트 중 «런타임에 주소로 읽는» 것 — 업그레이드 카드 아이콘 (`UndeadUpgradeTable.IconAddress`).</summary>
        private const string UiArtFolder = "Assets/Undead-Slayer/Art/UI";
        private const string DataFolder = "Assets/Undead-Slayer/Data";
        private const string GroupName = "GameArt";
        private const string DataGroupName = "GameData";
        private const string GameArtLabel = "game_art";
        private const string GameDataLabel = "game_data";

        public static void Setup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null)
            {
                Log.Error("어드레서블 설정을 만들지 못했다.");
                return;
            }

            int art = Register(settings, GameArtFolder, "t:Texture2D", GroupName, GameArtLabel);
            // ⚠ UI 프리팹은 Resources 지만 «카드 아이콘»은 표의 주소로 런타임에 읽는다 — 여기 안 넣으면 조용히 null 이다 (회차 10 사고)
            int ui = Register(settings, UiArtFolder, "t:Texture2D", GroupName, GameArtLabel);
            int data = Register(settings, DataFolder, "t:TextAsset", DataGroupName, GameDataLabel);

            AssetDatabase.SaveAssets();
            Log.Success($"Undead Slayer 어드레서블 등록 — 월드 아트 {art}장 · UI 아트 {ui}장 · 데이터 {data}개");

            Audit(settings);
        }

        private static int Register(AddressableAssetSettings settings, string folder, string filter,
                                    string groupName, string label)
        {
            if (AssetDatabase.IsValidFolder(folder) == false)
            {
                Log.Warning($"폴더가 없다: {folder}");
                return 0;
            }

            AddressableAssetGroup group = settings.FindGroup(groupName);

            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true,
                                             new List<AddressableAssetGroupSchema>(),
                                             typeof(BundledAssetGroupSchema),
                                             typeof(ContentUpdateGroupSchema));
            }

            settings.AddLabel(label, false);

            string[] guids = AssetDatabase.FindAssets(filter, new[] { folder });
            int count = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AddressableAssetEntry entry = settings.CreateOrMoveEntry(guids[i], group, false, false);

                if (entry == null)
                {
                    Log.Warning($"엔트리를 못 만들었다: {path}");
                    continue;
                }

                // ★ 주소는 «파일 이름»이다 — 표의 <c>Code</c> 와 같아야 한다.
                //   잘린 스프라이트는 <c>code[code_행_열]</c> 로 «자동»으로 붙는다.
                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(label, true, false, false);
                count++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
            return count;
        }

        /// <summary>
        /// <b>양방향 감사</b> — 표에 있는데 주소가 없거나, 주소가 있는데 표에 없으면 알린다.
        /// <para>⚠ 한쪽만 보면 이번 사고(표에 있는데 주소가 없음)를 «영원히» 못 잡는다.</para>
        /// </summary>
        private static void Audit(AddressableAssetSettings settings)
        {
            var container = new UndeadArtDataContainer();
            string json = Path.Combine(DataFolder, container.Name + ".json");

            if (File.Exists(json) == false)
                return;

            container.LoadJson(File.ReadAllText(json));

            var addresses = new HashSet<string>();
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, false);

            for (int i = 0; i < entries.Count; i++)
                addresses.Add(entries[i].address);

            int missing = 0;

            for (int i = 0; i < container.GameArt.Count; i++)
            {
                UndeadArtData a = container.GameArt[i];

                if (addresses.Contains(a.Code))
                    continue;

                Log.Error($"표에 있는 월드 아트 «{a.Code}» 에 주소가 없다 — 화면에 조용히 안 나온다");
                missing++;
            }

            // ★ 업그레이드 표가 가리키는 아이콘 주소 — «다른 표»를 가리키는 컬럼은 교차로 센다
            var upgrades = new UndeadUpgradeDataContainer();
            string upgradeJson = Path.Combine(DataFolder, upgrades.Name + ".json");
            int icons = 0;

            if (File.Exists(upgradeJson))
            {
                upgrades.LoadJson(File.ReadAllText(upgradeJson));

                for (int i = 0; i < upgrades.AllValues.Count; i++)
                {
                    string address = upgrades.AllValues[i].IconAddress;
                    icons++;

                    if (addresses.Contains(address))
                        continue;

                    Log.Error($"업그레이드 «{upgrades.AllValues[i].Code}» 의 아이콘 «{address}» 에 주소가 없다 — 카드가 기본 그림으로 뜬다");
                    missing++;
                }
            }

            if (missing == 0)
                Log.Success($"어드레서블 감사 통과 — 월드 아트 {container.GameArt.Count}종 · 카드 아이콘 {icons}종 전부 주소가 있다");
        }
    }
}
