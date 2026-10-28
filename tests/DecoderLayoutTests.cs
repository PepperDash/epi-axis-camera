using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderLayoutTests
{
	private static readonly AxisD1110PaneBuilder BuildsEverything = (int number, out AxisD1110StreamConfig stream, out string error) =>
	{
		stream = new AxisD1110StreamConfig { Url = "rtsp://cam" + number + "/stream" };
		error = null;
		return true;
	};

	private static readonly AxisD1110PaneBuilder BuildsNothing = (int number, out AxisD1110StreamConfig stream, out string error) =>
	{
		stream = null;
		error = "no url is configured";
		return false;
	};

	private static readonly AxisD1110PaneBuilder MustNotBeCalled = (int number, out AxisD1110StreamConfig stream, out string error) =>
		throw new InvalidOperationException("the stream must not be built");

	[Fact]
	public void Resolve_ZeroIsAnEmptyPane()
	{
		var pane = AxisD1110Layout.Resolve(0, false, MustNotBeCalled);

		pane.Status.Should().Be(AxisD1110PaneStatus.Empty);
		pane.Stream.Should().BeNull();
	}

	[Fact]
	public void Resolve_AStreamHiddenByPrivacyIsLeftOutWithoutBuildingIt()
	{
		var pane = AxisD1110Layout.Resolve(33, true, MustNotBeCalled);

		pane.Number.Should().Be(33);
		pane.Status.Should().Be(AxisD1110PaneStatus.HiddenByPrivacy);
		pane.Stream.Should().BeNull();
	}

	[Fact]
	public void Resolve_AStreamThatCannotBeBuiltIsUnavailableWithTheReason()
	{
		var pane = AxisD1110Layout.Resolve(41, false, BuildsNothing);

		pane.Status.Should().Be(AxisD1110PaneStatus.Unavailable);
		pane.Error.Should().Be("no url is configured");
		pane.Stream.Should().BeNull();
	}

	[Fact]
	public void Resolve_ABuiltStreamIsShown()
	{
		var pane = AxisD1110Layout.Resolve(41, false, BuildsEverything);

		pane.Status.Should().Be(AxisD1110PaneStatus.Shown);
		pane.Stream.Url.Should().Be("rtsp://cam41/stream");
	}

	[Fact]
	public void Visible_KeepsPaneOrder()
	{
		var panes = new[] { AxisD1110Layout.Resolve(5, false, BuildsEverything), AxisD1110Layout.Resolve(2, false, BuildsEverything) };

		AxisD1110Layout.Visible(panes).Select(s => s.Url).Should().Equal("rtsp://cam5/stream", "rtsp://cam2/stream");
	}

	[Theory]
	// 0 visible: blank
	[InlineData(0, false, true, 0, false, true, 0)]
	[InlineData(3, true, true, 0, false, true, 0)]
	[InlineData(3, false, false, 4, true, true, 0)]
	// 1 visible: the other pane is 0, hidden or cannot be built
	[InlineData(3, false, true, 0, false, true, 1)]
	[InlineData(0, false, true, 4, false, true, 1)]
	[InlineData(3, true, true, 4, false, true, 1)]
	[InlineData(3, false, false, 4, false, true, 1)]
	[InlineData(3, false, true, 4, false, false, 1)]
	// 2 visible
	[InlineData(3, false, true, 4, false, true, 2)]
	public void Visible_LeavesOutEmptyHiddenAndUnavailablePanes(
		int first, bool firstHidden, bool firstBuilds, int second, bool secondHidden, bool secondBuilds, int expected)
	{
		var panes = new[]
		{
			AxisD1110Layout.Resolve(first, firstHidden, firstBuilds ? BuildsEverything : BuildsNothing),
			AxisD1110Layout.Resolve(second, secondHidden, secondBuilds ? BuildsEverything : BuildsNothing)
		};

		AxisD1110Layout.Visible(panes).Should().HaveCount(expected);
	}

	[Fact]
	public void OnlyTheSecondPaneLeft_IsShownFullScreen()
	{
		// pane 1 cannot be built: the second stream is still shown, full screen
		var panes = new[] { AxisD1110Layout.Resolve(7, false, BuildsNothing), AxisD1110Layout.Resolve(41, false, BuildsEverything) };

		var json = AxisD1110DecoderClient.BuildViewConfiguration(AxisD1110Layout.Visible(panes));

		var pane = (JObject)json["panes"]!.Single();
		pane.Value<double>("left").Should().Be(0.0);
		pane.Value<double>("top").Should().Be(0.0);
		pane.Value<double>("right").Should().Be(1.0);
		pane.Value<double>("bottom").Should().Be(1.0);
		((JObject)json["streams"]!.Single()).Value<string>("url").Should().Be("rtsp://cam41/stream");
	}

	[Fact]
	public void BothPanesHiddenByPrivacy_BlanksTheOutput()
	{
		var panes = new[] { AxisD1110Layout.Resolve(33, true, MustNotBeCalled), AxisD1110Layout.Resolve(34, true, MustNotBeCalled) };

		var json = AxisD1110DecoderClient.BuildViewConfiguration(AxisD1110Layout.Visible(panes));

		json["panes"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
		json["views"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
	}

	[Fact]
	public void TwoPanes_IsTheDecoderPaneCount()
	{
		AxisD1110Layout.PaneCount.Should().Be(2);
	}
}
