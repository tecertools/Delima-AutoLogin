# Build Prompts

Ready-to-paste prompts for building this project with an AI coding assistant (Gemini CLI, Claude Code, or any agent that can read files and run commands in the repository).

**How to use:** paste one prompt, let it finish, review and commit, then move to the next. Do not paste several at once — the value of these being separate is that you check the work between them.

Companion: `AI_Build_Guide.md` explains the workflow around these prompts, including how to check work you can't fully read yourself.

---

## Prompt 0 — Orientation (run this once, first)

> You are helping me build a Windows desktop application. The repository contains **specifications only** — no application code exists yet. Your job for now is to read and plan, **not to write code**.
>
> Read these files in full, in this order:
>
> 1. `README.md` — what this repository is and where things stand
> 2. `Visual_SSO/PRD_Visual_SSO_v2.md` — the product requirements
> 3. `Visual_SSO/Technical_Architecture_Visual_SSO.md` — the technical spec
> 4. `Visual_SSO/Build_And_Release.md` — how it gets compiled and shipped
> 5. `Normal_SSO/stitch-wireframes/PROMPT.txt` — the design system the UI must follow
>
> Then answer these questions, and **only** these questions. Do not create any files.
>
> 1. In one paragraph, what does this application do and who uses it?
> 2. List the projects in the solution layout and state, for each, whether it may reference Win32 or UI code.
> 3. What is build step 3 in the build sequence, and what does it depend on?
> 4. Name three things the architecture explicitly forbids.
> 5. What is currently the only unresolved blocker, and which build step does it block?
>
> If anything in those documents contradicts anything else, say so. I would rather find contradictions now than after you have written code against them.

**Why this first:** it verifies the assistant has actually read the specs before it starts producing files, and its answers tell you whether it understood them. If question 2 or 4 comes back wrong, fix that before letting it write anything.

---

## Prompt 1 — Solution scaffolding

> Create the solution skeleton described in `Visual_SSO/Technical_Architecture_Visual_SSO.md` §2. Specifically:
>
> - `DelimaLauncher.sln` at the repository root
> - `src/Delima.Core/Delima.Core.csproj`
> - `tests/Delima.Core.Tests/Delima.Core.Tests.csproj` using xUnit
> - `Directory.Build.props` at the root, with the version properties given in `Visual_SSO/Build_And_Release.md` §2
>
> Requirements:
>
> - Target **.NET 10**. `Delima.Core` targets `net10.0` (not `net10.0-windows`) — it must build on macOS and Linux, which is what makes it testable anywhere.
> - `Delima.Core` must not reference any Windows-only package, any UI package, or any other project in this solution.
> - Add a `.gitignore` entry only if something is missing; the existing file already covers .NET output.
> - Create no other projects yet.
>
> When done, run `dotnet build` and `dotnet test` and show me the output. Both should succeed with zero tests.

---

## Prompt 2 — The credential store (build step 3)

This is the highest-risk piece in the product. Take it slowly.

> Implement the credential store described in `Visual_SSO/Technical_Architecture_Visual_SSO.md` §3, in `src/Delima.Core`. Read §3.1 through §3.5 carefully before writing anything.
>
> Scope for this task — the master bundle only:
>
> - The `school.dlmpack` format from §3.2, including its header and schema version
> - Argon2id key derivation from the admin passphrase, using `Konscious.Security.Cryptography.Argon2`
> - AES-256-GCM encryption and HMAC-SHA256 tamper detection, from the .NET base class library
> - The decryption discipline in §3.4 — plaintext must not linger in memory
>
> **Out of scope for now:** the per-PC DPAPI store (§3.3). That is Windows-only and would break the cross-platform build. Leave an interface for it.
>
> Hard constraints:
>
> - **No custom cryptography.** Use the named packages and the BCL. If you find yourself writing a cipher, a padding scheme, or a KDF, stop and tell me.
> - No file, class, or comment may contain a real password, a real pupil name, or a real school's data.
> - Do not add any package not listed in Appendix A of the architecture document without asking me first.
>
> Then write the tests from §11's "Crypto" and "Zeroing" rows in `tests/Delima.Core.Tests`:
>
> - Known-answer tests and a full round-trip
> - **Tamper tests:** flip one byte in the header, one in the ciphertext, and one in the authentication tag. Assert that decryption fails in all three cases. These three tests are the ones that matter most — do not skip them or mark them inconclusive.
> - A wrong-passphrase test that asserts failure without revealing whether the passphrase or the data was wrong
>
> Run `dotnet test` and show me the results. Then explain, in plain language, what each tamper test proves.

---

## Prompt 3 — Roster model and display names (build step 4)

