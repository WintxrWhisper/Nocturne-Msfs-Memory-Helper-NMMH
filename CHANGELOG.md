# Changelog

## 1.5.3 RC

- Replaced the external RAMMap dependency with native Windows memory-list operations.
- Added two-pass system-wide cleaning with a two-second redistribution delay.
- Added a hard SimConnect gate to manual and automatic cleaning.
- Added a second safety check inside the elevated cleanup worker.
- Added live active, modified, standby, priority-0 standby, and available-memory monitoring.
- Added cleanup progress, logging, tray controls, configurable intervals, and MSFS start/close behavior.
- Added one-time authorization through an elevated scheduled worker.
- Fixed taskbar icon behavior while the status window is open.
- Fixed clipping of the final memory row at scaled Windows display settings.

