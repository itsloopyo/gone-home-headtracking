# Gone Home Head Tracking

![Mod GIF](assets/readme-clip.gif)

An **unofficial** head tracking mod for Gone Home with decoupled look + aim support. Enables natural head movement using OpenTrack-compatible trackers while maintaining independent mouse aim.

## Features

- **Decoupled look + aim**: Look around freely with your head while mouse-controlled aim stays independent
- **6DOF head tracking**: Yaw, pitch, roll, and positional tracking (X/Y/Z) via OpenTrack UDP protocol

## Requirements

- Gone Home (Steam, GOG, or Epic)
- [OpenTrack](https://github.com/opentrack/opentrack) or an OpenTrack-compatible head tracking app (smartphone, webcam, or dedicated hardware)
- Windows 10/11

## Installation

1. Download the latest release from the [Releases page](https://github.com/itsloopyo/gone-home-headtracking/releases)
2. Extract the ZIP anywhere
3. Double-click `install.cmd`
4. Configure OpenTrack to output UDP to `127.0.0.1:4242`
5. Launch Gone Home

The installer automatically finds your game by checking Steam, GOG, and Epic installations. If it can't find the game, either:
- Set the `GONEHOME_PATH` environment variable to your game folder
- Run from command prompt: `install.cmd "D:\Games\Gone Home"`

## Controls

| Key | Action |
|-----|--------|
| **Home** | Recenter (set current head position as neutral) |
| **End** | Toggle head tracking on/off |

## Configuration

The mod creates `HeadTracking.cfg` in the game's `GoneHome_Data\Managed` folder with default settings on first run. Edit it to customize:

### General

| Setting | Default | Description |
|---------|---------|-------------|
| ShowReticle | true | Show/hide the custom crosshair |
| ReticleColor | 1.0,1.0,1.0,1.0 | RGBA color for the reticle |

### Keybindings

| Setting | Default | Description |
|---------|---------|-------------|
| RecenterKey | Home | Key to recenter head position |
| ToggleKey | End | Key to toggle tracking on/off |

### Network

| Setting | Default | Description |
|---------|---------|-------------|
| UdpPort | 4242 | UDP port for OpenTrack data |

### Sensitivity

| Setting | Default | Description |
|---------|---------|-------------|
| YawSensitivity | 1.0 | Horizontal rotation multiplier |
| PitchSensitivity | 1.0 | Vertical rotation multiplier |
| RollSensitivity | 1.0 | Head tilt multiplier |

### Smoothing

| Setting | Default | Description |
|---------|---------|-------------|
| Smoothing | 0.0 | Smoothing factor (0.0-1.0). Remote connections enforce a minimum of 0.15 automatically. |

### Position Tracking

| Setting | Default | Description |
|---------|---------|-------------|
| PositionSensitivityX | 1.0 | Lateral (left/right) position sensitivity multiplier |
| PositionSensitivityY | 1.0 | Vertical (up/down) position sensitivity multiplier |
| PositionSensitivityZ | 1.0 | Depth (forward/back) position sensitivity multiplier |
| InvertPositionX | true | Invert lateral axis |
| InvertPositionY | false | Invert vertical axis |
| InvertPositionZ | true | Invert depth axis |

## OpenTrack Setup

1. Install [OpenTrack](https://github.com/opentrack/opentrack) and configure any compatible tracker as input (smartphone apps, webcam-based tracking, dedicated hardware, etc.)
2. Set output to **UDP over network**
3. Configure remote IP: `127.0.0.1` and port: `4242`
4. Start tracking before launching the game

### Phone App Setup

This mod includes built-in smoothing to handle network jitter, so if your tracking app already provides a filtered signal, you can send directly from your phone to the mod on port 4242 without needing OpenTrack on PC.

1. Install an OpenTrack-compatible head tracking app from your phone's app store
2. Configure your phone app to send to your PC's IP address on port 4242 (run `ipconfig` to find it, e.g. `192.168.1.100`)
3. Set the protocol to OpenTrack/UDP
4. Start tracking

**With OpenTrack (optional):** If you experience jerky motion, want curve mapping, or want a visual preview, route through OpenTrack instead. The mod already listens on port 4242, so OpenTrack's input must use a different port:
1. In OpenTrack, set Input to **UDP over network** on port **5252** (or any port other than 4242)
2. Set Output to **UDP over network** at `127.0.0.1:4242`
3. In your phone app, send to your PC's IP on port **5252** (matching OpenTrack's input port)
4. Make sure port 5252 is open in your PC's firewall for incoming UDP traffic

## Verifying Installation

1. Start OpenTrack and enable tracking
2. Launch Gone Home
3. Once in-game, move your head - the camera should follow
4. Press **Home** to recenter if needed

Check the logs in `GoneHome_Data\Managed`:

**`HeadTracking_BOOT.log`** (bootstrap):
```
Loading HeadTracking.dll...
SUCCESS: ModLoader.Initialize() called
```

**`HeadTracking.log`** (mod):
```
[12:00:00.000] ModLoader.Initialize() called
[12:00:00.001] [Mod] Initializing Head Tracking v1.0.0...
[12:00:00.002] [Mod] Head Tracking loaded! Port: 4242, Toggle: End, Recenter: Home
```

## Troubleshooting

### Mod not loading

- Check `HeadTracking_BOOT.log` in the Managed folder
- Check `%TEMP%\HeadTracking_BOOT_ERROR.log` for errors
- Make sure all DLL files are in the Managed folder

### Camera not responding

1. Verify OpenTrack is running and tracking is active
2. Check UDP output is set to `127.0.0.1:4242`
3. Press **End** to make sure tracking is enabled
4. Press **Home** to recenter
5. Check firewall isn't blocking UDP port 4242

### Game crashes on startup

1. Run `uninstall.cmd` to restore original files
2. Verify game files through your launcher (Steam: Right-click > Properties > Local Files > Verify)
3. Try installing again

## Updating

1. Download the new release
2. Run `install.cmd` again - it will update the mod files

## Uninstallation

Run `uninstall.cmd` from the release folder. This restores the original `Assembly-CSharp.dll` from backup and removes all mod files.

You can also restore manually:
1. Delete from `GoneHome_Data\Managed`: `HeadTracking.dll`, `CameraUnlock.Core.dll`, `CameraUnlock.Core.Unity.dll`, `Mono.Cecil.dll`
2. Restore `Assembly-CSharp.dll`: rename `Assembly-CSharp.dll.original` back to `Assembly-CSharp.dll`, or verify game files through your launcher

## Building from Source

### Prerequisites

- [Pixi](https://pixi.sh) package manager
- .NET SDK 8.0+
- PowerShell 5.1+
- Gone Home installed (Unity DLLs are needed as build references)

### Build Steps

```bash
# Clone with submodules
git clone --recurse-submodules https://github.com/itsloopyo/gone-home-headtracking.git
cd gone-home-headtracking

# Copy required Unity DLLs from your game installation
pixi run setup-libs

# Build
pixi run build
```

### Available Commands

| Command | Description |
|---------|-------------|
| `pixi run setup-libs` | Copy Unity DLLs from game installation |
| `pixi run build` | Build the mod (Release configuration) |
| `pixi run install` | Build and install to game directory |
| `pixi run uninstall` | Remove the mod from the game |
| `pixi run package` | Create release ZIP |
| `pixi run clean` | Clean build artifacts |
| `pixi run release` | Version bump, build, tag, and push |

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Credits

- [The Fullbright Company](https://fullbright.company/) - Gone Home
- [OpenTrack](https://github.com/opentrack/opentrack) - Head tracking software (UDP protocol)
- [Mono.Cecil](https://github.com/jbevain/cecil) - .NET assembly manipulation library
