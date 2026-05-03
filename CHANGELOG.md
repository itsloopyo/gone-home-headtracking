# Changelog

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
