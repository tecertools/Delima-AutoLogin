# T0.3 & T0.4 — Injection and UIA Verification Spike

Validates core mechanisms of the DELIMa Smart Launcher **before** production deployment.

| Mode | Task | Question | Touches Google? | Credentials Typed? |
| :-- | :-- | :-- | :-- | :-- |
| **A — fidelity** | T0.3 | Do all password characters survive injection? | No | Synthetic in URL fragment |
| **B — timing** | T0.3 | How long until Chrome is actually ready to receive keystrokes? | Yes | None (no keystrokes sent) |
| **C — uia** | T0.4 | Does Chrome report `IsPassword` reliably via UI Automation? | Yes | Email only (no password typed) |

---

## Modes

### Mode A — Character Fidelity
`testpage.html` receives the expected password in the URL **fragment**, which never leaves the machine. It compares what actually landed in the field and writes the verdict into `document.title`. The harness reads that with `GetWindowText`. Proves `SendInput` with `KEYEVENTF_UNICODE` against `SendKeys`.

### Mode B — Timing Race
Measures cold-start latency against real Google sign-in pages without typing keystrokes, demonstrating why arbitrary sleeps (e.g. 1,500 ms) fail.

### Mode C — UI Automation Verification Probe (T0.4)
Observational probe for `IsPassword` field verification per `Visual_SSO/T0.4_UIA_Verification.md`.
- **Purely observational:** The operator drives Chrome by hand (clicks sign in, types real email on identifier page, lands on password page, closes window).
- **No password typed:** The probe only samples UI Automation state every 100 ms (`run`, `elapsed_ms`, `window_title`, `focus_resolvable`, `is_password`).
- **Pass criteria:** 50/50 reliability on `is_password == false` (identifier page) and `is_password == true` (password page), with zero false positives.
- References `Delima.Win32` for `UiaHelper`, `ChromeSession`, and `NativeMethods`.

---

## Running it

**On a representative lab PC**, not a developer machine. Cold-start latency and accessibility behavior on lab hardware is the entire point.

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and Google Chrome.

```powershell
cd InjectionSpike

# Mode A — Character fidelity (run both to compare)
dotnet run -- fidelity --method sendinput --runs 50
dotnet run -- fidelity --method sendkeys  --runs 50

# Mode B — Cold-start and window-detection latency
dotnet run -- timing --runs 50 --url https://d3.delima.edu.my

# Mode C — UI Automation verification probe (T0.4)
dotnet run -- uia --runs 50 --url https://d3.delima.edu.my/landing

# Mode C — Baseline comparison without accessibility flag (T0.4 Q6)
dotnet run -- uia --runs 50 --no-accessibility
```

Results are saved to `spike-results/uia_<timestamp>.csv` (or `fidelity_*.csv` / `timing_*.csv`).

> **The harness drives the real keyboard in Mode A.** Don't leave the desk mid-run, and don't run it over Remote Desktop — `SendInput` targets the physical input queue and `BlockInput` behaves differently in an RDP session.

---

## Reading the Results

- **Mode A**: Evaluates character fidelity across reserved symbols.
- **Mode B**: Measures window-ready latency percentiles (p50 / p95).
- **Mode C**: Evaluates against the six questions in `Visual_SSO/T0.4_UIA_Verification.md` Part 3:
  1. `focus_resolvable` on identifier page (≥ 49/50)
  2. `is_password == false` on identifier page (50/50, zero tolerance for false positives)
  3. `focus_resolvable` on password page (≥ 49/50)
  4. `is_password == true` on password page (50/50, zero tolerance)
  5. Settle latency to property readable (p50 / p95)
  6. Accessibility flag startup overhead comparison (`--no-accessibility`)

## Files

| File | Role |
| :-- | :-- |
| `Program.cs` | Harness supporting `fidelity`, `timing`, and `uia` modes |
| `testpage.html` | Mode A fidelity target |
| Referenced `Delima.Win32` | `UiaHelper`, `ChromeSession`, `NativeMethods` |

## Status

Targets **.NET 10 (LTS, supported to November 2028)**. Uses promoted `Delima.Win32` components.

