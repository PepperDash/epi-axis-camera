using FluentAssertions;
using Newtonsoft.Json;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class CameraConfigTests
{
	[Fact]
	public void Config_ReadsAFullPropertiesObject()
	{
		const string json = @"{
			""host"": ""192.168.10.241"",
			""port"": 8080,
			""username"": ""admin"",
			""password"": ""axis1234"",
			""authMode"": ""basic"",
			""panSpeed"": 40, ""tiltSpeed"": 30, ""zoomSpeed"": 80, ""focusSpeed"": 20, ""irisSpeed"": 10,
			""presetMode"": ""device"",
			""presetCount"": 12,
			""presets"": { ""1"": ""Home"", ""2"": ""Podium"" },
			""pollTimeMs"": 15000,
			""connectionSource"": ""bridge"",
			""joinLayout"": ""indexed"",
			""cameraIndex"": 7
		}";

		var config = JsonConvert.DeserializeObject<AxisCameraConfig>(json);

		config.Host.Should().Be("192.168.10.241");
		config.Port.Should().Be(8080);
		config.Username.Should().Be("admin");
		config.AuthMode.Should().Be("basic");
		config.PanSpeed.Should().Be(40);
		config.TiltSpeed.Should().Be(30);
		config.ZoomSpeed.Should().Be(80);
		config.FocusSpeed.Should().Be(20);
		config.IrisSpeed.Should().Be(10);
		config.PresetMode.Should().Be("device");
		config.UseServerPresets.Should().BeFalse();
		config.PresetCount.Should().Be(12);
		config.Presets["2"].Should().Be("Podium");
		config.PollTimeMs.Should().Be(15000);
		config.IsBridgeSource.Should().BeTrue();
		config.IsIndexedLayout.Should().BeTrue();
		config.CameraIndex.Should().Be(7);
	}

	[Fact]
	public void Config_MinimalPropertiesUseDefaults()
	{
		var config = JsonConvert.DeserializeObject<AxisCameraConfig>("{\"host\":\"10.0.0.5\"}");

		config.IsBridgeSource.Should().BeFalse();
		config.IsIndexedLayout.Should().BeFalse();
		config.UseServerPresets.Should().BeTrue("server presets are the default");
		config.PresetCount.Should().Be(0, "0 means the default of 8");
		config.PanSpeed.Should().Be(0, "0 means the default");
		config.Presets.Should().NotBeNull().And.BeEmpty();
	}

	[Theory]
	[InlineData(null, true)]
	[InlineData("", true)]
	[InlineData("server", true)]
	[InlineData("SERVER", true)]
	[InlineData("device", false)]
	[InlineData("Device", false)]
	public void PresetMode_DefaultsToServer(string mode, bool expectedServer)
	{
		new AxisCameraConfig { PresetMode = mode }.UseServerPresets.Should().Be(expectedServer);
	}

	[Fact]
	public void ToConnection_CarriesTheSharedProperties()
	{
		var config = new AxisCameraConfig
		{
			Host = "192.168.10.241", Port = 8080, UseHttps = true, IgnoreCertificateErrors = true,
			Username = "admin", Password = "pw", AuthMode = "basic", RequestTimeoutMs = 1234
		};

		var connection = config.ToConnection();

		connection.Host.Should().Be("192.168.10.241");
		connection.Port.Should().Be(8080);
		connection.UseHttps.Should().BeTrue();
		connection.IgnoreCertificateErrors.Should().BeTrue();
		connection.Username.Should().Be("admin");
		connection.Password.Should().Be("pw");
		connection.AuthMode.Should().Be("basic");
		connection.TimeoutMs.Should().Be(1234);
	}

	[Theory]
	[InlineData("", 0, false, 80)]
	[InlineData("", 0, true, 443)]
	[InlineData("", 8080, false, 8080)]
	public void Connection_FillsInTheDefaultPort(string host, int port, bool https, int expected)
	{
		new AxisConnection { Host = host, Port = port, UseHttps = https }.EffectivePort.Should().Be(expected);
	}

	[Fact]
	public void Connection_BuildsUrlsAndComparesConnections()
	{
		var a = new AxisConnection { Host = " 192.168.10.241 ", Username = "admin", Password = "pw" };
		var b = new AxisConnection { Host = "192.168.10.241", Port = 80, Username = "admin", Password = "pw" };

		a.IsUsable.Should().BeTrue();
		new AxisConnection { Host = "  " }.IsUsable.Should().BeFalse();
		a.BuildUri("/axis-cgi/com/ptz.cgi?query=position").ToString().Should().Be("http://192.168.10.241/axis-cgi/com/ptz.cgi?query=position");
		a.BuildUri("axis-cgi/restart.cgi").AbsolutePath.Should().Be("/axis-cgi/restart.cgi");
		a.SameAs(b).Should().BeFalse("the host has stray spaces in a");
		b.SameAs(new AxisConnection { Host = "192.168.10.241", Username = "admin", Password = "pw" }).Should().BeTrue();
		b.SameAs(new AxisConnection { Host = "192.168.10.241", Username = "admin", Password = "other" }).Should().BeFalse();
		b.SameAs(null).Should().BeFalse();
	}
}
