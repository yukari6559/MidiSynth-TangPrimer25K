using System.Numerics;
using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// サンプル周波数の合図を作る回路。<see cref="ClocksPerSample"/> クロックに 1 回だけ <see cref="Tick"/> を 1 にする。
/// サンプル周波数はクロック周波数 ÷ <see cref="ClocksPerSample"/>。
/// </summary>
public sealed class SampleTicker : Module
{
	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>同期リセット。1 のクロックで数え直す。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>サンプルを進めるクロックだけ 1。</summary>
	public Out Tick { get; } = Out.UInt(1, "tick");

	/// <summary>前の <see cref="Tick"/> から数えたクロック数。0 から <see cref="ClocksPerSample"/> − 1 まで。</summary>
	public Reg Count { get; }

	/// <summary>1 サンプルのクロック数。</summary>
	public uint ClocksPerSample { get; }

	/// <summary>合図の回路を作る。</summary>
	/// <param name="clocksPerSample">1 サンプルのクロック数。0 は受け付けない。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="clocksPerSample"/> が 0。</exception>
	public SampleTicker(uint clocksPerSample)
	{
		ArgumentOutOfRangeException.ThrowIfZero(clocksPerSample);
		ClocksPerSample = clocksPerSample;
		uint countWidth = Math.Max(1u, (uint)(32 - BitOperations.LeadingZeroCount(clocksPerSample - 1)));
		Count = Reg.UInt(countWidth, "count");
		SetPorts([Clk, Rst, Tick]);
		SetLocal(Count);
	}

	/// <summary>クロックを数え、最後のクロックで合図を出す回路を描く。</summary>
	public override void Describe()
	{
		Expr last = Count.Eq(Lit.Bits(Count.Width, ClocksPerSample - 1));
		Expr nextCount = last.Mux(Lit.Bits(Count.Width, 0), Count + Lit.Bits(Count.Width, 1));

		Seq(Clk, Rst, () => Count.Assign(0, nextCount));
		Comb(() => Tick.Assign(last));
	}
}
