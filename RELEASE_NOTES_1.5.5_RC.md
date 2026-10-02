# Nocturne MSFS Memory Helper 1.5.5 RC

Fixes the incorrect SimConnect handshake in 1.5.4 RC.

The previous build mixed an FSX version-4 header and identifier with MSFS 2024
version metadata, then rejected replies whose server version was not 4.
It passed simulated tests because those tests repeated the same assumption.

This build sends the MSFS 2024 SunRise version-6 handshake with the RS identifier.
Incoming server versions are checked independently, and unsupported-version
errors now show the actual version, packet size, and response ID. Connection logs
also include the reported simulator and SimConnect versions and the failed stage.

Server-PID verification, MSFS 2024 identity checks, matching read-only heartbeats,
disconnect locking, and automatic cleaning initially disabled remain in place.
The two-pass cleaning operations are unchanged. No SDK or SimConnect DLL is needed.

## Upgrade and verification

Exit the old NMMH version before launching this EXE. The first cleanup may ask for
administrator approval again because the worker executable path changes.

The Windows workflow builds the executable and runs updated protocol and local
pipe tests. These are simulated-server tests, not tests against a running MSFS.
Live confirmation of this fix is still required.

If connection still fails, provide the new cleanup-log lines. They now distinguish
OPEN failures from heartbeat failures and include the actual unsupported header
values when applicable.

Save important work and read the included README before cleaning. Previous
VirusTotal reports do not cover this new executable.
