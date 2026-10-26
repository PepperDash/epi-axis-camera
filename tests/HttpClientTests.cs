using System.Text;
using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

/// <summary>
/// The shared HTTP client, used the way the camera device uses it (GET with a query)
/// </summary>
public class HttpClientTests
{
	private static AxisConnection ConnectionFor(FakeHttpServer server, string authMode = null) => new()
	{
		Host = "127.0.0.1",
		Port = server.Port,
		Username = "admin",
		Password = "axis1234",
		AuthMode = authMode,
		TimeoutMs = 3000
	};

	[Fact]
	public void Get_SendsThePathAndQuery_AndASuccessHasNoBody()
	{
		using var camera = new FakeHttpServer(r => (204, "", null));
		using var client = new AxisHttpClient(ConnectionFor(camera), null);

		var result = client.Get("stop", AxisPtzCommands.StopPanTilt());

		result.IsSuccessStatusCode.Should().BeTrue();
		result.StatusCode.Should().Be(204);
		AxisPtzCommands.IsError(result, out _).Should().BeFalse();

		var request = camera.Requests.Single();
		request.Method.Should().Be("GET");
		request.Path.Should().Be("/axis-cgi/com/ptz.cgi");
		request.Query.Should().Be("?continuouspantiltmove=0,0");
	}

	[Fact]
	public void Get_AnErrorBodyOn200IsRecognizedAsAnError()
	{
		using var camera = new FakeHttpServer(r => (200, "Error:Unsupported command", null));
		using var client = new AxisHttpClient(ConnectionFor(camera), null);

		var result = client.Get("iris open", AxisPtzCommands.Iris(50));

		result.IsSuccessStatusCode.Should().BeTrue("HTTP says 200");
		AxisPtzCommands.IsError(result, out var error).Should().BeTrue();
		error.Should().Be("Unsupported command");
	}

	[Fact]
	public void Get_ReadsAPositionAnswer()
	{
		using var camera = new FakeHttpServer(r => (200, "pan=0.00\r\ntilt=0.00\r\nzoom=1\r\nautofocus=on\r\nautoiris=off\r\n", null));
		using var client = new AxisHttpClient(ConnectionFor(camera), null);

		var result = client.Get("poll", AxisPtzCommands.QueryPosition());

		var position = AxisPtzPosition.Parse(result.Body);
		position.AutoFocus.Should().BeTrue();
		position.AutoIris.Should().BeFalse();
	}

	[Fact]
	public void Get_AnswersADigestChallenge()
	{
		using var camera = new FakeHttpServer(r =>
			r.Authorization != null && r.Authorization.StartsWith("Digest ")
				? (204, "", null)
				: (401, "", "Digest realm=\"AXIS_ACCC8E\", nonce=\"abc123\", qop=\"auth\", algorithm=MD5"));
		using var client = new AxisHttpClient(ConnectionFor(camera), null);

		var result = client.Get("home", AxisPtzCommands.Home());

		result.IsSuccessStatusCode.Should().BeTrue();
		camera.Requests.Should().HaveCount(2);
		camera.Requests[1].Authorization.Should().Contain("username=\"admin\"");
	}

	[Fact]
	public void Get_BasicModeSendsTheHeaderFirstTime()
	{
		using var camera = new FakeHttpServer(r => (204, "", null));
		using var client = new AxisHttpClient(ConnectionFor(camera, "basic"), null);

		client.Get("home", AxisPtzCommands.Home());

		camera.Requests.Single().Authorization.Should().Be("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:axis1234")));
	}

	[Fact]
	public void Get_WrongPasswordIsAnHttpError()
	{
		using var camera = new FakeHttpServer(r => (401, "", "Basic realm=\"AXIS\""));
		using var client = new AxisHttpClient(ConnectionFor(camera, "basic"), null);

		var result = client.Get("home", AxisPtzCommands.Home());

		AxisPtzCommands.IsError(result, out var error).Should().BeTrue();
		error.Should().StartWith("HTTP 401");
	}

	[Fact]
	public void Get_LogsTheRequestAndTheAnswer()
	{
		using var camera = new FakeHttpServer(r => (204, "", null));
		var lines = new List<string>();
		using var client = new AxisHttpClient(ConnectionFor(camera), lines.Add);

		client.Get("zoom in", AxisPtzCommands.Zoom(75));

		lines.Should().HaveCount(2);
		lines[0].Should().StartWith("GET http://127.0.0.1:").And.Contain("continuouszoommove=75");
		lines[1].Should().Contain("zoom in answered HTTP 204 in");
	}

	[Fact]
	public void Get_NothingListeningIsReportedWithoutThrowing()
	{
		int unusedPort;
		using (var probe = new FakeHttpServer(r => (204, "", null)))
		{
			unusedPort = probe.Port;
		}

		using var client = new AxisHttpClient(new AxisConnection { Host = "127.0.0.1", Port = unusedPort, TimeoutMs = 1000 }, null);

		var result = client.Get("poll", AxisPtzCommands.QueryPosition());

		result.GotResponse.Should().BeFalse();
		AxisPtzCommands.IsError(result, out var error).Should().BeTrue();
		error.Should().NotBeNullOrEmpty();
	}
}
