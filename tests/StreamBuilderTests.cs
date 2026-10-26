using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class StreamBuilderTests
{
	private const string PreviewUrl = "rtsp://admin:axis1234@192.168.10.240/axis-media/media.amp?resolution=800x450";

	private static AxisD1110StreamConfig Build(AxisD1110Config config, AxisD1110StreamConfig stream, string bridgeUrl = null)
	{
		AxisD1110StreamBuilder.TryBuild(config, stream, bridgeUrl, out var result, out var error)
			.Should().BeTrue("building should work, error was: {0}", error);
		return result;
	}

	private static string Fail(AxisD1110Config config, AxisD1110StreamConfig stream, string bridgeUrl = null)
	{
		AxisD1110StreamBuilder.TryBuild(config, stream, bridgeUrl, out var result, out var error).Should().BeFalse();
		result.Should().BeNull();
		return error;
	}

	[Fact]
	public void ConfigMode_UsesTheConfiguredUrlAndMovesTheLoginOutOfIt()
	{
		var result = Build(new AxisD1110Config(), new AxisD1110StreamConfig { Name = "Monopod", Url = PreviewUrl });

		result.Url.Should().Be("rtsp://192.168.10.240/axis-media/media.amp?resolution=800x450");
		result.Username.Should().Be("admin");
		result.Password.Should().Be("axis1234");
		result.Name.Should().Be("Monopod");
	}

	[Fact]
	public void ConfigMode_IgnoresAUrlFromTheBridge()
	{
		var result = Build(new AxisD1110Config(), new AxisD1110StreamConfig { Url = "rtsp://10.0.0.1/a" }, "rtsp://10.0.0.2/b");

		result.Url.Should().Be("rtsp://10.0.0.1/a");
	}

	[Fact]
	public void BridgeMode_UsesTheBridgeUrlEvenWithoutAStreamEntry()
	{
		var config = new AxisD1110Config { StreamSource = "bridge" };

		var result = Build(config, null, PreviewUrl);

		result.Url.Should().Be("rtsp://192.168.10.240/axis-media/media.amp?resolution=800x450");
		result.Username.Should().Be("admin");
	}

	[Fact]
	public void BridgeMode_IgnoresTheConfiguredUrl()
	{
		var config = new AxisD1110Config { StreamSource = "Bridge" };

		var result = Build(config, new AxisD1110StreamConfig { Url = "rtsp://10.0.0.1/a" }, "rtsp://10.0.0.2/b");

		result.Url.Should().Be("rtsp://10.0.0.2/b");
	}

	[Fact]
	public void BridgeMode_StillTakesNameAndCodecFromTheStreamEntry()
	{
		var config = new AxisD1110Config { StreamSource = "bridge" };
		var stream = new AxisD1110StreamConfig { Name = "Left PTZ", VideoCodec = "MJPEG", AudioCodec = "AAC" };

		var result = Build(config, stream, PreviewUrl);

		result.Name.Should().Be("Left PTZ");
		result.VideoCodec.Should().Be("MJPEG");
		result.AudioCodec.Should().Be("AAC");
	}

	[Fact]
	public void BridgeMode_NoUrlYetIsAnError()
	{
		var config = new AxisD1110Config { StreamSource = "bridge" };

		Fail(config, null, null).Should().Contain("bridge");
		Fail(config, null, "").Should().Contain("bridge");
	}

	[Fact]
	public void ConfigMode_NoUrlIsAnError()
	{
		Fail(new AxisD1110Config(), new AxisD1110StreamConfig { Name = "x" }).Should().Contain("configured");
		Fail(new AxisD1110Config(), null).Should().Contain("configured");
	}

	[Fact]
	public void NotAUrl_IsAnErrorThatNamesTheText()
	{
		Fail(new AxisD1110Config(), new AxisD1110StreamConfig { Url = "192.168.10.240" }).Should().Contain("192.168.10.240");
	}

	[Fact]
	public void ResolutionOverride_ReplacesThePreviewResolution()
	{
		var config = new AxisD1110Config { Resolution = "1920x1080" };

		Build(config, new AxisD1110StreamConfig { Url = PreviewUrl }).Url
			.Should().Be("rtsp://192.168.10.240/axis-media/media.amp?resolution=1920x1080");
	}

	[Fact]
	public void ResolutionOverride_PerStreamBeatsPluginLevel()
	{
		var config = new AxisD1110Config { Resolution = "1920x1080" };
		var stream = new AxisD1110StreamConfig { Url = PreviewUrl, Resolution = "1280x720" };

		Build(config, stream).Url.Should().EndWith("resolution=1280x720");
	}

	[Fact]
	public void NoResolutionOverride_LeavesTheUrlAsReceived()
	{
		Build(new AxisD1110Config(), new AxisD1110StreamConfig { Url = PreviewUrl }).Url.Should().EndWith("resolution=800x450");
	}

	[Fact]
	public void Login_StreamEntryBeatsTheUrl()
	{
		var stream = new AxisD1110StreamConfig { Url = PreviewUrl, Username = "viewer", Password = "view-pw" };

		var result = Build(new AxisD1110Config(), stream);

		result.Username.Should().Be("viewer");
		result.Password.Should().Be("view-pw");
	}

	[Fact]
	public void Login_UrlBeatsThePluginDefault()
	{
		var config = new AxisD1110Config { StreamUsername = "default", StreamPassword = "default-pw" };

		var result = Build(config, new AxisD1110StreamConfig { Url = PreviewUrl });

		result.Username.Should().Be("admin");
		result.Password.Should().Be("axis1234");
	}

	[Fact]
	public void Login_PluginDefaultIsUsedWhenTheUrlHasNone()
	{
		var config = new AxisD1110Config { StreamUsername = "default", StreamPassword = "default-pw" };

		var result = Build(config, new AxisD1110StreamConfig { Url = "rtsp://192.168.10.240/axis-media/media.amp" });

		result.Username.Should().Be("default");
		result.Password.Should().Be("default-pw");
	}

	[Fact]
	public void Login_NoneAnywhereLeavesItEmpty()
	{
		var result = Build(new AxisD1110Config(), new AxisD1110StreamConfig { Url = "rtsp://192.168.10.240/axis-media/media.amp" });

		result.Username.Should().BeEmpty();
		result.Password.Should().BeEmpty();
	}

	[Fact]
	public void Login_PasswordNeverComesFromADifferentSourceThanTheLogin()
	{
		// the URL supplies the login, so the plugin default password must not be paired with it
		var config = new AxisD1110Config { StreamPassword = "default-pw" };

		var result = Build(config, new AxisD1110StreamConfig { Url = "rtsp://viewer@192.168.10.240/x" });

		result.Username.Should().Be("viewer");
		result.Password.Should().BeEmpty();
	}

	[Fact]
	public void SentUrl_NeverContainsALogin()
	{
		var result = Build(new AxisD1110Config { Resolution = "1920x1080" }, new AxisD1110StreamConfig { Url = PreviewUrl });

		result.Url.Should().NotContain("@").And.NotContain("axis1234");
	}

	private sealed class FakeCamera : IAxisCameraSource
	{
		public string Key { get; set; } = "cam-33";
		public string Host { get; set; } = "192.168.10.241";
		public string Username { get; set; } = "admin";
		public string Password { get; set; } = "axis1234";
	}

	private static AxisD1110StreamConfig BuildWithCamera(AxisD1110Config config, AxisD1110StreamConfig stream, IAxisCameraSource camera, string bridgeUrl = null)
	{
		AxisD1110StreamBuilder.TryBuild(config, stream, bridgeUrl, camera, out var result, out var error)
			.Should().BeTrue("building should work, error was: {0}", error);
		return result;
	}

	[Fact]
	public void Camera_StreamIsBuiltFromTheCamerasAddressAndLogin()
	{
		var stream = new AxisD1110StreamConfig { CameraKey = "cam-33" };

		var result = BuildWithCamera(new AxisD1110Config { Resolution = "1920x1080" }, stream, new FakeCamera());

		result.Url.Should().Be("rtsp://192.168.10.241/axis-media/media.amp?resolution=1920x1080");
		result.Username.Should().Be("admin");
		result.Password.Should().Be("axis1234");
	}

	[Fact]
	public void Camera_BeatsAConfiguredUrlAndABridgeUrl()
	{
		var stream = new AxisD1110StreamConfig { CameraKey = "cam-33", Url = "rtsp://10.0.0.1/other" };

		BuildWithCamera(new AxisD1110Config(), stream, new FakeCamera(), "rtsp://10.0.0.2/bridge").Url
			.Should().Be("rtsp://192.168.10.241/axis-media/media.amp");
		BuildWithCamera(new AxisD1110Config { StreamSource = "bridge" }, stream, new FakeCamera(), "rtsp://10.0.0.2/bridge").Url
			.Should().Be("rtsp://192.168.10.241/axis-media/media.amp");
	}

	[Theory]
	[InlineData("/stream/live", "rtsp://192.168.10.241/stream/live")]
	[InlineData("stream/live", "rtsp://192.168.10.241/stream/live")]
	[InlineData("/axis-media/media.amp?videocodec=h264", "rtsp://192.168.10.241/axis-media/media.amp?videocodec=h264")]
	public void Camera_StreamPathCanBeChanged(string path, string expected)
	{
		var config = new AxisD1110Config { CameraStreamPath = path };

		BuildWithCamera(config, new AxisD1110StreamConfig { CameraKey = "cam-33" }, new FakeCamera()).Url.Should().Be(expected);
	}

	[Fact]
	public void Camera_NotFoundNamesTheKey()
	{
		AxisD1110StreamBuilder.TryBuild(new AxisD1110Config(), new AxisD1110StreamConfig { CameraKey = "cam-99" }, null, null, out var result, out var error)
			.Should().BeFalse();

		result.Should().BeNull();
		error.Should().Contain("cam-99").And.Contain("not found");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void Camera_WithoutAnAddressYetIsAnError(string host)
	{
		AxisD1110StreamBuilder.TryBuild(new AxisD1110Config(), new AxisD1110StreamConfig { CameraKey = "cam-33" }, null, new FakeCamera { Host = host }, out _, out var error)
			.Should().BeFalse();

		error.Should().Contain("cam-33").And.Contain("no address");
	}

	[Fact]
	public void Camera_LoginPrecedence()
	{
		var camera = new FakeCamera();

		// the stream's own login wins over the camera's
		var own = BuildWithCamera(new AxisD1110Config(), new AxisD1110StreamConfig { CameraKey = "cam-33", Username = "viewer", Password = "view-pw" }, camera);
		own.Username.Should().Be("viewer");
		own.Password.Should().Be("view-pw");

		// the camera's wins over the plugin default
		var fromCamera = BuildWithCamera(new AxisD1110Config { StreamUsername = "default", StreamPassword = "default-pw" }, new AxisD1110StreamConfig { CameraKey = "cam-33" }, camera);
		fromCamera.Username.Should().Be("admin");
		fromCamera.Password.Should().Be("axis1234");

		// a camera with no login falls back to the plugin default
		var anonymous = new FakeCamera { Username = "", Password = "" };
		var fallback = BuildWithCamera(new AxisD1110Config { StreamUsername = "default", StreamPassword = "default-pw" }, new AxisD1110StreamConfig { CameraKey = "cam-33" }, anonymous);
		fallback.Username.Should().Be("default");
		fallback.Password.Should().Be("default-pw");
	}

	[Fact]
	public void Camera_SentUrlNeverContainsTheLogin()
	{
		var result = BuildWithCamera(new AxisD1110Config(), new AxisD1110StreamConfig { CameraKey = "cam-33" }, new FakeCamera());

		result.Url.Should().NotContain("@").And.NotContain("axis1234");
	}
}
