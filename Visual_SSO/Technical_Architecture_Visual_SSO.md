# Technical Architecture — DELIMa Smart Launcher v2 (C# WPF, Multi-School)

**Companion to:** `PRD_Visual_SSO_v2.md`
**Date:** 11 August 2026
**Target:** **.NET 10 (LTS)**, WPF, Windows 10 1809+ / Windows 11, x64
**Status:** Specification only.

> **Runtime version — corrected.** Earlier drafts said .NET 8, inherited from the spike. **.NET 8 reaches end of support on 10 November 2026** — roughly three months from this revision, and well before this product would ship. Building on it would mean a forced migration mid-project and shipping an unsupported runtime to schools, which is indefensible for software handling children's credentials.
>
> Target **.NET 10 LTS** (released 11 November 2025, supported to 14 November 2028). The spike was originally written against .NET 8 as a throwaway harness, but has since been retargeted to .NET 10 as well — one less version mismatch to reason about between the spike and the production code in §2, and .NET 8 was going to be a dead end within months regardless.

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
│   ├── Delima.Win32/           net10.0-windows, P/Invoke only
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

`credentials.dat` is the same payload, re-serialised and passed through `ProtectedData.Protect(bytes, entropy, DataProtectionScope.LocalMachine)`, where `entropy` is a 32-byte value stored beside it and ACL'd identically.

An HMAC-SHA256 over the whole blob detects tampering independently of DPAPI.

**State precisely what this buys, because it is easy to overstate.** `LocalMachine` scope means the key is derived from machine state, so:

- A `credentials.dat` copied to **any other machine** is undecryptable. This is the protection that matters for a stolen or decommissioned PC, and it is strong.
- On **that** machine, any process that can read both files can call `ProtectedData.Unprotect` and recover every password. DPAPI machine scope is not a defence against a local user — it never was.

Since the Launcher must read the store, the account the Launcher runs as must have read access, and on a lab PC that is the shared pupil Windows account (§3.5). The honest conclusion: **anyone who can obtain an interactive session on a lab PC and run arbitrary code gets the whole school's passwords.** Everything protecting against that is outside the crypto — execution control, kiosk lockdown, and physical access. Design accordingly, and say so to the school rather than implying the file is safe because it is encrypted.

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

The account model has to be stated before the ACLs mean anything. A lab PC runs a **shared pupil Windows account** (`Murid` or equivalent), and the Launcher runs in that account's interactive session — it must, because it draws a UI. Running it as a separate service account is not available: Session 0 isolation means a process running as another user cannot render to the pupil's desktop.

So the account that must read `credentials.dat` is the same account a pupil is sitting in front of.

```
%ProgramData%\DELIMa Launcher\
  credentials.dat        SYSTEM:F, Administrators:F, Murid:R   ← all other users: no access
  credentials.entropy    same
  audit\audit-2026-08.log  SYSTEM:F, Administrators:F, Murid:W (append-only, no delete)
  theme\                 Users:R
  assets\avatars\        Users:R
```

The ACL therefore keeps the store away from *other* accounts and from a plain file copy off the machine. It does not, and cannot, keep it away from code running as `Murid`.

**The mitigations that actually apply are all outside the crypto:**

| Control | Stops |
| :--- | :--- |
| **AppLocker / SRP** — allow execution only from `%ProgramFiles%` and `%SystemRoot%` | A pupil running PowerShell, a portable binary, or anything from USB or `%TEMP%`. **This is the single most load-bearing control on the list** and belongs in the install guide as a requirement, not a suggestion. |
| Windows kiosk / assigned access | Reaching a shell at all |
| Deny `Murid` write access to `%ProgramFiles%\DELIMa Launcher` | Replacing the Launcher binary with one that dumps the store |
| BitLocker | Offline disk access; drive removal |
| Append-only audit ACL | A pupil covering their tracks |

