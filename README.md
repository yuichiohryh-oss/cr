# Clash Royale Advisor MVP (WinForms + scrcpy)

## Purpose
Clash Royale を scrcpy で PC にミラーし、WinForms アプリで画面キャプチャ・解析して
次に「いつ / どこ」にカードを出すべきかを提案表示する MVP を実装します。
自動操作は行わず、提案のみを表示します。

## System Diagram

Android (Clash Royale)
  |  USB + scrcpy
  v
scrcpy window (title: ClashRoyale)
  |  Win32 capture
  v
WinFormsApp1
  |  Motion / Elixir analysis
  v
Suggestion (dot + text)

## Setup

1) Android 端末で USB デバッグを有効化
2) scrcpy を起動 (必須のウィンドウタイトル指定)

```
scrcpy --window-title "ClashRoyale"
```

3) ビルド / 実行

```
dotnet build
```

```
dotnet run --project src/WinFormsApp1
```

4) unsafe 有効化

本リポジトリでは `AllowUnsafeBlocks` を csproj に設定済みです。

5) 設定ファイル (GUI から調整可)

`appsettings.json` を GUI の PropertyGrid で編集し、`Save && Apply` で反映できます。
実行ディレクトリ直下に保存されます。

## Current Features

- scrcpy ウィンドウをキャプチャして表示
- フレーム差分の動体検出
- 自陣側で動きが増えたら防衛提案 (dot + text)
- エリクサーバーの色判定による推定と表示
- ROIをドラッグで調整 (Motion/Elixir)

## Suggestion Logic

- MotionAnalyzer が自陣側 ROI の動体量を左右別に集計
- 動体が閾値を超えると DefenseTrigger = true
- ElixirEstimator がエリクサーバーの紫色比率から 0-10 を推定
- SuggestionEngine が
  - DefenseTrigger
  - Elixir >= need (default: 3)
  - 2 フレーム連続でトリガー
  - 700ms クールダウン
  を満たしたときに提案を出します
- 提案位置は定跡ポイントへスナップ

## Design Policy

- 自動操作は一切行わない (タップ送信 / 入力注入なし)
- 画面上の提案表示のみ

## Roadmap

- [x] Phase 1: Capture + Draw MVP
- [x] Phase 2: Motion + Elixir estimation
- [x] Phase 3: Suggestion logic + tests
- [ ] Phase 4: Accuracy tuning + fixtures + parameter UI
