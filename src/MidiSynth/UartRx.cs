using System.Numerics;
using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// MIDI の UART（8 ビット、パリティなし、ストップ 1 ビット、下位ビットが先）から 1 バイトずつ受け取る回路。
/// 各ビットの中央で受信線を読み、ストップビットが 1 なら <see cref="Data"/> を更新して <see cref="Valid"/> を 1 クロックだけ 1 にする。
/// </summary>
public sealed class UartRx : Module
{
	private const uint StopBitIndex = 9;

	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>同期リセット。1 のクロックで、受信の途中経過と <see cref="Data"/>、<see cref="Valid"/> を 0 に戻す。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>受信線。空きは 1、開始ビットは 0。</summary>
	public In Rx { get; } = In.UInt(1, "rx");

	/// <summary>最後に受け取ったバイト。次のバイトを受け取るまで保持する。</summary>
	public Out Data { get; } = Out.UInt(8, "data");

	/// <summary><see cref="Data"/> が新しいバイトになったクロックだけ 1。</summary>
	public Out Valid { get; } = Out.UInt(1, "valid");

	/// <summary>開始ビットからストップビットの中央までのあいだ 1、待ちは 0。</summary>
	public Reg Busy { get; } = Reg.UInt(1, "busy");

	/// <summary>今のビットで既に使ったクロック数。待ちは 0。</summary>
	public Reg Phase { get; }

	/// <summary>今どのビットか。0 が開始ビット、1〜8 がデータビット、9 がストップビット。</summary>
	public Reg BitIndex { get; } = Reg.UInt(4, "bit_index");

	/// <summary>受け取り途中のデータ。新しいビットを最上位に入れ、右へずらす。</summary>
	public Reg Shift { get; } = Reg.UInt(8, "shift");

	/// <summary>1 ビットを何クロックとして数えるか。</summary>
	public uint ClocksPerBit { get; }

	private readonly uint PhaseWidth;

	/// <summary>受信回路を作る。</summary>
	/// <param name="clocksPerBit">1 ビットのクロック数。クロック周波数を 31250 で割った数。0 は受け付けない。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="clocksPerBit"/> が 0。</exception>
	public UartRx(uint clocksPerBit)
	{
		ArgumentOutOfRangeException.ThrowIfZero(clocksPerBit);
		ClocksPerBit = clocksPerBit;
		PhaseWidth = (uint)(32 - BitOperations.LeadingZeroCount(clocksPerBit));
		Phase = Reg.UInt(PhaseWidth, "phase");
		SetPorts([Clk, Rst, Rx, Data, Valid]);
		SetLocals([Busy, Phase, BitIndex, Shift]);
	}

	/// <summary>受信の状態をクロックごとに進める回路を描く。</summary>
	public override void Describe()
	{
		Expr rxLow = Rx.Eq(Lit.Bits(1, 0));
		Expr rxHigh = Rx.Eq(Lit.Bits(1, 1));
		Expr counting = Busy.LogicOr(rxLow);
		Expr phasePlusOne = Phase + Lit.Bits(PhaseWidth, 1);
		Expr bitDone = phasePlusOne.Eq(Lit.Bits(PhaseWidth, ClocksPerBit));
		Expr atMiddle = counting.LogicAnd(Phase.Eq(Lit.Bits(PhaseWidth, ClocksPerBit / 2)));

		Expr inStartBit = BitIndex.Eq(Lit.Bits(4, 0));
		Expr inStopBit = BitIndex.Eq(Lit.Bits(4, StopBitIndex));
		Expr inDataBit = (!inStartBit).LogicAnd(!inStopBit);

		Expr falseStart = atMiddle.LogicAnd(inStartBit).LogicAnd(rxHigh);
		Expr frameEnd = atMiddle.LogicAnd(inStopBit);
		Expr keepCounting = counting.LogicAnd(!(falseStart.LogicOr(frameEnd)));
		Expr byteReady = frameEnd.LogicAnd(rxHigh);

		Expr nextBusy = keepCounting;
		Expr nextPhase = keepCounting.LogicAnd(!bitDone).Mux(phasePlusOne, Lit.Bits(PhaseWidth, 0));
		Expr nextBitIndex = keepCounting.Mux(bitDone.Mux(BitIndex + Lit.Bits(4, 1), BitIndex), Lit.Bits(4, 0));
		Expr nextShift = atMiddle.LogicAnd(inDataBit).Mux(Rx.Concat(Shift.Slice(7, 1)), Shift);
		Expr nextData = byteReady.Mux(Shift, Data);

		Seq(Clk, Rst, () =>
		{
			Busy.Assign(0, nextBusy);
			Phase.Assign(0, nextPhase);
			BitIndex.Assign(0, nextBitIndex);
			Shift.Assign(0, nextShift);
			Data.Assign(0, nextData);
			Valid.Assign(0, byteReady);
		});
	}
}
