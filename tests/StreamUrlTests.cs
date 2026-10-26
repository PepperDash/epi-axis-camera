using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class StreamUrlTests
{
	private static AxisStreamUrl Parse(string url)
	{
		AxisStreamUrl.TryParse(url, out var parsed).Should().BeTrue("{0} should parse", url);
		return parsed;
	}

	[Fact]
	public void Parse_SplitsAUrlWithLogin()
	{
		var url = Parse("rtsp://admin:axis1234@192.168.10.240/axis-media/media.amp?resolution=800x450");

		url.Scheme.Should().Be("rtsp");
		url.Username.Should().Be("admin");
		url.Password.Should().Be("axis1234");
		url.HostPort.Should().Be("192.168.10.240");
		url.Path.Should().Be("/axis-media/media.amp");
		url.Query.Should().Be("resolution=800x450");
	}

	[Fact]
	public void Parse_UrlWithoutLoginHasEmptyCredentials()
	{
		var url = Parse("rtsp://192.168.10.240/axis-media/media.amp");

		url.Username.Should().BeEmpty();
		url.Password.Should().BeEmpty();
		url.Query.Should().BeEmpty();
	}

	[Fact]
	public void Parse_KeepsThePort()
	{
		Parse("rtsp://root:pw@192.168.10.240:554/stream").HostPort.Should().Be("192.168.10.240:554");
	}

	[Fact]
	public void Parse_PasswordMayContainAtSignsAndColons()
	{
		var url = Parse("rtsp://root:p@ss:w0rd@192.168.10.240/axis-media/media.amp");

		url.Username.Should().Be("root");
		url.Password.Should().Be("p@ss:w0rd", "the credentials end at the last @ before the host");
		url.HostPort.Should().Be("192.168.10.240");
	}

	[Fact]
	public void Parse_DecodesPercentEncodedCredentials()
	{
		var url = Parse("rtsp://root:p%40ss%3Aw0rd@192.168.10.240/x");

		url.Password.Should().Be("p@ss:w0rd");
	}

	[Fact]
	public void Parse_LoginWithoutPasswordIsAllowed()
	{
		var url = Parse("rtsp://viewer@192.168.10.240/x");

		url.Username.Should().Be("viewer");
		url.Password.Should().BeEmpty();
	}

	[Fact]
	public void Parse_UrlWithOnlyAHostHasNoPathOrQuery()
	{
		var url = Parse("rtsp://192.168.10.240");

		url.HostPort.Should().Be("192.168.10.240");
		url.Path.Should().BeEmpty();
		url.ToUrl().Should().Be("rtsp://192.168.10.240");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("192.168.10.240")]
	[InlineData("rtsp://")]
	[InlineData("rtsp://user:pass@/path")]
	[InlineData("://host/path")]
	public void Parse_RejectsTextThatIsNotAStreamUrl(string text)
	{
		AxisStreamUrl.TryParse(text, out var parsed).Should().BeFalse();
		parsed.Should().BeNull();
	}

	[Fact]
	public void ToUrl_LeavesTheLoginOut()
	{
		Parse("rtsp://admin:axis1234@192.168.10.240/axis-media/media.amp?resolution=800x450")
			.ToUrl().Should().Be("rtsp://192.168.10.240/axis-media/media.amp?resolution=800x450");
	}

	[Fact]
	public void ToMaskedUrl_ShowsTheLoginButNotThePassword()
	{
		Parse("rtsp://admin:axis1234@192.168.10.240/axis-media/media.amp")
			.ToMaskedUrl().Should().Be("rtsp://admin:***@192.168.10.240/axis-media/media.amp");
	}

	[Theory]
	[InlineData("rtsp://h/axis-media/media.amp?resolution=800x450", "rtsp://h/axis-media/media.amp?resolution=1920x1080")]
	[InlineData("rtsp://h/axis-media/media.amp?RESOLUTION=800x450&fps=15", "rtsp://h/axis-media/media.amp?resolution=1920x1080&fps=15")]
	[InlineData("rtsp://h/axis-media/media.amp?videocodec=h264&resolution=800x450&fps=15", "rtsp://h/axis-media/media.amp?videocodec=h264&resolution=1920x1080&fps=15")]
	[InlineData("rtsp://h/axis-media/media.amp", "rtsp://h/axis-media/media.amp?resolution=1920x1080")]
	[InlineData("rtsp://h/axis-media/media.amp?fps=15", "rtsp://h/axis-media/media.amp?fps=15&resolution=1920x1080")]
	[InlineData("rtsp://h/x?resolution=1x1&resolution=2x2", "rtsp://h/x?resolution=1920x1080")]
	public void WithResolution_ReplacesOrAddsTheResolutionParameter(string input, string expected)
	{
		Parse(input).WithResolution("1920x1080").ToUrl().Should().Be(expected);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void WithResolution_EmptyResolutionLeavesTheUrlAsReceived(string resolution)
	{
		Parse("rtsp://h/axis-media/media.amp?resolution=800x450").WithResolution(resolution)
			.ToUrl().Should().Be("rtsp://h/axis-media/media.amp?resolution=800x450");
	}

	[Fact]
	public void WithResolution_KeepsTheLogin()
	{
		var url = Parse("rtsp://admin:axis1234@h/x?resolution=800x450").WithResolution("1920x1080");

		url.Username.Should().Be("admin");
		url.Password.Should().Be("axis1234");
	}
}
