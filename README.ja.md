# QuickShelf

<p align="center">
  <img src="Assets/QuickShelf.png" width="96" alt="QuickShelf icon">
</p>

Windowsの右端に常駐する、システム監視・開発用ポート確認・一時ファイル置き場をまとめた軽量サイドシェルフです。

[English](README.md)

![QuickShelf panel](docs/screenshots/panel.png)

## 主な機能

- 画面右端ホバーで展開するネイティブアニメーション
- CPU / RAM / NVIDIA GPU 使用率をリアルタイム表示
- CPU/RAM使用率付きの実プロセス一覧
- プロセス一覧のスクロールとスクロール位置維持
- 行ホバーから `Taskkill`
- LISTEN中のTCPポート一覧、localhostを開く、該当プロセス終了
- ファイルを一時保持する「クイックシェルフ」
- クイックシェルフからExplorer、Discord、ブラウザ、エディタ、DAWなどへWindows標準のファイルD&D
- システムトレイ常駐（表示 / 終了）
- OS言語に合わせた日本語 / 英語UI
- Startメニュー登録とユーザー単位のWindowsログオン時自動起動に対応したインストールスクリプト

## スクリーンショット

![QuickShelf on desktop](docs/screenshots/desktop.jpg)

## ダウンロード

いちばん簡単なのは [GitHub Releases](https://github.com/nisesimadao/QuickShelf/releases) から Windows x64 向けの self-contained ZIP をダウンロードする方法です。Release版には .NET ランタイムを含めているため、**.NET 10を別途インストールする必要はありません**。Microsoft Edge WebView2 Runtime は必要です（現在のWindows 11には通常含まれています）。
## 必要環境

- Windows 10 Version 2004以降、またはWindows 11
- .NET 10 Desktop Runtime
- Microsoft Edge WebView2 Runtime
- GPU表示はNVIDIA GPU + `nvidia-smi` を使用。CPU/RAM/プロセス/ポート機能はNVIDIA環境でなくても動作します

## ビルド

```powershell
dotnet restore
dotnet build .\QuickShelf.csproj -c Release
```

インストーラー用のpublishフォルダを作る場合:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

出力先は `artifacts\publish` です。

## インストール

publish後に:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

インストール先:

```text
%LOCALAPPDATA%\Programs\QuickShelf
```

Startメニューにショートカットを作成し、HKCU RunへQuickShelfを登録してログオン時に自動起動するよう設定したうえで、そのままQuickShelfを起動します。

アンインストール:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

## 使い方

画面右端の当たり判定へカーソルを入れるとQuickShelfが展開します。離れると収納され、収納中はほぼ透明になります。当たり判定自体は見た目より大きく取ってあるので再展開しやすくしています。

プロセス一覧は1秒ごとに更新されます。行へホバーすると `Taskkill` が出ます。リストはスクロール可能で、更新中もスクロール位置を維持します。

クイックシェルフでは **追加** からファイルを登録できます。クリックで開き、ドラッグすれば普通のWindowsファイルとして他アプリへ持ち込めます。

## 構成

外形・ウィンドウリージョン・アニメーション・システム統合・トレイ・ファイルD&DはWPF/C#側、内側のUIはWebView2で描画しています。CPU/RAM/GPU・プロセス・ポート情報はC#で取得し、JSONとしてWebViewへ渡しています。

## 注意

`Taskkill` は選択したプロセスツリーを即座に終了します。未保存データがあるアプリでは注意してください。

