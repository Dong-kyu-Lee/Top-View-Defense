namespace TopViewDefense.Map
{
    /// <summary>
    /// "무엇을 플레이할지"를 씬 간에 넘기는 얇은 정적 홀더. (CLAUDE.md 2장 - 선택한 스테이지로 게임 시작)
    ///
    /// StageSelect의 버튼 클릭 시 <see cref="SelectedStage"/>에 담고 PlayScene을 로드하면,
    /// 씬 오브젝트가 아니라서 로드 후에도 값이 살아 있어 <see cref="MapBuilder"/>가 읽는다.
    /// PlayScene을 직접 열어 디버깅할 때는 <c>null</c>이므로 MapBuilder가 폴백 경로를 쓴다.
    ///
    /// MapBuilder가 상위 계층(Core)을 참조하지 않도록 Map 네임스페이스에 둔다.
    /// </summary>
    public static class StageSession
    {
        /// <summary>StageSelect에서 선택한 스테이지. MapBuilder가 우선 로드한다. 없으면 null.</summary>
        public static StageData SelectedStage { get; set; }
    }
}
