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
    /// FrontWars-io 데이터를 어드레서블에 등록한다 (확정표 10-c).
    ///
    /// <para>
    /// ⚠ 데이터 라벨(<c>game_data</c>)에는 <b>표 JSON 만</b> 넣는다. 맵 바이트(<c>Data/Maps</c>)는
    /// 따로 <c>fw_map</c> 라벨로 넣는다 — 같이 넣으면 데이터 로더가 3MB 바이너리를 문자열로 읽는다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsAddressableSetup.Setup</c></para>
    /// </summary>
    public static class FrontWarsAddressableSetup
    {
        private const string DataFolder = "Assets/FrontWars-io/Data";
        private const string MapFolder = "Assets/FrontWars-io/Data/Maps";
        private const string DataGroupName = "GameData";
        private const string MapGroupName = "FrontWarsMaps";
        private const string MapLabel = "fw_map";
        private const string ArtFolder = "Assets/FrontWars-io/Art/UI";
        private const string ArtGroupName = "FrontWarsArt";
        private const string ArtLabel = "fw_art";
        private const string ThumbnailPrefix = "MapThumbnail_";

        public static void Setup()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null)
            {
                Log.Error("어드레서블 설정을 만들지 못했다.");
                return;
            }

            int tables = 0, maps = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:TextAsset", new[] { DataFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isMap = path.StartsWith(MapFolder + "/");

                if (isMap)
                {
                    if (path.EndsWith(".md"))
                        continue;

                    // 주소 = 맵 폴더 이름 + 파일 이름 (Australia/map · Australia/manifest)
                    string map = Path.GetFileName(Path.GetDirectoryName(path));
                    Add(settings, guid, MapGroupName, MapLabel, $"{map}/{Path.GetFileNameWithoutExtension(path)}");
                    maps++;
                    continue;
                }

                if (path.EndsWith(".json") == false)
                    continue;

                Add(settings, guid, DataGroupName, DataManager.GameDataLabel, Path.GetFileNameWithoutExtension(path));
                tables++;
            }

            // 맵 썸네일 그림 — 주소 «맵이름/thumbnail» (FrontWarsMapThumbnail.BakeAll 이 굽는다)
            int thumbnails = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);

                if (file.StartsWith(ThumbnailPrefix) == false)
                    continue;

                Add(settings, guid, ArtGroupName, ArtLabel, $"{file.Substring(ThumbnailPrefix.Length)}/thumbnail");
                thumbnails++;
            }

            AssetDatabase.SaveAssets();
            Log.Success($"맵 썸네일 {thumbnails}장 등록");

            if (tables != FrontWarsContainerRegister.TableCount)
                Log.Error($"FrontWars 표 JSON {tables}개 — 등록한 컨테이너 {FrontWarsContainerRegister.TableCount}개와 다르다");
            else
                Log.Success($"FrontWars 어드레서블 등록 — 표 {tables}개 · 맵 파일 {maps}개");
        }

        private static void Add(AddressableAssetSettings settings, string guid, string groupName, string label, string address)
        {
            AddressableAssetGroup group = settings.FindGroup(groupName);

            if (group == null)
            {
                group = settings.CreateGroup(groupName, false, false, true,
                                             new List<AddressableAssetGroupSchema>(),
                                             typeof(BundledAssetGroupSchema),
                                             typeof(ContentUpdateGroupSchema));
            }

            settings.AddLabel(label, false);

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);

            if (entry == null)
            {
                Log.Warning($"엔트리를 못 만들었다: {AssetDatabase.GUIDToAssetPath(guid)}");
                return;
            }

            entry.address = address;
            entry.SetLabel(label, true, false, false);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true, true);
        }
    }
}
