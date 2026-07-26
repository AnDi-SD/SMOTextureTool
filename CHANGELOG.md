# Changelog

## 2.0.0

### Added

- Cross-platform Avalonia desktop interface.
- HD texture replacement with a safe `4096×4096` mode and an experimental
  `16384×16384` limit.
- RGBA, RGB, Alpha, checkerboard, and model-coloring preview modes.
- Material graph reconstruction for `spMaterialData`, passes, layers, texture
  states, and linked `spModel`/`spMeshData`.
- Vertex diffuse color preview for grayscale `D3DTOP_MODULATE` textures.
- Channel statistics, content classification, and UV-conflict diagnostics.
- Regression tests for 14 sample SMO files and 170 textures.
- Detailed reverse-engineering notes in `docs/SMO_FORMAT.md`.

### Fixed

- Correct 32-bit nested sizes for ABGR `0x32E3`/`0x43E3` textures.
- Correct unaligned 32-bit sizes and dimensions for BGRA `0x29E3` textures.
- Repacking beyond the former 2048×2048 header boundary.
- Preservation of unknown container tails during texture growth.
- Layout clipping and scrolling issues in the texture list.

### Changed

- Core SMO logic is separated from the graphical interface.
- Generated SMO files are parsed and validated before they are saved.
- Grayscale resources are no longer guessed to be shadow maps; actual shadow
  volumes are treated as a separate geometry resource hierarchy.

## 1.0.0

- Original Windows Forms texture extraction and replacement utility.
