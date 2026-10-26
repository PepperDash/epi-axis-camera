using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderConfigTests
{
	[Fact]
	public void Config_ReadsAFullPropertiesObject()
	{
		const string json = @"{
			""host"": ""192.168.10.60"",
			""port"": 8080,
			""useHttps"": true,
			""ignoreCertificateErrors"": true,
			""username"": ""root"",
			""password"": ""secret"",
			""authMode"": ""basic"",
			""pollTimeMs"": 15000,
			""requestTimeoutMs"": 2000,
			""defaultStream"": 3,
			""streams"": {
				""1"": { ""name"": ""Left PTZ"", ""url"": ""rtsp://192.168.10.211/axis-media/media.amp"", ""videoCodec"": ""H264"" },
				""3"": { ""name"": ""Shared Monopod"", ""url"": ""rtsp://192.168.10.240/axis-media/media.amp"", ""username"": ""viewer"", ""password"": ""pass"" }
			}
		}";

		var config = JsonConvert.DeserializeObject<AxisD1110Config>(json);

		config.Host.Should().Be("192.168.10.60");
		config.Port.Should().Be(8080);
		config.UseHttps.Should().BeTrue();
		config.IgnoreCertificateErrors.Should().BeTrue();
		config.Username.Should().Be("root");
		config.Password.Should().Be("secret");
		config.AuthMode.Should().Be("basic");
		config.PollTimeMs.Should().Be(15000);
		config.RequestTimeoutMs.Should().Be(2000);
		config.DefaultStream.Should().Be(3);
		config.Streams.Keys.Should().BeEquivalentTo(new[] { "1", "3" });
		config.Streams["1"].Name.Should().Be("Left PTZ");
		config.Streams["3"].Url.Should().Be("rtsp://192.168.10.240/axis-media/media.amp");
		config.Streams["3"].Username.Should().Be("viewer");
		config.Streams["3"].Password.Should().Be("pass");
	}

	[Fact]
	public void Config_ReadsTheStreamSourceResolutionAndLogin()
	{
		const string json = @"{
			""host"": ""10.0.0.5"",
			""streamSource"": ""bridge"",
			""resolution"": ""1920x1080"",
			""streamUsername"": ""admin"",
			""streamPassword"": ""axis1234"",
			""streams"": { ""1"": { ""name"": ""Left PTZ"", ""resolution"": ""1280x720"" } }
		}";

		var config = JsonConvert.DeserializeObject<AxisD1110Config>(json);

		config.IsBridgeSource.Should().BeTrue();
		config.Resolution.Should().Be("1920x1080");
		config.StreamUsername.Should().Be("admin");
		config.StreamPassword.Should().Be("axis1234");
		config.Streams["1"].Resolution.Should().Be("1280x720");
	}

	[Theory]
	[InlineData(null, false)]
	[InlineData("", false)]
	[InlineData("config", false)]
	[InlineData("bridge", true)]
	[InlineData("BRIDGE", true)]
	[InlineData("nonsense", false)]
	public void Config_StreamSourceDefaultsToConfig(string source, bool expectedBridge)
	{
		new AxisD1110Config { StreamSource = source }.IsBridgeSource.Should().Be(expectedBridge);
	}

	[Fact]
	public void Config_MinimalPropertiesUseDefaults()
	{
		var config = JsonConvert.DeserializeObject<AxisD1110Config>("{\"host\":\"10.0.0.5\"}");

		config.Streams.Should().NotBeNull().And.BeEmpty();
		config.Port.Should().Be(0);
		config.UseHttps.Should().BeFalse();
		config.AuthMode.Should().BeNull("an unset authMode means digest");
		config.PollTimeMs.Should().Be(0, "0 means the 30 second default");
		config.DefaultStream.Should().Be(0);
	}
}
