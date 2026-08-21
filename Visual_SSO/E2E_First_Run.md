# End-to-End — the first full run, step by step

**Status:** Not started
**Needs:** a Windows PC you control, a Windows lab PC, a pendrive, and **one Google account you personally control**
**Time:** three hours for the first attempt. It will not go smoothly, and that is the point.
**Prerequisite:** Prompt 17

This is the first time the whole chain runs as one thing. Every component has been tested alone; none have been tested together, and the seams between them are where this will fail.

---

## Part 0 — What you are actually doing

"End-to-end test" undersells it. **Nothing has ever produced a `school.dlmpack`**, so the sign-in is only the last of four stages:

| Stage | What runs | Produces | Run before? |
| :--- | :--- | :--- | :--- |
| **1** | `Delima.Admin` — the seven-step wizard | `school.dlmpack` | Never |
| **2** | `Delima.Provision` on a lab PC | `credentials.dat` | Never |
| **3** | `Delima.Launcher` — class, name, picture password | injection begins | Never |
| **4** | Two injections, then the consent screen | a signed-in session | Never |

**Expect stage 1 or 2 to fail first.** Stage 4 has the most evidence behind it — T0.3 and T0.4 both measured it on real hardware. The wizard and provisioning have unit tests and have never met a real file, a real pendrive, or a real ACL.

**Work in stages and stop at each checkpoint.** If stage 2 fails you want to know it failed at stage 2, not spend an hour wondering why a sign-in did not work.

---

## Part 1 — Before you start

Tick all of these first.

- [ ] **Prompt 17 has been run**, otherwise stage 4's result will not mean what it appears to mean
- [ ] `dotnet build -c Release` succeeds and `dotnet test` passes
- [ ] A **Windows PC you control** — for stage 1
- [ ] A **Windows lab PC** — for stages 2–4, ideally the slowest one
- [ ] A **standard (non-admin) Windows account** on that lab PC, to run as a pupil
- [ ] **Administrator access** on that lab PC, for provisioning
- [ ] An **empty pendrive**
- [ ] **A Google account you control** — the same one used for T0.2 and T0.4 — and its password
- [ ] A notebook, paper or digital, for Part 9

> ### The one thing you cannot undo
>
> **The admin passphrase you set in stage 1 has no recovery.** Not by you, not by anyone. Lose it and you rebuild the bundle from scratch (PRD §6 Step 2).
>
> For this test use something you will not lose — write it down. This is a throwaway bundle containing one account that belongs to you.

---

## Part 2 — Make the test data

**Use your own account as the only pupil.** The password must be real for the sign-in to succeed, so the only question is whose. Do not put a pupil's credential into software that has never run end to end.

Create a folder, say `C:\e2e-test`, and two files in it.

**`roster.csv`** — replace the name and the `g-` address with your own:

```csv
BIL,NAMA MURID,NO. KAD PENGENALAN,TAHUN,KELAS,ID PENGGUNA DELIMA
1,Nama Anda Di Sini,000000-00-0000,2,2 Ujian,g-41360438
```

**`katalaluan.csv`** — your real password:

```csv
ID PENGGUNA DELIMA,KATA LALUAN
g-41360438,PasswordSebenarAnda
```

**Save both as UTF-8.** In Notepad: *File → Save As → Encoding: UTF-8*. The importer detects encodings, and this is a chance to hand it a clean one first.

> **Delete both files at the end of Part 11.** The second one is a plaintext password sitting in a folder.

---

## Part 3 — Publish the apps

On your own PC, from the repository root:

```powershell
$cfg = "-c Release -r win-x64 --self-contained true /p:PublishSingleFile=true"
dotnet publish src\Delima.Admin\Delima.Admin.csproj         $cfg -o publish\Admin
dotnet publish src\Delima.Launcher\Delima.Launcher.csproj   $cfg -o publish\Launcher
dotnet publish src\Delima.Provision\Delima.Provision.csproj $cfg -o publish\Provision
```

**Checkpoint.** Each folder should contain essentially **one** `.exe`, tens of megabytes. Dozens of loose `.dll` files means single-file publishing is misconfigured — see `Build_And_Release.md` §3.

---

## Part 4 — Stage 1: the wizard

Run `publish\Admin\Delima.Admin.exe`.

**First screen — the T0.1 disclaimer.** Read it. This is the statement you will be asking other schools to accept, and this is the only time you will see it with fresh eyes. Tick the box, continue.

**Step 1 — Identiti Sekolah.** School code `UJIAN`, any school name, skip the crest. Note the contrast checker on the colour palette — that is FR-S1.4 working.

**Step 2 — Kata Laluan Pentadbir.** Set the passphrase. **Write it down now.**

**Step 3 — Import Senarai Murid.** Choose `roster.csv`.

- The **column mapper** shows five target fields with dropdowns. Check its guesses — `NAMA MURID` → Nama penuh, `ID PENGGUNA DELIMA` → ID DELIMa. Correct any it got wrong.
- The **preview table** should show your one row with real values. If it shows blanks or shifted columns, the mapping is wrong.
- **The dry-run report** should read: 1 ready, 0 warnings, 0 rejects. Anything else, read the message — it is telling you something true about your file.

**Step 4 — Import Kata Laluan.** The consent screen requires typing the school code (`UJIAN`) rather than ticking a box. Then choose `katalaluan.csv`. The grid should show your pupil with `••••••`. Click the masked value — it should ask for the admin passphrase, reveal briefly, then re-mask after about ten seconds.

**Step 5 — Avatar & Kata Laluan Gambar.** An avatar is assigned automatically. **Print or photograph the class avatar sheet** — you need it in stage 3 to know which pictures to press.

