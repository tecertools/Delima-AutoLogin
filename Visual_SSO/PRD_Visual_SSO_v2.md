# PRD — DELIMa Smart Launcher v2 (Windows Desktop, Multi-School)

**Product:** A Windows desktop application for primary-school computer labs that takes a pupil from a name card to a signed-in DELIMa session without typing an email address.
**Version:** 2.0 (Draft)
**Date:** 11 August 2026
**Owner:** King
**Status:** Specification only — no implementation authorised by this document.
**Supersedes:** `../DELIMa_Visual_SSO_PRD_TechArch.md` (v1, single-school)
**Companion:** `Technical_Architecture_Visual_SSO.md`
**Incorporates:** `../PRD_Gap_Analysis.md` — every blocker in that review is resolved here or explicitly deferred with a reason.
**Sibling product:** `../Normal_SSO/` — the web launcher that prefills the email and stores **no** passwords. Read §2.3 before building this one.

---

## 1. What changed from v1, and why

v1 was written for one school, one lab, one administrator who was also the author. Three of the four changes requested for v2 break that assumption:

| # | v2 requirement | What it actually changes |
| :-- | :-- | :--- |
| 1 | Distribute to other schools | Nothing school-specific may be compiled into the binary. Needs an installer, a branding layer, a versioning story, and code signing. Also converts a personal tool into **shipped software handling minors' credentials at other institutions** — see §2. |
| 2 | Schools import their own DELIMa emails **and passwords** | Needs a first-class Admin application with a setup wizard, a CSV importer that survives real APDM exports, and a provisioning path to lab PCs. This is the largest new surface in v2. |
| 3 | Keep the UI | Adopted — the `../Normal_SSO/stitch-wireframes/PROMPT.txt` design system is carried over verbatim, **except** that its colours are SK Seksyen 24's crest colours and must become themeable. See §7.1. |
| 4 | Push to git | Repository initialised; see `../README.md`. |

Requirement 3 is the only one that is nearly free. Requirements 1 and 2 together are the whole of v2.

---

## 2. The gate before any of this gets built

### 2.1 Phase 0 was never completed

`../PRD_Gap_Analysis.md` §5 defined three de-risking tasks. Their current state:

| Task | Question it answers | State |
| :-- | :--- | :--- |
| **T0.1** | Written ToS/policy position from BSTP or state ICT on storing and replaying pupil passwords | **Not started.** No document in the repository. |
| **T0.2** | Is the live `d3.delima.edu.my` SSO entry URL confirmed, and is `login_hint` honoured end to end? | **Not started.** `InjectionSpike/Program.cs` still carries a placeholder default with a comment saying so. |
| **T0.3** | Does injection actually work — 50 runs on lab hardware with reserved characters? | **Passed, 17 August 2026.** `SendInput` 100/100 across two independent 50-run batches on real lab hardware, including every reserved character combined in one password. `SendKeys` control failed exactly as predicted on the same hardware — corrupted 8 of 9 reserved-character passwords, crashed outright on the ninth. See arch §4.2–4.3 for the full result and one open follow-up (a topmost-overlay requirement, now confirmed rather than theoretical). |

**T0.3 no longer blocks.** `SendInput` reliably delivers a password into a verified Chrome window on real lab hardware — the question this whole document was contingent on is answered. **T0.1 is now the active blocker**, and it is slower because it depends on someone outside this project.

### 2.2 Multi-school distribution raises the stakes on T0.1

For one school, storing pupil passwords is a decision the headmaster can make about their own pupils. Distributing the software to other schools makes the author a **vendor supplying a credential-replay tool for children's MOE accounts** — a different position under both Google Workspace terms and PDPA 2010, and one that will eventually be asked about by someone senior.

The v2 architecture deliberately minimises this: **no credential ever leaves the school that created it**, there is no central server, and the author holds no key and receives no data (§5.1). That is the strongest defensible position available while still storing passwords at all. It does not remove the need for T0.1; it makes T0.1 answerable.

