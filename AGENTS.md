# AGENTS.md — AI / Contributor Quickstart

このリポジトリは「クラッシュ・ロワイヤル（Clash Royale）」の実プレイ画面をキャプチャし、
盤面・手札・行動を認識して、学習用データ（per-match JSONL + frames画像）を生成するプロジェクトです。

---

## 重要な設計前提（壊さないこと）

* 1試合 = 1 JSONL（per-match）
* JSONL 各行に以下を必ず含める

  * match_id
  * match_elapsed_ms
  * frame_index
* JSONL に記録する画像パスは **相対パス**（例: `frames/000123_prev.png`）
* JSON 記録時に必ず `prev` / `curr` の2枚画像を保存
* ActionDetector は Resolver 方式で統合
* OpenCV は使わず **System.Drawing ベース**
* PR ベース開発（小さく、テスト付き）

---

## リポジトリ構成（概要）

* src/WinFormsApp1/
  Recorder 本体（キャプチャ / 検出 / JSONL 記録）

* tools/CrDatasetInspector/
  JSONL + frames 整合チェッカー（CLI）

* tools/CrDatasetViewer/
  JSONL + prev/curr 画像ビューア（WinForms）

* tests/WinFormsApp1.Core.Tests/
  単体テスト

---

## 最初に実行するコマンド

```
dotnet --info
dotnet build
dotnet test
```

---

## ツールの使い方

### Dataset Inspector（CLI）

```
dotnet run --project tools/CrDatasetInspector -- <datasetRoot>
```

オプション例:

```
dotnet run --project tools/CrDatasetInspector -- <datasetRoot> --verify-image-load
```

---

### Dataset Viewer（GUI）

```
dotnet run --project tools/CrDatasetViewer
```

---

## データの想定ディレクトリ構造

```
dataset/
  <matchId>/
    <match>.jsonl
    frames/
      00001234_00056_prev.png
      00001234_00056_curr.png
  _inspect/
    summary.md
    report.json
    bad_rows.jsonl
```

---

## PR 作法（最低限）

* 変更前後で必ず以下を実行

  * dotnet build
  * dotnet test
* コミットは意味単位で分ける（目安 1〜2 個）
* 破壊的変更は禁止
  必要な場合は **理由 / 影響範囲 / 代替案** を PR に明記

---

## AI に依頼するときのルール

* コード全文をチャットに貼らせない
* build / test が失敗した場合は PR を作らせない
* 失敗時は「原因」と「修正方針」のみ報告させる
* 以下の設計前提を必ず守らせる

  * per-match JSONL
  * frames 相対パス
  * Resolver 方式
  * System.Drawing 使用

---

## 直近の開発テーマ

* 学習データの品質検証

  * JSONL ⇔ prev/curr 画像の整合
  * action 検出精度の評価
* Inspector / Viewer を使った検証ループ高速化
* 学習用前処理（Python）・モデル設計（将来）

---

このファイルを最初に読めば、新規チャット・新規参加者でも
プロジェクトの前提と進め方をすぐに理解できるようにしています。
