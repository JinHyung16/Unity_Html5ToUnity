using System.Collections.Generic;
using System.IO;
using JinHyung.Core;
using JinHyung.Data;
using JinHyung.UI;
using JinHyung.UI.Fx;
using JinHyung.UndeadSlayer;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// UI 프리팹을 <b>코드로 굽는다</b> (확정표 10-b). 손으로 고친 것은 다음 실행에 날아간다.
    ///
    /// <para>
    /// ★ <b>좌표는 원본 실측값</b>이다 — 원본 렌더러가 1031×580 일 때의 화면 좌표를
    /// 우리 캔버스(1920×1080)로 <b>세로 배율 1080/580 = 1.862069</b> 로 올린다.
    /// 원본이 «세로 고정»이므로 세로 배율 하나로 전부 환산된다 (확정표 3-d).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>가로는 «화면 폭»에 매단다.</b> 원본은 가로가 화면비만큼 늘어나므로
    /// 상단 바처럼 화면을 가로지르는 것은 <b>좌우 앵커를 늘려</b> 붙인다 —
    /// 고정 폭으로 박으면 다른 화면비에서 원본과 갈린다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.UndeadPrefabBuilder.BuildAll</c></para>
    /// </summary>
    public static class UndeadPrefabBuilder
    {
        private const string UiPrefabRoot = "Assets/Undead-Slayer/Resources/UI/Window";
        private const string UiArtRoot = "Assets/Undead-Slayer/Art/UI";

        /// <summary>
        /// 한글 폰트 에셋. ⚠ <b>이걸 안 물리면 원본 문구 60키가 전부 «두부»로 뜬다</b> —
        /// TMP 기본 폰트에 한글 글리프가 없다.
        /// </summary>
        private const string FontAssetPath = "Assets/Undead-Slayer/Resources/Font/UndeadSlayer SDF.asset";

        /// <summary>원본 설계 세로 [실측 · 확정표 3-d].</summary>
        private const float OriginHeight = 580f;

        /// <summary>원본 논리 폭 [실측 1031 = 580 × 16/9]. 가로 자리를 «화면 중앙 기준»으로 옮길 때 쓴다.</summary>
        private const float OriginWidth = 1031f;

        /// <summary>우리 캔버스 세로 (확정표 3).</summary>
        private const float CanvasHeight = 1080f;

        /// <summary>
        /// 데이터 표가 사는 곳 — <b>편집 모드에서 직접 읽는다</b>.
        /// <para>⚠ <c>GameRoot</c> 의 컨테이너는 «재생 중»에만 있다. 편집 모드에서 쓰면 조용히 <c>null</c> 이다.</para>
        /// </summary>
        private const string DataFolder = "Assets/Undead-Slayer/Data";

        /// <summary>원본 → 우리 환산 배율. <b>세로 하나로 전부 환산된다</b>.</summary>
        private const float Scale = CanvasHeight / OriginHeight;

        // ── 시작 게이트 [소스 startGame] — 값은 원본 저작 상수다
        private const float StartButtonW = 260f;
        private const float StartButtonH = 104f;
        private const float StartButtonYFactor = 0.75f;
        private const float StartLabelFont = 30f;

        // ── 원본 실측 배치 (렌더러 1031×580 · 좌상단 기준) [`04_UIUX규칙.md`]
        private const float GaugeBarX = 34f;
        private const float GaugeBarY = 17f;
        private const float GaugeBarRightMargin = 1031f - (34f + 957f);   // = 40
        private const float GaugeBarH = 20f;
        private const float GemIconX = 5f;
        private const float GemIconY = -3f;
        private const float GemIconSize = 60f;
        private const float LevelBgRightMargin = 1031f - (959f + 72f);    // = 0
        private const float LevelBgY = -9f;
        private const float LevelBgSize = 72f;
        // ── 좌하단 과제 포인터 [실측 · 회차 7 — 화살표 bbox (-2,475,110,94) · 숫자 bbox (32,508,59,19)]
        //    ⚠ bbox 110×94 는 «돌아간 뒤»의 외접 상자다 — 스프라이트 자체는 아틀라스 실측 **94×58** 이다.
        //      bbox 를 크기로 쓰면 화살표가 그 각도에서만 맞고 나머지 각도에서 커진다.
        private const float QuestArrowW = 94f;
        private const float QuestArrowH = 58f;
        private const float QuestTextFont = 14f;                      // [실측 — size 14 · bold]

        // ⚠ 세로는 «글리프 중심»이다 [사건 프레임 덤프 — 회차 12]. 「글자 위」가 아니다.
        //   가로도 «화면 중앙»이 아니다 — 게이지 «바»의 중앙이다 [소스 xpText.x = barX + barWidth/2 = 34 + 957/2].
        private const float GaugeTextCenterX = GaugeBarX + 957f * 0.5f;   // = 512.5
        private const float GaugeTextCenterY = 28f;
        private const float GaugeTextFont = 12f;
        private const float TimerTextCenterY = 55f;
        private const float TimerTextFont = 14f;
        private const float LevelTextY = 17f;
        private const float LevelTextFont = 18f;

        public static void BuildAll()
        {
            EnsureFolder(UiPrefabRoot);

            BuildBattleHud();
            BuildLevelUp();
            BuildReady();
            BuildRevive();
            BuildWeaponUnlock();
            BuildTaskComplete();
            BuildLobbyHud();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log.Success($"Undead Slayer UI 프리팹 굽기 완료 — 환산 배율 {Scale:F6} (세로 {OriginHeight} → {CanvasHeight})");
        }

        private static void BuildBattleHud()
        {
            var root = new GameObject(nameof(UndeadBattleHudWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());

            var window = root.AddComponent<UndeadBattleHudWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Normal);

            // ── 상단 게이지 바. ★ 좌우 앵커를 늘린다 — 원본은 가로가 화면비만큼 늘어난다.
            GameObject barBg = Sprite9(root.transform, "GaugeBar", "skill_bg");
            RectTransform barRect = barBg.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(GaugeBarX * Scale, 0f);
            barRect.offsetMax = new Vector2(-GaugeBarRightMargin * Scale, 0f);
            barRect.anchoredPosition = new Vector2(barRect.anchoredPosition.x, -GaugeBarY * Scale);
            barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, GaugeBarH * Scale);

            // 채워지는 부분 — 원본의 `bar` 다 (평소엔 꺼져 있어 캡처에 안 잡힌다 [실측])
            GameObject fill = Sprite9(barBg.transform, "Fill", "bar");
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect);

            // ★ 채움은 바 «안쪽»이다 — 좌·상·하로 1px 들어간다 [소스 fill.x = barX+1 · y = barY+1 · height = barH−2]
            //   ⚠ 폭은 «자란다» — 그래서 왼쪽 위/아래에만 붙이고 폭은 창이 매 프레임 준다.
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(0f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = new Vector2(1f * Scale, 1f * Scale);
            fillRect.offsetMax = new Vector2(1f * Scale, -1f * Scale);
            fillRect.sizeDelta = new Vector2(0f, fillRect.sizeDelta.y);

            var fillImage = fill.GetComponent<Image>();

            // ★★ <b>채움도 9슬라이스다</b> [소스 — 채움 판도 NineSlicePlane 이고 «폭»을 바꾼다].
            //   ⚠ [사고] <c>Filled</c>(가로 채우기)로 두었었다 — 그건 그림을 «자른다».
            //     원본은 폭이 줄어도 <b>양끝 둥근 마감이 남는데</b>, 자르면 오른쪽 끝이 «싹둑» 잘린다.
            //     둘은 게이지가 가득 찼을 때만 같아 보이고, 차오르는 동안 계속 다르다.
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 1f / Scale;

            // ── 좌상단 보석 아이콘
            GameObject gem = SpriteImage(root.transform, "GemIcon", "gem_icon");
            Place(gem, GemIconX, GemIconY, GemIconSize, GemIconSize, anchorRight: false);

            // ── 우상단 레벨 배경.
            // ⚠⚠ <b>9슬라이스가 아니다</b> [소스 — <c>new Se(K.from("lvl_bg")); scale.set(4.5)</c>].
            //   16×16 그림을 «통째로» 4.5배 키운다 — 테두리도 4.5배 굵어진다.
            //   [사고] 9슬라이스로 두었더니 테두리가 1px 그대로 남아 «실오라기»처럼 얇게 나왔다.
            GameObject levelBg = SpriteImage(root.transform, "LevelBg", "lvl_bg");
            Place(levelBg, LevelBgRightMargin, LevelBgY, LevelBgSize, LevelBgSize, anchorRight: true);

            // ── 문구 셋. 게이지·타이머는 «화면 가운데»에 붙는다 [실측 — 487.5 와 475 가 중앙 근처다]
            TMP_Text gaugeText = Text(root.transform, "GaugeText", "0/0", GaugeTextFont, Color.white);
            CenterAt(gaugeText.rectTransform, GaugeTextCenterY, GaugeTextFont, GaugeTextCenterX - OriginWidth * 0.5f);

            TMP_Text timerText = Text(root.transform, "TimerText", "00:00", TimerTextFont, Color.white);
            Outline(timerText, TimerTextFont, 3f, Color.black);   // [소스 timeText stroke {0, 3}]
            CenterAt(timerText.rectTransform, TimerTextCenterY, TimerTextFont, GaugeTextCenterX - OriginWidth * 0.5f);

            // 레벨 숫자는 «검정»이다 [실측 — fill 0]
            TMP_Text levelText = Text(levelBg.transform, "LevelText", "1", LevelTextFont, Color.black);
            Stretch(levelText.rectTransform);
            levelText.rectTransform.anchoredPosition = new Vector2(2f * Scale, -1f * Scale);   // [소스 levelText.x = circle.x+2 · y = circle.y+1]
            levelText.alignment = TextAlignmentOptions.Center;
            Outline(levelText, LevelTextFont, 2f, Color.white);   // [소스 levelText stroke {16777215, 2}]

            // ── 좌하단 «과제 포인터» — 화살표 + 거리 [실측 · 회차 7]
            //    ⚠ 화살표는 «자기 중심»으로 돈다. 피벗을 (0,1) 로 두면 회전이 화면 밖으로 휘둘린다.
            // ⚠ 자리는 «고정»이 아니다 — 대상 방향으로 화면 가장자리를 따라 움직인다 [소스 bd.update · 회차 12 덤프로 정정].
            //    그래서 화면 «가운데» 앵커에 두고 창(UndeadBattleHudWindow.SetQuest)이 매 프레임 놓는다.
            GameObject arrow = SpriteImage(root.transform, "QuestArrow", "quest_pointer");
            PlaceFromCenter(arrow, QuestArrowW, QuestArrowH);

            // 숫자는 «돌지 않는다» — 화살표의 자식으로 넣으면 같이 뒤집힌다.
            TMP_Text questText = Text(root.transform, "QuestDistanceText", "0m", QuestTextFont, Color.white);
            questText.fontStyle = FontStyles.Bold;
            Outline(questText, QuestTextFont, 3f, Color.black);   // [소스 distanceText stroke {0, 3}]
            PlaceFromCenter(questText.gameObject, 120f, QuestTextFont * 1.6f);

            // ── 스킬 슬롯 넷 [소스 skillHud] — 오른쪽 아래. 가진 것만 보이고 왼쪽부터 붙는다.
            BuildSkillSlots(root.transform, window);

            SetRef(window, "_gaugeFill", fillImage);
            SetRef(window, "_gaugeText", gaugeText);
            SetRef(window, "_timerText", timerText);
            SetRef(window, "_levelText", levelText);
            SetRef(window, "_questArrow", arrow.GetComponent<RectTransform>());
            SetRef(window, "_questDistanceText", questText);

            // ── 최고 기록 [소스 guiBestScore] — 여백 8 · 좌하단/우하단 · 글자 14 · 검정 + 흰 테두리 3 · 굵게
            TMP_Text bestLevel = Text(root.transform, "BestLevel", "최고 레벨: 1", BestFont, Color.black);
            CornerAt(bestLevel.rectTransform, left: true);
            Outline(bestLevel, Color.white, BestStrokeWidth, BestFont);

            TMP_Text bestTime = Text(root.transform, "BestTime", "최고 시간: 00:00", BestFont, Color.black);
            CornerAt(bestTime.rectTransform, left: false);
            Outline(bestTime, Color.white, BestStrokeWidth, BestFont);

            // 최고 기록은 «폭만» 제한한다 [소스 Mh(bestScoreText, .4 × 화면폭, 자기 높이)]
            Fit(bestLevel, 0.4f * 1031f, 0f);
            Fit(bestTime, 0.4f * 1031f, 0f);

            SetRef(window, "_bestLevelText", bestLevel);
            SetRef(window, "_bestTimeText", bestTime);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadBattleHudWindow)}.prefab");
        }

        /// <summary>
        /// 레벨업 카드 창. <b>원본 실측 배치</b> — 카드 3장이 세로로 y 148 / 255 / 362 (세로 580 기준),
        /// 「선택」 버튼이 y 497 이다.
        /// </summary>
        private static void BuildLevelUp()
        {
            var root = new GameObject(nameof(UndeadLevelUpWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            Dimmer(root.transform, 0.7f);   // 화면 전체 검은 반투명 [소스 redrawBackground alpha .7]

            var window = root.AddComponent<UndeadLevelUpWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Popup);

            // 「레벨 업!」 — 원본 실측 y 55~81 · 화면 가운데
            TMP_Text title = Text(root.transform, "Title", "레벨 업!", 24f, Color.white);   // [실측 덤프 — font 24 · 글리프 중심 68]
            Fit(title, 999f, 32f);   // 레벨업 제목 — [소스 Mh(title, 화면폭 − 32, 32)]
            CenterAt(title.rectTransform, 68f, 24f);

            // ── 카드 3장. 원본 카드 폭은 x 307~727 (=420) · 높이 약 90 [실측 캡처]
            // 카드 «중심» 148 / 255 / 362 · 크기 420×92 [소스 · 사건 프레임 덤프로 확인]
            float[] cardCenterY = { 148f, 255f, 362f };
            const float cardW = 420f;
            const float cardH = 92f;

            var buttons = new Button[3];
            var frames = new Image[3];
            var icons = new Image[3];
            var targets = new TMP_Text[3];
            var stats = new TMP_Text[3];

            for (int i = 0; i < 3; i++)
            {
                // ⚠ 카드는 인셋이 «10» 이다 — 액션 버튼(15)과 다르다 [소스 createButton / createActionButton].
                //   그림은 같고 인셋만 달라서 «행»이 둘이다 (엔진의 인셋은 스프라이트 에셋에 붙는다).
                GameObject card = Sprite9(root.transform, $"Card{i}", "reward_button_bg_card");
                RectTransform rect = card.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(cardW * Scale, cardH * Scale);
                rect.anchoredPosition = new Vector2(0f, -cardCenterY[i] * Scale);

                var image = card.GetComponent<Image>();

                buttons[i] = MakeButton(card);   // 배율·알파는 UI Fx 컴포넌트가 [소스] 값으로
                card.AddComponent<UiStateScale>().Configure(1.08f, 1f, 1f, 0.9f, image);   // [소스 selectedButtonScale · 알파 1/.9]
                card.AddComponent<UiHoverScale>().Configure(1.04f);                        // [소스 hoverButtonScale]
                frames[i] = image;

                GameObject icon = SpriteImage(card.transform, "Icon", "icon_bullet_damage");
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.sizeDelta = new Vector2(54f * Scale, 54f * Scale);
                iconRect.anchoredPosition = new Vector2(13f * Scale, 0f);   // [실측 — 아이콘 중심 = 카드 왼쪽 +40 · 폭 54]
                icons[i] = icon.GetComponent<Image>();

                targets[i] = Text(card.transform, "Target", "총알", 18f, Color.white);   // [덤프 — font 18]
                LeftAt(targets[i].rectTransform, 90f, 12f, 18f);    // [덤프 — 카드 중심 −12]

                stats[i] = Text(card.transform, "Stat", "피해량 +100%", 14f, Color.white);   // [덤프 — font 14]
                LeftAt(stats[i].rectTransform, 90f, -13f, 14f);    // [덤프 — 카드 중심 +13]
            }

            // ── 「선택」 — 원본 y 497 · 고른 뒤에야 나타난다 [실측]
            GameObject confirm = Sprite9(root.transform, "Confirm", "reward_button_bg");
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 1f);
            confirmRect.anchorMax = new Vector2(0.5f, 1f);
            confirmRect.pivot = new Vector2(0.5f, 0.5f);
            confirmRect.sizeDelta = new Vector2(210f * Scale, 58f * Scale);   // [실측 덤프 — 210.7×58.2]
            confirmRect.anchoredPosition = new Vector2(0f, -497f * Scale);

            Button confirmButton = MakeButton(confirm);
            confirm.AddComponent<UiScalePulse>().Configure(EUiPulseKind.SineWobble, 0.025f, 0.008f);   // [소스 1 + .025·sin(.008t)]

            TMP_Text confirmText = Text(confirm.transform, "Label", "선택", 20f, Color.white);   // [실측 덤프 — font 20]
            Fit(confirmText, 178f, 24f);   // 「선택」 — [소스 Mh(chooseButtonText, 버튼폭 − 32, 24)]
            Stretch(confirmText.rectTransform);

            SetRef(window, "_titleText", title);
            SetRefArray(window, "_cardButtons", buttons);
            SetRefArray(window, "_cardFrames", frames);
            SetRefArray(window, "_cardIcons", icons);
            SetRefArray(window, "_cardTargetTexts", targets);
            SetRefArray(window, "_cardStatTexts", stats);
            SetRef(window, "_confirmButton", confirmButton);
            SetRef(window, "_confirmText", confirmText);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadLevelUpWindow)}.prefab");
        }

        /// <summary>
        /// 바이옴 진입 화면 — 「시작」 버튼 하나.
        /// <para>실측: 「시작」 문구 중심 (515.5, 437) · 글자 높이 30 (세로 580 기준).</para>
        /// </summary>
        private static void BuildReady()
        {
            var root = new GameObject(nameof(UndeadReadyWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            Dimmer(root.transform, 0.5f);   // [소스 dimmer alpha .5]

            var window = root.AddComponent<UndeadReadyWindow>();

            // ★★ 시작 화면의 디머는 <b>HUD 를 «덮는다»</b> — 다른 모달과 «같은 층»이다.
            //   [덤프] 시작 화면 `jd` 의 부모는 모달 층 `Kd`(id 102) 이고, HUD `Qc`(id 61) 보다 «뒤에» 그려진다.
            //   ⚠ [사고] 회차 19 까지 «Normal» 이었다 — 근거가 「원본 캡처」였고, 그림을 눈으로 읽어
            //     「HUD 가 디머 위에 보인다」고 적어 두었다. 덤프의 «부모·순서»를 보면 반대다.
            SetEnum(window, "_windowType", (int)EWindowType.Popup);

            // [소스] buttonWidth 260 · buttonHeight 104 · btn_shadowed 9슬라이스 16 · y = 화면 높이 × 0.75
            GameObject button = Sprite9(root.transform, "Start", "btn_shadowed");
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(StartButtonW * Scale, StartButtonH * Scale);
            rect.anchoredPosition = new Vector2(0f, -OriginHeight * StartButtonYFactor * Scale);

            var image = button.GetComponent<Image>();

            Button startButton = MakeButton(button);   // 틴트·배율은 창이 [소스] 규칙으로 그린다

            // [소스 syncButtonTransform] hover(1.06 · 틴트 #3D9941) × (1 + 0.5·(sin(t·0.005)+1)·0.13)
            var pulse = button.AddComponent<UiScalePulse>();
            pulse.Configure(EUiPulseKind.SinePulse, 0.13f, 0.005f);
            button.AddComponent<UiHoverScale>().Configure(1.06f, new Color32(0x3D, 0x99, 0x41, 0xFF), image);

            // 글자 50 bold 를 높이 30 안에 맞춘다 [소스 Mh(…, 30)] — 실측 글자 높이 30 과 같다
            TMP_Text label = Text(button.transform, "Label", "시작", StartLabelFont, Color.white);
            Fit(label, 186f, 30f);   // 시작 버튼 — [소스 Mh(buttonText, 260 − 2×32 − 10, 30)]
            label.fontStyle = FontStyles.Bold;
            Stretch(label.rectTransform);
            label.rectTransform.anchoredPosition = new Vector2(0f, -2f * Scale);

            SetRef(window, "_startButton", startButton);
            SetRef(window, "_startText", label);
            SetRef(window, "_pulse", pulse);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadReadyWindow)}.prefab");
        }

        /// <summary>
        /// 사망 — 「부활?」 + 카운트다운 + 두 버튼.
        /// <para>실측 (세로 580 기준): 제목 y 167 · 숫자 y 222(높이 69) · 「부활」 y 333 · 「아니요」 y 401.</para>
        /// </summary>
        /// <summary>
        /// 무기 해금 [소스 <c>rewardLightningWeapon</c>] — 제목 18 · 부제 24 · 아이콘 배율 4 · 버튼 160×48.
        /// </summary>
        private static void BuildWeaponUnlock()
        {
            var root = new GameObject(nameof(UndeadWeaponUnlockWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            Dimmer(root.transform, 0.75f);   // 화면 전체 검은 반투명 [소스 alpha .75]

            var window = root.AddComponent<UndeadWeaponUnlockWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Popup);

            GameObject panel = Sprite9(root.transform, "Panel", "task_bg");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(440f * Scale, 300f * Scale);
            panelRect.anchoredPosition = new Vector2(0f, -140f * Scale);

            TMP_Text title = Text(root.transform, "Title", "과제 보상", 18f, new Color(0.62f, 0.84f, 0.78f));
            Fit(title, 927.9f, 24f);   // 해금 제목 — [소스 Mh(title, .9 × 화면폭, 24)]
            CenterAt(title.rectTransform, 188f, 18f);

            TMP_Text subtitle = Text(root.transform, "Subtitle", "새 무기: 번개", 24f, Color.white);
            Fit(subtitle, 927.9f, 38f);   // 해금 부제 — [소스 Mh(subtitle, .9 × 화면폭, 38)]
            CenterAt(subtitle.rectTransform, 222f, 24f);

            GameObject icon = SpriteImage(root.transform, "Icon", "icon_lightning");
            PlaceCenter(icon, 515.5f, 300f, 48f * 4f, 48f * 4f);

            GameObject ok = Sprite9(root.transform, "Ok", "reward_button_bg");
            RectTransform okRect = ok.GetComponent<RectTransform>();
            okRect.anchorMin = new Vector2(0.5f, 1f);
            okRect.anchorMax = new Vector2(0.5f, 1f);
            okRect.pivot = new Vector2(0.5f, 0.5f);
            okRect.sizeDelta = new Vector2(160f * Scale, 48f * Scale);
            okRect.anchoredPosition = new Vector2(0f, -390f * Scale);

            ok.GetComponent<Image>().color = new Color32(0x2E, 0x7D, 0x32, 0xFF);   // [소스 drawButton(3046706, 10217378)]
            Button okButton = MakeButton(ok);
            TMP_Text okText = Text(ok.transform, "Label", "확인", 18f, Color.white);
            Fit(okText, 198f, 24f);   // 액션 버튼 글자 — [소스 updateButtonText — Mh(text, 198, 24)]
            Stretch(okText.rectTransform);

            SetRef(window, "_titleText", title);
            SetRef(window, "_subtitleText", subtitle);
            SetRef(window, "_okButton", okButton);
            SetRef(window, "_okText", okText);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadWeaponUnlockWindow)}.prefab");
        }

        /// <summary>
        /// 과제 완료 — 「한 판의 끝」 [소스 <c>taskCompletionPrompt</c> · 데스크톱 레이아웃].
        /// <para>디머 .78 · 판 <c>task_bg</c> 폭 min(540, 화면−40) 높이 292 가운데 · 제목 30 bold 금색 (판 y+52) ·
        /// 문구 21 흰색 (판 y+124 · 줄 30) · 버튼 둘 y+204 높이 60 — 「로비로」 <c>#8A3F32</c>/<c>#E69A85</c> · 「계속」 <c>#2E7D32</c>/<c>#9BE7A2</c>.</para>
        /// </summary>
        // ══════════════════════════════ 로비 HUD [소스 Vu · Tu · Ad · ip · wu]

        private const float TaskPanelW = 280f;      // [소스 panel.width = 280]
        private const float TaskBarW = 192f;        // [소스 roundRect(−96, r, 192, 10, 5)]
        private const float TaskBarH = 10f;
        private const float PortalPanelW = 196f;    // [소스 Su = 196]
        private const float PortalPanelH = 64f;

        /// <summary>
        /// 로비 위에 얹히는 것들을 굽는다 — <b>과제 말풍선 둘 · 포털 말풍선 둘 · 수령 링 · 보상 팝업 · 페이드</b>.
        ///
        /// <para>
        /// ⚠ 자리는 <b>런타임이 매 프레임</b> 놓는다 (주인을 따라다닌다) — 여기서는 «부품과 크기»만 만든다.
        /// </para>
        /// </summary>
        private static void BuildLobbyHud()
        {
            var root = new GameObject(nameof(UndeadLobbyHudWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());

            var window = root.AddComponent<UndeadLobbyHudWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Normal);

            const int slots = 2;
            var taskRoots = new UnityEngine.Object[slots];
            var taskTitles = new UnityEngine.Object[slots];
            var taskProgress = new UnityEngine.Object[slots];
            var taskBars = new UnityEngine.Object[slots];
            var rewardTitles = new UnityEngine.Object[slots];
            var rewardIcons = new UnityEngine.Object[slots];
            var rewardNames = new UnityEngine.Object[slots];
            var portalRoots = new UnityEngine.Object[slots];
            var portalNames = new UnityEngine.Object[slots];
            var portalReqs = new UnityEngine.Object[slots];
            var portalProgress = new UnityEngine.Object[slots];

            for (int i = 0; i < slots; i++)
            {
                // ── 과제 말풍선 [소스 Vu] — 패널 폭 280 · 주인 위에 선다
                var bubble = new GameObject("TaskBubble" + i, typeof(RectTransform));
                bubble.transform.SetParent(root.transform, false);
                RectTransform bubbleRect = bubble.GetComponent<RectTransform>();
                bubbleRect.anchorMin = new Vector2(0.5f, 0.5f);
                bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
                bubbleRect.pivot = new Vector2(0.5f, 0f);
                bubbleRect.sizeDelta = new Vector2(TaskPanelW * Scale, 190f * Scale);

                GameObject panel = Sprite9(bubble.transform, "Panel", "task_bg");
                Stretch(panel.GetComponent<RectTransform>());

                TMP_Text title = Text(bubble.transform, "Title", string.Empty, 16f, new Color32(0xFF, 0xD3, 0x6A, 0xFF));
                TopAt(title.rectTransform, 15f, 260f, 56f);
                title.fontStyle = FontStyles.Bold;

                TMP_Text progress = Text(bubble.transform, "Progress", string.Empty, 14f, new Color32(0xDF, 0xF4, 0xFF, 0xFF));
                TopAt(progress.rectTransform, 86f, 260f, 20f);
                progress.fontStyle = FontStyles.Bold;

                GameObject track = SpriteImage(bubble.transform, "BarTrack", "task_bg");
                TopAt(track.GetComponent<RectTransform>(), 116f, TaskBarW, TaskBarH);
                track.GetComponent<Image>().color = new Color32(0x08, 0x0D, 0x16, 0xE6);

                GameObject fill = SpriteImage(track.transform, "BarFill", "task_bg");
                RectTransform fillRect = fill.GetComponent<RectTransform>();
                fillRect.anchorMin = new Vector2(0f, 0f);
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.offsetMin = Vector2.zero;
                fillRect.offsetMax = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
                fill.GetComponent<Image>().color = new Color32(0x62, 0xB8, 0xE8, 0xFF);

                TMP_Text rewardTitle = Text(bubble.transform, "RewardTitle", string.Empty, 16f, Color.white);
                TopAt(rewardTitle.rectTransform, 140f, 260f, 20f);
                rewardTitle.fontStyle = FontStyles.Bold;

                GameObject icon = SpriteImage(bubble.transform, "RewardIcon", "skill_dash");
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 1f);
                iconRect.anchorMax = new Vector2(0.5f, 1f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(68f * Scale, 68f * Scale);
                iconRect.anchoredPosition = new Vector2(-100f * Scale, -168f * Scale);
                icon.GetComponent<Image>().preserveAspect = true;

                TMP_Text rewardName = Text(bubble.transform, "RewardName", string.Empty, 14f, new Color32(0xBF, 0xF7, 0xC7, 0xFF));
                RectTransform nameRect = rewardName.rectTransform;
                nameRect.anchorMin = new Vector2(0.5f, 1f);
                nameRect.anchorMax = new Vector2(0.5f, 1f);
                nameRect.pivot = new Vector2(0f, 0.5f);
                nameRect.sizeDelta = new Vector2(180f * Scale, 48f * Scale);
                nameRect.anchoredPosition = new Vector2(-56f * Scale, -168f * Scale);
                rewardName.alignment = TextAlignmentOptions.Left;
                rewardName.fontStyle = FontStyles.Bold;

                taskRoots[i] = bubbleRect;
                taskTitles[i] = title;
                taskProgress[i] = progress;
                taskBars[i] = fillRect;
                rewardTitles[i] = rewardTitle;
                rewardIcons[i] = icon.GetComponent<Image>();
                rewardNames[i] = rewardName;

                // ── 포털 말풍선 [소스 Tu] — skill_bg 196×64
                var portal = new GameObject("PortalBubble" + i, typeof(RectTransform));
                portal.transform.SetParent(root.transform, false);
                RectTransform portalRect = portal.GetComponent<RectTransform>();
                portalRect.anchorMin = new Vector2(0.5f, 0.5f);
                portalRect.anchorMax = new Vector2(0.5f, 0.5f);
                portalRect.pivot = new Vector2(0.5f, 0f);
                portalRect.sizeDelta = new Vector2(PortalPanelW * Scale, PortalPanelH * Scale);

                GameObject portalPanel = Sprite9(portal.transform, "Panel", "skill_bg");
                Stretch(portalPanel.GetComponent<RectTransform>());

                TMP_Text portalName = Text(portal.transform, "Name", string.Empty, 17f, new Color32(0xDF, 0xF4, 0xFF, 0xFF));
                TopAt(portalName.rectTransform, 14f, 176f, 20f);
                portalName.fontStyle = FontStyles.Bold;

                TMP_Text portalReq = Text(portal.transform, "Requirement", string.Empty, 12f, Color.white);
                TopAt(portalReq.rectTransform, 34f, 176f, 18f);

                TMP_Text portalProg = Text(portal.transform, "Progress", string.Empty, 14f, new Color32(0xFF, 0xD3, 0x6A, 0xFF));
                TopAt(portalProg.rectTransform, 53f, 176f, 18f);
                portalProg.fontStyle = FontStyles.Bold;

                portalRoots[i] = portalRect;
                portalNames[i] = portalName;
                portalReqs[i] = portalReq;
                portalProgress[i] = portalProg;
            }

            // ── 수령 링 [소스 Ad — 히어로 기준 (0,75) · 배율 1.8]
            GameObject ring = SpriteImage(root.transform, "ClaimRing", "quest_progress");
            var ringRect = ring.GetComponent<RectTransform>();
            ringRect.anchorMin = new Vector2(0.5f, 0.5f);
            ringRect.anchorMax = new Vector2(0.5f, 0.5f);
            ringRect.pivot = new Vector2(0.5f, 1f);
            ringRect.sizeDelta = new Vector2(24f * 1.8f * Scale, 24f * 1.8f * Scale);

            var ringImage = ring.GetComponent<Image>();
            ringImage.type = Image.Type.Filled;
            ringImage.fillMethod = Image.FillMethod.Radial360;
            ringImage.fillOrigin = (int)Image.Origin360.Top;
            ringImage.enabled = false;
            SetRef(window, "_claimRing", ringImage);

            BuildRewardPopup(root.transform, window);

            // ── 진입 페이드 [소스 wu — 검정 · 1초]
            GameObject fade = SpriteImage(root.transform, "Fade", "skill_bg");
            Stretch(fade.GetComponent<RectTransform>());
            var fadeImage = fade.GetComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.enabled = false;
            SetRef(window, "_fade", fadeImage);

            SetRefArray(window, "_taskRoots", taskRoots);
            SetRefArray(window, "_taskTitles", taskTitles);
            SetRefArray(window, "_taskProgress", taskProgress);
            SetRefArray(window, "_taskBarFills", taskBars);
            SetRefArray(window, "_taskRewardTitles", rewardTitles);
            SetRefArray(window, "_taskRewardIcons", rewardIcons);
            SetRefArray(window, "_taskRewardNames", rewardNames);
            SetRefArray(window, "_portalRoots", portalRoots);
            SetRefArray(window, "_portalNames", portalNames);
            SetRefArray(window, "_portalRequirements", portalReqs);
            SetRefArray(window, "_portalProgress", portalProgress);
            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadLobbyHudWindow)}.prefab");
        }

        /// <summary>말풍선 안에서 «위에서 몇 px» 자리에 상자를 놓는다 — 원본 좌표를 그대로 쓴다.</summary>
        private static void TopAt(RectTransform rect, float fromTop, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width * Scale, height * Scale);
            rect.anchoredPosition = new Vector2(0f, -fromTop * Scale);
        }

        /// <summary>
        /// 보상 팝업 [소스 <c>skillRewardPopup</c>] — 디머 · 패널 · 제목 <c>taskReward</c> 25 ·
        /// 스킬 이름 30 · 아이콘 ×3 · 확인 버튼.
        /// </summary>
        private static void BuildRewardPopup(Transform parent, Component window)
        {
            var popup = new GameObject("RewardPopup", typeof(RectTransform));
            popup.transform.SetParent(parent, false);
            RectTransform popupRect = popup.GetComponent<RectTransform>();
            Stretch(popupRect);
            Dimmer(popup.transform, 0.78f);

            const float panelW = 460f;
            const float panelH = 300f;

            GameObject panel = Sprite9(popup.transform, "Panel", "task_bg");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            PlaceFromCenter(panel, panelW, panelH);
            panelRect.sizeDelta = new Vector2(panelW * Scale, panelH * Scale);

            TMP_Text title = Text(panel.transform, "Title",
                                  GameRoot.Instance.UndeadTextDataContainer != null ? "" : "", 25f,
                                  new Color32(0xFF, 0xD3, 0x6A, 0xFF));
            TopAt(title.rectTransform, 28f, panelW - 40f, 32f);
            title.fontStyle = FontStyles.Bold;
            title.text = LoadTextTable() != null ? LoadTextTable().Ko("taskReward") : "";

            TMP_Text skillName = Text(panel.transform, "SkillName", string.Empty, 30f, Color.white);
            TopAt(skillName.rectTransform, 76f, panelW - 40f, 40f);
            skillName.fontStyle = FontStyles.Bold;

            GameObject icon = SpriteImage(panel.transform, "Icon", "skill_dash");
            RectTransform iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 1f);
            iconRect.anchorMax = new Vector2(0.5f, 1f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            // ⚠ 원본은 아이콘을 «배율 3» 으로 키운다 — 상자에 맞추는 것이 아니다
            iconRect.sizeDelta = new Vector2(64f * 3f * Scale, 46f * 3f * Scale);
            iconRect.anchoredPosition = new Vector2(0f, -190f * Scale);
            icon.GetComponent<Image>().preserveAspect = true;

            GameObject confirm = Sprite9(panel.transform, "Confirm", "btn_shadowed");
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(0.5f, 1f);
            confirmRect.anchorMax = new Vector2(0.5f, 1f);
            confirmRect.pivot = new Vector2(0.5f, 1f);
            confirmRect.sizeDelta = new Vector2(200f * Scale, 60f * Scale);
            confirmRect.anchoredPosition = new Vector2(0f, -228f * Scale);

            Button confirmButton = MakeButton(confirm);
            TMP_Text confirmText = Text(confirm.transform, "Label",
                                        LoadTextTable() != null ? LoadTextTable().Ko("ok") : "", 24f, Color.white);
            Stretch(confirmText.rectTransform);
            confirmText.fontStyle = FontStyles.Bold;

            SetRef(window, "_rewardPopup", popupRect);
            SetRef(window, "_rewardSkillName", skillName);
            SetRef(window, "_rewardSkillIcon", icon.GetComponent<Image>());
            SetRef(window, "_rewardConfirm", confirmButton);
            popup.SetActive(false);
        }

        private static void BuildTaskComplete()
        {
            var root = new GameObject(nameof(UndeadTaskCompleteWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            Dimmer(root.transform, 0.78f);   // [소스 dimmer alpha .78]

            var window = root.AddComponent<UndeadTaskCompleteWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Popup);

            const float panelW = 540f;
            const float panelH = 292f;
            float panelTop = Mathf.Max(12f, (OriginHeight - panelH) * 0.5f);   // [소스] max(12, (h−292)/2) = 144
            float half = (panelW - 48f - 16f) * 0.5f;                          // [소스] 버튼 폭 238 / 248

            GameObject panel = Sprite9(root.transform, "Panel", "task_bg");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(panelW * Scale, panelH * Scale);
            panelRect.anchoredPosition = new Vector2(0f, -panelTop * Scale);

            TMP_Text title = Text(root.transform, "Title", "과제 완료!", 30f, new Color32(0xFF, 0xD3, 0x6A, 0xFF));
            title.fontStyle = FontStyles.Bold;
            CenterAt(title.rectTransform, 196f, 30f);

            TMP_Text message = Text(root.transform, "Message", "…", 21f, Color.white);
            CenterAt(message.rectTransform, 268f, 21f);
            message.rectTransform.sizeDelta = new Vector2((panelW - 10f) * Scale, 84f * Scale);
            message.enableWordWrapping = true;

            GameObject temple = Sprite9(root.transform, "Temple", "reward_button_bg");
            PlaceCenter(temple, 515.5f - (half + 16f + half + 10f) * 0.5f + half * 0.5f, panelTop + 204f + 30f, half, 60f);
            temple.GetComponent<Image>().color = new Color32(0x8A, 0x3F, 0x32, 0xFF);
            Button templeBtn = MakeButton(temple);
            TMP_Text templeLabel = Text(temple.transform, "Label", "로비로 돌아가기", 20f, Color.white);
            Fit(templeLabel, 198f, 24f);   // 액션 버튼 글자 — [소스 Mh(text, 198, 24)]
            templeLabel.fontStyle = FontStyles.Bold;
            Stretch(templeLabel.rectTransform);

            GameObject continueButton = Sprite9(root.transform, "Continue", "reward_button_bg");
            PlaceCenter(continueButton, 515.5f + (half + 16f + half + 10f) * 0.5f - (half + 10f) * 0.5f, panelTop + 204f + 30f, half + 10f, 60f);
            continueButton.GetComponent<Image>().color = new Color32(0x2E, 0x7D, 0x32, 0xFF);
            Button continueBtn = MakeButton(continueButton);
            TMP_Text continueLabel = Text(continueButton.transform, "Label", "계속", 20f, Color.white);
            Fit(continueLabel, 198f, 24f);   // 액션 버튼 글자 — [소스 Mh(text, 198, 24)]
            continueLabel.fontStyle = FontStyles.Bold;
            Stretch(continueLabel.rectTransform);

            SetRef(window, "_titleText", title);
            SetRef(window, "_messageText", message);
            SetRef(window, "_continueButton", continueBtn);
            SetRef(window, "_continueText", continueLabel);
            SetRef(window, "_templeButton", templeBtn);
            SetRef(window, "_templeText", templeLabel);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadTaskCompleteWindow)}.prefab");
        }

        private static void BuildRevive()
        {
            var root = new GameObject(nameof(UndeadReviveWindow), typeof(RectTransform), typeof(CanvasGroup));
            Stretch(root.GetComponent<RectTransform>());
            Dimmer(root.transform, 0.78f);   // 화면 전체 검은 반투명 [소스 alpha .78]

            var window = root.AddComponent<UndeadReviveWindow>();
            SetEnum(window, "_windowType", (int)EWindowType.Popup);

            GameObject panel = Sprite9(root.transform, "Panel", "task_bg");
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 1f);
            panelRect.anchorMax = new Vector2(0.5f, 1f);
            // ⚠ 피벗은 <b>위</b>다 — 아래로 잡으면 판이 «위로» 자라 화면 밖으로 나가고
            //   내용(제목 152 · 숫자 190 · 버튼 344/412)과 어긋난 자리에 검은 상자만 남는다.
            // [소스 rewardRevive] 판 440×320 가운데 → 위 130. 제목 y+52 · 숫자 y+126 · 「부활」 y+214 · 「아니요」 y+282.
            //   ⚠ 회차 6 «실측» 152/190/344/412 는 글자 «위 가장자리»를 잰 값이었다 — 같은 채록(learn6.json)의
            //   앵커 중심은 182/256/344/412 로 소스 식과 정확히 같다 (회차 10 정정).
            const float panelH = 320f;
            float panelTop = (OriginHeight - panelH) * 0.5f;
            panelRect.pivot = new Vector2(0.5f, 1f);
            panelRect.sizeDelta = new Vector2(440f * Scale, panelH * Scale);
            panelRect.anchoredPosition = new Vector2(0f, -panelTop * Scale);

            TMP_Text title = Text(root.transform, "Title", "부활?", 28f, Color.white);
            Fit(title, 886.66f, 34f);   // 부활 제목 — [소스 Mh(title, .86 × 화면폭, 34)]
            title.fontStyle = FontStyles.Bold;
            CenterAt(title.rectTransform, 182f, 28f);

            TMP_Text countdown = Text(root.transform, "Countdown", "8", 65f, new Color32(0xFF, 0xD3, 0x6A, 0xFF));   // [소스 fill 16765802 · 65 bold]
            Fit(countdown, 160f, 102f);   // 남은 시간 — [소스 Mh(countdown, 160, 102)]
            countdown.fontStyle = FontStyles.Bold;
            CenterAt(countdown.rectTransform, 256f, 65f);

            GameObject revive = Sprite9(root.transform, "Revive", "reward_button_bg");
            RectTransform reviveRect = revive.GetComponent<RectTransform>();
            reviveRect.anchorMin = new Vector2(0.5f, 1f);
            reviveRect.anchorMax = new Vector2(0.5f, 1f);
            reviveRect.pivot = new Vector2(0.5f, 0.5f);
            reviveRect.sizeDelta = new Vector2(230f * Scale, 58f * Scale);   // [소스 230×58]
            reviveRect.anchoredPosition = new Vector2(0f, -(panelTop + 214f) * Scale);

            revive.GetComponent<Image>().color = new Color32(0xFA, 0xCC, 0x15, 0xFF);   // [소스 tint 16436245]
            Button reviveButton = MakeButton(revive);
            revive.AddComponent<UiHoverScale>().Configure(1.02f);   // [소스 onButtonEnter 1.02]
            TMP_Text reviveText = Text(revive.transform, "Label", "부활", 20f, Color.white);   // [덤프 — font 20]
            Fit(reviveText, 156f, 24f);   // 아이콘 있는 액션 버튼 — [소스 Mh(text, 156, 24)]
            Stretch(reviveText.rectTransform);

            GameObject decline = Sprite9(root.transform, "Decline", "reward_button_bg");   // [덤프 — 원본도 같은 판이고 틴트만 다르다]
            RectTransform declineRect = decline.GetComponent<RectTransform>();
            declineRect.anchorMin = new Vector2(0.5f, 1f);
            declineRect.anchorMax = new Vector2(0.5f, 1f);
            declineRect.pivot = new Vector2(0.5f, 0.5f);
            declineRect.sizeDelta = new Vector2(230f * Scale, 58f * Scale);   // [소스 230×58]
            declineRect.anchoredPosition = new Vector2(0f, -(panelTop + 282f) * Scale);

            decline.GetComponent<Image>().color = new Color32(0xA5, 0x3A, 0x3A, 0xFF);   // [소스 tint 10828346]
            Button declineButton = MakeButton(decline);
            decline.AddComponent<UiHoverScale>().Configure(1.02f);
            TMP_Text declineText = Text(decline.transform, "Label", "아니요", 20f, Color.white);   // [덤프 — font 20]
            Fit(declineText, 198f, 24f);   // 액션 버튼 글자 — [소스 Mh(text, 198, 24)]
            Stretch(declineText.rectTransform);

            SetRef(window, "_titleText", title);
            SetRef(window, "_countdownText", countdown);
            SetRef(window, "_reviveButton", reviveButton);
            SetRef(window, "_reviveText", reviveText);
            SetRef(window, "_declineButton", declineButton);
            SetRef(window, "_declineText", declineText);

            SavePrefab(root, $"{UiPrefabRoot}/{nameof(UndeadReviveWindow)}.prefab");
        }

        /// <summary>카드 안에서 «왼쪽 정렬»로 앉힌다.</summary>
        private static void LeftAt(RectTransform rect, float originX, float originOffsetY, float originFont)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(300f * Scale, originFont * Scale * 1.6f);
            rect.anchoredPosition = new Vector2(originX * Scale, originOffsetY * Scale);

            var text = rect.GetComponent<TMP_Text>();

            if (text != null)
                text.alignment = TextAlignmentOptions.Left;
        }

        // ══════════════════════════════ 유틸

        /// <summary>
        /// 버튼을 만든다 — <b><see cref="Graphic.raycastTarget"/> 을 여기서 «반드시» 켠다.</b>
        ///
        /// <para>
        /// ⚠ <b>[사고]</b> <see cref="SpriteImage"/> 는 <c>raycastTarget = false</c> 로 낸다(장식이 대부분이라서).
        /// 그 위에 <c>Button</c> 만 얹으면 <b>컴포넌트는 다 붙어 있는데 포인터가 통과</b>해 버린다 —
        /// 뒤에 깔린 디머가 대신 맞고, 화면은 «정상으로 보이며», 데이터·프리팹 검사도 전부 통과한다.
        /// 실제로 「선택」·「확인」·「부활」·「아니요」·「로비로」·「계속」 <b>여섯 개가 통째로 안 눌렸다</b>.
        /// 그래서 버튼을 «손으로» 조립하지 않고 이 함수 하나만 지나게 한다.
        /// </para>
        ///
        /// <para>전환(Transition)은 끈다 — 배율·틴트는 <c>UiStateScale</c>·<c>UiHoverScale</c> 이 [소스] 값으로 든다.</para>
        /// </summary>
        /// <summary>
        /// 글자를 <b>상자 안에 줄여 넣는다</b> [소스 <c>Mh(글자, 폭, 높이)</c> — <c>min(w/폭, h/높이, 1)</c>].
        ///
        /// <para>
        /// ⚠ 원본은 «거의 모든» 문구를 이렇게 넣는다. 우리는 크기만 고정으로 넣어서
        /// <b>서체를 갈면 문구가 상자를 넘친다</b> — 「지금 서체로는 마침 맞는다」는 이관이 아니다.
        /// </para>
        ///
        /// <para>상자는 <b>원본 px</b> 로 준다 — 여기서 환산한다. 0 이면 그 축은 안 본다.</para>
        /// </summary>
        private static void Fit(TMP_Text text, float originBoxW, float originBoxH)
        {
            text.gameObject.AddComponent<UiTextFit>().Configure(originBoxW * Scale, originBoxH * Scale);
        }

        private static Button MakeButton(GameObject go)
        {
            var image = go.GetComponent<Image>();

            if (image == null)
            {
                Log.Error($"버튼에 Image 가 없다: {go.name}");
                return null;
            }

            image.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            return button;
        }

        /// <summary>최고 기록 글자 [소스 <c>fontSize 14</c> · <c>stroke width 3</c> · <c>padding 8</c>].</summary>
        private const float BestFont = 14f;

        private const float BestStrokeWidth = 3f;

        private const float BestPadding = 8f;

        /// <summary>
        /// 화면 <b>아래 모서리</b>에 붙인다 — 원본 앵커가 <c>(0,1)</c>/<c>(1,1)</c>(좌·우 하단)이다.
        /// <para>⚠ 가운데 정렬로 두면 문구 길이가 바뀔 때 «자리»가 흔들린다 — 모서리 기준이라 안 흔들린다.</para>
        /// </summary>
        // ══════════════════════════════ 스킬 슬롯 [소스 Wd · Hd]

        private const float SkillSlotSize = 54f;      // [소스 zd = 54]
        private const float SkillSlotStride = 62f;    // [소스 x = 62·i]
        private const float SkillRightMargin = 12f;   // [소스 x = 화면오른쪽 − 12 − 전체폭]
        private const float SkillBottomMargin = 42f;  // [소스 y = 화면아래 − 42 − zd]
        private const float SkillIconOffsetY = 8f;    // [소스 데스크톱이면 icon.y += 8]
        private const float SkillCooldownFont = 20f;  // [소스 fontSize 20 · stroke {0,4}]
        private const float SkillKeyFont = 11f;       // [소스 fontSize 11 · fill #161622]
        private const float SkillBadgeHeight = 16f;
        private const float SkillBadgeWideWidth = 48f;   // [소스 Space 면 48]
        private const float SkillBadgeNarrowWidth = 24f;

        /// <summary>
        /// 슬롯 넷을 굽는다. <b>표가 분모다</b> — 표에 스킬이 넷이면 슬롯도 넷이다.
        ///
        /// <para>
        /// ⚠ 안 가진 스킬은 <b>런타임이 통째로 끈다</b> (칸까지 빠진다). 여기서는 «자리와 부품»만 만든다.
        /// </para>
        /// </summary>
        private static void BuildSkillSlots(Transform parent, Component window)
        {
            var rootObject = new GameObject("SkillRoot", typeof(RectTransform));
            rootObject.transform.SetParent(parent, false);

            var rootRect = rootObject.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0f);
            rootRect.anchorMax = new Vector2(1f, 0f);
            rootRect.pivot = new Vector2(1f, 0f);
            rootRect.sizeDelta = new Vector2(0f, SkillSlotSize * Scale);
            rootRect.anchoredPosition = new Vector2(-SkillRightMargin * Scale, SkillBottomMargin * Scale);

            // ⚠ <b>편집 모드에는 런타임 컨테이너가 없다</b> — JSON 을 직접 읽는다
            //   (다른 빌더와 같은 방식이다. <c>GameRoot</c> 를 쓰면 «조용히 null» 이 된다).
            UndeadSkillDataContainer table = LoadSkillTable();

            if (table == null)
                return;

            IReadOnlyList<UndeadSkillData> all = table.AllValues;
            var slots = new UndeadSkillSlot[all.Count];

            for (int i = 0; i < all.Count; i++)
            {
                UndeadSkillData data = all[i];
                slots[data.Order] = BuildSkillSlot(rootRect, data);
            }

            SetRef(window, "_skillRoot", rootRect);
            SetRefArray(window, "_skillSlots", slots);
        }

        private static UndeadSkillSlot BuildSkillSlot(RectTransform parent, UndeadSkillData data)
        {
            var go = new GameObject($"Skill_{data.Code}", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(SkillSlotSize * Scale, SkillSlotSize * Scale);
            rect.anchoredPosition = new Vector2(SkillSlotStride * data.Order * Scale, 0f);

            // ★ 흐려지는 것은 «아이콘 묶음»뿐이다 — 쿨다운 숫자는 또렷하게 남는다 [소스 item.alpha]
            var itemObject = new GameObject("Item", typeof(RectTransform), typeof(CanvasGroup));
            itemObject.transform.SetParent(go.transform, false);
            var itemRect = itemObject.GetComponent<RectTransform>();
            Stretch(itemRect);

            GameObject background = Sprite9(itemObject.transform, "Bg", "skill_bg");
            Stretch(background.GetComponent<RectTransform>());

            GameObject icon = SpriteImage(itemObject.transform, "Icon", data.IconAddress);
            var iconRect = icon.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(SkillSlotSize * Scale, SkillSlotSize * Scale);

            // ⚠ 원본은 데스크톱에서 아이콘을 «아래로» 8 내린다 — 키 뱃지 자리를 비우려는 것이다
            iconRect.anchoredPosition = new Vector2(0f, -SkillIconOffsetY * Scale);
            icon.GetComponent<Image>().preserveAspect = true;

            // 키 뱃지 — 왼쪽 위 (3,3) 부터 [소스 roundRect(3,3,w,16,3)]
            float badgeWidth = data.DesktopKey == "Space" ? SkillBadgeWideWidth : SkillBadgeNarrowWidth;
            GameObject badge = SpriteImage(itemObject.transform, "KeyBadge", "skill_bg");
            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0f, 1f);
            badgeRect.anchorMax = new Vector2(0f, 1f);
            badgeRect.pivot = new Vector2(0f, 1f);
            badgeRect.sizeDelta = new Vector2(badgeWidth * Scale, SkillBadgeHeight * Scale);
            badgeRect.anchoredPosition = new Vector2(3f * Scale, -3f * Scale);
            badge.GetComponent<Image>().color = new Color32(0xF5, 0xE8, 0xC8, 0xF2);

            TMP_Text keyText = Text(badge.transform, "KeyText", KeyLabel(data.DesktopKey), SkillKeyFont,
                                    new Color32(0x16, 0x16, 0x22, 0xFF));
            Stretch(keyText.rectTransform);
            keyText.fontStyle = FontStyles.Bold;
            Fit(keyText, (badgeWidth - 4f) * Scale, 12f * Scale);

            // 쿨다운 숫자는 «묶음 밖»이다 — 흐려지면 안 읽힌다
            TMP_Text cooldown = Text(go.transform, "Cooldown", string.Empty, SkillCooldownFont, Color.white);
            Stretch(cooldown.rectTransform);
            cooldown.fontStyle = FontStyles.Bold;
            Outline(cooldown, SkillCooldownFont, 4f, Color.black);

            var slot = go.AddComponent<UndeadSkillSlot>();
            SetRef(slot, "_item", itemObject.GetComponent<CanvasGroup>());
            SetRef(slot, "_background", background.GetComponent<Image>());
            SetRef(slot, "_icon", icon.GetComponent<Image>());
            SetRef(slot, "_cooldownText", cooldown);
            SetRef(slot, "_keyBadge", badge.GetComponent<Image>());
            SetRef(slot, "_keyText", keyText);
            SetEnumValue(slot, "_skill", data.Order);
            return slot;
        }

        /// <summary>편집 모드에서 표를 읽는다 — 런타임 컨테이너가 없기 때문이다.</summary>
        private static UndeadSkillDataContainer LoadSkillTable()
        {
            var table = new UndeadSkillDataContainer();
            string path = Path.Combine(DataFolder, table.Name + ".json");

            if (File.Exists(path) == false)
            {
                Log.Error($"스킬 표가 없다: {path}");
                return null;
            }

            table.LoadJson(File.ReadAllText(path));

            if (table.Validate(out string error))
                return table;

            Log.Error("스킬 표가 검사를 통과하지 못했다 — 슬롯을 안 굽는다: " + error);
            return null;
        }

        private static UndeadTextDataContainer LoadTextTable()
        {
            var table = new UndeadTextDataContainer();
            string path = Path.Combine(DataFolder, table.Name + ".json");

            if (File.Exists(path) == false)
            {
                Log.Error($"문구 표가 없다: {path}");
                return null;
            }

            table.LoadJson(File.ReadAllText(path));
            return table;
        }

        /// <summary>화면에 찍히는 키 이름 [소스 — <c>Space</c> 는 문구 표, 나머지는 <c>Key</c> 접두어를 뗀다].</summary>
        private static string KeyLabel(string desktopKey)
        {
            if (desktopKey != "Space")
                return desktopKey;

            UndeadTextDataContainer texts = LoadTextTable();
            return texts != null ? texts.Ko("keyboardSpace") : "Space";
        }

        private static void CornerAt(RectTransform rect, bool left)
        {
            float ax = left ? 0f : 1f;
            rect.anchorMin = new Vector2(ax, 0f);
            rect.anchorMax = new Vector2(ax, 0f);
            rect.pivot = new Vector2(ax, 0f);
            rect.sizeDelta = new Vector2(400f * Scale, BestFont * 1.6f * Scale);
            rect.anchoredPosition = new Vector2((left ? BestPadding : -BestPadding) * Scale, BestPadding * Scale);

            var text = rect.GetComponent<TMP_Text>();

            if (text != null)
                text.alignment = left ? TextAlignmentOptions.BottomLeft : TextAlignmentOptions.BottomRight;
        }

        /// <summary>
        /// 글자 테두리 — 원본 <c>stroke.width</c> 는 «픽셀»이고 TMP 는 «글자 크기 대비 0~1» 이다.
        /// <para>⚠ 그대로 넣으면 테두리가 글자를 덮는다 — 글자 크기로 나눈다.</para>
        /// </summary>
        private static void Outline(TMP_Text text, Color color, float strokePixels, float originFont)
        {
            text.fontStyle |= FontStyles.Bold;
            text.fontSharedMaterial = OutlineMaterial(color, Mathf.Clamp01(strokePixels / Mathf.Max(1f, originFont)));
        }

        /// <summary>
        /// 테두리가 걸린 <b>재질 «에셋»</b>을 만들어(또는 재사용해) 돌려준다.
        ///
        /// <para>
        /// ⚠⚠ <b>[사고] 테두리가 굽는 순간 사라졌다.</b> <c>text.outlineWidth</c>·<c>outlineColor</c> 는
        /// <c>fontMaterial</c>(«인스턴스 재질»)에 쓰인다. 인스턴스 재질은 <b>어느 에셋에도 안 붙어 있어
        /// 프리팹을 저장하면 통째로 버려진다</b> — 실측으로 구운 프리팹의 <c>m_fontMaterial</c> 이
        /// 전부 <c>fileID: 0</c> 이었고, 재생에서 <b>모든 문구의 <c>_OutlineWidth</c> 가 0</b> 이었다.
        /// 원본은 거의 모든 문구에 <c>stroke</c> 를 건다 — 테두리가 없으면 세계 위의 글자가 안 읽힌다.
        /// </para>
        ///
        /// <para>
        /// ⇒ <b>재질을 «에셋»으로 저장하고 <c>fontSharedMaterial</c> 에 물린다.</b>
        /// 같은 (색·두께)는 한 벌만 만든다 — 문구마다 새로 만들면 배치가 깨진다.
        /// </para>
        /// </summary>
        private static Material OutlineMaterial(Color color, float width)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (font == null)
            {
                Log.Error($"폰트 에셋이 없다: {FontAssetPath} — 먼저 UndeadFontSetup.Setup 을 돌린다");
                return null;
            }

            var key = new Color32(color.r > 0.5f ? (byte)1 : (byte)0, 0, 0, 0);
            string name = $"UndeadSlayer Outline {ColorUtility.ToHtmlStringRGB(color)} {width:0.000}";
            string path = $"{OutlineMaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (existing != null)
                return existing;

            Directory.CreateDirectory(OutlineMaterialFolder);

            var material = new Material(font.material) { name = name };
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, color);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 테두리 재질이 사는 곳 — <b><c>Resources/</c> 다.</b>
        /// <para>프리팹이 참조하므로 어차피 빌드에 들어간다. 폰트 에셋 옆에 둔다.</para>
        /// </summary>
        private const string OutlineMaterialFolder = "Assets/Undead-Slayer/Resources/Font";

        private static GameObject SpriteImage(Transform parent, string name, string art)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.sprite = LoadUiSprite(art);
            image.raycastTarget = false;
            return go;
        }

        private static GameObject Sprite9(Transform parent, string name, string art)
        {
            GameObject go = SpriteImage(parent, name, art);
            var image = go.GetComponent<Image>();

            // ★ 원본이 작은 타일을 크게 늘려 쓴다 [실측] — 9-slice 다.
            image.type = Image.Type.Sliced;

            // ★★★ <b>테두리도 화면 배율을 타야 한다.</b>
            //   엔진은 9슬라이스 «모서리»를 «스프라이트 px» 그대로 그린다 —
            //   캔버스 referencePixelsPerUnit(100) 과 스프라이트 PPU(100) 가 같으면 <b>1 px = 1 캔버스 px</b> 다.
            //   그런데 우리 캔버스는 원본 세로 580 을 1080 으로 «키운다»(배율 Scale).
            //   ⇒ 가운데는 배율만큼 늘어나는데 <b>모서리만 원래 크기로 남아</b>
            //     테두리가 원본의 1/Scale 두께가 되고 모서리 곡률이 조여 보인다.
            //
            //   ⚠ [사고] 「인셋 값이 소스와 같다」까지만 보고 통과시켰다 —
            //     인셋은 «스프라이트 px» 단위라 값이 맞아도 <b>그려지는 두께는 틀릴 수 있다</b>.
            //     사람이 화면을 보고 「테두리 띠가 왜 이러냐」고 알려 줬다.
            //
            //   배수는 «나누는 값»이다 — 그려지는 두께 = 인셋 ÷ 배수. 그래서 1/Scale 을 넣는다.
            image.pixelsPerUnitMultiplier = 1f / Scale;
            return go;
        }

        private static Sprite LoadUiSprite(string art)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiArtRoot}/{art}.png");

            if (sprite == null)
                Log.Error($"UI 스프라이트가 없다: {UiArtRoot}/{art}.png — 먼저 UndeadSpriteBuilder 를 돌린다");

            return sprite;
        }

        private static TMP_Text Text(Transform parent, string name, string value, float originFont, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<TextMeshProUGUI>();

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

            if (font == null)
                Log.Error($"폰트 에셋이 없다: {FontAssetPath} — 먼저 UndeadFontSetup.Setup 을 돌린다");
            else
                text.font = font;

            text.text = value;
            text.fontSize = originFont * Scale;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.enableWordWrapping = false;

            return text;
        }

        /// <summary>
        /// 원본 <c>style.stroke</c> 를 옮긴다 — <b>테두리가 있는 글자와 없는 글자가 섞여 있다</b> [소스].
        /// <para>TMP 외곽선은 «글자 크기 대비 0~1» 이라 원본 px 두께를 그 비율로 바꾼다.</para>
        /// </summary>
        private static void Outline(TMP_Text text, float originFont, float originStrokeWidth, Color strokeColor)
        {
            // ⚠ 인스턴스 재질(`fontMaterial`)에 쓰면 프리팹 저장 때 버려진다 — <see cref="OutlineMaterial"/> 참고
            text.fontSharedMaterial = OutlineMaterial(strokeColor, Mathf.Clamp01(originStrokeWidth / Mathf.Max(1f, originFont)));
        }

        /// <summary>원본 좌상단 기준 좌표를 «위쪽 앵커»로 옮긴다.</summary>
        private static void Place(GameObject go, float originX, float originY, float w, float h, bool anchorRight)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(anchorRight ? 1f : 0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = new Vector2(anchorRight ? 1f : 0f, 1f);
            rect.sizeDelta = new Vector2(w * Scale, h * Scale);
            rect.anchoredPosition = new Vector2((anchorRight ? -originX : originX) * Scale, -originY * Scale);
        }

        /// <summary>
        /// 원본 «중심» 좌표에 놓는다 — <b>피벗이 가운데</b>라 회전이 제자리에서 돈다.
        /// <para>⚠ 회전하는 것에 <see cref="Place"/>(피벗 좌상단)를 쓰면 화면 밖으로 휘둘린다.</para>
        /// </summary>
        /// <summary>화면 «가운데» 앵커 · 중심 피벗 — 자리는 런타임이 놓는 요소용.</summary>
        private static void PlaceFromCenter(GameObject go, float w, float h)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w * Scale, h * Scale);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void PlaceCenter(GameObject go, float originCenterX, float originCenterY, float w, float h)
        {
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w * Scale, h * Scale);
            rect.anchoredPosition = new Vector2(originCenterX * Scale, -originCenterY * Scale);
        }

        /// <summary>
        /// 글자를 <b>원본 «글리프 중심»</b>에 놓는다 [사건 프레임 덤프의 <c>cy</c> 가 그 값이다].
        ///
        /// <para>
        /// ⚠ 예전 <c>CenterTop</c> 은 «글자 위쪽»을 받아 상자를 매달았고, TMP 가 상자 안에서 다시 가운데를 잡아
        /// 글자가 <b>글꼴 크기의 0.3배만큼 아래</b>로 내려갔다 (12px 글자에서 3.6px). 값이 아니라 <b>재는 자리</b>가 달랐던 것이다.
        /// </para>
        /// </summary>
        private static void CenterAt(RectTransform rect, float originCenterY, float originFont, float originCenterXOffset = 0f)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(400f, originFont * Scale * 1.6f);
            rect.anchoredPosition = new Vector2(originCenterXOffset * Scale, -originCenterY * Scale);
        }

        /// <summary>
        /// 화면 전체를 덮는 검은 반투명 — 원본 창마다 «자기 알파»가 있다 [소스 — 시작 .5 · 레벨업 .7 · 해금 .75 · 부활 .78].
        /// 맨 아래 형제로 넣어 다른 요소가 그 위에 그려진다. 레이캐스트를 먹어 뒤 화면 클릭을 막는다.
        /// </summary>
        private static void Dimmer(Transform parent, float alpha)
        {
            var go = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            Stretch(go.GetComponent<RectTransform>());

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, alpha);
            image.raycastTarget = true;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (Directory.Exists(path) == false)
                Directory.CreateDirectory(path);

            AssetDatabase.Refresh();
        }

        private static void SavePrefab(GameObject root, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);

            if (success == false)
                Log.Error($"프리팹 저장 실패: {path}");

            Object.DestroyImmediate(root);
        }

        /// <summary>⚠ 「썼다」가 아니라 «됐다»로 확인한다 — 조용히 안 들어가면 화면만 빈다.</summary>
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

        /// <summary>배열 필드 주입. ⚠ 「썼다」가 아니라 «됐다»로 확인한다.</summary>
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

            var verify = new SerializedObject(target);
            SerializedProperty check = verify.FindProperty(field);

            if (check.arraySize != values.Length)
                Log.Error($"{target.GetType().Name}.{field} 배열이 안 들어갔다 ({check.arraySize} / {values.Length})");
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// enum 필드에 <b>«값»을 넣는다</b> — <see cref="SetEnum"/> 과 다르다.
        ///
        /// <para>
        /// ⚠⚠ <b>[사고]</b> <c>SerializedProperty.enumValueIndex</c> 는 «값»이 아니라
        /// <b>«enum 목록에서 몇 번째인가»</b>다. 첫 항목이 <c>0</c> 이 아닌 enum
        /// (예: <c>None = −1</c> 을 앞에 둔 것)에 쓰면 <b>전부 한 칸씩 밀린다</b> —
        /// 실측으로 슬롯 넷이 <c>None · Dash · BlazingTrail · WinterPulse</c> 로 구워졌다.
        /// 값이 «있긴 해서» 오류도 안 났다.
        /// </para>
        ///
        /// <para>⇒ 밑값을 쓰려면 <c>intValue</c> 다. 그리고 <b>되읽어 확인한다</b>.</para>
        /// </summary>
        private static void SetEnumValue(Object target, string field, int value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(field);

            if (property == null)
            {
                Log.Error($"{target.GetType().Name} 에 {field} 이 없다");
                return;
            }

            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            int written = new SerializedObject(target).FindProperty(field).intValue;

            if (written != value)
                Log.Error($"{target.GetType().Name}.{field} 에 {value} 를 넣었는데 {written} 이 들어갔다");
        }
    }
}
