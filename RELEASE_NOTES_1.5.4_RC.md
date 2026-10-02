# Nocturne MSFS Memory Helper 1.5.4 RC

This update removes the external SimConnect DLL dependency. No SDK installation,
DLL selection, or simulator installation-path setup is needed.

NMMH now connects directly to MSFS 2024's standard local SimConnect pipe. Cleaning
requires a verified server PID, an MSFS 2024 OPEN reply, and matching read-only
heartbeat replies. Missing connections, disconnects, QUIT replies, and timeouts
keep or return the gate to locked. Automatic cleaning still starts disabled.

The two-pass system-wide cleanup, two-second delay, icons, and other controls are
unchanged. The connection is rechecked before cleanup and after UAC authorization.

## Upgrade

1. Exit the old NMMH version.
2. Extract the new ZIP to a permanent location and run its EXE.
3. Start MSFS 2024 and wait for **SimConnect connected**.
4. The first cleanup may request administrator approval again to update the
   scheduled worker's executable path.

Old saved DLL paths are ignored. Do not copy or download SimConnect DLLs.
Custom or remote SimConnect endpoints are not supported.

## Verification

The release workflow compiles with Windows' .NET Framework C# compiler and runs
protocol and local named-pipe tests without performing memory-cleaning operations.
Live MSFS testing of this new connection implementation still needs confirmation.

Save important work before cleaning and read the included README. A cleanup can
cause short repaging stutters, and the operations affect the whole Windows system.
Previous VirusTotal reports do not cover this new executable; this update makes
no promise of zero antivirus detections.
