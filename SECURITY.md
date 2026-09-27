# Security and false-positive reports

NMMH performs privileged Windows memory-management operations and registers an elevated scheduled worker after the user approves its first cleanup. Those capabilities can occasionally trigger generic antivirus heuristics even when no malicious behavior is present.

The complete source and Windows build script are included in this repository so the executable can be inspected and reproduced independently.

NMMH does not download or execute remote payloads, inject code into MSFS, modify simulator files, or include PowerShell or VBS components.

If a release is detected by security software, please open a security report containing:

- The NMMH version and SHA-256 hash
- The security product and detection name
- A link to the VirusTotal report, when available
- Whether the detection occurred on download, launch, authorization, or cleanup

Do not include passwords, tokens, personal paths, or other sensitive information in a public report.

