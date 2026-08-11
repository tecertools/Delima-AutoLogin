# Technical Architecture — DELIMa Smart Launcher v2 (C# WPF, Multi-School)

**Companion to:** `PRD_Visual_SSO_v2.md`
**Date:** 11 August 2026
**Target:** .NET 8, C# 12, WPF, Windows 10 1809+ / Windows 11, x64
**Status:** Specification only.

---

## 1. Position

This document specifies the parts that are not obvious: the credential store, the injection engine, provisioning, and the failure taxonomy. Everything it does not specify is a normal WPF application and should be built as one.

Two decisions frame the rest:

- **No network service exists.** The application makes exactly one class of outbound connection: launching Chrome at a Google URL. It has no listening ports, no API client, no telemetry, and no update check. This is what makes a multi-school deployment defensible.
- **The InjectionSpike code is the seed of the engine, not a throwaway.** `ChromeLauncher.cs` (path resolution, throwaway profile, scoped teardown) and `NativeMethods.cs` (`SendInput` with `KEYEVENTF_UNICODE`, foreground-window inspection, `BlockInput`) already implement the hard parts correctly and should be promoted into the production assembly rather than rewritten. **They have never been compiled** (`../InjectionSpike/README.md`), so budget a day for P/Invoke trivia first.

---

## 2. Solution layout

```
DelimaLauncher.sln
├── src/
│   ├── Delima.Core/            netstandard-ish domain, no UI, no Win32
│   │   ├── Roster/             School, ClassInfo, Student, DisplayName
│   │   ├── Store/              ICredentialStore, StoreSchema, migrations
│   │   ├── Crypto/             Argon2id, AES-GCM, DPAPI wrapper, secure wipe
│   │   └── Audit/              append-only log writer
│   ├── Delima.Win32/           net8.0-windows, P/Invoke only
│   │   ├── NativeMethods.cs    ← promoted from InjectionSpike
│   │   ├── ChromeLauncher.cs   ← promoted from InjectionSpike
│   │   ├── InjectionEngine.cs  window verification + injection + abort
│   │   └── KioskGuard.cs       keyboard hook, topmost overlay
│   ├── Delima.Launcher/        WPF, pupil-facing
│   │   ├── Views/ ViewModels/ Theming/
│   │   └── FloatingResetBar/
│   ├── Delima.Admin/           WPF, wizard + importer
│   │   ├── Wizard/             the 7 steps
│   │   ├── Import/             CsvReader, ColumnMapper, Validator, DryRun
│   │   └── Provisioning/       bundle build, USB/share/script emit
│   └── Delima.Provision/       tiny console/WPF, runs on each lab PC
├── tests/
│   ├── Delima.Core.Tests/      xUnit — crypto, display names, validation
│   └── Delima.Import.Tests/    real-world APDM export fixtures
├── spike/InjectionSpike/       ← existing, kept as the T0.3 harness
├── installer/
│   ├── DelimaLauncher.iss      Inno Setup script
│   └── assets/
└── docs/
```

`Delima.Core` must not reference `Delima.Win32`. That boundary is what makes the store and the importer unit-testable on any machine, including the one this spec was written on.

---

## 3. The credential store

### 3.1 Two artefacts, two key derivations

| Artefact | Lives on | Protected by | Portable? |
| :--- | :--- | :--- | :--- |
| `school.dlmpack` (master bundle) | Admin PC + backup | Admin passphrase → Argon2id | Yes — deliberately |
| `credentials.dat` (per-PC store) | Each lab PC | DPAPI, `LocalMachine` scope | **No — by design** |

DPAPI machine-scope keys are machine-specific, which is a feature: a `credentials.dat` copied off a lab PC is undecryptable anywhere else. The cost is that the admin cannot produce the file centrally, hence `Delima.Provision` running once per PC.

### 3.2 Master bundle format

```
┌──────────────────────────────────────────────┐
│ magic        "DLMPACK\0"          8 bytes    │
│ schema_ver   uint16               2          │
│ kdf_id       uint8  (1=Argon2id)  1          │
│ argon_m      uint32  (KiB)        4          │  64 MiB
│ argon_t      uint32               4          │  3
│ argon_p      uint32               4          │  4
│ salt         32 bytes                        │  CSPRNG per bundle
│ nonce        12 bytes                        │  CSPRNG per encryption
│ ciphertext   AES-256-GCM(payload)            │
│ tag          16 bytes                        │
└──────────────────────────────────────────────┘
```

