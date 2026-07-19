# AutoPilot Framework

Unityゲームに後付けできる**汎用の自動プレイ(ボット)フレームワーク**。
ポート&アダプタ設計により、コアはゲームを一切参照せず、ゲームごとに薄いアダプタを
書くだけで自動プレイを実現する。外部AIエージェントがゲームの実行・結果確認・コード修正を
無人で反復する**自律実行ループ**も同梱する。

このリポジトリは Unity Package Manager (UPM) パッケージ。実績として、あるオープンソースの
3Dアクションアドベンチャーを、タイトル画面からエンディングまで完全自動でクリアした
(その事例と設計思想の詳細は [Documentation~/FRAMEWORK.md](Documentation~/FRAMEWORK.md))。

---

## 何が入っているか

| アセンブリ | 内容 | 依存 |
|---|---|---|
| `AutoPilot.Core` | Blackboard(観測ストア)/ ISensor / BotTask とコンビネータ / Director(フェーズ別FSM)/ BotRunner(常駐+デバッグオーバーレイ)/ BotReporter(JSONレポート) | なし |
| `AutoPilot.InputSim` | VirtualGamepad(Input Systemへ仮想デバイスを追加し `QueueStateEvent` で入力注入) | Core, Input System |
| `AutoPilot.Nav` | NavigateTo(NavMesh経路→カメラ相対スティック変換+スタック復旧)/ WalkDirection | Core |
| `AutoPilot.Editor` | 自律実行ループのブリッジ(ファイルIPC)/ NavMesh一括ベイク / Enable On Play トグル | Core (Editorのみ) |

**入っていないもの**: 特定ゲーム向けのアダプタ(Sensor実装・シナリオ・ブート)。
これは消費側プロジェクトで書く。テンプレートは `Samples~/SmokeTest` と下記を参照。

---

## 導入

### インストール(git URL)

Unity の Package Manager → 「Add package from git URL」に、このリポジトリのURLを入力:

```
https://github.com/<あなたのアカウント>/AutoPilotFramework.git
```

またはローカルに置いて `manifest.json` に:

```json
"com.nobu.autopilot": "file:../../AutoPilotFramework"
```

前提: プロジェクトに新Input System(`com.unity.inputsystem`)が入っていること。

### アダプタを書く(消費側プロジェクトでやること)

コアはゲームを知らないので、2つのポートを埋めるアダプタを書く。実装順:

1. **入力の疎通(最初にやる)** — ブート(下記)で `VirtualGamepad` + `BotRunner` を生成し、
   Playで「仮想パッドのスティックでキャラが歩く」を確認する。
2. **PhaseSensor** — ゲームの状態(GameManagerのenum、ロード済みシーン名、UI表示状態など)を
   `NormalizedPhase`(Title/Playing/Dialogue/Menu/Loading…)へ写像する。移植の本体。
3. **観測Sensor** — プレイヤー位置・カメラを `Keys.PlayerPosition` 等へ。進行目標があれば
   ゲーム固有キーへ導出。
4. **シナリオ** — `Director.SetHandler(phase, () => task)` でフェーズ別の行動を登録。
   `EnumeratorTask` で「yield return タスク / 秒数 / null」の逐次スクリプトとして書ける。
5. **ブート** — 下記の雛形。フラグ判定→センサ登録→シナリオ登録。

```csharp
using System.IO;
using AutoPilot.Core;
using AutoPilot.InputSim;
using UnityEngine;

public static class MyGameAutoPilotBoot
{
    private static BotRunner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_runner != null || !IsEnabled()) return;
        Application.runInBackground = true;               // 非フォーカスでも動く(自律ループ用)

        _runner = BotRunner.Create(new VirtualGamepad());
        _runner.Sensors.Add(new MyPhaseSensor());
        _runner.Sensors.Add(new MyPlayerSensor());
        // レポーターは最後(他Sensorの最新値を読む)。ブリッジと同じ命名規則でパスを作る:
        string sessionId = GetSessionId();
        string reportPath = Path.GetFullPath(Path.Combine(
            Application.dataPath, "..", AutoPilotHandshake.BridgeDirName, "Reports", $"report_{sessionId}.json"));
        _runner.Sensors.Add(new BotReporter(reportPath, sessionId, _runner.Context.Log));

        MyScenario.Install(_runner.Director);
    }

    private static bool IsEnabled()
    {
        foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-autopilot") return true;
#if UNITY_EDITOR
        return UnityEditor.EditorPrefs.GetBool(AutoPilotHandshake.EnableOnPlayPrefKey, false);
#else
        return false;
#endif
    }

    private static string GetSessionId()
    {
#if UNITY_EDITOR
        string id = UnityEditor.SessionState.GetString(AutoPilotHandshake.CommandIdSessionKey, "");
        if (!string.IsNullOrEmpty(id)) return id;
#endif
        return System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }
}
```

---

## 手動で走らせる

1. メニュー **Tools > AutoPilot > Enable On Play** をチェック
2. Play → `BotRunner` が起動し、Gameビュー左上にデバッグオーバーレイが出る
3. ビルドで動かすなら起動引数に `-autopilot`

## 自律実行ループ(外部エージェント連携)

エディタを開いたまま、プロジェクトルートの `AutoPilotBridge/` 経由で外部プロセス
(CLIエージェント等)が実行→結果確認→コード修正→再実行を無人で回せる。

```
AutoPilotBridge/command.json   外部が書く: {"id":"run-001","action":"run","durationSeconds":180}
AutoPilotBridge/status.json    ブリッジが書く: refreshing/compile-error/playing/done
AutoPilotBridge/Reports/       BotReporterが書くセッションレポート
```

`action` は `run`(Refresh→コンパイル→Play→時間経過で停止) / `stop` / `bake`(NavMesh一括ベイク)。
起動シーンやベイク対象フォルダは **Tools > AutoPilot > Settings** で設定する。
仕組みの詳細は [Documentation~/AUTONOMY_AND_QA.md](Documentation~/AUTONOMY_AND_QA.md)。

---

## ドキュメント

- [FRAMEWORK.md](Documentation~/FRAMEWORK.md) — 設計・全コンポーネント・移植手順・落とし穴
- [AUTONOMY_AND_QA.md](Documentation~/AUTONOMY_AND_QA.md) — 自律ループの内部機構と、開発と並行して
  QAに使う際の注意点(ゲームバグ/ボットバグの切り分け、回復がバグを隠す罠、監査モード論)

## ライセンス

Apache License 2.0([LICENSE](LICENSE))。

## メタファイルについて

このリポジトリはソース(.cs / .asmdef)を追跡し、`.meta` は含んでいない。初回にUnityへ
取り込むと `.meta` が生成される。**GUIDを安定させるため、生成された `.meta` をコミットして
固定する**こと(以降のバージョンから追跡対象になる)。
