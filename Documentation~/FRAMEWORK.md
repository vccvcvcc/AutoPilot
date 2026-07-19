# AutoPilot フレームワーク 完全ガイド

Unityゲームに後付けできる自動プレイ(ボット)フレームワークと、
外部AIエージェント(Claude Code)がゲームの実行・結果確認・コード修正を
無人で反復する自律ループシステムの詳細ドキュメント。

実績: Chop Chop (Unity Open Project #1) をタイトル画面からエンディング(勝利カットシーン)まで
完全自動でクリア。自律ループ7回の反復で5つの実バグを検出・修正して到達した。

---

## 目次

1. [設計原則](#1-設計原則)
2. [アセンブリ構成とファイルマップ](#2-アセンブリ構成とファイルマップ)
3. [コンポーネント詳細](#3-コンポーネント詳細)
4. [実行時の流れ(1フレーム/フェーズ遷移/タスク)](#4-実行時の流れ)
5. [自律ループのしくみ(Claude Code ⇔ Unity)](#5-自律ループのしくみ)
6. [別プロジェクトへの導入手順](#6-別プロジェクトへの導入手順)
7. [既知の落とし穴と教訓](#7-既知の落とし穴と教訓)
8. [開発ログ(run-001〜007)](#8-開発ログ)

---

## 1. 設計原則

### 1.1 ポート&アダプタ(コアはゲームを知らない)

フレームワークは「ゲーム非依存のコア」と「ゲームごとのアダプタ」に分離されている。
コア(Core / InputSim / Nav)はゲームのクラスを一切参照せず、次の2つのポートだけを定義する:

- **観測ポート** `ISensor` — ゲーム状態を読んで `Blackboard` に正規化された値を書く
- **操作ポート** `IVirtualController` — スティック+ボタンの抽象ゲームパッド

別ゲームへの移植は、この2ポートの実装(アダプタ)を書き換えるだけで済む。

### 1.2 入力注入は「デバイスレベル」を既定とする

擬似入力の注入には3つのレベルがあり、上ほど忠実で、下ほど楽だが検証価値が低い:

| レベル | 方法 | 特徴 |
|---|---|---|
| デバイス | Input Systemに仮想Gamepadを追加し状態イベントを流す | ゲーム改変ゼロ。バインディング解決・ActionMapの有効/無効・UI入力モジュールまで実プレイヤーと同一経路 |
| アクション | 入力抽象層(InputReader等)のイベントを直接発火 | 楽だが、入力ゲート(メニュー中は無効等)を素通りして偽陽性を生む |
| コマンド | ゲームのAPIを直接呼ぶ | もはや入力ではない。UIなど壊れやすい箇所のフォールバック専用 |

本フレームワークは**デバイスレベルが既定**。UIがどうしても通らない箇所だけ、
アダプタ内で明示的にコマンドレベルへフォールバックする(例: タイトルの `UIMainMenu.NewGameButton()` 直呼び)。

### 1.3 観測はBlackboardに一元化

ボットの頭脳はゲームを直接見ない。Sensor群が毎フレーム `Blackboard` に書いた
正規化値(`NormalizedPhase`、プレイヤー位置、目標情報…)だけを読む。
ゲーム側の実装変更の影響はSensorだけで吸収できる。

### 1.4 フェーズ遷移が最優先の割り込み

最上位の `Director` は正規化フェーズ(Title/Playing/Dialogue/Menu/Loading…)を監視し、
フェーズが変わった瞬間に実行中タスクを `Cancel` して入力を中立化し、次のハンドラを起動する。
このため各ハンドラは「そのフェーズが続いている前提」の素直なループとして書ける。

### 1.5 信頼性装置は一級市民

自動プレイは「詰まる」のが日常なので、最初から組み込む:
全タスクにタイムアウト(`Timeout`)、進捗ウォッチドッグ(目的地への**接近量**で判定)、
段階的復旧(ジャンプ→ランダム迂回のエスカレーション)、失敗を握りつぶして続行する `Try`、
中断時に必ず入力を解放する `OnStop` 規約。

### 1.6 コンテンツ欠落への「シム」は隔離して明示する

未完成ゲームで進行不能な箇所(タグ未設定・入手経路の無いアイテム等)は、
`ChopChopShims` 1クラスに隔離し、発動を必ず `[SHIM]` プレフィックスでログする。
「入力で正しく到達した」のか「補完で越えた」のかをレポート上で区別できることが重要。

---

## 2. アセンブリ構成とファイルマップ

```
Assets/AutoPilot/
├── Core/                     [asmdef: AutoPilot.Core — ゲーム非依存]
│   ├── Blackboard.cs         型付きキーの観測値ストア + 標準キー(Keys)
│   ├── NormalizedPhase.cs    正規化フェーズ列挙
│   ├── Sensors.cs            ISensor / SensorHub(例外隔離Tick, DisposeAll)
│   ├── IVirtualController.cs 操作ポート + BotButton列挙
│   ├── BotTask.cs            Tick型協調タスク基底(OnStart/OnTick/OnStop/Cancel)
│   ├── Tasks.cs              コンビネータ(Sequence/RepeatForever/Try/Timeout/
│   │                          WaitFor/WaitSeconds/Do/TapButton/EnumeratorTask)
│   ├── BotContext.cs         共有コンテキスト(Blackboard/Controller/Log/Activity)
│   ├── Director.cs           フェーズ別ハンドラFSM(周回ボットの最上位)
│   ├── BotRunner.cs          常駐MonoBehaviour(Tick駆動+デバッグオーバーレイ)
│   ├── BotLog.cs             プレフィックス付きログ+購読イベント
│   └── BotReporter.cs        セッション結果のJSONレポーター(ISensor実装)
├── InputSim/                 [asmdef: AutoPilot.InputSim — Input System依存]
│   └── VirtualGamepad.cs     仮想Gamepadデバイス(QueueStateEvent注入)
├── Nav/                      [asmdef: AutoPilot.Nav — Core依存]
│   ├── NavigateTo.cs         NavMesh経路→スティック変換+スタック復旧
│   └── Steering.cs           カメラ相対変換 + WalkDirection(直進+閉塞検出)
├── Adapter/                  [asmdefなし → Assembly-CSharp。ゲーム参照可]
│   ├── ChopChopSensors.cs    Player/Camera/PhaseのSensor
│   ├── ChopChopQuestSensor.cs 次ステップ導出+対象NPC探索+ゾーン判定Sensor
│   ├── ChopChopLocationSensor.cs 現在ロケーション+出口観測
│   ├── ChopChopWorldMap.cs   ロケーション接続グラフ/NPC配置/BFSルーティング(静的知識)
│   ├── ChopChopShims.cs      コンテンツ欠落補完(調理・レシピ付与)
│   ├── ChopChopScenario.cs   フェーズ別ハンドラ(シナリオ本体)
│   ├── AutoPilotBoot.cs      起動処理(RuntimeInitializeOnLoadMethod)
│   └── Editor/
│       ├── AutoPilotMenu.cs           Tools > AutoPilot > Enable On Play
│       ├── AutoPilotNavMeshBaker.cs   NavMesh一括ベイクツール
│       └── AutoPilotBridgeService.cs  外部エージェント連携ブリッジ(常駐)
└── (プロジェクトルート)/AutoPilotBridge/   ← ブリッジの通信ディレクトリ(Assets外)
    ├── command.json          外部→エディタ へのコマンド
    ├── status.json           エディタ→外部 への状態通知
    └── Reports/report_*.json ランタイムのセッションレポート
```

依存方向: `Adapter → (Core, InputSim, Nav)` / `InputSim → Core` / `Nav → Core`。
ゲーム本体のスクリプトがAssembly-CSharp(asmdefなし)にある場合、
アダプタも同じくasmdefなしで置くことでゲームクラスを参照できる(asmdefアセンブリから
Assembly-CSharpは参照できないため)。

---

## 3. コンポーネント詳細

### 3.1 Blackboard(観測値ストア)

- `BlackboardKey<T>` は参照同一性で比較される型付きキー。`static readonly` で一度だけ定義して共有する
- 標準キー(`Keys`): `Phase` / `PlayerExists` / `PlayerPosition` / `CameraTransform`
- ゲーム固有キーはアダプタ側で追加定義する(Chop Chopでは `ObjectiveStep` /
  `ObjectivePosition` / `CanInteract` / `CurrentLocation` / `LocationExits` /
  `InteractAttempts` など)
- **タスク再起動をまたいで保持したい状態(試行カウンタ等)はローカル変数ではなく
  Blackboardに置く**。フェーズ遷移でハンドラは毎回作り直されるため(run-001の教訓)

### 3.2 NormalizedPhase(正規化フェーズ)

`Unknown / Boot / Title / Loading / Playing / Dialogue / Cutscene / Menu / Result`。
どのゲームでも「今ボットがすべき行動の種類」はこの粒度で分類できる、という仮説に基づく共通語彙。
アダプタのPhaseSensorがゲーム固有の状態(Chop ChopではGameStateSOの7状態+ロード済みシーン)を
これに写像する。

### 3.3 BotTask(協調タスク)

毎フレーム `Tick(ctx)` されて `Running / Success / Failure` を返す軽量タスク。

- `OnStart` → `OnTick`(毎フレーム) → 終了時 `OnStop(status)`
- 外部からの中断は `Cancel(ctx)`(実行中なら `OnStop(Failure)` が呼ばれる)
- **規約: 押したボタン・倒したスティックは `OnStop` で必ず解放する**(中断安全)
- `ctx.Activity` に「今何をしているか」を毎Tick書くとオーバーレイに表示される

コンビネータ(Tasks.cs):

| タスク | 意味 |
|---|---|
| `Sequence(a,b,c)` | 順次実行。子の失敗で全体失敗 |
| `RepeatForever(factory)` | 無限繰り返し(フェーズ遷移のCancelで終了) |
| `Try(child)` | 子の失敗をSuccessに変換(周回を止めない) |
| `Timeout(child, sec)` | 制限時間超過で子をCancelしてFailure |
| `WaitFor(pred, sec, failOnTimeout)` | 条件待ち(UnityEngine.WaitUntilと名前衝突するためWaitFor) |
| `WaitSeconds(sec)` / `Do(action)` | 時間待ち / 1フレーム副作用 |
| `TapButton(btn, hold)` | 押して離す。中断時も必ず離す |
| `EnumeratorTask(name, factory)` | IEnumeratorで逐次シナリオを書くためのアダプタ |

`EnumeratorTask` のyield規約: `null`=1フレーム待ち / `float・int`=秒待ち /
`BotTask`=完了まで実行(失敗は伝播)。進行ボットの手順書はこれで書くのが最も読みやすい:

```csharp
private static IEnumerator TitleRoutine(BotContext ctx)
{
    yield return 1.5f;                        // フェードイン待ち
    yield return new TapButton(BotButton.South);
    yield return new WaitFor(c => c.Blackboard.Get(Keys.Phase) != NormalizedPhase.Title, 20f);
}
```

### 3.4 Director(フェーズ別ハンドラFSM)

周回ボットの最上位。`SetHandler(phase, () => task)` で登録されたハンドラを管理する。

- フェーズが変わったら: 実行中タスクをCancel → コントローラ中立化 → 新フェーズのハンドラ起動
- ハンドラがSuccess: 同フェーズに留まる間は再実行しない
- ハンドラがFailure/例外: クールダウン(既定3秒)後に再実行
- 「閉じるまでループし続ける」種類のハンドラ(メニュー閉じ・会話送り)は
  無限ループで書いてよい — フェーズ遷移のCancelが終了条件になる

### 3.5 BotRunner(常駐ランナー+オーバーレイ)

`BotRunner.Create(controller)` で `DontDestroyOnLoad` のGameObjectを生成。
毎フレーム `SensorHub.Tick(blackboard)` → `Director.Tick(ctx)` の順に駆動する
(Sensorが先=タスクは常に今フレームの観測値を見る)。

OnGUIオーバーレイ(`BotRunner.OverlayVisible` で切替)には
フェーズ/実行中タスク+経過秒/activity/目標/仮想パッドの実出力(スティック値・押下ボタン)/
プレイヤー座標/直近ログ8件を表示。「ボットが考えていない」のか「入力が届いていない」のかを
画面だけで切り分けられる。

### 3.6 BotReporter(構造化レポート)

ISensorとして最後に登録し、フェーズ遷移・目標の変化・全ボットログ・Unityの例外/エラー・
移動距離をタイムラインとしてJSONに記録する。5秒ごとにフラッシュ(クラッシュ耐性)。
外部エージェントはこのファイルだけで結果判定できる。infoイベントは1500件で間引き、
20m以上の瞬間移動はテレポートとみなして移動距離に数えない。

### 3.7 VirtualGamepad(デバイスレベル注入)

```csharp
_device = InputSystem.AddDevice<Gamepad>("AutoPilotGamepad");
// 状態変更のたびに:
var state = new GamepadState { leftStick = ..., buttons = ... };
InputSystem.QueueStateEvent(_device, state);
```

- OSやウィンドウフォーカスに依存しない合成イベントとして、次のInput System更新で処理される
- 実機パッドとまったく同じ経路(バインディング→ActionMap→コールバック/UI入力モジュール)を通る
- 状態は保持型: スティックを倒したら戻すまで倒れたまま。`NeutralAll()` で全解放
- 旧Input Managerのゲームでは使えない → `IVirtualController` の別実装で対応(§6.4)

### 3.8 Nav(移動)

**NavigateTo** — NavMesh経路追従。1秒ごとに再計算し、次コーナーへの世界方向を
**カメラのforward/right平面基底に射影してスティック値へ変換**(3人称カメラ相対移動の要):

```csharp
stick = new Vector2(Dot(worldDir, cameraRightFlat), Dot(worldDir, cameraForwardFlat));
```

進捗ウォッチドッグ内蔵: 残距離が4秒縮まらなければジャンプで復旧を試み、
それでも駄目なら Failure(上位が `Try`/`Timeout` で包んで続行する)。

**WalkDirection** — NavMesh不要の直進歩行。1秒ごとに移動量を確認し、
閉塞していたらジャンプ→それでも駄目なら早期終了して呼び出し側に方向を選び直させる。

**部分経路への対処(アダプタのApproachLeg)** — NavMeshが分断されている場合、
`path.status != PathComplete` を検出して「届く終端まで経路追従 → 残りは直進」に
切り替える。これが無いと島の端で永久スタックする(run-006の教訓)。

### 3.9 Chop Chopアダプタ(ゲーム固有部の実例)

- **PhaseSensor**: MainMenuシーンのロード有無 → Title、GameStateSO(Resources.
  FindObjectsOfTypeAllで取得=アセット手動ワイヤリング不要)→ 各フェーズへ写像
- **QuestSensor**: QuestManagerSOの現在ステップはprivateなので、QuestlineSO資産群の
  IsDoneフラグから「最初の未完了ステップ」を同じ規則で導出。対象NPCはシーン上の
  StepControllerの `_actor`(リフレクション)と照合して位置を得る
- **InteractionSensor**: InteractionManagerの `_potentialInteractions`(リフレクション)で
  「今Interactが効くか」を観測。距離ではなくゲーム本体と同じゾーン判定を使うのが肝
  (近くに居るのにカウンター越しで話せない、を正しく検出)
- **LocationSensor + WorldMap**: シーン内のLocationExit(行き先はリフレクション)を観測し、
  静的解析で作った接続グラフ(9ロケーション)のBFSで次に向かう出口を決める
- **Scenario**: フェーズ別ハンドラ。Playing = 「次ステップの必要アイテムを調理チェーンで
  確保 → 対象NPCのロケーションへ移動 → ゾーンに入って話しかける」/
  Dialogue = 「選択肢が出たらWinningChoice優先で選ぶ、無ければ決定ボタン送り」/
  Menu = 閉じ操作巡回 / Title = New Game開始
- **Shims**: §1.6参照。調理(タグ付きポット不在)と入手経路の無いレシピ/素材のみ

---

## 4. 実行時の流れ

### 4.1 起動

```
[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
AutoPilotBoot.Bootstrap()
  ├─ 有効化判定(エディタ: EditorPrefs / ビルド: -autopilot 引数)
  ├─ Application.runInBackground = true   (非フォーカスでも停止しない)
  ├─ new VirtualGamepad()                 (仮想デバイス追加)
  ├─ BotRunner.Create(controller)         (常駐GameObject)
  ├─ Sensor登録(依存順: Player/Camera → Phase → Quest/Interaction/Location → Reporter)
  └─ ChopChopScenario.Install(director)   (フェーズ別ハンドラ登録)
```

シーンへの組み込みは一切不要。どのシーンからPlayしても起動する。

### 4.2 毎フレーム

```
BotRunner.Update()
  ├─ SensorHub.Tick(blackboard)     全Sensorが観測値を更新(例外は個別に隔離)
  └─ Director.Tick(ctx)
       ├─ フェーズ変化? → 実行中タスクCancel + NeutralAll + ハンドラ切替
       └─ 現在タスクをTick → タスク内でController操作(即座にQueueStateEvent)
                                    ↓
              Input Systemが次の更新でイベント処理 → ゲームの入力コールバック発火
```

ボットの決定からゲーム反映まで最大1フレームの遅延がある(問題にならない)。

### 4.3 タスクのライフサイクル例(NPCに話しかける)

```
PlayingRoutine (EnumeratorTask)
  ├─ yield ApproachLeg(...)     NavigateTo/WalkDirection でNPCへ
  ├─ CanInteract==true を確認   (ゾーン判定Sensorの値)
  ├─ yield TapButton(East)      Interact押下 → 会話開始
  └─ (ゲームがDialogue状態へ) → PhaseSensorがDialogueを書く
       → Director がPlayingRoutineをCancel(スティック等はOnStopで解放)
       → AdvanceRoutine起動(決定ボタン送り/選択肢選択)
       → 会話終了 → Playingへ戻る → PlayingRoutine再起動(続きはBlackboardの状態から)
```

---

## 5. 自律ループのしくみ

### 5.1 全体像

外部エージェント(Claude Code)とUnityエディタは**プロジェクトルートの
`AutoPilotBridge/` ディレクトリ経由のファイルベースIPC**で通信する。
ソケットもプラグインも不要で、エディタを開いておくだけでよい。

```
┌────────────── Claude Code(CLI エージェント)──────────────┐
│  ① command.json を書く {"id":"run-007","action":"run",...}   │
│  ⑥ status.json / report_*.json を読んで結果判定              │
│  ⑦ 問題があればC#コードをEdit → 次のコマンドへ(①に戻る)    │
└──────────────┬───────────────────────────────┘
               │ ファイル読み書き(ポーリング)
┌──────────────▼──────────── Unityエディタ ────────────────┐
│  AutoPilotBridgeService([InitializeOnLoad]常駐、0.5秒間隔Poll)     │
│  ② 新コマンド検出 → AssetDatabase.Refresh()(=エージェントの      │
│     コード変更を取り込んでコンパイル)                              │
│  ③ コンパイルエラーあり → 全文をstatus.jsonへ書いて中止            │
│     エラーなし → Initializationシーンを開き EnterPlaymode()         │
│  ④ Play中: AutoPilotが自動プレイ、BotReporterが5秒ごとに           │
│     report_<id>.json へタイムラインを書き出す                       │
│  ⑤ durationSeconds経過 → ExitPlaymode() → status=done               │
└─────────────────────────────────────────────┘
```

### 5.2 ブリッジのプロトコル

**command.json**(エージェント→エディタ):

```json
{ "id": "run-007", "action": "run", "durationSeconds": 900 }
```

- `id`: 一意なら何でもよい。同じidは再処理されない(冪等性)
- `action`: `run`(Refresh→コンパイル→Play→時間経過で停止) /
  `stop`(即時Play停止) / `bake`(NavMesh一括ベイク)

**status.json**(エディタ→エージェント):

```json
{ "state": "playing", "commandId": "run-007", "message": "...",
  "reportPath": ".../Reports/report_run-007.json", "utc": "..." }
```

`state` の遷移: `bridge-loaded`(生存通知) → `refreshing` → `compile-error`(エラー全文付き)
または `playing` → `stopping` → `done`。

**report_<id>.json**(ランタイム→エージェント): §3.6のBotReporter出力。
セッションIDはブリッジのコマンドIDを引き継ぐ(エディタの`SessionState`経由)ため、
コマンドとレポートが1:1で対応する。

### 5.3 エディタ側の実装ポイント

- **ドメインリロード生存**: Play出入りやコンパイルでC#の静的状態は消える。
  状態機械(`idle/waitCompile/play/finalize`)・コマンドID・終了時刻は
  `SessionState`(エディタ起動中は保持)に置き、`[InitializeOnLoad]` の静的コンストラクタで
  毎回 `EditorApplication.update` にPollを再登録する
- **コンパイルエラー捕捉**: `CompilationPipeline.assemblyCompilationFinished` で
  エラーメッセージ(ファイル/行番号付き)を蓄積し、status.jsonでエージェントに渡す。
  コンパイルが失敗しても**旧アセンブリのブリッジは生き続ける**ので、
  エージェントは修正→再コマンドで自己回復できる
- **Play開始前のシーン保証**: ベイク等でロケーションシーンが開いたままでも、
  必ずInitialization(起動シーン)を開いてからEnterPlaymodeする
- **非フォーカス動作**: ランタイム側で `Application.runInBackground = true`。
  仮想デバイスへのQueueStateEventは合成イベントなのでOSフォーカスに依存しない

### 5.4 エージェント側のループ(Claude Codeの動き)

1. `command.json` を書く(PowerShell/シェルからの単純なファイル書き込み)
2. バックグラウンドで `status.json` の変化をポーリング監視(5〜10秒間隔)
3. `done` になったら `report_<id>.json` を解析:
   - `errorCount` / `warnCount` / `totalDistanceMoved` で健全性を判定
   - objectiveイベントの並びで「どこまで進んだか」を判定
   - warn/errorイベントと座標ログで「どこで・なぜ詰まったか」を特定
4. 問題があればC#ソースを編集(次のrunコマンドのRefreshで自動コンパイルされる)
5. 新しいidでrunコマンド発行 → 2へ戻る

この2〜5の反復が「自律デバッグループ」。人間の役割はUnityエディタを開いておくことと、
最初の1回だけエディタをフォーカスしてブリッジ自体をコンパイルさせることのみ。

### 5.5 このループが実際に直したもの(実例)

| run | レポートから検出 | 修正 |
|---|---|---|
| 001 | 採集モードが一度も発動しない(attempt表示が毎回1/4) | 試行カウンタがハンドラ再起動でリセット → Blackboardへ移動 |
| 002 | 4回話しかけてもDialogueフェーズ遷移ゼロ | 距離判定を廃止し、InteractionManagerのゾーン判定を観測 |
| 003 | 採集対象が見つからない | 静的解析で「Pickableは討伐ドロップのみ」「必要品は調理品」と特定 → 方針転換 |
| 004 | 出口通過後、同一方向へ5分間壁ドン | NavMesh未ベイクが根因 → bakeコマンド追加 |
| 006 | 迂回が一度も発動しない/島の端で永久スタック | 進捗判定を「移動量」→「目的地への接近量」に変更/部分経路は終端まで歩いて直進フォールバック |
| 007 | — | **ゲームクリア達成** |

---

## 6. 別プロジェクトへの導入手順

### 6.1 コピーするもの

1. `Assets/AutoPilot/Core` / `InputSim` / `Nav` をそのままコピー(asmdefごと)
2. `Adapter/` は**コピーせず**、対象ゲーム用に新規作成する(以下参照)
3. Input Systemパッケージ(com.unity.inputsystem)が入っていることを確認

対象ゲームのスクリプトがasmdef管理なら、アダプタにもasmdefを作り
`AutoPilot.Core` 等と対象ゲームのアセンブリを参照する。
Assembly-CSharp(asmdefなし)ならアダプタもasmdefなしで置く。

### 6.2 アダプタ実装チェックリスト(実装順)

**Step 1 — 入力の疎通(最初にやる。技術リスクの塊)**
- [ ] ゲームが新Input Systemなら `VirtualGamepad` をそのまま使う
- [ ] Boot(下記雛形)を書き、Playで「仮想パッドのスティックでキャラが歩く」を確認
- [ ] 全操作にゲームパッドバインディングがあるか `.inputactions` を確認
      (無い操作は仮想Keyboard/Mouseの追加実装が必要)

**Step 2 — 観測**
- [ ] PhaseSensor: そのゲームの状態管理(GameManagerのenum、ロード済みシーン名、
      UIの表示状態など)を `NormalizedPhase` に写像する。**ここが移植の本体**
- [ ] PlayerSensor / CameraSensor: 位置とメインカメラ(`Keys.PlayerPosition` 等へ)
- [ ] 進行目標のSensor: クエスト/ミッション/スコア等から「次にやること」を導出して
      ゲーム固有キーに書く

**Step 3 — シナリオ**
- [ ] Title: メニュー突破(パッド決定 → 駄目ならUI直呼びフォールバック)
- [ ] Playing: 進行ロジック(まずは徘徊デモから始め、目標追跡に置き換える)
- [ ] Dialogue/Menu等: 送り・閉じ操作
- [ ] 周回モノなら Result: リトライ操作

**Step 4 — ブート**

```csharp
public static class MyGameAutoPilotBoot
{
    public const string EnableOnPlayPrefKey = "AutoPilot.EnableOnPlay";
    private static BotRunner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_runner != null || !IsEnabled()) return;
        Application.runInBackground = true;
        _runner = BotRunner.Create(new VirtualGamepad());
        _runner.Sensors.Add(new MyPlayerSensor());
        _runner.Sensors.Add(new MyCameraSensor());
        _runner.Sensors.Add(new MyPhaseSensor());
        // レポーターは最後(他Sensorの最新値を読むため)
        _runner.Sensors.Add(new BotReporter(reportPath, sessionId, _runner.Context.Log));
        MyScenario.Install(_runner.Director);
    }
}
```

**Step 5 — 自律ループ(任意だが強く推奨)**
- [ ] `AutoPilotBridgeService.cs` をコピーし、ゲーム固有箇所を2つ直す:
      起動シーンのパス(`Assets/Scenes/Initialization.unity`)と
      `EnableOnPlayPrefKey` の参照先
- [ ] `AutoPilotNavMeshBaker` はNavMeshを使うゲームでのみ必要

### 6.3 ゲームタイプ別の要点

- **3人称+カメラ相対移動**: そのまま使える(`Steering.WorldDirectionToStick` が吸収)
- **FPS**: 移動はカメラ相対でそのまま動く。照準は `SetRightStick` で
  「目標方向とカメラforwardの角度差→右スティック値」の変換タスクを1つ書く
- **トップダウン/2D**: カメラ変換を恒等にする(CameraTransform未設定なら
  world→stickの素通しになる実装済み)
- **メニュー主体(カード/パズル等)**: 移動系は不要。UI操作(ナビゲート+決定)の
  タスクとUI状態Sensorが本体になる

### 6.4 旧Input Manager(UnityEngine.Input)のゲーム

`Input.GetAxis` には外部から注入できないため、選択肢は2つ:

1. ゲーム側に入力ラッパー(例: FPS Microgameの `PlayerInputHandler`)があれば、
   それを `IVirtualController` の値を読む実装に差し替える(アクションレベル注入)
2. ラッパーが無ければ、`IVirtualController` を実装した「値保持クラス」を作り、
   ゲームの入力読み取り箇所を最小限書き換えてそこから読ませる

コア/シナリオ側は `IVirtualController` しか見ていないので、どちらでも無変更で動く。

### 6.5 移植コストの目安

Chop Chopアダプタの規模感: Sensor群 約300行、シナリオ 約350行、静的知識(WorldMap)約100行。
「徘徊デモが動く」までなら1日、「進行ボット」はゲームの複雑さ次第。
最初に必ず入力疎通(Step 1)から始めること。

---

## 7. 既知の落とし穴と教訓

1. **ブリッジへの新アクション追加は「先にコンパイル」**
   コマンドを処理するのは今コンパイル済みの旧アセンブリ。新アクションのコマンドを
   先に送ると黙って消費される。新コードを足したらまず `run`(Refreshを含む)を1回挟む
2. **タスクのローカル変数は揮発する**
   フェーズ遷移でハンドラは作り直される。跨いで保持したい状態はBlackboardへ
3. **進捗判定は「目的地への接近量」で**
   移動量ベースだと壁ずり歩きの揺れを進捗と誤認して復旧が発動しない
4. **NavMeshのPathPartialを必ず処理する**
   分断された島の端で永久スタックする。終端まで歩く+直進フォールバック
5. **UIオートメーションは入力→コマンドの2段構え**
   入力経路で数回試して駄目ならAPI直呼びへ。無限に入力リトライしない
6. **距離ではなくゲームの判定を観測する**
   「2m以内=話せる」は嘘だった。ゲーム自身のゾーン判定を読むのが正
7. **SO(ScriptableObject)の状態はエディタPlay間で残る**
   クエストフラグ等はPlay停止でリセットされない。周回検証ではリセット手段を確保する
8. **プライベートフィールドのリフレクションはアダプタに閉じ込める**
   ゲーム更新で壊れうる箇所。null検査+フォールバック(徘徊)を必ず用意
9. **エディタ非フォーカスでも動くようにする**
   `Application.runInBackground = true` + 合成入力イベント。自律ループの前提条件

---

## 8. 開発ログ

| run | 時間 | 結果 |
|---|---|---|
| run-001 | 3分 | タイトル突破、QL1のS1/S2完了。ライブロック発見 |
| run-002 | 4分 | カウンタ修正を検証。ゾーン未到達問題を発見 |
| run-003 | 4分 | 会話復活。採集不能の根因(ドロップ制+調理品要求)を特定 |
| run-004 | 7分 | シム調理でQL1クリア、選択肢制御動作、Field_Hillでスタック |
| bake | 70分 | 全11ロケーションにNavMeshベイク(遠隔コマンド) |
| run-005 | 8分 | QL2クリア。NavMesh経路でも部分経路スタックを発見 |
| run-006 | 15分 | 失敗ラン。迂回不発の原因(進捗判定)を特定 |
| run-007 | 15分 | **ゲームクリア**: QL3→QL4→QL5→QL6、WinningChoice選択、勝利カットシーン |

使用シム(コンテンツ欠落補完、[SHIM]ログで明示):
調理のインベントリ直接実行(タグ付きCookingPot不在のため)/
RockCandy・SweetDough・CakeWithRockCandyレシピの付与(入手経路なし)/
PhoenixChick 1個の付与(2個目の入手経路なし)。
それ以外の全進行(移動・会話・選択肢・クエスト)は入力シミュレーション経由。
