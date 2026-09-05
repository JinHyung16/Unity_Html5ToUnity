using System.Collections.Generic;
using System.IO;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Extensions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// 이 게임의 <b>월드 프리팹</b>을 어드레서블에 등록하고, <b>양방향 감사 3종</b>을 돌린다.
    ///
    /// <para>
    /// 공용 <c>AddressableSetup</c> 은 <c>Art/</c> 와 <c>Data/</c> 만 훑는다 (확장자 <c>.png/.jpg/.asset/.json</c>) —
    /// <b><c>Prefabs/</c> 의 <c>.prefab</c> 은 안 잡힌다</b>. 게임 전용 요구라 <b>여기</b>서 한다
    /// (CLAUDE.md 「두 번째 게임이 같은 것을 요구하면 승격」 — 아직 한 게임이다).
    /// </para>
    ///
    /// <para>
    /// ★ <b>파일을 만드는 생성기가 등록까지 책임진다</b> — 사람이 기억해서 등록하면 반드시 빠진다 (ResourceRule).
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiAddressableSetup.Setup</c></para>
    /// </summary>
    public static class BlumgiAddressableSetup
    {
        private const string PrefabFolder = "Assets/Blumgi-Bounce/Prefabs";
        private const string ArtFolder = "Assets/Blumgi-Bounce/Art";
        private const string PrefabGroupName = "GameArt";

        public static void Setup()
        {
            // 공용 세팅이 Art/Data 를 먼저 등록한다 — 순서가 뒤바뀌면 아트가 빠진 채 감사가 돈다.
            AddressableSetup.Setup();

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);

            if (settings == null)
            {
                Log.Error("어드레서블 설정을 만들지 못했다.");
                return;
            }

            int registered = RegisterPrefabs(settings);
            AssetDatabase.SaveAssets();

            Log.Success($"Blumgi 월드 프리팹 어드레서블 등록 — {registered}개");

            Audit(settings);
        }

        private static int RegisterPrefabs(AddressableAssetSettings settings)
        {
            if (AssetDatabase.IsValidFolder(PrefabFolder) == false)
            {
                Log.Warning($"프리팹 폴더가 없다: {PrefabFolder}");
                return 0;
            }

            AddressableAssetGroup group = settings.FindGroup(PrefabGroupName);

            if (group == null)
            {
                group = settings.CreateGroup(PrefabGroupName, false, false, true,
                                             new List<AddressableAssetGroupSchema>(),
                                             typeof(BundledAssetGroupSchema),
                                             typeof(ContentUpdateGroupSchema));
            }

            settings.AddLabel(AddressableSetup.GameArtLabel, false);

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });
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

                entry.address = Path.GetFileNameWithoutExtension(path);
                entry.SetLabel(AddressableSetup.GameArtLabel, true, false, false);
                count++;
            }

            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true, true);
            return count;
        }

        /// <summary>
        /// ★ <b>양방향 감사 3종</b> (ResourceRule 「어드레서블 등록은 양방향으로 감사한다」).
        ///
        /// <para>
        /// 동기화 도구는 「파일 → 주소」 한 방향만 본다. <b>반대 방향 — 코드가 요구하는 주소가 실제로 있는가 —</b>
        /// 는 아무도 안 본다. 그래서 이름이 어긋나면 <b>런타임에야</b> 터진다.
        /// </para>
        /// </summary>
        private static void Audit(AddressableAssetSettings settings)
        {
            var registered = new HashSet<string>();
            var entries = new List<AddressableAssetEntry>();
            settings.GetAllAssets(entries, true);

            int missingFile = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                AddressableAssetEntry entry = entries[i];
                registered.Add(entry.address);

                // ③ 등록됐는데 파일이 없는 «고아».
                //
                // ⚠ <b>감사기는 오탐을 낸다</b> (ResourceRule 「주의」). 실제로 두 종류를 만났다:
                //   ① 폴더 엔트리 — 파일이 아니다.
                //   ② <b>서브에셋 엔트리</b> — <c>GetAllAssets(recurseAll: true)</c> 는 텍스처 «안»의
                //      스프라이트까지 <c>block_base[block_base]</c> 형태로 돌려주는데 <c>AssetPath</c> 가 비어 있다.
                //      처음에 이걸 「고아 57건」으로 읽었다 — <b>전부 오탐이었다</b>.
                if (entry.IsSubAsset || entry.AssetPath.IsNullOrEmpty())
                    continue;

                if (AssetDatabase.IsValidFolder(entry.AssetPath))
                    continue;

                if (File.Exists(entry.AssetPath) == false)
                {
                    Log.Error($"[감사3 고아] 등록됐는데 파일이 없다: {entry.address} ({entry.AssetPath})");
                    missingFile++;
                }
            }

            // ① 코드가 요구하는 주소가 실제로 등록됐는가
            int missingAddress = 0;

            for (int i = 0; i < BlumgiArtAddress.All.Length; i++)
            {
                string address = BlumgiArtAddress.All[i];

                if (registered.Contains(address))
                    continue;

                Log.Error($"[감사1 주소] 코드가 요구하는데 등록이 없다: {address}");
                missingAddress++;
            }

            // ② 아트 파일명의 공백 · 복사본 접미 («이름 1.png» 는 조용한 시한폭탄이다)
            int badName = 0;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder });

            for (int i = 0; i < guids.Length; i++)
            {
                string file = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guids[i]));

                if (file.Contains(" ") == false)
                    continue;

                Log.Error($"[감사2 이름] 아트 파일명에 공백이 있다: {file}");
                badName++;
            }

            int total = missingAddress + badName + missingFile;

            if (total == 0)
                Log.Success($"양방향 감사 3종 0건 — 주소 {BlumgiArtAddress.All.Length}개 · 엔트리 {entries.Count}개");
            else
                Log.Error($"양방향 감사 {total}건 (주소 {missingAddress} · 이름 {badName} · 고아 {missingFile})");
        }
    }
}
