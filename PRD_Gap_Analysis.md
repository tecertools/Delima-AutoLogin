# DELIMa Smart Launcher — PRD Gap Analysis & Implementation Review

Review of `DELIMa_Visual_SSO_PRD_TechArch.md`
Date: 5 August 2026
Revision 2 — scoped to single-school lab deployment; local credential store; picture-password auth

---

## Verdict up front

The product idea is sound and the pain is real. Scoping to one school's PC lab removes a lot of work — the entire cloud credential-broker layer and most of the deployment phase disappear. What remains:

| # | Blocker | Status after re-scope |
| :-- | :-- | :-- |
| **B1** | Any pupil can log in as any classmate | **Unchanged — arguably worse.** The lab is the threat model: 30 pupils, every classmate's card on screen, one tap away. Resolved by picture password (§1.1). |
| **B2** | Credential storage | **Reshaped.** Moving passwords out of Firestore into a local DPAPI-wrapped store removes the internet-facing exposure and the shipped-key problem (§1.2). |
| **B3** | `SendKeys` into an unverified window | **Unchanged.** Pure correctness bug, independent of deployment scope. Will silently break accounts whose passwords contain `+ ^ % ~ ( ) { } [ ]` (§1.4). |

Sections 1–3 cover the blockers. Section 4 covers everything else.

---

## 1. Security & identity gaps

### 1.1 Pupil authentication — picture password (BLOCKER)
The PRD's flow is: pick class → tap a face → you're in. Nothing stops Amir tapping Aishah's card and reading her Gmail.

**Design.**
- Grid of 16 icons; pupil taps **3 in sequence**. 4,096 combinations — ample against this threat model when paired with lockout.
- **Shuffle icon positions on every attempt.** Costs the pupil a second or two, but a fixed grid means a classmate learns the *positions* after watching once. Shuffling is the whole point.
- Icons must be concrete, nameable objects in BM (*kucing, bola, bunga, kereta*…) so a non-reader can rehearse the sequence verbally.
- Store as **Argon2id hash with a per-pupil salt** in the local store (§1.2). Never plaintext, never the icon IDs in the clear.
- **Lockout:** 5 failed attempts → that card is locked for 5 minutes and flagged in teacher mode. Prevents a bored pupil brute-forcing a neighbour over a lesson.
- **Enrolment:** teacher sets it with the pupil in teacher mode; pupil picks 3 icons they like (memorable beats random here).
- **Reset:** teacher mode only. Expect to do this often in the first fortnight.

Budget roughly two days including the enrolment UI. This is the cheapest of the three blockers to close.

### 1.2 Local encrypted credential store (BLOCKER — architecture change)
**Passwords leave Firestore entirely.** Firestore keeps only non-sensitive roster data: names, class assignments, avatar IDs. This is the main simplification lab-only scope unlocks, and it deletes the Cloud Function broker from the plan.

**Store design:**

```
credentials.dat   AES-256-GCM
                  ├─ per-student: delima_email, password, picture_password_hash
                  ├─ password_version, updated_at
                  └─ HMAC over the whole blob (tamper detection)
```

**Key handling — the part that matters.** DPAPI machine-scope keys are machine-specific, so a file encrypted on the admin's PC cannot be decrypted on a lab PC. That constraint drives the following two-tier design:

1. **Master bundle** lives on an admin-controlled share, encrypted with an **admin passphrase** (Argon2id → AES-256-GCM). Lab PCs never see this passphrase.
2. **Provisioning utility**, run by the admin, decrypts the master bundle and writes a **per-PC copy re-wrapped with DPAPI machine scope** to each lab PC.
3. Lab PCs therefore hold only a DPAPI-wrapped store that is useless on any other machine, and no passphrase.
4. **Rotation:** admin updates the master bundle, re-runs the provisioning utility once across the lab. No hand-editing 40 PCs.

**Additional requirements:**
- Decrypt exactly one pupil's password at a time, into a `SecureString` (or pinned byte array), and zero it immediately after injection. Never decrypt the whole store into memory.
- File ACLs: readable only by the account the launcher runs as. Not by interactive lab users.
- Never write the decrypted value to a log, crash dump, or temp file. Disable WER dumps for the process.

