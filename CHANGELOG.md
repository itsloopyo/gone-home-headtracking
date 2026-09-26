# Changelog

## [Unreleased]

### Changed

- Settings move to `GoneHome_Data\Managed\CameraUnlock.ini`. Earlier versions of the mod kept these settings in `HeadTracking.cfg`, in the same folder. The first time this version starts and finds no `CameraUnlock.ini`, it reads your settings from `HeadTracking.cfg` and writes them into `CameraUnlock.ini`. It never changes `HeadTracking.cfg`, and does not read it again while `CameraUnlock.ini` exists.
- A setting that the defaults the README shows set to `default` is written as `default` when the value imported for it equals its default at that start, which is the value `Defaults.ini` gives it, or the built-in value where `Defaults.ini` gives none. It then follows `Defaults.ini`. Every other setting is written with the value imported for it.
- `RotationEnabled` and `PositionEnabled` are one setting here, the tracking mode, so both are written as `default` or neither is.
- Comments, and keys the mod never read, are not carried over. Nor are these, where your old file had them:
  - A sensitivity, scale, deadzone, response curve or axis inversion you changed from its default. Set these in your tracker instead.
  - Reticle settings, and a key that toggled the reticle.
  - The setting for a feature that earlier versions shipped switched off while it was untested. It now follows the mod's default.
- An older version of the mod reads `HeadTracking.cfg` and never reads `CameraUnlock.ini`, so a setting you change after updating is not in `HeadTracking.cfg`.
- Deleting only `CameraUnlock.ini` makes the next start read `HeadTracking.cfg` again. To go back to the defaults, replace everything in `CameraUnlock.ini` with the defaults the README shows. Every setting they set to `default` then follows `Defaults.ini`.
- Hotkeys are written as key names, and each hotkey lists every key that triggers it, the Ctrl+Shift chord included: `ToggleKey=End, Ctrl+Shift+Y`.
- A hotkey bound to a plain key no longer fires while Ctrl and Shift are both held, so Ctrl+Shift with that key reaches only a binding that names the chord.
- On Linux and macOS without Wine or Proton, this version reads its settings and saves none: it creates no `CameraUnlock.ini`, reads your settings from `HeadTracking.cfg` again at every start while there is no `CameraUnlock.ini`, and a change made in game lasts until the game closes.
- The tracking mode `Page Up` picks and the yaw mode `Page Down` picks are saved to `CameraUnlock.ini`, and the next start begins in them. Earlier versions always started in rotation and position, and in the yaw mode the file named.
- `PositionToggleKey` is `CycleTrackingModeKey` in `CameraUnlock.ini`, and the settings sit in sections: `[Network]`, `[General]`, `[Smoothing]`, `[Position]` and `[Hotkeys]`. The import carries each value over.
- An old file whose `UdpPort` is outside 1 to 65535 (for example `UdpPort = 0`) is not imported. The mod runs that session on the settings it read, saves nothing, says so in `HeadTracking.log`, and tries again at the next start.
- `uninstall.cmd` leaves `CameraUnlock.ini` and `HeadTracking.cfg` in place. It used to delete `HeadTracking.cfg`.

### Added

- `EnableOnStartup` in `CameraUnlock.ini` sets whether head tracking is on when the game starts. `End` still turns it on and off for the session only and never changes the file.
- A setting set to `default` in `CameraUnlock.ini` takes its value from `Defaults.ini`, which every head tracking mod that keeps its settings in `CameraUnlock.ini` reads. Head tracking mods that keep their settings in another file do not read it, and neither do earlier versions of this mod. Writing a value in place of `default` changes that setting for this game only. When the mod saves a setting that a hotkey changed in game, it writes the new value in place of `default`, so that setting no longer follows `Defaults.ini` in this game until you set it to `default` again.
- `Defaults.ini` is `%AppData%\CameraUnlock\Defaults.ini` on Windows; `$XDG_CONFIG_HOME/CameraUnlock/Defaults.ini` on Linux, or `~/.config/CameraUnlock/Defaults.ini` where `XDG_CONFIG_HOME` is not set, under Wine and Proton too; and `~/Library/Application Support/CameraUnlock/Defaults.ini` on macOS. The mod's log, where it writes one, names the file it read.
- When the mod starts and finds no `Defaults.ini`, it creates one holding the built-in values, unless Windows runs the game as a packaged app, or the game runs on Linux or macOS without Wine or Proton. The mod never changes `Defaults.ini` after that.

### Removed

