namespace JinHyung.BlumgiBounce
{
    /// <summary>
    /// 아트 <b>어드레서블 주소</b> 전수. 주소 = 파일명(확장자 제외) 이다 (<c>AddressableSetup</c> 규약).
    ///
    /// <para>
    /// ★ 이름에 <c>~Address</c> 를 붙이는 것은 <b>Resources 경로와 구분하기 위해서</b>다
    /// (CLAUDE.md 「로드 규칙」 — 두 경로가 섞이는 것은 의도된 것이고, 코드에서 어느 쪽인지 보여야 한다).
    /// </para>
    ///
    /// <para>
    /// ⚠ <b>UI 프리팹은 Resources 인데 아트는 어드레서블</b>이다. Resources 프리팹이 어드레서블 에셋을
    /// 직접 물면 <b>빌드에 중복 편입</b>되고 카테고리 단위 로드/해제를 우회한다 (ResourceRule) —
    /// 그래서 UI 프리팹은 스프라이트를 «비워 두고» <see cref="BlumgiUiArtBinder"/> 로 주입받는다.
    /// </para>
    /// </summary>
    public static class BlumgiArtAddress
    {
        // ── 월드 (Art/Game) — 프리팹도 어드레서블이라 프리팹이 직접 물어도 된다.
        /// <summary>
        /// 블록 스킨 «5프레임» 주소 [실측 <c>spr_BlockSimpleSkin</c>].
        /// ★ 예전 <c>block_base</c>/<c>block_pattern</c>/<c>block_cap</c> 세 장은 <b>기전 오독</b>이었다 —
        /// 원본은 한 장의 5프레임이고 색이 그림에 구워져 있다 (17회차 소스 픽셀 실측).
        /// </summary>
        public static readonly string[] BlockSkinAddresses =
        {
            "block_skin_0", "block_skin_1", "block_skin_2", "block_skin_3", "block_skin_4",
        };

        /// <summary>레벨 번호(1~5) → 스킨 프레임 [실측 462 인스턴스 · 예외 0건].</summary>
        public static readonly int[] BlockSkinFrameByLevelNo = { 0, 3, 1, 2, 4 };

        public const string BlobBodyAddress = "blob_body";
        public const string BlobEyesIdleAddress = "blob_eyes_idle";
        public const string BlobEyesAngryAddress = "blob_eyes_angry";

        /// <summary>발사 표정 (프레임 2) — 17회차가 소스 텍스처로 닫았다 [실측 50×47].</summary>
        public const string BlobEyesShoutAddress = "blob_eyes_shout";
        public const string BallAddress = "ball";
        public const string BallGhostAddress = "ball_ghost";
        public const string HoopRimAddress = "hoop_rim";
        public const string HoopNetAddress = "hoop_net";
        public const string GoalArrowAddress = "goal_arrow";
        public const string PalmLeafAddress = "palm_leaf";
        public const string PalmTrunkAddress = "palm_trunk";
        public const string GridTileAddress = "grid_tile";
        public const string SolidAddress = "solid";
        public const string ConfettiRectAddress = "confetti_rect";
        public const string SparkleAddress = "sparkle";

        /// <summary>
        /// ★ 골인 텔레포트 <c>FXteleport</c> — <b>6프레임 중 «그림이 있는» 5장</b> [23회차 소스 직독].
        /// <b>f5 는 완전 투명</b>(불투명 픽셀 0)이라 주소가 없다 — 그 50 ms 는 «살아 있는데 안 보이는» 구간이다.
        /// ⚠ <c>FXwinLight</c> 는 소스가 <b>1×1 순백</b>이라 <see cref="SolidAddress"/> 를 늘려 쓴다.
        /// </summary>
        public static readonly string[] TeleportAddresses =
        {
            "fx_teleport_0", "fx_teleport_1", "fx_teleport_2", "fx_teleport_3", "fx_teleport_4",
        };

        // ── UI (Art/UI) — Resources 프리팹이 «물면 안 되는» 쪽이다.
        public const string ButtonRetryAddress = "ui_btn_retry";
        public const string ButtonSoundOnAddress = "ui_btn_sound_on";
        public const string ButtonSoundOffAddress = "ui_btn_sound_off";
        public const string ButtonMapAddress = "ui_btn_map";
        public const string ButtonHomeAddress = "ui_btn_home";
        public const string DotEmptyAddress = "ui_dot_empty";
        public const string DotFullAddress = "ui_dot_full";
        public const string Panel9PAddress = "ui_panel_9p";
        public const string TutorialScreenAddress = "ui_tut_screen";
        /// <summary>
        /// 튜토 미니 그림 <b>컷 A(<c>HOLD</c>)</b> — 원본 <c>Sprite2 Animation 1</c> <b>f1 · 152 × 154</b>.
        /// </summary>
        public const string TutorialArtAddress = "ui_tut_art";

        /// <summary>
        /// 튜토 미니 그림 <b>컷 B(<c>SHOOT!</c>)</b> — 원본 <b>f2 · 230 × 258</b> [실측 25회차 소스 텍스처 직독].
        /// ⚠ <b>컷마다 «그림과 크기가 통째로» 바뀐다</b> — 한 장을 돌려 쓰면 컷 B 에서 안 커진다.
        /// </summary>
        public const string TutorialArtShootAddress = "ui_tut_art_b";
        public const string MouseUpAddress = "ui_mouse_up";
        public const string MouseDownAddress = "ui_mouse_down";
        public const string PlayerCardAddress = "ui_card";
        public const string HandCursorAddress = "ui_hand";
        public const string GradientAddress = "ui_gradient";

        /// <summary>
        /// 감사(양방향)와 배선이 함께 쓰는 전수 목록.
        /// ⚠ <b>여기에 없으면 아무도 로드하지 않는다</b> — 스프라이트를 추가하면 이 배열에도 넣는다.
        /// </summary>
        public static readonly string[] All =
        {
            BlockSkinAddresses[0], BlockSkinAddresses[1], BlockSkinAddresses[2],
            BlockSkinAddresses[3], BlockSkinAddresses[4],
            BlobBodyAddress, BlobEyesIdleAddress, BlobEyesAngryAddress, BlobEyesShoutAddress,
            BallAddress, BallGhostAddress,
            HoopRimAddress, HoopNetAddress, GoalArrowAddress,
            PalmLeafAddress, PalmTrunkAddress, GridTileAddress, SolidAddress,
            ConfettiRectAddress, SparkleAddress,
            TeleportAddresses[0], TeleportAddresses[1], TeleportAddresses[2],
            TeleportAddresses[3], TeleportAddresses[4],
            ButtonRetryAddress, ButtonSoundOnAddress, ButtonSoundOffAddress,
            ButtonMapAddress, ButtonHomeAddress,
            DotEmptyAddress, DotFullAddress,
            Panel9PAddress, TutorialScreenAddress, TutorialArtAddress, TutorialArtShootAddress,
            MouseUpAddress, MouseDownAddress,
            PlayerCardAddress, HandCursorAddress, GradientAddress,
        };
    }
}
