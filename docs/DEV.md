# DEV.md — 開発環境構築・実行ガイド

このドキュメントは、本リポジトリをローカル環境でビルド・実行し、
Recorder / Inspector / Viewer を使って開発・検証するための手順をまとめたものです。

---

## 前提環境

* OS: Windows 10 / 11
* .NET SDK: リポジトリ内の csproj と同じバージョン（例: net10.0-windows）
* Git

※ WinForms と System.Drawing を使用するため、Windows 環境必須です。

---

## リポジトリ取得

```bash
git clone https://github.com/yuichiohryh-oss/cr.git
cd cr
```

---

## 初回セットアップ確認

```bash
dotnet --info
dotnet build
dotnet test
```

すべて成功すれば、開発を開始できます。

---

## Recorder（WinFormsアプリ）の実行

```bash
dotnet run --project src/WinFormsApp1
```

### 基本操作

1. キャプチャ対象ウィンドウを選択
2. Match Start ボタンで試合開始
3. Match End ボタンで試合終了

### Recorderの主な役割

* PCに表示された Clash Royale 画面のキャプチャ
* 盤面・行動の検出（Resolver方式）
* per-match JSONL の生成
* 記録時に prev / curr 画像を frames ディレクトリへ保存

---

## Dataset Inspector（CLI）

学習データの整合性を機械的にチェックするツールです。

```bash
dotnet run --project tools/CrDatasetInspector -- <datasetRoot>
```

### 出力

* `<datasetRoot>/_inspect/summary.md`
* `<datasetRoot>/_inspect/report.json`
* `<datasetRoot>/_inspect/bad_rows.jsonl`

---

## Dataset Viewer（GUI）

JSONL と prev/curr 画像を目視確認するためのツールです。

```bash
dotnet run --project tools/CrDatasetViewer
```

### 主な機能

* JSONL の行一覧表示
* prev / curr 画像の並列表示
* bad_rows.jsonl との連携（問題行ハイライト・ジャンプ）

---

## データ配置の前提

```text
dataset/
  <matchId>/
    <match>.jsonl
    frames/
      ..._prev.png
      ..._curr.png
  _inspect/
    summary.md
    report.json
    bad_rows.jsonl
```

---

## よくある注意点

* DPI 設定やウィンドウの表示倍率によって、キャプチャ位置がずれる場合があります
* 画像保存はディスクI/Oを伴うため、SSD環境推奨です
* 大量データを扱う場合は Inspector で事前チェックすることを推奨します
