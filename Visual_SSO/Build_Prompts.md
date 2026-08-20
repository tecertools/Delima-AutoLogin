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
