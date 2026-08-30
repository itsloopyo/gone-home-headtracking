# Gone Home Head Tracking

![Gone Home running with this mod](https://raw.githubusercontent.com/itsloopyo/gone-home-headtracking/main/assets/readme-clip.gif)

*Recorded in Gone Home with this mod running. Gone Home is the property of [The Fullbright Company](https://fullbright.company/); the footage is shown here to demonstrate the mod and is not covered by this project's licence.*

An unofficial head tracking mod for Gone Home that moves the view with your head while your mouse keeps control of the cursor, driven by OpenTrack over UDP, with no VR headset required.

## Features

- **Decoupled look and aim**: Your head moves the view; the mouse still controls the interaction cursor.
- **6DOF head tracking**: Yaw, pitch, roll, and positional tracking (X/Y/Z) over the OpenTrack UDP protocol.

## Requirements

- [Gone Home on Steam](https://store.steampowered.com/app/232430/Gone_Home/) (or the GOG / Epic edition).
- A head tracking source: [OpenTrack](https://github.com/opentrack/opentrack) with a webcam, a phone app that speaks OpenTrack UDP, or dedicated tracking hardware.
- Windows 10 or 11 (64-bit).

## Installation

1. Download the latest installer ZIP from the [Releases page](https://github.com/itsloopyo/gone-home-headtracking/releases).
2. Extract the ZIP anywhere.
3. Double-click `install.cmd`. The installer auto-detects Steam, GOG, and Epic copies of Gone Home.
4. Configure OpenTrack to send UDP to `127.0.0.1:4242` (see "Setting Up OpenTrack" below).
5. Launch Gone Home.

If the installer can't find your game, point it at the install folder explicitly:

- Set the environment variable `GONEHOME_PATH` to the game folder, or
- Run from a command prompt: `install.cmd "D:\Games\Gone Home"`.

## Manual Installation

For users who prefer to place files by hand (advanced).

This mod uses a Mono.Cecil bootstrap patcher: the mod DLLs are loaded by a small instruction injected into `Assembly-CSharp.dll`. There is no separate mod loader to install, but `Assembly-CSharp.dll` must be patched once.

1. Download the Nexus ZIP from the [Releases page](https://github.com/itsloopyo/gone-home-headtracking/releases) and extract it into your Gone Home install folder. This places `HeadTracking.dll`, `CameraUnlock.Core.dll` and `CameraUnlock.Core.Unity.dll` into `GoneHome_Data\Managed\`. `Mono.Cecil.dll` is only needed by the patcher and ships in the installer ZIP.
2. Patch `Assembly-CSharp.dll` by running `install.cmd` from the installer ZIP with your game path:
   ```
   install.cmd "C:\Path\To\Gone Home"
   ```
   The patcher backs up the original as `Assembly-CSharp.dll.original` before modifying it.

## Setting Up OpenTrack

The mod listens for OpenTrack pose data on UDP port `4242`, on every network
interface. One datagram is six little-endian 64-bit floats in the order
`x, y, z, yaw, pitch, roll`: position in centimetres, rotation in degrees, 48
bytes in total. Anything that sends that to that port drives the view.
OpenTrack's **UDP over network** output sends exactly this, and the steps below
set it up.

1. Install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Pick a tracker under **Input**, using the notes below.
3. Set **Output** to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Press **Start**. Tracking and the game can start in either order.

### Webcam

OpenTrack ships a `neuralnet tracker` input that reads a plain webcam. Select it
under **Input**, pick your camera in its settings, and use the output settings
above. How well it tracks depends on your camera and your lighting, so try it
before buying anything.

### Phone

A phone app can reach the mod directly, with no OpenTrack on the PC, if it sends
the datagram described above. Point it at this PC's IP address (run `ipconfig`
to find it) on port `4242`. Not every phone tracker speaks this protocol, so
check yours for an OpenTrack or UDP output option first. [Headcam](https://headcam.app)
sends it, and I wrote it so decent tracking is free for anyone who already owns
a phone.

Sending direct works when the app filters its own signal on the device. The
mod's smoothing is sized to take the edge off a clean signal rather than to
rescue a noisy one, so a raw feed sent direct will jitter. If it does, point the
app at OpenTrack's **UDP over network** *input* on some other port, say 5252,
and let OpenTrack's filters and curves clean it up before its output forwards to
`127.0.0.1:4242`.

Anything arriving from outside `127.0.0.0/8` counts as a remote connection and
is smoothed with `RemoteSmoothing` rather than `LocalSmoothing`. That includes a
tracker on this very PC that sends to the machine's own LAN address, because the
mod reads the source address and not the machine.

### Headset or other hardware

If your device has an OpenTrack input driver, select it under **Input** and use
the same output settings. OpenTrack's own **Input** list is the authority on
what it can read; the mod only ever sees what OpenTrack sends.

### Centring

Centring belongs to your tracker. The mod subtracts no centre of its own: it
applies the pose it receives exactly as it arrives, so a stream of zeros holds
the view where the game itself puts it. Press the centre control in your tracker
(OpenTrack's **Center** bind, or the CENTER button in Headcam) and the tracker
zeroes its own output, which leaves the view centred with the mod doing nothing.

That is why there is no centre hotkey here and nothing to re-centre in game. Two
centres in series would drift apart, because each side re-centres at moments the
other cannot see, and you would end up pressing twice to centre once. If the
view sits off to one side, centre it in the tracker.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Toggle tracking     | `End`       | `Ctrl+Shift+Y`  |
| Cycle tracking mode | `Page Up`   | `Ctrl+Shift+G`  |
| Toggle yaw mode     | `Page Down` | `Ctrl+Shift+H`  |

`Page Up` / `Ctrl+Shift+G` cycles tracking mode:

1. Normal head-tracked gameplay
2. Positional tracking disabled, rotational tracking enabled
3. Rotational tracking disabled, positional tracking enabled
4. Back to normal

`Page Down` / `Ctrl+Shift+H` toggles yaw mode between horizon-locked world-space yaw (default; head yaw stays horizontal even when the camera is pitched, but produces a camera arc at extreme head yaw + mouse pitch combinations) and camera-local yaw (head yaw rotates around the camera's current up axis - matches the dying-light-2 and obra-dinn defaults).

The nav-cluster keys are configurable in `HeadTracking.cfg`; the chord set is fixed.

## Configuration

The mod creates `HeadTracking.cfg` in `GoneHome_Data\Managed\` on first run. Edit it and restart the game to apply changes.

```ini
# Network
UdpPort = 4242

# Keybindings (Unity KeyCode names)
# See https://docs.unity3d.com/ScriptReference/KeyCode.html
ToggleKey = End
PositionToggleKey = PageUp
YawModeKey = PageDown

# Yaw mode: true = horizon-locked world-space (default), false = camera-local
WorldSpaceYaw = true

# Sensitivity (multipliers). Not clamped; 0.1-5.0 is the useful range.
YawSensitivity = 1.0
PitchSensitivity = 1.0
RollSensitivity = 1.0

# Smoothing (0.0-1.0). Picked per connection from the tracker's source address,
# and both values cover rotation and position.
# Tracker running on this machine (loopback)
LocalSmoothing = 0.0
# Tracker on a remote device over the network
RemoteSmoothing = 0.15

# Position tracking
PositionSensitivityX = 1.0
PositionSensitivityY = 1.0
PositionSensitivityZ = 1.0
InvertPositionX = true
InvertPositionY = false
InvertTrackerZ = false

# Reticle (R,G,B,A in 0.0-1.0)
ShowReticle = true
ReticleColor = 1.0,1.0,1.0,1.0
```

## Troubleshooting

**Where the logs are:**
- `GoneHome_Data\Managed\HeadTracking.log` is the main mod log. It records the
  port it listened on, whether the camera hook attached, and an `OpenTrack
  connected` line the moment the first tracker packet arrives. Send this file
  when reporting a problem.
- `GoneHome_Data\Managed\HeadTracking_BOOT.log` records whether the patched
  game assembly loaded the mod at all.
- `%TEMP%\HeadTracking_BOOT_ERROR.log` records patch or load failures.

All three are rewritten from scratch on every game launch, so they only ever
contain the most recent session.

**Mod not loading:**
- Check `HeadTracking_BOOT.log` in `GoneHome_Data\Managed\`.
- Check `%TEMP%\HeadTracking_BOOT_ERROR.log` for patch or load errors.
- Confirm all four DLLs are present in `GoneHome_Data\Managed\`: `HeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`, `Mono.Cecil.dll`.

**No tracking response:**
- Verify OpenTrack (or your phone app) is running and outputting UDP.
- Confirm the destination port is `4242` and the host is `127.0.0.1` (or your PC's LAN IP if sending from a phone).
- Press `End` (or `Ctrl+Shift+Y`) to toggle tracking back on.

**View sits off to one side:**
- Centre it in your tracker app: OpenTrack's **Center** bind, or the centre button in your phone app. The mod applies what the tracker sends as-is and has no centre of its own.

**Jittery or unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `HeadTracking.cfg` toward `0.3`-`0.5`.
- For phone trackers over Wi-Fi, prefer wired USB tethering or a 5 GHz network.
- Tune the OpenTrack filter (Accela or similar) if you are routing through OpenTrack.

**Wrong rotation axis or inverted motion:**
- Flip `InvertPositionX`, `InvertPositionY`, or `InvertTrackerZ` in `HeadTracking.cfg`.
- For inverted yaw or pitch, use OpenTrack's per-axis "Invert" switches in the Output mapping.

**Yaw feels wrong when looking up or down at extreme angles:**
- Try toggling between world-locked and camera-local yaw with `Page Down` (or `Ctrl+Shift+H`). World-locked (default) is horizon-stable; camera-local follows the camera's current up-axis.

**Game crashes on startup:**
1. Run `uninstall.cmd` to restore the original `Assembly-CSharp.dll`.
2. Verify the game files through your launcher (Steam: Right-click > Properties > Local Files > Verify).
3. Try installing again.

## Updating

Download the new release and run `install.cmd` again. Your `HeadTracking.cfg` is preserved.

## Uninstalling

Run `uninstall.cmd`. This removes the mod DLLs and restores the original `Assembly-CSharp.dll` from the backup created at install time.

Because Gone Home has no separate mod loader, there is nothing additional to remove. The `/force` flag is accepted for parity with other CameraUnlock mods but is a no-op here.

## Building from Source

### Prerequisites

- [Pixi](https://pixi.sh) package manager
- .NET SDK 8.0 or newer
- Gone Home installed locally (Unity DLLs are needed as build references)

### Build

```bash
git clone --recurse-submodules https://github.com/itsloopyo/gone-home-headtracking.git
cd gone-home-headtracking
pixi run setup-libs    # copy Unity DLLs from your game install
pixi run build
pixi run install       # build and install to the game directory
```

Other tasks: `pixi run uninstall`, `pixi run package`, `pixi run clean`, `pixi run release`.

## Community & Support

- Discord: [Loop's Head Tracking Hangout](https://discord.com/invite/dxyZdyFNT9) - setup help, bug reports, and new-release announcements
- [Lopari](https://lopari.app) - free Windows launcher with one-click install and launch for the released head-tracking mods
- [Headcam](https://headcam.app) - free app that turns your iPhone or Android phone into the head tracker

## License

MIT License. See [LICENSE](LICENSE) for details.

The MIT licence covers the code, scripts and documentation in this repository. It does not extend to the Gone Home footage in `assets/`, or to any trademark. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Credits

- [The Fullbright Company](https://fullbright.company/) for Gone Home.
- [OpenTrack](https://github.com/opentrack/opentrack) for the head tracking protocol and tooling.
- [Mono.Cecil](https://github.com/jbevain/cecil) for runtime assembly patching.

## Disclaimer

This is an unofficial, fan-made modification. It is not affiliated with,
endorsed by, or sponsored by The Fullbright Company or any other rights holder,
and it requires a legitimately purchased copy of Gone Home. It contains no game
code, no extracted game assets and no game data files, aside from the demo clip
above: the mod is loaded by patching a copy of
`Assembly-CSharp.dll` on your own machine at install time, and the original is
kept as `Assembly-CSharp.dll.original` so `uninstall.cmd` can put it back.
Product names and trademarks are used only to identify the game this mod
applies to. If you hold rights in anything shown here and would rather it were
not published, open an issue or reach us on Discord and it comes down.