> Implement the roster model and display-name logic in `src/Delima.Core`, per `Visual_SSO/Technical_Architecture_Visual_SSO.md` §5 and the reference it cites, `Normal_SSO/Technical_Architecture_Normal_SSO.md` §4.3. Read both before starting.
>
> This has no dependency on the credential store — it is separate work in the same project.
>
> Pay particular attention to Malaysian naming conventions. The fixture set must cover:
>
> - Malay patronymics (`bin`, `binti`, `a/l`, `a/p`)
> - Chinese names where the surname comes first
> - Indian patronymics
> - Names that collide when shortened, at three different card widths
>
> Store the email local part only; the domain lives once in `school.domain` (§5).
>
> Write the display-name tests as a table-driven fixture set — input name, card width, expected output. I expect the fixtures to be the substantial part of this task, not the logic.
>
> Run `dotnet test` and show me any case where the output looks wrong to you, even if the test passes.

---

## Prompt 4 — The importer (build step 5)

> Implement the CSV and XLSX importer in `src/Delima.Core`, per `Visual_SSO/PRD_Visual_SSO_v2.md` §6 Step 3 and `Visual_SSO/Technical_Architecture_Visual_SSO.md` §11's "Importer" row.
>
> Required behaviour:
>
> - **Column mapping, not fixed headers.** Real APDM exports run 15–30 columns wide with unpredictable names. The five target fields are listed in PRD §6 Step 3.
> - **Encoding detection** — ANSI, UTF-8 with and without BOM, UTF-16, CRLF and LF line endings. Malay diacritics must survive.
> - **A dry-run validation report** producing three groups: ready to import, warnings that do not block, and hard rejects. The exact categories are in PRD §6 Step 3.
> - **Idempotent re-import** per FR-S3.7 — re-importing must flag leavers, not delete them.
>
> The test fixtures matter more than the code here. Build fixtures covering: blank rows, duplicate IDs, `m-` prefixed and bare IDs, malformed IDs, missing required fields, and a file with a byte-order mark. **Do not use real pupil data in fixtures — generate plausible synthetic names.**
>
> Run `dotnet test` and show me the dry-run report output for the messiest fixture you built.

---

## Reusable prompts

### Review what you just wrote

> Re-read `Visual_SSO/Technical_Architecture_Visual_SSO.md` §3 and §11, then review the code you just wrote against it. List anything that does not match the specification, anything you implemented differently than specified, and anything in the spec you skipped. Be specific and cite file and line. Do not fix anything yet — just tell me.

Use this after every substantial task. Assistants are noticeably better at finding their own gaps when asked in a separate turn than they are at avoiding them in the first place.

### When something does not work

> `dotnet test` fails with the output below. Before changing any code, tell me:
>
> 1. What the error actually means, in plain language
> 2. Which of these is true — the test is wrong, the code is wrong, or the spec is ambiguous
> 3. What you propose to change, and why
>
> Do not make the change until I reply.
>
> ```
> [paste the full output here]
> ```

The point of this one is to stop the assistant "fixing" a failing test by weakening the test — which is a real and common failure mode, and much worse here than a bug, because the tamper tests are the safety net.

### Before you commit

> Show me `git status` and `git diff --stat`. Then, for each file changed, give me one sentence on what changed and why. Flag anything you touched that I did not ask you to touch.

---

# Phase 2 — Windows

Everything above builds on any machine. Everything below needs Windows (`Build_Machine_Setup.md`, Parts 0–4 — **skip Parts 5 and 6**, they are the optional signing steps and signing happens in CI).

---

## Prompt 4b — Extract `Delima.Import` (do this on the Mac, before switching)

> Extract the roster importer out of `src/Delima.Admin` into its own project, `src/Delima.Import`, targeting `net10.0`.
>
> Reason: `Delima.Admin` becomes a WPF application shortly, which forces it to `net10.0-windows`. If the importer stays inside it, `Delima.Import.Tests` can no longer run on macOS or Linux, and the importer is the component whose fixtures need the most iteration. The test project is already named `Delima.Import.Tests`, which matches this split.
>
> - Move `Import/` — `ColumnMapping`, `DataFileReader`, `FileEncodingDetector`, `ImportModels`, `RosterImporter` — into `Delima.Import`
> - `Delima.Import` references `Delima.Core` only; no Windows-only or UI packages
> - `Delima.Admin` references `Delima.Import`
> - Repoint `Delima.Import.Tests` at the new project
>
> Run `dotnet test`. The same tests must pass, with the same count, before and after. Show me both numbers.

Cheap now — five files. Expensive once WPF is in the way.

---

## Prompt 5 — Verify the Windows environment

> This repository was developed on macOS. I have moved to a Windows machine. Before writing anything new, confirm the existing work still builds and passes here.
>
> Run `dotnet build` and `dotnet test`, and show me the full output. Report the test count and compare it to what the repository's last commit message claims. If anything fails, diagnose it before changing it — a test that passes on macOS and fails on Windows is telling us something real about the code, usually line endings, path separators, or culture-sensitive string comparison.

Do this **before** the first Windows-only line of code. If something is broken you want to know it was the platform, not the new work.

---

## Prompt 6 — `Delima.Win32` (build step 8)

The highest-confidence work in the project: most of it already exists and has been proven on real hardware.

