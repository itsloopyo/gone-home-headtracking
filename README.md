# Gone Home Head Tracking

An unofficial head tracking mod for Gone Home that lets you look around the Greenbriar house with your head while keeping the mouse free for cursor control and interaction, no VR headset required.

![Mod GIF](https://raw.githubusercontent.com/itsloopyo/gone-home-headtracking/main/assets/readme-clip.gif)

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

1. Download the Nexus ZIP from the [Releases page](https://github.com/itsloopyo/gone-home-headtracking/releases) and extract it into your Gone Home install folder. This places `HeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`, and `Mono.Cecil.dll` into `GoneHome_Data\Managed\`.
2. Patch `Assembly-CSharp.dll` by running `install.cmd` from the installer ZIP with your game path:
   ```
   install.cmd "C:\Path\To\Gone Home"
   ```
   The patcher backs up the original as `Assembly-CSharp.dll.original` before modifying it.

## Setting Up OpenTrack

1. Download and install [OpenTrack](https://github.com/opentrack/opentrack/releases).
2. Configure your tracker (see Webcam or Phone App below).
3. Set Output to **UDP over network**, host `127.0.0.1`, port `4242`.
4. Click **Start** to begin tracking.

### Webcam Setup

1. In OpenTrack, set Input to **neuralnet tracker**.
2. Position the webcam roughly at face height.
3. Tune the neuralnet tracker's smoothing and deadzone in OpenTrack's filter settings if the signal is noisy.

### Phone App Setup

If your phone tracking app already smooths its output, you can send directly to the mod on UDP `4242` without running OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app from your phone's app store.
2. Find your PC's local IP with `ipconfig` (look for something like `192.168.1.100`).
3. Configure the phone app to send to that IP on port `4242` using the OpenTrack/UDP protocol.
4. Start tracking in the app.

**Optional OpenTrack relay** (for curve mapping or extra smoothing):

1. In OpenTrack, set Input to **UDP over network** on port `5252` (any port other than 4242).
2. Set Output to **UDP over network** at `127.0.0.1:4242`.
3. In the phone app, send to your PC's IP on port `5252`.
4. Allow inbound UDP on port `5252` in the Windows firewall.

## Controls

Two equivalent binding sets - use whichever your keyboard has:

| Action              | Nav-cluster | Chord           |
|---------------------|-------------|-----------------|
| Recenter            | `Home`      | `Ctrl+Shift+T`  |
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
RecenterKey = Home
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
InvertPositionZ = true

# Reticle (R,G,B,A in 0.0-1.0)
ShowReticle = true
ReticleColor = 1.0,1.0,1.0,1.0
```

## Troubleshooting

**Mod not loading:**
- Check `HeadTracking_BOOT.log` in `GoneHome_Data\Managed\`.
- Check `%TEMP%\HeadTracking_BOOT_ERROR.log` for patch or load errors.
- Confirm all four DLLs are present in `GoneHome_Data\Managed\`: `HeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`, `Mono.Cecil.dll`.

**No tracking response:**
- Verify OpenTrack (or your phone app) is running and outputting UDP.
- Confirm the destination port is `4242` and the host is `127.0.0.1` (or your PC's LAN IP if sending from a phone).
- Press `End` (or `Ctrl+Shift+Y`) to toggle tracking back on.
- Press `Home` (or `Ctrl+Shift+T`) to recenter.

**Jittery or unstable tracking:**
- Raise `RemoteSmoothing` (phone/network tracker) or `LocalSmoothing` (tracker on this PC) in `HeadTracking.cfg` toward `0.3`-`0.5`.
- For phone trackers over Wi-Fi, prefer wired USB tethering or a 5 GHz network.
- Tune the OpenTrack filter (Accela or similar) if you are routing through OpenTrack.

**Wrong rotation axis or inverted motion:**
- Flip `InvertPositionX`, `InvertPositionY`, or `InvertPositionZ` in `HeadTracking.cfg`.
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

## Credits

- [The Fullbright Company](https://fullbright.company/) for Gone Home.
- [OpenTrack](https://github.com/opentrack/opentrack) for the head tracking protocol and tooling.
- [Mono.Cecil](https://github.com/jbevain/cecil) for runtime assembly patching.