**Residual risk, stated plainly for the school:** a person with an interactive session on a lab PC who can execute code holds every pupil's password for that school. Not a nine-year-old — but a bored teenager, a visiting contractor, or a member of staff. This risk is inherent to any product that stores replayable passwords on shared hardware; it is not a flaw in this implementation, and it does not have a technical fix. It is the strongest single argument for `../Normal_SSO/` and belongs in front of the headmaster when they ask "is it safe?".

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

**Input blocking** — `BlockInput(true)` for the duration so a stray click cannot move focus mid-password. `BlockInput` requires elevation, and **the topmost-overlay fallback is not a contingency — it is required for every standard lab deployment.** T0.3 confirmed this directly: across two 50-run fidelity batches on real lab hardware, `BlockInput` returned `false` (denied) on 100% of runs — the Launcher will not run elevated on a shared pupil account, so this is the normal case, not an edge case. The gap this leaves was also demonstrated directly, not theoretically: during the adversarial test, stealing focus *before* injection began produced zero leaked keystrokes (window verification working as designed), but stealing focus *during* active injection let the remaining characters land in the stolen window. Window verification protects the moment injection starts; only the overlay protects the moment it is running. Ship both. Record `blockinput_denied` in the audit log regardless — it is expected, not a fault to chase.

**Never send `{ENTER}` blind.** Either confirm the field accepted the expected length, or let the pupil press Enter — a design choice worth testing with real seven-year-olds, since one keystroke may be easier for them than for the app.

**Abort path.** The `Sedang Masuk` screen carries a visible cancel. Cancelling stops injection, kills the process tree, wipes the profile, and zeroes the credential.

### 4.3 Injectable character set — resolved by T0.3

**No restriction needed.** T0.3 ran against real lab hardware on 17 August 2026: `SendInput`/`KEYEVENTF_UNICODE` passed **100/100** across two independent 50-run batches, covering all twelve test passwords including `all-reserved` (`M+u^r%i~d(2){0}[26]`, every reserved character in one string) and the realistic `moe-style` shape. Zero failures, zero exceptions, both batches.

The control confirmed the mechanism behind the original bug at the same time: a clean 50-run `SendKeys.SendWait` batch on the same hardware showed the three plain passwords passing every time and all nine reserved-character passwords failing every time — eight via silent length/content corruption (`LEN_9_OF_10` and similar), and `Murid{2026}` via an outright `ArgumentException` (`Keyword "2026" is not valid`) rather than mistyping. A real MOE password containing `{` would have **crashed** the v1 design mid-lesson, not merely logged a pupil in wrong.

Wizard Step 4's warning threshold (PRD §6, Step 4) can therefore be removed rather than tuned — there is no character class `SendInput` is known to mishandle. Keep the length/shared-password warnings; drop the character-set one.

One run (of four total across the session) produced anomalous results on `SendKeys` — even plain passwords failed with `NO_VERDICT_TIMEOUT` — roughly 14 minutes after a clean run on the same machine, cause undetermined (possibly antivirus or a background scan). It does not affect this section's conclusion, since it was `SendKeys` (the rejected method) and `SendInput`'s own two batches were unaffected and fully clean. Worth a light look before the pilot, not a blocker now.

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

## 6. UI implementation — WPF specifics

§1 says everything not specified here "is a normal WPF application and should be built as one." That line covers ordinary MVVM plumbing but leaves a real gap: PRD §7 adopts `../Normal_SSO/stitch-wireframes/PROMPT.txt` — a design system written for a **web** Stitch mockup — as the visual spec for a **WPF** app. Translating CSS custom properties, web fonts, and a native `<select>`-replacement into XAML is not "normal WPF," so it gets specified here.

### 6.1 Hand-styled WPF, not a component library

**Do not adopt a third-party WPF UI kit** (WPF-UI, MaterialDesignInXAML, HandyControl, ModernWpf) as the basis for this app's look. Reasoning:

