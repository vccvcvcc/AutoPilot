# Changelog

このパッケージの主な変更を記録する。書式は Keep a Changelog に準拠。

## [0.1.0] - 2026-07-18

### Added
- 初版抽出。あるオープンソース3Dゲームで開発した汎用部分を独立パッケージ化。
- `AutoPilot.Core`: Blackboard / ISensor / BotTask + コンビネータ(Sequence/Try/Timeout/
  WaitFor/TapButton/EnumeratorTask 等)/ Director(フェーズ別ハンドラFSM)/ BotRunner
  (常駐+デバッグオーバーレイ)/ BotReporter(JSONセッションレポート)/ AutoPilotHandshake(共有定数)。
- `AutoPilot.InputSim`: VirtualGamepad(Input Systemへ仮想デバイスを追加し QueueStateEvent で
  デバイスレベル入力注入)。
- `AutoPilot.Nav`: NavigateTo(NavMesh経路→カメラ相対スティック変換+スタック復旧)/
  WalkDirection(NavMesh不要の直進+閉塞検出)。
- `AutoPilot.Editor`: 自律実行ループのブリッジ(command.json/status.json のファイルIPC、
  Refresh→コンパイル→Play→停止、ドメインリロードをSessionStateで越える)/ NavMesh一括ベイク /
  Enable On Play トグル / Settings(起動シーン・ベイク対象フォルダ)。ChopChop固有の結合を除去し汎用化。
- `Samples~/SmokeTest`: 入力注入とBotRunner起動を確認する最小アダプタ。

### Notes
- ゲーム固有アダプタ(Sensor実装・シナリオ・ブート)はパッケージに含めない(消費側で書く)。
- `.meta` 未同梱。初回Unity取り込みで生成し、コミットしてGUIDを固定すること。
