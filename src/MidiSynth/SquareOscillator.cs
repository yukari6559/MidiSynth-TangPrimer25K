using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// 位相から矩形波を出す回路。<see cref="Tick"/> のクロックごとに <see cref="Phase"/> へ <see cref="Increment"/> を足し、
/// <see cref="Phase"/> の最上位ビットを <see cref="Square"/> に出す。位相の 1 周の前半が 0、後半が 1 なので、デューティは 50%。
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

	/// <summary>矩形波。<see cref="Phase"/> の最上位ビット。</summary>
	public Out Square { get; } = Out.UInt(1, "square");

	/// <summary>今の位相。<see cref="PhaseWidth"/> ビットで 1 周する。</summary>
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
		SetPorts([Clk, Rst, Tick, Increment, Square]);
		SetLocal(Phase);
	}

	/// <summary>位相を進め、最上位ビットを出す回路を描く。</summary>
	public override void Describe()
	{
		Expr nextPhase = Tick.Eq(Lit.Bits(1, 1)).Mux(Phase + Increment, Phase);

		Seq(Clk, Rst, () => Phase.Assign(0, nextPhase));
		Comb(() => Square.Assign(Phase.Slice(PhaseWidth - 1, PhaseWidth - 1)));
	}
}