**Requirement G-1:** A written T0.1 position must exist before the software is given to a second school. The installer's licence page (§8.5) states the school's own responsibilities explicitly.

### 2.3 The honest alternative

`../Normal_SSO/` solves ~70% of the same pain (the unrememberable 26-character email) with **none** of the credential risk, no installer, no provisioning, and no policy gate. If T0.1 comes back negative, or comes back slowly, that product ships and this one waits.

This document does not recommend one over the other. It records that the cheap option exists, is already specified, and remains available — because the most likely failure mode for v2 is spending three months on it and then being told no.

---

## 3. Users

**Aisyah, 8, Tahap 1.** Recognises her name and her avatar. Cannot type `@`. Needs big targets, one idea per screen, and Bahasa Melayu written for a seven-year-old.

**Cikgu Farah, class teacher.** 40 minutes of lab time, 30–44 pupils. Needs the class in and working fast, and needs to fix one pupil's problem herself without filing a ticket.

**En. Zul, ICT coordinator (new to v2, and the reason v2 exists).** Installs the software on 20–40 lab PCs. Exports from APDM, holds the password list, runs the import, re-provisions when passwords rotate. **In v1 this person was the author. In v2 they are a stranger at another school who has never seen the code and will not read the source.** Everything En. Zul touches must work from a wizard with no command line, no JSON editing, and no assumed knowledge.

**Puan Hana, headmaster.** Will be asked to approve. Will ask two questions: *is it safe*, and *who is responsible if it isn't*.

---

## 4. Goals & Success Metrics

### Goals

