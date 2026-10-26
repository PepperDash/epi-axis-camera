using FluentAssertions;
using Xunit;

namespace PepperDash.Essentials.Plugins.Camera.Axis.Tests;

public class JoinMapTests
{
	[Fact]
	public void Contiguous_FollowsTheJoinStart()
	{
		var map = new AxisCameraBridgeJoinMap(1);

		map.Up.JoinNumber.Should().Be(1);
		map.DownLeft.JoinNumber.Should().Be(8);
		map.PresetRecall.JoinNumber.Should().Be(17);
		map.PresetRecall.JoinSpan.Should().Be(8);
		map.PresetStored.JoinNumber.Should().Be(25);
		map.Restart.JoinNumber.Should().Be(26);
		map.PresetSave.JoinNumber.Should().Be(1);
		map.Host.JoinNumber.Should().Be(1);
		map.Password.JoinNumber.Should().Be(4);
		map.DeviceName.JoinNumber.Should().Be(5);
		map.LastError.JoinNumber.Should().Be(6);
		map.PreviewUrl.JoinNumber.Should().Be(7);

		var shifted = new AxisCameraBridgeJoinMap(101);
		shifted.Up.JoinNumber.Should().Be(101);
		shifted.PresetSave.JoinNumber.Should().Be(101);
		shifted.Host.JoinNumber.Should().Be(101);
	}

	[Fact]
	public void IndexedCameraOne_MatchesTheContiguousDigitalJoins()
	{
		var contiguous = new AxisCameraBridgeJoinMap(1);
		var indexed = new AxisCameraBridgeJoinMap(1, 1);

		indexed.Up.JoinNumber.Should().Be(contiguous.Up.JoinNumber);
		indexed.PresetRecall.JoinNumber.Should().Be(contiguous.PresetRecall.JoinNumber);
		indexed.Restart.JoinNumber.Should().Be(contiguous.Restart.JoinNumber);
	}

	[Theory]
	[InlineData(1, 1u)]
	[InlineData(2, 31u)]
	[InlineData(5, 121u)]
	[InlineData(41, 1201u)]
	public void Indexed_DigitalJoinsAreInBlocksOf30(int camera, uint firstJoin)
	{
		var map = new AxisCameraBridgeJoinMap(1, camera);

		map.Up.JoinNumber.Should().Be(firstJoin);
		map.Down.JoinNumber.Should().Be(firstJoin + 1);
		map.UpRight.JoinNumber.Should().Be(firstJoin + 4);
		map.ZoomIn.JoinNumber.Should().Be(firstJoin + 8);
		map.FocusFar.JoinNumber.Should().Be(firstJoin + 10);
		map.FocusNear.JoinNumber.Should().Be(firstJoin + 11);
		map.AutoFocus.JoinNumber.Should().Be(firstJoin + 12);
		map.IrisOpen.JoinNumber.Should().Be(firstJoin + 13);
		map.AutoIris.JoinNumber.Should().Be(firstJoin + 15);
		map.PresetRecall.JoinNumber.Should().Be(firstJoin + 16);
		map.PresetStored.JoinNumber.Should().Be(firstJoin + 24);
		map.Restart.JoinNumber.Should().Be(firstJoin + 25);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(9)]
	[InlineData(41)]
	public void Indexed_AnalogAndSerialJoinsFollowTheCameraNumber(int camera)
	{
		var map = new AxisCameraBridgeJoinMap(1, camera);

		map.PresetSave.JoinNumber.Should().Be((uint)camera);
		map.Host.JoinNumber.Should().Be((uint)camera);
		map.Port.JoinNumber.Should().Be((uint)(50 + camera));
		map.Username.JoinNumber.Should().Be((uint)(100 + camera));
		map.Password.JoinNumber.Should().Be((uint)(150 + camera));
		map.DeviceName.JoinNumber.Should().Be((uint)camera);
		map.PreviewUrl.JoinNumber.Should().Be((uint)(50 + camera));
		map.LastError.JoinNumber.Should().Be((uint)(100 + camera));
	}

	[Fact]
	public void Indexed_SerialJoinsToSimplStayWithinWhatTheMainProgramReads()
	{
		// the main program's EISC has 191 serial joins
		var map = new AxisCameraBridgeJoinMap(1, 41);

		map.DeviceName.JoinNumber.Should().BeLessThanOrEqualTo(191);
		map.PreviewUrl.JoinNumber.Should().BeLessThanOrEqualTo(191);
		map.LastError.JoinNumber.Should().BeLessThanOrEqualTo(191);
	}

	[Fact]
	public void Indexed_ToSimplSerialJoinsDoNotOverlapAcrossAllFortyOneCameras()
	{
		var used = new HashSet<uint>();

		for (var camera = 1; camera <= 41; camera++)
		{
			var map = new AxisCameraBridgeJoinMap(1, camera);

			foreach (var join in new[] { map.DeviceName, map.PreviewUrl, map.LastError })
			{
				used.Add(join.JoinNumber).Should().BeTrue("camera {0} serial join {1} must not repeat", camera, join.JoinNumber);
			}
		}
	}

	[Fact]
	public void Indexed_NoCameraShareADigitalJoinAcrossAllFortyOne()
	{
		var used = new HashSet<uint>();

		for (var camera = 1; camera <= 41; camera++)
		{
			var map = new AxisCameraBridgeJoinMap(1, camera);

			foreach (var join in new[]
			{
				map.Up, map.Down, map.Left, map.Right, map.UpRight, map.DownRight, map.UpLeft, map.DownLeft, map.ZoomIn, map.ZoomOut,
				map.FocusFar, map.FocusNear, map.AutoFocus, map.IrisOpen, map.IrisClose, map.AutoIris, map.PresetStored, map.Restart,
				map.Home, map.IsOnline
			})
			{
				used.Add(join.JoinNumber).Should().BeTrue("camera {0} join {1} must not repeat", camera, join.JoinNumber);
			}

			for (uint i = 0; i < map.PresetRecall.JoinSpan; i++)
			{
				used.Add(map.PresetRecall.JoinNumber + i).Should().BeTrue("camera {0} preset join must not repeat", camera);
			}
		}
	}

	[Fact]
	public void Indexed_MatchesTheLayoutTheMainProgramAlreadyUses()
	{
		// the SIMPL program wires camera 3 up as digital 61 (Up) ... 77 (Preset01) ... 85 (Preset_Store feedback), 86 (Restart)
		var map = new AxisCameraBridgeJoinMap(1, 3);

		map.Up.JoinNumber.Should().Be(61);
		map.AutoFocus.JoinNumber.Should().Be(73);
		map.AutoIris.JoinNumber.Should().Be(76);
		map.PresetRecall.JoinNumber.Should().Be(77);
		map.PresetStored.JoinNumber.Should().Be(85);
		map.Restart.JoinNumber.Should().Be(86);
		map.PresetSave.JoinNumber.Should().Be(3, "Preset_Save is analog join 3 for camera 3");
		map.Host.JoinNumber.Should().Be(3, "IPAddress$ is serial join 3");
		map.Password.JoinNumber.Should().Be(153);
		map.DeviceName.JoinNumber.Should().Be(3, "the camera name goes to the main program on serial join 3");
		map.PreviewUrl.JoinNumber.Should().Be(53);
	}

	[Fact]
	public void IndexZeroOrLess_UsesTheContiguousLayout()
	{
		new AxisCameraBridgeJoinMap(1, 0).Up.JoinNumber.Should().Be(1);
		new AxisCameraBridgeJoinMap(1, -3).Host.JoinNumber.Should().Be(1);
	}
}
