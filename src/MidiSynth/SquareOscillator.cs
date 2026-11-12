using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// 位相から矩形波を出す回路。<see cref="Gate"/> が 1 のあいだ、<see cref="Tick"/> のクロックごとに <see cref="Phase"/> へ <see cref="Increment"/> を足し、
/// <see cref="Phase"/> の最上位ビットを <see cref="Square"/> に出す。位相の 1 周の前半が 0、後半が 1 なので、デューティは 50%。
/// <see cref="Gate"/> が 0 のあいだは <see cref="Square"/> を 0 にし、<see cref="Phase"/> を 0 に止める。どの音も位相 0 から鳴り始める。
/// </summary>
public sealed class SquareOscillator : Module
{
	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>同期リセット。1 のクロックで位相を 0 に戻す。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>位相を進めるクロックだけ 1。<see cref="SampleTicker.Tick"/> をつなぐ。</summary>
	public In Tick { get; } = In.UInt(1, "tick");

	/// <summary>1 サンプルごとに位相へ足す数。<see cref="PhaseIncrementTable.Increment"/> をつなぐ。</summary>
	public In Increment { get; }

	/// <summary>鍵盤を押しているあいだ 1。<see cref="NoteParser.Gate"/> をつなぐ。</summary>
	public In Gate { get; } = In.UInt(1, "gate");

	/// <summary>矩形波。<see cref="Gate"/> が 1 のときは <see cref="Phase"/> の最上位ビット、0 のときは 0。</summary>
	public Out Square { get; } = Out.UInt(1, "square");

	/// <summary>今の位相。<see cref="PhaseWidth"/> ビットで 1 周する。<see cref="Gate"/> が 0 のあいだは 0。</summary>
	public Reg Phase { get; }

	/// <summary>位相と <see cref="Increment"/> の幅。</summary>
	public uint PhaseWidth { get; }

	/// <summary>矩形波の回路を作る。</summary>
	/// <param name="phaseWidth">位相の幅（1〜64 ビット）。<see cref="PhaseIncrementTable.PhaseWidth"/> と同じにする。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="phaseWidth"/> が 1〜64 の外。</exception>
	public SquareOscillator(uint phaseWidth)
	{
		if (phaseWidth is 0 or > 64)
		{
			throw new ArgumentOutOfRangeException(nameof(phaseWidth), phaseWidth, "Phase width must be between 1 and 64.");
		}
		PhaseWidth = phaseWidth;
		Increment = In.UInt(phaseWidth, "increment");
		Phase = Reg.UInt(phaseWidth, "phase");
		SetPorts([Clk, Rst, Tick, Increment, Gate, Square]);
		SetLocal(Phase);
	}

	/// <summary>鍵盤を押しているあいだ位相を進め、最上位ビットを出す回路を描く。</summary>
	public override void Describe()
	{
		Expr gateOn = Gate.Eq(Lit.Bits(1, 1));
		Expr advanced = Tick.Eq(Lit.Bits(1, 1)).Mux(Phase + Increment, Phase);
		Expr nextPhase = gateOn.Mux(advanced, Lit.Bits(PhaseWidth, 0));

		Seq(Clk, Rst, () => Phase.Assign(0, nextPhase));
		Comb(() => Square.Assign(gateOn.LogicAnd(Phase.Slice(PhaseWidth - 1, PhaseWidth - 1))));
	}
}
