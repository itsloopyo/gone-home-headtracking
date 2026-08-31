# Third-Party Notices

GoneHomeHeadTracking bundles, statically links, or credits the third-party components
listed below. Each remains the property of its authors and is used under its own
licence. Where a licence requires the copyright notice, the conditions and the
disclaimer to accompany a binary distribution, the full text is reproduced here
verbatim, and this file ships at the root of every release ZIP we publish.

This repository contains no Gone Home code, no extracted game assets and no game
data files. The one piece of game-derived material it holds is the README demo
clip, which is covered under "Gone Home footage and screenshots" below.

| Component | Version | Licence | How it ships |
|-----------|---------|---------|--------------|
| Mono.Cecil | 0.11.5 | MIT | Pre-built assembly bundled in the installer ZIP and used as the install-time patcher |
| cameraunlock-core | fec3b4c8a6fe9c45401cf65d3d43d4f5acd22b72 | MIT | Compiled into `HeadTracking.dll` |
| OpenTrack | n/a | ISC | Not bundled; UDP protocol interoperability only |

---

## Mono.Cecil

Shipped as a pre-built assembly alongside `HeadTracking.dll` in every release ZIP
and deployed into the game folder by the installer.

- Upstream: https://github.com/jbevain/cecil
- Version: `0.11.5`

```
Copyright (c) 2008 - 2015 Jb Evain
Copyright (c) 2008 - 2011 Novell, Inc.

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

---

## cameraunlock-core

Git submodule at `cameraunlock-core/`, compiled into `HeadTracking.dll`. Our own code,
MIT licensed, reproduced here so the notices are complete.

- Pinned commit: `fec3b4c8a6fe9c45401cf65d3d43d4f5acd22b72`

```
MIT License

Copyright (c) 2026 CameraUnlock

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## OpenTrack

Not bundled and not linked. This mod implements the OpenTrack UDP pose datagram
layout so that OpenTrack (https://github.com/opentrack/opentrack, ISC licence)
and compatible trackers can drive it. No OpenTrack code, headers or binaries
are copied, linked or redistributed, so its licence triggers no notice
obligation here. It is credited because the wire format is its work.

---

## Third-party middleware named in the source

`GameTypeResolver`, `GameReticleFinder`, `CameraTrackingHook` and
`InteractionTextPositioner` look up game types by name at runtime: `vp_FPSCamera`
and the other `vp_FPS*` types (UFPS, by VisionPunk / Opsive) and `NGUI_HUD`
(NGUI, by Tasharen Entertainment). Those products are the property of their
authors and remain so.

Only the names are used, as the argument to a reflection lookup against
assemblies the game itself has already loaded. No UFPS or NGUI code, headers,
binaries or decompiled output are copied into, linked against, or redistributed
by this repository, so neither licence carries an obligation here. They are
named so that a reader can see exactly what the mod touches.

---

## Gone Home footage and screenshots

- **Files:** `assets/readme-clip.gif`
- **Rights holder:** the developers and publishers of Gone Home, together with the
  rights holders of any third-party marks visible in frame.
- **Usage:** recorded from the game running with this mod, captured on a
  legitimately purchased copy, shown so a reader can see what the mod does
  before installing it.
- **Bundled:** `assets/readme-clip.gif`: kept in this repository only. The packaging scripts
  ship no part of `assets/`, so these are in neither release ZIP nor
  anything the launcher deploys.
- **Licence:** none is granted or implied by this repository. This material is
  not covered by the MIT licence in `LICENSE`, and nothing here permits reuse
  of it. Rights holders who would rather it were not published: open an issue
  or reach us on Discord and it comes down.

---

## Gone Home

Gone Home and all related names, logos, characters and marks are trademarks of
their respective owners. They are used here only to identify the game this mod
applies to, which is nominative use and not a claim of any right in them. This
project is an unofficial, fan-made modification. It is not affiliated with,
endorsed by, or sponsored by the game's developers, its publishers, its engine
vendor, or any other rights holder. It redistributes no game code, no extracted
game assets and no game data files, and it requires a legitimately purchased
copy of the game. The README demo clip is the one piece of game-derived material
in this repository, and the footage section above states who holds the rights to
it and that no licence over it is granted here. Any engine structure offsets,
function addresses or byte patterns
referenced in the source were derived by the authors through independent
analysis of a legitimately owned copy. They are factual measurements recorded
as numbers; no decompiled or disassembled game code is stored in this
repository.