**Step 6 — Destinasi & Tetapan.** Defaults are fine.

**Step 7 — Bina & Sediakan.** Choose **Pendrive**, pick the drive, write.

**Checkpoint.** The pendrive holds `school.dlmpack` and `Delima.Provision.exe`. Open the `.dlmpack` in Notepad — it must be **binary gibberish**. If you can read a name or an address in it, stop; the bundle is not encrypted and that is a serious defect, not a test failure.

---

## Part 5 — Stage 2: provision the lab PC

Take the pendrive to the lab PC. Sign in as an **administrator**.

Install the app — either run the installer if you have built one, or copy `publish\Launcher` and `publish\Provision` to `C:\Program Files\DELIMa Launcher`.

Then, in an **administrator** PowerShell:

```powershell
E:\Delima.Provision.exe --pack E:\school.dlmpack
```

Enter the admin passphrase when prompted.

**Checkpoint — three things, and the third is the one that matters.**

1. It exits without error
2. `%ProgramData%\DELIMa Launcher\credentials.dat` exists
3. **Sign out. Sign in as the standard pupil account.** Try to open that file in Notepad.

**You want "Access is denied."** Anything else means the ACL did not apply on this machine, and the credential store is readable by every pupil — arch §3.5 is not in force. Stop and fix it before going further.

---

## Part 6 — Stage 3: the launcher

Still signed in as the **pupil account**, run `Delima.Launcher.exe`.

1. **Pilih Kelas** — Tahun 2 → 2 Ujian
2. **Cari Nama** — your name should be on a card. If it is truncated oddly, that is the display-name logic meeting a real name
3. **Kata Laluan Gambar** — press the pictures from the sheet you saved in stage 1

**Checkpoint.** Chrome opens. **Start a stopwatch when you press the last picture** — that number is the first real data against G1.

---

## Part 7 — Stage 4: the sign-in

Watch closely and do not touch anything. In order:

1. Chrome reaches the Google sign-in page
2. **Your email is typed**, character by character
3. Enter is pressed; the password page appears
4. **Your password is typed** — masked
5. Enter is pressed
6. **The consent screen appears** — your name and email, **Cancel** and **Continue**
7. **Injection stops here.** The floating bar shows *"Lihat nama kamu. Kalau betul, tekan butang biru di bawah."*
8. **You press Continue.** The launcher must not press it
9. DELIMa loads. Stop the stopwatch

**Checkpoint — the four things that matter more than "it worked":**

- Did **anything** get typed before the right field had focus? Any stray characters visible anywhere?
- Did the overlay come down before the consent screen, so you could actually click?
- Did the launcher stop at consent, or try to continue past it?
- **Write down the consent page's window title, exactly.** Nothing has measured it, and Prompt 17 currently works around that.

---

## Part 8 — Break it deliberately

The pupil will find these anyway. Better you find them first.

| Try this | Should happen |
| :--- | :--- |
| Wrong picture password ×5 | Lockout, in Bahasa Melayu, no crash |
| Press cancel during injection | Typing stops, Chrome closes, profile wiped |
| Close Chrome mid-injection | Abort, an error a child could act on, nothing typed elsewhere |
| Alt-Tab during injection | **Nothing should be typed into the other window.** This is §4.2's whole purpose |
| Press **Cancel** on the consent screen | **Undefined.** No behaviour has ever been specified — record what happens |
| Leave it idle 10 minutes after signing in | Idle reset, profile wiped (Appendix B `idle_reset_seconds`) |

---

## Part 9 — Record

| Item | Why |
| :--- | :--- |
| Which stage failed first | The seams are the point |
| Seconds from last picture to signed-in | First real data against G1 |
| Launcher cold-start time | arch §11.0 item 3, deferred until now |
| **The consent page's window title, exactly** | Still unmeasured |
| Anything needing a second attempt | A retry a teacher absorbs differs from one a child cannot |
| Did the audit log record the session? | §8, never verified against a real run |
| What Cancel-on-consent did | Currently unspecified |

---

## Part 10 — When it fails

**Nothing is typed, Chrome just sits there.** Most likely a title mismatch — the exact string does not match what the page reports. Check the audit log for `E02`, and compare against the list from Prompt 17.

**The email is typed into the wrong place.** Stop the test. This is the failure the whole product is built to prevent, and it means §4.2's verification is not working. Record everything.

**Provisioning fails with an access error.** You are not running as administrator, or the lab PC restricts `%ProgramData%`. See `Build_Machine_Setup.md`, Part 8.

**The wizard rejects your CSV.** Read the dry-run message before changing the file — it is usually right. Encoding and column mapping are the two common causes.

**Everything works first time.** Be suspicious rather than pleased. Confirm you were signed out at the start, and that Chrome opened a fresh profile rather than reusing a signed-in session.

---

## Part 11 — Clean up

**Do all of it. This is the stage people skip.**

1. Uninstall, or delete `%ProgramData%\DELIMa Launcher`
2. **Confirm `credentials.dat` is gone** — PRD §8.4 requires secure deletion on uninstall, and this is the first chance to check it
3. Delete `C:\e2e-test\katalaluan.csv` — plaintext password in a folder
4. Delete `school.dlmpack` from the pendrive
5. Sign out of Google on the lab PC and clear any leftover Chrome profile
6. **Change your Google account password.** It has been through software on its first ever run

---

## Part 12 — Then, and only then, a class

**One pupil, one clean pass, then one class.** G1's under-three-minutes target is measured with a real class (PRD §4) — but not on the first day the software has ever run.

A stage-1 failure on a quiet afternoon is a very different event from the same failure with a teacher and 44 seven-year-olds waiting.
