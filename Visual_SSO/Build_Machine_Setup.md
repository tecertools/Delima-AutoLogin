# Setting Up the Build Machine — Step by Step

**For:** whoever compiles the release. Assumes no prior experience with .NET, code signing, or installers.
**Result:** a Windows PC that can turn the source code into one signed `DELIMaLauncher-Setup-2.0.0.exe`.
**Time:** about 90 minutes, most of it downloads. The certificate in Part 6 takes days and is started separately.

Once this is done, the actual build is the commands in `Build_And_Release.md`. This document only gets you to the point where those commands work.

---

## Part 0 — What you are about to install, and why

Four tools. It helps to know what each is for before you install it, so that when one of them fails you know which one to blame.

| Tool | What it does | Roughly |
| :--- | :--- | :--- |
| **.NET 10 SDK** | Turns C# source code into a running program | The compiler |
| **Git** | Downloads the source code and keeps versions straight | Version control |
| **Inno Setup 6** | Wraps the finished programs into one `Setup.exe` | The installer builder |
| **Windows SDK** | Provides `signtool`, which signs the exe so Windows trusts it | The signing tool |

You also need a **code-signing certificate** (Part 6). That is not software you install in ten minutes — it is bought from a certificate authority and involves them verifying your identity. Start it early; the rest of the setup does not depend on it.

---

## Part 1 — Choose the machine

**Requirements:** Windows 10 22H2 or Windows 11, 64-bit, about 20 GB free disk, and an account with administrator rights.

**Do not use a school lab PC.** Two reasons:

1. Lab PCs are locked down and builds fail on them in confusing ways. This project already hit "insufficient access to delete" on one during the injection test.
2. **The signing certificate must not live on a shared machine.** A certificate is what tells every school's Windows "this software really is from these people". Anyone who can copy it can sign anything in your name. A machine other people log into is the wrong place for it.

A personal laptop, a teacher's own PC, or a dedicated machine is fine. It does not need to be fast — a slow build is only annoying, a shared build machine is a security problem.

**To check your Windows version:** press `Windows key + R`, type `winver`, press Enter. A box appears with the version. You want 10.0.19045 or higher for Windows 10, or any Windows 11.

---

## Part 2 — Install the .NET 10 SDK

1. Go to **https://dotnet.microsoft.com/download/dotnet/10.0**
2. Under **SDK**, download the **x64 Windows Installer**. Take the **SDK**, not the Runtime — the Runtime only *runs* programs, the SDK *builds* them. This is the single most common wrong turn here.
3. Run the installer, accept the defaults.
4. **Close every PowerShell window you have open**, then open a new one. Installers change the list of places Windows looks for programs, and only new windows see the change.

**To open PowerShell:** press the `Windows key`, type `powershell`, press Enter.

**Verify:**

```powershell
dotnet --version
```

You should see something starting with `10.` — for example `10.0.100`.

If instead you see *"dotnet is not recognized"*, the install did not finish or you are in an old window. Close it, open a new one, try again. If it still fails, restart the PC — that reliably fixes it.

Also run:

```powershell
dotnet --list-sdks
```

Confirm a `10.x` line is listed. Older versions may also be listed; that is fine and harmless.

---

## Part 3 — Install Git

1. Go to **https://git-scm.com/download/win**, download the 64-bit installer.
2. Run it. Accept every default — there are many pages and none of them need changing.

**Verify:**

```powershell
git --version
```

You should see `git version 2.x`.

**Then tell Git who you are** (it refuses to record changes otherwise):

```powershell
git config --global user.name "Your Name"
git config --global user.email "you@example.com"
```

---

## Part 4 — Install Inno Setup 6

1. Go to **https://jrsoftware.org/isdl.php**
2. Download **Inno Setup 6** (the stable release, not the beta).
3. Run the installer, accept the defaults.

**Verify** — Inno Setup does not add itself to the command path, so check the file exists:

```powershell
Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
```

You want `True`. If you get `False`, look for it under `C:\Program Files\Inno Setup 6\` instead and note which path is correct — you will need it when building.

**Optional but recommended** — make `iscc` work from anywhere:

```powershell
$env:Path += ";C:\Program Files (x86)\Inno Setup 6"
```

That lasts only for the current window. To make it permanent: press `Windows key`, type `environment variables`, open *Edit the system environment variables* → *Environment Variables* → select `Path` under *System variables* → *Edit* → *New* → paste the folder path → OK on all three dialogs. Then open a fresh PowerShell.

---

## Part 5 — Install the Windows SDK (for `signtool`)

`signtool.exe` is the program that applies your certificate to the finished exe. It comes bundled inside the Windows SDK, which is a large download for one small tool — but there is no smaller supported way to get it.

1. Go to **https://developer.microsoft.com/windows/downloads/windows-sdk/**
2. Download and run the installer.
3. **On the features page, untick everything except "Windows SDK Signing Tools for Desktop Apps".** The default selection is several gigabytes of things you do not need. With only the signing tools it is a few hundred megabytes.

**Verify:**

```powershell
Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Recurse -Filter signtool.exe |
  Where-Object { $_.FullName -like "*x64*" } |
  Select-Object -ExpandProperty FullName