- **G1** — Class-wide time-to-signed-in under 3 minutes, from a measured baseline.
- **G2** — A pupil cannot sign in as a classmate (v1's largest defect).
- **G3** — A new school goes from installer download to a working lab in **under 90 minutes** of ICT coordinator time, unaided, with no support call.
- **G4** — No credential leaves the school. No central service exists to breach.
- **G5** — A password rotation for a whole class is a 10-minute job, not a day.

### Metrics

| Metric | Baseline | Target | Measured how |
| :--- | :--- | :--- | :--- |
| Median class-wide time to all-signed-in | 15 min *(est. — unverified)* | ≤ 3 min | Teacher stopwatch, 5 lessons pre/post |
| Click → password submitted, p50 | n/a | ≤ 8 s | In-app timing, local log |
| Click → password submitted, p95 | n/a | ≤ 15 s | Same |
| Injection success rate | **100/100 at T0.3 (§2.1)** — gross failure ruled out | ≥ 99% | **Still measured at pilot, not at T0.3.** 100 clean runs is consistent with ≥99% but doesn't certify it statistically — T0.3's job was detecting gross, deterministic failure (which `SendKeys` had and `SendInput` didn't), not certifying a reliability figure. See `T0.3_Injection_Test_Protocol.md` §5. |
| Wrong-pupil sign-ins | 3–5 per lesson *(est.)* | 0 | Audit log + teacher tally |
| New-school setup time, ICT coordinator, unaided | n/a | ≤ 90 min | Timed with a real coordinator at school #2 |
| Import rejects on a real APDM export | n/a | ≤ 2% of rows, all with actionable messages | Dry-run report |
| Support calls per school in first month | n/a | ≤ 2 | Tally |

The 15-minute baseline is inherited from v1 and remains **an estimate, not a measurement**. It should be timed once before the pilot. If the real figure is 6 minutes, the case for v2 over `Normal_SSO` weakens considerably.

### Non-Goals

- Central cloud roster or credential service. Explicitly rejected (§5.1).
- Password reset or account provisioning — remains an APDM/MOE admin function.
- Attendance, analytics, or any pupil-level behavioural data leaving the PC.
- ChromeOS, macOS, or Chromebook support. Windows lab PCs only.
- Auto-update. v1 upgrade path is a silent reinstall (§8.4).
- Firebase. v1's Firestore dependency is removed entirely (§5.1).

---

## 5. Architecture in one page

### 5.1 No cloud. At all.

v1 put the roster in Firestore. v2 removes it. The reasoning, in order of weight:

1. **A multi-tenant credential store for thousands of minors across many schools is the single worst asset to own.** Not owning it is worth more than every feature it would enable.
2. Firestore made the author the data processor for other schools' pupils. Local-only keeps each school the sole controller of its own data.
3. The Gap Analysis (§1.2) already moved passwords out of Firestore. Once passwords are local, the roster is the only thing left in the cloud — and it is 2,000 children's names, which is PDPA-relevant on its own. The remaining benefit did not justify the remaining risk.
4. It deletes an entire layer: no Firebase project per school, no security rules, no anonymous auth, no offline-sync edge cases, no billing.

**Consequence to accept honestly:** roster updates are now a manual re-provisioning job, not a cloud push. §9 makes that a scheduled, owned task rather than an oversight.

### 5.2 Two programs, one installer

| Program | Runs on | Who uses it | Holds |
| :--- | :--- | :--- | :--- |
| **DELIMa Launcher** (`DelimaLauncher.exe`) | Every lab PC | Pupils, and teachers via PIN | A DPAPI-wrapped store readable only on that one machine |
| **DELIMa Admin** (`DelimaAdmin.exe`) | ICT coordinator's PC only | En. Zul | The master bundle, unlocked by his passphrase |

One installer, two components. A lab PC installs the Launcher only.

### 5.3 The credential path, end to end

```
APDM export ─┐
             ├─► DELIMa Admin ──► school.dlmpack ──► provisioning ──► credentials.dat
password list┘   (import wizard)   AES-256-GCM,        (USB or         (DPAPI machine
                                   admin passphrase     network share)   scope, per-PC,
                                   as the only key)                      useless elsewhere)
```

Three properties this shape guarantees:

- The admin passphrase never reaches a lab PC.
- `credentials.dat` copied off a lab PC is undecryptable on any other machine.
- There is no key in the shipped binary, so decompiling it yields nothing.

Full crypto specification in `Technical_Architecture_Visual_SSO.md` §3.

---

## 6. The School Setup Wizard (requirement 2)

This is the largest new component in v2 and the one that determines whether the product is distributable. It runs on first launch of DELIMa Admin and is re-enterable step by step afterwards.

### 6.0 Design stance

En. Zul is competent with a spreadsheet and Windows, and has never seen a JSON file he wanted to edit. Therefore: **no configuration file is ever hand-edited, no step requires the command line, and every destructive action has a dry run.** Every error message names the row, the column, and the fix.

### Step 1 — School identity

Establishes what makes this install *this school's*, so nothing is compiled in.

| Field | Notes |
| :--- | :--- |
| School code | e.g. `SKS24`. Used in filenames and the audit log. |
| School name | Appears in the header. |
| Motto | Optional; renders under the name. Blank hides the line. |
| MOE email domain | Defaults `moe-dl.edu.my`. Editable — schools on other domains exist. |
| Crest image | PNG/SVG, ≥ 256 px. Optional; a neutral placeholder is used if absent. |
| Brand palette | Primary, accent, and 8 class colours. Pre-filled with a **neutral default set**, not SK Seksyen 24's. Contrast is validated live (§7.1). |
| Number of tahun, classes per tahun | Read from the roster on import; this step only sets expectations for validation. |

**FR-S1.4:** The wizard rejects a palette that fails 4.5:1 contrast for the text it will carry, and says which pair failed. This exists because v1's own design notes record that white-on-orange at 2.3:1 was a real mistake someone nearly shipped — a school picking its own colours will make it again.

### Step 2 — Admin passphrase

- Minimum 12 characters, checked against a common-password list, strength meter shown.
- Entered twice. **There is no recovery.** The wizard states this plainly, in a box the coordinator must tick.
- Derives the master key by Argon2id (params in arch §3.2). Never stored.
- Produces a printable **recovery sheet**: school code, creation date, a key-check value (not the key), and instructions. Explicitly *not* a key escrow — losing the passphrase means re-importing from APDM, which is a bad afternoon, not a disaster.

### Step 3 — Import the roster

Pupil names, classes, and DELIMa IDs. **No passwords in this step** — the separation is deliberate (§6.3).

Accepts `.csv`, `.xlsx`, `.xls`.

**Column mapping, not fixed headers.** APDM exports differ between states and between years; a fixed header contract would break at the first school. The wizard shows the first 10 rows in a grid and asks the coordinator to point each required field at a column:

| Required | Accepts | Validation |
| :--- | :--- | :--- |
| Nama penuh | any text column | non-empty, ≤ 100 chars |
| Kelas | `2 Cemerlang`, `2C`, `2 CEMERLANG` | normalised; unknown forms listed for confirmation |
| Tahun | number 1–6, or derived from the class string | 1–6 |
| ID DELIMa | `m-12345678`, `12345678`, `m-12345678@moe-dl.edu.my` | normalised to 8 digits; `^\d{8}$` |
| No. KP / register no. | optional | used only as a join key for Step 4; never stored |

Mappings are remembered per school, so next year's import is one click.

**Encoding.** APDM exports are frequently ANSI (CP1252), sometimes UTF-8 with and without BOM, occasionally UTF-16. The importer sniffs the BOM, then heuristically detects, then **shows the coordinator a preview of names containing diacritics** and asks "does this look right?" — because a mojibake'd `Nurul A'in` is the failure a checksum cannot catch and a human spots instantly.

**Validation and dry run.** Nothing is written until the coordinator sees a report:

```
2,014 rows read
  1,987 valid
     18 duplicate ID DELIMa      → listed, first occurrence kept
      6 malformed ID DELIMa      → listed with row numbers
      3 unknown class "6 Amamah" → not in any tahun; confirm or fix
     54 classes across 6 tahun
      2 classes over 44 pupils   → grid will scroll on 1366×768 (see §7.2)
```

Rejects export to `rejects_<date>.csv` with an added reason column, so the coordinator fixes the source and re-imports only those rows.

**FR-S3.7:** Import is idempotent and re-runnable. A second import updates matched pupils, adds new ones, and **flags leavers rather than deleting them** — a pupil who vanishes from an export because of an APDM glitch must not silently lose their account mid-term.

### Step 4 — Import passwords

Deliberately a separate step, behind a separate screen, with a separate confirmation.

**FR-S4.1 — Informed-consent screen.** Before the file picker, a plain-language screen in BM and EN states: what will be stored, where, encrypted how, who can read it, what happens if the PC is stolen, and that the school — not the software author — is responsible for the decision. Coordinator types the school code to proceed. The acknowledgement (school code, Windows user, timestamp, software version) is written to the audit log and to the recovery sheet.

This screen is not compliance theatre. It is the only moment at which anyone at the receiving school is forced to notice what the product does.

**Input formats:**

- Separate CSV: `id_delima, kata_laluan` — joined on the DELIMa ID.
- Same file as Step 3, with a password column selected in the mapper.
- Manual entry for a handful of pupils, in Step 4's grid.

**Validation:**

- Report pupils with no password (they remain in the roster; their card shows a teacher-visible "belum siap" state rather than failing at injection time).
- Report passwords not present in the roster.
- ~~Warn on characters outside the injectable set~~ — **removed.** T0.3 found no character class `SendInput` mishandles (arch §4.3): 100/100 across two batches, including every reserved character combined. Nothing to warn about.
- Warn on obviously shared passwords (the same value across many pupils) — common in practice, and worth the coordinator knowing.
- **Never display a password.** The grid shows `••••••` with a per-row reveal that requires the passphrase and writes to the audit log.

**FR-S4.6:** The source password file is not copied, not cached, and not referenced by path afterwards. On completion the wizard offers to securely delete it and tells the coordinator to remove any copies — because the plaintext CSV sitting in `Downloads` is a bigger risk than anything the app stores.

### Step 5 — Avatars and picture passwords

- Avatars auto-assigned, **unique within a class**, stable year to year for pupils who stay. Coordinator can reassign; teachers can too, later.
- Picture-password policy set here: required (default) or disabled. **Disabling it is the v1 behaviour and reintroduces blocker B1** — the wizard says so in those words, and records the choice in the audit log.
- Optional: print a per-class avatar sheet for the classroom wall.

### Step 6 — Destinations and app settings

Destinations, idle timeouts, teacher PIN policy, language default. Shipped defaults are sensible; this step exists so a school can add its own portal.

### Step 7 — Build and provision

1. Builds `school.dlmpack` — the encrypted master bundle.
2. Offers three provisioning routes:
   - **USB** — writes a provisioning folder; run `Provision.exe` on each lab PC and enter the passphrase once per PC.
   - **Network share** — points lab PCs at a UNC path; the passphrase is still entered per PC, never stored on the share.
   - **Scripted** — emits a PowerShell script for PDQ/GPO deployment across the lab, with the passphrase prompted once per run and held in memory.
3. Prints a **lab checklist**: PC name, provisioned yes/no, software version, store date.

**FR-S7.4:** The Launcher refuses to start if its store is older than the master bundle by more than a configurable window (default 30 days), showing a teacher-facing "hubungi ICT" screen. This is how PC 14 stops silently serving last term's passwords.

### 6.3 Why steps 3 and 4 are separate

Combining them is one fewer screen and materially worse:

- A school can run the roster import and get a working `Normal_SSO`-equivalent experience **without ever handling passwords**, and decide about Step 4 later.
- The consent record attaches to a discrete act with a timestamp.
- Roster refreshes happen termly; password imports happen on rotation. Different cadences, different people, sometimes different authority.

---

## 7. The pupil-facing application (requirement 3 — unchanged)

The design system in `../Normal_SSO/stitch-wireframes/PROMPT.txt` is adopted in full: 20/16 px radii, 8/16/24/32/48 spacing, Nunito/Quicksand/Baloo 2, nothing below 16 px, no ellipsis truncation, ≥ 48 px targets, warm greys only, BM throughout, forbidden vocabulary list (*SSO, portal, autentikasi, sesi, log masuk tunggal*) enforced by a string-table lint.

Three things must change, and only three.

### 7.1 Colours become themeable

`#056839`, `#F7941D`, `#ED1B24` and the eight class colours are **SK Seksyen 24's crest colours**. Shipping them to another school is shipping the wrong school's identity. They become the default theme; §6.1 lets a school replace them, with the contrast rules enforced rather than documented:

- Never white text on the accent unless it passes 4.5:1.
- Never text of any colour on the alert colour.
- Focus ring derived from the primary at a guaranteed contrast against both surfaces.

### 7.2 The name grid must handle a school it wasn't designed for

The 7/8/9-column × 5-row grid is sized for classes of 30–44 at 1366 × 768. Another school may run classes of 50, or labs at 1024 × 768. Behaviour, in order:

1. Fit the class on one screen at the largest readable card.
2. If cards would fall below ~11 characters per line, keep them readable and **scroll vertically**, with the teacher search box as the fast path.
3. The import (§6.3) warns the coordinator at import time which classes will scroll, so it is a known condition rather than a surprise in a lesson.

### 7.3 New screen: picture password

Between the name card and the launch, closing blocker B1. Grid of 16 concrete BM-nameable icons (*kucing, bola, bunga, kereta*), pupil taps 3 in sequence, **positions shuffled on every attempt**, 5 failures locks the card for 5 minutes and flags it in teacher mode. Argon2id with a per-pupil salt. The icon set is visually distinct from the avatar set, or pupils confuse the two.

### 7.4 Screen inventory

| Screen | Purpose | Source |
| :--- | :--- | :--- |
| Pilih Kelas | Tahun → Kelas, "Kelas Terakhir" shortcut, Guru button | Normal_SSO screen 1, adapted |
| Cari Nama | Adaptive card grid + "Nama saya tiada" card | Normal_SSO screen 2 |
| **Kata Laluan Gambar** | 3-of-16 shuffled icon sequence | **New — closes B1** |
| Pergi ke Mana | Masked email, DELIMa / Classroom | Normal_SSO screen 3 |
| Sedang Masuk | Progress + abort, during injection | **New** |
| Selesai (floating bar) | Topmost bar, one red logout button | v1 §4 screen 3 |
| Ralat | Failure taxonomy, BM message + teacher code | **New — arch §7** |
| Mod Guru | PIN-gated: password update, picture reset, add pupil, reset all, diagnostics | v1, expanded |

**How to actually build these in WPF** — fonts, theming as runtime-swappable data rather than a compiled resource, the retemplated (never native) dropdown, and the adaptive card grid — is specified in `Technical_Architecture_Visual_SSO.md` §6. That section also restates the web product's WCAG 2.2 AA / keyboard-navigation requirement for this app, which this PRD does not currently state on its own.

---

## 8. The installer (requirement 1)

### 8.1 Choice: Inno Setup 6

Over WiX/MSI (a toolchain to learn and maintain for a benefit — GPO-native deployment — that a 40-PC lab does not need) and MSIX (signing and packaging constraints that fight a kiosk app and a per-machine credential store).

Inno Setup gives one `.exe`, a scriptable build, `/VERYSILENT` for scripted lab deployment, component selection, and a custom licence page. It is the standard choice for exactly this shape of product.

### 8.2 Payload

**Self-contained, single-file, win-x64, .NET 10 LTS.** Roughly 80–100 MB per program, and correct: lab PCs will not have any modern .NET, and telling En. Zul to install a runtime on 40 machines before he can install the app loses him at minute five. Framework-dependent builds are offered as a secondary download for schools with managed images.

**Not .NET 8**, which is what the spike was originally written against before being retargeted (arch §1): .NET 8 leaves support on 10 November 2026, before this product could ship. Shipping an unsupported runtime to schools holding children's credentials is not defensible, and the migration would land mid-project.

### 8.3 Install modes

| Component | Installs | For |
| :--- | :--- | :--- |
| **PC Makmal** (default) | Launcher, avatar assets, `Provision.exe` | Every lab PC |
| **Alat Pentadbir** | Admin, wizard, importer | ICT coordinator's PC only |

Program files to `%ProgramFiles%\DELIMa Launcher`. Data to `%ProgramData%\DELIMa Launcher`, ACL'd to deny read to interactive users (arch §3.5). Never `%APPDATA%` — the store is per-machine, not per-user.

**Optional tasks, all opt-in and explained:**

- Launch at logon (kiosk mode)
- Apply Chrome enterprise policy — disables the password manager, DevTools, incognito, and browser sign-in on the launcher's profile (Gap Analysis §1.6). Writes to `HKLM\SOFTWARE\Policies\Google\Chrome`. **Requires admin, affects the whole machine, and the checkbox says so.**
- Desktop / Start Menu shortcuts

**One thing the installer cannot do, and must not pretend to.** Restricting which programs the pupil account may run (AppLocker/SRP, arch §9) is what protects the credential store from anyone sitting at a lab PC. It depends on the school's Windows edition and existing group policy, so it ships as a documented snippet the coordinator applies, and as a **required** line on the lab checklist — not a checkbox in the installer that might silently fail.

### 8.4 Upgrade and uninstall

- Upgrade is a reinstall over the top; `AppId` GUID fixed across versions. `credentials.dat` and settings survive; assets are replaced.
- The store carries a schema version; a newer Launcher migrates forward and refuses to open a future-versioned store.
- **Uninstall securely deletes** `credentials.dat`, any temporary Chrome profiles, and the audit log only if the coordinator confirms — the log may be required evidence.

### 8.5 Signing — a distribution blocker, not a polish item

Unsigned, SmartScreen shows *"Windows protected your PC"* to every ICT coordinator who downloads it. For software that asks a school to entrust it with children's passwords, that is fatal to adoption, and correctly so.

**Requirement:** an OV code-signing certificate (~USD 200–400/year), applied to both executables and the installer. EV additionally bypasses SmartScreen reputation-building, at roughly double the cost. Budget for OV; treat the reputation ramp as a known first-month friction.

The installer's licence page states plainly: no warranty; the school is the data controller for its pupils' credentials; the school is responsible for obtaining its own MOE/BSTP position; no data is transmitted to the author or anyone else.

### 8.6 What ships

```
DELIMaLauncher-Setup-2.0.0.exe        signed installer
DELIMaLauncher-2.0.0-checksums.txt    SHA-256
Panduan_Pemasangan.pdf                BM install guide, screenshots, 10 pages
Panduan_Import.pdf                    BM CSV/import guide with worked examples
contoh_roster.csv / contoh_kata_laluan.csv   templates
```

The two PDFs are deliverables, not documentation debt. G3 — 90 minutes, unaided — is not achievable without them.

---

## 9. Operations

### 9.1 Annual rollover

Same failure mode as `Normal_SSO` §9: every January the roster is wrong in five ways at once, and nobody owns fixing it.

| Step | Owner | When |
| :--- | :--- | :--- |
| Export new APDM roster | ICT coordinator | Week 1 |
| Re-run wizard Steps 3–5 | ICT coordinator | Same day |
| Obtain and import new passwords for Tahun 1 | ICT coordinator | Week 1 |
| Re-provision every lab PC | ICT coordinator | Same day |
| Clear per-PC "Kelas Terakhir" | ICT coordinator | Same day |
| Verify Tahun 6 leavers are gone | ICT coordinator | Same day |
| Reprint avatar and fallback sheets | Class teachers | Week 1 |

The store date shown in the Launcher footer is the visible check.

### 9.2 Password rotation

Assume monthly for some subset, not annually. Wizard Step 4 re-run for the affected pupils → rebuild → re-provision. `password_version` per pupil lets the Launcher distinguish *"kata laluan sudah tukar"* from *"gambar salah"* — two failures a teacher must not have to guess between.

### 9.3 Support model

No central service means no central support. Each school owns its install. The author supplies the installer, the two guides, and a diagnostics export (`Mod Guru → Diagnostik`) that produces a redacted log bundle a coordinator can email. **State the support commitment in writing before school #2**, or it becomes an unbounded personal obligation.

---

## 10. Rollout

| Phase | Scope | Duration | Exit criteria |
| :--- | :--- | :--- | :--- |
| **0 — De-risk** | T0.1, T0.2, T0.3 (§2.1) | 1–2 weeks | Written policy position; confirmed SSO URL; **zero unexplained injection failures across 50 runs on lab hardware, with `SendKeys` corruption reproduced as the control**, and 5/5 on the adversarial focus-steal test. **T0.3: done.** T0.1 and T0.2: not started — now the active blockers. Pre-injection focus-steal is 2/2 clean so far (1s, 3s); complete to 5 for full protocol coverage. See `T0.3_Injection_Test_Protocol.md`. |
| **1 — Baseline** | Time 5 real lessons | 1 week | The 15-minute estimate replaced with a measurement |
| 2 — Credential foundation | Store format, Admin wizard, importer, provisioning | 3 weeks | A second person can import a real APDM export unaided |
| 3 — Client | WPF shell, picture password, injection engine, failure taxonomy | 4 weeks | One class signs in end to end on lab hardware |
| 4 — Hardening | Chrome policy, kiosk, audit log, teacher mode | 2 weeks | A curious nine-year-old cannot reach `chrome://settings/passwords` |
| 5 — Pilot, own school | 2 classes, 1 lab, 2 weeks | 2 weeks | ≤ 3 min class sign-in; zero wrong-account incidents; injection ≥ 99% |
| 6 — Packaging | Installer, signing, guides | 2 weeks | Coordinator at school #2 installs unaided in ≤ 90 min |
| 7 — School #2 | One external school, observed | 4 weeks | G3 met; ≤ 2 support calls |

Phases 2 and 3 are the only ones that look like building the app. That ratio is the honest shape of this product.

---

## 11. Risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| ~~T0.3 fails — injection unreliable on lab hardware~~ | ~~Fatal~~ | **Resolved, 17 August 2026.** 100/100 across two batches on real lab hardware. See arch §4.2–4.3. |
| MOE/BSTP refuses password storage | Fatal for v2 | T0.1 before school #2. `Normal_SSO` ships instead. |
| MOE enables 2SV on pupil accounts | Fatal | No mitigation exists. Ask now (Gap Analysis §6 Q2). Watch for announcements. |
| Google anti-automation (CAPTCHA, "unusual activity") on 40 near-identical logins from one IP | High | Jitter between launches; failure taxonomy surfaces it as a teacher-legible state rather than a hang. Cannot be fully solved. |
| Passphrase lost at another school | Medium | Recovery sheet; documented re-import path. Explicitly no escrow. |
| Lab PC stolen with `credentials.dat` | Medium | DPAPI machine scope makes the file useless on any other machine. BitLocker required in the install guide. |
| **Anyone who can run code in a lab PC session recovers every password for that school** | **High — inherent, no technical fix** | DPAPI protects against moving the file, not against a local user (arch §3.3). Mitigated only by AppLocker/SRP restricting execution to `%ProgramFiles%`, kiosk lockdown, and physical access control — all of which the school must maintain. **This is the strongest argument for `Normal_SSO` and must be stated to the headmaster, not buried.** |
| Coordinator leaves the plaintext password CSV in `Downloads` | **High, and likely** | Wizard offers secure delete and says why; both guides repeat it. Cannot be enforced. |
| Another school's ICT coordinator less capable than assumed | High — G3 misses | Wizard-only, no CLI, dry runs everywhere, two PDF guides, timed test with a real coordinator in Phase 6. |
| Unsigned binary → SmartScreen kills adoption | High | OV certificate budgeted (§8.5). |
| Author becomes unpaid support for N schools | Medium, compounding | Written support statement before school #2 (§9.3). |
| Picture password disabled by a school "to save time" | High — B1 returns | Warned in the wizard, recorded in the audit log, stated in the guide. Cannot be prevented. |
| Store drifts stale on one PC | Medium | Staleness refusal (FR-S7.4) + provisioning checklist. |

---

## 12. Open questions

1. ~~Has T0.3 been run?~~ **Yes — passed, 17 August 2026.** Next blocker is T0.1.
2. Who at each school actually holds pupil passwords, and in what form? If they don't hold them in bulk, Step 4 has no input and the product cannot work there.
3. Does MOE enforce 2SV on `moe-dl.edu.my` today, or plan to?
4. How many schools, realistically — 2, or 20? At 2, the installer matters and the support story doesn't. At 20, the reverse.
5. Who owns support after handover?
6. Is there any MOE-native alternative (QR sign-in, managed ChromeOS guest sessions) that solves this at the platform level? One email, potentially saves the whole programme.
7. Lab screen resolutions at the *other* schools — the grid assumes 1366 × 768.
8. Does the software need a name and identity independent of SK Seksyen 24 before it goes to school #2?

---

*Companion: `Technical_Architecture_Visual_SSO.md`. Prior art: `../PRD_Gap_Analysis.md`, `../Normal_SSO/`.*