- PROMPT.txt's requirements don't match any kit's default visual language — non-standard corner radii (20 px large cards, 16 px small), an adaptive card grid whose column count is computed from class size (§6.3 below), a dropdown that is explicitly *not* a native control and carries 64 px rows with a colour swatch, and an explicit ban list (no gradients on text, no glassmorphism, no dark mode). Every one of these fights a kit's own theme (Fluent, Material, Office) rather than being helped by it.
- Appendix A already commits to keeping dependencies short because "every package is something an ICT coordinator's antivirus may flag on an unsigned build." A UI kit is a large dependency for a benefit — generic chrome — this app doesn't need, since virtually every visible control is bespoke to this design regardless of what kit sits underneath it.
- WPF's native controls are fully retemplatable via `ControlTemplate` and `ItemContainerStyle` without losing their behaviour. Retemplating `ComboBox` for the Tahun/Kelas dropdowns (§6.4) keeps keyboard navigation, focus visuals, and screen-reader support that a from-scratch `Popup`-based control would have to reimplement by hand — and accessibility is a requirement here (§6.6), not a nice-to-have.

Build a small `Delima.Launcher/Theming/` set of `Styles.xaml` + `ControlTemplates.xaml` resource dictionaries instead. This is more upfront work than pulling in a kit, and it is the correct trade for a design this specific.

### 6.2 Theming as data, not as a compiled resource

`config.theme` (§3.2) carries `primary`, `accent`, and eight `class_colours` per school — this is meant to be **swapped per school**, the same way the web design assumes CSS custom properties can be swapped. A `ResourceDictionary` compiled into the binary can't do that; brushes must be constructed at runtime from the decrypted bundle and merged into `Application.Resources` before the first window shows.

```
Theming/
  DefaultTheme.xaml       fallback palette (SK Seksyen 24's, per PRD §7.1), used only if the bundle is unreadable
  ThemeBuilder.cs         config.theme -> ResourceDictionary at runtime (SolidColorBrush per token, keyed by name)
  Tokens.cs                the token names themselves: PrimaryBrush, AccentBrush, ClassColour[0..7],
                            SurfaceBrush, SoftSurfaceBrush, BorderBrush, PrimaryTextBrush, SecondaryTextBrush
```

`ThemeBuilder` is also where the contrast validation from PRD §6 Step 1 (FR-S1.4) actually runs against real brush values — reject a palette that fails 4.5:1 for the text it will carry, not just at wizard-authoring time but again defensively when the Launcher loads a bundle, in case a hand-edited or corrupted bundle slips through.

### 6.3 The adaptive name grid

PRD §7.2 and arch §5 specify rows fixed at 5, columns following class size (7 for 30–34 pupils, 8 for 35–39, 9 for 40–44), constant card height, varying card width. This is naturally a `ViewModel`-computed `int ColumnCount` property — not a XAML-only layout — bound to an `ItemsControl` whose `ItemsPanel` is a `UniformGrid` with `Columns="{Binding ColumnCount}"`. The below-1200px-width degrade to vertical scroll (PRD §7.2, "a known limit at 1024×768") is a `Trigger` on the window's `ActualWidth`, swapping the `UniformGrid` panel for a `WrapPanel` inside a `ScrollViewer` — do not implement this as two entirely separate views; the card `DataTemplate` must be identical in both so a pupil sees the same card regardless of which layout is active.

### 6.4 The non-native dropdown

PRD FR-1a is explicit: **never a native `<select>`-equivalent**. In WPF terms, that means never a bare `ComboBox` with its default `ComboBoxItem` styling — but it does mean a **retemplated** `ComboBox` (§6.1), because rebuilding open/close, keyboard arrow navigation, typeahead, and `AutomationPeer` support from a `Popup` is a lot of accessibility surface to reimplement correctly for very little visual gain over retemplating. Style target: closed state 72 px tall; open, each row 64 px with 20 px text and a `Rectangle`/`Ellipse` colour swatch bound to the class's `ColourIndex` (§Roster/ClassInfo). `IsEnabled="{Binding TahunSelected}"` on the Kelas dropdown covers FR-1c (disabled until a tahun is chosen) directly through binding rather than code-behind.

