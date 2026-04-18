## What's Changed in v1.0.6

### Changed

- Smoother head tracking at high refresh rates. Pulls in cameraunlock-core velocity extrapolation in PoseInterpolator, eliminating the flat spots between tracker samples that were visible on 144Hz+ displays.
- Ships a `launcher-manifest.json` alongside the installer ZIP so the forthcoming CameraUnlock Launcher can drive Install & Play / Uninstall via this mod's existing `install.cmd` / `uninstall.cmd`.

### Fixed

- Build no longer emits `MSB3245 UnityEngine.InputLegacyModule not found` on pre-2017.3 Unity titles. The reference in the shared core csproj is now gated on the DLL actually existing in `UnityEnginePath`.