- Header bytes are the GCM **associated data**, so tampering with the KDF parameters fails authentication rather than silently weakening the derivation.
- The nonce is regenerated on every write. A bundle is rewritten in full; there are no partial updates.
- Argon2id via `Konscious.Security.Cryptography.Argon2` (MIT). Parameters live in the header, so they can be raised later without breaking old bundles.

**Payload** (JSON, then compressed):

```jsonc
{
  "schema_version": 2,
  "school": { "code": "SKS24", "name": "...", "motto": "...", "domain": "moe-dl.edu.my" },
  "theme":  { "primary": "#056839", "accent": "#F7941D", "class_colours": ["#C41118", "..."] },
  "config": {
    "destinations": [
      { "id": "delima",    "label": "DELIMa 3.0",       "url": "https://d3.delima.edu.my/" },
      { "id": "classroom", "label": "Google Classroom", "url": "https://classroom.google.com/" }
    ],
    "picture_password_required": true,
    "idle_reset_seconds": 600,
    "injection_settle_ms": 400,
    "window_wait_timeout_ms": 30000,
    "store_max_age_days": 30
  },
  "generated_at": "2026-08-11T09:00:00+08:00",
  "classes":  [ { "id": "2_cemerlang", "name": "2 Cemerlang", "grade": 2, "colour_index": 4 } ],
  "students": [
    {
      "id": "s_0001",
      "name": "Nur Aishah Binti Ahmad",
      "class_id": "2_cemerlang",
      "email_local": "m-12345678",
      "avatar": "kucing",
      "password": "…",                  // present only inside the encrypted payload
      "password_version": 3,
      "password_updated_at": "2026-08-01T00:00:00+08:00",
      "picture_password": {
        "algo": "argon2id",
        "salt": "base64",
        "hash": "base64"
      },
      "active": true,
      "updated_at": "2026-08-11T09:00:00+08:00"
    }
  ]
}
```

`email_local` only; the domain lives once in `school.domain` — inherited from `../Normal_SSO/Technical_Architecture_Normal_SSO.md` §4.1 and kept for the same reasons.

### 3.3 Per-PC store

`credentials.dat` is the same payload, re-serialised and passed through `ProtectedData.Protect(bytes, entropy, DataProtectionScope.LocalMachine)`, where `entropy` is a 32-byte value stored beside it and ACL'd identically. DPAPI alone would be readable by any process running as any user on that machine; the entropy file plus the ACL raises the bar to "local administrator", which is the realistic ceiling on a lab PC.

An HMAC-SHA256 over the whole blob detects tampering independently of DPAPI.

### 3.4 Decryption discipline

```csharp
// One pupil. One password. Wiped before the method returns.
using var cred = store.OpenCredential(studentId);   // ICredential : IDisposable
var ok = injector.Inject(cred.PasswordSpan);         // ReadOnlySpan<char>, never string
// Dispose() → CryptographicOperations.ZeroMemory over pinned backing store
```

- **Never decrypt the whole store into memory.** Open one record at a time.
- **Never materialise a password as `string`.** .NET strings are immutable, interned unpredictably, and cannot be reliably zeroed. Use a pinned `char[]`/`byte[]` behind a `ref struct`-shaped API.
- `GC.KeepAlive` + `GCHandle.Alloc(..., GCHandleType.Pinned)` so a compacting GC cannot leave a copy behind.
- `<ServerGarbageCollection>false</ServerGarbageCollection>` and disable tiered-PGO instrumentation of the crypto path if it complicates zeroing.
- **Disable Windows Error Reporting dumps for the process** (`ExcludedApplications` under `HKLM\...\Windows Error Reporting`, set by the installer) — a crash dump taken mid-injection contains the plaintext.
- Never log, never `ToString()`, never interpolate. A Roslyn analyzer or a banned-API list should make this mechanical rather than a matter of discipline.

### 3.5 File locations and ACLs

```
%ProgramData%\DELIMa Launcher\
  credentials.dat        SYSTEM:F, Administrators:F, <ServiceAccount>:R   ← Users: no access
  credentials.entropy    same
  audit\audit-2026-08.log  SYSTEM:F, Administrators:F, <ServiceAccount>:W (append)
  theme\                 Users:R
  assets\avatars\        Users:R
```

The Launcher runs as an ordinary interactive user, which means an interactive user *can* read the store — this is unavoidable without a service. Mitigations: DPAPI + entropy means reading the file yields nothing without local admin, and pupils are not local admins. The install guide recommends BitLocker.

---

## 4. The injection engine

This is the part v1 got wrong, and the part the spike exists to prove.

### 4.1 What v1 specified, and why it fails

```csharp
Process.Start(chrome, url);
Thread.Sleep(1500);
SendKeys.SendWait(password);   // ← two independent, silent failures
SendKeys.SendWait("{ENTER}");
```

