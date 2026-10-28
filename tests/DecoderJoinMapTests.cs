using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderJoinMapTests
{
	[Fact]
	public void Contiguous_IsUnchanged()
	{
		var map = new AxisD1110BridgeJoinMap(1);

		map.IsOnline.JoinNumber.Should().Be(1);
		map.Clear.JoinNumber.Should().Be(2);
		map.Refresh.JoinNumber.Should().Be(3);
		map.SelectStream.JoinNumber.Should().Be(1);
		map.LastErrorCode.JoinNumber.Should().Be(2);
		map.DeviceName.JoinNumber.Should().Be(1);
		map.StreamNames.JoinNumber.Should().Be(11);
		map.StreamUrls.JoinNumber.Should().Be(31);

		new AxisD1110BridgeJoinMap(101).SelectStream.JoinNumber.Should().Be(101);
	}

	[Fact]
	public void Contiguous_SecondStreamIsAnalogThree()
	{
		new AxisD1110BridgeJoinMap(1).SelectStream2.JoinNumber.Should().Be(3);
		new AxisD1110BridgeJoinMap(101).SelectStream2.JoinNumber.Should().Be(103);
		new AxisD1110BridgeJoinMap(1).SelectStream2.Metadata.JoinCapabilities.Should().Be(PepperDash.Essentials.Core.eJoinCapabilities.ToFromSIMPL);
	}

	[Fact]
	public void Contiguous_StreamNameAndUrlJoinsStaySixteenWideAndDoNotOverlap()
	{
		var map = new AxisD1110BridgeJoinMap(1);

		AxisD1110BridgeJoinMap.MaxBridgeStreams.Should().Be(16);
		map.StreamNames.JoinSpan.Should().Be(16);
		map.StreamUrls.JoinSpan.Should().Be(16);

		// names 11 to 26, URLs 31 to 46, after the single serial joins 1 to 4
		(map.StreamNames.JoinNumber + map.StreamNames.JoinSpan - 1).Should().BeLessThan(map.StreamUrls.JoinNumber);
		map.StreamNames.JoinNumber.Should().BeGreaterThan(map.ConfigurationId.JoinNumber);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	[InlineData(7)]
	[InlineData(8)]
	public void Indexed_PutsEachDecoderInItsOwnJoins(int decoder)
	{
		var map = new AxisD1110BridgeJoinMap(1, decoder);
		var d = (uint)decoder;

		map.IsOnline.JoinNumber.Should().Be(d);
		map.Clear.JoinNumber.Should().Be(8 + d);
		map.Refresh.JoinNumber.Should().Be(16 + d);
		map.SelectStream.JoinNumber.Should().Be(d);
		map.LastErrorCode.JoinNumber.Should().Be(8 + d);
		map.SelectStream2.JoinNumber.Should().Be(16 + d);
		map.DeviceName.JoinNumber.Should().Be(d);
		map.SelectedStreamName.JoinNumber.Should().Be(8 + d);
		map.LastError.JoinNumber.Should().Be(16 + d);
		map.ConfigurationId.JoinNumber.Should().Be(24 + d);
	}

	[Fact]
	public void Indexed_EightDecodersFitInThirtyTwoJoinsOfEachTypeWithoutOverlap()
	{
		var digital = new HashSet<uint>();
		var analog = new HashSet<uint>();
		var serial = new HashSet<uint>();

		for (var decoder = 1; decoder <= 8; decoder++)
		{
			var map = new AxisD1110BridgeJoinMap(1, decoder);

			foreach (var join in new[] { map.IsOnline, map.Clear, map.Refresh })
			{
				join.JoinNumber.Should().BeInRange(1u, 32u);
				digital.Add(join.JoinNumber).Should().BeTrue("decoder {0} digital join {1} must not repeat", decoder, join.JoinNumber);
			}

			foreach (var join in new[] { map.SelectStream, map.LastErrorCode, map.SelectStream2 })
			{
				join.JoinNumber.Should().BeInRange(1u, 32u);
				analog.Add(join.JoinNumber).Should().BeTrue("decoder {0} analog join {1} must not repeat", decoder, join.JoinNumber);
			}

			foreach (var join in new[] { map.DeviceName, map.SelectedStreamName, map.LastError, map.ConfigurationId })
			{
				join.JoinNumber.Should().BeInRange(1u, 32u);
				serial.Add(join.JoinNumber).Should().BeTrue("decoder {0} serial join {1} must not repeat", decoder, join.JoinNumber);
			}
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(9)]
	[InlineData(40)]
	public void IndexOutOfRange_UsesTheContiguousLayout(int index)
	{
		var map = new AxisD1110BridgeJoinMap(1, index);

		map.SelectStream.JoinNumber.Should().Be(1);
		map.SelectStream2.JoinNumber.Should().Be(3);
		map.Clear.JoinNumber.Should().Be(2);
		map.ConfigurationId.JoinNumber.Should().Be(4);
	}

	[Fact]
	public void Indexed_DecoderEightSecondStreamIsAnalog24()
	{
		new AxisD1110BridgeJoinMap(1, 8).SelectStream2.JoinNumber.Should().Be(24);
		new AxisD1110BridgeJoinMap(1, 1).SelectStream2.JoinNumber.Should().Be(17);
	}
}
