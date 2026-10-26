using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class PtzCommandTests
{
	[Theory]
	[InlineData(50, 0, "/axis-cgi/com/ptz.cgi?continuouspantiltmove=50,0")]
	[InlineData(-50, 0, "/axis-cgi/com/ptz.cgi?continuouspantiltmove=-50,0")]
	[InlineData(0, 50, "/axis-cgi/com/ptz.cgi?continuouspantiltmove=0,50")]
	[InlineData(0, -50, "/axis-cgi/com/ptz.cgi?continuouspantiltmove=0,-50")]
	[InlineData(30, -40, "/axis-cgi/com/ptz.cgi?continuouspantiltmove=30,-40")]
	public void PanTilt_UsesContinuousMove(int pan, int tilt, string expected)
	{
		AxisPtzCommands.PanTilt(pan, tilt).Should().Be(expected);
	}

	[Fact]
	public void StopPanTilt_SendsZeroZero()
	{
		AxisPtzCommands.StopPanTilt().Should().Be("/axis-cgi/com/ptz.cgi?continuouspantiltmove=0,0");
	}

	[Fact]
	public void ZoomFocusIris_UseTheirContinuousMoves()
	{
		AxisPtzCommands.Zoom(75).Should().Be("/axis-cgi/com/ptz.cgi?continuouszoommove=75");
		AxisPtzCommands.Zoom(-75).Should().Be("/axis-cgi/com/ptz.cgi?continuouszoommove=-75");
		AxisPtzCommands.Zoom(0).Should().Be("/axis-cgi/com/ptz.cgi?continuouszoommove=0");
		AxisPtzCommands.Focus(50).Should().Be("/axis-cgi/com/ptz.cgi?continuousfocusmove=50");
		AxisPtzCommands.Focus(-50).Should().Be("/axis-cgi/com/ptz.cgi?continuousfocusmove=-50");
		AxisPtzCommands.Iris(50).Should().Be("/axis-cgi/com/ptz.cgi?continuousirismove=50");
		AxisPtzCommands.Iris(0).Should().Be("/axis-cgi/com/ptz.cgi?continuousirismove=0");
	}

	[Fact]
	public void HomeAutoFocusAutoIrisAndQuery()
	{
		AxisPtzCommands.Home().Should().Be("/axis-cgi/com/ptz.cgi?move=home");
		AxisPtzCommands.AutoFocus(true).Should().Be("/axis-cgi/com/ptz.cgi?autofocus=on");
		AxisPtzCommands.AutoFocus(false).Should().Be("/axis-cgi/com/ptz.cgi?autofocus=off");
		AxisPtzCommands.AutoIris(true).Should().Be("/axis-cgi/com/ptz.cgi?autoiris=on");
		AxisPtzCommands.AutoIris(false).Should().Be("/axis-cgi/com/ptz.cgi?autoiris=off");
		AxisPtzCommands.QueryPosition().Should().Be("/axis-cgi/com/ptz.cgi?query=position");
		AxisPtzCommands.RestartPath.Should().Be("/axis-cgi/restart.cgi");
	}

	[Theory]
	[InlineData(1, "Preset01")]
	[InlineData(8, "Preset08")]
	[InlineData(12, "Preset12")]
	[InlineData(100, "Preset100")]
	public void PresetName_IsTwoDigitsAtLeast(int number, string expected)
	{
		AxisPtzCommands.PresetName(number).Should().Be(expected);
	}

	[Fact]
	public void GotoPreset_ServerUsesTheNameDeviceUsesTheNumber()
	{
		AxisPtzCommands.GotoPreset(3, true).Should().Be("/axis-cgi/com/ptz.cgi?gotoserverpresetname=Preset03");
		AxisPtzCommands.GotoPreset(3, false).Should().Be("/axis-cgi/com/ptz.cgi?gotodevicepreset=3");
	}

	[Fact]
	public void SetPreset_ServerUsesTheNameAndOnlyPresetOneBecomesHome()
	{
		AxisPtzCommands.SetPreset(1, true, AxisPtzCommands.PtzConfigPath, true)
			.Should().Be("/axis-cgi/com/ptzconfig.cgi?setserverpresetname=Preset01&home=yes");
		AxisPtzCommands.SetPreset(2, true, AxisPtzCommands.PtzConfigPath, true)
			.Should().Be("/axis-cgi/com/ptzconfig.cgi?setserverpresetname=Preset02");
		AxisPtzCommands.SetPreset(1, true, AxisPtzCommands.PtzConfigPath, false)
			.Should().Be("/axis-cgi/com/ptzconfig.cgi?setserverpresetname=Preset01");
	}

	[Fact]
	public void SetPreset_CanUsePtzCgiLikeTheOldControlProgram()
	{
		AxisPtzCommands.SetPreset(2, true, AxisPtzCommands.PtzPath, true)
			.Should().Be("/axis-cgi/com/ptz.cgi?setserverpresetname=Preset02");
	}

	[Fact]
	public void SetPreset_DeviceUsesTheNumber()
	{
		AxisPtzCommands.SetPreset(5, false, AxisPtzCommands.PtzConfigPath, true)
			.Should().Be("/axis-cgi/com/ptzconfig.cgi?setdevicepreset=5");
	}

	[Theory]
	[InlineData(-5, 1)]
	[InlineData(0, 1)]
	[InlineData(1, 1)]
	[InlineData(50, 50)]
	[InlineData(100, 100)]
	[InlineData(250, 100)]
	public void ClampSpeed_KeepsSpeedsInTheCameraRange(int speed, int expected)
	{
		AxisPtzCommands.ClampSpeed(speed).Should().Be(expected);
	}
}

