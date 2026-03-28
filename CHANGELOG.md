# Changelog

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
