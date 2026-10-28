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
	public void StreamNumbersGoUpTo64()
	{
		AxisD1110Select.MaxStreams.Should().Be(64);
	}

	[Theory]
	[InlineData(1, true)]
	[InlineData(16, true)]
	[InlineData(17, true)]
	[InlineData(41, true)]
	[InlineData(64, true)]
	[InlineData(0, false)]
	[InlineData(-1, false)]
	[InlineData(65, false)]
	[InlineData(65535, false)]
	public void IsStreamNumber_Is1To64(int number, bool expected)
	{
		AxisD1110Select.IsStreamNumber(number).Should().Be(expected);
	}

	[Theory]
	[InlineData("1", 1)]
	[InlineData("33", 33)]
	[InlineData("41", 41)]
	[InlineData("64", 64)]
	public void TryParseStreamKey_AcceptsCameraNumbers(string key, int expected)
	{
		AxisD1110Select.TryParseStreamKey(key, out var number).Should().BeTrue();
		number.Should().Be(expected);
	}

	[Theory]
	[InlineData("0")]
	[InlineData("65")]
	[InlineData("-3")]
	[InlineData("cam1")]
	[InlineData("")]
	[InlineData(null)]
	public void TryParseStreamKey_RejectsAnythingElse(string key)
	{
		AxisD1110Select.TryParseStreamKey(key, out _).Should().BeFalse();
	}

	[Fact]
	public void OnlyConfigured_StreamsAbove16CanBeSelected()
	{
		Func<int, bool> cameras = n => n == 33 || n == 41;

		AxisD1110Select.ShouldSelect(41, true, cameras).Should().BeTrue();
		AxisD1110Select.ShouldSelect(40, true, cameras).Should().BeFalse();
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
