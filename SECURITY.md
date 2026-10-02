# Antivirus warnings

Microsoft has flagged a build of NMMH as `Trojan:Win32/Wacatac.C!ml`. I believe this is a false positive, and I want people to know about it before downloading.

NMMH uses Windows' own memory-management functions. It needs administrator approval to register its cleanup worker, which lets later cleanups run without repeated UAC prompts. It does not inject code into MSFS, download other programs or hide PowerShell/VBS scripts inside the EXE.

The complete source and build script are included. You can inspect what it does or build your own copy. I cannot promise that every antivirus will give it a clean scan, and Microsoft has not reviewed this detection yet.

## VirusTotal report

[View the report](https://www.virustotal.com/gui/file/0a66e18f023d811e3e52e2fc92b7f61d55a65623b6add1035f2ec944506fe095).

The file in that report has SHA-256:

`0a66e18f023d811e3e52e2fc92b7f61d55a65623b6add1035f2ec944506fe095`

The GitHub-built 1.5.5 RC EXE has SHA-256:

`64a92c3acac13edbb22748e992b26cfb05a0e3496a7e7c8d659534ede3360fee`

These are different files. A scan of one does not cover the other. Use the release's `SHA256SUMS.txt` to check the download you have. This notice does not replace the release download.

## If you get another warning

Please open an issue with the version, antivirus name, detection name and file hash. Include a VirusTotal link if you have one, and say whether it happened while downloading, opening the app or cleaning.

Do not post passwords, tokens or other private information. If you are not comfortable running it, leave it blocked. Microsoft's [file-submission guidance](https://learn.microsoft.com/en-us/defender-xdr/developer-faq) explains how to request a review of an incorrect detection.
