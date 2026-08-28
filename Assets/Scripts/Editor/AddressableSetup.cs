using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 어드레서블을 <b>코드로</b> 세팅한다. 사람이 인스펙터를 헤맬 일이 없게 만든다.
    ///
    /// <para>
    /// 설정 에셋 생성 → 그룹 만들기 → 라벨 등록 → 대상 에셋 등록까지 한 번에 한다.
    /// <b>여러 번 눌러도 결과가 같다</b> (이미 있으면 건드리지 않는다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>UI 프리팹은 여기 등록하지 않는다.</b> UI 프리팹만 <c>Resources</c> 이고
    /// 나머지는 전부 어드레서블이다 (`CLAUDE.md` 「로드 규칙」).
    /// </para>
    /// </summary>
    public static class AddressableSetup
    {
        /// <summary>데이터 JSON 이 사는 곳. <c>Assets/&lt;게임명&gt;/Data/*.json</c></summary>
        private const string DataFolderName = "Data";

        /// <summary>아트가 사는 곳. <c>Assets/&lt;게임명&gt;/Art/**</c></summary>
        private const string ArtFolderName = "Art";

        private const string DataGroupName = "GameData";
        private const string ArtGroupName = "GameArt";

        /// <summary>아트 라벨. 카테고리 단위 로드/해제의 단위가 된다.</summary>
        public const string GameArtLabel = "game_art";

        public static void Setup()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null)
            {
                Log.Error("어드레서블 설정을 만들지 못했다.");
                return;
            }

            int dataCount = RegisterFolder(settings, DataFolderName, DataGroupName,
                                           DataManager.GameDataLabel, ".json");
            int artCount = RegisterFolder(settings, ArtFolderName, ArtGroupName,
                                          GameArtLabel, ".png", ".jpg", ".asset");

            AssetDatabase.SaveAssets();

            Log.Success($"어드레서블 세팅 완료 — 데이터 {dataCount}개(label: {DataManager.GameDataLabel}) · " +
                        $"아트 {artCount}개(label: {GameArtLabel})");

            if (dataCount == 0)
                Log.Warning($"등록된 데이터가 0개다. Assets/<게임명>/{DataFolderName}/ 에 json 이 있는지 본다.");
        }

        /// <summary>
        /// 게임 폴더의 특정 하위 폴더를 훑어 등록한다.
        /// <b>주소는 파일명(확장자 제외)</b>으로 둔다 — 컨테이너의 <c>Name</c> 과 맞추기 위해서다.
        /// </summary>
        private static int RegisterFolder(AddressableAssetSettings settings, string folderName,
                                          string groupName, string label, params string[] extensions)
        {
            var paths = CollectAssetPaths(folderName, extensions);

            if (paths.Count == 0)
                return 0;

            var group = FindOrCreateGroup(settings, groupName);

            if (group == null)
                return 0;

            settings.AddLabel(label, false);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                string guid = AssetDatabase.AssetPathToGUID(path);

                if (string.IsNullOrEmpty(guid))
                {
                    Log.Warning($"GUID 를 못 찾았다: {path}");
                    continue;
                }

                var entry = settings.CreateOrMoveEntry(guid, group, false, false);

                if (entry == null)
                {
                    Log.Warning($"엔트리를 못 만들었다: {path}");
                    continue;
                }

                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(label, true, false, false);
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
            return paths.Count;
        }

        /// <summary><c>Assets/&lt;게임명&gt;/&lt;folderName&gt;/</c> 아래의 대상 파일을 모은다. <c>Assets/Scripts</c> 는 뺀다.</summary>
        private static List<string> CollectAssetPaths(string folderName, string[] extensions)
        {
            var result = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Object", new[] { "Assets" });

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);

                if (path.StartsWith("Assets/Scripts/"))
                    continue;

                if (path.Contains($"/{folderName}/") == false)
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                    continue;

                string ext = Path.GetExtension(path).ToLowerInvariant();
                bool matched = false;

                for (int e = 0; e < extensions.Length; e++)
                {
                    if (ext == extensions[e])
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                    result.Add(path);
            }

            return result;
        }

        private static AddressableAssetGroup FindOrCreateGroup(AddressableAssetSettings settings, string groupName)
        {
            var group = settings.FindGroup(groupName);

            if (group != null)
                return group;

            return settings.CreateGroup(groupName, false, false, true,
                                        new List<AddressableAssetGroupSchema>(),
                                        typeof(BundledAssetGroupSchema),
                                        typeof(ContentUpdateGroupSchema));
        }
    }
}
