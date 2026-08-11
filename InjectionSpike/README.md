# T0.3 — Injection Spike

Decides whether the DELIMa Smart Launcher's core mechanism is viable **before**
anyone builds the WPF client. Two questions, two modes.

| Mode | Question | Touches Google? |
| :-- | :-- | :-- |
| **A — fidelity** | Do all password characters survive injection? | No |
| **B — timing** | How long until Chrome is actually ready to receive them? | Yes (no keystrokes sent) |

---

## Why two modes

The PRD's pipeline is `Process.Start` → `Thread.Sleep(1500)` → `SendKeys`. Two
independent things can go wrong, and testing them together makes a failure
impossible to attribute:

1. **Character corruption.** `SendKeys` parses `+ ^ % ~ ( ) { } [ ]` as control
   syntax rather than literal characters. `+a` means Shift+A. `{ENTER}` is a
   keyword. A password containing any of these is silently mistyped.
2. **The timing race.** 1,500 ms is a guess. When Chrome takes longer, the
   keystrokes go wherever focus happens to be — the URL bar, a Word document,
   the desktop.

Mode A removes Google, the network and the account from the picture entirely, so
a failure is unambiguously a character-handling bug. Mode B measures the race
without ever sending a keystroke, so it needs no real credentials.

## How Mode A verifies without reading the browser

`testpage.html` receives the expected password in the URL **fragment**, which
never leaves the machine. It compares what actually landed in the field and
writes the verdict into `document.title`. The harness reads that with
`GetWindowText`. No local server, no WebSocket, no extension, no DevTools
protocol.

The title carries only `PASS` / `FAIL` plus character offsets — never the value.
Use the synthetic passwords supplied; do not point this at a real account.

---

## Running it

**On a representative lab PC**, not a developer machine. Cold-start latency on
lab hardware with a spinning disk is the entire point of Mode B.

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and
Google Chrome.

```powershell
cd InjectionSpike

# The comparison that matters — run both, diff the summaries.
dotnet run -- fidelity --method sendinput --runs 50
dotnet run -- fidelity --method sendkeys  --runs 50

# Cold-start and window-detection latency.
dotnet run -- timing --runs 50 --url https://d3.delima.edu.my
```

Each mode writes a timestamped CSV to the working directory.

> **The harness drives the real keyboard.** Don't leave the desk mid-run, and
> don't run it over Remote Desktop — `SendInput` targets the physical input
> queue and `BlockInput` behaves differently in an RDP session.

---

## Reading the results

**Mode A** prints a per-password summary. The expected shape:

```
  plain-lower         5/5   passed
  plus                0/5   passed   <-- CORRUPTED
  caret               0/5   passed   <-- CORRUPTED
  all-reserved        0/5   passed   <-- CORRUPTED
```

for `--method sendkeys`, and a clean sweep for `--method sendinput`. If that is
what you see, the finding is settled: production uses `SendInput` with
`KEYEVENTF_UNICODE`, and the PRD's `SendKeys.SendWait` would have failed silently
on every pupil whose password contains a reserved character.

If `sendinput` also fails, that is a much more interesting result — capture the
CSV and stop before building anything else.

**Mode B** prints p50 / p95 and, most usefully:

```
  runs exceeding the PRD's 1,500 ms assumption: 47/50 (94%)
```

Every one of those is a run where the PRD's design would have typed a child's
password into the wrong window. That number is the argument for foreground-window
verification, and it's worth putting in front of whoever signs off the design.

---

## What this validates beyond injection

The spike deliberately exercises the other mechanisms production needs, so they
get tested for free:

- **Chrome path resolution** via `App Paths` registry with 32-bit, 64-bit and
  per-user fallbacks — the PRD hardcodes one path that fails on two common setups.
- **Throwaway `--user-data-dir` per run**, wiped afterwards, proving session
  isolation between pupils.
- **Scoped process-tree teardown** — graceful `CloseMainWindow` first,
  `taskkill /T /F /PID` only on timeout, never `/IM chrome.exe` (which would kill
  the teacher's own browser and corrupt its profile).
- **`BlockInput` availability** — if it is denied without elevation on your lab
  image, Mode A reports `blockinput_denied` and you know that early.

## Files

| File | Role |
| :-- | :-- |
| `Program.cs` | Harness, both modes, CSV output |
| `NativeMethods.cs` | `SendInput` / `KEYEVENTF_UNICODE`, window inspection, `BlockInput` |
| `ChromeLauncher.cs` | Path resolution, throwaway profile, window wait, teardown |
| `testpage.html` | Mode A fidelity target |

## Status

Written against the .NET 8 SDK but **not yet compiled** — it was authored on a
machine without a Windows toolchain. Expect to fix trivia on first build; the
P/Invoke signatures and struct layouts are the parts worth reviewing closely if
something misbehaves.
