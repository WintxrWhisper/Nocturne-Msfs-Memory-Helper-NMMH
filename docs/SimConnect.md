# DLL-free SimConnect gate

NMMH implements only the read-only SimConnect traffic needed for its connection
gate. It is not a general SDK replacement.

## Correction in 1.5.5

Live 1.5.4 testing produced timeouts and an unsupported-wire-version error.
The implementation had mixed FSX protocol 4 and its XSF identifier with 2024
version metadata. The simulated server also used protocol 4, hiding the defect.

1.5.5 sends SunRise protocol 6 with the RS identifier. Tests use independently
specified literal packet bytes and simulated version-6 responses. These tests
are not a substitute for testing against a running MSFS 2024 installation.

## Transport and gate

- Connect to the standard local pipe, `\\.\pipe\Microsoft Flight Simulator\SimConnect`.
- Verify the server PID against the detected simulator using Windows
  `GetNamedPipeServerProcessId`.
- Send OPEN and require a complete reply identifying MSFS 2024 (SunRise).
- Send `RequestSystemState("Sim")` and require the matching request ID.
- Refresh this read-only heartbeat every five seconds, with a ten-second response
  timeout. A returned zero is valid in menus or paused states.
- Lock on disconnect, QUIT, bad packets, missing responses, process changes, or
  application exit. Retry in the background after three seconds.
- Reject late replies from cancelled or replaced sessions.
- Recheck the gate before a cleanup request and again after UAC.

The gate is not an instantaneous guarantee that MSFS cannot exit between approval
and worker execution. The worker's existing stale/unapproved-request checks remain
unchanged. No process-memory access, injection, remote transport, or listener is used.

## Wire details

Integers are little-endian. Outbound headers contain length, client protocol,
operation, and send ID (16 bytes). Inbound headers contain length, server protocol,
and response ID (12 bytes). Client and server version fields are not assumed equal.

The outgoing client protocol is 6 (MSFS 2024 / SunRise).
OPEN is operation 0xF0000001, 296 bytes. After the 256-byte application name:
a reserved uint32, a zero byte, the three-byte identifier RS plus a null byte,
and the four version integers 12, 2, 282174, 999.

The 24-byte tail at offset 272 is:

`00 00 00 00 00 52 53 00 0c 00 00 00 02 00 00 00 3e 4e 04 00 e7 03 00 00`

RequestSystemState is operation 0xF0000035, 276 bytes: a request ID and a 256-byte
state name. OPEN response ID is 2, QUIT is 3, and system-state response is 15.
Incoming packet lengths and recognized server header versions (4, 5, 6) are
bounded. Product and server-PID checks remain required regardless of header version.
No FSX handshake fallback is used.

## Sources and limits

Wire-format facts were cross-checked against current maintained client code:

- [node-simconnect protocol values](https://github.com/EvenAR/node-simconnect/blob/master/src/enums/Protocol.ts)
- [OPEN fields and system-state request](https://github.com/EvenAR/node-simconnect/blob/master/src/SimConnectConnection.ts)
- [outgoing header format](https://github.com/EvenAR/node-simconnect/blob/master/src/SimConnectPacketBuilder.ts)
- [incoming header reader](https://github.com/EvenAR/node-simconnect/blob/master/src/SimConnectSocket.ts)
- [OPEN reply fields](https://github.com/EvenAR/node-simconnect/blob/master/src/recv/RecvOpen.ts)

This is an independent implementation; no third-party client code is bundled.
The earlier protocol-4 source interpretation is not retained as authoritative.
Standard local MSFS 2024 connections are the target. Custom or remote endpoints
and future incompatible protocol changes are not supported.

## Tests

`Test-NMMH.cmd` runs packet and gate tests without executing memory-cleaning
operations. Windows integration tests use a simulated named-pipe server and
exercise the actual server-PID API, version-6 replies, fragmentation, disconnects,
timeouts, wrong product/owner/request ID, cancellation, retries, and disposal.
The packet tests include explicit literal-byte expectations rather than treating
the production serializer as their source of truth.
