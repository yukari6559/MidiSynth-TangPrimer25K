using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// Tang Primer 25K に書くトップ。オンボードの 50 MHz を PLL なしでそのまま使う。
/// MIDI の 1 ビットは 1600 クロック、1 サンプルは 1024 クロック（48828.125 Hz）、位相は 24 ビット。
/// リセットの端子は使わず、<see cref="PowerOnReset"/> が電源投入の直後だけ <see cref="MonoSynth"/> をリセットする。
/// </summary>
public sealed class TangPrimer25KTop : Module
{
	/// <summary>オンボードの発振器の周波数（Hz）。</summary>
	public const double ClockHz = 50_000_000;

	/// <summary>1 サンプルのクロック数。</summary>
	public const uint ClocksPerSample = 1024;

	/// <summary>位相の幅。</summary>
	public const uint PhaseWidth = 24;

	/// <summary>電源投入のあとリセットを出しておくクロック数。</summary>
	public const uint ResetHoldClocks = 16;

	private readonly PowerOnReset Por = new(ResetHoldClocks);
	private readonly MonoSynth Synth = new(ClockHz, ClocksPerSample, PhaseWidth);
	private readonly Wire Zero = Wire.UInt(1, "zero");
	private readonly Wire SynthReset = Wire.UInt(1, "synth_reset");

	/// <summary>オンボードの 50 MHz。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>MIDI の受信線。空きは 1。</summary>
	public In MidiRx { get; } = In.UInt(1, "midi_rx");

	/// <summary>音声の 1 ビット出力。直列抵抗を挟んでイヤホンかアンプへ。</summary>
	public Out Audio { get; } = Out.UInt(1, "audio");

	/// <summary>鍵盤を押しているあいだ 1。LED につなぐ。</summary>
	public Out Led { get; } = Out.UInt(1, "led");

	/// <summary>トップを作る。</summary>
	public TangPrimer25KTop()
	{
		SetPorts([Clk, MidiRx, Audio, Led]);
		SetLocals([Zero, SynthReset]);
	}

	/// <summary>電源投入のリセットと単音シンセをつなぐ。</summary>
	public override void Describe()
	{
		Comb(() => Zero.Assign(Lit.Bits(1, 0)));

		Por.Describe();
		Synth.Describe();

		Instance(Por, "por",
			(Por.Clk, Clk), (Por.Rst, Zero), (Por.Reset, SynthReset));
		Instance(Synth, "synth",
			(Synth.Clk, Clk), (Synth.Rst, SynthReset), (Synth.MidiRx, MidiRx), (Synth.Audio, Audio), (Synth.Playing, Led));
	}
}
