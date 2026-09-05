using System;
using System.IO;
using System.Text.RegularExpressions;
using JinHyung.BlumgiBounce;
using JinHyung.Core;
using JinHyung.Extensions;
using UnityEditor;
using UnityEngine;

namespace JinHyung.EditorTools
{
    /// <summary>
    /// Blumgi Bounce 의 아트를 <b>전부 새로 굽는다</b>.
    ///
    /// <para>
    /// ⚠ <b>원본 에셋은 상용(Blumgi / Poki)이라 추출이 금지</b>다 (확정표 7 · <c>06_리소스.md</c> ①).
    /// 그래서 「존재하는 리소스」가 원리적으로 0개이고, 이 파일이 리소스 목록의 전부다.
    /// </para>
    ///
    /// <para>
    /// ★ <b>확정 F — 흰색으로 굽고 색은 데이터로 준다.</b> 원본이 <c>FColorisable*</c> 패밀리 5종 +
    /// <c>ColorPicker</c> 151프레임으로 <b>런타임 tint</b> 를 하는 구조라, 레벨×색 조합으로 굽지 않는다.
    /// 다만 <b>테마를 안 따라가는 것</b>(블롭 주황 · 공 하늘색 · 골대 림 적/그늘)은 색을 굽는다
    /// (<c>06_리소스.md</c> §C 「테마 색은 그림에 굽지 않는다」 표의 왼쪽 칸).
    /// </para>
    ///
    /// <para>
    /// ★ <b>확정 E — world 1:1 · PPU 1.</b> 그래서 <b>텍스처 픽셀 = 원본 world px</b> 다.
    /// 크기는 <c>04_UIUX규칙.md</c> 2-g 「스프라이트 픽셀 크기 전수」의 <b>소스 텍스처 px</b> 를
    /// <b>4의 배수로 올림</b>한 값이다 (확정표 7 은 «텍스처 크기» 규칙이다 — 표시 크기는 확정 E 소관).
    /// 올림으로 생긴 여백은 <b>투명 패딩</b>이라 그림 자체는 원본 크기 그대로 앉는다.
    /// </para>
    ///
    /// <para>
    /// ⚠ 이것은 <b>「구성하는 도구」</b>다 — 이관이 끝나도 <b>지우지 않는다</b>.
    /// 없으면 「사람이 그림 편집기로」로 돌아간다 (CLAUDE.md 「에디터 도구는 두 갈래다」).
    /// <b>이 게임 전용</b>이라 게임 폴더에 산다. <c>[MenuItem]</c> 은 만들지 않는다 — 배치 실행 전용이다.
    /// </para>
    ///
    /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiSpriteBuilder.BuildAll</c></para>
    /// </summary>
    public static class BlumgiSpriteBuilder
    {
        private const string ArtRoot = "Assets/Blumgi-Bounce/Art";
        private const string GameDir = ArtRoot + "/Game";
        private const string UiDir = ArtRoot + "/UI";

        /// <summary>곡선 계단을 없애는 슈퍼샘플 배율. 원본은 벡터라 계단이 없다.</summary>
        private const int SuperSample = 4;

        /// <summary>
        /// 검은 외곽선 두께 (world).
        /// [실측] 836×470 렌더에서 2~3 css px 이고 요소 크기와 무관하게 거의 일정하다
        /// (<c>06_리소스.md</c> 「공통 스타일」) ⇒ 2.5 css px ÷ 0.3671875 ≈ <b>6.8 world</b>.
        /// 블록 행 스캔에서도 칸 경계의 검은 띠가 5~6 world 로 잡혔다.
        /// </summary>
        private const float Stroke = 6f;

        // ── 색. 전부 «원본 sRGB hex 그대로»다 — Linear 역산을 하지 않는다 (CLAUDE.md 「렌더 규칙」).
        private static readonly Color Black = Hex("000000");
        private static readonly Color White = Hex("FFFFFF");

        /// <summary>블롭 밝은 쪽 [실측 캡처 샘플 (251,201,80)].</summary>
        private static readonly Color BlobLight = Hex("FBC950");

        /// <summary>블롭 진한 쪽 [실측 캡처 샘플 (255,155,50)].</summary>
        private static readonly Color BlobDeep = Hex("FF9B32");

        /// <summary>블롭 하이라이트 [실측 (252,250,142) 근방].</summary>
        private static readonly Color BlobHighlight = Hex("FFF08E");

        /// <summary>공 윗부분 [실측 (102,192,255)].</summary>
        private static readonly Color BallTop = Hex("66C0FF");

        /// <summary>공 아랫부분 [실측 (99,153,246)].</summary>
        private static readonly Color BallBottom = Hex("6399F6");

        /// <summary>공 하이라이트 [실측 (103,242,246)].</summary>
        private static readonly Color BallHighlight = Hex("67F2F6");

        /// <summary>골대 림 밝은쪽 [05_연출 §1 · 전 레벨 동일].</summary>
        private static readonly Color RimLight = Hex("FF3333");

        /// <summary>골대 림 그늘쪽 [05_연출 §1 · 전 레벨 동일].</summary>
        private static readonly Color RimShade = Hex("CC3366");

        /// <summary>블록 무늬 둘레의 연회색 내부선 [06_리소스 B-1 #5].</summary>
        private static readonly Color BlockInnerLine = Hex("E4E4E4");

        // ══════════════════════════════════════════════════════════════════════
        // ★★★ 블록 스킨 5프레임 색 — 17회차가 «소스 텍스처 픽셀»을 그대로 꺼내 잰 값이다
        //   (`gap17/pix/spr_BlockSimpleSkin__Animation_1__{0..4}.png` · ExtractImageToCanvas).
        //
        //   ⚠ 블록은 «틴트가 아니다» — 17회차가 5레벨 전 인스턴스의
        //     GetUnpremultipliedColor() 를 (255,255,255) 로 실측했다. 색은 «프레임에 구워져 있다».
        //     그래서 여기서만 색을 굽고, 뷰는 «프레임 번호»만 고른다.
        //
        //   레벨↔프레임 [실측 462 인스턴스 · 예외 0건] — L1→0 · L2→3 · L3→1 · L4→2 · L5→4.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>좌측 캡 색 (프레임 0~4 순) [실측 소스 픽셀 최빈색].</summary>
        private static readonly Color[] BlockCapColors =
        {
            Hex("01FF67"), Hex("43BCFF"), Hex("FDFF68"), Hex("FE9CCB"), Hex("32349A"),
        };

        /// <summary>안쪽 사각 색 (프레임 0~4 순) [실측 소스 픽셀 최빈색].</summary>
        private static readonly Color[] BlockSquareColors =
        {
            Hex("FF3235"), Hex("FBAC00"), Hex("3466CC"), Hex("66FF33"), Hex("4AB2FF"),
        };

        /// <summary>웰컴 카드 헤더 [실측 (255,103,50)].</summary>
        private static readonly Color CardHeader = Hex("FF6732");

        /// <summary>웰컴 카드 하단 두께 면 [실측 (204,204,204)].</summary>
        private static readonly Color CardBase = Hex("CCCCCC");

        public static void BuildAll()
        {
            Directory.CreateDirectory(GameDir);
            Directory.CreateDirectory(UiDir);

            BuildGameArt();
            BuildUiArt();

            AssetDatabase.Refresh();
            Log.Success($"Blumgi 스프라이트 굽기 완료 — 월드 {GameCount}종 · UI {UiCount}종");

            CheckDataPalette();
        }

        private const int GameCount = 20;
        private const int UiCount = 15;

        // ══════════════════════════════════════════════════════════════════════
        // ★★ 데이터 ↔ 구운 색 «교차 검산» — 축이 둘이면 조용히 갈린다
        //
        //   블록 색은 «두 곳»에 있다.
        //     ① 여기 BlockCapColors / BlockSquareColors — 실제로 그림에 굽는 값
        //     ② BlumgiLevelTable.json 의 BlockFill/Square/LeftCap — 원본 값의 «기록»
        //
        //   ②는 렌더에 안 쓰인다. 그래서 «갈려도 화면이 안 변하고», 실제로 갈린 채
        //   17회차나 지나서야 잡혔다 (W1L2 좌캡 = 컨페티 분홍 · W1L3 좌캡 = 야자수 색).
        //   ⇒ 대조를 «사람 기억»이 아니라 «기계»가 한다. 갈리면 즉시 로그가 뜬다.
        //
        //   ⚠ 허용 오차 ±6 인 이유 — 원본 시트가 «손실 webp» 라 같은 단색 면 안에서도
        //     채널이 ±3 정도 흔들린다. ①과 ②를 «다른 회차가 다른 통계»(최빈값 / 중앙값)로
        //     떴기 때문에 0 오차를 요구하면 정상인 칸까지 걸린다.
        //     ★ 실제 결함은 Δ 47 · 64 였다 — ±6 으로도 충분히 걸린다.
        // ══════════════════════════════════════════════════════════════════════

        private const string LevelTablePath = "Assets/Blumgi-Bounce/Data/BlumgiLevelTable.json";

        /// <summary>webp 손실 압축 + 통계 차이를 흡수하는 채널 허용 오차.</summary>
        private const int PaletteTolerance = 6;

