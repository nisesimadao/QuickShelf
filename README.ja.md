# QuickShelf

<p align="center">
  <img src="Assets/QuickShelf.png" width="96" alt="QuickShelf icon">
</p>

QuickShelf は、Windows の画面右端に常駐するサイドパネルです。
システム監視、開発用ポートの確認、一時的なファイル置き場を一つの画面にまとめます。

[English](README.md)

![QuickShelf panel](docs/screenshots/panel.png)

## 主な機能

- 画面右端へカーソルを移動すると、パネルをアニメーションで展開します。
- CPU / RAM / NVIDIA GPU の使用率をリアルタイムで表示します。
- CPU と RAM の使用率を含むプロセス一覧を表示します。
- プロセス一覧はスクロールでき、更新後もスクロール位置を維持します。
- プロセス行から `Taskkill` を実行できます。
- LISTEN 状態の TCP ポートを一覧表示し、localhost を開く操作と該当プロセスの終了操作を行えます。
- ファイルを一時保持するクイックシェルフを備えています。
- クイックシェルフのファイルは、Explorer、Discord、ブラウザ、エディタ、DAW などへ Windows 標準のファイルドラッグ＆ドロップで渡せます。
- システムトレイから表示と終了を操作できます。
- OS の言語に応じて日本語 UI と英語 UI を切り替えます。
- インストールスクリプトは、スタートメニューへの登録とユーザー単位のログオンタスクによる自動起動を設定できます。

## スクリーンショット

![QuickShelf on desktop](docs/screenshots/desktop.jpg)

## ダウンロード

[GitHub Releases](https://github.com/nisesimadao/QuickShelf/releases) から Windows x64 向けの self-contained ZIP をダウンロードできます。
Release 版には .NET ランタイムを含むため、.NET 10 を別途インストールする必要はありません。
Microsoft Edge WebView2 Runtime は必要ですが、現在の Windows 11 には通常含まれています。

## 必要環境

- Windows 10 Version 2004 以降、または Windows 11。
- ソースから実行する場合は .NET 10 Desktop Runtime。
- Microsoft Edge WebView2 Runtime。
- GPU 表示には NVIDIA GPU と `nvidia-smi` を使用します。
  CPU、RAM、プロセス、ポート機能は NVIDIA GPU がない環境でも動作します。

## ビルド

```powershell
dotnet restore
dotnet build .\QuickShelf.csproj -c Release
```

インストーラー用の publish フォルダを作る場合は、次のスクリプトを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

出力先は `artifacts\publish` です。

## インストール

publish 後に次のスクリプトを実行します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

インストール先は次の通りです。

```text
%LOCALAPPDATA%\Programs\QuickShelf
```

インストールスクリプトは、スタートメニューにショートカットを作成し、ログオン 10 秒後に QuickShelf を起動するタスクを登録します。
登録後、そのまま QuickShelf を起動します。

アンインストールには次のスクリプトを使用します。

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

## 使い方

画面右端の当たり判定へカーソルを入れると QuickShelf が展開します。
カーソルを離すと収納され、収納中はほぼ透明になります。
再展開しやすいように、当たり判定は表示部分より広く設定しています。

プロセス一覧は 1 秒ごとに更新します。
行へカーソルを合わせると `Taskkill` を表示します。
一覧はスクロールでき、更新中もスクロール位置を維持します。

クイックシェルフでは「追加」からファイルを登録できます。
登録したファイルはクリックで開けます。
ドラッグすると、通常の Windows ファイルとして他のアプリへ渡せます。

## 構成

外形、ウィンドウリージョン、アニメーション、システム統合、トレイ、ファイルドラッグ＆ドロップは WPF / C# 側で処理します。
パネル内部の UI は WebView2 で描画します。
CPU、RAM、GPU、プロセス、ポートの情報は C# で取得し、JSON として WebView2 へ渡します。

## 注意

`Taskkill` は選択したプロセスツリーを即座に終了します。
未保存のデータがあるアプリを終了しないよう注意してください。
