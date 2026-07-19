# Changelog

このパッケージの主な変更を記録する。書式は Keep a Changelog に準拠。

## [Unreleased]

### Fixed
- 前回の重複解消で `AutoPilot.Core`/`AutoPilot.InputSim`/`AutoPilot.Nav` の3つの.asmdefを
  `Runtime/` 直下に同居させてしまっていた。Unityはフォルダ単位でアセンブリの所有権を
  決めるため、同一フォルダに複数の.asmdefがあるとどのC#ファイルがどのアセンブリに
  属すか判別できない(特に `Unity.InputSystem` 参照が必要な `VirtualGamepad.cs` が
  無関係の `AutoPilot.Core.asmdef` 側に巻き込まれると型解決エラーになる)。
  `AutoPilot.InputSim.asmdef`+`VirtualGamepad.cs` を `Runtime/InputSim/` へ、
  `AutoPilot.Nav.asmdef`+`NavigateTo.cs`+`Steering.cs` を `Runtime/Nav/` へ
  (.metaごと`git mv`でGUIDを保持したまま)移動し、フォルダごとに.asmdefが1つになるよう戻した。
  `AutoPilot.Core.asmdef` は他に同居するアセンブリが無くなったため `Runtime/` 直下のままでよい。
- `Runtime/Core`・`Runtime/InputSim`・`Runtime/Nav` 配下に旧構成のファイルが残っており、
  フラット化後の `Runtime/*.cs` と同名クラス・同名アセンブリ(`AutoPilot.Core`/
  `AutoPilot.InputSim`)が二重定義される状態だった(Unity取り込み時にアセンブリ名衝突で
  コンパイル不能)。調査の結果、フラット側(`Runtime/*.cs`、.meta同梱)は
  "AnyRPG integration" コミットで持ち込まれた簡略版で、`RepeatForever`/`TapButton`/
  `EnumeratorTask`/`Try`/`WaitFor` 等のコンビネータや`Director`の再試行クールダウン・
  例外捕捉が欠落した退行版だったと判明。入れ子側(`Runtime/Core` 等、meta無し)が
  v0.1.0本来のより完成度の高い実装だったため、**フラット側の.metaは維持したまま中身を
  入れ子側の内容で上書き**し、`Runtime/Core`・`Runtime/InputSim` を削除。`Runtime/Nav` も
  同様にフラット化し(新規meta採番)、ディレクトリを解消。
- `Samples~/AnyRPGAutoPilotAdapter.cs.meta` が本体`.cs`を持たない孤児メタになっていた
  (該当ファイルは同コミットで `SmokeTestAdapter.cs` へリネームされたがmetaだけ取り残されていた)
  ため削除。そのリネーム後の `Samples~/SmokeTestAdapter.cs`(package.jsonの`samples`に
  未登録の重複版)と、`Editor/AutoPilotRunControl.cs` と機能が重複する
  `Samples~/AutoPilotMenu.cs`(AnyRPG固有の名残)も削除。正式なサンプルは
  `Samples~/SmokeTest/` のみ。

### Docs
- AUTONOMY_AND_QA.md に A-8「起床を"完了駆動"にする(定期起床を避ける)」を追加。
  外部エージェントをタイマーでなく再生完了で起こす監視パターン、定期起床の原因、
  バックグラウンド約10分制約、「1 runを9分以内に収めて完了時に1回だけ起床」させる推奨と
  長時間はrun連鎖で伸ばす方法を明記。READMEの自律駆動節からも参照。

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
