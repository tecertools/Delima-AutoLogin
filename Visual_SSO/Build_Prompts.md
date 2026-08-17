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
| `Delima.Win32`, injection engine | Needs a Windows machine | Build step 8 |
| Any WPF screen | Needs Windows; needs the roster model first | Steps 4, then 9 |
| The injection flow | Needs the confirmed live SSO URL | **T0.2** |
| The Inno Setup script | It is already written out in `Build_And_Release.md` §5 — copy it, don't generate it | Step 15 |
| The GitHub Actions workflow | Needed only for the first release | Step 15 |

**T0.2 is the one to start in parallel** — one real pupil account, an afternoon, and it blocks step 11.