> Create `src/Delima.Win32`, targeting `net10.0-windows`, per `Visual_SSO/Technical_Architecture_Visual_SSO.md` §2 and §4.
>
> **Promote, do not rewrite.** `InjectionSpike/NativeMethods.cs` and `InjectionSpike/ChromeLauncher.cs` already implement the hard parts correctly and passed T0.3 at 100/100 on real lab hardware (`Visual_SSO/T0.3_Injection_Test_Protocol.md`). Move them in and tidy them; do not reimplement them. If you think something in them is wrong, tell me before changing it — that code is the only part of this project with real-world evidence behind it.
>
> Then add what the spike did not have, per §4.2:
>
> - **Window verification before every keystroke.** Two layers, and §4.2 is exact: window identity by class `Chrome_WidgetWin_1` **and PID belonging to the process tree this launch started** — never by title — then page identity by exact full-title match from config, held stable across ≥ 3 consecutive polls. T0.2 confirmed the identifier and password pages are distinguishable, but only on exact strings; substring matching is what produced 47 false ready states in T0.3.
> - **An abort path** that sends nothing and returns a failure code from the taxonomy in §7.
> - **The topmost overlay**, which §4.2 marks as required rather than optional — T0.3 found `BlockInput` is consistently denied to non-elevated processes on lab PCs, so the overlay is the actual defence, not a fallback.
>
> `Delima.Win32` may reference `Delima.Core`. `Delima.Core` must never reference `Delima.Win32`.
>
> Do not wire this to any UI yet.

---

## Prompt 7 — The per-PC DPAPI store

This completes build step 3, which was deliberately left half-done because it cannot work off Windows.

> Implement the per-PC credential store described in `Visual_SSO/Technical_Architecture_Visual_SSO.md` §3.3 and §3.5, against the `ICredentialStore` interface already in `Delima.Core`.
>
> **Put the implementation in `Delima.Win32`, not `Delima.Core`.** DPAPI is Windows-only, and `Delima.Core` must stay cross-platform (§2). Tell me if you disagree before moving anything.
>
> Requirements:
>
> - `ProtectedData` with **`LocalMachine` scope**, plus entropy — not `CurrentUser`. §3.3 requires any pupil account on the PC to be able to read it; a user-scoped store would give every Windows profile its own broken copy.
> - The file location and ACLs from §3.5
> - The decryption discipline from §3.4 — plaintext must not linger
>
> §3.3 states plainly what this does and does not protect against. Read it, then write me one paragraph in your own words on what an attacker with an interactive lab session can still do. If your answer is more reassuring than §3.3 is, you have misread it.

---

## Prompt 8 — `Delima.Provision` (build step 7)

> Implement `src/Delima.Provision` per `Visual_SSO/Technical_Architecture_Visual_SSO.md` §10 — the seven numbered steps are the specification.
>
> It must work as a single self-contained executable run from a pendrive on each lab PC, so keep it dependency-light.
>
> Include the silent mode: `--quiet --pack <path> --passphrase-stdin`. **The passphrase is read from stdin and must never appear in a command line or a process list** — that is the entire point of the flag. Exit 0 on success, non-zero on failure, so PDQ or GPO can drive 40 PCs from one prompt.

---

## Prompt 9 — Launcher shell and the first two screens (build step 9)

> Create `src/Delima.Launcher`, a WPF application targeting `net10.0-windows`, and implement the **Pilih Kelas** and **Cari Nama** screens.
>
> Read first, in this order: `Visual_SSO/Technical_Architecture_Visual_SSO.md` §6.1–6.7, `Visual_SSO/PRD_Visual_SSO_v2.md` §7, and `Normal_SSO/stitch-wireframes/PROMPT.txt`. `Visual_SSO/mockups/DELIMa_Screen_Mockups.html` shows the intended result — treat it as a visual reference to reproduce in XAML, not as code to port.
>
> Binding constraints from those documents:
>
> - **No third-party UI kit** (§6.1). Retemplate native controls.
> - **Theming is runtime data, not compiled resources** (§6.2) — colours come from the school's config.
> - **The name grid column count is computed in the ViewModel** and bound to a `UniformGrid` (§6.3); it must handle a 44-pupil class at 1366×768.
> - **Fonts are embedded** via pack URIs (§6.5). Nunito and Baloo 2, both OFL-1.1.
> - **Accessibility is binding** (§6.7): `AutomationProperties.Name` in Bahasa Melayu on every interactive element, tab order matching visual order, ≥48×48 px targets, 4.5:1 contrast.
> - **All pupil-facing text in Bahasa Melayu**, and the forbidden vocabulary list in PRD §7 applies — *SSO, portal, autentikasi, sesi, log masuk tunggal* must never appear.
>
> Use the roster model and display-name logic already in `Delima.Core`. Do not reimplement them.

---

## Prompt 10 — Picture password (build step 10)

> Implement the picture-password screen per `Visual_SSO/PRD_Visual_SSO_v2.md` §7.3 and the security requirements in `Visual_SSO/Technical_Architecture_Visual_SSO.md`.
>
> Two requirements that are easy to get wrong and matter more than they look:
>
> - **The shuffle must not animate** (§6.6). Animation leaks position information and makes the layout predictable to an observer. This is deliberate; do not add motion here for polish.
> - **Lock out after 5 failed attempts**, and verify with Argon2id.
>
> This screen is what closes blocker B1 — a pupil signing in as a classmate — which was v1's largest defect. Treat it accordingly.