- The reticle settings, `ShowReticle` and `ReticleColor`. The reticle is drawn in white whenever head tracking is on during gameplay, as it was by default.
- The sensitivity and axis inversion settings: `YawSensitivity`, `PitchSensitivity`, `RollSensitivity`, `PositionSensitivityX`, `PositionSensitivityY`, `PositionSensitivityZ`, `InvertPositionX`, `InvertPositionY` and `InvertTrackerZ`. Set these in your tracker app instead.
- With these settings at their shipped defaults the camera moves as it did before.

## [1.5.0] - 2026-08-20

### Added

- remove recentring; the tracker owns the centre

### Changed

- Recentring is gone entirely: the `Home` / `Ctrl+Shift+T` hotkey, the
  `RecenterKey` config entry, and every centre the mod used to capture. Your
  tracker owns the centre now. Centre it there, with OpenTrack's Center bind or
  your phone app's own centre button, and the mod applies what it sends.

  Two centres in series was the problem. Each side recentred at moments the
  other could not see, so when the view was off you could not tell which one was
  wrong, and switching between trackers meant recentring in both. With one
  centre there is nothing to disagree about.

### Fixed

- `HeadTracking_BOOT.log` and `HeadTracking_BOOT_ERROR.log` are now rewritten on
  every launch instead of being appended to forever, so a log sent in with a bug
  report only contains the session it describes.
- A fault inside the per-frame camera hook, the reticle search or the
  interaction-text positioner is now logged once instead of on every frame, which
  could previously fill the log at roughly 17 MB per hour.

## [1.4.2] - 2026-08-18

### Fixed

- restore the forward lean budget; InvertPositionZ becomes InvertTrackerZ
- unblock release notes generation

## [1.4.1] - 2026-08-18

### Fixed

- follow core's per-connection smoothing split
- match stub member kinds to the shipped Unity assemblies
- compile the uGUI stubs into UnityEngine.UI, not UnityEngine

### Changed

- Smoothing is now two `HeadTracking.cfg` keys instead of one: `LocalSmoothing`
  (default 0.0) applies when the tracker runs on this machine, `RemoteSmoothing`
  (default 0.15) applies when the tracker is a remote device on the network. The
  value is selected per connection from the packet source address and
  re-evaluated whenever the connection changes.
- Removed the `Smoothing` key. Both new keys cover rotation and position, so
  there is no separate position smoothing setting.
- Removed the hidden 0.15 baseline smoothing floor. Local users now get
  zero-latency tracking by default instead of a silently enforced minimum.
- Sample-rate-to-frame-rate interpolation is no longer gated on the smoothing
  value, so local users at smoothing 0.0 keep smooth motion on high-refresh
  displays.

## [1.4.0] - 2026-08-03

### Added

- default yaw mode to horizon-locked world-space
- honor tracker recenter requests; re-arm auto-recenter only on game-state stops

### Fixed

- show full control set in pixi install via shared -Controls

## [1.3.2] - 2026-06-08

### Added

- add HeadTrackingSession and expand C++ core with RE Engine, Unreal, and tracking-session modules
- aim projection, reframework/unreal hooks, input/logging hardening, games
- add Mass Effect Legendary Edition to games catalog
- expand games catalog, fix unicode games.json read, stage launcher manifest
- add Pacific Drive to games catalog
- add Homeworld: Remastered Collection to games catalog
- add manifest-mode installer validator and ASI loader subdir support
- authenticate GitHub API requests via env token when present
- migrate to manifest delivery mode and pixi-driven CI
- add R.E.P.O. detection data
- reversible Cecil patch and net35 retarget
- guard the .original backup against patched assemblies

### Fixed

- fail fast in ASI dev-deploy when the game is running
- restore il2cpp camera position by undoing applied local delta
- set SO_REUSEADDR so the receiver reclaims its port on relaunch
- align UnityStubs sceneLoaded with UnityAction signature

### Other

