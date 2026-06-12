# UNBOUND GP — Vertical Slice Prototype

> **もし、F1にマシンのレギュレーションが存在しなかったら?**

禁止された技術をすべて解放し、理論上最速のマシンを設計するレースゲーム。
ただしコクピットに人間が座る限り、たったひとつだけ書き換えられないルールが残っている──**人体のG限界**。

このプロトタイプの最重要目標は、プレイヤーに
**「ルールがない世界を体験した結果、レギュレーションの意味に自分で気づく」**
体験を与えることです。

---

## 体験の設計 (Design Pillars)

1. **解放の快感** — 研究ツリーでファンカーやアクティブエアロなど“現実で禁止された技術”を解放する。各ノードには現実のF1での顛末 (禁止年など) を併記し、問いの種を蒔く。
2. **数字による予感** — マシン設計画面は「人間が乗った場合」と「AIが乗った場合」の理論ラップを常に併記する。マシンを強化するほど、その差が開いていく。
3. **身体での理解 (Human GP)** — 自分で運転すると、強化したマシンのコーナリングGが人間の持続限界 (約5G) を超え、視野が狭窄し、最後はブラックアウトする。**マシンより先に、自分が壊れる。**
4. **不在による理解 (Machine GP)** — 同じマシンをAIに渡して観戦すると、理論限界そのままの異常な速度でレースが進む。ミスも恐怖もなく、性能差がそのまま着順になる。**レースは出走前に終わっている。**
5. **デブリーフ** — レース後、生理データ (最大G/失神回数/限界超過時間) と理論ラップ比較を提示し、短い問いだけを置く。説教はしない。

Human GP と Machine GP の実装上の違いは `DriverProfile` の数値 (G限界) と `IDriverInput` の実装だけです。
「違いはルールではなく、コクピットの中身だった」をコード構造そのものでも表現しています。

---

## セットアップ

1. **Unity 2022.3 LTS 以降**でこのフォルダをプロジェクトとして開く (Built-in Render Pipeline / 追加アセット不要)
2. メニュー **[UNBOUND GP > Setup Project (Data + Scenes)]** を実行
   - `Assets/UnboundGP/Resources/UnboundGP/` に ScriptableObject アセット群を生成
   - `Assets/UnboundGP/Scenes/` に MainMenu / Garage / Race シーンを生成し、ビルド設定に登録
3. `MainMenu.unity` を開いて再生

※ Setup を実行しなくても、任意のシーンを直接再生すれば既定データが実行時生成されて動作します (`DefaultDataFactory` によるフォールバック)。
※ UI は日本語を含むため、テキスト描画は OS フォントフォールバックに依存します (Windows/macOS で確認可)。

### 操作

| 場面 | 操作 |
|---|---|
| Human GP (運転) | WASD / 矢印キー: アクセル・ステア、Space: ブレーキ、R: コース復帰 |
| Machine GP (観戦) | Tab: カメラ切替 |

---

## ゲームフロー

```
MainMenu → Garage ──┬─ [研究ツリー]  RPを消費して技術を解放
                    ├─ [マシン設計]  技術の装備/取り外し → 最終性能と理論ラップが即時更新
                    └─ [出走]        Human GP / Machine GP を選択
                                        ↓
                    Race (SUZUKA UNBOUND・3周) → Debrief (気づきの提示・RP獲得) → Garage へ戻る
```

## プロジェクト構成