---

## Prompt 11a — Make the UIA gate actually able to run

The gate is built and correct. It is switched off twice over, and enabling only one switch breaks the product.

> `Delima.Win32/UiaHelper.cs` and the `CheckUiaPasswordElement` gate in `RouteCLoginOrchestrator` are implemented and fail closed correctly — that part is right, leave the failure semantics alone. But the gate cannot currently work:
>
> 1. `ChromeSession` does not launch Chrome with **`--force-renderer-accessibility`**. Chrome builds its accessibility tree lazily, so `UiaGetFocusedElement` has nothing to read. Add the flag.
> 2. `CheckUiaPasswordElement` defaults to `false`, so the gate never runs at all.
>
> **Add the flag first, and leave the default as `false` for now.** Turning the gate on before Chrome exposes an accessibility tree would abort every password injection — safe, but the product would never sign anyone in. The default flips to `true` only after T0.4 (arch §11.1) measures that `IsPassword` is reported reliably on lab hardware.
>
> Then strengthen the test. `UiaHelper_IsFocusedElementPassword_ReturnsBooleanWithoutCrashing` asserts only that the call does not throw, which is honest but proves nothing about correctness. Add a test that a known password field returns `true` and a known text field returns `false` — a WPF `PasswordBox` and `TextBox` in a test window are enough to exercise it without needing Chrome.
>
> Record in `Visual_SSO/T0.2_URL_Confirmation.md` or a T0.4 result section which of the two switches are on, so nobody reading the code assumes the hardening is active when it is not.

---

## Prompt 10b — Stop the ACL failure being silent

Small, and it protects the credential store.

> In `src/Delima.Win32/Store/StoreAclConfigurator.cs`, `SetAccessControl` is wrapped in a `try`/`catch (UnauthorizedAccessException)` whose body is a comment. If applying the ACL fails, the credential store keeps whatever permissions it had, the caller is told nothing, and nothing is recorded.
>
> Arch §3.5 names this ACL as the control that stops a pupil opening `credentials.dat` in Explorer, so a silent failure is the difference between the documented protection and none at all.
>
> Change it to: write the failure to the audit log (§8) and return or throw so the caller can surface it as a provisioning error. Do not leave a code path where the store is unprotected and the app believes it succeeded.
>
> Check the other `catch` blocks in that file for the same pattern while you are there. Leave the `if (!OperatingSystem.IsWindows()) return;` guards alone — those are correct.

---

## Prompt 10a — Harden the injection engine against §4.2

Run this before Prompt 11. Two requirements from the revised §4.2 are not currently enforceable in `Delima.Win32`.

> Review `src/Delima.Win32/InjectionEngine.cs` against `Visual_SSO/Technical_Architecture_Visual_SSO.md` §4.2, which was revised after T0.2. Two gaps to close. Do not change anything else — the per-keystroke window re-verification already there is correct and exceeds what §4.2 requires, so leave it alone.
>
> **1. The title check cannot currently be trusted, because the engine has no opinion about it.** `Inject` accepts `Func<string, bool> titlePredicate`, so a caller can pass `t => t.Contains("Welcome")` and reintroduce the exact defect that produced 47 false ready states in T0.3. §4.2 requires an **exact full-string match against a configured value, never a substring**.
>
> Replace the predicate parameter with an expected-title `string`, compared with `string.Equals(..., StringComparison.Ordinal)`. If a predicate overload must remain for testing, mark it `internal` and keep it out of the public surface. The public API should make the unsafe thing impossible to express rather than merely discouraged.
>
> **2. `InjectionSettleMs` is a delay, not a stability check.** The current code sleeps for a fixed period and then verifies once. §4.2 requires the title be **stable across ≥ 3 consecutive 100 ms polls** before any keystroke. A delay does not detect a title that is still changing; two samples separated by a sleep are not three consecutive matches.
>
> Implement the settle as a polling loop: sample the title every 100 ms, require `title_settle_polls` (Appendix B, default 3) consecutive identical matches against the expected string, and abort to `E02` if that is not reached inside `window_wait_timeout_ms`.
>
> Then add tests for both: a substring-matching title must be rejected, and a title that changes during the settle window must abort rather than inject.

---

## Prompt 11 — Injection flow (build step 11) — **unblocked, T0.2 complete**

