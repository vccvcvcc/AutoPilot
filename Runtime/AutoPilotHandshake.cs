namespace AutoPilot.Core
{
	/// <summary>
	/// エディタ側の自律ループ基盤(AutoPilot.Editor)と、ゲーム側のブートストラップが
	/// 直接参照し合わずに握手するための共有定数。ランタイムアセンブリに置くことで、
	/// エディタ拡張とゲームのランタイムコードの両方から参照できる。
	/// </summary>
	public static class AutoPilotHandshake
	{
		/// <summary>
		/// この真偽値のEditorPrefsが立っていると、Play開始時にゲーム側ブートが自分を起動する。
		/// エディタ側ブリッジがPlay突入前にtrueにする。手動トグルにも使う。
		/// </summary>
		public const string EnableOnPlayPrefKey = "AutoPilot.EnableOnPlay";

		/// <summary>
		/// ブリッジが処理中コマンドのIDを入れるSessionStateキー。ゲーム側ブートはこれを
		/// レポートのセッションIDとして引き継ぐ(コマンドとレポートを1:1で対応させる)。
		/// </summary>
		public const string CommandIdSessionKey = "AutoPilot.CommandId";

		/// <summary>ブリッジ通信ディレクトリ名(プロジェクトルート直下)。</summary>
		public const string BridgeDirName = "AutoPilotBridge";
	}
}
