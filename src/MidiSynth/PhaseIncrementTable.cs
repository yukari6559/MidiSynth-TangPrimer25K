using SharpHdl.Core.Model;

namespace MidiSynth;

/// <summary>
/// ノート番号から、1 サンプルごとに位相へ足す数（位相増分）を引く表の回路。
/// 音高は平均律で、A4（ノート 69）を 440 Hz とする。位相は <see cref="PhaseWidth"/> ビットで 1 周する。
/// 128 ノート分の増分は回路を作るときに計算し、クロックを待たずに <see cref="Note"/> から <see cref="Increment"/> が決まる。
/// </summary>
public sealed class PhaseIncrementTable : Module
{
	private const int NoteCount = 128;
	private const int ReferenceNote = 69;
	private const double ReferenceFrequencyHz = 440.0;

	/// <summary>ノート番号（0〜127）。<see cref="NoteParser.Note"/> をつなぐ。</summary>
	public In Note { get; } = In.UInt(7, "note");

	/// <summary><see cref="Note"/> の位相増分。幅は <see cref="PhaseWidth"/>。</summary>
	public Out Increment { get; }

	/// <summary>位相を足すサンプル周波数（Hz）。</summary>
	public double SampleRateHz { get; }

	/// <summary>位相と位相増分の幅。位相はこのビット数で 1 周する。</summary>
	public uint PhaseWidth { get; }

	private readonly ulong[] Increments;

	/// <summary>表の回路を作る。</summary>
	/// <param name="sampleRateHz">位相を足すサンプル周波数（Hz）。ノート 127（約 12544 Hz）より高くする。</param>
	/// <param name="phaseWidth">位相の幅（1〜64 ビット）。</param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="sampleRateHz"/> が有限の正の数でない、またはノート 127 の増分が位相の 1 周以上になる。
	/// <paramref name="phaseWidth"/> が 1〜64 の外。
	/// </exception>
	public PhaseIncrementTable(double sampleRateHz, uint phaseWidth)
	{
		if (!double.IsFinite(sampleRateHz) || sampleRateHz <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, "Sample rate must be a finite positive number.");
		}
		if (phaseWidth is 0 or > 64)
		{
			throw new ArgumentOutOfRangeException(nameof(phaseWidth), phaseWidth, "Phase width must be between 1 and 64.");
		}

		double fullTurn = Math.ScaleB(1.0, (int)phaseWidth);
		Increments = new ulong[NoteCount];
		for (int note = 0; note < NoteCount; note++)
		{
			double increment = Math.Round(NoteFrequencyHz(note) / sampleRateHz * fullTurn, MidpointRounding.AwayFromZero);
			if (increment >= fullTurn)
			{
				throw new ArgumentOutOfRangeException(nameof(sampleRateHz), sampleRateHz, $"Note {note} does not fit below the sample rate.");
			}
			Increments[note] = (ulong)increment;
		}

		SampleRateHz = sampleRateHz;
		PhaseWidth = phaseWidth;
		Increment = Out.UInt(phaseWidth, "increment");
		SetPorts([Note, Increment]);
	}

	/// <summary>平均律でのノートの周波数（Hz）。ノート 69 が 440 Hz。</summary>
	/// <param name="note">ノート番号（0〜127）。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="note"/> が 0〜127 の外。</exception>
	public static double NoteFrequencyHz(int note)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(note);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(note, NoteCount);
		return ReferenceFrequencyHz * Math.Pow(2.0, (note - ReferenceNote) / 12.0);
	}

	/// <summary>この表がノートに出す位相増分。</summary>
	/// <param name="note">ノート番号（0〜127）。</param>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="note"/> が 0〜127 の外。</exception>
	public ulong IncrementFor(int note)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(note);
		ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(note, NoteCount);
		return Increments[note];
	}

	/// <summary>ノート番号で増分を選ぶ表を描く。</summary>
	public override void Describe()
	{
		Comb(() => Switch(Note, [.. Enumerable.Range(0, NoteCount).Select(note =>
			((uint)note, (Action)(() => Increment.Assign(Lit.Bits(PhaseWidth, Increments[note])))))]));
	}
}