```

This searches for the tool and prints where it landed. You should get at least one path ending in `x64\signtool.exe`. **Write it down** — you need it in Part 7, and the version number in the middle of the path differs between machines.

---

## Part 6 — Get a code-signing certificate

This part takes days, not minutes, and costs money. Start it before you need it.

**What it is for.** Without a signature, when a coordinator downloads your installer Windows shows a blue box saying *"Windows protected your PC — Microsoft Defender SmartScreen prevented an unrecognised app from starting"*. Most people stop there, and they are right to. For software that asks a school to trust it with children's passwords, that warning is fatal to adoption.

**What to buy.** An **OV (Organisation Validation) code-signing certificate**, roughly USD 200–400 per year, from any of the usual authorities (DigiCert, Sectigo, SSL.com and others). **EV** costs about double and additionally skips the reputation-building period described below; budget OV unless the extra cost is easy.

**What to expect:**

- The authority verifies the organisation exists. Expect to supply registration documents, and expect a phone call. For a school this may need someone in administration.
- Modern code-signing certificates are usually issued on a **hardware token** (a USB device) or in a cloud signing service — the private key cannot be exported. That is inconvenient by design and it is protecting you.
- **Even with a valid OV certificate, SmartScreen may still warn for the first few weeks** until enough installations build reputation. This is normal, it resolves on its own, and it is worth telling the first two or three schools to expect it.

**Follow your certificate authority's instructions to install it.** They differ enough between providers that a generic walkthrough here would mislead you.

**Verify it is installed:**

```powershell
Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert
```

Your certificate should be listed with a subject naming your organisation.

> **While you are waiting for the certificate**, everything else in this document works. You can build unsigned installers and test them on your own machines. Just do not send one to another school — an unsigned installer teaches coordinators to click past exactly the warning that protects them.

---

## Part 7 — First build

Now check the whole chain works.

**1. Get the source:**

```powershell
mkdir C:\build
cd C:\build
git clone <repository-url> delima
cd delima
```

**2. Restore and compile:**

```powershell
dotnet restore
dotnet build -c Release
```

The first run downloads packages and takes a few minutes. You want `Build succeeded` at the end.

**3. Publish one program** to confirm the self-contained single-file path works — this is the step most likely to reveal a problem:

```powershell
dotnet publish src\Delima.Provision\Delima.Provision.csproj `
  -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true `
  -o publish\Provision
```

Then look at what came out:

```powershell
Get-ChildItem publish\Provision
```

**You should see essentially one file**, `Delima.Provision.exe`, tens of megabytes in size. If you see dozens of `.dll` files beside it, single-file publishing is not configured correctly — see `Build_And_Release.md` §3.

**4. Run it:**

```powershell
.\publish\Provision\Delima.Provision.exe --help
```

If it prints its usage text, the toolchain works end to end.

**5. Test signing** (only once Part 6 is complete). Using the path you wrote down in Part 5:

```powershell
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
& $signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a `
  publish\Provision\Delima.Provision.exe

& $signtool verify /pa /v publish\Provision\Delima.Provision.exe
```

`Successfully verified` means you are done. **The `/tr` part is a timestamp and it is not optional** — without it your signature silently stops being valid the day the certificate expires, on every copy already distributed.

---

## Part 8 — Troubleshooting

**`dotnet is not recognized`**
The window predates the install, or the SDK did not install. Open a fresh PowerShell; if that fails, restart the PC; if that fails, reinstall the SDK and confirm you took the **SDK**, not the Runtime.

**`MSB1011: Specify which project or solution file to use`**
You pointed a command at a folder containing more than one project. Give the full path to the specific `.csproj`, exactly as in the commands above. This bit this project during the injection test too.

**`error CS2015: ... is a binary file instead of a text file`**
Source files were copied from a Mac, which leaves hidden `._` companion files that the C# compiler tries to compile. Delete them:

```powershell
Get-ChildItem -Recurse -Force | Where-Object { $_.Name -like "._*" } | Remove-Item -Force
```

The `-Force` matters — some of these files are hidden and are otherwise invisible even to the listing.

**`cannot be loaded because running scripts is disabled`**
PowerShell's script policy. For the current window only:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
```

Scoped to the process, so it lapses when you close the window. That is deliberate — do not weaken it machine-wide.

**Publish succeeds but the app crashes on another PC**
Almost always because a framework-dependent build was published to a machine without .NET 10. Confirm `--self-contained true` was used. See `Build_And_Release.md` §3.

**Antivirus quarantines the freshly built exe**
Common with unsigned, self-contained, single-file executables — a large opaque binary with no signature is genuinely suspicious-looking. It is a reason to sign, not a reason to add a permanent antivirus exclusion.

**`signtool` is not found at the path in Part 5**
The version number folder differs between machines. Re-run the search command in Part 5 and use whatever it actually prints.

---

## Part 9 — One-page checklist

Before your first real release, all of these should be true:

- [ ] Windows 10 22H2 / Windows 11, administrator access, not a shared lab PC
- [ ] `dotnet --version` shows `10.x`
- [ ] `git --version` works, name and email configured
- [ ] `ISCC.exe` found; path noted
- [ ] `signtool.exe` found; full path written down
- [ ] Code-signing certificate installed; `Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert` lists it
- [ ] `dotnet build -c Release` succeeds on a clean clone
- [ ] A published single-file exe is genuinely one file, and runs
- [ ] `signtool verify /pa` reports success

Then go to `Build_And_Release.md` and follow it from §2.

---

## A last word

Nothing here should be run against a real school until **T0.1** — the written MOE/BSTP position on storing and replaying pupil passwords — has an answer (`PRD_Visual_SSO_v2.md` §2.1). Setting up the build machine, compiling, and testing on your own hardware are all fine now. Handing a signed installer to a second school is not, yet.
