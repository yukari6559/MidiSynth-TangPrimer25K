# MidiSynth-TangPrimer25K

Tang Primer 25K 向け MIDI シンセの就活デモ用リポジトリです。

SharpHdl は **NuGet 参照のみ**（ソース同梱なし）。回路・ボード統合はこのリポジトリ側に実装していきます。

## 依存

- [SharpHdl.Core](https://www.nuget.org/packages/SharpHdl.Core) `0.11.0`
- [SharpHdl.Emit](https://www.nuget.org/packages/SharpHdl.Emit) `0.11.0`
- [SharpHdl.Sim](https://www.nuget.org/packages/SharpHdl.Sim) `0.11.0`

```bash
dotnet restore
dotnet build
dotnet test
```

## 構成

- `src/MidiSynth/UartRx.cs` — MIDI の UART（31250 baud、8 ビット、パリティなし、ストップ 1 ビット）から 1 バイトずつ受け取る回路
- `src/MidiSynth.Tests` — SharpHdl.Sim で回路を動かすテスト

## 目標

- MIDI 入力 → 音源 → Tang Primer 25K で音声出力するデモ