        /// <summary>
        /// 레벨 테이블의 블록 3색이 <b>실제로 굽는 색과 같은지</b> 대조한다.
        /// <para>실행: <c>Tools/unity-batch.sh JinHyung.EditorTools.BlumgiSpriteBuilder.CheckDataPalette</c></para>
        /// </summary>
        public static void CheckDataPalette()
        {
            if (!File.Exists(LevelTablePath))
            {
                Log.Error($"레벨 테이블이 없다 — {LevelTablePath}");
                return;
            }

            string text = File.ReadAllText(LevelTablePath);
            MatchCollection records = Regex.Matches(text, @"\{[^{}]*\}", RegexOptions.Singleline);

            int checkedCells = 0;
            int mismatches = 0;

            foreach (Match record in records)
            {
                string body = record.Value;

                int levelNo = ReadInt(body, "LevelNo");
                int[] frameByLevel = BlumgiArtAddress.BlockSkinFrameByLevelNo;
                if (levelNo < 1 || levelNo > frameByLevel.Length)
                    continue;

                string code = ReadString(body, "Code");
                int frame = frameByLevel[levelNo - 1];

                mismatches += ComparePaletteCell(code, "BlockFillColorHex", ReadString(body, "BlockFillColorHex"), White);
                mismatches += ComparePaletteCell(code, "BlockSquareColorHex", ReadString(body, "BlockSquareColorHex"), BlockSquareColors[frame]);
                mismatches += ComparePaletteCell(code, "BlockLeftCapColorHex", ReadString(body, "BlockLeftCapColorHex"), BlockCapColors[frame]);
                checkedCells += 3;
            }

            if (mismatches > 0)
                Log.Error($"블록 색 교차 검산 실패 — {mismatches}/{checkedCells} 칸이 구운 색과 다르다 (허용 ±{PaletteTolerance})");
            else
                Log.Success($"블록 색 교차 검산 통과 — {checkedCells}/{checkedCells} 칸 (허용 ±{PaletteTolerance})");
        }

        /// <summary>한 칸을 대조한다. 어긋나면 1, 같으면 0.</summary>
        private static int ComparePaletteCell(string code, string field, string dataHex, Color baked)
        {
            if (dataHex.IsNullOrEmpty())
            {
                Log.Error($"{code} · {field} 가 비었다");
                return 1;
            }

            Color data = Hex(dataHex);
            int dr = ChannelDelta(data.r, baked.r);
            int dg = ChannelDelta(data.g, baked.g);
            int db = ChannelDelta(data.b, baked.b);

            if (dr <= PaletteTolerance && dg <= PaletteTolerance && db <= PaletteTolerance)
                return 0;

            Log.Error($"{code} · {field} = #{dataHex} 인데 굽는 색은 #{ColorUtility.ToHtmlStringRGB(baked)} 다 (Δ {dr},{dg},{db})");
            return 1;
        }

        private static int ChannelDelta(float a, float b)
        {
            return Mathf.Abs(Mathf.RoundToInt(a * 255f) - Mathf.RoundToInt(b * 255f));
        }

        private static string ReadString(string body, string key)
        {
            Match m = Regex.Match(body, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        private static int ReadInt(string body, string key)
        {
            Match m = Regex.Match(body, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value) : -1;
        }

        // ══════════════════════════════════════════════════ 월드 (PPU 1)

        private static void BuildGameArt()
        {
            // ── 블록 스킨 «5프레임». 격자 칸 50×50 · 스프라이트 bbox 84×58.983 [확정 D · UIUX 2-c].
            //    ⚠ 충돌(50)과 그림(84×58.983)은 «다른 것»이다. 여기서는 그림만 만든다.
            //    ★ 세 장(base/pattern/cap)을 겹쳐 틴트하던 예전 구성은 «원본 기전이 아니었다» —
            //      원본은 한 장짜리 스킨의 «5프레임»이고 색은 그림에 구워져 있다 (17회차 실측).
            for (int frame = 0; frame < BlockFrameCount; frame++)
                WritePng(GameDir, $"block_skin_{frame}.png", BuildBlockSkin(frame));

            // ── 블롭 (발사대). 몸통은 색을 굽고, 표정은 별개 스프라이트다 [실측 spr_Slime / spr_SlimeEyes].
            WritePng(GameDir, "blob_body.png", BuildBlobBody());
            WritePng(GameDir, "blob_eyes_idle.png", BuildBlobEyesIdle());
            WritePng(GameDir, "blob_eyes_angry.png", BuildBlobEyesAngry());
            WritePng(GameDir, "blob_eyes_shout.png", BuildBlobEyesShout());

            // ── 공 · 트레일 잔상. 트레일은 파티클이 아니라 «공 잔상 31장»이다 [05_연출 3-2].
            WritePng(GameDir, "ball.png", BuildBall(false));
            WritePng(GameDir, "ball_ghost.png", BuildBall(true));

            // ── 골대. 6부품이지만 «그리는» 것은 림·그물·화살표 셋뿐이다 (나머지는 충돌·감지 전용).
            WritePng(GameDir, "hoop_rim.png", BuildHoopRim());
            WritePng(GameDir, "hoop_net.png", BuildHoopNet());
            WritePng(GameDir, "goal_arrow.png", BuildGoalArrow());

            // ── 배경. 테마를 «따라가는» 것들이라 전부 흰색으로 굽는다 (확정 F).
            WritePng(GameDir, "palm_leaf.png", BuildPalmLeaf());
            WritePng(GameDir, "palm_trunk.png", BuildPalmTrunk());
            WritePng(GameDir, "grid_tile.png", BuildGridTile());
            WritePng(GameDir, "solid.png", BuildSolid());

            // ── 이펙트
            WritePng(GameDir, "confetti_rect.png", BuildConfettiRect());
            WritePng(GameDir, "sparkle.png", BuildSparkle());

            // ── ★ 골인 텔레포트 (원본 FXteleport). 6프레임 중 «그림이 있는» 5장만 굽는다 —
            //    f5 는 불투명 픽셀 0(완전 투명)이라 «그릴 것이 없다» [23회차 소스 직독].
            for (int frame = 0; frame < BlumgiGoalBurst.TeleportSpriteCount; frame++)
                WritePng(GameDir, $"fx_teleport_{frame}.png", BuildTeleportFrame(frame));

            // ⚠ FXwinLight 는 «굽지 않는다» — 원본 소스가 1×1 순백 불투명 1픽셀이라
            //    solid 를 늘려 쓰는 것이 원본과 «같은 것»이다 [23회차 소스 직독].
        }

        // ── 블록 ───────────────────────────────────────────

        /// <summary>블록 스킨 프레임 수 [실측 <c>spr_BlockSimpleSkin</c> 5프레임 · <c>speed = 0</c>].</summary>
        public const int BlockFrameCount = 5;

        /// <summary>블록 텍스처 폭 — 표시 bbox 84 [UIUX 2-g]. 이미 4의 배수다.</summary>
        private const int BlockW = 84;

        /// <summary>블록 텍스처 높이 — 표시 bbox 58.983 을 4의 배수로 올린 60. 위아래 투명 패딩.</summary>
        private const int BlockH = 60;

        /// <summary>격자 피치 — <b>배치</b> 간격이다. 그림은 이보다 크다 (확정 D).</summary>
        private const float BlockPitch = 50f;

        // ══════════════════════════════════════════════════════════════════════
        // ★★★ 블록 가로 기하 — **[정정 · 17회차]** 소스 텍스처 픽셀을 그대로 꺼내 다시 쟀다.
        //
        //   5회차의 스캔라인 실측은 «화면 합성 결과»였고, 그것을 「몸통이 −16…+38 에 앉는다」로
        //   읽어 **몸통 한 장 + 좌캡 한 장** 두 스프라이트로 나눠 구웠다. 그게 틀렸다 —
        //   원본은 **한 장이 격자점 기준 ±42 로 «대칭»**이고, 그 한 장 안에
        //   좌캡 · 칸막이 · 몸통 · 안쪽 사각이 전부 들어 있다.
        //
        //   [실측 · frame 1 (84×58) 픽셀 경계 · 격자점 기준 world 오프셋]
        //     −42.0 … −40.0  투명 (라운드 모서리 자리)
        //     −40.0 … −35.0  검정 외곽선
        //     −35.0 … −15.0  ★ 좌캡 색
        //     −15.0 … −10.0  검정 칸막이
        //     −10.0 … +34.0  흰 몸통 (안쪽에 사각 −0.5 … +26.5)
        //     +34.0 … +40.0  검정 외곽선
        //     +40.0 … +42.0  투명
        //   세로는 −29.5 … +29.5 이고 검정 테두리가 위아래 6 px, 색 있는 몸통이 −22.5 … +22.5 다.
        //
        //   ⇒ **좌캡을 «덮는» 기전은 그대로다.** 왼쪽 이웃(중심 x−50)의 몸통이 [x−60, x−8] 를
        //     덮어 이 칸의 캡 [x−35, x−15] 을 통째로 가린다. 코드가 판정하지 않는다 (재발방지 #53).
        //
        //   ⚠ **프레임 0(W1L1)만 소스가 83 px** 이고 표시는 5레벨 전부 84 다 [실측] ⇒ 원본이
        //     W1L1 을 가로 1.012 배 «늘려» 그린다. 우리는 PPU 1(텍스처 px = world px)이라
        //     **늘어난 뒤의 84 로 굽는다** — 화면에 나오는 그림이 같다.
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>스킨 바깥 라운드 사각 반폭 [실측 −40 … +40].</summary>
        private const float BlockOuterHalfW = 40f;

        /// <summary>스킨 바깥 라운드 사각 반높이 [실측 −28 … +28 (표시 −29.5…+29.5 의 불투명 구간)].</summary>
        private const float BlockOuterHalfH = 28f;

        /// <summary>검정 외곽선 두께 [실측 좌 5 · 우 6 · 상하 6 ⇒ 5.5].</summary>
        private const float BlockStroke = 5.5f;

        /// <summary>바깥 모서리 반경 [실측 — y1 에서 x3, y3 에서 x1 ⇒ ≈4].</summary>
        private const float BlockOuterCorner = 4f;

        /// <summary>검정 칸막이 왼쪽 [실측 −15].</summary>
        private const float BlockDividerLeft = -15f;

        /// <summary>
        /// 검정 칸막이 오른쪽 [실측 −10].
        /// ★ <b>「캡 오른쪽 + 격자 피치 50 = 몸통 오른쪽」 등식이 겹침 기전의 전부</b>다 —
        /// <c>-10 + 50 = +40</c> 이 곧 바깥 오른쪽 끝이다. 한쪽만 고치면 캡이 안 덮인다.
        /// </summary>
        private const float BlockDividerRight = BlockOuterHalfW - BlockPitch;

        /// <summary>안쪽 사각 중심 [실측 −0.5 … +26.5 ⇒ 중심 +13 · 반폭 13.5].</summary>
        private const float BlockSquareCenterX = 13f;

        private const float BlockSquareHalfX = 13.5f;

        /// <summary>안쪽 사각 세로 반높이 [실측 −13.5 … +13.5].</summary>
        private const float BlockSquareHalfY = 13.5f;

        /// <summary>안쪽 사각 모서리 반경 [실측 1 px 인셋 ⇒ ≈3].</summary>
        private const float BlockSquareCorner = 3f;

        /// <summary>
        /// 블록 스킨 한 프레임 — <b>좌캡 · 칸막이 · 흰 몸통 · 안쪽 사각이 «한 장»에 들어 있다</b>.
        /// 색은 <see cref="BlockCapColors"/> / <see cref="BlockSquareColors"/> 에서 온다 —
        /// <b>틴트가 아니라 그림에 굽는다</b> [17회차 실측 · 인스턴스 색이 5레벨 전부 (255,255,255)].
        /// </summary>
        private static Texture2D BuildBlockSkin(int frame)
        {
            float cx = BlockW * 0.5f;
            float cy = BlockH * 0.5f;

            Color cap = BlockCapColors[Mathf.Clamp(frame, 0, BlockFrameCount - 1)];
            Color square = BlockSquareColors[Mathf.Clamp(frame, 0, BlockFrameCount - 1)];

            return Bake(BlockW, BlockH, (x, y) =>
            {
                float lx = x - cx;
                float ly = y - cy;

                float d = SdRoundRect(lx, ly, BlockOuterHalfW, BlockOuterHalfH, BlockOuterCorner);

                if (d > 0f)
                    return Color.clear;

                if (d > -BlockStroke)
                    return Black;

                // 검정 칸막이 — 캡과 몸통을 가르는 세로 띠.
                if (lx >= BlockDividerLeft && lx <= BlockDividerRight)
                    return Black;

                if (lx < BlockDividerLeft)
                    return cap;

                // 흰 몸통 + 안쪽 사각.
                float s = SdRoundRect(lx - BlockSquareCenterX, ly,
                                      BlockSquareHalfX, BlockSquareHalfY, BlockSquareCorner);

                if (s <= 0f)
                    return square;

                // 사각 둘레의 «연회색 얇은 내부선» [06_리소스 B-1 #5].
                if (s <= 1.5f)
                    return BlockInnerLine;

                return White;
            });
        }

        // ── 블롭 ───────────────────────────────────────────

        /// <summary>블롭 몸통 — 소스 114×88 [UIUX 2-g] → 4의 배수 116×88.</summary>
        private static Texture2D BuildBlobBody()
        {
            const int W = 116;
            const int H = 88;

            float cx = W * 0.5f;
            float cy = H * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - cx;
                float ly = y - cy;

                // 아래가 넓은 물방울/젤리 실루엣 — 위는 둥근 돔, 밑면은 평평하다 [06_리소스 B-1 #1].
                // 위로 갈수록 좁아지는 «달걀»을 라운드 사각으로 근사한다.
                float narrow = Mathf.Clamp01((ly + H * 0.5f) / H);           // 0 = 맨 위, 1 = 맨 아래
                float halfW = Mathf.Lerp(W * 0.30f, W * 0.47f, Smooth(narrow));
                float d = SdRoundRect(lx, ly, halfW, H * 0.5f - 1f, Mathf.Min(halfW, 30f));

                if (d > 0f)
                    return Color.clear;

                if (d > -Stroke)
                    return Black;

                // 주황 «방사» 그라디언트 — 좌상단이 밝고 중앙 아래가 진하다 [실측 캡처 샘플].
                float hx = (lx + W * 0.16f) / (W * 0.55f);
                float hy = (ly + H * 0.22f) / (H * 0.60f);
                float t = Mathf.Clamp01(Mathf.Sqrt(hx * hx + hy * hy));

                Color body = Color.Lerp(BlobLight, BlobDeep, Smooth(t));

                // 좌상단 작은 하이라이트
                float gx = (lx + W * 0.22f) / (W * 0.13f);
                float gy = (ly + H * 0.26f) / (H * 0.10f);
                float g = gx * gx + gy * gy;

                if (g < 1f)
                    body = Color.Lerp(BlobHighlight, body, Smooth(Mathf.Sqrt(g)));

                return body;
            });
        }