### 6.5 Fonts

Nunito, Quicksand, and Baloo 2 (PROMPT.txt) are Google Fonts (OFL-1.1 licensed — free to embed and redistribute) and **are not present on a lab PC by default**. They must ship as embedded resources, not assumed system fonts:

```
Assets/Fonts/
  Nunito-Regular.ttf, Nunito-Bold.ttf
  Baloo2-Bold.ttf     (headings, per PROMPT.txt's 40px/28px/28px weights)
```

Referenced via a pack URI FontFamily (`/Delima.Launcher;component/Assets/Fonts/#Nunito`) set once in `App.xaml`'s base `TextBlock`/`Control` style, not per-view. Verify the exact font files' licence file is retained in the repo alongside them (`OFL.txt` per font family) — a small thing, but the kind of thing that gets asked about during a school procurement review.

### 6.6 Motion — restrained, and WPF-native

The audience is 7–9-year-olds on kiosk hardware; motion should confirm what happened, not entertain. WPF's `Storyboard` + `CubicEase`/`BackEase` easing functions are sufficient — there's no need for a physics-based spring animation library here (that's a web/Skia-adjacent concern; the equivalent WPF tooling doesn't carry the same ecosystem weight and isn't worth the dependency). Concretely:

- Screen transitions: a short (150–200 ms) cross-fade or slide, matching PROMPT.txt's "calm, orderly" instruction — never a bounce or an attention-seeking entrance.
- The floating **Selesai** reset bar (PRD §7.4): minimize/restore is the one moment worth a slightly longer, clearly legible transition, since it's the pupil's confirmation that their session ended.
- Picture-password shuffle (PRD §7.3): the 16-icon grid re-shuffling on each attempt should happen instantly, with no animation — an animated shuffle would let a classmate watching over a shoulder track icons *through* the transition, which defeats the whole point of shuffling.

### 6.7 Accessibility — a gap this document is closing

`../Normal_SSO/PRD_Normal_SSO.md` §7 requires WCAG 2.2 AA, 4.5:1 contrast, ≥48×48 px touch targets, full keyboard navigation, and BM screen-reader labels for the web product. **`PRD_Visual_SSO_v2.md` never restates this for the desktop app**, and it should — the pupil audience is identical, and Mod Guru (teacher-mode admin) is used standing at a shared kiosk PC where keyboard-only operation matters just as much. Treat the same bar as binding here: `AutomationProperties.Name` in Bahasa Melayu on every interactive element, `TabIndex` ordering that matches visual flow, and the retemplated controls in §6.4 chosen specifically because they keep this "for free" rather than requiring a second accessibility pass later.

### 6.8 `Delima.Admin` — a deliberately different visual language

§6.1–6.7 specify `Delima.Launcher`, used by seven-year-olds on a kiosk. `Delima.Admin` — the School Setup Wizard (PRD §6) — is used by En. Zul, an adult ICT coordinator, sitting at a desk, doing data-entry and validation work: mapping spreadsheet columns, reading a 2,014-row dry-run report, reconciling reject rows. **Reusing the Launcher's rounded, candy-coloured, 48 px-touch-target design system here would actively hurt this task** — a picture-book aesthetic fights information density exactly where density is the job. This was previously unstated; it needs to be a deliberate choice, not a default.

**What carries over:** the crest, the school's primary colour used *only* as a thin accent (selected nav item, primary button), Nunito for body text (readable, still on-brand), and the `../../Normal_SSO/stitch-wireframes/PROMPT.txt` ban list (no gradients, no glassmorphism, no dark mode — those bans exist independent of audience).

**What does not carry over:** 20 px card radii shrink to the ordinary WPF/Fluent-adjacent 6–8 px; 48 px touch targets shrink to normal desktop control heights (32–36 px row height, 28–32 px button height); body copy runs 13–14 px, not 16–18 px; and the layout is a conventional **left-sidebar step navigator + right content pane**, not full-bleed illustrated screens. Grids use alternating-row striping and monospace (`--font-mono`-equivalent, a fixed-width font) for anything columnar — pupil IDs, row numbers — because a coordinator scanning 2,000 rows for a misaligned digit needs fixed-width alignment, not warmth.

**Navigation pattern.** A persistent left sidebar lists all 7 steps with their state (`not started` / `in progress` / `done` / `needs attention`, the last for e.g. unresolved reject rows). **On first run, steps 2 onward are locked** until the preceding step completes — there is nothing to map columns *into* before Step 1 sets the school identity, and no roster to attach passwords to before Step 3 completes. **Once a school has completed setup once, every step unlocks for direct navigation** — Step 3 (roster refresh) and Step 4 (password rotation) are re-entered on entirely different schedules (termly vs monthly, PRD §6.3), and forcing a coordinator back through Steps 1–2 to reach Step 4 in March would be a real, remembered annoyance.

**The column mapper (Step 3), concretely.** Not "click a column header and pick its meaning" — with APDM exports running 15–30 columns wide, that puts a decision on every column, most of which are irrelevant. Instead: a **fixed list of the five target fields** (Nama penuh, Kelas, Tahun, ID DELIMa, No. KP/register — PRD §6 Step 3's table), each with a dropdown listing the source file's column headers, defaulting to a best-guess match by header-name similarity. Below the mapping list, a **live preview table** of the first 10 rows re-renders as each dropdown changes, so the coordinator sees actual data flow into place rather than trusting an abstract mapping. Required fields with no mapping yet block the "Seterusnya" button with an inline reason, not a disabled button with no explanation (§6.1's "restraint" principle from the CDS content guidance applies here too: say what's wrong, don't just refuse).

**The dry-run report (Step 3), concretely.** A full step of its own, not a modal — the numbers matter enough to want back/forward history and a printable record. Three collapsible sections: **Sedia diimport** (the clean count, collapsed by default), **Amaran** (warnings that don't block — duplicate IDs, unknown classes — expanded by default, each row showing the row number and the specific problem), and **Ditolak** (hard rejects — malformed IDs, empty required fields — expanded by default). Two actions: **Import & Sahkan** (primary, proceeds with the valid rows; warnings are accepted as-is with the documented resolution — e.g. "first occurrence kept" for duplicates) and **Muat Naik Fail Lain** (secondary, returns to file selection). This maps directly to the console-style report already specified in PRD §6 Step 3 — the report here is that same content, laid out as a screen instead of a text block.

**The password grid (Step 4), concretely.** A table: pupil name, class, DELIMa ID, and a password-status column showing `••••••` (set), a muted "Tiada" pill (not set — PRD §6 Step 4 already specifies this pupil shows a "belum siap" state, not a failure), or a small amber "Dikongsi" badge (this exact password value appears elsewhere in the import — the shared-password warning from PRD §6 Step 4). **Reveal is per-row, not per-grid**: clicking the masked value prompts for the admin passphrase inline (not a full dialog — a small popover anchored to the row, so the context of *which* pupil is never lost), and successful reveal writes `{student_id, Windows user, timestamp}` to the audit log per arch §8. A revealed value auto-re-masks after 10 seconds or on losing focus, whichever first — this is a working screen a coordinator might step away from, and a password sitting revealed on an unattended monitor is exactly the kind of residual risk §3.4's "never linger in memory" discipline is trying to close at the crypto layer; the UI layer needs the same discipline.

**The consent screen (Step 4, FR-S4.1), concretely.** Plain heading ("Sebelum meneruskan"), then four short statements in sequence — what will be stored (passwords, encrypted), where (this PC and this PC only, arch §3.3), who can read it (nobody without the admin passphrase and local admin access to this machine, arch §3.3's honest residual-risk statement), and who is responsible (the school, not the software author, PRD §8.5's licence stance). Below that, a text field labelled "Taip kod sekolah untuk teruskan" pre-filled with nothing (typing the actual school code, not a checkbox, is the deliberate friction PRD §6 Step 4 calls for — a checkbox gets clicked without reading; typing a specific string requires having read *something*). The "Teruskan" button stays disabled with an inline reason until the typed value matches.

**Provisioning route selector (Step 7), concretely.** Three cards side by side — USB, Rangkaian (network share), Skrip (scripted) — each a summary (one line: what it needs, e.g. "Pendrive kosong, 1 GB+") plus a "Pilih" button. Selecting one replaces the cards with that route's own sub-panel (USB: a drive picker + "Tulis ke Pendrive" button; Rangkaian: a UNC path field + validation that it's reachable; Skrip: a generated PowerShell snippet with a "Salin" copy button and a one-line explanation that the passphrase is prompted at run time, never embedded in the script). All three routes end at the same lab checklist print preview (PC name, provisioned yes/no, version, store date — PRD §6 Step 7), generated identically regardless of route.

---

## 7. Failure taxonomy

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

## 8. Audit log

Append-only, local, one file per month, in `%ProgramData%\DELIMa Launcher\audit\`.

**Recorded:** timestamp, `student_id`, `device_id`, `school_code`, outcome code, duration ms, software version, store version.
**Never recorded:** the password, the picture-password hash, any key, the passphrase, or a full email address.
**Also recorded, from the Admin side:** wizard step completions, the Step 4 consent acknowledgement, password-reveal events, picture-password disablement.

Default retention 12 months, configurable. Rotation to a network share is optional and off by default. `Mod Guru → Diagnostik` exports a redacted bundle a coordinator can email for support.

This is what tells a school whether impersonation is happening, and it is not optional for software handling minors' accounts.

---

## 9. Kiosk hardening

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

**Execution control — the one that protects the credential store.** AppLocker (or SRP on Home/Pro images without AppLocker) restricting execution for the pupil account to `%ProgramFiles%` and `%SystemRoot%`. Per §3.5 this is what stands between a lab session and the whole school's passwords, so unlike the Chrome policies it is **required, not optional**, and the install guide must say so in those words. The installer cannot apply it reliably across every school's image; it ships as a documented GPO/`Set-AppLockerPolicy` snippet that the coordinator applies and confirms in the lab checklist.

**Not solvable in software:** the 👁 reveal icon on Google's own password field. A pupil who taps it sees the injected password. Mitigations are all partial — inject, submit immediately, and keep the window brief. Worth stating plainly in the install guide rather than pretending otherwise.

---

## 10. Provisioning

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

## 11. Testing

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

## 12. Build sequence

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
| 9 | `Delima.Launcher` — theming tokens, embedded fonts, retemplated dropdown, class + name screens (§6) | 4 |
| 10 | `Delima.Launcher` — picture password | 3, 9 |
| 11 | `Delima.Launcher` — injection flow, failure taxonomy, floating reset bar | 8, 10 |
| 12 | Audit log | 11 |
| 13 | Mod Guru | 11, 12 |
| 14 | Kiosk hardening + Chrome policy | 11 |
| 15 | Inno Setup script, signing, guides | all |

Steps 1 and 2 are days. Everything after them is weeks. Doing them in the other order is the most expensive mistake available here.

Step 15 is specified in full in `Build_And_Release.md` — publish flags, the `.iss` script, signing order, and a pre-release checklist. It is worth reading before step 3 rather than at step 15: self-contained single-file WPF forbids trimming and constrains how embedded fonts and theme resources are loaded (§6.2, §6.5), and those are decisions made early and expensive to revisit.

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