**Higher-security variant, if the school will tolerate it:** drop the per-PC store; the teacher enters an unlock passphrase once when opening the lab, key held in memory only and wiped at shutdown. Nothing sensitive at rest anywhere. Trade-off: lab is down if the teacher forgets, and the passphrase will end up on a whiteboard. Not recommended as the default for a school with thin IT support.

### 1.3 Firestore rules still required (reduced, not eliminated)
Even with passwords removed, the roster is 2,000 children's full names and class assignments — PDPA-relevant on its own. A Firebase config embedded in a binary sitting on lab PCs is readable by anyone with ten minutes and a decompiler, and Firestore is internet-facing regardless of where the app runs.

**Requirements:** authenticated reads only (anonymous auth at minimum, scoped by `school_id`); no client writes; admin operations through a separate authenticated path. Default-deny rules, explicitly tested.

### 1.4 SendKeys has no target verification (BLOCKER)
`Process.Start` → `Thread.Sleep(1500)` → `SendKeys` assumes Chrome is running, foreground, and focused on the password field. On a cold Chrome start on lab hardware, 1,500 ms is routinely not enough — 4–8 s is common on spinning disks. When the assumption fails, a child's password is typed in plaintext into the URL bar, a Word document, or whatever else has focus.

**Requirements:**
- Poll `GetForegroundWindow()`; verify window class is `Chrome_WidgetWin_1` **and** the title matches the expected Google sign-in page, before sending a single keystroke.
- Abort to a friendly "Cuba lagi" screen if the target isn't confirmed within a timeout.
- Block user input during injection (`BlockInput()`, or a topmost transparent overlay) so a stray click can't redirect keystrokes.
- **Use `SendInput` via P/Invoke, not `SendKeys.SendWait`.** `SendKeys` interprets `+ ^ % ~ ( ) { } [ ]` as control characters. These appear in MOE-generated passwords, and every affected account will fail with no obvious cause. This is the single highest-value fix in this document.
- Never send `{ENTER}` blind — confirm the field accepted the expected length first, or let the pupil press Enter.

### 1.5 Session bleed between pupils
`taskkill /F /IM chrome.exe` does **not** clear cookies. Pupil 2 taps their card and lands in Pupil 1's authenticated session. It also kills the teacher's own Chrome on the same PC, and `/F` corrupts the profile — triggering "Restore pages?" on next launch, which breaks the automation and may restore the previous pupil's tabs.

**Requirements:**
- Launch with `--user-data-dir=%TEMP%\delima_session_{guid}` — throwaway profile per pupil, directory deleted on reset.
- Graceful shutdown first (`WM_CLOSE`); `taskkill /F` only as a timeout fallback.
- Track the PID and kill **only that process tree**, never `/IM chrome.exe`.
- Disable Chrome's password manager and profile sign-in on the kiosk profile, or pupils get "save password?" prompts and credentials persist.

### 1.6 Kiosk hardening entirely absent
A curious nine-year-old finds these in week one:

`chrome://settings/passwords` · F12 DevTools · the 👁 reveal icon on the password field · Alt+Tab / Win key / Ctrl+Shift+Esc · Ctrl+Shift+N to bypass session logic.

**Requirements:** Chrome ADMX/registry policy disabling `PasswordManagerEnabled`, `DeveloperToolsAvailability`, `IncognitoModeAvailability`, `BrowserSignin`; URL allowlist restricting the profile to `accounts.google.com` and `*.delima.edu.my`; Windows kiosk or shell replacement plus a keyboard hook for the launcher itself.

### 1.7 No audit log
Not optional for a school handling minors' accounts, and it's what tells you whether impersonation is happening. Log timestamp, `student_id`, `device_id`, outcome (success / wrong picture password / locked out / aborted), duration. Local append-only file, synced to Firestore or a share. Never log the password, the hash, or the key. Define a retention period.