- Add Ubisoft Connect detection and VendorZip BepInEx install
- Add PluginSubfolder param to Invoke-DevDeployBepInEx
- Add Xbox install path for Easy Delivery Co
- Add GOG IDs for Cyberpunk 2077
- Add PLUGIN_SUBFOLDER support to BepInEx install/uninstall bodies
- scripts: drop the two-phase loader-init prompt from install bodies
- data: add Black & White (Lionhead) to games registry
- scripts: detect BepInEx 6 IL2CPP via BepInEx.Core.dll marker
- powershell: skip cameraunlock-core remote refresh in CI
- scripts: add UE4SS install template, fix delayed expansion in ASI body, expand games registry
- protocol: reject finite-but-out-of-float-range packet values
- data: add Subnautica 2 to games registry
- detection: add installer-registry game path lookup (Black & White GameDir)
- protocol: reorder tracking data member in udp_receiver
- data: fix Subnautica 2 Steam app id (3367150 -> 1962700)
- data: add Ni no Kuni Remastered and Yakuza 0; switch find-game output to UTF-8
- detection: add Xbox/GDK build support for Subnautica 2 (and any future GDK title)
- find-game: escape `&` in GAME_DISPLAY_NAME so echo doesn't split
- templates: add uninstall.ps1; data: add Deus Ex Mankind Divided
- powershell: add NightlyRelease module for Patreon-gated nightly builds
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics; powershell: write nightly manifest.json without UTF-8 BOM; data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: publish dev builds as GitHub pre-releases
- protocol: disable SIO_UDP_CONNRESET and add one-shot receiver diagnostics
- data: add Mixtape
- powershell: stop redirecting git stderr in Update-CameraUnlockCoreToRemoteTip
- powershell: run gh under Continue so its stderr doesn't abort the dev-release publish
- reframework: strip VR runtime DLLs on install for flatscreen mode
- reframework: cache GetValue method and avoid per-call heap in ArrayGetValue; data: add BioShock Infinite
- uninstall: remove reframework_revision.txt marker dropped at game root
- install: render MOD_CONTROLS multi-line via percent expansion
- Add YAPYAP to games.json
- powershell: write state file BOM-less so Lopari JSON parser accepts it
- powershell: stop redirecting git stderr in Invoke-VersionCommit

## [1.3.1] - 2026-05-03

### Other

- Verify existing BepInEx loader arch and replace on mismatch
- Fall back to dev-tree vendor path in BepInEx install body

## [1.3.0] - 2026-05-03

### Other

- Add DX11 overlay header for crosshair rendering
- Update PositionInterpolator tests for bounded extrapolation
- Skip vendor refresh when SHA-256 matches existing copy
- Fix degenerate-input bugs in scanners, projection, and color parser
- Add yaw-mode key and WorldSpaceYaw config options
- Quote /y flag detection and add shared install/uninstall bodies
- Convert install/uninstall.cmd to thin wrappers over shared bodies
- Add DevDeploy module with Cecil dev-install orchestrator
- Auto-refresh cameraunlock-core submodule in Copy-SharedBundle
- Add yaw mode toggle (world-space vs camera-local)
- Add install bodies and dev-deploy orchestrators for non-Cecil frameworks
- Default yaw mode to camera-local
- Resolve exe relpath from games.json in ASI/shim dev-deploy
- Add automatic port retry to C++ UdpReceiver
- Take BuildOutputPath in dev-deploy and add loader/config auto-install
- Fix roll sign in camera-local yaw branch

## [1.2.0] - 2026-04-30

### Other

- Sync install scripts to template with /y fix, bump to 1.1.1
- Expand submodule pointer commits in generated changelogs
- Fix /y flag detection and bundle vendored BepInEx in installers
- Cycle tracking mode on PgUp instead of toggling position only
- Use WriteAllBytes for .cmd output to avoid Defender race

## [1.1.0] - 2026-04-29

### Other

- Sync to shared standards: chord hotkeys, non-interactive release, data-driven detection
- Add anyKey/anyKeyDown stubs to Unity Input

## [1.0.6] - 2026-04-18

### Changed

- Smoother head tracking at high refresh rates. Pulls in cameraunlock-core velocity extrapolation in PoseInterpolator, eliminating the flat spots between tracker samples that were visible on 144Hz+ displays.
- Ships a `launcher-manifest.json` alongside the installer ZIP so the forthcoming CameraUnlock Launcher can drive Install & Play / Uninstall via this mod's existing `install.cmd` / `uninstall.cmd`.

### Fixed

- Build no longer emits MSB3245 "UnityEngine.InputLegacyModule not found" on pre-2017.3 Unity titles. The reference in the shared core csproj is now gated on the DLL actually existing in UnityEnginePath.

## [1.0.5] - 2026-03-28

### Other

- Skip pose interpolation at zero smoothing to avoid correction stutters
- Remove neck model parameters after core API simplification
- Use camera-relative rotation and fix reticle jitter

## [1.0.4] - 2026-03-13

### Other

- Set default smoothing to 0.15, simplify config comment

## [1.0.3] - 2026-03-13

### Other

- Switch to view-matrix-only head tracking, remove transform save/restore

## [1.0.2] - 2026-03-13

### Other

- Use horizon-locked yaw via Rodrigues rotation, remove output smoothing
- Use shared rotation/position helpers, add auto-recenter on tracking loss

## [1.0.1] - 2026-03-10

### Other

- Add position toggle hotkey and fix rotation projection
- Apply output smoothing to all connections, not just remote

## [1.0.0] - 2026-03-08

First release.
