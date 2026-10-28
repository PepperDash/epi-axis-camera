using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderRequestTests
{
	[Fact]
	public void BuildViewConfiguration_ShowsOneStreamFullScreen()
	{
		var stream = new AxisD1110StreamConfig
		{
			Name = "Left PTZ",
			Url = "rtsp://192.168.10.211/axis-media/media.amp",
			VideoCodec = "MJPEG",
			AudioCodec = "AAC",
			Username = "viewer",
			Password = "pass"
		};

		var json = AxisD1110DecoderClient.BuildViewConfiguration(stream);

		var pane = (JObject)json["panes"]!.Single();
		pane.Value<int>("paneId").Should().Be(0);
		pane.Value<double>("left").Should().Be(0.0);
		pane.Value<double>("top").Should().Be(0.0);
		pane.Value<double>("right").Should().Be(1.0);
		pane.Value<double>("bottom").Should().Be(1.0);

		var s = (JObject)json["streams"]!.Single();
		s.Value<int>("streamId").Should().Be(0);
		s.Value<string>("url").Should().Be("rtsp://192.168.10.211/axis-media/media.amp");
		s.Value<string>("videoCodec").Should().Be("MJPEG");
		s.Value<string>("audioCodec").Should().Be("AAC");
		s.Value<string>("username").Should().Be("viewer");
		s.Value<string>("password").Should().Be("pass");

		var view = (JObject)json["views"]!.Single();
		view.Value<int>("viewId").Should().Be(0);
		view.Value<int>("duration").Should().Be(0, "0 shows the view continuously");
		var segment = (JObject)view["segments"]!.Single();
		segment.Value<int>("stream").Should().Be(0);
		segment.Value<int>("pane").Should().Be(0);
	}

	[Fact]
	public void BuildViewConfiguration_DefaultsToH264WithNoAudioOrCredentials()
	{
		var stream = new AxisD1110StreamConfig { Url = "rtsp://cam/stream" };

		var s = (JObject)AxisD1110DecoderClient.BuildViewConfiguration(stream)["streams"]!.Single();

		s.Value<string>("videoCodec").Should().Be("H264");
		s.Value<string>("audioCodec").Should().BeEmpty();
		s.Value<string>("username").Should().BeEmpty();
		s.Value<string>("password").Should().BeEmpty();
	}

	[Fact]
	public void BuildViewConfiguration_NullStreamSendsAnEmptyConfiguration()
	{
		var json = AxisD1110DecoderClient.BuildViewConfiguration((AxisD1110StreamConfig)null);

		json["panes"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
		json["streams"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
		json["views"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
	}

	[Fact]
	public void BuildViewConfiguration_ListOfOneIsTheSameAsOneStream()
	{
		var stream = new AxisD1110StreamConfig { Url = "rtsp://cam/stream", Username = "viewer", Password = "pass" };

		var fromList = AxisD1110DecoderClient.BuildViewConfiguration(new List<AxisD1110StreamConfig> { stream });
		var fromOne = AxisD1110DecoderClient.BuildViewConfiguration(stream);

		JToken.DeepEquals(fromList, fromOne).Should().BeTrue("one visible stream is shown full screen, exactly as before");
	}

	[Fact]
	public void BuildViewConfiguration_NullEntriesAreLeftOut()
	{
		var stream = new AxisD1110StreamConfig { Url = "rtsp://cam2/stream" };

		var json = AxisD1110DecoderClient.BuildViewConfiguration(new List<AxisD1110StreamConfig> { null, stream });

		JToken.DeepEquals(json, AxisD1110DecoderClient.BuildViewConfiguration(stream)).Should().BeTrue();
	}

	[Fact]
	public void BuildViewConfiguration_NoStreamsSendsAnEmptyConfiguration()
	{
		foreach (var streams in new[] { new List<AxisD1110StreamConfig>(), new List<AxisD1110StreamConfig> { null, null }, null })
		{
			var json = AxisD1110DecoderClient.BuildViewConfiguration(streams);

			json["panes"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
			json["streams"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
			json["views"].Should().BeOfType<JArray>().Which.Should().BeEmpty();
		}
	}

	[Fact]
	public void BuildViewConfiguration_TwoStreamsAreSideBySideIn16By9Panes()
	{
		var first = new AxisD1110StreamConfig { Url = "rtsp://192.168.10.211/axis-media/media.amp", Username = "viewer", Password = "one" };
		var second = new AxisD1110StreamConfig { Url = "rtsp://192.168.10.212/axis-media/media.amp", VideoCodec = "MJPEG" };

		var json = AxisD1110DecoderClient.BuildViewConfiguration(new List<AxisD1110StreamConfig> { first, second });

		var panes = json["panes"]!.Cast<JObject>().ToList();
		panes.Should().HaveCount(2);

		panes[0].Value<int>("paneId").Should().Be(0);
		panes[0].Value<double>("left").Should().Be(0.0);
		panes[0].Value<double>("top").Should().Be(0.25);
		panes[0].Value<double>("right").Should().Be(0.5);
		panes[0].Value<double>("bottom").Should().Be(0.75);

		panes[1].Value<int>("paneId").Should().Be(1);
		panes[1].Value<double>("left").Should().Be(0.5);
		panes[1].Value<double>("top").Should().Be(0.25);
		panes[1].Value<double>("right").Should().Be(1.0);
		panes[1].Value<double>("bottom").Should().Be(0.75);

		var streams = json["streams"]!.Cast<JObject>().ToList();
		streams.Should().HaveCount(2);
		streams[0].Value<int>("streamId").Should().Be(0);
		streams[0].Value<string>("url").Should().Be("rtsp://192.168.10.211/axis-media/media.amp");
		streams[0].Value<string>("videoCodec").Should().Be("H264");
		streams[0].Value<string>("username").Should().Be("viewer");
		streams[0].Value<string>("password").Should().Be("one");
		streams[1].Value<int>("streamId").Should().Be(1);
		streams[1].Value<string>("url").Should().Be("rtsp://192.168.10.212/axis-media/media.amp");
		streams[1].Value<string>("videoCodec").Should().Be("MJPEG");
		streams[1].Value<string>("username").Should().BeEmpty();

		var view = (JObject)json["views"]!.Single();
		view.Value<int>("viewId").Should().Be(0);
		view.Value<int>("duration").Should().Be(0);
		var segments = view["segments"]!.Cast<JObject>().ToList();
		segments.Should().HaveCount(2);
		segments[0].Value<int>("stream").Should().Be(0);
		segments[0].Value<int>("pane").Should().Be(0);
		segments[1].Value<int>("stream").Should().Be(1);
		segments[1].Value<int>("pane").Should().Be(1);
	}

	[Fact]
	public void BuildViewConfiguration_MoreThanTwoStreamsIsRejected()
	{
		var stream = new AxisD1110StreamConfig { Url = "rtsp://cam/stream" };

		var act = () => AxisD1110DecoderClient.BuildViewConfiguration(new List<AxisD1110StreamConfig> { stream, stream, stream });

		act.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void MaskSecrets_HidesEveryPassword()
	{
		var body = "{\"params\":{\"streams\":[{\"username\":\"viewer\",\"password\":\"p@ss\\\"word\"},{\"password\":\"second\"}]}}";

		var masked = AxisD1110DecoderClient.MaskSecrets(body);

		masked.Should().NotContain("p@ss").And.NotContain("second");
		masked.Should().Contain("\"username\":\"viewer\"");
		masked.Should().Contain("\"password\":\"***\"");
		JObject.Parse(masked).Should().NotBeNull("masking must leave valid JSON");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("{\"method\":\"getViewConfiguration\"}")]
	public void MaskSecrets_LeavesTextWithoutPasswordsAlone(string body)
	{
		AxisD1110DecoderClient.MaskSecrets(body).Should().Be(body);
	}

	[Theory]
	[InlineData("rtsp://root:secret@192.168.10.211/axis-media/media.amp", "rtsp://root:***@192.168.10.211/axis-media/media.amp")]
	[InlineData("rtsp://192.168.10.211/axis-media/media.amp", "rtsp://192.168.10.211/axis-media/media.amp")]
	[InlineData("rtsp://root@192.168.10.211/x", "rtsp://root@192.168.10.211/x")]
	public void MaskUrl_HidesThePasswordInTheUrl(string url, string expected)
	{
		AxisD1110DecoderClient.MaskUrl(url).Should().Be(expected);
	}
}
