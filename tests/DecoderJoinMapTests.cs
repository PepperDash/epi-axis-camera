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

			foreach (var join in new[] { map.SelectStream, map.LastErrorCode })
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
		map.Clear.JoinNumber.Should().Be(2);
		map.ConfigurationId.JoinNumber.Should().Be(4);
	}
}
