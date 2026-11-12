using SharpHdl.Core.Model;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class SquareWaveTests
{
	[Fact]
	public void TickerPortsAreClockResetTick()
	{
		List<Signal> ports = [.. new SampleTicker(4).GetPorts()];

		Assert.Equal(["clk", "rst", "tick"], ports.Select(p => p.Name));
		Assert.Equal([1u, 1u, 1u], ports.Select(p => p.Width));
	}

	[Theory]
	[InlineData(1u, new[] { 1, 1, 1, 1, 1, 1, 1, 1 })]
	[InlineData(2u, new[] { 1, 0, 1, 0, 1, 0, 1, 0 })]
	[InlineData(4u, new[] { 0, 0, 1, 0, 0, 0, 1, 0 })]
	[InlineData(5u, new[] { 0, 0, 0, 1, 0, 0, 0, 0 })]
	public void TickerTicksOnceEveryClocksPerSample(uint clocksPerSample, int[] ticks)
	{
		SimSession<SampleTicker> s = new SampleTicker(clocksPerSample).Run();
		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);

		List<int> seen = [];
		for (int i = 0; i < ticks.Length; i++)
		{
			s.Advance(s.Module.Clk);
			seen.Add((int)s.Get(s.Module.Tick));
		}

		Assert.Equal(ticks, seen);
	}

	[Fact]
	public void TickerRejectsZero()
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SampleTicker(0));
		Assert.Equal("clocksPerSample", ex.ParamName);
	}

	[Fact]
	public void OscillatorPortsAreClockResetTickIncrementSquare()
	{
		List<Signal> ports = [.. new SquareOscillator(16).GetPorts()];

		Assert.Equal(["clk", "rst", "tick", "increment", "square"], ports.Select(p => p.Name));
		Assert.Equal([1u, 1u, 1u, 16u, 1u], ports.Select(p => p.Width));
	}

	[Fact]
	public void SquareIsLowForHalfATurnThenHigh()
	{
		SimSession<SquareOscillator> s = StartOscillator(8, increment: 16);

		List<int> square = Run(s, 48, tick: 1);

		int[] oneTurn = [.. Enumerable.Repeat(0, 7), .. Enumerable.Repeat(1, 8), 0];
		Assert.Equal([.. oneTurn, .. oneTurn, .. oneTurn], square);
	}

	[Theory]
	[InlineData(1ul)]
	[InlineData(3ul)]
	[InlineData(77ul)]
	[InlineData(255ul)]
	public void SquareIsHighForHalfOfEveryFullCycle(ulong increment)
	{
		SimSession<SquareOscillator> s = StartOscillator(8, increment);

		List<int> square = Run(s, 256, tick: 1);

		Assert.Equal(128, square.Count(v => v == 1));
	}

	[Fact]
	public void PhaseDoesNotMoveWithoutTick()
	{
		SimSession<SquareOscillator> s = StartOscillator(8, increment: 16);
		Run(s, 5, tick: 1);
		ulong phase = s.Get(s.Module.Phase);

		Run(s, 20, tick: 0);

		Assert.Equal(phase, s.Get(s.Module.Phase));
	}

	[Fact]
	public void TickerDrivesOscillatorAtSampleRate()
	{
		SimSession<SampleTicker> ticker = new SampleTicker(4).Run();
		SimSession<SquareOscillator> osc = StartOscillator(8, increment: 32);
		ticker.Set(ticker.Module.Rst, 1);
		ticker.Advance(ticker.Module.Clk);
		ticker.Set(ticker.Module.Rst, 0);

		List<int> square = [];
		for (int i = 0; i < 64; i++)
		{
			osc.Set(osc.Module.Tick, ticker.Get(ticker.Module.Tick));
			osc.Advance(osc.Module.Clk);
			ticker.Advance(ticker.Module.Clk);
			square.Add((int)osc.Get(osc.Module.Square));
		}

		Assert.Equal(32, square.Count(v => v == 1));
		Assert.Equal(2, square.Zip(square.Skip(1)).Count(p => p.First == 0 && p.Second == 1));
	}

	[Theory]
	[InlineData(0u)]
	[InlineData(65u)]
	public void OscillatorRejectsBadPhaseWidth(uint phaseWidth)
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new SquareOscillator(phaseWidth));
		Assert.Equal("phaseWidth", ex.ParamName);
	}

	private static SimSession<SquareOscillator> StartOscillator(uint phaseWidth, ulong increment)
	{
		SimSession<SquareOscillator> s = new SquareOscillator(phaseWidth).Run();
		s.Set(s.Module.Increment, increment);
		s.Set(s.Module.Tick, 0);
		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);
		return s;
	}

	private static List<int> Run(SimSession<SquareOscillator> s, int clocks, int tick)
	{
		List<int> square = [];
		s.Set(s.Module.Tick, (ulong)tick);
		for (int i = 0; i < clocks; i++)
		{
			s.Advance(s.Module.Clk);
			square.Add((int)s.Get(s.Module.Square));
		}
		return square;
	}
}
