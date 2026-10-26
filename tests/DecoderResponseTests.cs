using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class DecoderResponseTests
{
	[Fact]
	public void ParseResponse_DataMeansSuccess()
	{
		var result = AxisD1110DecoderClient.ParseResponse(
			"{\"apiVersion\":\"1.0\",\"context\":\"123\",\"method\":\"setViewConfiguration\",\"data\":{\"configurationId\":\"42\"}}");

		result.Success.Should().BeTrue();
		result.ConfigurationId.Should().Be("42");
		result.ErrorCode.Should().Be(0);
		result.ErrorMessage.Should().BeNull();
	}

	[Fact]
	public void ParseResponse_ErrorsWithoutDataIsAFailure()
	{
		var result = AxisD1110DecoderClient.ParseResponse(
			"{\"apiVersion\":\"1.0\",\"context\":\"123\",\"method\":\"setViewConfiguration\",\"errors\":[{\"code\":202,\"message\":\"Invalid JSON\"}]}");

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(202);
		result.ErrorMessage.Should().Be("Invalid JSON");
	}

	[Fact]
	public void ParseResponse_StreamErrorWithDataStillAppliesTheConfiguration()
	{
		var result = AxisD1110DecoderClient.ParseResponse(
			"{\"data\":{\"configurationId\":\"43\"},\"errors\":[{\"code\":400,\"message\":\"Failed to connect\",\"viewId\":0,\"streamId\":0}]}");

		result.Success.Should().BeTrue();
		result.ConfigurationId.Should().Be("43");
		result.ErrorCode.Should().Be(400);
		result.ErrorMessage.Should().Be("Failed to connect");
	}

	[Fact]
	public void ParseResponse_ReportsFirstErrorOnly()
	{
		var result = AxisD1110DecoderClient.ParseResponse(
			"{\"errors\":[{\"code\":401,\"message\":\"first\"},{\"code\":402,\"message\":\"second\"}]}");

		result.ErrorCode.Should().Be(401);
		result.ErrorMessage.Should().Be("first");
	}

	[Fact]
	public void ParseResponse_ConfigurationChangedCodeIsPassedThrough()
	{
		// Code 100 is informational. The device decides it is not an error
		var result = AxisD1110DecoderClient.ParseResponse(
			"{\"data\":{\"configurationId\":\"7\"},\"errors\":[{\"code\":100,\"message\":\"7\"}]}");

		result.Success.Should().BeTrue();
		result.ErrorCode.Should().Be(100);
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("")]
	[InlineData("<html>login</html>")]
	public void ParseResponse_UnreadableBodyIsATransportError(string body)
	{
		var result = AxisD1110DecoderClient.ParseResponse(body);

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(-2);
		result.ErrorMessage.Should().NotBeNullOrEmpty();
	}

	[Fact]
	public void ParseResponse_NeitherDataNorErrorsIsAFailure()
	{
		var result = AxisD1110DecoderClient.ParseResponse("{\"apiVersion\":\"1.0\"}");

		result.Success.Should().BeFalse();
		result.ErrorCode.Should().Be(-2);
	}
}
