# DLL-free SimConnect gate

NMMH implements only the small read-only part of the SimConnect wire protocol
needed for its connection gate. It is not a general SDK replacement.

## Transport and gate

- Connect to the standard local pipe, `\\.\pipe\Microsoft Flight Simulator\SimConnect`.
- Use Windows `GetNamedPipeServerProcessId` to verify ownership against the
  simulator PID selected by the existing watcher.
- Send OPEN and require a complete OPEN reply identifying MSFS 2024 (SunRise).
- Send `RequestSystemState("Sim")` and require the matching request ID.
- Refresh this read-only heartbeat every five seconds. A response timeout is ten
  seconds. A value of zero is valid in menus/paused states: this gate confirms a
  responsive connection, not that an aircraft is airborne.
- Lock on disconnect, QUIT, bad packets, missing responses, process changes, or
  application exit. Retry every three seconds in a background thread.
- Reject late replies from cancelled/replaced sessions.
- Recheck the gate immediately before a cleanup request and after UAC.

The gate is not an instantaneous guarantee that MSFS cannot exit between approval
and worker execution. The existing worker's stale/unapproved-request checks remain
unchanged. No network listener, process-memory access, or injection is used.

## Wire details

All integers are little-endian. Outbound headers are 16 bytes (length, version,
operation, send ID); inbound headers are 12 bytes (length, version, response ID).
The wire version is 4.

OPEN is operation 0xF0000001, 296 bytes: a 256-byte application name, the eight-byte
FSX magic, and four client version integers. Announce 12.2.282174.999; only on an
explicit version-mismatch exception retry with 10.0.61259.0 on a fresh connection.
The negotiated client version is not used to identify the simulator.

RequestSystemState is operation 0xF0000035, 276 bytes: request ID and a 256-byte
state name. OPEN response ID is 2, QUIT is 3, and system-state response is 15.
Replies are size/version bounded and read fully even if transport reads fragment.

Independent implementation based on wire-format facts cross-checked against:

- [wegylexy/SimConnect protocol and version table](https://github.com/wegylexy/SimConnect/blob/rust/simconnect-proto/src/protocol.rs)
- [wire framing](https://github.com/wegylexy/SimConnect/blob/rust/simconnect-proto/src/codec.rs)
- [operation layouts](https://github.com/wegylexy/SimConnect/blob/rust/simconnect-proto/src/send.rs)
- [local transport](https://github.com/wegylexy/SimConnect/blob/rust/simconnect/src/transport.rs)

No third-party SimConnect implementation is bundled. Standard local MSFS 2024
connections are the target; custom/remote transports and future incompatible wire
changes are not covered. Testing against a running MSFS installation is still
required to confirm the release on real simulator builds.

## Tests

`Test-NMMH.cmd` builds and runs a standalone console test using the production
connection source. It never calls the memory-cleaning functions. Tests cover
packet layout, fragmentation, invalid/truncated/oversized packets, absent
connections, OPEN plus heartbeat, wrong owner/product/request ID, OPEN without
liveness, disconnect, QUIT, heartbeat timeout, version fallback, process changes,
cancelled sessions, disposal, and Windows' actual server-PID check.
