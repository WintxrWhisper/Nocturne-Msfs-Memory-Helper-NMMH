# Nocturne MSFS Memory Helper 1.5.5 RC

This release fixes the DLL-free SimConnect connection. NMMH now connects to MSFS 2024 without asking you to install the SDK or find a SimConnect DLL. I have confirmed it connects on my own system.

The two-pass memory cleanup is unchanged. Cleaning still requires a live SimConnect connection, and automatic cleaning starts disabled.

## Updating

Exit the old version before opening this one. The first cleanup may ask for administrator approval again because the worker's EXE location has changed.

If it keeps waiting for SimConnect, include the cleanup-log messages in your report. The log now gives more useful connection details.

## Antivirus warning

Microsoft has flagged a build as `Trojan:Win32/Wacatac.C!ml`. I believe this is a false positive. NMMH uses Windows memory-management functions and an elevated cleanup worker; it does not inject into MSFS, download other programs or contain hidden scripts.

The full source is available if you want to inspect it or compile your own copy. [Here is the VirusTotal report](https://www.virustotal.com/gui/file/0a66e18f023d811e3e52e2fc92b7f61d55a65623b6add1035f2ec944506fe095). That scan is for a different-hash build from the GitHub-built EXE, so check the file you have against `SHA256SUMS.txt` rather than assuming one scan covers every copy.

More information is in [SECURITY.md](https://github.com/WintxrWhisper/Nocturne-Msfs-Memory-Helper-NMMH/blob/main/SECURITY.md). The download itself has not been replaced.

Save important work and read the included README before cleaning. Cleanup affects Windows memory system-wide and can cause brief repaging stutters.
