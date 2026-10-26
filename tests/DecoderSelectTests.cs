using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderSelectTests
{
	private static readonly Func<int, bool> StreamsOneToThree = n => n >= 1 && n <= 3;

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(3)]
	[InlineData(9)]
	[InlineData(51)]
	public void Default_ActsOnEveryValue(int value)
	{
		AxisD1110Select.ShouldSelect(value, false, StreamsOneToThree).Should().BeTrue("0 blanks and unknown numbers report an error, as before");
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void OnlyConfigured_ActsOnConfiguredStreams(int value)
	{
		AxisD1110Select.ShouldSelect(value, true, StreamsOneToThree).Should().BeTrue();
	}

	[Theory]
	[InlineData(0)]
	[InlineData(4)]
	[InlineData(5)]
	[InlineData(9)]
	[InlineData(50)]
	[InlineData(51)]
	[InlineData(65535)]
	public void OnlyConfigured_IgnoresEverythingElse(int value)
	{
		// a display's selected source is 4 to 9 for Aux, codec and multiviewer, and 50 or 51 for nothing selected
		AxisD1110Select.ShouldSelect(value, true, StreamsOneToThree).Should().BeFalse();
	}

	[Fact]
	public void OnlyConfigured_StreamsThatSkipANumberAreRespected()
	{
		// Micro Surgical has cameras on sources 1 and 3 only
		Func<int, bool> oneAndThree = n => n == 1 || n == 3;

		AxisD1110Select.ShouldSelect(1, true, oneAndThree).Should().BeTrue();
		AxisD1110Select.ShouldSelect(2, true, oneAndThree).Should().BeFalse();
		AxisD1110Select.ShouldSelect(3, true, oneAndThree).Should().BeTrue();
	}
}