        /// <summary>
        /// 평상 표정 — 검은 점 눈 2개 + 작은 <c>∪</c> 미소. 소스 50×22 → 52×24.
        /// ⚠ <b>몸통과 별개 오브젝트</b>다 (<c>spr_SlimeEyes</c>) — 몸통에 굽지 않는다.
        /// </summary>
        private static Texture2D BuildBlobEyesIdle()
        {
            const int W = 52;
            const int H = 24;

            float cx = W * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - cx;

                // 눈 — 세로로 살짝 긴 타원 2개
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = side * 13f;
                    float dx = (lx - ex) / 5.0f;
                    float dy = (y - 8f) / 6.5f;

                    if (dx * dx + dy * dy <= 1f)
                        return Black;
                }

                // 미소 — 아래로 볼록한 얇은 호
                float mx = lx / 8.0f;
                float my = (y - 14.5f) / 5.0f;
                float r = Mathf.Sqrt(mx * mx + my * my);

                if (my >= -0.15f && r >= 0.72f && r <= 1.0f)
                    return Black;

                return Color.clear;
            });
        }

        /// <summary>
        /// 찡그림 — 굵은 <b>사선 눈썹형 눈</b> + <b>흰 이를 앙다문 가로 사각 입</b> [06_리소스 B-1 #2].
        /// 소스 48×28 → 4의 배수 48×28 (이미 배수다).
        /// </summary>
        private static Texture2D BuildBlobEyesAngry()
        {
            const int W = 48;
            const int H = 28;

            float cx = W * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - cx;

                // 사선 눈썹형 눈 — 안쪽이 내려온 굵은 막대
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = side * 12f;
                    float rx = lx - ex;
                    float slope = side * 0.45f;              // 안쪽이 내려온다
                    float ry = y - 7f - rx * slope;

                    if (Mathf.Abs(rx) <= 8f && Mathf.Abs(ry) <= 3.2f)
                        return Black;
                }

                // 앙다문 입 — 검은 테두리 안에 «흰 이» 가로 사각 [06_리소스 B-1 #2]
                if (SdRoundRect(lx, y - 20.5f, 9f, 5f, 2f) <= 0f)
                {
                    bool inner = SdRoundRect(lx, y - 20.5f, 6.5f, 2.5f, 1f) <= 0f;
                    return inner ? White : Black;
                }

                return Color.clear;
            });
        }

        /// <summary>
        /// ★★ <b>발사 표정 (프레임 2)</b> — 「관측 0회」로 <b>비워 두었던 3번째 프레임</b>이다.
        /// 17회차가 소스 텍스처를 그대로 꺼내 닫았다 [실측 <c>gap17/pix/spr_SlimeEyes__Animation_1__2.png</c>].
        ///
        /// <para>
        /// 소스 <b>50 × 47</b> → 4의 배수 <b>52 × 48</b>.
        /// ⚠ <b>이 프레임만 시트가 다르다</b>(<c>shared-1-sheet2.webp</c> · 0·1 은 sheet3) —
        /// 「눈 감음」이 아니라 <b>위아래로 큰 그림</b>이다.
        /// </para>
        ///
        /// <para>
        /// 그림 = <b>평상시와 같은 둥근 눈 2개 + 세로로 길게 벌린 «외치는 입»</b> [실측 픽셀 전수].
        /// 입은 <c>x 19…30 · y 14…45</c> 의 세로 캡슐이고, <b>눈 위치는 프레임 0 과 같다</b> —
        /// 원본 원점이 세 프레임 모두 위에서 9~10 px 라 <b>눈이 제자리에 있고 입만 아래로 자란다</b>.
        /// </para>
        ///
        /// <para>뜨는 조건은 <b>릴리즈(발사) 프레임</b>이고 <b>340.4 ms 뒤 프레임 0 으로 복귀</b>한다 [실측].</para>
        /// </summary>
        private static Texture2D BuildBlobEyesShout()
        {
            const int W = 52;
            const int H = 48;

            float cx = W * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - cx;

                // 눈 — 프레임 0 과 «같은 자리 · 같은 크기» [실측 x 1…14 / 35…48 · y 1…14].
                for (int side = -1; side <= 1; side += 2)
                {
                    float ex = side * 17f;
                    float dx = (lx - ex) / 7.0f;
                    float dy = (y - 8.5f) / 7.0f;

                    if (dx * dx + dy * dy <= 1f)
                        return Black;
                }

                // 외치는 입 — 세로 캡슐 [실측 중심 (25.5, 30.5) · 반폭 5.5 · 반높이 16].
                if (SdRoundRect(lx - 0.5f, y - 30.5f, 5.5f, 16f, 5.5f) <= 0f)
                    return Black;

                return Color.clear;
            });
        }

        // ── 공 ────────────────────────────────────────────

        /// <summary>
        /// 농구공 — 소스 94×93 [UIUX 2-g] → 4의 배수 96×96.
        ///
        /// <para>
        /// ⚠ <c>06_리소스.md</c> B-1 #3 이 「남색 바탕 + <b>흰</b> 곡선 라인」이라고 적었으나
        /// <b>캡처 실측은 하늘색 바탕 + «검은» 라인</b>이다 (<c>G00_W1L1_clean.png</c> 확대 · 픽셀 샘플
        /// (102,192,255)/(10,17,23)). <b>캡처가 이긴다</b> — PD 에 정정 보고한다.
        /// </para>
        /// </summary>
        private static Texture2D BuildBall(bool ghost)
        {
            const int S = 96;

            float c = S * 0.5f;
            const float R = 46f;

            return Bake(S, S, (x, y) =>
            {
                float lx = x - c;
                float ly = y - c;
                float d = Mathf.Sqrt(lx * lx + ly * ly) - R;

                if (d > 0f)
                    return Color.clear;

                // ★ 트레일 잔상은 «공과 같은 실루엣의 흰 반투명 고스트»다 [05_연출 3-2].
                //   선·하이라이트를 그리지 않는다 — 그리면 원본보다 또렷해진다.
                if (ghost)
                    return new Color(1f, 1f, 1f, 0.45f);

                if (d > -Stroke)
                    return Black;

                Color body = Color.Lerp(BallTop, BallBottom, Mathf.Clamp01((ly + R) / (R * 2f)));

                // 좌상단 하이라이트
                float hx = (lx + R * 0.42f) / (R * 0.30f);
                float hy = (ly + R * 0.46f) / (R * 0.22f);

                if (hx * hx + hy * hy < 1f)
                    body = BallHighlight;

                // 라인 4줄 — 세로 1 · 가로 1 · 좌우 호 2 [실측 확대 캡처]
                const float LineHalf = 2.6f;

                if (Mathf.Abs(lx) <= LineHalf)
                    return Black;

                if (Mathf.Abs(ly) <= LineHalf)
                    return Black;

                for (int side = -1; side <= 1; side += 2)
                {
                    float ax = lx + side * R * 1.25f;
                    float rr = Mathf.Sqrt(ax * ax + ly * ly);

                    if (Mathf.Abs(rr - R * 1.32f) <= LineHalf)
                        return Black;
                }

                return body;
            });
        }

        // ── 골대 ──────────────────────────────────────────

        /// <summary>림 — 가로로 누운 라운드 캡슐. 소스 212×55 → 212×56. 왼 2/3 밝고 오른 1/3 이 그늘 [05_연출 §4].</summary>
        private static Texture2D BuildHoopRim()
        {
            const int W = 212;
            const int H = 56;

            float cx = W * 0.5f;
            float cy = H * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - cx;
                float ly = y - cy;
                float d = SdRoundRect(lx, ly, W * 0.5f - 1f, H * 0.5f - 1f, H * 0.5f - 1f);

                if (d > 0f)
                    return Color.clear;

                if (d > -Stroke)
                    return Black;

                // 왼쪽 끝의 «흰 타원 하이라이트»
                float hx = (lx + W * 0.36f) / (W * 0.09f);
                float hy = (ly + H * 0.20f) / (H * 0.12f);

                if (hx * hx + hy * hy < 1f)
                    return White;

                return x < W * (2f / 3f) ? RimLight : RimShade;
            });
        }

        /// <summary>
        /// 그물 — 흰 지그재그 격자 + 검은 외곽선. 위가 넓고 아래로 좁아지는 사다리꼴. 소스 164×140.
        /// </summary>
        private static Texture2D BuildHoopNet()
        {
            const int W = 164;
            const int H = 140;

            float cx = W * 0.5f;

            // [실측 확대 캡처] 굵은 «W» 자 지그재그가 3줄 정도로 성기게 짜여 있다 —
            // 촘촘한 체인링크로 그리면 원본보다 세밀해진다 (처음에 그렇게 구웠다).
            const int Strands = 3;

            return Bake(W, H, (x, y) =>
            {
                float t = Mathf.Clamp01(y / (H - 1f));

                // 사다리꼴 — 아래로 좁아지다 끝에서 살짝 벌어진다 [06_리소스 B-1 #7]
                float halfTop = W * 0.5f - 2f;
                float halfBottom = W * 0.26f;
                float half = Mathf.Lerp(halfTop, halfBottom, Smooth(t)) + Mathf.Pow(t, 5f) * 14f;

                float lx = x - cx;

                if (Mathf.Abs(lx) > half)
                    return Color.clear;

                // 지그재그 두 방향의 줄을 겹쳐 격자를 만든다
                float u = (lx / half) * 0.5f + 0.5f;         // 0..1
                float v = t * Strands;

                float a = Mathf.Abs(Frac(u * Strands + v) - 0.5f);
                float b = Mathf.Abs(Frac(u * Strands - v) - 0.5f);
                float w = Mathf.Min(a, b);

                // 줄 굵기는 폭이 좁아질수록 상대적으로 굵어지므로 절대 픽셀로 잡는다
                float band = 5.0f / (half * 2f / Strands);

                if (w > band * 1.7f)
                    return Color.clear;

                return w > band ? Black : White;
            });
        }

        /// <summary>
        /// 골 지시 화살표 — 굵은 아래방향 쐐기 <c>⌄</c>.
        /// 소스 100×170 → 100×172. ⚠ 표시 bbox 는 86×50 이라 <b>세로로 크게 눌러 쓴다</b> [UIUX 2-g] —
        /// 눌림은 프리팹 스케일이 하고, 텍스처는 <b>원화 비율</b>로 굽는다.
        ///
        /// <para>
        /// ★ <b>[정정 · 17회차] 흰색으로 굽는다.</b> 소스 텍스처의 불투명 색이 <b>흰색 1종</b>(9,635 px)
        /// 뿐이고 색은 인스턴스 틴트다 — 2장 겹침이 <b>흰 화살표 + 25 px 아래의 «검정 그림자»</b> 이므로
        /// 검게 구우면 그림자 쪽만 맞고 본체가 틀린다.
        /// </para>
        /// </summary>
        private static Texture2D BuildGoalArrow()
        {
            const int W = 100;
            const int H = 172;

            float cx = W * 0.5f;

            return Bake(W, H, (x, y) =>
            {
                float lx = Mathf.Abs(x - cx);

                // ⚠ <b>아래를 향한 쐐기</b>다 — 꼭짓점이 «아래»에 온다.
                //   (처음에 부호를 반대로 잡아 ∧ 로 구웠고 컨택트시트에서 잡았다.)
                float thickness = 34f;
                float armY = H - 24f - thickness - lx * 1.55f;

                bool inArm = y >= armY && y <= armY + thickness && lx <= W * 0.5f - 4f;

                return inArm ? White : Color.clear;
            });
        }

        // ── 배경 ──────────────────────────────────────────

        /// <summary>
        /// 야자수 잎 — <b>하늘색 단색 실루엣</b>이지만 색은 데이터(<c>PalmColorHex</c>)라 <b>흰색으로 굽는다</b>.
        /// 소스 179×96 → 180×96. ⚠ 표시는 434×233 (2.43배) 이라 <b>확대해 쓴다</b> — 프리팹 스케일이 한다.
        /// </summary>
        private static Texture2D BuildPalmLeaf()
        {
            const int W = 180;
            const int H = 96;

            float cx = W * 0.5f;

            // ── 잎 6장이 «위 가운데»에서 방사형으로 뻗어 «끝이 아래로 늘어진다» [06_리소스 B-1 #9 · 캡처 실측].
            //    각 잎을 «꺾은선 + 폭»으로 만든다 — 축 투영으로 그리면 잎이 서로 겹쳐 뭉친다
            //    (처음에 그렇게 구웠다가 컨택트시트에서 「나비 모양」인 것을 잡았다).
            const int Fronds = 6;
            const int Steps = 10;

            var path = new Vector2[Fronds, Steps + 1];
            var halfWidth = new float[Steps + 1];

            for (int s = 0; s <= Steps; s++)
            {
                float t = s / (float)Steps;
                halfWidth[s] = 15f * Mathf.Sin(Mathf.Pow(t, 0.6f) * Mathf.PI) + 2f;
            }

            for (int f = 0; f < Fronds; f++)
            {
                // −78° ~ +78° (아래를 0 으로 본 좌우 부채꼴)
                float angle = Mathf.Lerp(-1.36f, 1.36f, f / (float)(Fronds - 1));
                float dirX = Mathf.Sin(angle);
                float dirY = Mathf.Cos(angle);

                for (int s = 0; s <= Steps; s++)
                {
                    float t = s / (float)Steps;

                    // 길이는 옆으로 갈수록 길고, 끝으로 갈수록 «아래로 처진다»
                    float length = Mathf.Lerp(58f, 96f, Mathf.Abs(dirX));
                    float droop = t * t * 34f;

                    path[f, s] = new Vector2(cx + dirX * length * t,
                                             10f + dirY * length * t * 0.55f + droop);
                }
            }

            return Bake(W, H, (x, y) =>
            {
                var p = new Vector2(x, y);

                for (int f = 0; f < Fronds; f++)
                {
                    for (int s = 0; s < Steps; s++)
                    {
                        float d = SdSegment(p, path[f, s], path[f, s + 1]);

                        if (d <= (halfWidth[s] + halfWidth[s + 1]) * 0.5f)
                            return White;
                    }
                }

                return Color.clear;
            });
        }

        /// <summary>
        /// 야자수 기둥 — 원본은 <b>타일드 배경</b>이다 [05_연출 §1].
        ///
        /// <para>
        /// ★★ <b>[정정 · 17회차] 소스 타일이 «194 × 26» 이다</b> [실측 <c>tlb_palmtrunk-sheet0.webp</c> 픽셀 전수] —
        /// 예전에 76 × 76 으로 굽던 것은 「표시 폭 75」를 소스 크기로 오인한 값이었다.
        /// <c>imageScaleX/Y = 3</c> · <c>imageOffset 0/0</c> · <c>imageAngle 0</c> · 랜덤화 꺼짐 ·
        /// <b>원점은 아래변 왼쪽</b>.
        /// </para>
        ///
        /// <para>
        /// ★★★ <b>그런데 그 타일드 배경은 «각도 270°» 로 서 있다</b> [5회차 실측] —
        /// 그래서 <b>소스의 26 축이 «화면 가로»</b>, <b>194 축이 «화면 세로»</b> 다.
        /// 26 × 3 = <b>78</b> 이 곧 줄기 두께이고(실측 인스턴스 폭 <b>75.246</b> — 타일이 살짝 잘린다),
        /// 194 × 3 = <b>582</b> 마다 무늬가 반복된다.
        /// </para>
        ///
        /// <para>
        /// 소스 그림은 <b>194 축을 따라 «마디» 8개</b>이고 각 마디는 26 축 방향으로
        /// <b>가운데가 꽉 차고 양 끝(0·25)이 좁아진다</b> [실측 픽셀 전수 — y5~20 이 전부 불투명].
        /// ⇒ 세워 놓으면 <b>가운데가 통짜로 이어지고 «좌우 가장자리»만 마디마다 파인다</b> —
        /// 화면에서 <b>솔리드 기둥</b>으로 읽히는 이유가 이것이다.
        /// ⚠ 이걸 안 세우고 그대로 깔면 <b>구슬을 꿴 것처럼</b> 토막 난다 (실제로 한 번 그렇게 나왔다).
        /// </para>
        ///
        /// <para>세워서 굽는다 — 4의 배수로 <b>28 × 196</b>. 세로 타일링이라 <b>위아래가 맞물려야</b> 한다.</para>
        /// </summary>
        private static Texture2D BuildPalmTrunk()
        {
            const int W = PalmTrunkTileWidth;
            const int H = PalmTrunkTileHeight;

            // [실측] 마디 8개. 194/8 = 24.25 → 196 으로 올렸으므로 24.5 가 된다.
            const float Segment = H / (float)PalmTrunkSegments;
            const float HalfSegment = Segment * 0.5f;

            // [실측] 26 축에서 y5…20 이 꽉 찬 구간(= 중앙 반폭 8), 그 바깥 4.5 px 이 타원으로 좁아진다.
            const float FlatHalf = 8f;
            const float CapSpan = 5f;

            return Bake(W, H, (x, y) =>
            {
                // ★ 가로(= 소스 26 축) 위치가 «마디가 얼마나 파이는가»를 정한다.
                float dx = Mathf.Abs(x - W * 0.5f);
                float half = HalfSegment;

                if (dx > FlatHalf)
                {
                    float k = (dx - FlatHalf) / CapSpan;

                    if (k >= 1f)
                        return Color.clear;

                    half *= Mathf.Sqrt(1f - k * k);
                }

                // ★ 세로(= 소스 194 축)로 마디가 반복된다.
                float ly = Frac(y / Segment) * Segment - HalfSegment;

                return Mathf.Abs(ly) <= half ? White : Color.clear;
            });
        }

        /// <summary>소스 타일의 «짧은 변» 26 을 4의 배수로 올린 값 = 줄기 두께 축 [실측].</summary>
        public const int PalmTrunkTileWidth = 28;

        /// <summary>소스 타일의 «긴 변» 194 를 4의 배수로 올린 값 = 줄기 길이 축 [실측].</summary>
        public const int PalmTrunkTileHeight = 196;

        /// <summary>한 타일에 든 마디 수 [실측 픽셀 전수 8개].</summary>
        public const int PalmTrunkSegments = 8;

        /// <summary>
        /// 배경 격자 — 원본 타일 <b>50 × 50</b> · <b>네 변 전부에 1 px 선</b> [17회차 실측 픽셀 전수 · 불투명 196 px].
        ///
        /// <para>
        /// ★ <b>[정정 · 17회차] 예전에는 «상·좌 두 변»만 그었다.</b> 네 변이라
        /// 타일이 맞닿는 자리에서 <b>선이 두 겹(2 px)</b>이 되는 것이 원본이다 —
        /// 두 변만 그으면 화면 선 굵기가 절반이 된다 (<c>imageScale 4.65</c> 라 9.3 world → 4.65 world).
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>50 은 4의 배수가 아니다.</b> 52 로 올리면 <b>타일 이음매가 어긋나</b> 격자가 깨진다
        /// (타일링 텍스처는 패딩을 못 넣는다). 그래서 <b>2×2 칸을 한 장</b>으로 굽는다 —
        /// 100×100 은 4의 배수이고 <b>피치 50 이 그대로 유지</b>된다.
        /// </para>
        /// </summary>
        private static Texture2D BuildGridTile()
        {
            const int Cell = 50;
            const int S = Cell * 2;

            return Bake(S, S, (x, y) =>
            {
                float u = Frac(x / (float)Cell) * Cell;
                float v = Frac(y / (float)Cell) * Cell;

                bool onLine = u < 1f || u >= Cell - 1f || v < 1f || v >= Cell - 1f;

                return onLine ? White : Color.clear;
            });
        }

        /// <summary>배경 단색 판 · 클리어 흰 플래시 · 전환 페이드 공용. 늘려 쓴다.</summary>
        private static Texture2D BuildSolid()
        {
            return Bake(4, 4, (x, y) => White);
        }

        // ── 이펙트 ────────────────────────────────────────

        /// <summary>
        /// 컨페티 조각 — ★★ <b>[전면 교체 · 23회차 소스 직독] «모서리를 깎은 16×24 마름모»가 아니었다.</b>
        ///
        /// <para>
        /// 원본 소스는 <b>52 × 52 · 6프레임 @ speed 10 · 순백 단색 «마스크»</b>이고,
        /// <b>f0 는 «모서리도 안 깎인» 완전 불투명 정사각</b>이다 [실측 · <c>ExtractImageToCanvas()</c> 직독].
        /// 색 4종(<c>#FF709B</c> · <c>#FCFF47</c> · <c>#7DFF69</c> · <c>#7D64FF</c>)은 <b>전부 런타임 tint</b> 다
        /// (<c>재발방지 #94</c> — 합성 화면이 아니라 자산을 재서 알았다).
        /// </para>
        ///
        /// <para>
        /// ⚠ 나머지 5프레임은 <b>세로 52 그대로 · 가로만 52 → 30 → 6 → 26</b> 으로 줄어드는
        /// <b>«뒤집기»</b>다 — 그림이 아니라 <b>폭 배율</b>이라 <see cref="BlumgiConfettiBurst"/> 가
        /// <b>스케일로 재생</b>한다. 그래서 <b>여기서는 한 장(f0)만 굽는다</b>.
        /// </para>
        ///
        /// <para>
        /// ★ 크기 <b>52</b> 는 실측 소스 크기 그대로다 (4의 배수라 압축 포맷 규칙에도 맞는다).
        /// <see cref="BlumgiConfettiBurst"/> 가 이 크기를 알고 스케일을 되돌린다.
        /// </para>
        /// </summary>
        private static Texture2D BuildConfettiRect()
        {
            const int S = 52;

            return Bake(S, S, (x, y) => White);
        }

        // ── ★★★ 골인 텔레포트 `FXteleport` — 6프레임 ────────────────────
        //
        //   ⚠ 23회차 요약이 「링(ring)」이라 적었는데 **링이 아니다.**
        //     소스 픽셀을 그려 보니 «찬 원 → 마름모 → 십자 → 가는 십자 → 끝동강 → 무»로
        //     **닫히면서 십자로 뻗는** 그림이었다 (재발방지 #60 — 눈으로 봐야 갈린다).
        //
        //   ★ 아래 숫자는 전부 **소스 마스크에서 «잰» 값**이다 (재발방지 #48 — 점수에 맞춘 값이 아니다):
        //     프레임별 불투명 bbox 와 팔 두께를 재고, 그 값 그대로 넣었을 때의 겹침(IoU)을 기록한다.
        //
        //     | f | 소스 | 모양            | 잰 값                                      | IoU   |
        //     |---|------|-----------------|--------------------------------------------|-------|
        //     | 0 | 128² | 찬 원           | r 45.0 · 중심 (64.5, 64.5)                 | 0.959 |
        //     | 1 | 256² | 마름모          | a = b = 65.0 · 중심 (129.5, 129.5)         | 0.877 |
        //     | 2 | 256² | 십자 (굵다)     | 반길이 90 × 100 · 팔 반두께 10             | 0.981 |
        //     | 3 | 256² | 십자 (가늘다)   | 반길이 114 × 122.5 · 팔 반두께 3.5         | 0.856 |
        //     | 4 | 256² | 끝동강 4개      | 가로 |dx| 101~108 · 세로 |dy| 110.5~116.5  | 0.800 |
        //     | 5 | 256² | **완전 투명**   | 불투명 픽셀 0                               | —     |
        //
        //   ⚠ IoU 는 **구운 PNG 를 소스 마스크와 픽셀 대조한 값**이다 (α > 127 기준) — 예측이 아니다.
        //     다시 구운 뒤 이 표와 어긋나면 «모양이 바뀐 것»이므로 렌더 판정을 다시 재야 한다.
        //
        //   ⚠ **원본 PNG 를 그대로 넣지 않는다** (CLAUDE.md — 원본 에셋을 뜯어 넣지 않는다).
        //     잰 기하로 «다시 굽고», 위 IoU 로 「같은 그림인가」를 숫자로 남긴다.
        //
        //   ★★ **중심에 `+0.5` 를 더한다** — «값을 맞춘 것»이 아니라 **좌표 규약**이다.
        //     `Bake` 는 픽셀 x 를 `x + 0.125 … x + 0.875` 로 «픽셀 중심»에서 샘플한다.
        //     소스에서 «픽셀 인덱스 129.5 가 중심»이면 샘플 좌표로는 **130.0** 이다.
        //     이 반 픽셀을 빼먹으면 f2 IoU 가 0.993 → 0.936 으로 떨어진다 [검산].

        /// <summary>텔레포트 f0 소스 한 변 [실측 128].</summary>
        private const int TeleportFrame0Size = 128;

        /// <summary>텔레포트 f1~f5 소스 한 변 [실측 256].</summary>
        private const int TeleportSize = 256;

        /// <summary>
        /// «픽셀 인덱스» → «샘플 좌표» 보정. <see cref="Bake"/> 가 픽셀 중심에서 샘플하기 때문이다.
        /// </summary>
        private const float PixelCenter = 0.5f;

        /// <summary>f0 중심 [실측 bbox 20…109 ⇒ 픽셀 인덱스 64.5].</summary>
        private const float TeleportCenter0 = 64.5f + PixelCenter;

        /// <summary>f1~f4 중심 [실측 bbox 중심이 텍스처 중심에서 (+2, +2) ⇒ 픽셀 인덱스 129.5].</summary>
        private const float TeleportCenter = 129.5f + PixelCenter;

        /// <summary>f3 만 세로 중심이 반 픽셀 아래다 [실측 bbox y 8…252].</summary>
        private const float TeleportCenter3Y = 130f + PixelCenter;

        /// <summary>
        /// 텔레포트 프레임 하나. <b>순백 마스크</b>다 — 색은 런타임이 입힌다 (<c>재발방지 #94</c>).
        /// </summary>
        private static Texture2D BuildTeleportFrame(int frame)
        {
            switch (frame)
            {
                // f0 — 찬 원. 중심이 텍스처 중심에서 (+1, +1) 이다 [실측 bbox 20…109].
                case 0:
                    return Bake(TeleportFrame0Size, TeleportFrame0Size, (x, y) =>
                    {
                        float dx = x - TeleportCenter0;
                        float dy = y - TeleportCenter0;
                        return dx * dx + dy * dy <= 45f * 45f ? White : Color.clear;
                    });

                // f1 — 마름모. 반대각 65 [실측 bbox 65…194].
                case 1:
                    return Bake(TeleportSize, TeleportSize, (x, y) =>
                    {
                        float dx = Mathf.Abs(x - TeleportCenter);
                        float dy = Mathf.Abs(y - TeleportCenter);
                        return dx / 65f + dy / 65f <= 1f ? White : Color.clear;
                    });

                // f2 — 굵은 십자. 팔은 «끝으로 갈수록 가늘어진다» (타원 테이퍼) [실측 두께 20 → 5].
                case 2:
                    return BakeCross(TeleportCenter, TeleportCenter, 90f, 100f, 10f);

                // f3 — 가는 십자. 같은 기전이고 팔만 얇다 [실측 두께 7].
                case 3:
                    return BakeCross(TeleportCenter, TeleportCenter3Y, 114f, 122.5f, 3.5f);

                // f4 — 팔 «끝동강»만 남는다. **가운데가 비어 있다** [실측 88 px · 중심 반경 0].
                //   가로 끝동강 = 행 129·130 의 x 230…237 (좌우 대칭) · 세로 끝동강 = 열 129·130 의 y 13…19.
                case 4:
                    return Bake(TeleportSize, TeleportSize, (x, y) =>
                    {
                        float dx = Mathf.Abs(x - TeleportCenter);
                        float dy = Mathf.Abs(y - TeleportCenter);

                        bool horizontal = dy <= 1f && dx >= 101f && dx <= 108f;
                        bool vertical = dx <= 1f && dy >= 110.5f && dy <= 116.5f;

                        return horizontal || vertical ? White : Color.clear;
                    });

                default:
                    Log.Error($"텔레포트 프레임 번호가 범위 밖이다: {frame}");
                    return Bake(TeleportSize, TeleportSize, (x, y) => Color.clear);
            }
        }

        /// <summary>
        /// 십자 — <b>가로 팔 ∪ 세로 팔</b>. 팔은 반두께 <c>t</c> 에서 시작해 끝으로 갈수록
        /// <c>sqrt(1 − (거리/반길이)²)</c> 로 가늘어진다 [실측 두께 프로파일과 맞춘 테이퍼].
        /// </summary>
        private static Texture2D BakeCross(float cx, float cy, float halfWidth, float halfHeight, float halfThickness)
        {
            return Bake(TeleportSize, TeleportSize, (x, y) =>
            {
                float dx = x - cx;
                float dy = y - cy;

                if (Mathf.Abs(dx) <= halfWidth)
                {
                    float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - dx / halfWidth * (dx / halfWidth)));

                    if (Mathf.Abs(dy) <= halfThickness * taper)
                        return White;
                }

                if (Mathf.Abs(dy) <= halfHeight)
                {
                    float taper = Mathf.Sqrt(Mathf.Max(0f, 1f - dy / halfHeight * (dy / halfHeight)));

                    if (Mathf.Abs(dx) <= halfThickness * taper)
                        return White;
                }

                return Color.clear;
            });
        }

        /// <summary>반짝임 <c>FXChling</c> — 흰 4갈래 별. 소스 42×42 → 44×44. 크기 자체가 애니메이션이다 [UIUX 2-g].</summary>
        private static Texture2D BuildSparkle()
        {
            const int S = 44;
            float c = S * 0.5f;

            return Bake(S, S, (x, y) =>
            {
                float lx = Mathf.Abs(x - c) / c;
                float ly = Mathf.Abs(y - c) / c;

                // 오목한 마름모 십자 — 두 축의 거리를 곱해 «별» 모양을 만든다
                float v = Mathf.Sqrt(lx) + Mathf.Sqrt(ly);
                return v <= 1f ? White : Color.clear;
            });
        }

        // ══════════════════════════════════════════════════ UI (PPU 100)

        private static void BuildUiArt()
        {
            // ── 우상단 버튼 4종. 흰 단색 실루엣 · 외곽선 없음 · 100×100 world [UIUX 2-b · 2-g].
            WritePng(UiDir, "ui_btn_retry.png", BuildRetryIcon());
            WritePng(UiDir, "ui_btn_sound_on.png", BuildSoundIcon(true));
            WritePng(UiDir, "ui_btn_sound_off.png", BuildSoundIcon(false));
            WritePng(UiDir, "ui_btn_map.png", BuildMapIcon());
            WritePng(UiDir, "ui_btn_home.png", BuildHomeIcon());

            // ── 레벨 진행 바. ★ 점을 «켜는» 것이 아니라 «폭이 58 단위로 자라는 바»다 [UIUX 2-b].
            //    타일링해야 하므로 58 칸 두 개(116)로 굽는다 — 116·48 둘 다 4의 배수다.
            WritePng(UiDir, "ui_dot_empty.png", BuildDotStrip(false));
            WritePng(UiDir, "ui_dot_full.png", BuildDotStrip(true));

            // ── 튜토리얼 패널. 판 2장(뒤=그림자) · 안쪽 하늘 화면 · 미니 그림 · 마우스 2프레임.
            WritePng(UiDir, "ui_panel_9p.png", BuildPanel9P());
            WritePng(UiDir, "ui_tut_screen.png", BuildTutScreen());
            // ★ 튜토 미니 그림은 «컷마다 다른 프레임»이다 — 두 장을 굽는다 [실측 25회차].
            WritePng(UiDir, "ui_tut_art.png", BuildTutArtHold());
            WritePng(UiDir, "ui_tut_art_b.png", BuildTutArtShoot());
            WritePng(UiDir, "ui_mouse_up.png", BuildMouse(false));
            WritePng(UiDir, "ui_mouse_down.png", BuildMouse(true));

            // ── WELCOME 화면
            WritePng(UiDir, "ui_card.png", BuildPlayerCard());
            WritePng(UiDir, "ui_hand.png", BuildHandCursor());
            WritePng(UiDir, "ui_gradient.png", BuildGradient());
        }

        private const int IconSize = 100;

        /// <summary>↺ 재시작 — 반시계 화살표 고리.</summary>
        private static Texture2D BuildRetryIcon()
        {
            float c = IconSize * 0.5f;
            const float R = 30f;
            const float T = 9f;

            return Bake(IconSize, IconSize, (x, y) =>
            {
                float lx = x - c;
                float ly = y - c;
                float r = Mathf.Sqrt(lx * lx + ly * ly);

                // 고리 — 위쪽 오른편이 트여 있다
                if (Mathf.Abs(r - R) <= T * 0.5f)
                {
                    float angle = Mathf.Atan2(-ly, lx) * Mathf.Rad2Deg;   // 위가 +

                    if (angle < 40f || angle > 105f)
                        return White;
                }

                // 화살촉 — 트인 자리에 삼각형
                float ax = lx - R * 0.62f;
                float ay = ly + R * 0.80f;

                if (Mathf.Abs(ax) + Mathf.Abs(ay) <= 13f)
                    return White;

                return Color.clear;
            });
        }

        /// <summary>🔊 음소거 — 스피커 + 음파 2줄. 꺼짐 상태는 음파 대신 <c>×</c> 다.</summary>
        private static Texture2D BuildSoundIcon(bool on)
        {
            float c = IconSize * 0.5f;

            return Bake(IconSize, IconSize, (x, y) =>
            {
                float lx = x - c;
                float ly = y - c;

                // 스피커 몸통 — 사각 + 삼각 나팔
                if (lx >= -34f && lx <= -16f && Mathf.Abs(ly) <= 12f)
                    return White;

                if (lx >= -16f && lx <= 4f && Mathf.Abs(ly) <= 12f + (lx + 16f) * 1.15f)
                    return White;

                if (on)
                {
                    float r = Mathf.Sqrt(lx * lx + ly * ly);

                    for (int i = 0; i < 2; i++)
                    {
                        float radius = 20f + i * 13f;

                        if (Mathf.Abs(r - radius) <= 4f && lx > 10f && Mathf.Abs(ly) < radius * 0.8f)
                            return White;
                    }
                }
                else
                {
                    float ax = lx - 26f;

                    if ((Mathf.Abs(ax - ly) <= 4.5f || Mathf.Abs(ax + ly) <= 4.5f)
                        && Mathf.Abs(ax) <= 12f && Mathf.Abs(ly) <= 12f)
                        return White;
                }

                return Color.clear;
            });
        }

        /// <summary>⠿ 맵(월드 선택) — 3×3 점 격자.</summary>
        private static Texture2D BuildMapIcon()
        {
            float c = IconSize * 0.5f;

            return Bake(IconSize, IconSize, (x, y) =>
            {
                for (int row = -1; row <= 1; row++)
                {
                    for (int col = -1; col <= 1; col++)
                    {
                        float dx = x - (c + col * 22f);
                        float dy = y - (c + row * 22f);

                        if (dx * dx + dy * dy <= 49f)
                            return White;
                    }
                }

                return Color.clear;
            });
        }

        /// <summary>🏠 홈 — 집 실루엣.</summary>
        private static Texture2D BuildHomeIcon()
        {
            float c = IconSize * 0.5f;

            return Bake(IconSize, IconSize, (x, y) =>
            {
                float lx = x - c;
                float ly = y - c;

                // 지붕 — 위로 모이는 삼각형
                if (ly >= -32f && ly <= 2f && Mathf.Abs(lx) <= (ly + 32f) * 1.15f)
                    return White;

                // 몸통 — 라운드 사각
                if (SdRoundRect(lx, ly - 18f, 24f, 17f, 5f) <= 0f)
                    return White;

                return Color.clear;
            });
        }

        /// <summary>
        /// 레벨 진행 바 한 칸 — 58 × 48 [런타임 실측]. 두 칸(116)을 굽어 타일링한다.
        /// 채움 = 흰 단색 타원 · 빈칸 = 흰 링.
        /// </summary>
        private static Texture2D BuildDotStrip(bool filled)
        {
            const int Cell = 58;
            const int W = Cell * 2;
            const int H = 48;

            return Bake(W, H, (x, y) =>
            {
                float lx = Mathf.Abs(Frac(x / (float)Cell) * Cell - Cell * 0.5f);
                float ly = Mathf.Abs(y - H * 0.5f);

                float dx = lx / 20f;
                float dy = ly / 17f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);

                if (r > 1f)
                    return Color.clear;

                if (filled)
                    return White;

                return r >= 0.66f ? White : Color.clear;
            });
        }

        /// <summary>
        /// 튜토 패널 판 — 흰 라운드 사각 + 검은 굵은 외곽선. <b>9-slice</b> 라 고정 크기가 아니다 [UIUX 2-g].
        /// ⚠ 원본 소스 크기는 <b>미측정</b>(9-slice 오브젝트라 <c>_imageInfo</c> 가 안 나온다).
        /// 경계는 <c>BlumgiArtImportSetup</c> 이 코드로 건다 — 두 값이 어긋나면 모서리가 늘어난다.
        /// </summary>
        private static Texture2D BuildPanel9P()
        {
            const int S = 96;
            const float Radius = 26f;

            return Bake(S, S, (x, y) =>
            {
                float d = SdRoundRect(x - S * 0.5f, y - S * 0.5f, S * 0.5f - 1f, S * 0.5f - 1f, Radius);

                if (d > 0f)
                    return Color.clear;

                return d > -Stroke ? Black : White;
            });
        }

        /// <summary>튜토 패널 안쪽 화면 — 원본 <c>BGTut</c> 250×250 → 252×252. 색 <c>#85E0FF</c> 는 프리팹 tint 다.</summary>
        private static Texture2D BuildTutScreen()
        {
            const int S = 252;

            return Bake(S, S, (x, y) =>
            {
                float d = SdRoundRect(x - S * 0.5f, y - S * 0.5f, S * 0.5f - 1f, S * 0.5f - 1f, 8f);
                return d <= 0f ? White : Color.clear;
            });
        }

        /// <summary>
        /// ★★★ 튜토 패널 안 그림 <b>컷 A</b> — 원본 <c>Sprite2 Animation 1</c> <b>f1 · 152 × 154</b> → 4의 배수 <b>152 × 156</b>.
        ///
        /// <para>
        /// [실측 25회차 · <c>ExtractImageToCanvas</c> 로 «캡처가 아니라 소스»를 떠서 픽셀로 쟀다]
        /// 공 bbox <c>(66,7)-(145,86)</c> = <b>80 × 80</b> · 몸통 bbox <c>(7,87)-(125,146)</c> = <b>119 × 60</b>
        /// (가장 납작한 «홀드로 눌린» 자세) · 눈 <c>(62.5, 109.9)</c>·<c>(102.1, 109.9)</c> · 입 <c>(82.0, 116.9)</c>.
        /// </para>
        /// </summary>
        private static Texture2D BuildTutArtHold()
        {
            // 152×154 를 152×156 에 굽는다 — 세로만 +2 라 y 를 1 내려 가운데 맞춘다.
            return BuildTutArtFrame(152, 156, 0f, 1f,
                                    new Vector2(105.5f, 46.5f), 40f,
                                    new Vector2(65.2f, 118.5f), 59f, 30f, 0f,
                                    new Vector2(62.5f, 109.9f), new Vector2(102.1f, 109.9f),
                                    new Vector2(8f, 5.5f),
                                    new Vector2(82f, 116.9f), new Vector2(8.5f, 3.5f));
        }

        /// <summary>
        /// ★★★ 튜토 패널 안 그림 <b>컷 B</b> — 원본 <b>f2 · 230 × 258</b> → 4의 배수 <b>232 × 260</b>.
        ///
        /// <para>
        /// [실측 25회차] 공 bbox <c>(143,7)-(222,86)</c> = <b>80 × 80</b>(세 프레임 «전부» 80×80 이고
        /// 움직이는 것은 «자리»다) · 몸통 bbox <c>(7,155)-(113,250)</c> = <b>107 × 96</b> 이고
        /// <b>주축이 −39.7° 로 누운 «대각선으로 늘어난 튀어오른 자세»</b>(PCA 반축 60.1 / 43.6) ·
        /// 눈 <c>(64.5, 192.4)</c>·<c>(98.3, 192.3)</c> · 입 <c>(77.8, 203.4)</c>.
        /// </para>
        ///
        /// <para>
        /// ⚠ <b>f0(138 × 167) 은 굽지 않는다</b> — 튜토 6 s 채록 전수에서 <b>0회</b>다.
        /// 어디서 쓰이는지는 <b>미측정</b>이라 관측 못 한 것을 굽지 않는다.
        /// </para>
        /// </summary>
        private static Texture2D BuildTutArtShoot()
        {
            return BuildTutArtFrame(232, 260, 1f, 1f,
                                    new Vector2(182.4f, 46.5f), 40f,
                                    new Vector2(62f, 205f), 59f, 43f, -39.7f,
                                    new Vector2(64.5f, 192.4f), new Vector2(98.3f, 192.3f),
                                    new Vector2(7f, 8f),
                                    new Vector2(77.8f, 203.4f), new Vector2(7.5f, 4f));
        }

        /// <summary>
        /// 튜토 미니 그림의 <b>한 프레임</b>을 굽는다 — 공(농구공) + 몸통(기울어진 블롭) + 눈 2 + 입.
        /// 좌표는 <b>원본 소스 프레임의 픽셀</b>(y 아래로 +)이고 <paramref name="padX"/>·<paramref name="padY"/> 로
        /// 4의 배수 패딩만큼 밀어 넣는다.
        /// </summary>
        private static Texture2D BuildTutArtFrame(int width, int height, float padX, float padY,
                                                  Vector2 ballCenter, float ballRadius,
                                                  Vector2 bodyCenter, float bodySemiMajor,
                                                  float bodySemiMinor, float bodyAngleDegrees,
                                                  Vector2 eyeLeft, Vector2 eyeRight, Vector2 eyeHalf,
                                                  Vector2 mouthCenter, Vector2 mouthHalf)
        {
            float cos = Mathf.Cos(bodyAngleDegrees * Mathf.Deg2Rad);
            float sin = Mathf.Sin(bodyAngleDegrees * Mathf.Deg2Rad);

            return Bake(width, height, (px, py) =>
            {
                float x = px - padX;
                float y = py - padY;

                // ── 몸통이 «위»다 — 원본에서 블롭이 공을 가린다.
                float bx = x - bodyCenter.x;
                float by = y - bodyCenter.y;

                // 주축으로 회전시킨 타원. 거리 근사는 짧은 반축을 곱해 «픽셀 단위»로 되돌린다.
                float u = bx * cos + by * sin;
                float v = -bx * sin + by * cos;
                float e = Mathf.Sqrt(u * u / (bodySemiMajor * bodySemiMajor)
                                     + v * v / (bodySemiMinor * bodySemiMinor)) - 1f;
                float bodyDistance = e * bodySemiMinor;

                if (bodyDistance <= 0f)
                {
                    if (bodyDistance > -Stroke)
                        return Black;

                    // 눈 — 좌우 두 개. 원본은 «검은 타원»이다.
                    if (InEllipse(x, y, eyeLeft, eyeHalf) || InEllipse(x, y, eyeRight, eyeHalf))
                        return Black;

                    // 입 — 아래로 볼록한 짧은 곡선.
                    float mx = (x - mouthCenter.x) / mouthHalf.x;

                    if (Mathf.Abs(mx) <= 1f)
                    {
                        float lip = mouthCenter.y + (1f - mx * mx) * mouthHalf.y;

                        if (Mathf.Abs(y - lip) <= mouthHalf.y * 0.55f)
                            return Black;
                    }

                    Color body = Color.Lerp(BlobLight, BlobDeep,
                                            Smooth(Mathf.Clamp01((v + bodySemiMinor)
                                                                 / (bodySemiMinor * 2f))));

                    // 좌상단 하이라이트 — 주축을 따라 눕는다 [실측 소스에 밝은 점 2개].
                    float hu = (u + bodySemiMajor * 0.45f) / (bodySemiMajor * 0.22f);
                    float hv = (v + bodySemiMinor * 0.45f) / (bodySemiMinor * 0.20f);

                    if (hu * hu + hv * hv < 1f)
                        body = BlobHighlight;

                    return body;
                }

                // ── 공 — 농구공(세로선 · 가로선 · 좌우 호). `BuildBall` 과 같은 그림이다.
                float lx = x - ballCenter.x;
                float ly = y - ballCenter.y;
                float d = Mathf.Sqrt(lx * lx + ly * ly) - ballRadius;

                if (d > 0f)
                    return Color.clear;

                if (d > -Stroke)
                    return Black;

                Color ball = Color.Lerp(BallTop, BallBottom,
                                        Mathf.Clamp01((ly + ballRadius) / (ballRadius * 2f)));

                float bhx = (lx + ballRadius * 0.42f) / (ballRadius * 0.30f);
                float bhy = (ly + ballRadius * 0.46f) / (ballRadius * 0.22f);

                if (bhx * bhx + bhy * bhy < 1f)
                    ball = BallHighlight;

                const float LineHalf = 2.4f;

                if (Mathf.Abs(lx) <= LineHalf || Mathf.Abs(ly) <= LineHalf)
                    return Black;

                for (int side = -1; side <= 1; side += 2)
                {
                    float ax = lx + side * ballRadius * 1.25f;
                    float rr = Mathf.Sqrt(ax * ax + ly * ly);

                    if (Mathf.Abs(rr - ballRadius * 1.32f) <= LineHalf)
                        return Black;
                }

                return ball;
            });
        }

        private static bool InEllipse(float x, float y, Vector2 center, Vector2 half)
        {
            float dx = (x - center.x) / half.x;
            float dy = (y - center.y) / half.y;

            return dx * dx + dy * dy <= 1f;
        }

        /// <summary>마우스 아이콘 — 소스 59×71 → 60×72. 좌버튼 눌림/떼임 2프레임 [실측 <c>leftClic</c> 2f].</summary>
        private static Texture2D BuildMouse(bool pressed)
        {
            const int W = 60;
            const int H = 72;

            return Bake(W, H, (x, y) =>
            {
                float lx = x - W * 0.5f;
                float ly = y - H * 0.5f;

                float d = SdRoundRect(lx, ly, W * 0.5f - 1f, H * 0.5f - 1f, 24f);

                if (d > 0f)
                    return Color.clear;

                if (d > -4.5f)
                    return Black;

                // 버튼 분할선 (세로 + 가로)
                if (Mathf.Abs(lx) <= 1.6f && ly < 0f)
                    return Black;

                if (Mathf.Abs(ly + H * 0.16f) <= 1.6f)
                    return Black;

                bool leftButton = lx < 0f && ly < -H * 0.16f;

                if (pressed && leftButton)
                    return Black;

                return White;
            });
        }

        /// <summary>
        /// <c>1 PLAYER</c> 카드 — world bbox 400×400 [UIUX 2-a].
        ///
        /// <para>
        /// ⚠ <b>헤더 문자는 굽지 않는다.</b> 원본은 스프라이트에 구워져 있으나(<c>04_UIUX규칙.md</c> 4-c)
        /// 우리는 서체 자체가 대체(의도된 차이 #5)라 문자를 <b>TMP 로 얹는다</b> — 굽든 얹든 자형은 원본과 다르고,
        /// 얹으면 <b>2 PLAYERS 를 숨기는 확정표 C</b> 와 다국어 대응이 같은 자리에서 처리된다. PD 등재 요청 항목이다.
        /// </para>
        /// </summary>
        private static Texture2D BuildPlayerCard()
        {
            const int S = 400;
            const float Radius = 46f;
            const float HeaderH = S * 0.20f;
            const float BaseH = S * 0.075f;

            return Bake(S, S, (x, y) =>
            {
                float d = SdRoundRect(x - S * 0.5f, y - S * 0.5f, S * 0.5f - 1f, S * 0.5f - 1f, Radius);

                if (d > 0f)
                    return Color.clear;

                if (d > -Stroke)
                    return Black;

                if (y < HeaderH)
                    return CardHeader;

                if (y > S - BaseH)
                    return CardBase;

                // 본문 흰 바탕 위의 «공(우상) · 블롭(좌하) · 블롭 그림자»
                float bx = x - S * 0.62f;
                float by = y - S * 0.36f;
                float br = Mathf.Sqrt(bx * bx + by * by) - 52f;

                if (br <= 0f)
                {
                    if (br > -Stroke)
                        return Black;

                    if (Mathf.Abs(bx) <= 3f || Mathf.Abs(by) <= 3f)
                        return Black;

                    return Color.Lerp(BallTop, BallBottom, Mathf.Clamp01((by + 52f) / 104f));
                }

                float sx = (x - S * 0.40f) / 62f;
                float sy = (y - S * 0.76f) / 13f;

                bool inShadow = sx * sx + sy * sy <= 1f;

                float lx = x - S * 0.40f;
                float ly = y - S * 0.66f;
                float narrow = Mathf.Clamp01((ly + 52f) / 104f);
                float halfW = Mathf.Lerp(38f, 60f, Smooth(narrow));
                float bd = SdRoundRect(lx, ly, halfW, 50f, Mathf.Min(halfW, 34f));

                if (bd <= 0f)
                {
                    if (bd > -Stroke)
                        return Black;

                    return Color.Lerp(BlobLight, BlobDeep, Smooth(Mathf.Clamp01((ly + 50f) / 100f)));
                }

                if (inShadow)
                    return Black;

                return White;
            });
        }

        /// <summary>손가락 커서 — 흰 검지 아이콘 + 검은 외곽선. 표시 100×100 world [UIUX 2-a].</summary>
        private static Texture2D BuildHandCursor()
        {
            const int S = 100;

            return Bake(S, S, (x, y) =>
            {
                float lx = x - S * 0.42f;
                float ly = y - S * 0.5f;

                // ⚠ 부호를 한 번 틀려 「집/화살표」로 구웠다 — 검지는 «위», 주먹은 «아래»다.
                //   SdRoundRect 의 두 번째 인자는 «중심에서의 거리»라 중심이 −18 이면 (ly + 18) 이다.

                // 검지 — 위로 뻗은 라운드 막대 (중심 ly = −18)
                float finger = SdRoundRect(lx, ly + 18f, 11f, 28f, 11f);

                // 주먹 — 아래 라운드 사각 (중심 ly = +20)
                float fist = SdRoundRect(lx + 5f, ly - 20f, 21f, 20f, 9f);

                // 엄지 — 주먹 왼쪽에 붙은 작은 돌기
                float thumb = SdRoundRect(lx + 22f, ly - 10f, 8f, 12f, 8f);

                float d = Mathf.Min(finger, Mathf.Min(fist, thumb));

                if (d > 0f)
                    return Color.clear;

                return d > -5f ? Black : White;
            });
        }

        /// <summary>하단 그라디언트 (<c>spr_gradiant</c>) — 흰색→투명. 가로로 늘려 쓴다.</summary>
        private static Texture2D BuildGradient()
        {
            const int W = 4;
            const int H = 256;

            return Bake(W, H, (x, y) =>
            {
                float t = Mathf.Clamp01(y / (H - 1f));
                return new Color(1f, 1f, 1f, t * t);
            });
        }

        // ══════════════════════════════════════════════════ 굽기 유틸

        /// <summary>
        /// 라운드 사각의 부호 거리 (안쪽이 음수). 원본은 <b>전부 라운드</b>라 각진 코너가 없다
        /// [06_리소스 「공통 스타일」].
        /// </summary>
        private static float SdRoundRect(float px, float py, float halfW, float halfH, float radius)
        {
            radius = Mathf.Min(radius, Mathf.Min(halfW, halfH));

            float qx = Mathf.Abs(px) - (halfW - radius);
            float qy = Mathf.Abs(py) - (halfH - radius);

            float ax = Mathf.Max(qx, 0f);
            float ay = Mathf.Max(qy, 0f);

            return Mathf.Sqrt(ax * ax + ay * ay) + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        /// <summary>선분까지의 거리. 잎맥·그물처럼 «꺾은선 + 폭»으로 그리는 것에 쓴다.</summary>
        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;

            float denominator = Vector2.Dot(ba, ba);
            float h = denominator <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(pa, ba) / denominator);

            return (pa - ba * h).magnitude;
        }

        private static float Frac(float v)
        {
            return v - Mathf.Floor(v);
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// 슈퍼샘플로 덮개를 평균낸다 — 이진 판정을 그대로 쓰면 곡선에 계단이 남고,
        /// 원본(벡터 스프라이트)보다 거칠어진다.
        /// </summary>
        private static Texture2D Bake(int width, int height, Func<float, float, Color> sample)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[width * height];

            float step = 1f / SuperSample;
            float half = step * 0.5f;
            int samples = SuperSample * SuperSample;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;

                    for (int sy = 0; sy < SuperSample; sy++)
                    {
                        for (int sx = 0; sx < SuperSample; sx++)
                        {
                            Color c = sample(x + sx * step + half, y + sy * step + half);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }

                    a /= samples;

                    // 알파로 나눠 «색»을 되살린다 (프리멀티플라이 해제).
                    Color32 result = a <= 0f
                        ? new Color32(255, 255, 255, 0)
                        : new Color32(
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(r / samples / a) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(g / samples / a) * 255f),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(b / samples / a) * 255f),
                            (byte)Mathf.RoundToInt(a * 255f));

                    // ⚠ 유니티 텍스처는 아래에서 위로 쌓인다 — 세로를 뒤집어 넣는다.
                    //   원본(C3 캔버스)은 «아래가 +y» 라 샘플 좌표를 그대로 쓰고 여기서만 뒤집는다.
                    pixels[(height - 1 - y) * width + x] = result;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }

        private static void WritePng(string directory, string fileName, Texture2D texture)
        {
            File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color c);
            return c;
        }
    }
}
