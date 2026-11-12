using SharpHdl.Core.Model;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class MonoSynthTests
{
	private const double ClockHz = 125_000;
	private const uint ClocksPerSample = 8;
	private const uint PhaseWidth = 16;
	private const int ClocksPerBit = 4;

	[Fact]
	public void PortsAreClockResetMidiRxAudio()
	{
		List<Signal> ports = [.. new MonoSynth(ClockHz, ClocksPerSample, PhaseWidth).GetPorts()];

		Assert.Equal(["clk", "rst", "midi_rx", "audio"], ports.Select(p => p.Name));
		Assert.Equal([1u, 1u, 1u, 1u], ports.Select(p => p.Width));
	}

	[Fact]
	public void BoardClockGivesMidiBitAndSampleRate()
	{
		MonoSynth synth = new(50_000_000, 1024, 24);

		Assert.Equal(1600u, synth.ClocksPerBit);
		Assert.Equal(48828.125, synth.SampleRateHz);
	}

	[Fact]
	public void NoteOnPlaysSquareAndNoteOffSilences()
	{
		SimSession<MonoSynth> s = Start();

		List<int> beforeNote = Play(s, Idle(50));
		List<int> noteOnFrames = Play(s, Uart(0x90, 0x45, 0x64));
		List<int> held = Play(s, Idle(3000));
		Play(s, Uart(0x80, 0x45, 0x00));
		List<int> afterNoteOff = Play(s, Idle(2000));

		Assert.All(beforeNote, v => Assert.Equal(0, v));
		Assert.All(noteOnFrames, v => Assert.Equal(0, v));

		List<int> rises = [.. Enumerable.Range(1, held.Count - 1).Where(i => held[i - 1] == 0 && held[i] == 1)];
		Assert.True(rises.Count >= 8, $"only {rises.Count} rising edges");
		double periodClocks = (double)(rises[^1] - rises[0]) / (rises.Count - 1);
		Assert.InRange(ClockHz / periodClocks, 440 * 0.99, 440 * 1.01);
		double duty = (double)held.Skip(rises[0]).Take(rises[^1] - rises[0]).Count(v => v == 1) / (rises[^1] - rises[0]);
		Assert.InRange(duty, 0.45, 0.55);

		Assert.True(afterNoteOff.LastIndexOf(1) < ClocksPerBit, $"still sounding {afterNoteOff.LastIndexOf(1)} clocks after the Note Off frame");
		Assert.All(afterNoteOff.Skip(ClocksPerBit), v => Assert.Equal(0, v));
	}

	[Fact]
	public void HigherNotePlaysHigherPitch()
	{
		SimSession<MonoSynth> s = Start();

		Play(s, Uart(0x90, 0x51, 0x64));
		List<int> held = Play(s, Idle(3000));

		List<int> rises = [.. Enumerable.Range(1, held.Count - 1).Where(i => held[i - 1] == 0 && held[i] == 1)];
		double periodClocks = (double)(rises[^1] - rises[0]) / (rises.Count - 1);
		Assert.InRange(ClockHz / periodClocks, 880 * 0.98, 880 * 1.02);
	}

	[Fact]
	public void OtherChannelStaysSilent()
	{
		SimSession<MonoSynth> s = Start();

		Play(s, Uart(0x91, 0x45, 0x64));

		Assert.All(Play(s, Idle(1000)), v => Assert.Equal(0, v));
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(0.0)]
	[InlineData(-125_000.0)]
	[InlineData(20_000.0)]
	[InlineData(40_000.0)]
	public void ClockFarFromMidiBaudIsRejected(double clockHz)
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MonoSynth(clockHz, 1, PhaseWidth));
		Assert.Equal("clockHz", ex.ParamName);
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(10u)]
	public void SampleRateTooLowIsRejected(uint clocksPerSample)
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new MonoSynth(ClockHz, clocksPerSample, PhaseWidth));
		Assert.Equal("clocksPerSample", ex.ParamName);
	}

	private static SimSession<MonoSynth> Start()
	{
		SimSession<MonoSynth> s = new MonoSynth(ClockHz, ClocksPerSample, PhaseWidth).Run();
		s.Set(s.Module.MidiRx, 1);
		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);
		return s;
	}

	private static List<int> Play(SimSession<MonoSynth> s, IEnumerable<int> levels)
	{
		List<int> audio = [];
		foreach (int level in levels)
		{
			s.Set(s.Module.MidiRx, (ulong)level);
			s.Advance(s.Module.Clk);
			audio.Add((int)s.Get(s.Module.Audio));
		}
		return audio;
	}

	private static IEnumerable<int> Idle(int clocks)
	{
		return Enumerable.Repeat(1, clocks);
	}

	private static List<int> Uart(params byte[] bytes)
	{
		List<int> levels = [];
		foreach (byte b in bytes)
		{
			levels.AddRange(Enumerable.Repeat(0, ClocksPerBit));
			for (int i = 0; i < 8; i++)
			{
				levels.AddRange(Enumerable.Repeat((b >> i) & 1, ClocksPerBit));
			}
			levels.AddRange(Enumerable.Repeat(1, ClocksPerBit));
		}
		return levels;
	}
}
