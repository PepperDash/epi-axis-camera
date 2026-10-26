using System.Net;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

/// <summary>
/// Runs the real client against a local HttpListener standing in for the decoder
/// </summary>
public class DecoderClientTests
{
	private const string Success = "{\"apiVersion\":\"1.0\",\"context\":\"d1110-1\",\"method\":\"x\",\"data\":{\"configurationId\":\"9\"}}";

	private static AxisD1110Config ConfigFor(FakeHttpServer decoder, string authMode = null) => new()
	{
		Host = "127.0.0.1",
		Port = decoder.Port,
		Username = "root",
		Password = "pass",
		AuthMode = authMode,
		RequestTimeoutMs = 3000
	};

	[Fact]
	public void GetViewConfiguration_PostsTheMethodToDecoderCgi()
	{
		using var decoder = new FakeHttpServer(r => (200, Success, null));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1");

		var result = client.GetViewConfiguration();

		result.Success.Should().BeTrue();
		result.ConfigurationId.Should().Be("9");

		var request = decoder.Requests.Single();
		request.Method.Should().Be("POST");
		request.Path.Should().Be("/axis-cgi/decoder.cgi");
		request.ContentType.Should().StartWith("application/json");
		var body = JObject.Parse(request.Body);
		body.Value<string>("apiVersion").Should().Be("1.0");
		body.Value<string>("context").Should().Be("d1110-1");
		body.Value<string>("method").Should().Be("getViewConfiguration");
		body.ContainsKey("params").Should().BeFalse();
	}

	[Fact]
	public void SetViewConfiguration_SendsTheStreamAsParams()
	{
		using var decoder = new FakeHttpServer(r => (200, Success, null));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1");

		var result = client.SetViewConfiguration(new AxisD1110StreamConfig { Url = "rtsp://cam/stream" });

		result.Success.Should().BeTrue();
		var body = JObject.Parse(decoder.Requests.Single().Body);
		body.Value<string>("method").Should().Be("setViewConfiguration");
		body["params"]!["streams"]![0]!.Value<string>("url").Should().Be("rtsp://cam/stream");
	}

	[Fact]
	public void BasicMode_SendsTheAuthorizationHeaderOnTheFirstRequest()
	{
		using var decoder = new FakeHttpServer(r => (200, Success, null));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder, "basic"), "d1110-1");

		client.GetViewConfiguration();

		var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("root:pass"));
		decoder.Requests.Single().Authorization.Should().Be(expected);
	}

	[Fact]
	public void DigestMode_AnswersADigestChallenge()
	{
		using var decoder = new FakeHttpServer(r =>
			r.Authorization != null && r.Authorization.StartsWith("Digest ")
				? (200, Success, null)
				: (401, "", "Digest realm=\"AXIS\", nonce=\"abc123\", qop=\"auth\", algorithm=MD5"));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1");

		var result = client.GetViewConfiguration();

		result.Success.Should().BeTrue();
		decoder.Requests.Should().HaveCount(2);
		decoder.Requests[0].Authorization.Should().BeNull();
		decoder.Requests[1].Authorization.Should().StartWith("Digest ").And.Contain("username=\"root\"");
	}

	[Fact]
	public void WrongCredentials_ReportTheHttpStatus()
	{
		using var decoder = new FakeHttpServer(r => (401, "", "Basic realm=\"AXIS\""));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder, "basic"), "d1110-1");

		var result = client.GetViewConfiguration();

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(401);
		result.ErrorMessage.Should().StartWith("HTTP 401");
	}

	[Fact]
	public void DecoderErrors_ComeBackThroughTheResult()
	{
		using var decoder = new FakeHttpServer(r =>
			(200, "{\"errors\":[{\"code\":402,\"message\":\"Insufficient decoder resources\"}]}", null));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1");

		var result = client.SetViewConfiguration(new AxisD1110StreamConfig { Url = "rtsp://cam/stream" });

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(402);
	}

	[Fact]
	public void NothingListening_ReportsATransportError()
	{
		int unusedPort;
		using (var probe = new FakeHttpServer(r => (200, Success, null)))
		{
			unusedPort = probe.Port;
		}

		var config = new AxisD1110Config { Host = "127.0.0.1", Port = unusedPort, RequestTimeoutMs = 1000 };
		using var client = new AxisD1110DecoderClient(config, "d1110-1");

		var result = client.GetViewConfiguration();

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(-1);
		result.ErrorMessage.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void SlowDecoder_TimesOutInsteadOfHanging()
	{
		using var decoder = new FakeHttpServer(r => { Thread.Sleep(1500); return (200, Success, null); });
		var config = ConfigFor(decoder);
		config.RequestTimeoutMs = 300;
		using var client = new AxisD1110DecoderClient(config, "d1110-1");

		var result = client.GetViewConfiguration();

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(-1);
	}

	[Fact]
	public void Send_PassesAnyMethodAndParamsThrough()
	{
		using var decoder = new FakeHttpServer(r => (200, Success, null));
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1");

		var result = client.Send("getCapabilities", null);

		result.Success.Should().BeTrue();
		JObject.Parse(decoder.Requests.Single().Body).Value<string>("method").Should().Be("getCapabilities");
	}

	[Fact]
	public void Logging_RecordsEachRequestAndResponse_WithoutPasswords()
	{
		using var decoder = new FakeHttpServer(r => (200, Success, null));
		var lines = new List<string>();
		using var client = new AxisD1110DecoderClient(ConfigFor(decoder), "d1110-1", lines.Add);

		client.SetViewConfiguration(new AxisD1110StreamConfig { Url = "rtsp://cam/stream", Username = "viewer", Password = "hunter2" });

		lines.Should().HaveCount(2);
		lines[0].Should().StartWith("POST http://127.0.0.1:").And.Contain("/axis-cgi/decoder.cgi").And.Contain("setViewConfiguration");
		lines[1].Should().Contain("setViewConfiguration answered HTTP 200 in").And.Contain("configurationId");
		lines.Should().NotContain(l => l.Contains("hunter2"));
	}

	[Fact]
	public void Logging_RecordsATransportFailure()
	{
		int unusedPort;
		using (var probe = new FakeHttpServer(r => (200, Success, null)))
		{
			unusedPort = probe.Port;
		}

		var lines = new List<string>();
		var config = new AxisD1110Config { Host = "127.0.0.1", Port = unusedPort, RequestTimeoutMs = 1000 };
		using var client = new AxisD1110DecoderClient(config, "d1110-1", lines.Add);

		client.GetViewConfiguration();

		lines.Should().HaveCount(2);
		lines[1].Should().Contain("getViewConfiguration to http://127.0.0.1:").And.Contain("failed after");
	}
}
