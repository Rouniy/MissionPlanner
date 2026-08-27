# Mission Planner — agent instructions

## Rules

- Check control widths at the window's `MinWidth`, not its default size: Avalonia's `Grid` silently
  paints an overflowing child across its neighbours, and a `NumericUpDown` spends 82px on spinner
  chrome and padding before its first glyph.

## Docs

- `README.md` — build, run, Linux prerequisites
- `SITL-TESTING.md` — running the build against a simulated autopilot
- `Porting/README.md` — Avalonia migration architecture
- `Porting/STATUS.md` — migration state and handoff record
- `Porting/BASELINE.md` — frozen baseline commit
- `Porting/NATIVE_SURFACE.tsv` — per-file port disposition
- `Porting/FEATURE_AUDIT.md` — cross-platform feature status
- `Porting/RELEASE.md` — packaging and releases
- `Porting/NV_MODEM.md` — NV modem setup
