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
- `src/MidiSynth/MonoSynth.cs` — 上の回路をつないだ単音シンセ。MIDI の受信線（`midi_rx`）から 1 ビットの音声（`audio`）を出す。`playing` は鍵盤を押しているあいだ 1
- `src/MidiSynth/PowerOnReset.cs` — 電源投入の直後だけリセットを出す回路
- `src/MidiSynth/TangPrimer25KTop.cs` — Tang Primer 25K に書くトップ。端子は `clk`（50 MHz）、`midi_rx`、`audio`、`led`。PLL なし、MIDI の 1 ビットは 1600 クロック、サンプル周波数は 48828.125 Hz
- `src/MidiSynth.Emit` — トップの Verilog を書き出すコマンド
- `src/MidiSynth.Tests` — SharpHdl.Sim で回路を動かすテスト

## Verilog の書き出し

```bash
dotnet run --project src/MidiSynth.Emit
```

`build/TangPrimer25KTop.v` ができます。`build/` は Git に入れません。

## 目標

- MIDI 入力 → 音源 → Tang Primer 25K で音声出力するデモ
