# Building This With an AI Assistant — Step by Step

**For:** someone who has not built a real application with an AI coding assistant before.
**Assumes:** you can use a terminal a little, and you have a Google or Anthropic account.
**Result:** a working, tested `Delima.Core` — the credential store and roster logic — with everything committed to git.

The prompts themselves live in `Build_Prompts.md`. This document is the workflow around them: what to install, what order to work in, and — the part most guides skip — **how to tell whether the code you got is any good when you can't read all of it yourself.**

> **Current status:** All 15 build steps and Prompts 0–19 have now been implemented and tested in this codebase. This guide remains the reference for the assistant-driven development methodology used to build the solution.

---

## Part 0 — What you are actually doing

You are not asking a chatbot to write an app. You are running a project where the assistant does the typing and **you make the decisions and check the work.** That distinction is the whole game.

Three things only you can do:

1. **Decide when something is done.** The assistant will tell you a task is complete. Sometimes it isn't. Tests are how you check.
2. **Notice when it drifts from the spec.** It will occasionally invent a simpler design than the one specified, because the simpler one is more common in its training. The specs exist to be the authority.
3. **Refuse bad fixes.** The most damaging thing an assistant does is make a failing test pass by weakening the test. Watch for it.

**You do not need to understand every line of C#.** You do need to understand what each part is *for*, and the specs already tell you that in plain language.

---

## Part 1 — Install the tool

**Gemini CLI** — free tier, runs in your terminal, can read and edit files in the repository.

1. Install Node.js if you don't have it: **https://nodejs.org** — take the LTS version.
2. Verify it worked. Open Terminal and run:

   ```bash
   node --version
   ```

   You should see a version number. If you see "command not found", restart the Terminal, then restart the computer if it still fails.
3. Install the CLI:

   ```bash
   npm install -g @google/gemini-cli
   ```
4. Sign in:

   ```bash
   gemini login
   ```

   A browser window opens. Use your Google account.

> Google has announced **Antigravity** as the successor to Gemini CLI for consumer users. If the install above doesn't work or you're pointed at something newer, use whatever Google currently ships — everything in this guide is about the workflow, not the specific tool, and works equally with Claude Code or any other agent that can read files and run commands.

**Install the .NET 10 SDK** as well, or the assistant cannot compile or test anything it writes: **https://dotnet.microsoft.com/download/dotnet/10.0** — take the **SDK**, not the Runtime. Verify:

```bash
dotnet --version
```

You want a `10.` version.

---

## Part 2 — Open the project

```bash
cd "/Users/rajazrien/z.Dev Space/SSO Sks24"
gemini
```

Starting the tool *inside the project folder* is what gives it access to the specification files. If you start it somewhere else it has nothing to read.

**Check git is clean before you begin:**

```bash
git status --short
```

Nothing should print. If something does, commit or discard it first — you want a clean line to fall back to.

---

## Part 3 — The first session

**Paste Prompt 0 from `Build_Prompts.md`.** It asks the assistant to read the specs and answer five questions. It is not allowed to write any code.

**Read the answers properly.** This is the cheapest quality check you will get all project. You are looking for:

- Question 2 — does it know `Delima.Core` may not reference Win32 or UI code? That boundary is what lets you build on a Mac.
- Question 4 — does it know what's forbidden? No cloud, no custom crypto, no third-party UI kit.
- Question 5 — does it correctly identify T0.2 as the only open blocker?

**If any answer is wrong, correct it before continuing.** Say so directly: *"Answer 2 is wrong. Re-read architecture §2 and try again."* An assistant that has misunderstood the boundaries will produce code that violates them.

Then run **Prompt 1** (scaffolding) and **Prompt 2** (the credential store). Prompt 2 is the big one and may take a while.

---

## Part 4 — How to check work you can't fully read

This is the part that matters. Five checks, in increasing order of effort.

### Check 1 — Do the tests pass?

```bash
dotnet test
```

You want `Passed!` and a test count that isn't zero. **Zero tests passing is not success**, and an assistant will sometimes report success on an empty test run.

### Check 2 — Do the *right* tests exist?

Ask directly:

> List every test you wrote, with a one-sentence description of what each one proves.

For Prompt 2, three specific tests must be there: flipping a byte in the **header**, in the **ciphertext**, and in the **authentication tag** — each asserting that decryption fails. If any of the three is missing, the tamper detection is unproven. Ask for it.

### Check 3 — Does a test fail when it should?

The single most valuable check in this guide, because it catches tests that pass no matter what.

> Temporarily change the code so the tamper test should fail. Run the tests and show me that it does fail. Then undo the change.