### 1.8 Offline cache — now trivially resolved
§4's "cache the roster JSON locally" was in tension with the old design. With credentials already local, the cache holds **names and avatars only** and the tension disappears. Add a TTL (24 h) and an explicit "offline" state. Logins keep working offline, which is a genuine improvement over the original architecture.

---

## 2. Correctness bugs in the PRD as written

| Location | Issue | Fix |
| :-- | :-- | :-- |
| §5 step 2 | URL is `AccountChooser?Email=` — legacy, and *not* `login_hint`, which §2 and Task 3 both specify. Markdown is mangled with embedded link syntax. | Use the SP-initiated flow with `login_hint`, or the DELIMa SP entry point. **Verify against `d3.delima.edu.my` before coding** — the portal moved from d2 to d3. |
| §5 step 3 | Chrome path hardcoded to `C:\Program Files\Google\Chrome\Application\chrome.exe`. Fails on 32-bit installs (`Program Files (x86)`) and per-user installs (`%LOCALAPPDATA%`). | Resolve via `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe` with fallbacks. Handle "Chrome not installed." |
| §4 latency | "< 5 s to portal load" is not achievable — Chrome cold start alone is 2–4 s on lab hardware, before Google's page load and the SAML redirect. Picture password adds ~3 s. | Re-baseline to click → password submitted. Target < 8 s p50, < 15 s p95, stating the hardware assumption. Still a large win against 15–20 min. |
| Numbering | Sections 4 and 5 each appear twice; §5 code fences are broken. | Renumber before handing to implementers. |
| Schema | No `active`, `updated_at`, `password_version`. Avatar assets unspecified. | Add. `encrypted_password` moves out of Firestore entirely (§1.2). |
| Language | PRD mixes English and Malay. | Pupil-facing UI 100% BM. Define a string table; decide whether teacher mode is BM or EN. |

---

## 3. Google policy risk — unchanged by re-scope

§4 claims compliance because real Chrome is launched. That avoids the *WebView* prohibition but does not address:

- **Credential handling.** Google Workspace ToS and MOE policy generally require a password be known only to its user. An app that stores and replays 2,000 pupils' passwords needs sign-off. **Get a written answer from your BSTP or state ICT contact before pilot.** Policy decision, not an engineering one.
- **Anti-automation defenses.** Rapid, near-identical logins from one lab IP will trigger CAPTCHA, "unusual activity" challenges, or device verification. The PRD handles none of these — the app will simply appear broken.
- **2SV.** If MOE enables 2-step verification on pupil accounts, the flow dies outright. No contingency in the PRD.
- **Forced password change / expiry.** Pupil sees an unexplained failure with no path forward.

**Requirement:** a failure taxonomy with a pupil-friendly BM screen and a teacher-facing detail code for each — wrong password, CAPTCHA, 2SV prompt, account suspended, network down, Chrome missing, injection timeout, store decrypt failure.

**Still worth one email before you build:** ask MOE whether QR/badge sign-in or managed ChromeOS guest sessions are available to your school. Either solves this at the platform level rather than working around it. Cheap to ask, expensive to discover late.

---

## 4. Product & operational gaps

**Password lifecycle — missing entirely.** What happens when a pupil's password changes? Needs a teacher-mode "update this pupil's password" flow, the `password_version` field, a stale-credential state distinguishable from a wrong picture password, and the §1.2 rotation path. Assume this happens monthly, not annually.

**No session timeout.** A pupil who walks off without pressing "Selesai" leaves their account open. Idle timer (10 min → auto-logout, profile wiped), plus a teacher "reset all" for end of class.

**Avatar collisions.** `avatar_id: "cat_icon"` — three pupils with the cat and a non-reader can't find themselves. Enforce uniqueness within a class; combine avatar + colour + background. Specify the asset set, sizes, fallback. Keep avatar icons visually distinct from the picture-password icon set, or pupils will confuse the two.

**Teacher PIN.** Storage, rotation, lockout all undefined. A 4-digit PIN read over a teacher's shoulder unlocks admin functions in a room full of children. Hash it, add lockout, make it changeable.

