# Nocturne MSFS Memory Helper 1.5.3 RC

This is the first public release candidate of Nocturne MSFS Memory Helper.

NMMH is a lightweight Windows tray utility created to reduce the performance degradation I experience as physical system memory becomes crowded while using Microsoft Flight Simulator 2024.

## Included

- Native two-pass Windows memory cleanup
- Live physical-memory monitoring
- Manual and configurable automatic cleaning
- Hard SimConnect safety gate
- One-time administrator authorization for later cleanups
- Start with MSFS and Close with MSFS options
- Tray controls, cleanup progress, and logging
- Corrected status-window layout for scaled Windows displays

## Important safety information

NMMH performs system-wide Windows memory-management operations. Save important work and avoid cleaning during rendering, exporting, compiling, recording, or another operation you cannot afford to interrupt.

MSFS may briefly stutter after cleaning while required pages and assets are loaded back into physical RAM. Livestreaming, recording software, and primarily streamed MSFS world-content configurations have not yet been thoroughly tested.

Read the included `README.txt` before use.

## Antivirus transparency

This release was compiled locally on Windows using Microsoft's .NET Framework C# compiler. VirusTotal reported three generic heuristic detections from Bkav Pro, McAfee Scanner, and SecureAge. Microsoft Defender and the remaining engines reported the file as clean. The complete source and build script are available in this repository for independent inspection and reproduction.

NMMH legitimately adjusts a Windows process privilege, invokes native system-memory operations, and registers an elevated scheduled worker after user approval. Those capabilities can resemble the static capability profile of a system utility or malicious software to heuristic scanners, even though NMMH does not download payloads, inject into MSFS, or contain PowerShell or VBS components.

## SHA-256

```text
ZIP  e573f7de740ade4fd09298c04d70c9faf2b976bf8e5f147a581284134dfb061b
EXE  6b400ff8c66dc3b763cea65fd11d2417691198be8154e0de9cfbb34ac94edf0a
```

