using System.Numerics;
using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// 電源投入の直後だけリセットを出す回路。FPGA のレジスタはコンフィギュレーション直後に 0 になる前提で、
/// そこから <see cref="HoldClocks"/> クロックのあいだ <see cref="Reset"/> を 1 にし、その後は 0 のままにする。
/// </summary>
public sealed class PowerOnReset : Module
{
	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>数え直しの入力。1 のクロックで <see cref="Reset"/> をもう一度出す。ボードでは 0 につなぐ。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>電源投入から <see cref="HoldClocks"/> クロックのあいだ 1。</summary>
	public Out Reset { get; } = Out.UInt(1, "reset");

	/// <summary>電源投入から数えたクロック数。<see cref="HoldClocks"/> で止まる。</summary>
	public Reg Count { get; }

	/// <summary><see cref="Reset"/> を 1 にしておくクロック数。</summary>
	public uint HoldClocks { get; }

	/// <summary>リセットの回路を作る。</summary>
	/// <param name="holdClocks"><see cref="Reset"/> を 1 にしておくクロック数。0 は受け付けない。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="holdClocks"/> が 0。</exception>
	public PowerOnReset(uint holdClocks)
	{
		ArgumentOutOfRangeException.ThrowIfZero(holdClocks);
		HoldClocks = holdClocks;
		Count = Reg.UInt((uint)(32 - BitOperations.LeadingZeroCount(holdClocks)), "count");
		SetPorts([Clk, Rst, Reset]);
		SetLocal(Count);
	}

	/// <summary>数え終わるまでリセットを出す回路を描く。</summary>
	public override void Describe()
	{
		Expr holding = Count.Neq(Lit.Bits(Count.Width, HoldClocks));
		Expr nextCount = holding.Mux(Count + Lit.Bits(Count.Width, 1), Count);

		Seq(Clk, Rst, () => Count.Assign(0, nextCount));
		Comb(() => Reset.Assign(holding));
	}
}
