using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// MIDI の受信線から、単音の矩形波を 1 ビットで出す回路。
/// <see cref="UartRx"/>、<see cref="NoteParser"/>、<see cref="PhaseIncrementTable"/>、<see cref="SampleTicker"/>、<see cref="SquareOscillator"/> をつなぐ。
/// <see cref="MidiRx"/> はクロックと無関係に変わるので、レジスタ 2 段に通してから <see cref="UartRx"/> に渡す。
/// </summary>
public sealed class MonoSynth : Module
{
	/// <summary>MIDI のボーレート（ビット毎秒）。</summary>
	public const double MidiBaud = 31250;

	private const double MaxBaudError = 0.02;

	private readonly UartRx Uart;
	private readonly NoteParser Parser;
	private readonly PhaseIncrementTable Table;
	private readonly SampleTicker Ticker;
	private readonly SquareOscillator Oscillator;

	private readonly Wire UartData = Wire.UInt(8, "uart_data");
	private readonly Wire UartValid = Wire.UInt(1, "uart_valid");
	private readonly Wire Note = Wire.UInt(7, "note");
	private readonly Wire Gate = Wire.UInt(1, "gate");
	private readonly Wire Increment;
	private readonly Wire Tick = Wire.UInt(1, "tick");

	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>同期リセット。1 のクロックで、すべての子回路を初めの状態に戻す。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>MIDI の受信線。空きは 1。</summary>
	public In MidiRx { get; } = In.UInt(1, "midi_rx");

	/// <summary>音声の 1 ビット出力。鍵盤を押しているあいだ矩形波、それ以外は 0。</summary>
	public Out Audio { get; } = Out.UInt(1, "audio");

	/// <summary>鍵盤を押しているあいだ 1。実機で、UART と Note On の解釈まで届いているかを LED で見るための出力。</summary>
	public Out Playing { get; } = Out.UInt(1, "playing");

	/// <summary><see cref="MidiRx"/> を 1 クロック遅らせた値。リセットでは空きの 1。</summary>
	public Reg RxSync1 { get; } = Reg.UInt(1, "rx_sync1");

	/// <summary><see cref="RxSync1"/> をさらに 1 クロック遅らせた値。<see cref="UartRx"/> に渡す。</summary>
	public Reg RxSync2 { get; } = Reg.UInt(1, "rx_sync2");

	/// <summary>クロック周波数（Hz）。</summary>
	public double ClockHz { get; }

	/// <summary>MIDI の 1 ビットのクロック数。<see cref="ClockHz"/> ÷ 31250 を丸めた数。</summary>
	public uint ClocksPerBit { get; }

	/// <summary>1 サンプルのクロック数。</summary>
	public uint ClocksPerSample { get; }

	/// <summary>サンプル周波数（Hz）。<see cref="ClockHz"/> ÷ <see cref="ClocksPerSample"/>。</summary>
	public double SampleRateHz { get; }

	/// <summary>位相の幅。</summary>
	public uint PhaseWidth { get; }

	/// <summary>単音シンセの回路を作る。</summary>
	/// <param name="clockHz">クロック周波数（Hz）。31250 の整数倍から 2% 以内にする。</param>
	/// <param name="clocksPerSample">1 サンプルのクロック数。サンプル周波数がノート 127（約 12544 Hz）より高くなるようにする。</param>
	/// <param name="phaseWidth">位相の幅（1〜64 ビット）。</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="clockHz"/> が有限の正の数でない、または MIDI のボーレートとのずれが 2% を超える。
	/// <paramref name="clocksPerSample"/> が 0、またはサンプル周波数が低すぎる。<paramref name="phaseWidth"/> が 1〜64 の外。
	/// </exception>
	public MonoSynth(double clockHz, uint clocksPerSample, uint phaseWidth)
	{
		if (!double.IsFinite(clockHz) || clockHz <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(clockHz), clockHz, "Clock frequency must be a finite positive number.");
		}
		double clocksPerBit = Math.Round(clockHz / MidiBaud);
		if (clocksPerBit < 1 || clocksPerBit > uint.MaxValue || Math.Abs((clockHz / clocksPerBit) - MidiBaud) > MidiBaud * MaxBaudError)
		{
			throw new ArgumentOutOfRangeException(nameof(clockHz), clockHz, "Clock frequency must be within 2% of a whole multiple of 31250 Hz.");
		}
		ArgumentOutOfRangeException.ThrowIfZero(clocksPerSample);

		ClockHz = clockHz;
		ClocksPerBit = (uint)clocksPerBit;
		ClocksPerSample = clocksPerSample;
		SampleRateHz = clockHz / clocksPerSample;
		PhaseWidth = phaseWidth;

		Uart = new UartRx(ClocksPerBit);
		Parser = new NoteParser();
		try
		{
			Table = new PhaseIncrementTable(SampleRateHz, phaseWidth);
		}
		catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "sampleRateHz")
		{
			throw new ArgumentOutOfRangeException(nameof(clocksPerSample), clocksPerSample, ex.Message);
		}
		Ticker = new SampleTicker(clocksPerSample);
		Oscillator = new SquareOscillator(phaseWidth);
		Increment = Wire.UInt(phaseWidth, "increment");

		SetPorts([Clk, Rst, MidiRx, Audio, Playing]);
		SetLocals([RxSync1, RxSync2, UartData, UartValid, Note, Gate, Increment, Tick]);
	}

	/// <summary>受信線の同期と、子回路のつなぎ方を描く。</summary>
	public override void Describe()
	{
		Seq(Clk, Rst, () =>
		{
			RxSync1.Assign(1, MidiRx);
			RxSync2.Assign(1, RxSync1);
		});
		Comb(() => Playing.Assign(Gate));

		Uart.Describe();
		Parser.Describe();
		Table.Describe();
		Ticker.Describe();
		Oscillator.Describe();

		Instance(Uart, "uart",
			(Uart.Clk, Clk), (Uart.Rst, Rst), (Uart.Rx, RxSync2), (Uart.Data, UartData), (Uart.Valid, UartValid));
		Instance(Parser, "parser",
			(Parser.Clk, Clk), (Parser.Rst, Rst), (Parser.MidiByte, UartData), (Parser.MidiValid, UartValid),
			(Parser.Note, Note), (Parser.Gate, Gate));
		Instance(Table, "increment_table",
			(Table.Note, Note), (Table.Increment, Increment));
		Instance(Ticker, "ticker",
			(Ticker.Clk, Clk), (Ticker.Rst, Rst), (Ticker.Tick, Tick));
		Instance(Oscillator, "oscillator",
			(Oscillator.Clk, Clk), (Oscillator.Rst, Rst), (Oscillator.Tick, Tick), (Oscillator.Increment, Increment),
			(Oscillator.Gate, Gate), (Oscillator.Square, Audio));
	}
}
