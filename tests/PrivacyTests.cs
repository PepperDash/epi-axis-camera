using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class PrivacyTests
{
	[Fact]
	public void PreviewUrl_IsTheUrlWhenPrivacyIsOff()
	{
		AxisPrivacy.PreviewUrl("192.168.10.241", "/axis-media/media.amp", "800x450", false)
			.Should().Be("rtsp://192.168.10.241/axis-media/media.amp?resolution=800x450");
	}

	[Theory]
	[InlineData("192.168.10.241")]
	[InlineData("")]
	public void PreviewUrl_IsEmptyWhilePrivacyHidesTheVideo(string host)
	{
		AxisPrivacy.PreviewUrl(host, "/axis-media/media.amp", "800x450", true).Should().BeEmpty();
	}

	[Fact]
	public void PreviewUrl_ComesBackWhenPrivacyEnds()
	{
		var hidden = AxisPrivacy.PreviewUrl("192.168.10.241", null, "800x450", true);
		var shown = AxisPrivacy.PreviewUrl("192.168.10.241", null, "800x450", false);

		hidden.Should().BeEmpty();
		shown.Should().Be("rtsp://192.168.10.241/axis-media/media.amp?resolution=800x450");
	}

	[Theory]
	[InlineData(true, false, AxisPrivacyAction.Blank)]
	[InlineData(false, true, AxisPrivacyAction.Restore)]
	[InlineData(true, true, AxisPrivacyAction.None)]
	[InlineData(false, false, AxisPrivacyAction.None)]
	public void Plan_DecidesWhatADecoderShowingTheCameraDoes(bool cameraHidesVideo, bool blankedByPrivacy, AxisPrivacyAction expected)
	{
		AxisPrivacy.Plan(cameraHidesVideo, blankedByPrivacy).Should().Be(expected);
	}

	[Fact]
	public void Plan_FollowsAFullPrivacyCycle()
	{
		var blanked = false;

		// privacy on: blank
		var action = AxisPrivacy.Plan(true, blanked);
		action.Should().Be(AxisPrivacyAction.Blank);
		blanked = true;

		// privacy signal repeats: nothing to do
		AxisPrivacy.Plan(true, blanked).Should().Be(AxisPrivacyAction.None);

		// privacy off: show the stream again
		action = AxisPrivacy.Plan(false, blanked);
		action.Should().Be(AxisPrivacyAction.Restore);
		blanked = false;

		AxisPrivacy.Plan(false, blanked).Should().Be(AxisPrivacyAction.None);
	}
}
