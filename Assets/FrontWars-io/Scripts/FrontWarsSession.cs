using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JinHyung.Core;
using JinHyung.Data;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace JinHyung.FrontWarsIo
{
    /// <summary>싱글 설정 창의 선택값 — 원본 싱글 시작 설정 `[소스] BVamDUD6.js:useSinglePlayerController … let m={gameMap:e,difficulty:…}`.</summary>
    public sealed class FrontWarsSingleplayerOptions
    {
        public string MapName = "Australia";
        public EFrontWarsDifficulty Difficulty = EFrontWarsDifficulty.Medium;
        public EFrontWarsEconomyMode EconomyMode = EFrontWarsEconomyMode.Classic;

        /// <summary>개인전 · 팀전 `[소스] BVamDUD6.js:playerTeams:l.selectedMode===xa.Team?l.numberOfTeams:void 0`.</summary>
        public EFrontWarsGameMode GameMode = EFrontWarsGameMode.FFA;

        /// <summary>팀 수 — "2"~"7" · "Quads" · "Trios" · "Duos" · "HumansVsNations". 기본 2 `[소스] BVamDUD6.js:[_,v]=(0,p.useState)(2)`.</summary>
        public string PlayerTeams = "2";

        public int Bots = 400;

        /// <summary>null = 맵 기본 (전부).</summary>
        public int? Nations;

        public bool InstantBuild;
        public bool InfiniteGold;
        public bool InfiniteTroops;
        public bool CompactMap;
        public bool WaterNukes;
        public bool RandomSpawn;
        public bool DisableAlliances;
        public bool DonateGold = true;
        public bool DonateTroops = true;
        public HashSet<EFrontWarsUnit> DisabledUnits = new HashSet<EFrontWarsUnit>();
        public double? GoldMultiplier;
        public long? StartingGoldMillions;
        public int? GameLengthMinutes;
        public bool Tutorial;

        /// <summary>튜토리얼 표 값 — null 이면 기본.</summary>
        public int? SpawnPhaseTurns;
        public int? StartingTroops;
    }

    /// <summary>
    /// 한 판을 차린다 — 맵 바이트 읽기 · 표로 설정 만들기 · 시뮬 생성 · 매니저에 넘기기.
    /// <para>원본은 광고 콜백 뒤에 판을 연다 — 광고는 플랫폼 소속이라 옮기지 않았고 바로 연다 (`01_게임플로우.md` 「싱글 시작과 광고」).</para>
    /// </summary>
    public static class FrontWarsSession
    {
        public const string LocalClientId = "local";

        private const int GameIdLength = 8;
        private const string GameIdAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";

        private static readonly Dictionary<string, (FrontWarsTerrainMap Map, FrontWarsTerrainMap Mini)> MapCache = new Dictionary<string, (FrontWarsTerrainMap, FrontWarsTerrainMap)>();

        /// <summary>맵 바이트 · manifest 를 어드레서블에서 읽는다 (주소 «맵이름/map» · «맵이름/manifest»).</summary>
        public static async Task<(FrontWarsTerrainMap Map, FrontWarsTerrainMap Mini)> LoadMapAsync(string mapName, CancellationToken ct)
        {
            if (MapCache.TryGetValue(mapName, out var cached))
                return cached;

            string manifestText = await LoadTextAsync($"{mapName}/manifest", ct);
            JObject manifest = JObject.Parse(manifestText);
            int width = (int)manifest["map"]["width"];
            int height = (int)manifest["map"]["height"];

            byte[] bytes = await LoadBytesAsync($"{mapName}/map", ct);
            FrontWarsTerrainMap map = FrontWarsTerrainMap.FromRawTiles(bytes, width, height);

            // 축소 지도 = OpenFront map4x (원본 FrontWars 축소 파일과 땅 62칸 · 높이 162,950칸 차이 — 사람 결정 2026-09-17, 의도된 차이)
            byte[] miniBytes = await LoadBytesAsync($"{mapName}/map4x", ct);
            FrontWarsTerrainMap mini = FrontWarsTerrainMap.FromRawTiles(miniBytes, (int)manifest["map4x"]["width"], (int)manifest["map4x"]["height"]);

            MapCache[mapName] = (map, mini);
            return (map, mini);
        }

        private static async Task<byte[]> LoadBytesAsync(string address, CancellationToken ct)
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            TextAsset asset = await handle.Task;
            ct.ThrowIfCancellationRequested();

            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                throw new InvalidOperationException($"어드레서블을 못 읽었다: {address}");

            byte[] bytes = asset.bytes;
            Addressables.Release(handle);
            return bytes;
        }

        private static async Task<string> LoadTextAsync(string address, CancellationToken ct)
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(address);
            TextAsset asset = await handle.Task;
            ct.ThrowIfCancellationRequested();

            if (handle.Status != AsyncOperationStatus.Succeeded || asset == null)
                throw new InvalidOperationException($"어드레서블을 못 읽었다: {address}");

            string text = asset.text;
            Addressables.Release(handle);
            return text;
        }

        /// <summary>맵 썸네일 그림 — 주소 «맵이름/thumbnail». 없으면 null (그림 없이 보인다).</summary>
        public static async Task<Sprite> LoadMapThumbnailAsync(string mapName, CancellationToken ct)
        {
            if (ThumbnailCache.TryGetValue(mapName, out Sprite cached))
                return cached;

            AsyncOperationHandle<Sprite> handle = Addressables.LoadAssetAsync<Sprite>($"{mapName}/thumbnail");
            Sprite sprite = await handle.Task;
            ct.ThrowIfCancellationRequested();

            if (handle.Status != AsyncOperationStatus.Succeeded || sprite == null)
            {
                Log.Warning($"맵 썸네일을 못 읽었다: {mapName}");
                return null;
            }

            ThumbnailCache[mapName] = sprite;
            return sprite;
        }

        private static readonly Dictionary<string, Sprite> ThumbnailCache = new Dictionary<string, Sprite>();

        /// <summary>표 컨테이너로 판 설정을 만든다.</summary>
        public static FrontWarsGameSettings BuildSettings(FrontWarsSingleplayerOptions o, string gameId)
        {
            GameRoot g = GameRoot.Instance;

            FrontWarsGameSettings s = FrontWarsGameSettings.FromTables(
                g.FrontWarsConfigDataContainer.Config,
                g.FrontWarsDifficultyDataContainer.AllValues,
                g.FrontWarsPersonalityDataContainer.AllValues,
                g.FrontWarsTerrainDataContainer.AllValues,
                g.FrontWarsUnitDataContainer.AllValues,
                g.FrontWarsNukeDataContainer.AllValues,
                o.Difficulty,
                gameId);

            s.EconomyMode = o.EconomyMode;
            s.GameMode = o.GameMode;
            s.PlayerTeams = o.GameMode == EFrontWarsGameMode.Team ? o.PlayerTeams : null;
            s.Bots = o.Bots;
            s.Nations = o.Nations;
            s.InstantBuild = o.InstantBuild;
            s.InfiniteGold = o.InfiniteGold;
            s.InfiniteTroops = o.InfiniteTroops;
            s.CompactMap = o.CompactMap;
            s.WaterNukes = o.WaterNukes;
            s.RandomSpawn = o.RandomSpawn;
            s.DisableAlliances = o.DisableAlliances;
            s.DonateGold = o.DonateGold;
            s.DonateTroops = o.DonateTroops;
            s.DisabledUnits = new HashSet<EFrontWarsUnit>(o.DisabledUnits);
            s.GoldMultiplier = o.GoldMultiplier;
            s.StartingGoldMillions = o.StartingGoldMillions;
            s.GameLengthMinutes = o.GameLengthMinutes;
            s.Tutorial = o.Tutorial;
            s.SpawnPhaseTurnsOverride = o.SpawnPhaseTurns;
            s.StartingTroops = o.StartingTroops;
            return s;
        }

        /// <summary>판을 만들어 매니저에 넘긴다.</summary>
        public static async Task<FrontWarsGame> StartSingleplayerAsync(FrontWarsGameManager manager, FrontWarsSingleplayerOptions options, string humanName, CancellationToken ct)
        {
            var (map, mini) = await LoadMapAsync(options.MapName, ct);
            string gameId = NewGameId();
            FrontWarsGameSettings settings = BuildSettings(options, gameId);
            GameRoot g = GameRoot.Instance;

            FrontWarsGame game = FrontWarsGameSetup.Create(
                map,
                mini,
                settings,
                string.IsNullOrWhiteSpace(humanName) ? "Player" : humanName,
                LocalClientId,
                g.FrontWarsNationDataContainer.ForMap(options.MapName).ToList(),
                g.FrontWarsBotNameDataContainer.Prefixes,
                g.FrontWarsBotNameDataContainer.Suffixes);

            manager.StartGame(game, LocalClientId);
            Log.Success($"FrontWars 판 시작 — {options.MapName} · {options.Difficulty} · 봇 {options.Bots} · 게임 id {gameId}");
            return game;
        }

        /// <summary>8자리 무작위 게임 id (원본 Ka() 처럼 판마다 새로).</summary>
        public static string NewGameId()
        {
            var sb = new StringBuilder(GameIdLength);
            var random = new System.Random();

            for (int i = 0; i < GameIdLength; i++)
                sb.Append(GameIdAlphabet[random.Next(GameIdAlphabet.Length)]);

            return sb.ToString();
        }
    }
}
