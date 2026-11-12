using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// MIDI のバイト列から、チャンネル 1 の Note On と Note Off だけを取り出し、1 音分の状態にする回路。
/// ベロシティ 0 の Note On は Note Off と同じに扱う。ランニングステータスに対応し、リアルタイムメッセージ（0xF8〜0xFF）は読み飛ばす。
/// 別のノートの Note On が来たら、そのノートに切り替える。今鳴っていないノートの Note Off は無視する。
/// </summary>
public sealed class NoteParser : Module
{
	private const uint StatusNone = 0;
	private const uint StatusNoteOff = 1;
	private const uint StatusNoteOn = 2;

	/// <summary>クロック。</summary>
	public In Clk { get; } = In.UInt(1, "clk");

	/// <summary>同期リセット。1 のクロックで、ランニングステータスと <see cref="Note"/>、<see cref="Gate"/> を 0 に戻す。</summary>
	public In Rst { get; } = In.UInt(1, "rst");

	/// <summary>受け取った MIDI のバイト。<see cref="UartRx.Data"/> をつなぐ。</summary>
	public In MidiByte { get; } = In.UInt(8, "midi_byte");

	/// <summary><see cref="MidiByte"/> が新しいバイトのクロックだけ 1。<see cref="UartRx.Valid"/> をつなぐ。</summary>
	public In MidiValid { get; } = In.UInt(1, "midi_valid");

	/// <summary>最後に Note On を受けたノート番号（0〜127）。Note Off のあとも保持する。</summary>
	public Out Note { get; } = Out.UInt(7, "note");

	/// <summary><see cref="Note"/> の鍵盤を押しているあいだ 1。</summary>
	public Out Gate { get; } = Out.UInt(1, "gate");

	/// <summary>今のランニングステータス。0 は読まないメッセージ、1 は Note Off、2 は Note On。</summary>
	public Reg RunningStatus { get; } = Reg.UInt(2, "running_status");

	/// <summary>ノート番号のデータバイトを受け取り、ベロシティを待っているあいだ 1。</summary>
	public Reg HaveKey { get; } = Reg.UInt(1, "have_key");

	/// <summary>ベロシティを待っているノート番号。</summary>
	public Reg Key { get; } = Reg.UInt(7, "key");

	/// <summary>回路を作る。</summary>
	public NoteParser()
	{
		SetPorts([Clk, Rst, MidiByte, MidiValid, Note, Gate]);
		SetLocals([RunningStatus, HaveKey, Key]);
	}

	/// <summary>バイトを受け取るたびに、ランニングステータスとノートの状態を進める回路を描く。</summary>
	public override void Describe()
	{
		Expr valid = MidiValid.Eq(Lit.Bits(1, 1));
		Expr isStatus = MidiByte.Slice(7, 7).Eq(Lit.Bits(1, 1));
		Expr isRealtime = MidiByte >= Lit.Bits(8, 0xF8);
		Expr dataValue = MidiByte.Slice(6, 0);

		Expr statusIn = valid.LogicAnd(isStatus).LogicAnd(!isRealtime);
		Expr dataIn = valid.LogicAnd(!isStatus);
		Expr listening = RunningStatus.Neq(Lit.Bits(2, StatusNone));
		Expr keyIn = dataIn.LogicAnd(listening).LogicAnd(!HaveKey);
		Expr velocityIn = dataIn.LogicAnd(listening).LogicAnd(HaveKey);

		Expr statusKind = MidiByte.Eq(Lit.Bits(8, 0x90)).Mux(
			Lit.Bits(2, StatusNoteOn),
			MidiByte.Eq(Lit.Bits(8, 0x80)).Mux(Lit.Bits(2, StatusNoteOff), Lit.Bits(2, StatusNone)));

		Expr inNoteOn = RunningStatus.Eq(Lit.Bits(2, StatusNoteOn));
		Expr inNoteOff = RunningStatus.Eq(Lit.Bits(2, StatusNoteOff));
		Expr velocityZero = dataValue.Eq(Lit.Bits(7, 0));
		Expr noteOn = velocityIn.LogicAnd(inNoteOn).LogicAnd(!velocityZero);
		Expr releases = inNoteOff.LogicOr(inNoteOn.LogicAnd(velocityZero));
		Expr noteOff = velocityIn.LogicAnd(releases).LogicAnd(Gate).LogicAnd(Note.Eq(Key));

		Expr nextRunningStatus = statusIn.Mux(statusKind, RunningStatus);
		Expr nextHaveKey = keyIn.LogicOr(HaveKey.LogicAnd(!(statusIn.LogicOr(velocityIn))));
		Expr nextKey = keyIn.Mux(dataValue, Key);
		Expr nextNote = noteOn.Mux(Key, Note);
		Expr nextGate = noteOn.LogicOr(Gate.LogicAnd(!noteOff));

		Seq(Clk, Rst, () =>
		{
			RunningStatus.Assign(0, nextRunningStatus);
			HaveKey.Assign(0, nextHaveKey);
			Key.Assign(0, nextKey);
			Note.Assign(0, nextNote);
			Gate.Assign(0, nextGate);
		});
	}
}
