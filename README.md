![PepperDash Essentials Plugin Logo](/images/essentials-plugin-blue.png)

![PepperDash Essentials](https://img.shields.io/badge/PepperDash%20Essentials-≥%20v3.0.0-blue)
![.NET](https://img.shields.io/badge/.NET-net8-512BD4)
![Crestron](https://img.shields.io/badge/Crestron-4--Series-lightgrey)
![License](https://img.shields.io/badge/license-MIT-green)

# Axis Camera and Video Decoder Essentials Plugin (c) 2026

## License

Provided under MIT license

## Overview

Controls Axis network devices from PepperDash Essentials over VAPIX. Two device types:

| Type | Device | What it does |
|---|---|---|
| `axisCamera` | Axis PTZ camera, written for the **M5526-E** | Pan, tilt, zoom, focus, iris, home, presets (recall and save), restart |
| `axisD1110` | Axis D1110 video decoder | Tells the decoder which camera stream to show on its HDMI output |

They work together. A decoder stream can be a camera (`"cameraKey": "cam-33"`), so each camera's address and login is written once and the decoder builds the RTSP URL from it.

Typical use: Axis IP camera → network → Axis D1110 → HDMI → DM-NVX encoder → NVX. Essentials routes the NVX encoder. The `axisD1110` device chooses which camera the D1110 feeds it, and the `axisCamera` device moves that camera.

## Quick start

1. Install the plugin (`epi-axis-camera.4Series.<version>.cplz`) on a CP4N running Essentials 3.0.0 or later.
2. Add the devices to the Essentials configuration. Complete working files are in [examples/](examples/):
   * [configurationFile-axis-example.json](examples/configurationFile-axis-example.json): three cameras, two decoders and an EISC.
   * [configurationFile-axis-indexed-example.json](examples/configurationFile-axis-indexed-example.json): many cameras on one EISC, laid out by camera number. SIMPL is sent the camera names and preview URLs.
   Replace the addresses, passwords and the plugin version.
3. To control the devices from SIMPL, add them to an `eiscApiAdvanced` device (see [Bridge joins](#bridge-joins)).
4. Check a device from the processor console: `devjson:1 {"deviceKey":"cam-left","methodName":"PrintStatus","params":[]}`. The device logs its configuration and goes online after the first poll.
5. Move it: `devjson:1 {"deviceKey":"cam-left","methodName":"PositionHome","params":[]}`.

---

# axisCamera

## What you can control

| You want to | How | Notes |
|---|---|---|
| Pan, tilt (8 directions) | Hold the direction join. Release stops | Continuous move at `panSpeed` / `tiltSpeed` |
| Zoom | Hold ZoomIn or ZoomOut | At `zoomSpeed` |
| Focus | Hold FocusFar or FocusNear | At `focusSpeed`. FocusFar sends a positive speed, FocusNear a negative one |
| Autofocus | Press the AutoFocus join to toggle. The join also gives the state | State is read back from the camera |
| Iris, auto iris | Hold IrisOpen or IrisClose. Press AutoIris to toggle | See the note on the M5526-E below |
| Home | Press Home | `move=home` |
| Recall a preset | Press PresetRecall 1 to 8 | Presets 1 to `presetCount` (up to 100) |
| Save a preset | Send the preset number to the PresetSave analog join | Saves the camera's current position. Preset 1 also becomes the home position |
| Restart the camera | Press Restart | |
| Hide a camera's video everywhere (privacy) | Hold the Privacy join high, or call `SetPrivacy` | Clears the preview URL sent to SIMPL and blanks every decoder showing this camera. See [Privacy](#privacy) |
| Give SIMPL the camera's name and preview URL | Serial joins to SIMPL (see below) | The camera is configured once in Essentials. SIMPL gets the name and an RTSP URL for touch panel previews, so it needs no camera list of its own |
| Check it is reachable | Online feedback, `Poll` | A `query=position` request every `pollTimeMs` |

Presets are saved on the camera, so they survive a restart of the processor. By default they are *server presets* named `Preset01`, `Preset02`, ... (`"presetMode": "server"`). `"presetMode": "device"` uses the camera's numbered device presets instead.

Essentials code can also use the camera through the standard interfaces: `IHasCameraPtzControl`, `IHasCameraFocusControl`, `IHasAutoFocusMode` and `IHasCameraPresets`.

### What you get back

* **Online**: the camera answered the last request.
* **Autofocus** and **auto iris** state, read from the camera after a toggle and on every poll.
* **Preset saved**: goes high for a second after a preset was saved.
* **Last error**: the camera's error text, or a connection or HTTP failure. Empty when the last request worked.

### What it does not do

* **Absolute and relative moves** (`pan=`, `rpan=` and the like), **area zoom** and **click to center**. Only continuous moves, home and presets.
* **Camera settings** such as image, network, users and firmware.
* **Video.** Use the `axisD1110` device, or an RTSP client, for the picture.

### Notes for the M5526-E

According to the [Axis datasheet](https://axis.com/dam/public/d0/14/73/datasheet-axis-m5526-e-ptz-camera-pt-BR-475289.pdf), the M5526-E has continuous 360° pan, 0 to 90° tilt, 10x optical zoom and 100 preset positions, so `presetCount` can go up to 100. Whether it accepts the VAPIX iris commands has not been checked on a camera. If it does not, an iris request is answered with an error, which the plugin logs once and shows in Last error; nothing else is affected. Run `{"methodName":"SendRequest","params":["/axis-cgi/com/ptz.cgi?info=1"]}` on the bench to list the PTZ commands the camera really offers.

Axis announces a discontinuation for this model on its [support page](https://www.axis.com/en-ca/products/axis-m5526-e/support), so check availability before ordering more.

### Privacy

Privacy hides a camera's video everywhere, in addition to the camera moving to its privacy preset. Hold the Privacy join high (or call `SetPrivacy` with `true`) and, with `privacyClearsStream` (the default):

* **The preview URL sent to SIMPL is cleared**, so touch panel previews of this camera stop.
* **Every `axisD1110` decoder showing this camera is blanked.** The decoder remembers the stream it was asked to show, and shows it again when privacy ends. Selecting a stream whose camera is in privacy keeps the output blank until privacy ends.

Moving the camera to its privacy position is not done here. Whatever recalls the preset (the main program's privacy logic recalls a preset through the preset joins) keeps doing it, so the join and the preset recall do not conflict and the order they arrive in does not matter. The camera's controls are not blocked by this plugin.

What this cannot do: anything that pulls the camera's RTSP stream directly (a recorder, for example) is not affected. Only the video this plugin sends and the decoders it controls are.

### How the camera answers

VAPIX answers a PTZ command with `204 No Content` when it worked. A failed command is HTTP `200` with a body that starts with `Error:`. The plugin treats both the HTTP status and that body as the result. Saving a preset is documented under `ptzconfig.cgi` (administrator access). The old SIMPL camera program used `ptz.cgi`, which older firmware accepts, so the plugin tries `ptzconfig.cgi` first and falls back to `ptz.cgi`.

## Configuration

```json
{
  "key": "cam-left",
  "name": "Left PTZ",
  "type": "axisCamera",
  "group": "camera",
  "properties": {
    "host": "192.168.10.241",
    "username": "admin",
    "password": "password",
    "presetMode": "server",
    "presetCount": 8,
    "presets": { "1": "Home", "2": "Podium" }
  }
}
```

| Property | Default | Notes |
|---|---|---|
| `host` | required, unless `connectionSource` is `bridge` | Camera IP or hostname |
| `port` | 80 (443 with `useHttps`) | |
| `useHttps` | false | |
| `ignoreCertificateErrors` | false | Accept a self signed certificate when using HTTPS |
| `username`, `password` | | Camera login. Saving presets needs an administrator |
| `authMode` | `digest` | `digest` answers whichever challenge the camera sends (Digest or Basic). `basic` sends a Basic header on every request |
| `requestTimeoutMs` | 5000 | |
| `panSpeed`, `tiltSpeed` | 50 | 1 to 100 |
| `zoomSpeed` | 75 | 1 to 100 |
| `focusSpeed`, `irisSpeed` | 50 | 1 to 100 |
| `presetMode` | `server` | `server` (named presets) or `device` (numbered) |
| `presetCount` | 8 | 1 to 100 |
| `presets` | | Names for the presets, keyed by number. Others are "Preset N" |
| `pollTimeMs` | 30000 | A negative value polls once at startup only |
| `connectionSource` | `config` | `bridge`: the address, port and login come from SIMPL on serial joins, and the properties above are the fallback for any that are empty |
| `streamPath` | `/axis-media/media.amp` | Path of the camera's main stream. Used for the preview URL sent to SIMPL and by decoder streams that name this camera |
| `previewResolution` | `800x450` | Resolution in the preview URL sent to SIMPL. Decoders ask for their own resolution, so this only sets what touch panel previews pull |
| `privacyClearsStream` | true | When privacy is on for this camera, clear its preview URL and blank the decoders showing it. `false` makes privacy only a reported state |
| `joinLayout` | `contiguous` | `indexed`: see [Indexed layout](#indexed-layout-many-cameras-on-one-eisc) |
| `cameraIndex` | | Which camera this is (1 or higher) with the indexed layout |

---

# axisD1110

## What you can control

The D1110 Decoder API has one job: it tells the decoder which streams to decode and where to draw them. The plugin uses it to show one stream full screen.

| You want to | How | Notes |
|---|---|---|
| Show a camera | Send the stream number (1 to 16) to the analog join, or call `SelectStream` | The decoder connects to the camera and replaces what it was showing |
| Show a camera by name | `SelectStreamByName` (DEVJSON) | |
| Blank the output | Pulse the Clear digital join, send 0 to the analog join, or call `Clear` | Sends an empty view configuration. Not yet confirmed on a real D1110 that this blanks the HDMI output |
| Start on a stream | `defaultStream` in the config | Applied once, the first time the decoder comes online. 0 leaves the decoder as it is |
| Check it is reachable | Online feedback, `Poll`, the Refresh digital join | A `getViewConfiguration` request every `pollTimeMs` |
| Probe the decoder | `SendRequest` (DEVJSON) with any Decoder API method | For example `getCapabilities`. The answer is logged |

### What you get back

* **Online**: the decoder answered the last request.
* **Selected stream** (number and name): the last stream the plugin sent. The decoder does not report which stream it is showing, so this is what the plugin asked for, not a reading from the decoder.
* **Configuration id**: the decoder view configuration id. If it changes without the plugin causing it (someone used the decoder's web page), the selected stream is cleared to 0 and a warning is logged.
* **Last error code and message**: the first error the decoder returned, or an HTTP or connection failure. A stream that fails to connect (code 400) is reported here even though the configuration was applied.

### What it does not do

* **PTZ.** Use the `axisCamera` device for that.
* **Split screens and tours.** The Decoder API can show several panes and rotate through views. The plugin always sends one full screen pane showing one stream.
* **Audio control.** `audioCodec` is passed to the decoder with each stream. There is no mute or volume.
* **Decoder settings.** Network settings, firmware, users and the like.

## Where the streams come from

Each stream is an RTSP URL, supplied one of three ways:

**A camera (`cameraKey`)**, the usual choice. The stream is an `axisCamera` device. The URL is `rtsp://<camera address>` plus `cameraStreamPath` (default `/axis-media/media.amp`), and the login is the camera's. When the camera gets its address from SIMPL, so does the stream, and the stream is called what the camera is called.

**A URL in the configuration (`"streamSource": "config"`, the default).** Write the `url` in each entry of `streams`.

**A URL from SIMPL (`"streamSource": "bridge"`).** SIMPL sends the URL of each stream on the stream URL serial joins (31 to 46, from `joinStart`). The `streams` entries are optional and only give names and per stream settings.

Whichever way the URL arrives, the plugin takes it apart before it sends anything to the decoder:

* **Login:** the camera login is optional. If the cameras allow anonymous RTSP viewing, set nothing. A `rtsp://user:password@host/path` URL has its login moved into the decoder's own username and password fields. A URL without a login uses the camera's, then `streamUsername` and `streamPassword`. A stream entry's own `username` and `password` override all of them. The URL sent to the decoder and written to the log never contains the password.
* **Resolution:** if `resolution` is set (for example `"1920x1080"`), it replaces the `resolution=` value in the URL query, or is added if the URL has none. A stream entry's own `resolution` wins. This is how a small preview URL (for example `resolution=800x450`) becomes a full resolution stream for a display. Leave it empty to use the URL as received.

```
URL           rtsp://192.168.10.240/axis-media/media.amp?resolution=800x450
resolution    1920x1080
decoder gets  rtsp://192.168.10.240/axis-media/media.amp?resolution=1920x1080   login streamUsername / streamPassword, if set
```

If a stream's URL changes while it is on screen, the plugin sends the new one to the decoder. Selecting a stream that has no URL yet logs a warning and sets the error text.

## Configuration

```json
{
  "key": "d1110-1",
  "name": "D1110 #1",
  "type": "axisD1110",
  "group": "decoder",
  "properties": {
    "host": "192.168.10.60",
    "username": "root",
    "password": "password",
    "resolution": "1920x1080",
    "streams": {
      "1": { "cameraKey": "cam-left" },
      "2": { "cameraKey": "cam-right" },
      "3": { "cameraKey": "cam-monopod" }
    }
  }
}
```

| Property | Default | Notes |
|---|---|---|
| `host` | required | Decoder IP or hostname |
| `port`, `useHttps`, `ignoreCertificateErrors`, `authMode`, `requestTimeoutMs` | | As for the camera |
| `username`, `password` | | Decoder login, Operator role or higher |
| `pollTimeMs` | 30000 | Poll with `getViewConfiguration`. A negative value polls once at startup only |
| `defaultStream` | 0 | Stream selected the first time the decoder comes online. 0 leaves the decoder as it is |
| `streamSource` | `config` | `config` or `bridge`, for streams that have no `cameraKey` |
| `resolution` | | Resolution to request, for example `1920x1080`. Empty leaves the URL as received |
| `cameraStreamPath` | `/axis-media/media.amp` | Path of the stream on a camera, for streams with a `cameraKey` |
| `streamUsername`, `streamPassword` | | Camera login when nothing else supplies one |
| `joinLayout` | `contiguous` | `indexed`: up to 8 decoders share one small EISC, see [Indexed layout (decoders)](#indexed-layout-decoders-on-one-small-eisc) |
| `decoderIndex` | | Which decoder this is (1 to 8) with the indexed layout |
| `selectOnlyConfiguredStreams` | false | When true, the select stream join only acts on numbers that are configured streams, and ignores everything else including 0 (use the clear join to blank). Use it when the join is driven by a display's selected source: the camera buttons are sources 1 to 3 and the other sources are not streams |
| `streams` | | Keyed 1 to 16. Each has `cameraKey` or `url`, optional `name`, `resolution`, `videoCodec` (H264 default, MJPEG, MPEG4), `audioCodec` (default none), and `username` / `password` which override any other login |

The stream number is what SIMPL sends, so keep the same numbers on every decoder that serves the same kind of camera (for example 1 = Left PTZ, 2 = Right PTZ, 3 = Monopod).

---

# Bridge joins

Add each device to an EISC and give it a `joinStart`:

```json
{
  "key": "eisc-axis",
  "type": "eiscApiAdvanced",
  "group": "api",
  "properties": {
    "control": { "tcpSshProperties": { "address": "127.0.0.2", "port": 0 }, "ipid": "F2" },
    "devices": [
      { "deviceKey": "cam-left", "joinStart": 1 },
      { "deviceKey": "cam-right", "joinStart": 51 },
      { "deviceKey": "d1110-1", "joinStart": 151 }
    ]
  }
}
```

Joins are offsets from `joinStart` (so the analog join of `cam-right` above that saves a preset is analog 51). Leave at least 50 joins between devices.

## axisCamera joins

| Join | Type | Direction | Description |
|---|---|---|---|
| 1 | Digital | From SIMPL | Tilt up while held |
| 2 | Digital | From SIMPL | Tilt down while held |
| 3 | Digital | From SIMPL | Pan left while held |
| 4 | Digital | From SIMPL | Pan right while held |
| 5 | Digital | From SIMPL | Up and right while held |
| 6 | Digital | From SIMPL | Down and right while held |
| 7 | Digital | From SIMPL | Up and left while held |
| 8 | Digital | From SIMPL | Down and left while held |
| 9 | Digital | From SIMPL | Zoom in while held |
| 10 | Digital | From SIMPL | Zoom out while held |
| 11 | Digital | From SIMPL | Focus far (positive speed) while held |
| 12 | Digital | From SIMPL | Focus near (negative speed) while held |
| 13 | Digital | To/From SIMPL | Press to toggle autofocus. Feedback is the autofocus state |
| 14 | Digital | From SIMPL | Open the iris while held |
| 15 | Digital | From SIMPL | Close the iris while held |
| 16 | Digital | To/From SIMPL | Press to toggle auto iris. Feedback is the auto iris state |
| 17 to 24 | Digital | From SIMPL | Press to recall preset 1 to 8 |
| 25 | Digital | To SIMPL | High for a second after a preset was saved |
| 26 | Digital | From SIMPL | Press to restart the camera |
| 27 | Digital | From SIMPL | Press to move to the home position |
| 28 | Digital | To SIMPL | Camera online |
| 29 | Digital | To/From SIMPL | Privacy. Held high while the camera is in privacy. Feedback is the state |
| 1 | Analog | From SIMPL | Set to a preset number to save the current position as that preset |
| 1 to 4 | Serial | From SIMPL | Address, port, username, password. Used when `connectionSource` is `bridge` |
| 5 | Serial | To SIMPL | Camera name (the device name) |
| 6 | Serial | To SIMPL | Last error |
| 7 | Serial | To SIMPL | Preview URL: `rtsp://<host><streamPath>?resolution=<previewResolution>`, empty until the camera has an address |

### Indexed layout: many cameras on one EISC

With `"joinLayout": "indexed"` and a `cameraIndex`, many cameras share one EISC and are told apart by their number `n` (give every camera `joinStart` 1):

* **Digital joins:** the table above, plus 30 for every camera before this one (camera `n` starts at `(n-1) × 30 + 1`).
* **Analog:** preset save is analog join `n`.
* **Serial from SIMPL** (only when `connectionSource` is `bridge`): address `n`, port `50 + n`, username `100 + n`, password `150 + n`.
* **Serial to SIMPL:** camera name `n`, preview URL `50 + n`, last error `100 + n`. Serial joins to and from SIMPL are separate, so the numbers can repeat.

Camera 3, for example: tilt up is digital 61, preset 1 recall is digital 77, the restart join is digital 86, preset save is analog 3, and the address is serial 3.

This is the layout the old SIMPL camera program used for its 41 cameras, so a SIMPL program that talked to it can talk to this plugin: give the EISC the same IPID and the PTZ, preset and feedback joins are where they were.

**Where the camera data lives.** Configure each camera once, in Essentials: address, login and name. SIMPL is sent only what it needs, the camera name and the preview URL. That removes the camera list (and the camera URLs and logins) from the SIMPL configuration files. `"connectionSource": "bridge"` still exists for systems that would rather keep the address and login in SIMPL, and is not needed for this layout.

## axisD1110 joins

| Join | Type | Direction | Description |
|---|---|---|---|
| 1 | Digital | To SIMPL | Decoder online |
| 2 | Digital | From SIMPL | Pulse to blank the output |
| 3 | Digital | From SIMPL | Pulse to poll now |
| 1 | Analog | To/From SIMPL | Stream to show (1 to 16, 0 blanks) and selected stream feedback |
| 2 | Analog | To SIMPL | Last Decoder API error code (0 none) |
| 1 | Serial | To SIMPL | Device name |
| 2 | Serial | To SIMPL | Selected stream name |
| 3 | Serial | To SIMPL | Last error message |
| 4 | Serial | To SIMPL | Decoder configuration id |
| 11 to 26 | Serial | To SIMPL | Stream names, in stream number order |
| 31 to 46 | Serial | From SIMPL | Stream RTSP URLs. Used when `streamSource` is `bridge` |

### Indexed layout: decoders on one small EISC

With `"joinLayout": "indexed"` and a `decoderIndex` from 1 to 8, up to eight decoders share one EISC of 32 joins of each type, and are told apart by their number `n` (give every decoder `joinStart` 1):

| Join | Type | Direction | Description |
|---|---|---|---|
| `n` | Digital | To SIMPL | Decoder online |
| `8 + n` | Digital | From SIMPL | Pulse to blank the output |
| `16 + n` | Digital | From SIMPL | Pulse to poll now |
| `n` | Analog | To/From SIMPL | Stream to show (1 to 16, 0 blanks) and selected stream feedback |
| `8 + n` | Analog | To SIMPL | Last Decoder API error code |
| `n` | Serial | To SIMPL | Device name |
| `8 + n` | Serial | To SIMPL | Selected stream name |
| `16 + n` | Serial | To SIMPL | Last error message |
| `24 + n` | Serial | To SIMPL | Decoder configuration id |

The stream names and stream URL joins do not exist in the indexed layout, so use streams that name a camera (`cameraKey`) or a URL in the configuration, not `streamSource` `bridge`.

---

# Troubleshooting and testing from the console

Set the plugin log level to Debug to see every request and response (for example `appdebug:1 debug` on the processor. See the console help of your Essentials version). Information level shows what the devices are doing: configuration at startup, presets, online and offline changes. Passwords are masked in the log.

| You see | Likely cause |
|---|---|
| `poll failed: ...` and never goes online | The device is not reachable from the processor (address, VLAN, firewall) or the port is wrong |
| `HTTP 401` | Wrong username or password, or the device rejects the `authMode`. Try the other mode |
| A camera moves but a preset will not save | The login is not an administrator. Saving needs `ptzconfig.cgi` or `ptz.cgi` with administrator access |
| `Cannot send ...: there is no camera address yet` | `connectionSource` is `bridge` and SIMPL has not sent the address yet. Check the serial joins |
| Iris commands fail with an `Error:` message | The camera does not support them. Check `ptz.cgi?info=1` |
| Decoder: selecting works but the screen stays black, error `400` | The decoder cannot connect to the camera URL. Check the camera address, codec and login |
| Decoder: error `402` | The decoder has no resources for that stream (codec or resolution too high) |
| `Stream N cannot be used: camera cam-x was not found` | The stream's `cameraKey` is not an `axisCamera` device |
| `Stream N cannot be used: camera cam-x has no address yet` | The camera gets its address from SIMPL and it has not arrived |
| `Stream N cannot be used: no URL has been received from the bridge` | `streamSource` is `bridge` and SIMPL has not sent a URL for that stream |
| `Decoder configuration changed outside this plugin` | Someone used the decoder's own interface. Select a stream again |

DEVJSON commands (`devjson:1 <json>`) that call a device directly:

```
{"deviceKey":"cam-left","methodName":"PrintStatus","params":[]}
{"deviceKey":"cam-left","methodName":"PositionHome","params":[]}
{"deviceKey":"cam-left","methodName":"PresetSelect","params":[2]}
{"deviceKey":"cam-left","methodName":"PresetStore","params":[2]}
{"deviceKey":"cam-left","methodName":"ToggleFocusMode","params":[]}
{"deviceKey":"cam-left","methodName":"Poll","params":[]}
{"deviceKey":"cam-left","methodName":"SendRequest","params":["/axis-cgi/com/ptz.cgi?info=1"]}
{"deviceKey":"d1110-1","methodName":"PrintStatus","params":[]}
{"deviceKey":"d1110-1","methodName":"SelectStream","params":[1]}
{"deviceKey":"d1110-1","methodName":"SelectStreamByName","params":["Left PTZ"]}
{"deviceKey":"d1110-1","methodName":"Clear","params":[]}
{"deviceKey":"d1110-1","methodName":"SendRequest","params":["getCapabilities",""]}
```

`SendRequest` on a camera sends any path and query to the camera and logs the answer. On a decoder it sends any Decoder API method with the params object you give it as JSON text. Use them on the bench to see what a real device supports.

Unit tests (`tests/`) cover the VAPIX requests and answers, the join layouts, the URL handling, the HTTP client against a local fake Axis device, and every example configuration: `dotnet test`. The device classes themselves need the Crestron runtime and are tested on a processor.

## How it works

* **Camera:** continuous moves start when a join goes high and stop when it goes low. Requests are queued and sent one at a time in the order they were made, so a stop never overtakes the move it belongs to.
* **Decoder:** the Decoder API has no "select input" command. Selecting a stream sends a complete view configuration (one full screen pane showing one stream), which replaces what the decoder was showing. The decoder keeps its configuration across a restart. Commands are queued, so a poll never overlaps a select.

## Upgrading from the 1.0.0-rc plugin

Version 3 is a rewrite and is not compatible with the 1.0.0-rc releases of this repository (a .NET 3.5 plugin for Essentials 1.x). It needs Essentials 3.0.0 or later on a CP4N. The device type is still `axisCamera`, but:

* **Configuration:** `host` replaces `hostname`, `presets` is an object keyed by preset number (not a list), and the `communicationMonitor` block is gone (use `pollTimeMs`).
* **Joins:** the join layout is new (see [Bridge joins](#bridge-joins)). Programs written for the old plugin must be rewired.
* **Presets:** saving a preset works. In the old plugin it never did.
* **Removed:** the power and privacy joins, which the old plugin never implemented.

## Not yet verified on hardware

* Which VAPIX PTZ commands the M5526-E accepts (iris and auto iris in particular), and whether saving presets works through `ptzconfig.cgi` on its firmware.
* That the D1110 and the cameras accept the Decoder API in the way described here.
* Digest versus Basic authentication on the firmware in use.
* That blanking a decoder (stream 0, an empty view configuration) clears the HDMI output.
* How long a switch takes and whether the HDMI signal drops while it happens.

## Dependencies

The [Essentials](https://github.com/PepperDash/Essentials) libraries are required (v3.0.0 or later, .NET 8). They are referenced through NuGet and restored when the project builds.

## Git Hooks (Husky.Net)

This repo uses [Husky.Net](https://alirezanet.github.io/Husky.Net/) to run a `pre-commit` hook. The hook runs `.github/scripts/Update-ReadmeBadges.ps1`, which keeps the `.NET` and `PepperDash Essentials` badges at the top of this README in sync with the project:

* `.NET` badge - `TargetFramework` in `src/*.4Series.csproj`
* `PepperDash Essentials` badge - the highest `MinimumEssentialsFrameworkVersion` in `src/*Factory.cs`

If the badges change, `README.md` is re-staged automatically so the update is part of your commit. A warning is printed if the `PepperDashEssentials` package version in the csproj differs from `MinimumEssentialsFrameworkVersion`.

### Requirements

* [.NET SDK](https://dotnet.microsoft.com/download) (provides `dotnet tool`)
* [PowerShell 7+ (`pwsh`)](https://learn.microsoft.com/powershell/scripting/install/installing-powershell) on the `PATH` (Windows, macOS and Linux). Windows PowerShell 5.1 is not used.

### Setup

The hook is installed automatically the first time you do any of the following in a fresh clone:

* Open the solution in Visual Studio, or run `dotnet restore` / `dotnet build`
* Open the folder in VS Code and allow the "Install git hooks" automatic task

To install it manually:

```
dotnet tool restore
dotnet husky install
```

Verify with `git config core.hooksPath`, which should print `.husky`.

### Usage

* Commit as usual; the hook runs on every `git commit`.
* Run it on demand: `dotnet husky run`
* Check without modifying files (useful for CI): `pwsh -NoProfile -File .github/scripts/Update-ReadmeBadges.ps1 -Check`
* The hook refuses to run if `README.md`, the `.4Series.csproj`, or a `*Factory.cs` file has unstaged or untracked changes, so a partial commit never picks up unrelated edits. Stage or stash them and commit again.
* Skip the hook for a single commit: `git commit --no-verify`
* Skip the automatic install on restore: set the `HUSKY=0` environment variable. It is also skipped when `CI=true`.

## Build Instructions (PepperDash Internal) 

## Generating Nuget Package

A nuget package is automatically generated when the plugin is build. To modify the name and other details of the package, edit the following properties in the .csproj file:

1. `PackageId` - This is the name that will be used to pull the package from Nuget once it's published
2. `PackageProjectUrl` - This should match the URL for the plugin repo
3. `AssemblyTitle` - This is the dll file name that is will show on a processor when the plugin is loaded