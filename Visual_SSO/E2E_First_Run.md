# End-to-End — the first full run

**Status:** Not started
**Needs:** a Windows lab PC, a pendrive, and **one Google account you personally control**
**Time:** two to three hours for the first attempt
**Prerequisite:** Prompt 17

This is the first time the whole chain runs as one thing. Every component has been tested alone; none have been tested together, and the seams between them are where this will fail.

---

## Part 0 — This is bigger than a sign-in

"End-to-end test" undersells it. Nothing has ever produced a `school.dlmpack`, so the sign-in is only the last link:

| # | Stage | Produces | Never run before |
| :-- | :--- | :--- | :--- |
| 1 | `Delima.Admin` wizard, steps 1–7 | `school.dlmpack` | ✔ |
| 2 | `Delima.Provision` on a lab PC | `credentials.dat` | ✔ |
| 3 | `Delima.Launcher` — class, name, picture password | injection | ✔ |
| 4 | Two injections, then consent | signed-in session | ✔ |

**Expect stage 1 or 2 to fail first.** The sign-in is the part with the most evidence behind it — T0.3 and T0.4 both measured it. The wizard and provisioning have unit tests and have never met a real file, a real pendrive or a real ACL.

---

## Part 1 — Use your own account as the only pupil

**Do not put a real pupil's password into a system nobody has ever run end to end.**

Build a roster with **one row**, using an account you control — the same one used for T0.2 and T0.4:

```csv
BIL,NAMA MURID,NO. KAD PENGENALAN,TAHUN,KELAS,ID PENGGUNA DELIMA
1,Nama Anda,000000-00-0000,2,2 Ujian,g-41360438
```

And a password file with that one row, carrying the real password.

**Why this and not synthetic data:** the sign-in must actually succeed, so the password has to be real. Using your own account means the only credential at risk is yours, on a machine you control, in software you are testing precisely because you do not yet trust it. Scale to a real class only after a full clean pass.

**Afterwards:** uninstall, confirm `credentials.dat` is gone (PRD §8.4), and consider changing that password — it has been through a system on its first run.

---

## Part 2 — Run it

**Stage 1 — the wizard.** All seven steps on your own PC. Watch for:

- Step 3's column mapper against a real file, not a fixture
- The dry-run report — one row in, one row ready, zero rejects
- Step 4's consent screen (typing the school code) and the password grid
- Step 7 writing to an actual pendrive

**Stage 2 — provisioning.** Take the pendrive to a lab PC, run `Delima.Provision`. Then verify what the ACL test already proved once, now via the real path: log in as a **standard pupil account** and confirm `%ProgramData%\DELIMa Launcher\credentials.dat` will not open.

**Stage 3 — the launcher.** As the pupil account: pick class, pick name, picture password, then watch the injection.

**Stage 4 — the seam that matters.** Email typed → Enter → password typed → Enter → **consent screen**, with the floating bar showing the instruction, the overlay down, and nothing further typed. Press Continue yourself and confirm you land on DELIMa.

---

## Part 3 — What to record

| Item | Why |
| :--- | :--- |
| Which stage failed first, if any | The seams are the point |
| Wall-clock time for one pupil, click to signed-in | The first real data against G1 |
| Cold-start time of the launcher | arch §11.0 item 3, deferred until now |
| The **consent page's window title**, exactly | Still unmeasured; Prompt 17 works around it |
| Anything that needed a second attempt | A retry a teacher can absorb is different from one a seven-year-old cannot |
| Whether the audit log recorded the session | §8 — never verified against a real run |

**Also try the failure paths deliberately**, since the pupil will find them anyway: wrong picture password five times, cancel mid-injection, close Chrome mid-injection, and press **Cancel** on the consent screen rather than Continue. That last one has no specified behaviour and needs one.

---

## Part 4 — Timing

**Do this before, not after, inviting a class.** A stage-1 failure with a teacher and 44 pupils waiting is an entirely different event from the same failure on a quiet afternoon.

**One pupil, one clean pass, then one class.** G1's under-three-minutes target is measured with a real class (PRD §4) — but not the first time the software has ever run.
