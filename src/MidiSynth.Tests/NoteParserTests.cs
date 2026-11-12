using SharpHdl.Core.Model;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class NoteParserTests
{
	[Fact]
	public void PortsAreClockResetByteValidNoteGate()
	{
		List<Signal> ports = [.. new NoteParser().GetPorts()];

		Assert.Equal(["clk", "rst", "midi_byte", "midi_valid", "note", "gate"], ports.Select(p => p.Name));
		Assert.Equal([1u, 1u, 8u, 1u, 7u, 1u], ports.Select(p => p.Width));
	}

	[Fact]
	public void ResetLeavesSilence()
	{
		SimSession<NoteParser> s = Start();

		Assert.Equal((0u, 0u), State(s));
	}

	[Fact]
	public void NoteOnSetsNoteAndGate()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);

		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void NoteOffAfterNoteOnClosesGateAndKeepsNote()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0x80, 0x3C, 0x40);

		Assert.Equal((60u, 0u), State(s));
	}

	[Fact]
	public void NoteOnWithZeroVelocityIsNoteOff()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x45, 0x64);
		Send(s, 0x90, 0x45, 0x00);

		Assert.Equal((69u, 0u), State(s));
	}

	[Fact]
	public void RunningStatusReusesNoteOn()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0x3C, 0x00);
		Assert.Equal((60u, 0u), State(s));

		Send(s, 0x3E, 0x64);
		Assert.Equal((62u, 1u), State(s));
	}

	[Fact]
	public void RunningStatusReusesNoteOff()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0x80, 0x3E, 0x40);
		Send(s, 0x3C, 0x40);

		Assert.Equal((60u, 0u), State(s));
	}

	[Fact]
	public void PartialMessageIsKeptAcrossIdleClocks()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C);
		Idle(s, 20);
		Assert.Equal((0u, 0u), State(s));

		Send(s, 0x64);
		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void OtherChannelIsIgnoredIncludingItsRunningStatus()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x91, 0x3C, 0x64);
		Send(s, 0x3E, 0x64);

		Assert.Equal((0u, 0u), State(s));
	}

	[Fact]
	public void OtherChannelNoteOffDoesNotStopChannelOne()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0x81, 0x3C, 0x40);

		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void ControlChangeAndPitchBendAreIgnored()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0xB0, 0x07, 0x00);
		Send(s, 0xE0, 0x00, 0x40);
		Assert.Equal((60u, 1u), State(s));

		Send(s, 0x3C, 0x00);
		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void SystemExclusiveEndsRunningStatus()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0xF0, 0x7E, 0x00, 0xF7);
		Send(s, 0x3C, 0x00);

		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void RealtimeBytesInsideAMessageAreSkipped()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0xF8, 0x3C, 0xFE, 0x64);
		Assert.Equal((60u, 1u), State(s));

		Send(s, 0xF8, 0x3C, 0xF8, 0x00);
		Assert.Equal((60u, 0u), State(s));
	}

	[Fact]
	public void NoteOffForAnotherKeyKeepsGate()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64);
		Send(s, 0x80, 0x3E, 0x40);

		Assert.Equal((60u, 1u), State(s));
	}

	[Fact]
	public void NewNoteOnSwitchesNoteAndOldNoteOffIsIgnored()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x90, 0x3C, 0x64, 0x3E, 0x64);
		Assert.Equal((62u, 1u), State(s));

		Send(s, 0x80, 0x3C, 0x40);
		Assert.Equal((62u, 1u), State(s));

		Send(s, 0x3E, 0x40);
		Assert.Equal((62u, 0u), State(s));
	}

	[Fact]
	public void DataBytesBeforeAnyStatusAreIgnored()
	{
		SimSession<NoteParser> s = Start();

		Send(s, 0x3C, 0x64);

		Assert.Equal((0u, 0u), State(s));
	}

	[Fact]
	public void BytesWithoutValidAreIgnored()
	{
		SimSession<NoteParser> s = Start();

		foreach (byte b in new byte[] { 0x90, 0x3C, 0x64 })
		{
			s.Set(s.Module.MidiByte, b);
			s.Set(s.Module.MidiValid, 0);
			s.Advance(s.Module.Clk);
		}

		Assert.Equal((0u, 0u), State(s));
	}

	[Fact]
	public void BytesFromUartRxReachTheParser()
	{
		const uint clocksPerBit = 4;
		SimSession<UartRx> rx = new UartRx(clocksPerBit).Run();
		SimSession<NoteParser> parser = Start();
		rx.Set(rx.Module.Rx, 1);
		rx.Set(rx.Module.Rst, 1);
		rx.Advance(rx.Module.Clk);
		rx.Set(rx.Module.Rst, 0);

		List<(uint Note, uint Gate)> seen = [];
		foreach (int level in UartLine(clocksPerBit, [0x90, 0x45, 0x64, 0x80, 0x45, 0x00]))
		{
			rx.Set(rx.Module.Rx, (ulong)level);
			rx.Advance(rx.Module.Clk);
			parser.Set(parser.Module.MidiByte, rx.Get(rx.Module.Data));
			parser.Set(parser.Module.MidiValid, rx.Get(rx.Module.Valid));
			parser.Advance(parser.Module.Clk);
			if (seen.Count == 0 || seen[^1] != State(parser))
			{
				seen.Add(State(parser));
			}
		}

		Assert.Equal([(0u, 0u), (69u, 1u), (69u, 0u)], seen);
	}

	private static SimSession<NoteParser> Start()
	{
		SimSession<NoteParser> s = new NoteParser().Run();
		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);
		return s;
	}

	private static void Send(SimSession<NoteParser> s, params byte[] bytes)
	{
		foreach (byte b in bytes)
		{
			s.Set(s.Module.MidiByte, b);
			s.Set(s.Module.MidiValid, 1);
			s.Advance(s.Module.Clk);
			Idle(s, 3);
		}
	}

	private static void Idle(SimSession<NoteParser> s, int clocks)
	{
		s.Set(s.Module.MidiValid, 0);
		s.Advance(s.Module.Clk, clocks);
	}

	private static (uint Note, uint Gate) State(SimSession<NoteParser> s)
	{
		return ((uint)s.Get(s.Module.Note), (uint)s.Get(s.Module.Gate));
	}

	private static List<int> UartLine(uint clocksPerBit, IEnumerable<byte> bytes)
	{
		int n = (int)clocksPerBit;
		List<int> levels = [.. Enumerable.Repeat(1, 2 * n)];
		foreach (byte b in bytes)
		{
			levels.AddRange(Enumerable.Repeat(0, n));
			for (int i = 0; i < 8; i++)
			{
				levels.AddRange(Enumerable.Repeat((b >> i) & 1, n));
			}
			levels.AddRange(Enumerable.Repeat(1, 2 * n));
		}
		return levels;
	}
}