> Wire the injection flow per `Visual_SSO/Technical_Architecture_Visual_SSO.md` §4.2, §4.4, §4.5 and §7, using `Delima.Win32` from Prompt 6 as hardened by Prompt 10a.
>
> **`InjectionEngine` currently performs one injection.** Route C needs two — email, Enter, then password — each with its own expected title and its own verification. Build that orchestration here rather than by loosening the engine.
>
> **T0.2 selected route C (§4.5): the launcher types the email, then the password.** There is no pre-fill — DELIMa drops `login_hint`, and `/AccountChooser` is retired. Entry URL is `https://d3.delima.edu.my/landing`, and the pupil's sign-in button click is part of the flow.
>
> **Two injections means two verifications, and §4.2 is exact about this.** The identifier and password pages are distinguishable by title, but only under three conditions, all of which are requirements and not suggestions:
>
> - **Exact full-string match against a configured value, never a substring.** `Welcome` is a generic word.
> - **The title must be stable across ≥ 3 consecutive 100 ms polls** before anything is typed. Titles lag page state mid-transition; the T0.3 harness hit exactly this race.
> - **The password injection is sequence-gated.** It may fire only after the engine has observed a verified transition *out of* the identifier title. Matching the password-page title in isolation must never authorise typing a password.
>
> Titles are **per-locale configuration** (§3.2, Appendix B), not constants in code — Google's strings differ on a Malay-locale Chrome.
>
> Every failure mode in §7's taxonomy needs a code and a Bahasa Melayu message a seven-year-old can act on. Implement the floating reset bar from PRD §7.4.
>
> Then write the adversarial tests from §11. Steal focus at 500, 1000, 2000 and 4000 ms and **assert zero keystrokes are sent**. Add two more that T0.2 made necessary: assert nothing is injected while a title is still settling, and assert the password step refuses to fire without a preceding verified identifier state. Run on real lab hardware — never a developer machine, never over RDP.

**Consider T0.4 first** (arch §11.1) — UIA `IsPassword` as a second gate before the password injection. It is structural rather than textual, so it survives the locale and redesign risks the titles carry. Recommended before the pilot, not required before this prompt.

---

# Phase 3 — What remains

Build steps 6, 13, 14 and 15. Everything else in arch §12 is done.

---

## Prompt 12 — The Admin wizard (build step 6)

The largest remaining piece, and the one with the most spec behind it. `src/Delima.Admin` is currently empty — the importer moved to `Delima.Import` at Prompt 4b.

