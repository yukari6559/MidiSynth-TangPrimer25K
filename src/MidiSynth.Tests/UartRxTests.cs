using SharpHdl.Core.Model;
using SharpHdl.Sim;

namespace MidiSynth.Tests;

public class UartRxTests
{
	private const uint ClocksPerBit = 4;

	[Fact]
	public void PortsAreClockResetRxDataValid()
	{
		List<Signal> ports = [.. new UartRx(ClocksPerBit).GetPorts()];

		Assert.Equal(["clk", "rst", "rx", "data", "valid"], ports.Select(p => p.Name));
		Assert.Equal([1u, 1u, 1u, 8u, 1u], ports.Select(p => p.Width));
	}

	[Fact]
	public void ZeroClocksPerBitIsRejected()
	{
		ArgumentOutOfRangeException ex = Assert.Throws<ArgumentOutOfRangeException>(() => new UartRx(0));
		Assert.Equal("clocksPerBit", ex.ParamName);
	}

	[Theory]
	[InlineData(1u, 1u)]
	[InlineData(3u, 2u)]
	[InlineData(4u, 3u)]
	[InlineData(8u, 4u)]
	public void PhaseWidthFitsClocksPerBit(uint clocksPerBit, uint width)
	{
		Assert.Equal(width, new UartRx(clocksPerBit).Phase.Width);
	}

	[Fact]
	public void ResetClearsState()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);

		Assert.Equal(0u, s.Get(s.Module.Busy));
		Assert.Equal(0u, s.Get(s.Module.Phase));
		Assert.Equal(0u, s.Get(s.Module.BitIndex));
		Assert.Equal(0u, s.Get(s.Module.Data));
		Assert.Equal(0u, s.Get(s.Module.Valid));
	}

	[Fact]
	public void IdleLineReceivesNothing()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);

		Assert.Empty(Feed(s, Enumerable.Repeat(1, 100)));
		Assert.Equal(0u, s.Get(s.Module.Busy));
	}

	[Fact]
	public void StartBitCountsClocksOfOneBit()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);

		(uint Busy, uint Phase, uint BitIndex)[] expected =
		[
			(1, 1, 0),
			(1, 2, 0),
			(1, 3, 0),
			(1, 0, 1),
		];
		foreach ((uint busy, uint phase, uint bitIndex) in expected)
		{
			s.Set(s.Module.Rx, 0);
			s.Advance(s.Module.Clk);
			Assert.Equal(busy, s.Get(s.Module.Busy));
			Assert.Equal(phase, s.Get(s.Module.Phase));
			Assert.Equal(bitIndex, s.Get(s.Module.BitIndex));
		}
	}

	[Theory]
	[InlineData(1u)]
	[InlineData(3u)]
	[InlineData(4u)]
	[InlineData(16u)]
	public void NoteOnBytesArriveInOrder(uint clocksPerBit)
	{
		SimSession<UartRx> s = Start(clocksPerBit);
		byte[] noteOn = [0x90, 0x3C, 0x64];

		List<byte> received = Feed(s, Line(clocksPerBit, noteOn));

		Assert.Equal(noteOn, received);
	}

	[Fact]
	public void BackToBackFramesWithoutIdleArriveInOrder()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);
		byte[] bytes = [0x00, 0xFF, 0xA5, 0x5A];

		List<int> levels = [.. Enumerable.Repeat(1, 3)];
		foreach (byte b in bytes)
		{
			levels.AddRange(Frame(ClocksPerBit, b));
		}
		levels.AddRange(Enumerable.Repeat(1, 3));

		Assert.Equal(bytes, Feed(s, levels));
	}

	[Fact]
	public void ValidIsHighForOneClockAndDataIsHeld()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);

		List<(uint Data, uint Valid)> trace = [];
		foreach (int level in Line(ClocksPerBit, [0x3C]))
		{
			s.Set(s.Module.Rx, (ulong)level);
			s.Advance(s.Module.Clk);
			trace.Add(((uint)s.Get(s.Module.Data), (uint)s.Get(s.Module.Valid)));
		}

		int pulse = trace.FindIndex(t => t.Valid == 1);
		Assert.Equal(1, trace.Count(t => t.Valid == 1));
		Assert.All(trace[..pulse], t => Assert.Equal(0u, t.Data));
		Assert.All(trace[pulse..], t => Assert.Equal(0x3Cu, t.Data));
	}

	[Fact]
	public void GlitchShorterThanHalfBitIsNotAStart()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);

		List<int> levels = [0, .. Enumerable.Repeat(1, 10)];

		Assert.Empty(Feed(s, levels));
		Assert.Equal(0u, s.Get(s.Module.Busy));
		Assert.Equal(0u, s.Get(s.Module.BitIndex));
	}

	[Fact]
	public void LowStopBitDropsByteAndKeepsData()
	{
		SimSession<UartRx> s = Start(ClocksPerBit);
		Assert.Equal([0x11], Feed(s, Line(ClocksPerBit, [0x11])));

		List<int> broken = Frame(ClocksPerBit, 0x22);
		broken.RemoveRange(broken.Count - (int)ClocksPerBit, (int)ClocksPerBit);
		broken.AddRange(Enumerable.Repeat(0, (int)ClocksPerBit));
		broken.AddRange(Enumerable.Repeat(1, 3 * (int)ClocksPerBit));

		Assert.Empty(Feed(s, broken));
		Assert.Equal(0x11u, s.Get(s.Module.Data));
		Assert.Equal(0u, s.Get(s.Module.Busy));
	}

	private static SimSession<UartRx> Start(uint clocksPerBit)
	{
		SimSession<UartRx> s = new UartRx(clocksPerBit).Run();
		s.Set(s.Module.Rx, 1);
		s.Set(s.Module.Rst, 1);
		s.Advance(s.Module.Clk);
		s.Set(s.Module.Rst, 0);
		return s;
	}

	private static List<byte> Feed(SimSession<UartRx> s, IEnumerable<int> levels)
	{
		List<byte> received = [];
		foreach (int level in levels)
		{
			s.Set(s.Module.Rx, (ulong)level);
			s.Advance(s.Module.Clk);
			if (s.Get(s.Module.Valid) == 1)
			{
				received.Add((byte)s.Get(s.Module.Data));
			}
		}
		return received;
	}

	private static List<int> Line(uint clocksPerBit, IEnumerable<byte> bytes)
	{
		List<int> levels = [.. Enumerable.Repeat(1, 2 * (int)clocksPerBit)];
		foreach (byte b in bytes)
		{
			levels.AddRange(Frame(clocksPerBit, b));
			levels.AddRange(Enumerable.Repeat(1, (int)clocksPerBit));
		}
		return levels;
	}

	private static List<int> Frame(uint clocksPerBit, byte value)
	{
		List<int> bits = [0];
		for (int i = 0; i < 8; i++)
		{
			bits.Add((value >> i) & 1);
		}
		bits.Add(1);
		return [.. bits.SelectMany(bit => Enumerable.Repeat(bit, (int)clocksPerBit))];
	}
}
