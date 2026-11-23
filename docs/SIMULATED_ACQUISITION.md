# Simulated acquisition (experimental; no instrument required)

This mode is **not** the Thermo instrument acquisition path. It uses a fresh
shared directory containing versioned scan packets and a status handshake.
Production `MsScanArrived` callbacks, `DataPipe`, and the existing offline
positional test harness remain unchanged.

## Start FLASHIda on Windows

Build **`src/Flash.Simulator.sln`**, not `src/Flash.sln`. The latter is the
real instrument controller and still needs Thermo's proprietary iAPI assemblies.
The new simulator solution contains only instrument-independent sources.

Install Visual Studio 2022, the .NET desktop development workload and the
.NET Framework 4.8 targeting pack, then open Developer PowerShell:

```powershell
cd C:\dev\FlashIDA
git switch feature/simulated-acquisition
git pull --ff-only
nuget restore .\src\Flash.Simulator.sln
msbuild .\src\Flash.Simulator.sln /t:Build /p:Configuration=Debug /p:Platform=x64 /m
```

Run the standalone NUnit tests without any Thermo dependencies:

```powershell
.\src\packages\NUnit.ConsoleRunner.3.16.3\tools\nunit3-console.exe `
  .\src\Flash.Simulator.Tests\bin\x64\Debug\Flash.Simulator.Tests.dll --noresult
```

The compiled simulator copies the repository's native OpenMS DLLs and shared
data into its output directory. Real acquisition still requires a compatible
native `OpenMS.dll` (the committed DLL may be older than the current bridge).

Start the standalone executable with:

```powershell
.\src\Flash.Simulator\bin\x64\Debug\Flash.Simulator.exe `
  --simulate-directory "C:\flashida_sim\run_AA" "C:\flashida\method.json"
```

The directory must be new: do not reuse one with `consumer_status.json` or
`commands.tsv` from a previous run. It can be created by the consumer.
The method must have matching FAIMS settings for the source (v1 supports
one CV or FAIMS off). This command **does not** connect to Thermo iAPI.

Then run the matching Python producer in WSL from the thesis environment. **For a cold RAW conversion cache, start the WSL producer first**; it prepares and caches spectra before waiting for the Windows consumer, so a long first conversion does not exhaust the consumer's input timeout. Start this Windows command after the WSL log says it is waiting for FLASHIda (within the default five-minute ready timeout):

```bash
simulate_instrument --input /path/to/ecoli_AA.raw \
  --output-dir /mnt/c/flashida_sim/run_AA \
  --cache-dir .cache/data --cv -40
```

Use `--cv` only when your source contains multiple CV groups. The Python side
does RAW conversion, CV splitting, centroiding, and caching before publishing
the first scan; release of the first scan is immediate once the consumer is
ready. Existing centroided mzML can use `--centroided-input`.

## Wire protocol v1

The producer writes **atomically** to `scans/scan_00000001.scan` using
a temporary file of the same basename plus `.tmp`. File format is UTF-8
(tab-separated, invariant decimal separator), with:

```text
#FLASHIDA_SIM_V1  <sequence> <RT_seconds> <MS_level> <faims_enabled_0_or_1> <CV> <original_scan_number>
<m/z> <intensity>
...
```

The fields shown separated by spaces above are actually separated by TABs.
The native tracking ID **must not** appear in the packet: the consumer dequeues
its own engine-issued survey command and echoes its `ScanDescription` when
calling `ProcessScan`. The original instrument scan number is only for logs.
RT is recorded in seconds on disk and converted to minutes at the C++ bridge.

After all published packets, the producer atomically writes
`producer_complete.json`: `{"schema_version":1,"scan_count":N}`.

FLASHIda atomically updates `consumer_status.json`:

```json
{
  "schema_version": 1,
  "state": "running",
  "last_received_sequence": 5,
  "last_completed_sequence": 3,
  "queued_scans": 1,
  "processing_scans": 1,
  "idle": false,
  "error": "",
  "updated_utc": "2026-11-23T18:00:00Z"
}
```

The producer may fast-forward only when **its own last published sequence**
equals the consumer's last completed sequence and no work is queued or running.
This protects against a stale `idle` snapshot between publishing a file and
the consumer accepting it. Otherwise, experimental time advances at wall-clock
rate, preserving the original RT gaps. No fixed speed multiplier exists.

The consumer accepts scans in strict sequence order, never drops a queued
scan, and only advances the completion counter after native processing and
command draining return. No progress after the configured input timeout
(15 minutes by default) fails the run rather than reporting success.

The consumer's `commands.tsv` records `survey_echo`, `agc_empty_ack`,
and `unfulfilled` scan commands. The producer's `replay_log.tsv` records
source and virtual acquisition timing. A completed run requires all scans
to be acknowledged and an explicit producer completion marker.

## Deliberate version-1 limitations

* **Only MS1 survey packets are processed.** Recorded MS2 spectra are
  **not** treated as arbitrary responses to acquisition requests.
* Requested MS2/MS3 commands are reported as **unfulfilled**; this version
  cannot establish that any targeted MS2 was acquired.
* An empty AGC acknowledgement is permitted because the native engine needs
  only the tracking description to clear its pending AGC bookkeeping.
* The C++ engine still uses real elapsed time for some scheduling decisions;
  accelerated replay is a software integration test, **not** proof that
  time-sensitive acquisition decisions match a physical Eclipse run.
* Only one FAIMS CV per simulated run; mismatched method/source FAIMS settings
  terminate acquisition instead of silently changing conditions.
* C# code runs on Windows and requires the same native OpenMS DLL build as the
  installed FLASHIda engine. No proprietary Thermo dependency should be
  added to commits or to the simulation packet format.

Before an upstream PR, build and run `SimulationProtocolTests` and the
existing regression suite on Windows with the matching DLL, and compare
command logs against the existing offline harness. The upcoming MS2
response-provider and virtual C++ scheduling clock are separate changes.
