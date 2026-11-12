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
- `src/MidiSynth/NoteParser.cs` — MIDI のバイト列から、チャンネル 1 の Note On / Note Off を 1 音分のノート番号とゲートにする回路
- `src/MidiSynth/PhaseIncrementTable.cs` — ノート番号から位相増分を引く表（平均律、A4 = 440 Hz。サンプル周波数と位相の幅はコンストラクタで決める）
- `src/MidiSynth/SampleTicker.cs` — クロックを数え、サンプル周波数の合図（tick）を出す回路
- `src/MidiSynth/SquareOscillator.cs` — gate が 1 のあいだ、tick ごとに位相へ増分を足し、位相の最上位ビットを矩形波（デューティ 50%）として出す回路。gate が 0 なら出力は 0
- `src/MidiSynth/MonoSynth.cs` — 上の回路をつないだ単音シンセ。MIDI の受信線（`midi_rx`）から 1 ビットの音声（`audio`）を出す
- `src/MidiSynth.Tests` — SharpHdl.Sim で回路を動かすテスト

## 目標

- MIDI 入力 → 音源 → Tang Primer 25K で音声出力するデモ
