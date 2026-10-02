# Changelog

## 1.5.5 RC

- Fixed the mismatched SimConnect handshake in 1.5.4: send SunRise protocol 6 and its RS identifier, rather than FSX protocol 4 with 2024 version metadata.
- Corrected receive-version handling so it is not assumed to match an obsolete client version.
- Removed the FSX fallback; this build targets the MSFS 2024 protocol directly.
- Added independent literal-byte regression tests and protocol-6 simulated server replies.
- Log server version, simulator identity, reported SimConnect version, and which receive stage failed.
- Retained server-PID verification, MSFS 2024 identity checks, heartbeat matching, and disconnect locking.
- No changes to cleaning operations.
- Live MSFS confirmation remains pending; automated tests alone do not prove simulator compatibility.

## 1.5.4 RC

- Known issue: the handshake mixed FSX protocol/identifier with MSFS 2024 version metadata. Live testing failed; use 1.5.5 RC instead.
- Removed external SimConnect DLL loading, SDK directory searches, and the runtime file picker.
- Added a direct, read-only client for MSFS 2024's standard local SimConnect pipe.
- Require the detected simulator's server PID, a valid MSFS 2024 OPEN reply, and matching heartbeat replies before enabling cleaning.
- Added background retries, response timeouts, disconnect/QUIT handling, and cancellation of stale sessions.
- Recheck the connection before cleanup and again after administrator authorization.
- Added automated packet, gate, and Windows named-pipe tests.
- Updated installation and upgrade instructions; old saved DLL paths are ignored.
- Left the two-pass cleaning operations, timing, icons, and other UI controls unchanged.
- Live MSFS validation of the new connection implementation remains pending.

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
