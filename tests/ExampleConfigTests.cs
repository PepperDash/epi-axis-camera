using FluentAssertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

/// <summary>
/// The example configuration files are read by the plugin's own configuration classes, so they cannot drift from the code
/// </summary>
public class ExampleConfigTests
{
	public static IEnumerable<object[]> ExampleFiles()
	{
		var folder = Path.Combine(AppContext.BaseDirectory, "examples");

		foreach (var file in Directory.GetFiles(folder, "*.json"))
		{
			yield return new object[] { Path.GetFileName(file) };
		}
	}

	private static List<JObject> Devices(string file)
	{
		var root = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", file)));
		return root["template"]!["devices"]!.Children<JObject>().ToList();
	}

	[Fact]
	public void ThereAreExamples()
	{
		ExampleFiles().Should().HaveCountGreaterThanOrEqualTo(2);
	}

	[Theory]
	[MemberData(nameof(ExampleFiles))]
	public void EveryDeviceIsReadableByThePluginAndComplete(string file)
	{
		var devices = Devices(file);

		var keys = devices.Select(d => d.Value<string>("key")).ToList();
		keys.Should().OnlyHaveUniqueItems();
		devices.Select(d => d.Value<int>("uid")).Should().OnlyHaveUniqueItems();

		var cameraKeys = devices.Where(d => d.Value<string>("type") == "axisCamera").Select(d => d.Value<string>("key")).ToList();

		foreach (var device in devices)
		{
			switch (device.Value<string>("type"))
			{
				case "axisCamera":
				{
					var config = device["properties"]!.ToObject<AxisCameraConfig>()!;

					if (config.IsBridgeSource)
					{
						config.Host.Should().BeNullOrEmpty("the address comes from SIMPL");
					}
					else
					{
						config.Host.Should().NotBeNullOrEmpty();
						config.Username.Should().NotBeNullOrEmpty();
					}

					if (config.IsIndexedLayout) config.CameraIndex.Should().BeGreaterThanOrEqualTo(1);

					break;
				}

				case "axisD1110":
				{
					var config = device["properties"]!.ToObject<AxisD1110Config>()!;

					config.Host.Should().NotBeNullOrEmpty();
					config.Username.Should().NotBeNullOrEmpty();
					config.Streams.Should().NotBeEmpty();
					config.Streams.Keys.Should().OnlyContain(k => int.Parse(k) >= 1 && int.Parse(k) <= 16);

					foreach (var stream in config.Streams.Values)
					{
						if (!string.IsNullOrEmpty(stream.CameraKey))
						{
							cameraKeys.Should().Contain(stream.CameraKey, "a stream's cameraKey must be a camera in the same file");
						}
						else if (config.IsBridgeSource)
						{
							stream.Url.Should().BeNullOrEmpty("the URLs come from the bridge");
							stream.Name.Should().NotBeNullOrEmpty();
						}
						else
						{
							stream.Url.Should().StartWith("rtsp://");
						}
					}

					break;
				}
			}
		}
	}

	[Theory]
	[MemberData(nameof(ExampleFiles))]
	public void EveryAxisDeviceIsOnAnEiscWithoutOverlappingJoins(string file)
	{
		var devices = Devices(file);
		var axisKeys = devices.Where(d => (d.Value<string>("type") ?? "").StartsWith("axis")).Select(d => d.Value<string>("key")!).ToList();

		var eiscs = devices.Where(d => d.Value<string>("type") == "eiscApiAdvanced").ToList();
		eiscs.Should().NotBeEmpty();
		eiscs.Select(e => e["properties"]!["control"]!.Value<string>("ipid")).Should().OnlyHaveUniqueItems("two EISCs cannot share an IPID");

		var linked = eiscs.SelectMany(e => e["properties"]!["devices"]!.Children<JObject>()).ToList();
		linked.Select(l => l.Value<string>("deviceKey")).Should().BeEquivalentTo(axisKeys, "every Axis device is linked to a bridge exactly once");

		foreach (var eisc in eiscs)
		{
			var onThisEisc = eisc["properties"]!["devices"]!.Children<JObject>().ToList();

			// indexed cameras share joinStart 1 and are told apart by cameraIndex
			var indexed = onThisEisc
				.Select(l => devices.Single(d => d.Value<string>("key") == l.Value<string>("deviceKey")))
				.Where(d => d.Value<string>("type") == "axisCamera" && d["properties"]!.ToObject<AxisCameraConfig>()!.IsIndexedLayout)
				.ToList();

			indexed.Select(d => d["properties"]!.Value<int>("cameraIndex")).Should().OnlyHaveUniqueItems();
			indexed.All(d => onThisEisc.Single(l => l.Value<string>("deviceKey") == d.Value<string>("key")).Value<int>("joinStart") == 1)
				.Should().BeTrue("indexed cameras use joinStart 1");

			var contiguous = onThisEisc
				.Where(l => !indexed.Any(d => d.Value<string>("key") == l.Value<string>("deviceKey")))
				.Select(l => l.Value<int>("joinStart"))
				.OrderBy(s => s)
				.ToList();

			for (var i = 1; i < contiguous.Count; i++)
			{
				(contiguous[i] - contiguous[i - 1]).Should().BeGreaterThanOrEqualTo(50, "each device can use up to 50 joins of a type");
			}
		}
	}
}
