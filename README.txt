NOCTURNE MSFS MEMORY HELPER 1.5.3 RC
===================================

Nocturne MSFS Memory Helper (NMMH) is a lightweight Windows tray utility for
Microsoft Flight Simulator 2024.

It was created to reduce the performance degradation I experience as physical
system memory becomes crowded. This pressure can begin during the initial
simulator startup, while loading into an aircraft, or whenever MSFS loads new
scenery, airports, traffic, camera views or other assets.


HOW CLEANING WORKS
------------------

Each cleanup performs two complete system-wide passes:

  1. Empty Windows working sets.
  2. Flush the modified page list.
  3. Purge the standby list.
  4. Purge the priority-0 standby list.
  5. Wait two seconds for Windows to finish redistributing memory.
  6. Repeat the complete sequence.

The second pass is intentional. During testing, the first pass frequently
pushed a large amount of memory into compressed or modified states. The second
pass cleaned much of the memory created by the first operation and produced a
substantially larger reduction in active physical-memory use.

NMMH uses Windows' own native memory-management interfaces. It does not modify
MSFS files, aircraft, flight plans or simulator variables. It does not inject
code into MSFS or directly clear dedicated VRAM.


WHY IT HELPS MSFS
-----------------

The problem is not simply that MSFS allocates a large amount of memory. The
problem appears when previously loaded data remains physically resident while
the simulator continues loading more.

Shared GPU memory is also backed by physical system RAM. This means MSFS's
normal process memory and part of its graphics workload can compete within the
same physical-memory pool. As that pool becomes crowded, Windows and WDDM must
increasingly move, page and make resources resident while MSFS is trying to
render the next frame.

In my testing, performance degradation consistently begins as this memory
pressure and residency activity increase. The simulator itself may continue
running normally underneath, but its graphics queues begin falling behind and
visible performance deteriorates.

Restoring physical-memory headroom reduces that residency churn and gives MSFS
room for the next loading operation. This is why controlling memory pressure
has allowed the same system to run higher settings more reliably without
changing the underlying CPU or GPU.


FEATURES
--------

  - One normally compiled C# executable; no PowerShell or VBS components
  - Live physical-memory monitoring
  - Manual Clean now button
  - Configurable automatic-cleaning interval
  - Start with MSFS option
  - Close with MSFS option
  - Tray controls and status window
  - Cleanup progress for both passes
  - Cleanup log
  - Hard SimConnect safety gate

Cleaning is disabled whenever NMMH does not have a live SimConnect connection.
The elevated worker also refuses missing, stale or unapproved cleanup requests.
Automatic cleaning starts disabled whenever NMMH opens, so launching the
utility alone will never trigger a cleanup.

The first cleanup requests administrator approval once. NMMH then registers
its own elevated scheduled worker so later cleanups do not produce recurring
UAC prompts. The same EXE performs the normal tray, worker and watcher roles;
there are no extracted worker scripts or hidden payloads.


QUICK START
-----------

  1. Extract the complete release folder to a permanent location.
  2. Read this README before using the utility.
  3. Run Nocturne-MSFS-Memory-Helper-1.5.3-RC.exe.
  4. Start MSFS 2024 and wait for NMMH to report SimConnect connected.
  5. Use Clean now, or enable automatic cleaning and choose an interval.

Keep the EXE in its chosen location after granting administrator approval.
Windows scheduled tasks point to that exact file. If you move it later, run it
from the new location and approve authorization again when requested.

Settings, worker status and the cleanup log are stored in:

  %LOCALAPPDATA%\NocturneMemoryCleaner


WHAT TO EXPECT
--------------

A cleanup may cause a large and immediate drop in active physical-memory use.
Committed memory may change much less because applications still own their
virtual-memory allocations.

MSFS may briefly stutter after cleaning while required pages and assets are
loaded back into physical RAM. This is expected. More aggressive cleaning
creates greater memory headroom, but it can also produce more noticeable
short-term repaging.


TESTING AND SAFETY
------------------

NMMH has not been tested on every possible computer or software configuration.
It has, however, been extensively stress-tested on my own 32 GB system with
MSFS, multiple simulator add-ons and many other applications running in
different working states.

I have tested cleaning during startup, takeoff, climb, cruise, descent,
approach, landing and rollout, including repeated cleanups in close succession.
So far, I have not observed data loss, corrupted work, application crashes or
lasting damage directly caused by NMMH.

These operations affect Windows system memory globally. Save important work
before cleaning and avoid triggering a cleanup during rendering, exporting,
compiling, recording or any other operation you cannot afford to interrupt.

Livestreaming, recording software and MSFS installations that primarily stream
world content have not yet been thoroughly tested. My own MSFS installation
contains everything currently available for local download, so streamed
configurations may behave differently.


REMOVAL
-------

To remove NMMH completely:

  1. Exit NMMH from its tray menu.
  2. Open Windows Task Scheduler.
  3. Delete these two tasks if present:

       Nocturne Memory Cleaner - Elevated Native Worker
       Nocturne MSFS Memory Helper - MSFS Watcher

  4. Delete the NMMH EXE and README.
  5. Optionally delete %LOCALAPPDATA%\NocturneMemoryCleaner to remove settings
     and logs.


RELEASE AND UPDATE POLICY
-------------------------

This is currently a release-candidate investigation tool.

I cannot promise regular updates, a fixed development schedule or continued
feature additions. If NMMH remains stable and continues doing its job, there
may simply be nothing that needs updating.

Future updates will primarily depend on finding additional reproducible
problems, identifying meaningful improvements, or receiving useful reports of
instability on other configurations. Reports showing no improvement or
increased instability are just as valuable as successful results, but
individual support or a fix for every system cannot be guaranteed.