> Build `src/Delima.Admin` as a WPF application targeting `net10.0-windows`: the seven-step School Setup Wizard.
>
> Read first: `Visual_SSO/PRD_Visual_SSO_v2.md` §6 (the seven steps and their requirements) and `Visual_SSO/Technical_Architecture_Visual_SSO.md` §6.8 (the visual language, and it is deliberately not the Launcher's). `Visual_SSO/mockups/DELIMa_Admin_Wizard_Mockups.html` shows the intended result — reproduce it in XAML, do not port the HTML.
>
> **This app must not look like `Delima.Launcher`.** §6.8 is explicit: En. Zul is an adult doing data-entry at a desk, scanning a 2,000-row report for the three rows that are wrong. Rounded 20 px cards and 48 px touch targets fight that job. Radii 6–8 px, control heights 32–36 px, body text 13–14 px, alternating row striping, and a fixed-width font for anything columnar — pupil IDs, row numbers. Only the crest and a thin accent of the school colour carry over.
>
> Structural requirements from §6.8:
>
> - **Left sidebar step navigator** with per-step state. On first run steps 2 onward are locked until the preceding step completes; **once setup has been completed once, every step unlocks for direct navigation** — Step 3 and Step 4 are re-entered on completely different schedules and forcing a coordinator back through 1–2 to reach 4 in March is a real, remembered annoyance.
> - **The column mapper is a fixed list of the five target fields**, each with a dropdown of the source file's headers and a best-guess default — not a decision per column, since APDM exports run 15–30 columns wide. A live preview of the first 10 rows re-renders as each dropdown changes.
> - **The dry-run report is a full step, not a modal**, with three collapsible sections: ready, warnings (expanded), rejects (expanded).
> - **Step 4's consent screen** requires typing the school code, not ticking a box. A checkbox gets clicked without reading.
> - **The password grid reveals per row, not per grid** — passphrase prompt in a popover anchored to the row, auto-re-mask after 10 seconds or on losing focus, and every reveal written to the audit log per §8.
>
> Use `Delima.Import` for all import logic and `Delima.Core` for the store — reimplement neither. Blocked buttons must state their reason inline rather than sitting disabled and silent.

---

## Prompt 13 — Mod Guru (build step 13)

> Implement Mod Guru per `Visual_SSO/PRD_Visual_SSO_v2.md` §7.4, in `Delima.Launcher`.
>
> It is the teacher's escape hatch at a kiosk: PIN-gated, per Appendix B's `teacher_pin_policy` (4 digits, lock after 5 attempts). It must let a teacher resolve a stuck pupil without a support call — that is the feature's whole purpose (PRD §3, Cikgu Farah).
>
> Every action taken in Mod Guru is written to the audit log (§8) with the Windows user and timestamp. Use the `AuditLogger` already in `Delima.Core`.
>
> Accessibility applies here as much as to the pupil screens (§6.7): this is used standing at a shared kiosk, so keyboard-only operation matters. Bahasa Melayu throughout, and the forbidden vocabulary list still applies.

---

## Prompt 14 — Kiosk hardening and Chrome policy (build step 14)

`KioskGuard` and `TopmostOverlay` exist from Prompt 6. This completes the surrounding controls.

> Complete kiosk hardening per `Visual_SSO/Technical_Architecture_Visual_SSO.md` §9.
>
> Already built: `KioskGuard`, `TopmostOverlay`, the injection shield. What remains is the environment around them — idle reset on `idle_reset_seconds` (Appendix B, default 600) wiping the profile and zeroing credentials, launch-at-logon, and the Chrome enterprise policy values.
>
> **The Chrome policy writes to `HKLM` and changes Chrome for every user on the machine, including the teacher's own browsing.** It is opt-in, and whatever surfaces it must say so plainly (PRD §8.3). Do not apply it silently.
>
> **AppLocker is deliberately out of scope for code.** It depends on the school's Windows edition and existing group policy, and a checkbox that silently fails on Windows Home would be worse than none — a coordinator would believe he was protected. Produce it as a documented PowerShell snippet plus a required line on the lab checklist, per PRD §8.3.

---

## Prompt 16 — The T0.1 statement: fix the legal claim, add the two missing placements

PRD §8.7 requires the statement in three places. `Delima.Admin`'s first-run screen has it and is mostly right; the other two are missing, and one paragraph needs correcting.

> **1. Fix the legal claim in `src/Delima.Admin/Views/FirstRunDisclaimerView.xaml`.** It currently reads that the school is *"pengawal data mutlak di bawah Akta Perlindungan Data Peribadi 2010 (PDPA)"*. That is wrong twice over:
>
> - **PDPA 2010 excludes the Federal and State Governments from its scope**, and a government school is plausibly within that exclusion — so the Act may not apply at all.
> - Even where it does apply, the Act's term is **`pengguna data`**, not the GDPR-style `pengawal data`.
>
> Asserting a statute incorrectly, in the one document whose entire purpose is honesty, is worse than not naming a statute. Replace that sentence with the wording from PRD §8.7 point 3, which states the responsibility plainly and leaves the legal classification open:
>
> *"Sekolah yang memasang perisian ini bertanggungjawab sepenuhnya ke atas kata laluan murid yang disimpan — termasuk keputusan untuk menggunakannya, cara ia dilindungi, dan pematuhan terhadap mana-mana dasar atau perundangan yang terpakai. Tanggungjawab untuk menilai amalan ini dan mendapatkan kelulusan daripada pihak berkuasa yang berkenaan terletak pada pihak sekolah, bukan pada pembangun perisian."*
>
> **2. Strengthen point 2 while you are there.** It says the developer holds no approval letter, which reads as a gap in the developer's paperwork. PRD §8.7 requires the stronger and more accurate statement — that nobody has ruled either way. Add: *"Ini bermakna amalan tersebut tidak pernah disahkan, dan tidak pernah dilarang, oleh mana-mana pihak berkuasa."*
>
> **3. Add the statement to `installer/assets/LESEN.rtf`**, all four points from PRD §8.7, ahead of the licence terms, so the coordinator scrolls past it before accepting.
>
> **4. Add it to the release notes** generated by `.github/workflows/release.yml`, **above the download links, not below them**.
>
> Use the exact Bahasa Melayu from PRD §8.7 in all three places. **Do not paraphrase per placement** — the same statement worded three ways invites the question of which one is the real one.

---

## Prompt T0.4b — Apply the T0.4 findings to the code

**Run this before anything else.** T0.4 measured what the sign-in pages actually report, and all four relevant values in `RouteCLoginOrchestrator` and `InjectionEngine` still hold pre-measurement guesses. As it stands the identifier title never matches, so every sign-in aborts at `E02` — safe, but the launcher does not work.

> Apply the T0.4 results to `src/Delima.Win32`, per the revised `Visual_SSO/Technical_Architecture_Visual_SSO.md` §4.2 and Appendix B. Full evidence in `Visual_SSO/T0.4_UIA_Verification.md`.
>
> **1. `TitleIdentifierPage` is wrong.** It reads `"Sign in - Google Accounts - Google"`; the measured value is `"Sign in - Google Accounts - Google Chrome"`. The trailing ` Chrome` was dropped when T0.2 transcribed it. **This alone makes the product non-functional**, since exact matching never succeeds.
>
> **2. `TitlePasswordPage` cannot be a constant, and must stop being used as one.** The password page shows `"Welcome - Google Chrome"` briefly, then `"Hi <ACCOUNT HOLDER NAME> - Google Chrome"` — **it contains the pupil's own name**, so no fixed string matches it for more than one account.
>
> Restructure the password step's verification to what §4.2 now specifies:
>
> - **`IsPassword == true` via UIA is the primary gate.** T0.4 justifies this: 49/49 runs, and `true` on no page that was not a password page.
> - **The title check degrades to sequence-and-stability** — it must have changed away from `TitleIdentifierPage` and held stable for `TitleSettlePolls`. Not equality against a constant.
> - Keep `TitlePasswordPageGeneric` (`"Welcome - Google Chrome"`) only as an optional positive signal, never as a requirement.
>
> **3. Enable the gate.** `CheckUiaPasswordElement` → `true`. The measurement it was waiting on has been made and passed.
>
> **4. `InjectionSettleMs` 400 → 700.** T0.4 measured p50 314 ms, p95 417 ms, max 434 ms to `IsPassword` becoming readable. 400 sat below the 95th percentile, so about one sign-in in twenty would have started typing before the field was confirmed. 700 leaves headroom over the measured max.
>
> **Then add a regression test for the identifier title specifically.** A single dropped word made the whole product silently non-functional and nothing caught it; assert the configured value equals the measured string exactly, so a future edit cannot reintroduce it quietly.
>
> **5. Route C has a third step — the OAuth consent screen.** After the password is accepted, Google shows a consent page (*"Sign in to DELIMa 3"*, listing name and email, with **Cancel** and **Continue**) and only then reaches DELIMa. It appears on **every** sign-in for **every** account, because §4.4's throwaway Chrome profile means consent is never remembered.
>
> - Extend the sequence gate to a third state: identifier → password → consent → destination.
> - **Do not click Continue.** Two reasons, both in arch §4.5: it is a consent dialog, so automating it means the software consents on a seven-year-old's behalf every lesson; and the page shows the pupil's own name and email, making it an identity check that supports G2 — clicking through would destroy it. The pupil presses it.
> - Reaching the consent page is the **normal, successful terminal state** of injection — not a failure, not an `E0x`. The engine finishes there.
> - Show a line on the floating reset bar (PRD §7.4), since the pupil is looking at Chrome rather than the launcher. Clear it when the destination loads, and ensure the topmost overlay is down first. **Do not name the button in English** — Google localises it (`Continue` / `Teruskan`). Identify it by position and colour, and use the line to prompt the identity check: *"Lihat nama kamu. Kalau betul, tekan butang biru di bawah."*
> - Capture the consent page's window title on lab hardware and add it to Appendix B, the same way the other two were captured.
>
> Do not change the sequence gate's existing transitions, the per-keystroke re-verification or the fail-closed semantics — all three are correct.

---

## Prompt T0.4 — Build the UIA probe

Small observational tool. Procedure and pass conditions are in `Visual_SSO/T0.4_UIA_Verification.md`.

> Add a `uia` mode to `InjectionSpike`, or a small separate console project if that is cleaner — it needs to reference `Delima.Win32` for `UiaHelper` and `NativeMethods`.
>
> **It does not automate the sign-in.** An operator drives Chrome by hand; the probe only observes and records. This is deliberate: reaching the password page requires entering a real email address, and automating that adds failure modes without adding information.
>
> Behaviour, per iteration:
>
> 1. Launch Chrome at a configurable URL, defaulting to `https://d3.delima.edu.my/landing`, **with `--force-renderer-accessibility`** — without it Chrome exposes no accessibility tree and every sample is meaningless
> 2. Poll every 100 ms until the window closes, appending one CSV row per sample: `run`, `elapsed_ms`, `window_title`, `focus_resolvable`, `is_password`
> 3. `focus_resolvable` is whether UIA returned a focused element at all; `is_password` is the property value, left **blank** when unresolvable — do not write `false` for "could not tell", since questions 2 and 4 depend on telling those apart
> 4. When the operator closes the window, finish the run and relaunch, up to `--runs` (**default 20** — fifty is more than the questions need and long enough that runs get abandoned mid-way, which biases the sample)
>
> Also add a `--no-accessibility` switch that omits the flag, so question 6 — whether forcing accessibility slows Chrome's start — can be measured by comparison.
>
> Write the CSV to `spike-results/uia_<timestamp>.csv`. **No password is ever typed by this tool or by the operator**, so it needs no credential handling at all — do not add any.
>
> **Then have the probe answer the six questions itself**, printed as a summary block when the last run finishes and appended to the CSV folder as `uia_<timestamp>_summary.txt`. Reading 50 runs × ~200 samples by hand in a computer lab is how mistakes get made.
>
> Take the two expected titles from `Appendix B` config keys `title_identifier_page` and `title_password_page` (arch §4.2), matched exactly, so the summary uses the same strings the product will:
>
> ```
> T0.4 — UIA IsPassword verification
> Runs: 50    Chrome: <version>    Accessibility flag: on
>
> Q1  Focus resolvable, identifier page ......  50/50   PASS  (need >= 49)
> Q2  IsPassword == false there .............   50/50   PASS  (need 50, no tolerance)
> Q3  Focus resolvable, password page .......   49/50   PASS  (need >= 49)
> Q4  IsPassword == true there ..............   50/50   PASS  (need 50, no tolerance)
> Q5  Page load -> property readable ........   p50 180 ms   p95 340 ms
> Q6  Launch -> first title, flag on/off ....   1240 ms / 1190 ms  (+50 ms)
>
> VERDICT: PASS — enable CheckUiaPasswordElement
> ```
>
> **Two rules for the summary, both about not flattering the result:**
>
> - A run where the page was never reached at all is **excluded from the denominator and reported separately** as an incomplete run. It is operator error, not a UIA failure, and folding it in either way distorts the answer.
> - **Any `is_password=true` observed on a title that is not the password page is a hard fail**, reported on its own line regardless of the counts. That is the one outcome that means a password could be typed into a visible field, and it must not be averaged into a percentage.

---

## Prompt 15a — Two fixes in the installer and pipeline

Both small, both would bite on the first real release.

> Two corrections against `Visual_SSO/Build_And_Release.md`. Change nothing else — the AppLocker handling, the opt-in Chrome policy task, the signed-file checksum ordering and the draft release are all correct as built.
>
> **1. `.github/workflows/release.yml` calls `iscc` without installing Inno Setup.** GitHub's `windows-latest` image does not ship it, so the job fails at that step; and if a version ever is present, it is unpinned, which breaks reproducibility and SignPath's origin verification (§4.2 of that document requires the build to be fully determined by the repository).
>
> Add an explicit install step before the compile step, pinned to a version:
>
> ```yaml
> - name: Install Inno Setup
>   run: winget install --id JRSoftware.InnoSetup -e -v 6.7.3 --silent `
>          --accept-package-agreements --accept-source-agreements
> ```
>
> Then invoke `iscc` by full path rather than relying on `PATH`, or add its directory to `PATH` in the same step. Confirm the compile step actually runs — a silent skip here produces a release with no installer in it.
>
> **2. `installer/DelimaLauncher.iss` dropped `everyone-none` from the `[Dirs]` permissions.** It currently reads `Permissions: admins-full`; §5 specifies `Permissions: everyone-none admins-full`.
>
> Without `everyone-none` the directory keeps `%ProgramData%`'s inherited grant, which gives every interactive user — every pupil — read access. `StoreAclConfigurator` does fix this at runtime by disabling inheritance, and the comment in the file correctly says so, but that leaves a window between installation and first provisioning where the directory is readable, and it removes a layer that was specified deliberately. Restore it.
>
> Then verify empirically per arch §11.0: install on a test machine, log in as a **standard non-admin account**, and confirm `%ProgramData%\DELIMa Launcher` cannot be listed or read.

---

## Prompt 15 — Installer and release pipeline (build step 15)

> Build the installer and release pipeline per `Visual_SSO/Build_And_Release.md`.
>
> **The Inno Setup script is already written out in full in §5 of that document.** Copy it into `installer/DelimaLauncher.iss` and adapt only what the real project layout requires — do not generate a new one. Same for the publish flags in §3: they are specified, including the two that fail at runtime rather than build time (`PublishTrimmed` must stay `false` — WPF is not trim-compatible).
>
> Then the GitHub Actions workflow per §4.4: triggered on `v*` tags, `windows-latest`, build → test → publish → package → **submit to SignPath** → checksum the *signed* installer → create a **draft** release. Leave it a draft; a human confirms the T0.1 responsibility statement (PRD §8.7) is on the release page before it goes public.
>
> Three properties the workflow must preserve, because they are what SignPath's origin verification checks: every build setting lives in the repository, no step reuses a cached build output, and the workflow accepts no inputs that change what gets compiled.
>
> Generate a fixed `AppId` GUID once and add a comment saying never to change it — it is how Windows knows 2.0.1 upgrades 2.0.0 rather than installing beside it.

---

## Constraints to repeat when the assistant drifts

Paste any of these when you see the relevant mistake:

- *"`Delima.Core` must not reference `Delima.Win32`, any Windows-only package, or any UI framework. Architecture §2 requires this so the store and importer stay testable on any machine. Revert that reference."*
- *"Do not write custom cryptography. Use the packages in Appendix A and the .NET base class library."*
- *"That test now passes because you weakened the assertion. Restore the original assertion and fix the code instead."*
- *"No real pupil names, real passwords, or real school data anywhere in this repository, including test fixtures and comments."*
- *"We target .NET 10, not .NET 8. .NET 8 leaves support in November 2026."*
- *"Do not add that package. Appendix A lists the approved dependencies and says to keep the list short."*
- *"Stop and plan first. Tell me what you intend to change and why, before changing it."*

---

## What not to ask for yet

These depend on work not done, or on decisions not made. Asking early produces code you will throw away.

| Not yet | Why | Unblocked by |
| :--- | :--- | :--- |
| The injection flow (Prompt 11) | Needs the confirmed live SSO URL | **T0.2** |
| Audit log, Mod Guru, kiosk hardening | Need the injection flow working first | Steps 12–14 |
| The Inno Setup script | It is already written out in `Build_And_Release.md` §5 — copy it, don't generate it | Step 15 |
| The GitHub Actions workflow | Needed only for the first release | Step 15 |
| The SignPath application | The Foundation requires the project already be released in the form to be signed | One unsigned release |

**T0.2 is the one to do now** — one real pupil account, an afternoon, and it is the only thing standing between you and Prompt 11. If you are travelling to a school for a Windows machine anyway, do both on the same trip.
