using System.IO;
using JinHyung.Core;
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
            fillRect.offsetMin = new Vector2(1f * Scale, 1f * Scale);
            fillRect.offsetMax = new Vector2(0f, -1f * Scale);

            var fillImage = fill.GetComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            // ── 좌상단 보석 아이콘
            GameObject gem = SpriteImage(root.transform, "GemIcon", "gem_icon");
            Place(gem, GemIconX, GemIconY, GemIconSize, GemIconSize, anchorRight: false);

            // ── 우상단 레벨 배경
            GameObject levelBg = Sprite9(root.transform, "LevelBg", "lvl_bg");
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

            SetRef(window, "_gaugeFill", fillImage);
            SetRef(window, "_gaugeText", gaugeText);
            SetRef(window, "_timerText", timerText);
            SetRef(window, "_levelText", levelText);
            SetRef(window, "_questArrow", arrow.GetComponent<RectTransform>());
            SetRef(window, "_questDistanceText", questText);

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
                GameObject card = Sprite9(root.transform, $"Card{i}", "reward_button_bg");
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
            // ★ 원본 시작 화면은 HUD(게이지·타이머·레벨)가 디머 «위»에 보인다 [원본 캡처] — HUD 와 같은 대역에 두고 HUD 를 뒤에 연다
            SetEnum(window, "_windowType", (int)EWindowType.Normal);

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
            CenterAt(title.rectTransform, 188f, 18f);

            TMP_Text subtitle = Text(root.transform, "Subtitle", "새 무기: 번개", 24f, Color.white);
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
            templeLabel.fontStyle = FontStyles.Bold;
            Stretch(templeLabel.rectTransform);

            GameObject continueButton = Sprite9(root.transform, "Continue", "reward_button_bg");
            PlaceCenter(continueButton, 515.5f + (half + 16f + half + 10f) * 0.5f - (half + 10f) * 0.5f, panelTop + 204f + 30f, half + 10f, 60f);
            continueButton.GetComponent<Image>().color = new Color32(0x2E, 0x7D, 0x32, 0xFF);
            Button continueBtn = MakeButton(continueButton);
            TMP_Text continueLabel = Text(continueButton.transform, "Label", "계속", 20f, Color.white);
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
            title.fontStyle = FontStyles.Bold;
            CenterAt(title.rectTransform, 182f, 28f);

            TMP_Text countdown = Text(root.transform, "Countdown", "8", 65f, new Color32(0xFF, 0xD3, 0x6A, 0xFF));   // [소스 fill 16765802 · 65 bold]
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

            // ★ 원본이 6×6 · 16×16 짜리 작은 타일을 크게 늘려 쓴다 [실측] — 9-slice 다.
            image.type = Image.Type.Sliced;
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
            text.fontMaterial = new Material(text.fontMaterial);   // 공유 재질을 건드리면 «모든» 글자에 번진다
            text.fontMaterial.EnableKeyword("OUTLINE_ON");
            text.outlineColor = strokeColor;
            text.outlineWidth = Mathf.Clamp01(originStrokeWidth / Mathf.Max(1f, originFont));
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
    }
}
