namespace AutoPilot.Core
{
	/// <summary>
	/// ゲーム間で共通の正規化フェーズ。
	/// 各ゲームのアダプタ(Sensor)が、そのゲーム固有の状態をこの列挙に写像する。
	/// Directorはこのフェーズだけを見てハンドラを切り替える。
	/// </summary>
	public enum NormalizedPhase
	{
		Unknown,
		Boot,       // 起動直後の初期化中
		Title,      // タイトル/メインメニュー
		Loading,    // シーン遷移・ロード中(入力を受け付けない)
		Playing,    // 通常プレイ(戦闘含む)
		Dialogue,   // 会話中(送り操作が必要)
		Cutscene,   // カットシーン再生中
		Menu,       // ポーズ・インベントリ等のゲーム内メニュー
		Result,     // リザルト/ゲームオーバー画面
	}
}