1. **`SendKeys` parses `+ ^ % ~ ( ) { } [ ]` as control syntax.** `Murid+2026` sends Shift+`2026`. Every affected pupil fails with no diagnosable cause.
2. **1,500 ms is a guess.** Chrome cold-start on lab hardware is routinely 4–8 s. When it loses, a child's password is typed in plaintext into the URL bar, a Word document, or the desktop.

The spike measures both. `InjectionSpike/README.md` predicts the shape of the result; **that prediction is not a result.**

### 4.2 What production does instead

```
launch  →  wait for verified window  →  settle  →  block input
        →  inject via SendInput/KEYEVENTF_UNICODE  →  verify  →  unblock  →  wipe
```

**Window verification** — poll `GetForegroundWindow()` at 100 ms and require *all* of:

- window class `Chrome_WidgetWin_1`
- PID belongs to the process tree this launch started (not the teacher's Chrome)
- title matches the expected Google sign-in page for the current locale

Never a bare sleep. Timeout (default 30 s, configurable) → abort to the `Ralat` screen, inject nothing.

**Injection** — `SendInput` with `KEYEVENTF_UNICODE`, one `INPUT` pair per UTF-16 code unit, surrogate pairs sent as two units. This is codepoint-transparent and immune to the `SendKeys` parsing problem entirely.

**Input blocking** — `BlockInput(true)` for the duration so a stray click cannot move focus mid-password. `BlockInput` requires elevation on some images; if denied, fall back to a topmost transparent overlay covering all monitors, and record `blockinput_denied` in the audit log. The spike already reports this condition.

**Never send `{ENTER}` blind.** Either confirm the field accepted the expected length, or let the pupil press Enter — a design choice worth testing with real seven-year-olds, since one keystroke may be easier for them than for the app.

**Abort path.** The `Sedang Masuk` screen carries a visible cancel. Cancelling stops injection, kills the process tree, wipes the profile, and zeroes the credential.

### 4.3 Injectable character set

Step 4 of the wizard warns on passwords outside the set the engine is proven to handle. That set is defined by **T0.3's actual results**, not by assumption. Until the spike runs, this section has a hole in it, and that hole is the reason Phase 0 comes first.

Expected: `SendInput`/UNICODE handles the full BMP, so the warning should end up applying to nothing. If it does not, the finding is far more interesting than the app.

### 4.4 Session isolation

Straight from Gap Analysis §1.5, and already implemented in the spike:

- `--user-data-dir=%TEMP%\delima_session_{guid}` — throwaway profile per pupil, deleted on reset.
- Additional flags: `--no-first-run`, `--no-default-browser-check`, `--disable-features=PasswordManager`, `--password-store=basic`.
- **Teardown:** `CloseMainWindow()` first; `taskkill /T /F /PID <pid>` only on timeout. **Never `/IM chrome.exe`** — that kills the teacher's browser and corrupts its profile, producing a "Restore pages?" prompt that breaks the next launch and may restore the previous pupil's tabs.
- Profile directory deleted after teardown; failure to delete is logged, not silent.

### 4.5 The handoff URL

v1's `AccountChooser?Email=` is legacy and contradicts v1's own §2, which specifies `login_hint`. Resolve by **T0.2, against the live portal**, before writing this code. Both candidates:

```
# Account chooser (Normal_SSO §5.2, in use today)
https://accounts.google.com/AccountChooser?Email=<email>&hd=<domain>&continue=<dest>

# OAuth 2.0 login_hint (documented, needs a school Cloud project)
https://accounts.google.com/o/oauth2/v2/auth?...&login_hint=<email>&hd=<domain>&prompt=login
```

Destination URLs are **configuration, not code** (§3.2 `config.destinations`), so a Google change is a bundle rebuild rather than a release.

---

## 5. Roster logic carried over from Normal_SSO

Reimplemented in C#, semantics unchanged — `../Normal_SSO/Technical_Architecture_Normal_SSO.md` §4.3 is the reference:

- **Display names.** Longest form that fits two lines at the current card width, with the unique calling name as the floor. Must handle Malay (`Nur Aishah` **Binti** Ahmad), Indian (`Arjun` **A/L** Kumaran) and Chinese (Tan `Wei Ming` — surname first) conventions. Getting this wrong silently mislabels a whole demographic. Computed once at import, cached in the bundle.
- **Duplicate disambiguation.** Calling name + initial, within a class. No two cards may read alike.
- **Grid sizing.** Rows fixed at 5; columns 7/8/9 by class size; card height constant, width varies. Degrades to vertical scroll below ~11 characters per line (PRD §7.2).
- **Search** filters on the full name even when an abbreviated form is displayed.

`IRosterStore` mirrors `RosterStore` from the web product so the two stay conceptually aligned and the display-name test fixtures can be shared.

---

## 6. Failure taxonomy

Gap Analysis §3 requires this and v1 has none. Every failure gets a calm BM message for the pupil and a code for the teacher. The pupil never sees a code; the teacher never has to guess.

| Code | Condition | Pupil sees (BM) | Teacher action |
| :--- | :--- | :--- | :--- |
| `E01` | Chrome not installed / path unresolvable | Alamak, ada masalah. Panggil cikgu. | Install Chrome |
| `E02` | Window not verified before timeout | Cuba lagi. | Slow PC — raise `window_wait_timeout_ms` |
| `E03` | Injection aborted by pupil | *(returns to name grid)* | None |
| `E04` | Wrong password at Google | Kata laluan tidak betul. Panggil cikgu. | Update via Mod Guru; check `password_version` |
| `E05` | Password stale (`password_version` behind bundle) | Kata laluan sudah tukar. Panggil cikgu. | Re-import + re-provision |
| `E06` | Google CAPTCHA / "unusual activity" | Tunggu sekejap, cuba lagi. | Space out launches; known limitation |
| `E07` | 2SV prompt | Panggil cikgu. | Escalate — this may end the product |
| `E08` | Account suspended / password expired | Panggil cikgu. | MOE admin task |
| `E09` | Store decrypt failure | Alamak, ada masalah. Panggil cikgu. | Re-provision this PC |
| `E10` | Store stale beyond `store_max_age_days` | Panggil cikgu. | Re-provision this PC |
| `E11` | No password stored for this pupil | Panggil cikgu. | Complete wizard Step 4 |
| `E12` | Picture password locked (5 failures) | Tunggu 5 minit. | Reset via Mod Guru |
| `E13` | Network unreachable | Tiada internet. Panggil cikgu. | Network |

`E06` and `E07` are the two the product cannot engineer around. They are listed so their appearance is a legible finding rather than a mystery.

---

## 7. Audit log

Append-only, local, one file per month, in `%ProgramData%\DELIMa Launcher\audit\`.

**Recorded:** timestamp, `student_id`, `device_id`, `school_code`, outcome code, duration ms, software version, store version.
**Never recorded:** the password, the picture-password hash, any key, the passphrase, or a full email address.
**Also recorded, from the Admin side:** wizard step completions, the Step 4 consent acknowledgement, password-reveal events, picture-password disablement.

Default retention 12 months, configurable. Rotation to a network share is optional and off by default. `Mod Guru → Diagnostik` exports a redacted bundle a coordinator can email for support.

This is what tells a school whether impersonation is happening, and it is not optional for software handling minors' accounts.

---

## 8. Kiosk hardening

Gap Analysis §1.6 — a curious nine-year-old finds all of these in week one.

**Chrome, via `HKLM\SOFTWARE\Policies\Google\Chrome` (installer, opt-in):**

| Policy | Value | Stops |
| :--- | :--- | :--- |
| `PasswordManagerEnabled` | 0 | "Save password?" and `chrome://settings/passwords` |
| `DeveloperToolsAvailability` | 2 | F12 |
| `IncognitoModeAvailability` | 1 | Ctrl+Shift+N bypassing session logic |
| `BrowserSignin` | 0 | Profile sign-in persisting credentials |
| `URLAllowlist` | `accounts.google.com`, `*.delima.edu.my`, `classroom.google.com` | Wandering |
| `URLBlocklist` | `*` | Everything else |

**Launcher:** low-level keyboard hook suppressing Alt+Tab, Win, Alt+F4 while a session is active; topmost borderless window; `Ctrl+Shift+Esc` cannot be blocked from user mode — accept it, and rely on Windows kiosk/shell replacement where the school will tolerate it.

**Not solvable in software:** the 👁 reveal icon on Google's own password field. A pupil who taps it sees the injected password. Mitigations are all partial — inject, submit immediately, and keep the window brief. Worth stating plainly in the install guide rather than pretending otherwise.

---

## 9. Provisioning

`Delima.Provision.exe`, run once per lab PC:

1. Read `school.dlmpack` from USB or UNC path.
2. Prompt for the admin passphrase (memory only, zeroed on exit).
3. Argon2id → decrypt → validate HMAC and schema version.
4. Re-wrap with DPAPI `LocalMachine` + entropy → write `credentials.dat`.
5. Write `device_id` (GUID, first run only) and the store date.
6. Append to the lab checklist file on the share, if present.
7. Zero everything. Exit code 0/non-zero for scripting.

Silent mode `--quiet --pack <path> --passphrase-stdin` for the PowerShell route, so PDQ/GPO can drive 40 PCs from one prompt. The passphrase is read from stdin and never appears in a command line or a process list.

---

## 10. Testing

| Layer | Approach |
| :--- | :--- |
| Crypto | Known-answer tests; round-trip; **tamper tests** — flip one byte in header, ciphertext, and tag; assert authentication failure in all three. |
| Zeroing | Allocate, use, dispose, scan the pinned region for residue. |
| Display names | Fixture set covering Malay, Chinese and Indian conventions, plus collisions at 3 card widths. Shared with `Normal_SSO`. |
| Importer | **Real APDM exports** — ANSI, UTF-8 BOM/no-BOM, UTF-16, CRLF/LF, diacritics, blank rows, duplicate IDs, `m-` prefixed and bare IDs. Fixtures are the deliverable here, not the tests. |
| Injection | The spike, on **representative lab hardware**, ≥ 50 runs per method. Never on a developer machine, never over RDP. |
| Window verification | Adversarial: launch, then steal focus at 500/1000/2000/4000 ms; assert **zero** keystrokes are sent. |
| Teardown | Assert the teacher's separate Chrome survives; assert the temp profile directory is gone. |
| Picture password | Lockout after 5; shuffle produces a different layout each attempt; Argon2id verification. |
| Installer | Clean Win10 1809 and Win11 VMs; upgrade over 2.0.0 → 2.0.1 preserving the store; uninstall wipes it. |
| End to end | One class, real accounts, lab hardware, teacher present. |

The window-verification adversarial test is the one that matters most: it is the test that proves a child's password is never typed into the wrong window.

---

## 11. Build sequence

| # | Deliverable | Depends on |
| :-- | :--- | :--- |
| 1 | **Compile and run InjectionSpike (T0.3)** | Lab hardware. **Blocks everything.** |
| 2 | Confirm SSO entry URL (T0.2) | One real pupil account |
| 3 | `Delima.Core` — store format, crypto, tamper tests | 1 |
| 4 | `Delima.Core` — roster model, display names, port fixtures from Normal_SSO | — |
| 5 | `Delima.Admin` — importer, column mapper, encoding detection, dry run | 3, 4 |
| 6 | `Delima.Admin` — wizard steps 1–7 | 5 |
| 7 | `Delima.Provision` | 3 |
| 8 | `Delima.Win32` — promote spike code, add window verification + abort | 1 |
| 9 | `Delima.Launcher` — theming, class + name screens | 4 |
| 10 | `Delima.Launcher` — picture password | 3, 9 |
| 11 | `Delima.Launcher` — injection flow, failure taxonomy, floating reset bar | 8, 10 |
| 12 | Audit log | 11 |
| 13 | Mod Guru | 11, 12 |
| 14 | Kiosk hardening + Chrome policy | 11 |
| 15 | Inno Setup script, signing, guides | all |

Steps 1 and 2 are days. Everything after them is weeks. Doing them in the other order is the most expensive mistake available here.

---

## Appendix A — Dependencies

| Package | Purpose | Licence |
| :--- | :--- | :--- |
| `Konscious.Security.Cryptography.Argon2` | Argon2id | MIT |
| `CsvHelper` | CSV parsing | MS-PL / Apache-2.0 |
| `ClosedXML` or `ExcelDataReader` | `.xlsx` import | MIT |
| `UTF.Unknown` | Encoding detection | MIT |
| `CommunityToolkit.Mvvm` | WPF MVVM | MIT |
| `System.Security.Cryptography.ProtectedData` | DPAPI | MIT |

AES-256-GCM, HMAC-SHA256 and the CSPRNG come from the BCL. **No custom cryptography anywhere.** Keep the dependency list this short — every package is something an ICT coordinator's antivirus may flag on an unsigned build.

## Appendix B — Config reference

| Key | Default | Notes |
| :--- | :--- | :--- |
| `picture_password_required` | `true` | `false` reintroduces blocker B1; warned and logged |
| `idle_reset_seconds` | `600` | Auto-logout, profile wipe |
| `injection_settle_ms` | `400` | After window verification, before first keystroke |
| `window_wait_timeout_ms` | `30000` | Then `E02` |
| `store_max_age_days` | `30` | Then `E10` |
| `force_signout` | `true` | Lab only; never at home |
| `enter_mode` | `manual` | `manual` \| `auto` — auto only after length verification |
| `audit_retention_months` | `12` | |
| `language` | `ms` | `ms` \| `en` |
