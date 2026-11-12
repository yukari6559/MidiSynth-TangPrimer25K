using SharpHdl.Core.Model;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class PhaseIncrementTableTests
{
	private const double SampleRateHz = 48000;
	private const uint PhaseWidth = 24;

	[Fact]
	public void PortsAreNoteAndIncrement()
	{
		List<Signal> ports = [.. new PhaseIncrementTable(SampleRateHz, PhaseWidth).GetPorts()];

		Assert.Equal(["note", "increment"], ports.Select(p => p.Name));
		Assert.Equal([7u, PhaseWidth], ports.Select(p => p.Width));
	}

	[Theory]
	[InlineData(0, 8.1758)]
	[InlineData(60, 261.6256)]
	[InlineData(69, 440.0)]
	[InlineData(81, 880.0)]
	[InlineData(127, 12543.8540)]
	public void NoteFrequencyIsEqualTemperamentFromA440(int note, double hz)
	{
		Assert.Equal(hz, PhaseIncrementTable.NoteFrequencyHz(note), 4);
	}

	[Theory]
	[InlineData(0, 2858ul)]
	[InlineData(60, 91445ul)]
	[InlineData(69, 153791ul)]
	[InlineData(81, 307582ul)]
	[InlineData(127, 4384395ul)]
	public void SimGivesIncrementForNote(int note, ulong increment)
	{
		SimSession<PhaseIncrementTable> s = new PhaseIncrementTable(SampleRateHz, PhaseWidth).Run();

		s.Set(s.Module.Note, (ulong)note);
		s.Settle();

		Assert.Equal(increment, s.Get(s.Module.Increment));
	}

	[Theory]
	[InlineData(48000.0, 24u)]
	[InlineData(52734.375, 24u)]
	[InlineData(27_000_000.0 / 512, 32u)]
	public void NoteSixtyNineIncrementTurnsAt440Hz(double sampleRateHz, uint phaseWidth)
	{
		SimSession<PhaseIncrementTable> s = new PhaseIncrementTable(sampleRateHz, phaseWidth).Run();

		s.Set(s.Module.Note, 69);
		s.Settle();

		double turnsPerSecond = s.Get(s.Module.Increment) * sampleRateHz / Math.ScaleB(1.0, (int)phaseWidth);
		double resolutionHz = sampleRateHz / Math.ScaleB(1.0, (int)phaseWidth);
		Assert.InRange(turnsPerSecond, 440.0 - resolutionHz, 440.0 + resolutionHz);
	}

	[Fact]
	public void SimMatchesIncrementForEveryNote()
	{
		PhaseIncrementTable table = new(SampleRateHz, PhaseWidth);
		SimSession<PhaseIncrementTable> s = table.Run();

		for (int note = 0; note < 128; note++)
		{
			s.Set(s.Module.Note, (ulong)note);
			s.Settle();
			Assert.Equal(table.IncrementFor(note), s.Get(s.Module.Increment));
		}
	}

	[Fact]
	public void IncrementsRiseAndDoubleEachOctave()
	{
		PhaseIncrementTable table = new(SampleRateHz, PhaseWidth);

		for (int note = 1; note < 128; note++)
		{
			Assert.True(table.IncrementFor(note) > table.IncrementFor(note - 1));
		}
		for (int note = 12; note < 128; note++)
		{
			Assert.InRange((long)table.IncrementFor(note) - (2 * (long)table.IncrementFor(note - 12)), -1, 1);
		}
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(-48000.0)]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(12000.0)]
	public void BadSampleRateIsRejected(double sampleRateHz)
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseIncrementTable(sampleRateHz, PhaseWidth));
		Assert.Equal("sampleRateHz", ex.ParamName);
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(65u)]
	public void BadPhaseWidthIsRejected(uint phaseWidth)
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseIncrementTable(SampleRateHz, phaseWidth));
		Assert.Equal("phaseWidth", ex.ParamName);
	}

	[Theory]
	[InlineData(1u)]
	[InlineData(64u)]
	public void PhaseWidthEdgesAreAccepted(uint phaseWidth)
	{
		PhaseIncrementTable table = new(SampleRateHz, phaseWidth);

		Assert.Equal(phaseWidth, table.Increment.Width);
	}

	[Theory]
	[InlineData(-1)]
	[InlineData(128)]
	public void NoteOutsideMidiRangeIsRejected(int note)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => PhaseIncrementTable.NoteFrequencyHz(note));
		Assert.Throws<ArgumentOutOfRangeException>(() => new PhaseIncrementTable(SampleRateHz, PhaseWidth).IncrementFor(note));
	}

	[Fact]
	public void NoteFromNoteParserSelectsIncrement()
	{
		SimSession<NoteParser> parser = new NoteParser().Run();
		SimSession<PhaseIncrementTable> table = new PhaseIncrementTable(SampleRateHz, PhaseWidth).Run();
		parser.Set(parser.Module.Rst, 1);
		parser.Advance(parser.Module.Clk);
		parser.Set(parser.Module.Rst, 0);

		foreach (byte b in new byte[] { 0x90, 0x45, 0x64 })
		{
			parser.Set(parser.Module.MidiByte, b);
			parser.Set(parser.Module.MidiValid, 1);
			parser.Advance(parser.Module.Clk);
		}
		table.Set(table.Module.Note, parser.Get(parser.Module.Note));
		table.Settle();

		Assert.Equal(153791ul, table.Get(table.Module.Increment));
	}
}
