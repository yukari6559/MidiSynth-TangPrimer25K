using SharpHdl.Core.Model;
using SharpHdl.Emit;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class TangPrimer25KTopTests
{
	[Fact]
	public void TopPortsAreClockMidiRxAudioLed()
	{
		List<Signal> ports = [.. new TangPrimer25KTop().GetPorts()];

		Assert.Equal(["clk", "midi_rx", "audio", "led"], ports.Select(p => p.Name));
		Assert.All(ports, p => Assert.Equal(1u, p.Width));
	}

	[Fact]
	public void TopVerilogHasEveryModuleAndBoardConstants()
	{
		TangPrimer25KTop top = new();
		top.Describe();

		string verilog = VerilogEmitter.Emitter(top, nameof(TangPrimer25KTop));

		foreach (string module in new[] { "TangPrimer25KTop", "PowerOnReset", "MonoSynth", "UartRx", "NoteParser", "PhaseIncrementTable", "SampleTicker", "SquareOscillator" })
		{
			Assert.Contains($"module {module}(", verilog, StringComparison.Ordinal);
		}
		Assert.Contains("11'd1600", verilog, StringComparison.Ordinal);
		Assert.Contains("10'd1023", verilog, StringComparison.Ordinal);
		Assert.Contains("(note == 7'd69) ? (24'd151183)", verilog, StringComparison.Ordinal);
		Assert.Contains("assign zero = 1'd0;", verilog, StringComparison.Ordinal);
	}

	[Fact]
	public void PowerOnResetHoldsThenReleases()
	{
		SimSession<PowerOnReset> s = new PowerOnReset(4).Run();

		List<int> reset = [(int)s.Get(s.Module.Reset)];
		for (int i = 0; i < 8; i++)
		{
			s.Advance(s.Module.Clk);
			reset.Add((int)s.Get(s.Module.Reset));
		}

		Assert.Equal([1, 1, 1, 1, 0, 0, 0, 0, 0], reset);
	}

	[Fact]
	public void PowerOnResetStartsAgainOnRst()
	{
		SimSession<PowerOnReset> s = new PowerOnReset(4).Run();
		s.Advance(s.Module.Clk, 10);

		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);

		Assert.Equal(1u, s.Get(s.Module.Reset));
		s.Advance(s.Module.Clk, 4);
		Assert.Equal(0u, s.Get(s.Module.Reset));
	}

	[Fact]
	public void PowerOnResetRejectsZero()
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new PowerOnReset(0));
		Assert.Equal("holdClocks", ex.ParamName);
	}
}