**Deployment — much reduced.** Manual or scripted install across the lab's PCs, plus the §1.2 provisioning utility. Still needs: per-PC `device_id`, a version check so PCs don't drift, and a documented rollback. Skip MSI/GPO/Intune.

**CSV importer.** Task 1 gives no spec: column mapping, encoding (APDM exports are frequently ANSI, and Malay names carry diacritics), duplicate handling, validation, dry-run, partial-failure reporting. Critically — **define how passwords enter the system in the first place, and who is authorised to run this.** The importer now writes to the master bundle (§1.2), not Firestore.

**Accessibility for Tahap 1.** Ages 7–9. Minimum 96×96 px touch targets, high contrast, no text-only affordances, audio name playback (a real win for non-readers). Treat the search bar as optional — many pupils can't type their own name, which is the product's entire premise.

**Observability.** No crash reporting, no telemetry, no success-rate metric. You won't know the launcher is failing on PC 14 unless a teacher complains. A local log plus a weekly rollup is enough at this scale.

**Testing & rollout.** No test plan, pilot scope, success criteria, rollback, or support model. Recommend one class, two weeks, measured against the stated 15–20 min baseline.

---

## 5. Revised implementation plan

**Phase 0 — de-risk (1–2 weeks, do first)**
- T0.1 Written ToS/policy position from BSTP or state ICT.
- T0.2 Verify the live `d3.delima.edu.my` SSO entry URL and that `login_hint` is honoured end-to-end. Test manually with one real pupil account.
- T0.3 **Injection spike.** 50 runs on representative lab hardware, deliberately including passwords with `+ ^ % ~ ( ) { }`. Measure success rate. This decides whether the whole approach is viable.

**Phase 1 — credential foundation**
- T1.1 Local store format: AES-256-GCM + HMAC, per-student records.
- T1.2 Provisioning utility: master bundle (admin passphrase) → per-PC DPAPI re-wrap.
- T1.3 CSV importer with validation, dry-run, encoding handling → writes master bundle.
- T1.4 Firestore schema (roster only) + default-deny security rules.

**Phase 2 — client**
- T2.1 WPF shell: ClassSelectionView, StudentRosterView, **PicturePasswordView**, FloatingResetBar.
- T2.2 Picture password: shuffled 16-icon grid, Argon2id verification, 5-attempt lockout, teacher enrolment and reset.
- T2.3 ProcessLauncher: Chrome path resolution, throwaway profile, PID tracking, foreground-window verification, `SendInput` injection, input blocking, full failure taxonomy.
- T2.4 Session manager: graceful teardown, profile wipe, idle timeout, credential zeroing.
- T2.5 Offline cache (names/avatars, 24 h TTL).

**Phase 3 — operations**
- T3.1 Chrome enterprise policy + Windows kiosk hardening.
- T3.2 Teacher mode: password update, picture-password reset, roster refresh, reset-all, diagnostics.
- T3.3 Audit log + weekly rollup.

**Phase 4 — pilot**
- One class, two weeks, measured. Then widen to the lab.

---

## 6. Open questions

1. Who currently holds the pupils' DELIMa passwords, and in what form? Determines whether §1.2's model is even available to you.
2. Does MOE enforce 2SV on `moe-dl.edu.my` accounts today, or plan to? A "yes" invalidates the approach.
3. How many PCs in the lab? Sets the provisioning-utility effort.
4. Who owns this after launch, when a password rotation breaks 200 accounts on a Monday morning?

---

**Sources**
- [DELIMa 3.0 – Cara Kemaskini, Log Masuk & Ciri Terbaru 2026, Cikgu Digital](https://cikgudigital.my/delima-3-0-cara-kemaskini-log-masuk-ciri-terbaru-2026/)
- [DELIMa 2026: Cara Login & Kegunaan, Cerdik.my](https://cerdik.my/delima/)
- [SSO sign-in flow when using login hints, Google Workspace Admin Help](https://support.google.com/a/answer/15544042?hl=en-GB)
- [Optional SSO settings and maintenance, Google Workspace Help](https://knowledge.workspace.google.com/admin/apps/optional-sso-settings-and-maintenance)