If the test still passes after the code was broken, **the test is worthless** and everything it was supposed to guarantee is unguaranteed. This takes two minutes and finds a class of problem nothing else catches.

### Check 4 — Ask it to review itself

Paste the **"Review what you just wrote"** prompt from `Build_Prompts.md`. Assistants find their own gaps far more reliably when asked in a fresh turn than they avoid them while working.

### Check 5 — Read the diff

```bash
git diff --stat
```

You don't need to understand every line. You are looking for surprises: files changed that you didn't ask about, a new package appearing, a test file shrinking.

---

## Part 5 — Commit

Once tests pass and the checks above are satisfied:

```bash
git add -A
git commit -m "Delima.Core: credential store, crypto, tamper tests"
```

**Commit after every completed task, not at the end of the day.** Commits are how you undo a session that went badly. Without them your only options are keeping bad code or losing good code along with it.

To see where you are:

```bash
git log --oneline
```

To throw away uncommitted changes and return to the last good state:

```bash
git restore .
```

---

## Part 6 — The order to work in

| # | Task | Prompt | Machine |
| :-- | :--- | :--- | :--- |
| 1 | Orientation | Prompt 0 | Any |
| 2 | Solution scaffolding | Prompt 1 | Any |
| 3 | Credential store + tamper tests | Prompt 2 | Any |
| 4 | Roster model + display names | Prompt 3 | Any |
| 5 | Importer + fixtures | Prompt 4 | Any |
| — | **T0.2 — confirm the SSO URL** | not a coding task | A real pupil account |
| 6 | `Delima.Win32`, WPF screens, injection | later | **Windows required** |

**Steps 1–5 all run on your Mac.** `Delima.Core` deliberately contains no Windows code, which is why. Windows becomes necessary at task 6, which is a long way off.

**Do T0.2 while you work through 3–5.** It needs one real pupil account and an afternoon: confirm the live `d3.delima.edu.my` entry URL, and whether `login_hint` is honoured. It blocks the injection flow later, and there is no reason to discover that on the day you need it.

---

## Part 7 — When it goes wrong

**It says a task is done but tests fail.**
Paste the failing output with the "When something does not work" prompt from `Build_Prompts.md`. It forces a diagnosis before a change.

**It made a failing test pass by changing the test.**
Say: *"That test now passes because you weakened the assertion. Restore the original assertion and fix the code instead."* Watch for this specifically around the tamper tests — those three are the safety net for the whole credential store.

**It added a package you didn't approve.**
Appendix A of the architecture document lists the approved dependencies and says explicitly to keep the list short, because every package is something a school's antivirus may flag. Say: *"Do not add that package. Appendix A lists the approved dependencies."*

**It wrote Windows-only code in `Delima.Core`.**
Say: *"`Delima.Core` must not reference Win32 or any Windows-only package — architecture §2. Revert that."* You'll notice because the build stops working on your Mac.

**It's confidently wrong and won't let go.**
Start a fresh session. Context accumulates, and a conversation that has gone in circles tends to keep going in circles. You lose nothing if you committed.

**You don't understand what it did.**
Ask: *"Explain what this file does as if I have never written C#. What would break if it were wrong?"* This is a reasonable question and a good assistant answers it well. If the answer is vague, that's information about the code.

---

## Part 8 — Things to never let slide

Short list. Each one is in the specs; each is easy to erode by accident.

- **No real pupil data anywhere** — not in test fixtures, not in comments, not in example files. Synthetic names only. `.gitignore` blocks real CSVs, but it cannot block a name pasted into a comment.
- **No custom cryptography.** If the assistant writes a cipher, a padding scheme, or a key-derivation function, stop it.
- **The three tamper tests stay strict.** They are what proves a modified credential file is detected.
- **`Delima.Core` stays clean of Win32 and UI.** It is what keeps the project buildable and testable on your Mac.
- **.NET 10, not .NET 8.** .NET 8 leaves support in November 2026.

---

## Part 9 — What "done" looks like for this stage

You have finished the part you can do on a Mac when all of these are true:

- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes, with a non-zero test count
- [ ] Header, ciphertext and tag tamper tests all exist and all pass
- [ ] Breaking the code on purpose makes a tamper test fail (Check 3)
- [ ] Display-name fixtures cover Malay, Chinese and Indian conventions at three card widths
- [ ] The importer handles a UTF-8 BOM file, a UTF-16 file, duplicate IDs and malformed IDs
- [ ] No real pupil data anywhere in the repository
- [ ] `git status --short` is empty — everything committed
- [ ] T0.2 is answered (passed August 2026; Route C selected)

Then you need a Windows machine, and `Build_Machine_Setup.md` takes over.