```
Assets/UnboundGP/Scripts/
├── Core/                       # ドメインモデル (Unityシーン非依存の中心層)
│   ├── GameMode.cs             # HumanGP / MachineGP
│   ├── MachineStats.cs         # 性能モデル。旋回限界G・制動などの“物理の単一の真実”
│   ├── DriverProfile.cs        # 人間/AIのG限界プロファイル (テーマの中心点)
│   ├── SaveData.cs             # 進行状況 (PlayerPrefs + JSON)
│   └── GameContext.cs          # シーン横断シングルトン。研究/装備/セーブ/データ解決
├── Data/                       # ScriptableObject データ層
│   ├── TechNodeData.cs         # 技術ノード (効果・前提・現実のF1での顛末)
│   ├── TechTreeData.cs         # 研究ツリー
│   ├── MachineChassisData.cs   # ベースシャシー
│   ├── TrackData.cs            # コース定義 (制御点リスト)
│   ├── RaceModeData.cs         # モード定義 (デブリーフ文含む)
│   └── DefaultDataFactory.cs   # 既定データ生成 (アセット書き出し兼フォールバック)
├── Track/                      # コース生成
│   ├── SuzukaTrackDefinition.cs# 鈴鹿風・立体交差つき8の字レイアウトの制御点
│   ├── CatmullRomSpline.cs     # スプライン評価 + 距離/曲率サンプラ
│   ├── TrackPath.cs            # コースの論理表現 (AI追従・ラップ計測の基盤)
│   └── TrackBuilder.cs         # 路面/ウォール/スタートラインの実行時メッシュ生成
├── Race/                       # レース実行
│   ├── IDriverInput.cs         # 人間とAIを差し替える入力インターフェース
│   ├── PlayerInputDriver.cs    # キーボード入力
│   ├── AIDriver.cs             # 「自分のG限界の内側でだけ攻める」ライン追従AI
│   ├── DriverCondition.cs      # G負荷→視野狭窄→失神の生理シミュレーション
│   ├── CarController.cs        # 簡易走行物理 (旋回限界G→許容ヨーレート)
│   ├── CarFactory.cs           # プリミティブ製マシンの生成
│   ├── RaceManager.cs          # レースフロー統括 (スポーン/計測/順位/復帰/結果)
│   ├── RaceHUD.cs              # Gメーター/意識ゲージ/視野狭窄ビネット
│   ├── RaceResult.cs           # 結果+生理統計
│   └── ChaseCamera.cs          # 速度連動FOVの追従カメラ
├── Design/
│   └── LapTimeEstimator.cs     # 速度プロファイル法の理論ラップ推定 (人間/AI併記用)
├── UI/                         # コード生成uGUI (プレハブ不要)
│   ├── UiFactory.cs            # Canvas/Text/Button/Bar/ビネット生成ヘルパー
│   ├── SceneFlow.cs            # シーン遷移
│   ├── MainMenuScreen.cs       # タイトル
│   ├── GarageScreen.cs         # ハブ (3タブ)
│   ├── ResearchTreeView.cs     # 研究ツリー
│   ├── MachineDesignView.cs    # マシン設計
│   ├── RaceEntryView.cs        # 出走 (モード選択)
│   └── DebriefView.cs          # レース後の“気づき”画面 (最重要画面)
└── Editor/
    └── UnboundProjectBuilder.cs# ワンクリックセットアップ
```

## 実装方針

- **データ駆動**: 技術・コース・モードはすべて ScriptableObject。バランス調整・コンテンツ追加はアセット編集のみで可能。`DefaultDataFactory` が初期値の単一ソース。
- **物理の単一の真実**: 旋回限界G の式 `mechanicalGrip + downforceFactor × (v/300km/h)²` を CarController / AIDriver / LapTimeEstimator が共有。設計画面の予想とコース上の挙動が必ず一致する。
- **Human/Machine の対称性**: 両モードは同一のコードパスを通る。差分は `DriverProfile.maxSustainedG` と入力実装のみ。
- **アセット非依存**: メッシュ・UI・シーン内容はすべて実行時生成。アートパス導入時は `CarFactory` / `TrackBuilder` / `UiFactory` をプレハブ参照に差し替えるだけ。
- **立体交差対応**: コース上の自車位置は「前回位置の近傍のみ探索」する最寄り点検索で求め、8の字交差でもラップ計測・AIが破綻しない。

## 拡張ガイド

| やりたいこと | 方法 |
|---|---|
| 技術を追加 | [Create > UNBOUND GP > Tech Node] でノード作成 → TechTree に登録 (またはコードなら `DefaultDataFactory.CreateTechTree`) |
| コースを追加 | [Create > UNBOUND GP > Track] で制御点を定義 (y で立体交差も可) |
| シャシーを追加 | MachineChassisData を複製しガレージに選択UIを追加 |
| 新ステータス | `StatType` に追加 → `MachineStats.Get/Set` と物理側の参照を実装 |
| ライバルの個性化 | `RaceManager.SpawnEntrants` のスケーリングを、独自の研究状態を持つ AI チームに置き換える |

## 既知の制限 (Vertical Slice)

- 物理は雰囲気重視の簡易モデル (サスペンション・タイヤ温度なし)
- 信頼性ステータスは表示のみ (リタイア未実装)
- ライバルAIの性能はプレイヤー機のスケーリングで代用
- 日本語フォントは OS フォールバック依存 (本実装では TextMeshPro + フォントアセット化を想定)
