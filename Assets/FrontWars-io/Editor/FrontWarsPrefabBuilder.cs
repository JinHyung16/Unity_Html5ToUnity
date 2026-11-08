using System.IO;
using JinHyung.Core;
using JinHyung.FrontWarsIo;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// FrontWars-io 창 프리팹을 <b>코드로 굽는다</b> (확정표 10-b — 첫 초안만, 이후 프리팹이 주인).
    ///
    /// <para>
    /// ★ 원본 HUD 는 창 크기와 무관한 px 고정이다 (`04_UIUX규칙.md` 「좌표계」) — 1920×1080 캔버스에 <b>원본 px 을 1:1</b> 로 넣는다.
    /// 인게임 HUD 좌표는 `04_UIUX규칙.md` 「인게임」 실측 표 그대로다. 메뉴 · 싱글 설정 · 판 끝 · 오른쪽 클릭 메뉴는 배치를 아직 안 쟀다 (미측정 표시).
    /// </para>
    ///
    /// <para>글꼴은 창이 열릴 때 <see cref="FrontWarsFonts.ApplyTo"/> 가 한글 글꼴로 바꾼다 — 프리팹에는 TMP 기본 글꼴이 들어간다.</para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.FrontWarsPrefabBuilder.BuildAll</c></para>
    /// </summary>
    public static class FrontWarsPrefabBuilder
    {
        private const string UiPrefabRoot = "Assets/FrontWars-io/Resources/UI/Window";

        private const float CanvasW = 1920f;
        private const float CanvasH = 1080f;

        private static readonly Color White = Color.white;
        private static readonly Color White70 = new Color(1f, 1f, 1f, 0.7f);
        private static readonly Color White80 = new Color(1f, 1f, 1f, 0.8f);
        private static readonly Color White75 = new Color(1f, 1f, 1f, 0.75f);
        private static readonly Color Gray = new Color32(156, 163, 175, 255);
        private static readonly Color Orange = new Color32(242, 149, 32, 255);

        /// <summary>미측정 — HUD 판 바탕 (반투명 어두운 회색).</summary>
        private static readonly Color PanelBg = new Color32(31, 41, 55, 179);

        /// <summary>미측정 — 창 바탕.</summary>
        private static readonly Color ModalBg = new Color32(30, 41, 59, 247);

        private static readonly Color ButtonBg = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color Accent = new Color32(59, 130, 246, 255);

        // ── 인게임 실측 [`04_UIUX규칙.md` 「인게임」]
        private const float EconomyX = 44f;
        private const float EconomyY = 32f;
        private const float EconomyFont = 11f;
        private const float LeaderboardToggleX = 16f;
        private const float LeaderboardToggleY = 24f;
        private const float LeaderboardY = 68f;
        private const float LeaderboardW = 400f;
        private const float HeaderH = 35f;
        private const float RowH = 40f;
        private const float ExpandH = 47f;
        private static readonly float[] ColumnWidths = { 28f, 152f, 70f, 70f, 80f };

        /// <summary>순위표 줄 수 — 1~4위 + 내 줄 [실측], 펼치면 10 + 내 줄 (미측정).</summary>
        private const int LeaderboardRows = 11;

        private static readonly float[] TopRightButtonX = { 1792f, 1824f, 1856f, 1888f };
        private const float TopRightButtonY = 24f;
        private const float TopRightButtonSize = 24f;
        private const float TimerX = 1805f;
        private const float TimerY = 63f;
        private const float TimerW = 94f;
        private const float TimerH = 21f;

        private const float ControlX = 28f;
        private const float ControlTop = 832f;
        private const float ControlW = 256f;
        private const float ControlBottom = 1052f;
        private const float ControlFont = 16f;
        private const float SliderH = 8f;

        private const float BuildSlotW = 80f;
        private const float BuildSlotH = 38f;
        private const float BuildSlotStep = 86f;
        private const float BuildRow1X = 705f;
        private const float BuildRow1Y = 968f;
        private const float BuildRow2X = 791f;
        private const float BuildRow2Y = 1014f;

        private const float EventsX = 1504f;
        private const float EventsTop = 963f;
        private const float EventsW = 400f;
        private const float EventsBottom = 1064f;
        private const float FilterSize = 32f;
        private const float FilterStep = 40f;
        private const float HideW = 55f;
        private const float HideH = 29f;

        /// <summary>미측정 — 이벤트 한 줄 높이 · 보이는 줄 수.</summary>
        private const float EventRowH = 26f;
        private const int EventRows = 10;

        private const int RadialButtons = 12;

        public static void BuildAll()
        {
            Directory.CreateDirectory(UiPrefabRoot);

            BuildMenu();
            BuildSingleplayer();
            BuildHud();
            BuildGameOver();
            BuildRadial();
            BuildTutorial();
            BuildInstructions();
            BuildQuickChat();
            BuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.Success("FrontWars-io 창 프리팹 굽기 완료 — 메뉴 · 싱글 설정 · HUD · 판 끝 · 오른쪽 클릭 메뉴");
        }

        // ── 메뉴 — 자리 · 글자 크기 실측 `[실측] probe/menu_flow_ko2.json menu_ko` (색은 미측정)

        private static void BuildMenu()
        {
            GameObject root = WindowRoot(nameof(FrontWarsMenuWindow));
            var window = root.AddComponent<FrontWarsMenuWindow>();

            // 로고 자리 [실측 0,0,212,63] — 원본은 그림 로고, 우리는 글자 (리소스 단위에서 다시 그린다)
            TMP_Text title = Text(root.transform, "Title", "FrontWars", 26f, White, FontStyles.Bold);
            PlaceScreen(title.rectTransform, 0f, 0f, 212f, 63f);

            // 이름 입력 [실측 842,154,584,56]
            TMP_InputField input = InputField(root.transform, "NameInput", out TMP_Text placeholder);
            PlaceMeasured((RectTransform)input.transform, 842f, 154f, 584f, 56f);

            // 싱글 플레이어 [실측 572,732,210,132 · 글자 16px, 칸 아래쪽 y 823]
            Button single = ButtonWithLabel(root.transform, "SingleplayerButton", Accent, "싱글 플레이", 16f, out TMP_Text singleLabel);
            PlaceMeasured((RectTransform)single.transform, 572f, 732f, 210f, 132f);
            PlaceTopLeft(singleLabel.rectTransform, 0f, 91f, 210f, 20f);

            // 튜토리얼 · 설명 [실측 572,880,433,64 · 1019,880,433,64]
            Button tutorial = ButtonWithLabel(root.transform, "TutorialButton", new Color(1f, 1f, 1f, 0.12f), "튜토리얼", 16f, out TMP_Text tutorialLabel);
            PlaceMeasured((RectTransform)tutorial.transform, 572f, 880f, 433f, 64f);

            Button instructions = ButtonWithLabel(root.transform, "InstructionsButton", new Color(1f, 1f, 1f, 0.12f), "게임 방법", 16f, out TMP_Text instructionsLabel);
            PlaceMeasured((RectTransform)instructions.transform, 1019f, 880f, 433f, 64f);

            // 지도 데이터 저작자 표시 — 화면 오른쪽 아래 구석 (원본에 없는 줄 · CC BY-SA 4.0 의무)
            TMP_Text mapCredit = Text(root.transform, "MapCredit", "-", 11f, new Color(1f, 1f, 1f, 0.5f), FontStyles.Normal, TextAlignmentOptions.BottomRight);
            PlaceScreen(mapCredit.rectTransform, CanvasW - 16f - 400f, CanvasH - 8f - 16f, 400f, 16f);

            SetRef(window, "_mapCredit", mapCredit);
            SetRef(window, "_title", title);
            SetRef(window, "_nameInput", input);
            SetRef(window, "_namePlaceholder", placeholder);
            SetRef(window, "_singleplayerButton", single);
            SetRef(window, "_singleplayerLabel", singleLabel);
            SetRef(window, "_tutorialButton", tutorial);
            SetRef(window, "_tutorialLabel", tutorialLabel);
            SetRef(window, "_instructionsButton", instructions);
            SetRef(window, "_instructionsLabel", instructionsLabel);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsMenuWindow)}.prefab");
        }

        // ── 싱글 설정 — 자리 · 글자 크기 실측 `[실측] probe/menu_flow_ko2.json single_rules_ko_scroll0` (화면 px, 가운데 앵커)
        //    ⚠ 원본 창은 규칙 줄이 119px 스크롤된다 — 1080 높이 안에 들어가 스크롤은 없다. 창 테두리 · 색은 미측정.

        private static readonly float[] DifficultyX = { 494f, 670f, 845f, 1021f };
        private static readonly float[] ModeX = { 494f, 668f };
        private static readonly float[] EconomyButtonX = { 849f, 1023f };
        private static readonly float[] RuleX = { 494f, 729f, 963f };
        private static readonly float[] RuleY = { 568f, 623f, 677f };
        private static readonly float[] UnitX = { 494f, 728f, 962f };
        private static readonly float[] UnitY = { 879f, 927f, 974f, 1022f };

        /// <summary>규칙 스크롤 창 왼쪽 위 (화면 px) — 난이도 줄 위 여백을 둔 자리 (창 테두리 미측정).</summary>
        private const float RulesLeft = 477f;
        private const float RulesTop = 245f;

        /// <summary>화면 px 좌표를 규칙 스크롤 내용 안 자리로 옮긴다.</summary>
        private static void AtRules(RectTransform rect, float x, float y, float w, float h)
        {
            PlaceTopLeft(rect, x - RulesLeft, y - RulesTop, w, h);
        }

        private static void BuildSingleplayer()
        {
            GameObject root = WindowRoot(nameof(FrontWarsSingleplayerWindow));
            var window = root.AddComponent<FrontWarsSingleplayerWindow>();
            Dimmer(root.transform, 0.6f);

            // 창 판 — 뒤로(477,129) 와 게임 시작(1217~1547) 을 감싸는 자리 (테두리 미측정)
            RectTransform panel = Panel(root.transform, "Panel", ModalBg);
            PlaceMeasured(panel, 460f, 112f, 1104f, 960f);
            Round(panel, 16f);

            TMP_Text title = Text(root.transform, "Title", "싱글 플레이어", 26f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            PlaceMeasured(title.rectTransform, 535f, 131f, 300f, 39f);

            Button back = ButtonWithLabel(root.transform, "BackButton", ButtonBg, "뒤로", 15f, out TMP_Text backLabel);
            PlaceMeasured((RectTransform)back.transform, 477f, 129f, 44f, 44f);
            backLabel.enableAutoSizing = true;
            backLabel.fontSizeMin = 8f;
            backLabel.fontSizeMax = 15f;

            Button mapTab = ButtonWithLabel(root.transform, "MapTab", ButtonBg, "지도", 15f, out TMP_Text mapTabLabel);
            PlaceMeasured((RectTransform)mapTab.transform, 477f, 189f, 358f, 43f);

            Button rulesTab = ButtonWithLabel(root.transform, "RulesTab", ButtonBg, "규칙 및 옵션", 15f, out TMP_Text rulesTabLabel);
            PlaceMeasured((RectTransform)rulesTab.transform, 843f, 189f, 358f, 43f);

            RectTransform mapPanel = Group(root.transform, "MapPanel");
            Stretch(mapPanel);

            // 맵 칸 — 4열 칸 158~162px, 첫 칸 y 418 [실측]. 호주 한 칸만 첫 자리에 둔다
            Button mapTile = ButtonWithLabel(mapPanel, "MapTile", new Color(1f, 1f, 1f, 0.25f), "호주", 12f, out TMP_Text mapTileLabel);
            mapTileLabel.fontStyle = FontStyles.Bold;
            mapTileLabel.color = White75;
            PlaceMeasured((RectTransform)mapTile.transform, 494f, 418f, 160f, 160f);

            // 썸네일 그림 — 칸 안쪽 [실측 158×158]. 그림은 굽는 도구가 만든다 (FrontWarsMapThumbnail)
            RectTransform thumb = Panel(mapTile.transform, "Thumbnail", White);
            thumb.GetComponent<Image>().raycastTarget = false;
            Stretch(thumb);
            thumb.offsetMin = new Vector2(1f, 1f);
            thumb.offsetMax = new Vector2(-1f, -1f);
            thumb.SetAsFirstSibling();
            mapTileLabel.rectTransform.SetAsLastSibling();
            SetRef(window, "_mapThumbnail", thumb.GetComponent<Image>());

            // 규칙 탭 — 원본처럼 스크롤 창 안에 둔다. 팀전이면 팀 수 묶음(163px)만큼 아래 줄이 내려가 스크롤이 생긴다 [실측]
            RectTransform rulesPanel = Group(root.transform, "RulesPanel");
            Stretch(rulesPanel);

            RectTransform viewport = Panel(rulesPanel, "Viewport", new Color(0f, 0f, 0f, 0.001f));
            PlaceMeasured(viewport, RulesLeft, RulesTop, 740f, 1072f - RulesTop);
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = Group(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, FrontWarsSingleplayerWindow.RulesContentHeight);

            var scroll = rulesPanel.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            // 규칙 탭에는 난이도 · 모드 · 경제 묶음 제목이 없다 [실측] — 제목은 굽되 끈다
            TMP_Text difficultyTitle = Text(content, "DifficultyTitle", "난이도", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            AtRules(difficultyTitle.rectTransform, 494f, 270f, 200f, 20f);
            difficultyTitle.gameObject.SetActive(false);

            var difficultyButtons = new Button[4];
            var difficultyLabels = new TMP_Text[4];
            for (int i = 0; i < 4; i++)
            {
                difficultyButtons[i] = ButtonWithLabel(content, $"Difficulty{i}", ButtonBg, "-", 15f, out difficultyLabels[i]);
                AtRules((RectTransform)difficultyButtons[i].transform, DifficultyX[i], 297f, 168f, 43f);
            }

            TMP_Text modeTitle = Text(content, "ModeTitle", "모드", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            AtRules(modeTitle.rectTransform, 494f, 360f, 200f, 20f);
            modeTitle.gameObject.SetActive(false);

            var modeButtons = new Button[2];
            var modeLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                modeButtons[i] = ButtonWithLabel(content, $"Mode{i}", ButtonBg, "-", 15f, out modeLabels[i]);
                AtRules((RectTransform)modeButtons[i].transform, ModeX[i], 387f, 166f, 43f);
            }

            TMP_Text economyTitle = Text(content, "EconomyTitle", "경제", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            AtRules(economyTitle.rectTransform, 849f, 360f, 200f, 20f);
            economyTitle.gameObject.SetActive(false);

            var economyButtons = new Button[2];
            var economyLabels = new TMP_Text[2];
            for (int i = 0; i < 2; i++)
            {
                economyButtons[i] = ButtonWithLabel(content, $"Economy{i}", ButtonBg, "-", 15f, out economyLabels[i]);
                AtRules((RectTransform)economyButtons[i].transform, EconomyButtonX[i], 387f, 166f, 43f);
            }

            // 팀 수 [실측 probe/team_options_team.json — 제목 494,445 (14px 굵게 흰색 40%) · 첫 줄 2인/3인/4인 팀 · 인류 vs 국가 (난이도 열) · 둘째 줄 2~7팀 x 494+117n, 폭 109]
            RectTransform teamsRoot = Group(content, "Teams");
            AtRules(teamsRoot, 494f, 445f, 695f, FrontWarsSingleplayerWindow.TeamsBlockHeight);
            TMP_Text teamsTitle = Text(teamsRoot, "TeamsTitle", "팀 수", 14f, new Color(1f, 1f, 1f, 0.4f), FontStyles.Bold, TextAlignmentOptions.Center);
            PlaceTopLeft(teamsTitle.rectTransform, 0f, 0f, 694f, 21f);

            int teamCount = FrontWarsSingleplayerWindow.TeamChoices.Length;
            var teamButtons = new Button[teamCount];
            var teamLabels = new TMP_Text[teamCount];
            for (int i = 0; i < teamCount; i++)
            {
                bool firstRow = i < 4;
                teamButtons[i] = ButtonWithLabel(teamsRoot, $"Team{i}", ButtonBg, "-", 15f, out teamLabels[i]);

                if (firstRow)
                    PlaceTopLeft((RectTransform)teamButtons[i].transform, DifficultyX[i] - 494f, 31f, 168f, 64f);
                else
                    PlaceTopLeft((RectTransform)teamButtons[i].transform, 117f * (i - 4), 105f, 109f, 43f);
            }

            // 팀 수 아래 줄 — 팀전이면 창이 통째로 163px 내린다
            RectTransform below = Group(content, "BelowTeams");
            below.anchorMin = new Vector2(0f, 1f);
            below.anchorMax = new Vector2(1f, 1f);
            below.pivot = new Vector2(0f, 1f);
            below.anchoredPosition = Vector2.zero;
            below.sizeDelta = new Vector2(0f, FrontWarsSingleplayerWindow.RulesContentHeight);

            // 봇 · 국가 [실측 글자 509,497 · 861,497 (14px) · 슬라이더 509,531,312×8 · 861,531]
            TMP_Text botsLabel = Text(below, "BotsLabel", "봇", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            AtRules(botsLabel.rectTransform, 509f, 497f, 312f, 19f);
            Slider bots = MakeSlider(below, "BotsSlider");
            AtRules((RectTransform)bots.transform, 509f, 531f, 312f, SliderH);

            TMP_Text nationsLabel = Text(below, "NationsLabel", "네이션", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            AtRules(nationsLabel.rectTransform, 861f, 497f, 312f, 19f);
            Slider nations = MakeSlider(below, "NationsSlider");
            AtRules((RectTransform)nations.transform, 861f, 531f, 312f, SliderH);

            // 규칙 토글 7 [실측 225×45 · x 494/729/963 · y 568/623/677 · 글자 15px]
            int ruleCount = FrontWarsSingleplayerWindow.RuleKeys.Length;
            var ruleToggles = new Toggle[ruleCount];
            var ruleLabels = new TMP_Text[ruleCount];
            for (int i = 0; i < ruleCount; i++)
            {
                ruleToggles[i] = MakeToggle(below, $"Rule{i}", 15f, out ruleLabels[i]);
                AtRules((RectTransform)ruleToggles[i].transform, RuleX[i % 3], RuleY[i / 3], 225f, 45f);
            }

            // 시작 골드 · 골드 배율 · 게임 시간 카드 [실측 probe/gameover.json rulesAfter — 225×89 · 흰색 5% · 테두리 흰색 20%(켜면 주황) · 점 10 · 제목 +43,+11 15px · 「—」 24px 흰색 25% · 입력 +13,+41 199×36 · 지우기 22×22]
            int extraCount = FrontWarsSingleplayerWindow.ExtraKeys.Length;
            var extraCards = new Button[extraCount];
            var extraBorders = new Outline[extraCount];
            var extraDots = new Image[extraCount];
            var extraLabels = new TMP_Text[extraCount];
            var extraOffValues = new TMP_Text[extraCount];
            var extraInputs = new TMP_InputField[extraCount];
            var extraClears = new Button[extraCount];

            for (int i = 0; i < extraCount; i++)
            {
                Button card = PlainButton(below, $"Extra{i}", new Color(1f, 1f, 1f, 0.05f));
                AtRules((RectTransform)card.transform, RuleX[i], 738f, 225f, 89f);
                extraCards[i] = card;

                extraBorders[i] = card.gameObject.AddComponent<Outline>();
                extraBorders[i].effectColor = new Color(1f, 1f, 1f, 0.2f);
                extraBorders[i].effectDistance = new Vector2(1f, -1f);

                RectTransform dot = Panel(card.transform, "Dot", Orange);
                dot.GetComponent<Image>().raycastTarget = false;
                PlaceTopLeft(dot, 18f, 18f, 10f, 10f);
                extraDots[i] = dot.GetComponent<Image>();

                extraLabels[i] = Text(card.transform, "Label", "-", 15f, White, FontStyles.Normal, TextAlignmentOptions.Left);
                PlaceTopLeft(extraLabels[i].rectTransform, 43f, 11f, 170f, 23f);

                extraOffValues[i] = Text(card.transform, "OffValue", "—", 24f, new Color(1f, 1f, 1f, 0.25f), FontStyles.Normal);
                PlaceTopLeft(extraOffValues[i].rectTransform, 13f, 47f, 199f, 24f);

                TMP_InputField input = InputField(card.transform, "Input", out TMP_Text placeholder);
                placeholder.text = string.Empty;
                input.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
                input.textComponent.fontSize = 15f;
                input.contentType = i == 1 ? TMP_InputField.ContentType.DecimalNumber : TMP_InputField.ContentType.IntegerNumber;
                PlaceTopLeft((RectTransform)input.transform, 13f, 41f, 199f, 36f);
                extraInputs[i] = input;

                Button clear = ButtonWithLabel(card.transform, "Clear", new Color(0f, 0f, 0f, 0f), "×", 16f, out TMP_Text clearLabel);
                clearLabel.color = new Color(1f, 1f, 1f, 0.3f);
                PlaceTopLeft((RectTransform)clear.transform, 194f, 7f, 22f, 22f);
                extraClears[i] = clear;
            }

            // 유닛 토글 11 [실측 226×40 · x 494/728/962 · y 879/927/974/1022 · 글자 14px]
            int unitCount = FrontWarsSingleplayerWindow.UnitToggles.Length;
            var unitToggles = new Toggle[unitCount];
            var unitLabels = new TMP_Text[unitCount];
            for (int i = 0; i < unitCount; i++)
            {
                unitToggles[i] = MakeToggle(below, $"Unit{i}", 14f, out unitLabels[i]);
                AtRules((RectTransform)unitToggles[i].transform, UnitX[i % 3], UnitY[i / 3], 226f, 40f);
            }

            SetRef(window, "_rulesContent", content);
            SetRef(window, "_belowTeams", below);

            // 오른쪽 요약 [실측 — 이름 14px · 값 15px (모디파이어 값 20px)]
            (float x, float y, float w)[] summary =
            {
                (1286f, 200f, 248f), (1230f, 275f, 135f), (1399f, 275f, 135f), (1230f, 347f, 135f), (1399f, 347f, 135f), (1230f, 422f, 304f)
            };

            var summaryLabels = new TMP_Text[summary.Length];
            var summaryValues = new TMP_Text[summary.Length];
            for (int i = 0; i < summary.Length; i++)
            {
                summaryLabels[i] = Text(root.transform, $"SummaryLabel{i}", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
                PlaceMeasured(summaryLabels[i].rectTransform, summary[i].x, summary[i].y, summary[i].w, 21f);

                bool modifiers = i == summary.Length - 1;
                summaryValues[i] = Text(root.transform, $"SummaryValue{i}", "-", modifiers ? 20f : 15f, White, FontStyles.Bold, TextAlignmentOptions.Left);
                summaryValues[i].overflowMode = TextOverflowModes.Ellipsis;
                PlaceMeasured(summaryValues[i].rectTransform, summary[i].x, summary[i].y + (modifiers ? 29f : 23f), summary[i].w, modifiers ? 24f : 23f);
            }

            // 게임 시작 [실측 1217,905,330,54]
            Button start = ButtonWithLabel(root.transform, "StartButton", Accent, "게임 시작", 18f, out TMP_Text startLabel);
            PlaceMeasured((RectTransform)start.transform, 1217f, 905f, 330f, 54f);

            SetRef(window, "_title", title);
            SetRef(window, "_backButton", back);
            SetRef(window, "_backLabel", backLabel);
            SetRef(window, "_startButton", start);
            SetRef(window, "_startLabel", startLabel);
            SetRef(window, "_mapTab", mapTab);
            SetRef(window, "_mapTabLabel", mapTabLabel);
            SetRef(window, "_rulesTab", rulesTab);
            SetRef(window, "_rulesTabLabel", rulesTabLabel);
            SetRef(window, "_mapPanel", mapPanel.gameObject);
            SetRef(window, "_rulesPanel", rulesPanel.gameObject);
            SetRef(window, "_mapTile", mapTile);
            SetRef(window, "_mapTileLabel", mapTileLabel);
            SetRef(window, "_difficultyTitle", difficultyTitle);
            SetRefArray(window, "_difficultyButtons", difficultyButtons);
            SetRefArray(window, "_difficultyLabels", difficultyLabels);
            SetRef(window, "_modeTitle", modeTitle);
            SetRefArray(window, "_modeButtons", modeButtons);
            SetRefArray(window, "_modeLabels", modeLabels);
            SetRef(window, "_teamsRoot", teamsRoot.gameObject);
            SetRef(window, "_teamsTitle", teamsTitle);
            SetRefArray(window, "_teamButtons", teamButtons);
            SetRefArray(window, "_teamLabels", teamLabels);
            SetRef(window, "_economyTitle", economyTitle);
            SetRefArray(window, "_economyButtons", economyButtons);
            SetRefArray(window, "_economyLabels", economyLabels);
            SetRef(window, "_botsLabel", botsLabel);
            SetRef(window, "_botsSlider", bots);
            SetRef(window, "_nationsLabel", nationsLabel);
            SetRef(window, "_nationsSlider", nations);
            SetRefArray(window, "_ruleToggles", ruleToggles);
            SetRefArray(window, "_ruleLabels", ruleLabels);
            SetRefArray(window, "_unitToggles", unitToggles);
            SetRefArray(window, "_unitLabels", unitLabels);
            SetRefArray(window, "_extraCards", extraCards);
            SetRefArray(window, "_extraBorders", extraBorders);
            SetRefArray(window, "_extraDots", extraDots);
            SetRefArray(window, "_extraLabels", extraLabels);
            SetRefArray(window, "_extraOffValues", extraOffValues);
            SetRefArray(window, "_extraInputs", extraInputs);
            SetRefArray(window, "_extraClears", extraClears);
            SetRefArray(window, "_summaryLabels", summaryLabels);
            SetRefArray(window, "_summaryValues", summaryValues);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsSingleplayerWindow)}.prefab");
        }

        // ── 인게임 HUD (실측 좌표)

        private static void BuildHud()
        {
            GameObject root = WindowRoot(nameof(FrontWarsHudWindow));
            var window = root.AddComponent<FrontWarsHudWindow>();

            // 창 전체가 클릭을 먹으면 지도를 못 누른다 — 바탕 없이, 요소만 레이캐스트한다
            root.GetComponent<CanvasGroup>().blocksRaycasts = true;

            // 경고 테두리 — 네 변 그라데이션 띠 (위 · 아래 · 왼쪽 · 오른쪽). 두께 · 색 · 깜빡임은 컴포넌트가 원본 식으로 그린다
            RectTransform alert = Group(root.transform, "AlertFrame");
            Stretch(alert);
            var alertFrame = alert.gameObject.AddComponent<FrontWarsAlertFrame>();
            (Vector2 Min, Vector2 Max)[] edges =
            {
                (new Vector2(0f, 1f), new Vector2(1f, 1f)),
                (new Vector2(0f, 0f), new Vector2(1f, 0f)),
                (new Vector2(0f, 0f), new Vector2(0f, 1f)),
                (new Vector2(1f, 0f), new Vector2(1f, 1f))
            };
            var edgeImages = new Image[edges.Length];

            for (int i = 0; i < edges.Length; i++)
            {
                RectTransform edge = Panel(alert, $"Edge{i}", Color.clear);
                edge.GetComponent<Image>().raycastTarget = false;
                edge.anchorMin = edges[i].Min;
                edge.anchorMax = edges[i].Max;
                edge.pivot = new Vector2(0.5f, 0.5f);
                edge.anchoredPosition = Vector2.zero;
                edge.sizeDelta = Vector2.zero;
                edgeImages[i] = edge.GetComponent<Image>();
            }

            SetRefArray(alertFrame, "_edges", edgeImages);
            SetRef(window, "_alertFrame", alertFrame);

            // ── 왼쪽 위
            Button toggle = PlainButton(root.transform, "LeaderboardToggle", ButtonBg);
            PlaceScreen((RectTransform)toggle.transform, LeaderboardToggleX, LeaderboardToggleY, 20f, 36f);
            Image toggleIcon = IconImage(toggle.transform, 18f, White);
            SetRef(window, "_leaderboardIcon", toggleIcon);

            TMP_Text economy = Text(root.transform, "EconomyText", "클래식", EconomyFont, White80, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceScreen(economy.rectTransform, EconomyX, EconomyY, 200f, 16f);

            // 팀 글자 — 팀전에서만 · 바탕 없는 팀 색 글자 · 누르면 순위표 접기 [실측 44,32,286×21 · 14px 500 · aria 「Toggle Leaderboard」]
            Button team = PlainButton(root.transform, "TeamButton", new Color(1f, 1f, 1f, 0f));
            PlaceScreen((RectTransform)team.transform, 44f, 32f, 286f, 21f);
            TMP_Text teamLabel = Text(team.transform, "Label", "Team: -", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            teamLabel.overflowMode = TextOverflowModes.Ellipsis;
            Stretch(teamLabel.rectTransform);
            team.gameObject.SetActive(false);
            SetRef(window, "_teamButton", team);
            SetRef(window, "_teamLabel", teamLabel);

            BuildLeaderboard(root.transform, window);

            // ── 오른쪽 위 판 [실측 1784,16,136×83 · 검정 41% · 왼쪽만 둥글게 6] — 속도 판이 열리면 124 로 길어진다
            RectTransform topRight = Panel(root.transform, "TopRightPanel", new Color(0f, 0f, 0f, 0.41f));
            topRight.GetComponent<Image>().raycastTarget = false;
            PlaceScreen(topRight, 1784f, 16f, 136f, 83f);
            Round(topRight, 6f);
            SetRef(window, "_topRightPanel", topRight);

            // 타이머 칸 [실측 1792,56,120×35 · 검정 40% · 둥근 4 · 테두리 1px 검정 20%]
            RectTransform timerBox = Panel(root.transform, "TimerBox", new Color(0f, 0f, 0f, 0.4f));
            timerBox.GetComponent<Image>().raycastTarget = false;
            PlaceScreen(timerBox, 1792f, 56f, 120f, 35f);
            Round(timerBox, 4f);

            // 다시 보기 · 일시정지 · 설정 · 나가기 [실측 자리 24×24 · 바탕 없음 · 흰 아이콘] · 아이콘은 코드로 그린 도형 (원본 SVG 안 옮김)
            string[] names = { "ReplayButton", "PauseButton", "SettingsButton", "ExitButton" };
            string[] fields = { "_replayButton", "_pauseButton", "_settingsButton", "_exitButton" };
            string[] iconFields = { "_replayIcon", "_pauseIcon", "_settingsIcon", "_exitIcon" };
            Color iconColor = White;

            for (int i = 0; i < 4; i++)
            {
                Button b = PlainButton(root.transform, names[i], new Color(1f, 1f, 1f, 0f));
                PlaceScreen((RectTransform)b.transform, TopRightButtonX[i], TopRightButtonY, TopRightButtonSize, TopRightButtonSize);
                Image icon = IconImage(b.transform, 16f, iconColor);
                SetRef(window, fields[i], b);
                SetRef(window, iconFields[i], icon);
            }

            TMP_Text timer = Text(root.transform, "TimerText", "-", 14f, White, FontStyles.Bold);
            PlaceScreen(timer.rectTransform, TimerX, TimerY, TimerW, TimerH);

            BuildSpeedPanel(root.transform, window);
            BuildTopBar(root.transform, window);

            // 스폰 안내 [실측 first_screen.json 817,558,285×35 · 14px 흰색]
            TMP_Text spawnHint = Text(root.transform, "SpawnHint", "시작할 땅을 누르세요", 14f, White, FontStyles.Normal);
            PlaceMeasured(spawnHint.rectTransform, 817f, 558f, 285f, 35f);

            BuildControlPanel(root.transform, window);
            BuildBuildBar(root.transform, window);
            BuildEvents(root.transform, window);
            BuildPlayerInfo(root.transform, window);

            SetRef(window, "_economyText", economy);
            SetRef(window, "_timerText", timer);
            SetRef(window, "_leaderboardToggle", toggle);
            SetRef(window, "_spawnHint", spawnHint);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsHudWindow)}.prefab");
        }

        /// <summary>
        /// 플레이어 정보 판 — 오른쪽 위 [실측 1690,160,230×321 · 검정 41%] (`04_UIUX규칙.md` 「플레이어 정보 판」).
        /// <para>줄은 위에서 아래로 쌓인다 — 공격 중 병력 줄이 없으면 골드 칸이 올라온다 [실측 봇 · 사람 비교].</para>
        /// </summary>
        private static void BuildPlayerInfo(Transform parent, Component window)
        {
            RectTransform panel = Panel(parent, "PlayerInfo", new Color(0f, 0f, 0f, 0.41f));
            PlaceScreen(panel, 1690f, 160f, 230f, 321f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 이름 [실측 18px 600]
            TMP_Text name = Text(panel, "Name", "-", 18f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            name.overflowMode = TextOverflowModes.Ellipsis;
            Height(name.rectTransform, 41f);

            RectTransform typeRow = Group(panel, "TypeRow");
            Height(typeRow, 20f);
            TMP_Text type = Text(typeRow, "Type", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(type.rectTransform);
            TMP_Text relation = Text(typeRow, "Relation", "-", 14f, White, FontStyles.Bold, TextAlignmentOptions.Right);
            Stretch(relation.rectTransform);

            RectTransform troopsRow = Group(panel, "TroopsRow");
            Height(troopsRow, 20f);
            TMP_Text troopsLabel = Text(troopsRow, "Label", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(troopsLabel.rectTransform);
            TMP_Text troops = Text(troopsRow, "Value", "-", 14f, White, FontStyles.Bold, TextAlignmentOptions.Right);
            Stretch(troops.rectTransform);

            RectTransform attackingRow = Group(panel, "AttackingRow");
            Height(attackingRow, 20f);
            TMP_Text attackingLabel = Text(attackingRow, "Label", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(attackingLabel.rectTransform);
            TMP_Text attacking = Text(attackingRow, "Value", "-", 14f, White, FontStyles.Bold, TextAlignmentOptions.Right);
            Stretch(attacking.rectTransform);

            // 골드 칸 [실측 198×44 · 검정 40% · 테두리 2px 노랑]
            Color gold = new Color32(234, 179, 8, 255);
            RectTransform goldBox = Panel(panel, "GoldBox", new Color(0f, 0f, 0f, 0.4f));
            goldBox.GetComponent<Image>().raycastTarget = false;
            Height(goldBox, 44f);
            var outline = goldBox.gameObject.AddComponent<Outline>();
            outline.effectColor = gold;
            outline.effectDistance = new Vector2(2f, -2f);
            TMP_Text goldValue = Text(goldBox, "Value", "-", 16f, gold, FontStyles.Bold, TextAlignmentOptions.Left);
            Stretch(goldValue.rectTransform);
            goldValue.rectTransform.offsetMin = new Vector2(38f, 0f);

            // 무역 상태 · 보낸 핵 · 배신자 [실측 설명 창 「정보 메뉴」 — 이름 · 골드 · 병력과 함께 보인다 · 줄 배치 미측정]
            var stateLabels = new TMP_Text[3];
            var stateValues = new TMP_Text[3];

            for (int i = 0; i < stateLabels.Length; i++)
            {
                RectTransform row = Group(panel, $"StateRow{i}");
                Height(row, 20f);
                stateLabels[i] = Text(row, "Label", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
                Stretch(stateLabels[i].rectTransform);
                stateValues[i] = Text(row, "Value", "-", 14f, White, FontStyles.Bold, TextAlignmentOptions.Right);
                Stretch(stateValues[i].rectTransform);
            }

            // 건물 수 6칸 [실측 95×36 · 2열 · 간격 8]
            RectTransform grid = Group(panel, "Counts");
            Height(grid, 36f * 3f + 8f * 2f);
            var gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            gridLayout.cellSize = new Vector2(95f, 36f);
            gridLayout.spacing = new Vector2(8f, 8f);
            gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = 2;

            int countTypes = FrontWarsHudWindow.InfoCountTypes.Length;
            var countLabels = new TMP_Text[countTypes];
            var counts = new TMP_Text[countTypes];

            for (int i = 0; i < countTypes; i++)
            {
                RectTransform cell = Panel(grid, $"Count{i}", new Color(0f, 0f, 0f, 0.4f));
                cell.GetComponent<Image>().raycastTarget = false;
                countLabels[i] = Text(cell, "Label", "-", 11f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
                PlaceTopLeft(countLabels[i].rectTransform, 8f, 8f, 50f, 20f);
                counts[i] = Text(cell, "Value", "0", 14f, White, FontStyles.Bold, TextAlignmentOptions.Right);
                PlaceTopLeft(counts[i].rectTransform, 55f, 8f, 32f, 20f);
            }

            // 외교 버튼 — 붙잡은 남의 플레이어일 때만 (원본은 정보 쪽 창 · 배치 미측정)
            const int actionSlots = 6;
            RectTransform actionsRoot = Group(panel, "Actions");
            Height(actionsRoot, 30f * 3f + 6f * 2f);
            var actionsGrid = actionsRoot.gameObject.AddComponent<GridLayoutGroup>();
            actionsGrid.cellSize = new Vector2(95f, 30f);
            actionsGrid.spacing = new Vector2(8f, 6f);
            actionsGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            actionsGrid.constraintCount = 2;

            var actionButtons = new Button[actionSlots];
            var actionLabels = new TMP_Text[actionSlots];

            for (int i = 0; i < actionSlots; i++)
                actionButtons[i] = ButtonWithLabel(actionsRoot, $"Action{i}", new Color(1f, 1f, 1f, 0.12f), "-", 12f, out actionLabels[i]);

            actionsRoot.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);

            SetRef(window, "_infoActionsRoot", actionsRoot.gameObject);
            SetRefArray(window, "_infoActionButtons", actionButtons);
            SetRefArray(window, "_infoActionLabels", actionLabels);
            SetRef(window, "_infoRoot", panel.gameObject);
            SetRef(window, "_infoName", name);
            SetRef(window, "_infoType", type);
            SetRef(window, "_infoRelation", relation);
            SetRef(window, "_infoTroopsLabel", troopsLabel);
            SetRef(window, "_infoTroops", troops);
            SetRef(window, "_infoAttackingRow", attackingRow.gameObject);
            SetRef(window, "_infoAttackingLabel", attackingLabel);
            SetRef(window, "_infoAttacking", attacking);
            SetRef(window, "_infoGold", goldValue);
            SetRef(window, "_infoTradeLabel", stateLabels[0]);
            SetRef(window, "_infoTrade", stateValues[0]);
            SetRef(window, "_infoNukesLabel", stateLabels[1]);
            SetRef(window, "_infoNukes", stateValues[1]);
            SetRef(window, "_infoTraitorLabel", stateLabels[2]);
            SetRef(window, "_infoTraitor", stateValues[2]);
            SetRefArray(window, "_infoCountLabels", countLabels);
            SetRefArray(window, "_infoCounts", counts);
        }

        /// <summary>
        /// 「게임 속도」 판 — 다시 보기 버튼으로 연다 [실측 probe/followup_replay.json · 판 1649,16,127×124 검정 41% 둥근 6 ·
        /// 제목 1657,24,111×24 16px 600 흰색 70% · 버튼 ×0.5 1657,60 · ×1 1715,60 · ×2 1657,99 · Max 1715,99 (53×33 · 둥근 4 · 14px 600)].
        /// </summary>
        private static void BuildSpeedPanel(Transform parent, Component window)
        {
            RectTransform panel = Panel(parent, "SpeedPanel", new Color(0f, 0f, 0f, 0.41f));
            panel.GetComponent<Image>().raycastTarget = false;
            PlaceScreen(panel, 1649f, 16f, 127f, 124f);
            Round(panel, 6f);

            TMP_Text title = Text(panel, "Title", "게임 속도", 16f, new Color(1f, 1f, 1f, 0.7f), FontStyles.Bold, TextAlignmentOptions.Center);
            PlaceTopLeft(title.rectTransform, 8f, 8f, 111f, 24f);

            string[] labels = { "×0.5", "×1", "×2", "Max" };
            (float X, float Y)[] spots = { (1657f, 60f), (1715f, 60f), (1657f, 99f), (1715f, 99f) };
            var buttons = new Button[labels.Length];

            for (int i = 0; i < labels.Length; i++)
            {
                buttons[i] = ButtonWithLabel(panel, $"Speed{i}", new Color32(31, 41, 55, 128), labels[i], 14f, out TMP_Text label);
                label.fontStyle = FontStyles.Bold;
                PlaceTopLeft((RectTransform)buttons[i].transform, spots[i].X - 1649f, spots[i].Y - 16f, 53f, 33f);
                Round(buttons[i], 4f);
            }

            panel.gameObject.SetActive(false);
            SetRef(window, "_speedPanel", panel.gameObject);
            SetRef(window, "_speedTitle", title);
            SetRefArray(window, "_speedButtons", buttons);
        }

        /// <summary>
        /// 화면 맨 위 10px 막대 — 바탕 rgb(27,35,47) 위에 조각(스폰 진행 · 팀 비율)을 얹는다 `[소스] QPQdQ6Sy.js function ea(){ … height:10px`.
        /// </summary>
        private static void BuildTopBar(Transform parent, Component window)
        {
            const int segmentCount = 8;
            RectTransform bar = Panel(parent, "TopBar", new Color32(27, 35, 47, 255));
            bar.GetComponent<Image>().raycastTarget = false;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 10f);
            bar.anchoredPosition = Vector2.zero;

            var segments = new Image[segmentCount];

            for (int i = 0; i < segmentCount; i++)
            {
                RectTransform seg = Panel(bar, $"Segment{i}", White);
                seg.offsetMin = Vector2.zero;
                seg.offsetMax = Vector2.zero;
                segments[i] = seg.GetComponent<Image>();
                segments[i].raycastTarget = false;
                seg.gameObject.SetActive(false);
            }

            SetRefArray(window, "_topBarSegments", segments);
        }

        private static void BuildLeaderboard(Transform parent, Component window)
        {
            // 순위표 판 자체는 바탕 없음 — 머리줄만 검정 60% [실측 thead oklab(0 0 0 / 0.6)]
            RectTransform board = Panel(parent, "Leaderboard", new Color(0f, 0f, 0f, 0f));
            PlaceScreen(board, 0f, LeaderboardY, LeaderboardW, HeaderH + RowH * 5 + ExpandH);

            var layout = board.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            var fitter = board.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform header = Panel(board, "Header", new Color(0f, 0f, 0f, 0.6f));
            header.GetComponent<Image>().raycastTarget = false;
            Height(header, HeaderH);
            string[] headerTexts = { "#", "플레이어", "소유", "골드", "병력" };
            var headers = new TMP_Text[5];
            float x = 0f;

            for (int c = 0; c < 5; c++)
            {
                headers[c] = Text(header, $"Header{c}", headerTexts[c], 12f, White70, FontStyles.Bold);
                ColumnAt(headers[c].rectTransform, x, ColumnWidths[c]);
                x += ColumnWidths[c];
            }

            // 줄은 스크롤 창 안에 둔다 — 편 상태에서 모든 플레이어를 굴려 본다 `[소스] min(400px, calc(100vh - 360px))`
            RectTransform viewport = Group(board, "RowsViewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportLayout = viewport.gameObject.AddComponent<LayoutElement>();
            viewportLayout.preferredHeight = RowH * 5;
            RectTransform rowsContent = Group(viewport, "Rows");
            rowsContent.anchorMin = new Vector2(0f, 1f);
            rowsContent.anchorMax = new Vector2(1f, 1f);
            rowsContent.pivot = new Vector2(0.5f, 1f);
            rowsContent.anchoredPosition = Vector2.zero;
            rowsContent.sizeDelta = new Vector2(0f, RowH * 5);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = rowsContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = RowH;

            var rowButtons = new Button[LeaderboardRows];
            var rank = new TMP_Text[LeaderboardRows];
            var name = new TMP_Text[LeaderboardRows];
            var owned = new TMP_Text[LeaderboardRows];
            var gold = new TMP_Text[LeaderboardRows];
            var troops = new TMP_Text[LeaderboardRows];

            for (int r = 0; r < LeaderboardRows; r++)
            {
                Button row = PlainButton(rowsContent, $"Row{r}", new Color(0f, 0f, 0f, 0f));
                RectTransform rowRect = (RectTransform)row.transform;
                rowRect.anchorMin = new Vector2(0f, 1f);
                rowRect.anchorMax = new Vector2(1f, 1f);
                rowRect.pivot = new Vector2(0.5f, 1f);
                rowRect.sizeDelta = new Vector2(0f, RowH);
                TMP_Text[] cells = new TMP_Text[5];
                x = 0f;

                for (int c = 0; c < 5; c++)
                {
                    cells[c] = Text(row.transform, $"Cell{c}", "-", 14f, White, FontStyles.Normal);
                    ColumnAt(cells[c].rectTransform, x, ColumnWidths[c]);
                    x += ColumnWidths[c];
                }

                cells[1].overflowMode = TextOverflowModes.Ellipsis;
                rowButtons[r] = row;
                rank[r] = cells[0];
                name[r] = cells[1];
                owned[r] = cells[2];
                gold[r] = cells[3];
                troops[r] = cells[4];
            }

            Button expand = ButtonWithLabel(board, "ExpandButton", new Color(0f, 0f, 0f, 0f), "+", 20f, out TMP_Text expandLabel);
            expandLabel.fontStyle = FontStyles.Bold;
            Height((RectTransform)expand.transform, ExpandH);

            // 튜토리얼 단계별 드러내기 — 투명도 · 누름을 한꺼번에 막는다
            board.gameObject.AddComponent<CanvasGroup>();
            SetRef(window, "_leaderboardRoot", board.gameObject);
            SetRefArray(window, "_headerTexts", headers);
            SetRefArray(window, "_rowButtons", rowButtons);
            SetRefArray(window, "_rowRank", rank);
            SetRefArray(window, "_rowName", name);
            SetRefArray(window, "_rowOwned", owned);
            SetRefArray(window, "_rowGold", gold);
            SetRefArray(window, "_rowTroops", troops);
            SetRef(window, "_rowsViewport", viewportLayout);
            SetRef(window, "_rowsContent", rowsContent);
            SetRef(window, "_leaderboardScroll", scroll);
            SetRef(window, "_expandButton", expand);
            SetRef(window, "_expandLabel", expandLabel);
        }

        private static void BuildControlPanel(Transform parent, Component window)
        {
            RectTransform panel = Group(parent, "ControlPanel");
            PlaceScreen(panel, ControlX, ControlTop, ControlW, ControlBottom - ControlTop);

            // 미측정 — 판 바탕은 글자 둘레로 12px 넓힌다
            RectTransform bg = Panel(panel, "Background", PanelBg);
            bg.anchorMin = Vector2.zero;
            bg.anchorMax = Vector2.one;
            bg.offsetMin = new Vector2(-12f, -12f);
            bg.offsetMax = new Vector2(12f, 12f);

            TMP_Text troopsLabel = ControlLine(panel, "Troops", 0f, out TMP_Text troopsValue, out TMP_Text troopsIncrease);
            TMP_Text goldLabel = ControlLine(panel, "Gold", 864f - ControlTop, out TMP_Text goldValue, out _);
            TMP_Text attackLabel = ControlLine(panel, "Attack", 908f - ControlTop, out TMP_Text attackValue, out _);
            Slider attack = MakeSlider(panel, "AttackSlider");
            PlaceTopLeft((RectTransform)attack.transform, 0f, 940f - ControlTop, ControlW, SliderH);

            TMP_Text sendTroopsLabel = ControlLine(panel, "SendTroops", 960f - ControlTop, out TMP_Text sendTroopsValue, out _);
            Slider sendTroops = MakeSlider(panel, "SendTroopsSlider");
            PlaceTopLeft((RectTransform)sendTroops.transform, 0f, 992f - ControlTop, ControlW, SliderH);

            TMP_Text sendGoldLabel = ControlLine(panel, "SendGold", 1012f - ControlTop, out TMP_Text sendGoldValue, out _);
            Slider sendGold = MakeSlider(panel, "SendGoldSlider");
            PlaceTopLeft((RectTransform)sendGold.transform, 0f, 1044f - ControlTop, ControlW, SliderH);

            SetRef(window, "_controlRoot", panel.gameObject);
            SetRef(window, "_troopsLabel", troopsLabel);
            SetRef(window, "_troopsValue", troopsValue);
            SetRef(window, "_troopsIncrease", troopsIncrease);
            SetRef(window, "_goldLabel", goldLabel);
            SetRef(window, "_goldValue", goldValue);
            SetRef(window, "_attackLabel", attackLabel);
            SetRef(window, "_attackValue", attackValue);
            SetRef(window, "_attackSlider", attack);
            SetRef(window, "_sendTroopsLabel", sendTroopsLabel);
            SetRef(window, "_sendTroopsValue", sendTroopsValue);
            SetRef(window, "_sendTroopsSlider", sendTroops);
            SetRef(window, "_sendGoldLabel", sendGoldLabel);
            SetRef(window, "_sendGoldValue", sendGoldValue);
            SetRef(window, "_sendGoldSlider", sendGold);
        }

        /// <summary>한 줄 — 「이름 : 값 (증가)」. 이름 16px 400 흰색 70% · 값 16px 600 흰색 [실측].</summary>
        private static TMP_Text ControlLine(RectTransform panel, string name, float top, out TMP_Text value, out TMP_Text extra)
        {
            RectTransform line = Group(panel, name + "Line");
            PlaceTopLeft(line, 0f, top, ControlW, 24f);

            var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            TMP_Text label = Text(line, "Label", name, ControlFont, White70, FontStyles.Normal, TextAlignmentOptions.Left);
            value = Text(line, "Value", "0", ControlFont, White, FontStyles.Bold, TextAlignmentOptions.Left);
            extra = Text(line, "Extra", string.Empty, ControlFont, White, FontStyles.Bold, TextAlignmentOptions.Left);
            return label;
        }

        private static void BuildBuildBar(Transform parent, Component window)
        {
            RectTransform bar = Group(parent, "BuildBar");
            Stretch(bar);

            int count = FrontWarsHudWindow.BuildSlots.Length;
            var buttons = new Button[count];
            var hotkeys = new TMP_Text[count];
            var counts = new TMP_Text[count];
            var icons = new Image[count];

            for (int i = 0; i < count; i++)
            {
                bool firstRow = i < 6;
                float left = firstRow ? BuildRow1X + i * BuildSlotStep : BuildRow2X + (i - 6) * BuildSlotStep;
                float top = firstRow ? BuildRow1Y : BuildRow2Y;

                Button slot = PlainButton(bar, $"Slot{i}", new Color(0f, 0f, 0f, 0.35f));
                // ⚠ 건설 막대는 «가운데 아래» 다 — 모서리에 매달면 화면 폭이 1920 이 아닐 때 오른쪽 칸이 왼쪽 칸 위로 겹친다 (재생 검사 Slot0 → Slot3)
                PlaceBottomCenter((RectTransform)slot.transform, left, top, BuildSlotW, BuildSlotH);

                hotkeys[i] = Text(slot.transform, "Hotkey", "1", 12f, White, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                PlaceTopLeft(hotkeys[i].rectTransform, 4f, 2f, 20f, 14f);

                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(slot.transform, false);
                icons[i] = iconGo.GetComponent<Image>();
                icons[i].raycastTarget = false;
                icons[i].preserveAspect = true;
                PlaceTopLeft((RectTransform)iconGo.transform, 18f, 8f, 22f, 22f);

                counts[i] = Text(slot.transform, "Count", "0", 16f, White, FontStyles.Bold, TextAlignmentOptions.Right);
                PlaceTopLeft(counts[i].rectTransform, 42f, 8f, 32f, 22f);

                buttons[i] = slot;
            }

            // 튜토리얼 단계별 드러내기 — 투명도 · 누름을 한꺼번에 막는다
            bar.gameObject.AddComponent<CanvasGroup>();
            SetRef(window, "_buildRoot", bar.gameObject);
            SetRefArray(window, "_buildButtons", buttons);
            SetRefArray(window, "_buildHotkeys", hotkeys);
            SetRefArray(window, "_buildCounts", counts);
            SetRefArray(window, "_buildIcons", icons);
        }

        private static void BuildEvents(Transform parent, Component window)
        {
            RectTransform events = Group(parent, "Events");
            PlaceScreen(events, EventsX, EventsTop, EventsW, EventsBottom - EventsTop);

            RectTransform bg = Panel(events, "Background", PanelBg);
            Stretch(bg);

            int filterCount = FrontWarsHudWindow.FilterKeys.Length;
            var filters = new Button[filterCount];
            var filterLabels = new TMP_Text[filterCount];

            for (int i = 0; i < filterCount; i++)
            {
                filters[i] = ButtonWithLabel(events, $"Filter{i}", ButtonBg, "-", 10f, out filterLabels[i]);
                PlaceTopLeft((RectTransform)filters[i].transform, 8f + i * FilterStep, 8f, FilterSize, FilterSize);
            }

            Button hide = ButtonWithLabel(events, "HideButton", ButtonBg, "숨기기", 14f, out TMP_Text hideLabel);
            hideLabel.color = White70;
            PlaceTopLeft((RectTransform)hide.transform, EventsW - HideW - 8f, 10f, HideW, HideH);

            TMP_Text empty = Text(events, "EmptyText", "이벤트 없음", 16f, Gray, FontStyles.Normal);
            PlaceTopLeft(empty.rectTransform, 0f, 52f, EventsW, 36f);

            // 이벤트 줄은 판 위로 쌓인다 — 새 것이 아래 (미측정)
            RectTransform list = Group(events, "EventList");
            list.anchorMin = new Vector2(0f, 1f);
            list.anchorMax = new Vector2(1f, 1f);
            list.pivot = new Vector2(0.5f, 0f);
            list.anchoredPosition = Vector2.zero;
            list.sizeDelta = new Vector2(0f, 0f);

            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.LowerCenter;

            var fitter = list.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var eventButtons = new Button[EventRows];
            var eventTexts = new TMP_Text[EventRows];
            var acceptButtons = new Button[EventRows];
            var acceptLabels = new TMP_Text[EventRows];
            var rejectButtons = new Button[EventRows];
            var rejectLabels = new TMP_Text[EventRows];

            for (int i = 0; i < EventRows; i++)
            {
                Button row = PlainButton(list, $"Event{i}", PanelBg);
                Height((RectTransform)row.transform, EventRowH);

                eventTexts[i] = Text(row.transform, "Text", "-", 14f, White, FontStyles.Normal, TextAlignmentOptions.Left);
                Stretch(eventTexts[i].rectTransform);
                eventTexts[i].rectTransform.offsetMin = new Vector2(8f, 0f);
                eventTexts[i].rectTransform.offsetMax = new Vector2(-8f, 0f);
                eventTexts[i].overflowMode = TextOverflowModes.Ellipsis;
                eventButtons[i] = row;

                // 동맹 요청 줄에만 붙는 수락 · 거절 (자리 미측정 — 줄 오른쪽 끝에 작게)
                acceptButtons[i] = ButtonWithLabel(row.transform, "Accept", new Color(34f / 255f, 197f / 255f, 94f / 255f, 0.35f), "수락", 12f, out acceptLabels[i]);
                PlaceRowRight((RectTransform)acceptButtons[i].transform, 96f, 48f);
                rejectButtons[i] = ButtonWithLabel(row.transform, "Reject", new Color(239f / 255f, 68f / 255f, 68f / 255f, 0.35f), "거절", 12f, out rejectLabels[i]);
                PlaceRowRight((RectTransform)rejectButtons[i].transform, 44f, 48f);

                acceptButtons[i].gameObject.SetActive(false);
                rejectButtons[i].gameObject.SetActive(false);
            }

            // 튜토리얼 단계별 드러내기 — 투명도 · 누름을 한꺼번에 막는다
            events.gameObject.AddComponent<CanvasGroup>();
            SetRef(window, "_eventsRoot", events.gameObject);
            SetRefArray(window, "_filterButtons", filters);
            SetRefArray(window, "_filterLabels", filterLabels);
            SetRef(window, "_hideButton", hide);
            SetRef(window, "_hideLabel", hideLabel);
            SetRef(window, "_emptyText", empty);
            SetRefArray(window, "_eventButtons", eventButtons);
            SetRefArray(window, "_eventTexts", eventTexts);
            SetRefArray(window, "_eventAcceptButtons", acceptButtons);
            SetRefArray(window, "_eventAcceptLabels", acceptLabels);
            SetRefArray(window, "_eventRejectButtons", rejectButtons);
            SetRefArray(window, "_eventRejectLabels", rejectLabels);
        }

        // ── 판 끝 — 원본 배치 [실측 probe/gameover.json] · Steam · Discord 홍보 카드는 옮기지 않는다 (의도된 차이 2)

        private static void BuildGameOver()
        {
            GameObject root = WindowRoot(nameof(FrontWarsGameOverWindow));
            var window = root.AddComponent<FrontWarsGameOverWindow>();

            // 창 판 [실측 624,273,672×535 · 검정 41% · 테두리 흰색 10%] — 뒤 HUD 는 그대로 보인다
            RectTransform panel = Panel(root.transform, "Panel", new Color(0f, 0f, 0f, 0.41f));
            PlaceMeasured(panel, 624f, 273f, 672f, 535f);
            Round(panel, 16f);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.1f);
            border.effectDistance = new Vector2(1f, -1f);

            // 아이콘 칸 [실측 920,306,80×80 · 흰색 10% · 아이콘 40 흰색 80%] — 원본 그림 대신 별 도형
            RectTransform iconBox = Panel(root.transform, "IconBox", new Color(1f, 1f, 1f, 0.1f));
            iconBox.GetComponent<Image>().raycastTarget = false;
            PlaceMeasured(iconBox, 920f, 306f, 80f, 80f);
            // ⚠ 코드로 그린 스프라이트는 에셋이 아니라 프리팹에 저장되지 않는다 — 그림은 창이 열릴 때 넣는다
            Image icon = IconImage(iconBox, 40f, new Color(1f, 1f, 1f, 0.8f));

            // 제목 [실측 815,402 · 26px 700 · 가운데 정렬] — 「○○가 승리했습니다!」
            TMP_Text title = Text(root.transform, "Title", "-", 26f, White, FontStyles.Bold);
            PlaceMeasured(title.rectTransform, 624f, 402f, 672f, 33f);

            // 승리 · 패배 한 줄 — 원본은 이 자리에 홍보 카드가 있다 (우리 추가 · 자리 미측정)
            TMP_Text message = Text(root.transform, "Message", "-", 18f, Orange, FontStyles.Bold);
            PlaceMeasured(message.rectTransform, 624f, 452f, 672f, 26f);

            // 게임 종료 [실측 736,727,219×48 · 흰색 5% · 16px 600 흰색 85%] · 관전 [967,727,217×48 · 주황 · 16px 700]
            Button exit = ButtonWithLabel(root.transform, "ExitButton", new Color(1f, 1f, 1f, 0.05f), "게임 종료", 16f, out TMP_Text exitLabel);
            exitLabel.fontStyle = FontStyles.Bold;
            exitLabel.color = new Color(1f, 1f, 1f, 0.85f);
            PlaceMeasured((RectTransform)exit.transform, 736f, 727f, 219f, 48f);

            Button watch = ButtonWithLabel(root.transform, "WatchButton", Orange, "관전", 16f, out TMP_Text watchLabel);
            watchLabel.fontStyle = FontStyles.Bold;
            PlaceMeasured((RectTransform)watch.transform, 967f, 727f, 217f, 48f);

            SetRef(window, "_title", title);
            SetRef(window, "_message", message);
            SetRef(window, "_exitButton", exit);
            SetRef(window, "_exitLabel", exitLabel);
            SetRef(window, "_watchButton", watch);
            SetRef(window, "_watchLabel", watchLabel);
            SetRef(window, "_icon", icon);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsGameOverWindow)}.prefab");
        }

        // ── 오른쪽 클릭 메뉴 (미측정 배치)

        private static void BuildRadial()
        {
            GameObject root = WindowRoot(nameof(FrontWarsRadialMenuWindow));
            var window = root.AddComponent<FrontWarsRadialMenuWindow>();

            // 바깥을 누르면 닫힌다 — 보이지 않는 전체 버튼
            Button dismiss = PlainButton(root.transform, "Dismiss", new Color(0f, 0f, 0f, 0.001f));
            Stretch((RectTransform)dismiss.transform);

            RectTransform center = Group(root.transform, "Center");
            center.anchorMin = new Vector2(0.5f, 0.5f);
            center.anchorMax = new Vector2(0.5f, 0.5f);
            center.pivot = new Vector2(0.5f, 0.5f);
            center.sizeDelta = Vector2.zero;

            // 가장 큰 겹(둘째 겹 바깥 175) 기준으로 판을 잡는다 — 조각 크기는 창이 겹마다 바꾼다
            float diameter = FrontWarsRadialMenuWindow.OuterRadiusSub * 2f;
            int sliceCount = FrontWarsRadialMenuWindow.MaxSlices;
            var slices = new Image[sliceCount];
            var cooldowns = new Image[sliceCount];
            var icons = new Image[sliceCount];
            var glyphs = new TMP_Text[sliceCount];
            var counters = new TMP_Text[sliceCount];

            for (int i = 0; i < sliceCount; i++)
            {
                RectTransform slice = Panel(center, $"Slice{i}", Color.white);
                slice.GetComponent<Image>().raycastTarget = false;
                CenterSquare(slice, diameter);
                slices[i] = slice.GetComponent<Image>();

                // 재사용 대기 덮개 [소스 fill rgba(0,0,0,0.55)]
                RectTransform shade = Panel(center, $"Cooldown{i}", new Color(0f, 0f, 0f, 0.55f));
                shade.GetComponent<Image>().raycastTarget = false;
                CenterSquare(shade, diameter);
                cooldowns[i] = shade.GetComponent<Image>();
                shade.gameObject.SetActive(false);
            }

            // 가운데 원 [소스 r 55 · rgba(31,41,55,0.95) · 아이콘 64 — 방패 / 칼 / 뒤로]
            RectTransform disc = Panel(center, "CenterDisc", new Color32(31, 41, 55, 242));
            disc.GetComponent<Image>().raycastTarget = false;
            CenterSquare(disc, FrontWarsRadialMenuWindow.CenterRadius * 2f);
            Round(disc, FrontWarsRadialMenuWindow.CenterRadius);
            Image centerIcon = IconImage(disc, FrontWarsRadialMenuWindow.CenterIconSize, White);

            for (int i = 0; i < sliceCount; i++)
            {
                RectTransform iconRect = Panel(center, $"SliceIcon{i}", White);
                iconRect.GetComponent<Image>().raycastTarget = false;
                CenterSquare(iconRect, FrontWarsRadialMenuWindow.IconSize);
                icons[i] = iconRect.GetComponent<Image>();
                icons[i].preserveAspect = true;

                glyphs[i] = Text(center, $"SliceGlyph{i}", "-", 20f, White, FontStyles.Bold);
                CenterSquare(glyphs[i].rectTransform, FrontWarsRadialMenuWindow.IconSize);

                // 배 남은 수 [소스 16px 700 · 0.6 배]
                counters[i] = Text(center, $"SliceCounter{i}", "-", 16f * FrontWarsRadialMenuWindow.SvgScale, White, FontStyles.Bold);
                CenterSquare(counters[i].rectTransform, 40f);
            }

            // 툴팁 [소스 bg rgba(17,24,39,0.95) · 테두리 1px rgba(55,65,81,0.5) · 둥근 8 · 안쪽 8/12 · 최대 폭 250]
            RectTransform tooltip = Panel(center, "Tooltip", new Color32(17, 24, 39, 242));
            tooltip.GetComponent<Image>().raycastTarget = false;
            tooltip.anchorMin = new Vector2(0.5f, 0.5f);
            tooltip.anchorMax = new Vector2(0.5f, 0.5f);
            tooltip.pivot = new Vector2(0f, 1f);
            Round(tooltip, 8f);
            var tipOutline = tooltip.gameObject.AddComponent<Outline>();
            tipOutline.effectColor = new Color32(55, 65, 81, 128);
            tipOutline.effectDistance = new Vector2(1f, -1f);
            var tipLayout = tooltip.gameObject.AddComponent<VerticalLayoutGroup>();
            tipLayout.padding = new RectOffset(12, 12, 8, 8);
            tipLayout.spacing = 4f;
            tipLayout.childControlWidth = true;
            tipLayout.childControlHeight = true;
            tipLayout.childForceExpandWidth = false;
            tipLayout.childForceExpandHeight = false;
            var tipFit = tooltip.gameObject.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text tipTitle = Text(tooltip, "Title", "-", 14f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            TMP_Text tipBody = Text(tooltip, "Body", "-", 13f, new Color32(209, 213, 219, 255), FontStyles.Normal, TextAlignmentOptions.Left);
            tipBody.enableWordWrapping = true;
            var bodyLimit = tipBody.gameObject.AddComponent<LayoutElement>();
            bodyLimit.preferredWidth = 226f;
            tooltip.gameObject.SetActive(false);

            // 누름 판 — 맨 위에 둔다 (조각 고르기는 창이 각도로 한다)
            RectTransform hit = Panel(center, "HitArea", new Color(0f, 0f, 0f, 0.001f));
            CenterSquare(hit, diameter);
            var hitArea = hit.gameObject.AddComponent<FrontWarsRadialHitArea>();
            tooltip.SetAsLastSibling();

            SetRef(window, "_center", center);
            SetRef(window, "_centerIcon", centerIcon);
            SetRefArray(window, "_slices", slices);
            SetRefArray(window, "_cooldowns", cooldowns);
            SetRefArray(window, "_sliceIcons", icons);
            SetRefArray(window, "_sliceGlyphs", glyphs);
            SetRefArray(window, "_sliceCounters", counters);
            SetRef(window, "_tooltip", tooltip);
            SetRef(window, "_tooltipTitle", tipTitle);
            SetRef(window, "_tooltipBody", tipBody);
            SetRef(window, "_hitArea", hitArea);
            SetRef(window, "_dismiss", dismiss);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsRadialMenuWindow)}.prefab");
        }

        // ── 튜토리얼 HUD (미측정 배치 — 칩은 화면 위 가운데)

        // ── 튜토리얼 — 원본 tut2 화면 규격 `[소스] QPQdQ6Sy.js: fr · mr · nr · qn · sr · Sr · br · Cr · Tr`

        /// <summary>칩 · 안내 카드 바탕 — panel-bg 위에 검정 55% 를 겹친 색.</summary>
        private static readonly Color TutorialPanel = new Color32(17, 22, 30, 222);

        /// <summary>승리 · 사망 카드 바탕 — panel-bg 위에 검정 45%.</summary>
        private static readonly Color TutorialCard = new Color32(20, 26, 35, 235);

        private static void BuildTutorial()
        {
            GameObject root = WindowRoot(nameof(FrontWarsTutorialWindow));
            var window = root.AddComponent<FrontWarsTutorialWindow>();
            Color accent = FrontWarsTutorialWindow.AccentColor;

            // 경고 테두리 — 맨 아래 [소스 Sr z 9996 · inset 0 0 120px 20px rgba(255,40,30,.45)]
            RectTransform alert = Panel(root.transform, "Alert", new Color(1f, 40f / 255f, 30f / 255f, 0.45f));
            alert.GetComponent<Image>().raycastTarget = false;
            Stretch(alert);

            // ── 스폰 표시 [소스 qn — 화면 (50%, 45%) · 고리 120 테두리 4 · 점 18 · 이름표 +74]
            RectTransform spawn = Group(root.transform, "SpawnMarker");
            AnchorAt(spawn, 0.5f, 1f - FrontWarsTutorialWindow.SpawnTopRatio);
            var spawnRings = new Image[3];
            for (int i = 0; i < spawnRings.Length; i++)
                spawnRings[i] = CenterImage(spawn, $"Ring{i}", 120f, accent);
            Image spawnDot = CenterImage(spawn, "Dot", 18f, White);
            Image spawnDotFill = CenterImage(spawnDot.transform, "Fill", 18f - 5f, accent);
            RectTransform spawnLabelRoot = LabelPill(spawn, "Label", 74f, accent, 2f, new RectOffset(16, 16, 5, 5), 14f, out TMP_Text spawnLabel);

            // ── 조준경 [소스 sr — 고리 96 테두리 4 · 점선 3 · 눈금 3×14 · 점 16 · 이름표 +60]
            RectTransform reticle = Group(root.transform, "Reticle");
            AnchorAt(reticle, 0.5f, 0.5f);
            var reticleRings = new Image[2];
            for (int i = 0; i < reticleRings.Length; i++)
                reticleRings[i] = CenterImage(reticle, $"Ring{i}", 96f, FrontWarsTutorialWindow.EnemyColor);
            RectTransform spinner = Group(reticle, "Spinner");
            CenterSquare(spinner, 96f);
            Image dashed = CenterImage(spinner, "Dashed", 96f, FrontWarsTutorialWindow.EnemyColor);
            // 눈금 네 개 — 원본 좌표(96 상자 왼쪽 위 기준 left/top)를 가운데 기준으로 옮긴다
            var ticks = new Image[4];
            (float Left, float Top, float W, float H)[] tickBoxes =
            {
                (-1.5f, -48f, 3f, 14f), (-1.5f, 48f - 14f, 3f, 14f), (-48f, -1.5f, 14f, 3f), (48f - 14f, -1.5f, 14f, 3f)
            };
            for (int i = 0; i < ticks.Length; i++)
            {
                var box = tickBoxes[i];
                RectTransform tick = Panel(spinner, $"Tick{i}", FrontWarsTutorialWindow.EnemyColor);
                tick.GetComponent<Image>().raycastTarget = false;
                CenterSquare(tick, 0f);
                tick.sizeDelta = new Vector2(box.W, box.H);
                tick.anchoredPosition = new Vector2(box.Left + box.W * 0.5f, -(box.Top + box.H * 0.5f));
                Round(tick, 1f);
                ticks[i] = tick.GetComponent<Image>();
            }
            Image reticleDot = CenterImage(reticle, "Dot", 16f, White);
            Image reticleDotFill = CenterImage(reticleDot.transform, "Fill", 12f, FrontWarsTutorialWindow.EnemyDot);
            RectTransform reticleLabelRoot = LabelPill(reticle, "Label", 60f, FrontWarsTutorialWindow.EnemyColor, 2f, new RectOffset(14, 14, 4, 4), 13f, out TMP_Text reticleLabel);

            // ── 목표 칩 [소스 fr — 위 10 · 점 줄(지금 10 · 나머지 6 · 간격 6) 아래 알약(가로 16 · 세로 6 · 간격 8)]
            RectTransform chip = Group(root.transform, "ObjectiveChip");
            chip.anchorMin = new Vector2(0.5f, 1f);
            chip.anchorMax = new Vector2(0.5f, 1f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.anchoredPosition = new Vector2(0f, -FrontWarsTutorialWindow.ChipTop);
            var chipColumn = chip.gameObject.AddComponent<VerticalLayoutGroup>();
            chipColumn.spacing = 6f;
            chipColumn.childAlignment = TextAnchor.UpperCenter;
            chipColumn.childControlWidth = true;
            chipColumn.childControlHeight = true;
            chipColumn.childForceExpandWidth = false;
            chipColumn.childForceExpandHeight = false;
            FitBoth(chip);

            RectTransform dotsRow = Group(chip, "Dots");
            var dotsLayout = dotsRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            dotsLayout.spacing = 6f;
            dotsLayout.childAlignment = TextAnchor.MiddleCenter;
            dotsLayout.childControlWidth = true;
            dotsLayout.childControlHeight = true;
            dotsLayout.childForceExpandWidth = false;
            dotsLayout.childForceExpandHeight = false;
            var dots = new Image[FrontWarsTutorialObjectives.MaxObjectives];
            for (int i = 0; i < dots.Length; i++)
            {
                RectTransform dot = Panel(dotsRow, $"Dot{i}", White);
                dot.GetComponent<Image>().raycastTarget = false;
                var element = dot.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 6f;
                element.preferredHeight = 6f;
                dots[i] = dot.GetComponent<Image>();
            }

            RectTransform pill = Panel(chip, "Pill", TutorialPanel);
            pill.GetComponent<Image>().raycastTarget = false;
            Round(pill, 16f);
            var pillBorder = pill.gameObject.AddComponent<Outline>();
            pillBorder.effectColor = new Color(accent.r, accent.g, accent.b, 0.4f);
            pillBorder.effectDistance = new Vector2(1f, -1f);
            var pillLayout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            pillLayout.padding = new RectOffset(16, 16, 6, 6);
            pillLayout.spacing = 8f;
            pillLayout.childAlignment = TextAnchor.MiddleCenter;
            pillLayout.childControlWidth = true;
            pillLayout.childControlHeight = true;
            pillLayout.childForceExpandWidth = false;
            pillLayout.childForceExpandHeight = false;
            TMP_Text chipIcon = Text(pill, "Icon", "🚩", 16f, White, FontStyles.Normal);
            TMP_Text chipText = Text(pill, "Text", "-", 14f, accent, FontStyles.Bold);
            TMP_Text chipProgress = Text(pill, "Progress", "-", 14f, White80, FontStyles.Bold);

            // ── 안내 카드 [소스 mr — 위 88 · 최대 폭 340 · 안쪽 16/10 · 12px 흰색 85% · 테두리 흰색 15% · 둥근 12]
            RectTransform tip = Panel(root.transform, "TipCard", TutorialPanel);
            tip.GetComponent<Image>().raycastTarget = false;
            tip.anchorMin = new Vector2(0.5f, 1f);
            tip.anchorMax = new Vector2(0.5f, 1f);
            tip.pivot = new Vector2(0.5f, 1f);
            tip.anchoredPosition = new Vector2(0f, -FrontWarsTutorialWindow.TipTop);
            Round(tip, 12f);
            var tipGroup = tip.gameObject.AddComponent<CanvasGroup>();
            tipGroup.blocksRaycasts = false;
            AddBorder(tip, new Color(1f, 1f, 1f, 0.15f));
            ColumnLayout(tip, new RectOffset(16, 16, 10, 10), 0f);
            FitBoth(tip);
            TMP_Text tipText = WrappedText(tip, "Text", 12f, new Color(1f, 1f, 1f, 0.85f), 340f - 32f);

            // ── 비추기 [소스 nr — 가리개 · 강조 테두리 · 카드 폭 300 · 안쪽 16/12 · 12px 흰색 90% · 「알겠습니다!」]
            RectTransform spot = Group(root.transform, "Spotlight");
            Stretch(spot);
            var masks = new RectTransform[4];
            for (int i = 0; i < masks.Length; i++)
                masks[i] = CenterBox(spot, $"Mask{i}", new Color(0f, 0f, 0f, 0.6f));
            var frame = new RectTransform[4];
            for (int i = 0; i < frame.Length; i++)
                frame[i] = CenterBox(spot, $"Frame{i}", accent);
            // 구멍 · 테두리의 둥근 모서리 (그림은 창이 켜질 때 넣는다)
            var corners = new Image[4];
            var arcs = new Image[4];
            for (int i = 0; i < corners.Length; i++)
            {
                corners[i] = CenterImage(spot, $"Corner{i}", FrontWarsTutorialWindow.SpotHoleRadius, new Color(0f, 0f, 0f, 0.6f));
                arcs[i] = CenterImage(spot, $"Arc{i}", FrontWarsTutorialWindow.SpotHoleRadius + FrontWarsTutorialWindow.SpotArcWidth, accent);
            }

            RectTransform spotCard = Panel(spot, "Card", new Color32(30, 41, 59, 245));
            var spotCardGroup = spotCard.gameObject.AddComponent<CanvasGroup>();
            spotCardGroup.blocksRaycasts = true;
            spotCard.GetComponent<Image>().raycastTarget = false;
            spotCard.anchorMin = new Vector2(0.5f, 0.5f);
            spotCard.anchorMax = new Vector2(0.5f, 0.5f);
            spotCard.pivot = new Vector2(0f, 1f);
            spotCard.sizeDelta = new Vector2(FrontWarsTutorialWindow.SpotCardWidth, 80f);
            Round(spotCard, 12f);
            AddBorder(spotCard, new Color(accent.r, accent.g, accent.b, 0.6f));
            ColumnLayout(spotCard, new RectOffset(16, 16, 12, 12), 10f);
            var spotFit = spotCard.gameObject.AddComponent<ContentSizeFitter>();
            spotFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text spotText = WrappedText(spotCard, "Text", 12f, new Color(1f, 1f, 1f, 0.9f), FrontWarsTutorialWindow.SpotCardWidth - 32f);

            RectTransform gotItRow = Group(spotCard, "GotItRow");
            var rowElement = gotItRow.gameObject.AddComponent<LayoutElement>();
            rowElement.preferredHeight = 28f;
            Button gotIt = ButtonWithLabel(gotItRow, "GotIt", accent, "알겠습니다!", 12f, out TMP_Text gotItLabel);
            gotItLabel.fontStyle = FontStyles.Bold;
            CenterSquare((RectTransform)gotIt.transform, 0f);
            ((RectTransform)gotIt.transform).sizeDelta = new Vector2(110f, 28f);
            Round(gotIt, 8f);
            // 시간 막대 — 버튼 위를 왼쪽에서부터 검정 25% 로 채운다 [소스 tr scaleX 0→1]
            RectTransform gotItTimer = Panel(gotIt.transform, "Timer", new Color(0f, 0f, 0f, 0.25f));
            gotItTimer.GetComponent<Image>().raycastTarget = false;
            Stretch(gotItTimer);
            gotItTimer.pivot = new Vector2(0f, 0.5f);
            gotItTimer.localScale = new Vector3(0f, 1f, 1f);
            Round(gotItTimer, 8f);
            gotItLabel.transform.SetAsLastSibling();

            // ── 색종이 [소스 br — 화면 (50%, 30%) · 8×8 둥근 2 · ✅ 30px]
            RectTransform confetti = Group(root.transform, "Confetti");
            AnchorAt(confetti, 0.5f, 0.5f);
            var pieces = new Image[FrontWarsTutorialWindow.ConfettiCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                RectTransform piece = CenterBox(confetti, $"Piece{i}", White);
                piece.pivot = new Vector2(0.5f, 0.5f);
                piece.sizeDelta = new Vector2(8f, 8f);
                Round(piece, 2f);
                pieces[i] = piece.GetComponent<Image>();
            }
            TMP_Text check = Text(confetti, "Check", "✅", 30f, White, FontStyles.Normal);
            check.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            check.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            check.rectTransform.pivot = new Vector2(0.5f, 1f);
            check.rectTransform.sizeDelta = new Vector2(40f, 40f);

            // ── 승리 카드 [소스 Cr — 위 18% · 폭 400 · 안쪽 24 · 🏆 48 · 제목 20 굵게 · 본문 14 흰색 70% · 버튼 16 굵게]
            RectTransform victory = Panel(root.transform, "Victory", TutorialCard);
            victory.anchorMin = new Vector2(0.5f, 0.82f);
            victory.anchorMax = new Vector2(0.5f, 0.82f);
            victory.pivot = new Vector2(0.5f, 1f);
            victory.sizeDelta = new Vector2(400f, 300f);
            Round(victory, 16f);
            var victoryGroup = victory.gameObject.AddComponent<CanvasGroup>();
            AddBorder(victory, new Color(accent.r, accent.g, accent.b, 0.6f));
            ColumnLayout(victory, new RectOffset(24, 24, 24, 24), 0f);
            var victoryFit = victory.gameObject.AddComponent<ContentSizeFitter>();
            victoryFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            FixedText(victory, "Trophy", "🏆", 48f, White, FontStyles.Normal, 48f + 8f);
            TMP_Text victoryTitle = FixedText(victory, "Title", "-", 20f, accent, FontStyles.Bold, 28f + 4f);
            TMP_Text victoryBody = WrappedText(victory, "Body", 14f, new Color(1f, 1f, 1f, 0.7f), 400f - 48f);
            Spacer(victory, 16f);
            Button keep = CardButton(victory, "Keep", accent, 48f, 16f, out TMP_Text keepLabel);
            Spacer(victory, 10f);
            Button realMatch = CardButton(victory, "RealMatch", new Color(0f, 0f, 0f, 0f), 28f, 14f, out TMP_Text realMatchLabel);
            realMatchLabel.color = new Color(1f, 1f, 1f, 0.6f);
            realMatchLabel.fontStyle = FontStyles.Normal;

            // ── 사망 화면 [소스 Tr — 검정 85% · 카드 폭 384 · 안쪽 32 · 💪 48 · 제목 20 · 본문 14 · 재시작 버튼]
            RectTransform death = Panel(root.transform, "Death", new Color(0f, 0f, 0f, 0.85f));
            Stretch(death);
            RectTransform deathCard = Panel(death, "Card", TutorialCard);
            deathCard.GetComponent<Image>().raycastTarget = false;
            CenterSquare(deathCard, 0f);
            deathCard.sizeDelta = new Vector2(384f, 280f);
            Round(deathCard, 16f);
            AddBorder(deathCard, new Color(accent.r, accent.g, accent.b, 0.5f));
            ColumnLayout(deathCard, new RectOffset(32, 32, 32, 32), 0f);
            var deathFit = deathCard.gameObject.AddComponent<ContentSizeFitter>();
            deathFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            FixedText(deathCard, "Muscle", "💪", 48f, White, FontStyles.Normal, 48f + 12f);
            TMP_Text deathTitle = FixedText(deathCard, "Title", "-", 20f, accent, FontStyles.Bold, 28f + 8f);
            TMP_Text deathBody = WrappedText(deathCard, "Body", 14f, new Color(1f, 1f, 1f, 0.7f), 384f - 64f);
            Spacer(deathCard, 20f);
            Button restart = CardButton(deathCard, "Restart", accent, 48f, 16f, out TMP_Text restartLabel);

            SetRef(window, "_chipGroup", chip);
            SetRefArray(window, "_dots", dots);
            SetRef(window, "_chipBorder", pillBorder);
            SetRef(window, "_chipIcon", chipIcon);
            SetRef(window, "_chipText", chipText);
            SetRef(window, "_chipProgress", chipProgress);
            SetRef(window, "_tipCard", tip);
            SetRef(window, "_tipText", tipText);
            SetRef(window, "_spotRoot", spot);
            SetRefArray(window, "_spotMasks", masks);
            SetRefArray(window, "_spotFrame", frame);
            SetRefArray(window, "_spotCorners", corners);
            SetRefArray(window, "_spotArcs", arcs);
            SetRef(window, "_spotCardGroup", spotCardGroup);
            SetRef(window, "_tipGroup", tipGroup);
            SetRef(window, "_victoryGroup", victoryGroup);
            SetRef(window, "_spotCard", spotCard);
            SetRef(window, "_spotText", spotText);
            SetRef(window, "_gotIt", gotIt);
            SetRef(window, "_gotItLabel", gotItLabel);
            SetRef(window, "_gotItTimer", gotItTimer);
            SetRef(window, "_spawnRoot", spawn);
            SetRefArray(window, "_spawnRings", spawnRings);
            SetRef(window, "_spawnDot", spawnDot);
            SetRef(window, "_spawnDotFill", spawnDotFill);
            SetRef(window, "_spawnLabelRoot", spawnLabelRoot);
            SetRef(window, "_spawnLabel", spawnLabel);
            SetRef(window, "_reticleRoot", reticle);
            SetRefArray(window, "_reticleRings", reticleRings);
            SetRef(window, "_reticleSpinner", spinner);
            SetRef(window, "_reticleDashed", dashed);
            SetRefArray(window, "_reticleTicks", ticks);
            SetRef(window, "_reticleDot", reticleDot);
            SetRef(window, "_reticleDotFill", reticleDotFill);
            SetRef(window, "_reticleLabelBg", reticleLabelRoot.GetComponent<Image>());
            SetRef(window, "_reticleLabelBorder", reticleLabelRoot.GetComponent<Outline>());
            SetRef(window, "_reticleLabel", reticleLabel);
            SetRef(window, "_alert", alert.GetComponent<Image>());
            SetRef(window, "_confettiRoot", confetti);
            SetRefArray(window, "_confetti", pieces);
            SetRef(window, "_check", check);
            SetRef(window, "_victoryCard", victory);
            SetRef(window, "_victoryTitle", victoryTitle);
            SetRef(window, "_victoryBody", victoryBody);
            SetRef(window, "_keepButton", keep);
            SetRef(window, "_keepLabel", keepLabel);
            SetRef(window, "_realMatchButton", realMatch);
            SetRef(window, "_realMatchLabel", realMatchLabel);
            SetRef(window, "_deathRoot", death.gameObject);
            SetRef(window, "_deathTitle", deathTitle);
            SetRef(window, "_deathBody", deathBody);
            SetRef(window, "_restartButton", restart);
            SetRef(window, "_restartLabel", restartLabel);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsTutorialWindow)}.prefab");
        }

        /// <summary>부모 안 (x, y) 비율 자리에 크기 0 으로 매단다.</summary>
        private static void AnchorAt(RectTransform rect, float x, float y)
        {
            rect.anchorMin = new Vector2(x, y);
            rect.anchorMax = new Vector2(x, y);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>가운데에 놓는 정사각 그림 (누름 없음) — 스프라이트는 창이 켜질 때 넣는다.</summary>
        private static Image CenterImage(Transform parent, string name, float size, Color color)
        {
            RectTransform rect = Panel(parent, name, color);
            var image = rect.GetComponent<Image>();
            image.raycastTarget = false;
            CenterSquare(rect, size);
            return image;
        }

        /// <summary>화면 가운데 앵커 상자 (창이 좌표를 매 프레임 준다).</summary>
        private static RectTransform CenterBox(Transform parent, string name, Color color)
        {
            RectTransform rect = Panel(parent, name, color);
            rect.GetComponent<Image>().raycastTarget = false;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        /// <summary>검정 80% 알약 이름표 — 부모 가운데에서 <paramref name="below"/> 아래.</summary>
        private static RectTransform LabelPill(Transform parent, string name, float below, Color border, float borderWidth, RectOffset padding, float size, out TMP_Text label)
        {
            RectTransform pill = Panel(parent, name, new Color(0f, 0f, 0f, 0.8f));
            pill.GetComponent<Image>().raycastTarget = false;
            pill.anchorMin = new Vector2(0.5f, 0.5f);
            pill.anchorMax = new Vector2(0.5f, 0.5f);
            pill.pivot = new Vector2(0.5f, 1f);
            pill.anchoredPosition = new Vector2(0f, -below);
            Round(pill, 14f);
            var outline = pill.gameObject.AddComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(borderWidth, -borderWidth);
            var layout = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = padding;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            FitBoth(pill);
            label = Text(pill, "Text", "-", size, White, FontStyles.Bold);
            label.characterSpacing = 0.6f;
            return pill;
        }

        private static void FitBoth(RectTransform rect)
        {
            var fit = rect.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void AddBorder(RectTransform rect, Color color)
        {
            var outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(1f, -1f);
        }

        private static void ColumnLayout(RectTransform rect, RectOffset padding, float spacing)
        {
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        /// <summary>줄바꿈 가운데 정렬 글 — 폭 고정.</summary>
        private static TMP_Text WrappedText(Transform parent, string name, float size, Color color, float width)
        {
            TMP_Text text = Text(parent, name, "-", size, color, FontStyles.Normal);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.lineSpacing = 12f;
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            return text;
        }

        /// <summary>한 줄 글 — 높이(아래 여백 포함) 고정.</summary>
        private static TMP_Text FixedText(Transform parent, string name, string value, float size, Color color, FontStyles style, float height)
        {
            TMP_Text text = Text(parent, name, value, size, color, style, TextAlignmentOptions.Top);
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return text;
        }

        private static void Spacer(Transform parent, float height)
        {
            RectTransform gap = Group(parent, "Gap");
            var element = gap.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        /// <summary>카드 폭을 채우는 버튼 (둥근 12).</summary>
        private static Button CardButton(Transform parent, string name, Color color, float height, float size, out TMP_Text label)
        {
            Button button = ButtonWithLabel(parent, name, color, "-", size, out label);
            label.fontStyle = FontStyles.Bold;
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return button;
        }

        // ── 설정 — 원본 「설정」 창 틀 · 탭 · 카드 자리 [실측 probe/menu_modals.json settings]

        private static void BuildSettings()
        {
            GameObject root = WindowRoot(nameof(FrontWarsSettingsWindow));
            var window = root.AddComponent<FrontWarsSettingsWindow>();
            BuildSettingsBody(root, window);
            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsSettingsWindow)}.prefab");
        }

        private static void BuildSettingsBody(GameObject root, FrontWarsSettingsWindow window)
        {

            // 창 판 · 뒤로 · 제목 [실측 — 설명 창과 같은 틀]
            RectTransform panel = Panel(root.transform, "Panel", new Color(0f, 0f, 0f, 0.41f));
            PlaceMeasured(panel, 452f, 104f, 1120f, 880f);
            Round(panel, 16f);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.1f);
            border.effectDistance = new Vector2(1f, -1f);

            Button back = PlainButton(root.transform, "BackButton", new Color(1f, 1f, 1f, 0.05f));
            PlaceMeasured((RectTransform)back.transform, 477f, 129f, 44f, 44f);
            Image backIcon = IconImage(back.transform, 20f, new Color(1f, 1f, 1f, 0.8f));

            TMP_Text title = Text(root.transform, "Title", "설정", 26f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceMeasured(title.rectTransform, 535f, 131f, 400f, 39f);

            // 왼쪽 탭 칸 [실측 477,189,250×770 · 검정 25%] · 묶음 제목 「설정」 488,200 · 「테마」 488,342 (14px 700 흰색 35%)
            RectTransform nav = Panel(root.transform, "Nav", new Color(0f, 0f, 0f, 0.25f));
            nav.GetComponent<Image>().raycastTarget = false;
            Round(nav, 16f);
            PlaceMeasured(nav, 477f, 189f, 250f, 770f);

            TMP_Text navGroupSettings = Text(root.transform, "NavGroupSettings", "설정", 14f, new Color(1f, 1f, 1f, 0.35f), FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceMeasured(navGroupSettings.rectTransform, 500f, 200f, 200f, 31f);
            TMP_Text navGroupTheme = Text(root.transform, "NavGroupTheme", "테마", 14f, new Color(1f, 1f, 1f, 0.35f), FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceMeasured(navGroupTheme.rectTransform, 500f, 342f, 200f, 41f);

            // 탭 버튼 다섯 [실측 488,y,228×50 · 켜진 탭만 주황 10% 바탕 · 글자 538,y+12 17px 600]
            var tabButtons = new Button[FrontWarsSettingsWindow.Tabs.Length];
            var tabBackgrounds = new Image[FrontWarsSettingsWindow.Tabs.Length];
            var tabLabels = new TMP_Text[FrontWarsSettingsWindow.Tabs.Length];

            for (int i = 0; i < FrontWarsSettingsWindow.Tabs.Length; i++)
            {
                var (_, textKey, y) = FrontWarsSettingsWindow.Tabs[i];
                tabButtons[i] = PlainButton(root.transform, $"Tab{i}", new Color(1f, 1f, 1f, 0f));
                PlaceMeasured((RectTransform)tabButtons[i].transform, 488f, y, 228f, 50f);

                RectTransform highlight = Panel(tabButtons[i].transform, "Highlight", new Color(242f / 255f, 149f / 255f, 32f / 255f, 0.1f));
                Stretch(highlight);
                tabBackgrounds[i] = highlight.GetComponent<Image>();
                tabBackgrounds[i].raycastTarget = false;

                tabLabels[i] = Text(tabButtons[i].transform, "Label", textKey, 17f, Orange, FontStyles.Bold, TextAlignmentOptions.Left);
                PlaceTopLeft(tabLabels[i].rectTransform, 50f, 12f, 170f, 26f);
            }

            // 오른쪽 내용 [실측 743,189,804×770] · 묶음 제목 [764,210 · 764,356 · 764,582 — 14px 700 흰색 40%]
            RectTransform section = Panel(root.transform, "Section", new Color(0f, 0f, 0f, 0.25f));
            section.GetComponent<Image>().raycastTarget = false;
            Round(section, 16f);
            PlaceMeasured(section, 743f, 189f, 804f, 770f);

            var sectionTitles = new TMP_Text[FrontWarsSettingsWindow.Sections.Length];

            for (int i = 0; i < FrontWarsSettingsWindow.Sections.Length; i++)
            {
                sectionTitles[i] = Text(root.transform, $"SectionTitle{i}", "-", 14f, new Color(1f, 1f, 1f, 0.4f), FontStyles.Bold, TextAlignmentOptions.Left);
                PlaceMeasured(sectionTitles[i].rectTransform, 764f, FrontWarsSettingsWindow.Sections[i].Y, 762f, 21f);
            }

            // 카드 — 스위치는 카드 오른쪽 +307 자리에 세로 가운데 [실측 1071 · 1457], 슬라이더는 카드 +17, +70 [실측 1167,311 · 1167,537]
            int count = FrontWarsSettingsWindow.Cards.Length;
            var cardRoots = new GameObject[count];
            var cardTitles = new TMP_Text[count];
            var cardDescs = new TMP_Text[count];
            var cardValues = new TMP_Text[count];
            var cardSwitches = new Button[count];
            var cardTracks = new Image[count];
            var cardKnobs = new RectTransform[count];
            var cardSliders = new Slider[count];

            for (int i = 0; i < count; i++)
            {
                FrontWarsSettingCard card = FrontWarsSettingsWindow.Cards[i];
                RectTransform cardRect = SettingsCard(root.transform, $"Card{i}", card.X, card.Y, card.Height, "-",
                                                      out cardTitles[i], out cardDescs[i], card.TitleIndent, card.TitleTop);
                cardRoots[i] = cardRect.gameObject;

                if (card.Kind == EFrontWarsSettingKind.Slider)
                {
                    cardValues[i] = Text(root.transform, $"CardValue{i}", "-", 15f, Orange, FontStyles.Bold, TextAlignmentOptions.Right);
                    PlaceMeasured(cardValues[i].rectTransform, card.X + 259f, card.Y + 13f, 100f, 19f);
                    cardValues[i].transform.SetParent(cardRect, true);

                    cardSliders[i] = MakeSlider(root.transform, $"CardSlider{i}");
                    PlaceMeasured((RectTransform)cardSliders[i].transform, card.X + 17f, card.Y + 70f, 342f, SliderH);
                    cardSliders[i].transform.SetParent(cardRect, true);
                }
                else
                {
                    cardSwitches[i] = SettingsSwitch(root.transform, $"CardSwitch{i}", card.X + 307f, card.Y + (card.Height - FrontWarsSettingsWindow.SwitchHeight) * 0.5f,
                                                     out cardTracks[i], out cardKnobs[i]);
                    cardSwitches[i].transform.SetParent(cardRect, true);
                }
            }

            // 키 설정 탭 — 원본은 키를 바꿀 수 있지만 우리는 «보여주기만» 한다 (의도된 차이). 줄 자리는 미측정 — 설명 창 줄 규격을 빌려 두 열로 세운다
            RectTransform keysRoot = Group(root.transform, "KeysRoot");
            Stretch(keysRoot);
            var keyRowKeys = new TMP_Text[FrontWarsInstructionsWindow.Rows.Length];
            var keyRowTexts = new TMP_Text[FrontWarsInstructionsWindow.Rows.Length];

            for (int i = 0; i < FrontWarsInstructionsWindow.Rows.Length; i++)
            {
                float x = i < 8 ? 764f : 1150f;
                float y = 241f + (i % 8) * 70f;
                RectTransform row = Panel(keysRoot, $"KeyRow{i}", new Color(1f, 1f, 1f, 0.03f));
                row.GetComponent<Image>().raycastTarget = false;
                PlaceMeasured(row, x, y, 376f, 60f);

                keyRowKeys[i] = Text(row, "Keys", "-", 16f, White, FontStyles.Bold, TextAlignmentOptions.Left);
                PlaceTopLeft(keyRowKeys[i].rectTransform, 14f, 18f, 140f, 24f);
                keyRowTexts[i] = Text(row, "Text", "-", 15f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal, TextAlignmentOptions.Right);
                keyRowTexts[i].overflowMode = TextOverflowModes.Ellipsis;
                PlaceTopLeft(keyRowTexts[i].rectTransform, 160f, 18f, 202f, 24f);
            }

            // 테마 메인 · 배경 탭 — 고를 테마가 하나뿐이라 알림만 (의도된 차이)
            RectTransform themeRoot = Group(root.transform, "ThemeNoticeRoot");
            Stretch(themeRoot);
            TMP_Text themeNotice = Text(themeRoot, "ThemeNotice", "-", 15f, new Color(1f, 1f, 1f, 0.45f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            PlaceMeasured(themeNotice.rectTransform, 764f, 241f, 762f, 60f);

            // 초기화 카드 [실측 764,890,762×71 · 빨강 5% · 설명 781,904,562×41 · 버튼 1359,903,150×45 빨강 15% · 글자 15px 600 rgb(248,113,113)]
            Color resetRed = new Color(248f / 255f, 113f / 255f, 113f / 255f, 1f);
            RectTransform resetRoot = Panel(root.transform, "ResetCard", new Color(resetRed.r, resetRed.g, resetRed.b, 0.05f));
            resetRoot.GetComponent<Image>().raycastTarget = false;
            PlaceMeasured(resetRoot, 764f, 890f, 762f, 71f);

            TMP_Text resetDesc = Text(resetRoot, "Desc", "-", 15f, new Color(1f, 1f, 1f, 0.45f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            PlaceTopLeft(resetDesc.rectTransform, 17f, 14f, 562f, 41f);

            Button resetButton = PlainButton(root.transform, "ResetButton", new Color(resetRed.r, resetRed.g, resetRed.b, 0.15f));
            PlaceMeasured((RectTransform)resetButton.transform, 1359f, 903f, 150f, 45f);
            resetButton.transform.SetParent(resetRoot, true);
            TMP_Text resetLabel = Text(resetButton.transform, "Label", "-", 15f, resetRed, FontStyles.Bold, TextAlignmentOptions.Center);
            Stretch(resetLabel.rectTransform);

            SetRef(window, "_title", title);
            SetRef(window, "_backButton", back);
            SetRef(window, "_backIcon", backIcon);
            SetRef(window, "_navGroupSettings", navGroupSettings);
            SetRef(window, "_navGroupTheme", navGroupTheme);
            SetRefArray(window, "_tabButtons", tabButtons);
            SetRefArray(window, "_tabBackgrounds", tabBackgrounds);
            SetRefArray(window, "_tabLabels", tabLabels);
            SetRefArray(window, "_sectionTitles", sectionTitles);
            SetRefArray(window, "_cardRoots", cardRoots);
            SetRefArray(window, "_cardTitles", cardTitles);
            SetRefArray(window, "_cardDescs", cardDescs);
            SetRefArray(window, "_cardValues", cardValues);
            SetRefArray(window, "_cardSwitches", cardSwitches);
            SetRefArray(window, "_cardTracks", cardTracks);
            SetRefArray(window, "_cardKnobs", cardKnobs);
            SetRefArray(window, "_cardSliders", cardSliders);
            SetRef(window, "_keysRoot", keysRoot.gameObject);
            SetRefArray(window, "_keyRowKeys", keyRowKeys);
            SetRefArray(window, "_keyRowTexts", keyRowTexts);
            SetRef(window, "_themeNoticeRoot", themeRoot.gameObject);
            SetRef(window, "_themeNotice", themeNotice);
            SetRef(window, "_resetRoot", resetRoot.gameObject);
            SetRef(window, "_resetDesc", resetDesc);
            SetRef(window, "_resetButton", resetButton);
            SetRef(window, "_resetLabel", resetLabel);
        }

        /// <summary>설정 카드 [실측 흰색 3% · 테두리 흰색 10% · 제목 16px 600 · 설명 15px 흰색 45%] — 제목 · 설명은 카드 왼쪽에서 들여쓴 자리.</summary>
        private static RectTransform SettingsCard(Transform parent, string name, float x, float y, float height, string titleText,
                                                  out TMP_Text title, out TMP_Text desc, float titleIndent, float titleTop = 13f)
        {
            RectTransform card = Panel(parent, name, new Color(1f, 1f, 1f, 0.03f));
            card.GetComponent<Image>().raycastTarget = false;
            Round(card, 12f);
            PlaceMeasured(card, x, y, 376f, height);
            var border = card.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.1f);
            border.effectDistance = new Vector2(1f, -1f);

            title = Text(card, "Title", titleText, 16f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceTopLeft(title.rectTransform, titleIndent, titleTop, 260f, 20f);

            desc = Text(card, "Desc", "-", 15f, new Color(1f, 1f, 1f, 0.45f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            desc.overflowMode = TextOverflowModes.Ellipsis;
            PlaceTopLeft(desc.rectTransform, 17f, titleTop + 24f, 280f, 41f);
            return card;
        }

        /// <summary>켜기 끄기 스위치 [실측 52×28 · 둥근 끝 · 손잡이 20 흰색].</summary>
        private static Button SettingsSwitch(Transform parent, string name, float x, float y, out Image track, out RectTransform knob)
        {
            Button button = PlainButton(parent, name, new Color(1f, 1f, 1f, 0.2f));
            PlaceMeasured((RectTransform)button.transform, x, y, 52f, 28f);
            track = button.GetComponent<Image>();
            Round(button, 14f);

            knob = Panel(button.transform, "Knob", White);
            Round(knob, 10f);
            knob.GetComponent<Image>().raycastTarget = false;
            knob.anchorMin = new Vector2(0f, 0.5f);
            knob.anchorMax = new Vector2(0f, 0.5f);
            knob.pivot = new Vector2(0f, 0.5f);
            knob.sizeDelta = new Vector2(20f, 20f);
            knob.anchoredPosition = new Vector2(4f, 0f);
            return button;
        }

        // ── 게임 방법 — 원본 「설명」 창 틀 [실측 probe/menu_modals.json instructions]

        // ── 퀵 채팅 창 — 정보 판 「퀵 채팅」 버튼이 연다 [소스 폭 900 · 카테고리 → 문구 → 플레이어 → 미리 보기 → 전송]

        private const int QuickChatCategories = 6;
        private const int QuickChatPhrases = 16;
        private const int QuickChatPlayers = 10;

        private static void BuildQuickChat()
        {
            GameObject root = WindowRoot(nameof(FrontWarsQuickChatWindow));
            var window = root.AddComponent<FrontWarsQuickChatWindow>();
            Dimmer(root.transform, 0.6f);

            const float width = FrontWarsQuickChatWindow.WindowWidth;
            const float height = 560f;
            const float columnTop = 132f;
            const float columnHeight = 330f;

            RectTransform panel = Panel(root.transform, "Panel", ModalBg);
            PlaceCenter(panel, 0f, 0f, width, height);
            Round(panel, 16f);
            AddBorder(panel, new Color(1f, 1f, 1f, 0.1f));

            TMP_Text title = Text(panel, "Title", "-", 24f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceTopLeft(title.rectTransform, 32f, 28f, 400f, 32f);

            TMP_Text subtitle = Text(panel, "Subtitle", "-", 14f, White70, FontStyles.Normal, TextAlignmentOptions.Left);
            PlaceTopLeft(subtitle.rectTransform, 32f, 66f, 500f, 20f);

            Button close = ButtonWithLabel(panel, "CloseButton", new Color(1f, 1f, 1f, 0.12f), "×", 20f, out TMP_Text _);
            PlaceTopLeft((RectTransform)close.transform, width - 32f - 36f, 24f, 36f, 36f);

            // 왼쪽 카테고리 · 가운데 문구 · 오른쪽 플레이어
            RectTransform categoryColumn = Group(panel, "CategoryColumn");
            PlaceTopLeft(categoryColumn, 32f, columnTop, 200f, columnHeight);
            TMP_Text categoryTitle = Text(categoryColumn, "Title", "-", 14f, White70, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceTopLeft(categoryTitle.rectTransform, 0f, 0f, 200f, 20f);
            var categoryButtons = new Button[QuickChatCategories];
            var categoryLabels = new TMP_Text[QuickChatCategories];

            for (int i = 0; i < QuickChatCategories; i++)
            {
                categoryButtons[i] = ButtonWithLabel(categoryColumn, $"Category{i}", new Color(1f, 1f, 1f, 0.08f), "-", 14f, out categoryLabels[i]);
                PlaceTopLeft((RectTransform)categoryButtons[i].transform, 0f, 28f + i * 44f, 200f, 36f);
                Round(categoryButtons[i], 8f);
            }

            RectTransform phraseColumn = Group(panel, "PhraseColumn");
            PlaceTopLeft(phraseColumn, 252f, columnTop, 340f, columnHeight);
            TMP_Text phraseTitle = Text(phraseColumn, "Title", "-", 14f, White70, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceTopLeft(phraseTitle.rectTransform, 0f, 0f, 340f, 20f);
            var phraseButtons = new Button[QuickChatPhrases];
            var phraseLabels = new TMP_Text[QuickChatPhrases];

            for (int i = 0; i < QuickChatPhrases; i++)
            {
                phraseButtons[i] = ButtonWithLabel(phraseColumn, $"Phrase{i}", new Color(1f, 1f, 1f, 0.08f), "-", 13f, out phraseLabels[i]);
                PlaceTopLeft((RectTransform)phraseButtons[i].transform, 0f, 28f + i * 30f, 340f, 26f);
                Round(phraseButtons[i], 6f);
                phraseLabels[i].alignment = TextAlignmentOptions.Left;
                phraseLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                PlaceTopLeft(phraseLabels[i].rectTransform, 10f, 3f, 320f, 20f);
            }

            RectTransform playerColumn = Group(panel, "PlayerColumn");
            PlaceTopLeft(playerColumn, 612f, columnTop, 256f, columnHeight);
            TMP_Text playerTitle = Text(playerColumn, "Title", "-", 14f, White70, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceTopLeft(playerTitle.rectTransform, 0f, 0f, 256f, 20f);
            TMP_InputField search = InputField(playerColumn, "PlayerSearch", out TMP_Text placeholder);
            PlaceTopLeft((RectTransform)search.transform, 0f, 28f, 256f, 32f);
            var playerButtons = new Button[QuickChatPlayers];
            var playerLabels = new TMP_Text[QuickChatPlayers];

            for (int i = 0; i < QuickChatPlayers; i++)
            {
                playerButtons[i] = ButtonWithLabel(playerColumn, $"Player{i}", new Color(1f, 1f, 1f, 0.08f), "-", 13f, out playerLabels[i]);
                PlaceTopLeft((RectTransform)playerButtons[i].transform, 0f, 70f + i * 26f, 256f, 24f);
                Round(playerButtons[i], 6f);
                playerLabels[i].alignment = TextAlignmentOptions.Left;
                playerLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                PlaceTopLeft(playerLabels[i].rectTransform, 8f, 2f, 240f, 20f);
            }

            // 미리 보기 · 전송
            RectTransform previewBox = Panel(panel, "PreviewBox", new Color(1f, 1f, 1f, 0.06f));
            PlaceTopLeft(previewBox, 32f, 480f, width - 64f - 160f, 48f);
            Round(previewBox, 8f);
            TMP_Text preview = Text(previewBox, "Preview", "-", 15f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            PlaceTopLeft(preview.rectTransform, 14f, 13f, width - 64f - 190f, 24f);

            Button send = ButtonWithLabel(panel, "SendButton", Accent, "-", 16f, out TMP_Text sendLabel);
            sendLabel.fontStyle = FontStyles.Bold;
            PlaceTopLeft((RectTransform)send.transform, width - 32f - 140f, 480f, 140f, 48f);

            SetRef(window, "_title", title);
            SetRef(window, "_subtitle", subtitle);
            SetRef(window, "_categoryTitle", categoryTitle);
            SetRef(window, "_phraseTitle", phraseTitle);
            SetRef(window, "_playerTitle", playerTitle);
            SetRefArray(window, "_categoryButtons", categoryButtons);
            SetRefArray(window, "_categoryLabels", categoryLabels);
            SetRefArray(window, "_phraseButtons", phraseButtons);
            SetRefArray(window, "_phraseLabels", phraseLabels);
            SetRef(window, "_playerSearch", search);
            SetRef(window, "_playerPlaceholder", placeholder);
            SetRefArray(window, "_playerButtons", playerButtons);
            SetRefArray(window, "_playerLabels", playerLabels);
            SetRef(window, "_preview", preview);
            SetRef(window, "_sendButton", send);
            SetRef(window, "_sendLabel", sendLabel);
            SetRef(window, "_closeButton", close);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsQuickChatWindow)}.prefab");
        }

        private static void BuildInstructions()
        {
            GameObject root = WindowRoot(nameof(FrontWarsInstructionsWindow));
            var window = root.AddComponent<FrontWarsInstructionsWindow>();

            // 창 판 [실측 452,104,1120×880 · 검정 41% · 테두리 흰색 10%] — 둥근 모서리는 그림이 없어 생략
            RectTransform panel = Panel(root.transform, "Panel", new Color(0f, 0f, 0f, 0.41f));
            PlaceMeasured(panel, 452f, 104f, 1120f, 880f);
            Round(panel, 16f);
            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.1f);
            border.effectDistance = new Vector2(1f, -1f);

            // 뒤로 [실측 477,129,44×44 · 흰색 5% · 아이콘 20]
            Button close = PlainButton(root.transform, "CloseButton", new Color(1f, 1f, 1f, 0.05f));
            PlaceMeasured((RectTransform)close.transform, 477f, 129f, 44f, 44f);
            Image closeIcon = IconImage(close.transform, 20f, new Color(1f, 1f, 1f, 0.8f));

            // 제목 [실측 535,131 · 26px 700]
            TMP_Text title = Text(root.transform, "Title", "게임 방법", 26f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceMeasured(title.rectTransform, 535f, 131f, 400f, 39f);

            // 왼쪽 탭 목록 [실측 477,189,250×770 · 검정 25%] + 켜진 탭 「단축키」 [488,200,228×51 · 주황 10% · 글자 18px 600 주황]
            RectTransform nav = Panel(root.transform, "Nav", new Color(0f, 0f, 0f, 0.25f));
            nav.GetComponent<Image>().raycastTarget = false;
            Round(nav, 16f);
            PlaceMeasured(nav, 477f, 189f, 250f, 770f);

            // 탭 여덟 개 [실측 488,y,228×51 · 간격 55 · 켜진 탭만 주황 10% 바탕 · 글자 538,y+12 18px 600]
            int tabCount = FrontWarsInstructionsWindow.Tabs.Length;
            var tabButtons = new Button[tabCount];
            var tabHighlights = new Image[tabCount];
            var tabLabels = new TMP_Text[tabCount];

            for (int i = 0; i < tabCount; i++)
            {
                tabButtons[i] = PlainButton(root.transform, $"Tab{i}", new Color(1f, 1f, 1f, 0f));
                PlaceMeasured((RectTransform)tabButtons[i].transform, 488f, FrontWarsInstructionsWindow.Tabs[i].Y, 228f, 51f);

                RectTransform highlight = Panel(tabButtons[i].transform, "Highlight", new Color(242f / 255f, 149f / 255f, 32f / 255f, 0.1f));
                Stretch(highlight);
                tabHighlights[i] = highlight.GetComponent<Image>();
                tabHighlights[i].raycastTarget = false;
                Round(highlight, 12f);

                tabLabels[i] = Text(tabButtons[i].transform, "Label", "-", 18f, Orange, FontStyles.Bold, TextAlignmentOptions.Left);
                PlaceTopLeft(tabLabels[i].rectTransform, 50f, 12f, 170f, 27f);
            }

            // 오른쪽 내용 [실측 743,189,804×770 · 검정 25%] + 제목 [764,210 · 24px 700]
            RectTransform section = Panel(root.transform, "Section", new Color(0f, 0f, 0f, 0.25f));
            section.GetComponent<Image>().raycastTarget = false;
            Round(section, 16f);
            PlaceMeasured(section, 743f, 189f, 804f, 770f);

            TMP_Text heading = Text(root.transform, "Heading", "단축키", 24f, White, FontStyles.Bold, TextAlignmentOptions.Left);
            PlaceMeasured(heading.rectTransform, 764f, 210f, 762f, 32f);

            // 카드 [실측 376×60 · 2열 x 764/1150 · 줄 간격 70 · y 258 부터 · 흰색 3%]
            int rows = FrontWarsInstructionsWindow.Rows.Length;
            var keys = new TMP_Text[rows];
            var texts = new TMP_Text[rows];

            RectTransform keysRoot = Group(root.transform, "KeysRoot");
            Stretch(keysRoot);

            for (int i = 0; i < rows; i++)
            {
                float x = i % 2 == 0 ? 764f : 1150f;
                float y = 258f + (i / 2) * 70f;

                RectTransform card = Panel(keysRoot, $"Card{i}", new Color(1f, 1f, 1f, 0.03f));
                card.GetComponent<Image>().raycastTarget = false;
                PlaceMeasured(card, x, y, 376f, 60f);

                // 키 칩 [실측 x+17, y+13, 높이 34 · 검정 50% · 16px 600 흰색 90%]
                RectTransform chip = Panel(card, "KeyChip", new Color(0f, 0f, 0f, 0.5f));
                chip.GetComponent<Image>().raycastTarget = false;
                PlaceTopLeft(chip, 17f, 13f, 116f, 34f);
                keys[i] = Text(chip, $"Key{i}", "-", 16f, new Color(1f, 1f, 1f, 0.9f), FontStyles.Bold);
                Stretch(keys[i].rectTransform);

                // 설명 [실측 오른쪽 정렬 · 끝 x+359 · 16px 흰색 75%]
                texts[i] = Text(card, $"Row{i}", "-", 16f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal, TextAlignmentOptions.Right);
                texts[i].overflowMode = TextOverflowModes.Ellipsis;
                PlaceTopLeft(texts[i].rectTransform, 140f, 19f, 219f, 22f);
            }

            // 나머지 탭 내용 — 「이름 + 설명」 두 열 [원본은 왼쪽 글 837 · 오른쪽 글 1223 · 줄 간격 약 76 · 자리 미측정이라 근사]
            int items = FrontWarsInstructionsWindow.ItemsPerTab;
            RectTransform itemsRoot = Group(root.transform, "ItemsRoot");
            Stretch(itemsRoot);
            var itemRoots = new GameObject[items];
            var itemNames = new TMP_Text[items];
            var itemDescs = new TMP_Text[items];

            for (int i = 0; i < items; i++)
            {
                float x = i % 2 == 0 ? 781f : 1167f;
                float y = 254f + (i / 2) * 120f;

                RectTransform item = Group(itemsRoot, $"Item{i}");
                PlaceMeasured(item, x, y, 342f, 110f);
                itemRoots[i] = item.gameObject;

                itemNames[i] = Text(item, "Name", "-", 16f, White, FontStyles.Bold, TextAlignmentOptions.TopLeft);
                PlaceTopLeft(itemNames[i].rectTransform, 0f, 0f, 342f, 22f);

                itemDescs[i] = Text(item, "Desc", "-", 15f, new Color(1f, 1f, 1f, 0.75f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
                PlaceTopLeft(itemDescs[i].rectTransform, 0f, 26f, 342f, 80f);
            }

            itemsRoot.gameObject.SetActive(false);

            SetRef(window, "_title", title);
            SetRef(window, "_heading", heading);
            SetRef(window, "_closeButton", close);
            SetRef(window, "_closeIcon", closeIcon);
            SetRefArray(window, "_rowKeys", keys);
            SetRefArray(window, "_rowTexts", texts);
            SetRef(window, "_keysRoot", keysRoot.gameObject);
            SetRefArray(window, "_tabButtons", tabButtons);
            SetRefArray(window, "_tabHighlights", tabHighlights);
            SetRefArray(window, "_tabLabels", tabLabels);
            SetRef(window, "_itemsRoot", itemsRoot.gameObject);
            SetRefArray(window, "_itemRoots", itemRoots);
            SetRefArray(window, "_itemNames", itemNames);
            SetRefArray(window, "_itemDescs", itemDescs);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(FrontWarsInstructionsWindow)}.prefab");
        }

        // ── 도우미

        private static GameObject WindowRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            return root;
        }

        private static RectTransform Group(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return go.GetComponent<RectTransform>();
        }

        /// <summary>모서리를 둥글린다 [실측 창 16px · 카드 · 버튼 12px · 작은 칸 4~8px · 스위치는 반원]. 그림은 런타임에 만들어 넣는다.</summary>
        private static void Round(Component target, float radius)
        {
            if (target == null)
                return;

            var rounded = target.gameObject.GetComponent<FrontWarsRoundedImage>();

            if (rounded == null)
                rounded = target.gameObject.AddComponent<FrontWarsRoundedImage>();

            var serialized = new SerializedObject(rounded);
            serialized.FindProperty("_radius").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>줄 안에서 오른쪽 끝에 붙인다 (오른쪽에서 <paramref name="right"/> 만큼 떨어진 자리, 폭 <paramref name="width"/>).</summary>
        private static void PlaceRowRight(RectTransform rect, float right, float width)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(width, 20f);
            rect.anchoredPosition = new Vector2(-right + width, 0f);
        }

        private static void Dimmer(Transform parent, float alpha)
        {
            RectTransform dim = Panel(parent, "Dimmer", new Color(0f, 0f, 0f, alpha));
            dim.SetAsFirstSibling();
            Stretch(dim);
        }

        private static TMP_Text Text(Transform parent, string name, string value, float size, Color color, FontStyles style,
                                     TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = style;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }

        private static Button PlainButton(Transform parent, string name, Color color)
        {
            RectTransform rect = Panel(parent, name, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            Round(rect, 12f);
            return button;
        }

        private static Button ButtonWithLabel(Transform parent, string name, Color color, string label, float size, out TMP_Text text)
        {
            Button button = PlainButton(parent, name, color);
            text = Text(button.transform, "Label", label, size, White, FontStyles.Normal);
            Stretch(text.rectTransform);
            return button;
        }

        private static Slider MakeSlider(Transform parent, string name)
        {
            RectTransform root = Group(parent, name);

            RectTransform bg = Panel(root, "Background", new Color(1f, 1f, 1f, 0.2f));
            Stretch(bg);

            RectTransform fillArea = Group(root, "Fill Area");
            Stretch(fillArea);
            RectTransform fill = Panel(fillArea, "Fill", Orange);
            fill.GetComponent<Image>().raycastTarget = false;
            Stretch(fill);

            RectTransform handleArea = Group(root, "Handle Slide Area");
            Stretch(handleArea);
            RectTransform handle = Panel(handleArea, "Handle", White);
            handle.sizeDelta = new Vector2(14f, 10f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 100f;
            slider.wholeNumbers = true;
            return slider;
        }

        private static Toggle MakeToggle(Transform parent, string name, float size, out TMP_Text label)
        {
            RectTransform root = Group(parent, name);

            RectTransform box = Panel(root, "Background", new Color(1f, 1f, 1f, 0.15f));
            box.anchorMin = new Vector2(0f, 0.5f);
            box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0f, 0.5f);
            box.anchoredPosition = Vector2.zero;
            box.sizeDelta = new Vector2(20f, 20f);

            RectTransform check = Panel(box, "Checkmark", Orange);
            check.GetComponent<Image>().raycastTarget = false;
            check.anchorMin = new Vector2(0.5f, 0.5f);
            check.anchorMax = new Vector2(0.5f, 0.5f);
            check.sizeDelta = new Vector2(12f, 12f);

            label = Text(root, "Label", name, size, White, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(28f, 0f);

            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        private static TMP_InputField InputField(Transform parent, string name, out TMP_Text placeholder)
        {
            RectTransform root = Panel(parent, name, new Color(1f, 1f, 1f, 0.1f));

            RectTransform area = Group(root, "Text Area");
            Stretch(area);
            area.offsetMin = new Vector2(12f, 4f);
            area.offsetMax = new Vector2(-12f, -4f);
            area.gameObject.AddComponent<RectMask2D>();

            placeholder = Text(area, "Placeholder", "이름", 18f, new Color(1f, 1f, 1f, 0.4f), FontStyles.Italic, TextAlignmentOptions.Left);
            Stretch(placeholder.rectTransform);

            TMP_Text text = Text(area, "Text", string.Empty, 18f, White, FontStyles.Normal, TextAlignmentOptions.Left);
            Stretch(text.rectTransform);

            var input = root.gameObject.AddComponent<TMP_InputField>();
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = root.GetComponent<Image>();
            input.characterLimit = 27;
            return input;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Height(RectTransform rect, float height)
        {
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void ColumnAt(RectTransform rect, float x, float width)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width, 0f);
        }

        /// <summary>부모 왼쪽 위 기준 px 상자.</summary>
        private static void PlaceTopLeft(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>
        /// 원본 화면 px(1920×1080 왼쪽 위 기준) 상자를 <b>가장 가까운 화면 모서리</b>에 매단다 — 오른쪽 절반은 오른쪽 · 아래 절반은 아래.
        /// 원본 HUD 는 창 모서리 기준이라 다른 화면비에서도 모서리와의 거리가 같다.
        /// </summary>
        private static void PlaceScreen(RectTransform rect, float x, float y, float w, float h)
        {
            bool right = x + w * 0.5f > CanvasW * 0.5f;
            bool bottom = y + h * 0.5f > CanvasH * 0.5f;

            rect.anchorMin = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(right ? x - CanvasW : x, bottom ? CanvasH - y : -y);
        }

        /// <summary>
        /// 원본 화면 px 상자(1920×1080 왼쪽 위 기준)를 «화면 가운데» 앵커로 놓는다 — 메뉴 · 싱글 설정처럼 가운데 모인 화면용.
        /// 다른 화면비에서도 가운데에서 잰 거리가 같다.
        /// </summary>
        private static void PlaceMeasured(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x - CanvasW * 0.5f, CanvasH * 0.5f - y);
        }

        /// <summary>원본 화면 px 상자를 화면 «가운데 아래»에 매단다 — 가로는 화면 중앙에서 잰 거리가 같다.</summary>
        private static void PlaceBottomCenter(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x - CanvasW * 0.5f, CanvasH - y);
        }

        /// <summary>버튼 가운데 아이콘 자리 — 스프라이트는 창이 열릴 때 넣는다 (코드로 그린 도형).</summary>
        private static Image IconImage(Transform parent, float size, Color color)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            CenterSquare((RectTransform)go.transform, size);
            return image;
        }

        /// <summary>부모 가운데에 한 변 크기 정사각형.</summary>
        private static void CenterSquare(RectTransform rect, float size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>화면 가운데 기준 (y 위로 +).</summary>
        private static void PlaceCenter(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h);
            rect.anchoredPosition = new Vector2(x, y);
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);

            if (success == false)
                Log.Error($"프리팹 저장 실패: {path}");

            Object.DestroyImmediate(root);
        }

        /// <summary>⚠ 「썼다」가 아니라 «됐다»로 확인한다.</summary>
        private static void SetRef(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (new SerializedObject(target).FindProperty(field).objectReferenceValue != value)
                Log.Error($"{target.GetType().Name}.{field} 이 안 들어갔다");
        }

        private static void SetRefArray(Object target, string field, Object[] values)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.arraySize = values.Length;

            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (new SerializedObject(target).FindProperty(field).arraySize != values.Length)
                Log.Error($"{target.GetType().Name}.{field} 배열이 안 들어갔다");
        }
    }
}