public class PtzResponseTests
{
	private static AxisHttpResult Answer(int status, string body = "") =>
		new() { GotResponse = true, StatusCode = status, ReasonPhrase = "x", Body = body };

	[Fact]
	public void NoContent_IsSuccess()
	{
		AxisPtzCommands.IsError(Answer(204), out var error).Should().BeFalse();
		error.Should().BeNull();
	}

	[Fact]
	public void Ok_WithAPositionBody_IsSuccess()
	{
		AxisPtzCommands.IsError(Answer(200, "pan=0.00\r\ntilt=0.00\r\nzoom=1\r\nautofocus=on\r\nautoiris=on\r\n"), out _).Should().BeFalse();
	}

	[Theory]
	[InlineData("Error:Unknown command", "Unknown command")]
	[InlineData("  Error: query: unknown value: x", "query: unknown value: x")]
	[InlineData("error:lower case", "lower case")]
	public void Ok_WithAnErrorBody_IsAnErrorEvenThoughTheStatusIs200(string body, string expected)
	{
		AxisPtzCommands.IsError(Answer(200, body), out var error).Should().BeTrue();
		error.Should().Be(expected);
	}

	[Fact]
	public void ErrorBodyWithNoText_StillReportsSomething()
	{
		AxisPtzCommands.IsError(Answer(200, "Error:"), out var error).Should().BeTrue();
		error.Should().NotBeNullOrEmpty();
	}

	[Theory]
	[InlineData(401)]
	[InlineData(403)]
	[InlineData(404)]
	[InlineData(500)]
	public void HttpFailureStatuses_AreErrors(int status)
	{
		AxisPtzCommands.IsError(Answer(status), out var error).Should().BeTrue();
		error.Should().StartWith("HTTP " + status);
	}

	[Fact]
	public void NoResponse_IsAnErrorWithTheExceptionMessage()
	{
		var result = new AxisHttpResult { GotResponse = false, Error = "No connection could be made" };

		AxisPtzCommands.IsError(result, out var error).Should().BeTrue();
		error.Should().Be("No connection could be made");
	}
}

public class PtzPositionTests
{
	[Fact]
	public void Parse_ReadsAnswerWithSeparateLines()
	{
		var position = AxisPtzPosition.Parse("pan=-12.50\r\ntilt=30.25\r\nzoom=1500\r\nautofocus=on\r\nautoiris=off\r\n");

		position.Pan.Should().Be(-12.5);
		position.Tilt.Should().Be(30.25);
		position.Zoom.Should().Be(1500);
		position.AutoFocus.Should().BeTrue();
		position.AutoIris.Should().BeFalse();
	}

	[Fact]
	public void Parse_ReadsAnswerWithRunTogetherValues()
	{
		var position = AxisPtzPosition.Parse("pan=10.0tilt=-5.5zoom=200autofocus=offautoiris=on");

		position.Pan.Should().Be(10.0);
		position.Tilt.Should().Be(-5.5);
		position.Zoom.Should().Be(200);
		position.AutoFocus.Should().BeFalse();
		position.AutoIris.Should().BeTrue();
	}

	[Fact]
	public void Parse_ValuesTheCameraDidNotReportAreNull()
	{
		var position = AxisPtzPosition.Parse("pan=1.0\ntilt=2.0\n");

		position.AutoFocus.Should().BeNull();
		position.AutoIris.Should().BeNull();
		position.Zoom.Should().BeNull();
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("garbage")]
	public void Parse_NothingUsefulGivesAllNulls(string body)
	{
		var position = AxisPtzPosition.Parse(body);

		position.Pan.Should().BeNull();
		position.Tilt.Should().BeNull();
		position.AutoFocus.Should().BeNull();
		position.AutoIris.Should().BeNull();
	}

	[Fact]
	public void Parse_AutoFocusIsNotConfusedWithOtherKeys()
	{
		// "focus=" style keys and "preset...pan" must not be read as pan/tilt
		var position = AxisPtzPosition.Parse("autopan=5\nautofocus=on\nfocus=1234\n");

		position.Pan.Should().BeNull();
		position.AutoFocus.Should().BeTrue();
	}

	[Fact]
	public void Parse_IsNotCaseSensitive()
	{
		var position = AxisPtzPosition.Parse("AutoFocus=ON\nAUTOIRIS=Off\n");

		position.AutoFocus.Should().BeTrue();
		position.AutoIris.Should().BeFalse();
	}
}
