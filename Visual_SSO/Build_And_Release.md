# Build & Release — DELIMa Smart Launcher v2

**Target:** .NET 10 (LTS), WPF, win-x64, self-contained single-file
**Packaging:** Inno Setup 6 → one signed `.exe`
**Audience of this document:** whoever runs the build. Not the ICT coordinator — see `Panduan_Pemasangan.pdf` for them.

This is **step 15 of 15** in `Technical_Architecture_Visual_SSO.md` §12. Nothing here can be executed until steps 1–14 produce the projects in arch §2. It is written now, ahead of the code, so that packaging is a checklist at the end rather than a week of surprises — single-file WPF, code signing and per-machine ACLs each have a failure mode that is much cheaper to design around than to discover.

For setting up the machine that runs these commands, see `Build_Machine_Setup.md`.

---

## 1. The build machine

**Windows. There is no way around this.** WPF is a Windows-only UI stack; `dotnet publish` for a WPF project fails on macOS and Linux regardless of `-r win-x64`, because the WPF targets themselves aren't installed. Inno Setup and `signtool` are also Windows-only. The specs in this repository were written on a Mac; the build cannot be.

**Requirements:** Windows 10 22H2 or Windows 11, .NET 10 SDK, Inno Setup 6, Windows SDK (for `signtool`), Git.

**Do not use a school lab PC as the build machine.** Two reasons, and the second is the serious one:

1. Lab PCs are locked down in ways that break builds — the T0.3 spike already hit "insufficient access to delete" on one (`T0.3_Tutorial_Step_By_Step.md`, troubleshooting).
2. **The code-signing private key must never sit on a shared machine.** Whoever controls that key can sign anything as you, including malware that schools will then trust because it carries your name. Use a personal or dedicated machine, and prefer a hardware token if the CA offers one (EV certificates are typically issued on one; OV increasingly is too).

**Build from a clean checkout of a tagged commit.** Not from a working tree with uncommitted edits. A release you cannot reproduce from a tag is a release you cannot debug six months later when one school reports a fault.

```powershell
git clone <repo> C:\build\delima
cd C:\build\delima
git checkout v2.0.0
git status --short     # must print nothing
```

---

## 2. Version stamping — one source of truth

Version numbers appear in at least five places: two executables, the installer, the checksums file, and the store's schema compatibility check. They must not be maintained separately.

**`Directory.Build.props` at the solution root** sets the version once, and every project inherits it:

```xml
<Project>
  <PropertyGroup>
    <Version>2.0.0</Version>
    <Company>SK Seksyen 24</Company>
    <Product>DELIMa Smart Launcher</Product>
    <Copyright>© 2026</Copyright>
    <NeutralLanguage>ms-MY</NeutralLanguage>
  </PropertyGroup>
</Project>
```

The Inno Setup script reads the same value from the command line (§5), so the installer cannot drift from the binaries it contains.

**Scheme: `MAJOR.MINOR.PATCH`.**

- **MAJOR** — the store format changed incompatibly. A major bump means every school re-provisions. Avoid; the schema version in the store (arch §3.2) exists precisely so this stays rare.
- **MINOR** — new features, store format compatible.
- **PATCH** — fixes only.

The store carries its own schema version independent of the app version (arch §3.2). A newer Launcher migrates an older store forward and refuses a future-versioned one. **Do not couple the two numbers** — a patch release must not invalidate a school's `credentials.dat`.

---

## 3. Publish — the three executables

Three programs ship (arch §2): `Delima.Launcher`, `Delima.Admin`, `Delima.Provision`. `Delima.Core` and `Delima.Win32` are libraries and are not published separately; they are compiled into the three above.

```powershell
$cfg = "-c Release -r win-x64 --self-contained true /p:PublishSingleFile=true"

dotnet publish src\Delima.Launcher\Delima.Launcher.csproj  $cfg -o publish\Launcher
dotnet publish src\Delima.Admin\Delima.Admin.csproj        $cfg -o publish\Admin
dotnet publish src\Delima.Provision\Delima.Provision.csproj $cfg -o publish\Provision
```

Set the rest in each `.csproj`, not on the command line, so a hand-typed build cannot differ from a scripted one:

```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishTrimmed>false</PublishTrimmed>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <SatelliteResourceLanguages>en</SatelliteResourceLanguages>
  <DebugType>embedded</DebugType>
</PropertyGroup>
```

**Four of those settings are load-bearing, and one is a trap:**

- **`PublishTrimmed=false` — this is the trap.** Trimming is the obvious way to shrink an 80 MB binary and **WPF does not support it**. Trimming a WPF app produces something that builds, publishes, and then throws `MissingMethodException` at runtime when XAML reflection reaches a member the trimmer removed — often on a screen you didn't test, in a school you can't reach. Leave it off. Accept the size.
- **`IncludeNativeLibrariesForSelfExtract=true`** — without it, native dependencies land beside the exe instead of inside it, and "single file" isn't. The whole point is that `Provision.exe` can be copied to a pendrive on its own.
- **`PublishReadyToRun=true`** — precompiles to native code. Adds ~20 MB and cuts cold start noticeably on the spinning-disk lab PCs this targets. On a machine where a seven-year-old is watching a blank screen, startup latency is a feature.
- **`EnableCompressionInSingleFile=true`** — roughly halves the on-disk size at a small startup cost. Worth it; the installer download is the thing coordinators actually complain about.
- **`SatelliteResourceLanguages=en`** — drops ~30 localisation folders of framework resources the app never uses. The app's own Malay/English strings are its own resources and are unaffected.

**Expect 80–110 MB per executable.** That is correct and intended (PRD §8.2): lab PCs have no modern .NET runtime, and asking a coordinator to install one on 40 machines first loses him immediately.

**A framework-dependent build is a secondary download** for schools with managed images that already carry .NET 10:

```powershell
dotnet publish src\Delima.Launcher\Delima.Launcher.csproj -c Release -r win-x64 `
  --self-contained false -o publish-fx\Launcher
```

Ship it as a separate archive, not as an installer option. A coordinator who picks the wrong one gets a "the app won't start" failure with no useful error, and that support call costs more than the bandwidth saved.

---

## 4. Signing — decided against, and what that costs

**Decision: releases are unsigned.** A code-signing certificate is roughly RM 900/year and the free routes all require open-sourcing the project, which is a separate decision the project has not made (PRD §8.5). This section records what that costs and what has to compensate, because the honest answer is "something", not "nothing".

### 4.1 What it does *not* cost

**SmartScreen usually does not appear on the pendrive route — but this depends on how the pendrive is formatted.** The *"Windows protected your PC"* dialog is triggered by **Mark-of-the-Web**, a tag Windows attaches to files arriving from the internet. That tag is stored in an NTFS alternate data stream, so:

- **FAT32 / exFAT pendrive → tag is lost, no SmartScreen.** These are the default formats on virtually every USB stick sold. Copying a downloaded file onto one strips the tag.
- **NTFS pendrive → tag survives, SmartScreen still warns.** `T0.3_Tutorial_Step_By_Step.md` Part 10 records exactly this happening with the spike binary.

**So this must be verified, not assumed** — hence check 11 in §7. Format the distribution pendrive as **FAT32 or exFAT** and confirm on a test machine that no dialog appears. Do not rely on it having worked once on a different stick.

This matters because **the pendrive is already the primary provisioning route** (PRD §6 Step 7, arch §10): the distribution model chosen for other reasons happens to sidestep the loudest consequence of not signing, provided the medium is right.

Two cases where the warning definitely does appear:

- **Network share.** Since a 2024 File Explorer change, Windows applies Mark-of-the-Web to files copied from shares it does not consider trusted. The `Rangkaian` provisioning route can therefore trip SmartScreen where a FAT32 USB does not.
- **Any web download.** A website, Drive link, or email attachment all apply Mark-of-the-Web. **Do not distribute unsigned builds this way.**

Note that SmartScreen is a warning the coordinator can click past — *More info* → *Run anyway*, as the T0.3 tutorial documents. It is a friction and adoption problem, not a hard block. The tamper-evidence gap in §4.2 is the more serious cost.

### 4.2 What it does cost

**The UAC prompt says "Unknown Publisher".** The installer requests administrator rights (§5, `PrivilegesRequired=admin`). Signed, Windows shows the blue elevation dialog naming the publisher. Unsigned, it shows the amber one reading *Publisher: Unknown*. This appears on every install regardless of how the file arrived — pendrive included. It cannot be suppressed and should not be; the install guide must show a screenshot of it so coordinators expect it rather than abandon the install.

**There is no tamper-evidence.** This is the real cost, and it is not cosmetic. A signature proves the installer a school received is the one that was built. Without it, a coordinator handed a pendrive has no way to distinguish the genuine installer from a modified one — and this is software that writes children's passwords to disk. A malicious build would be an excellent way to collect them.

**Antivirus false positives get more likely.** A large, unsigned, self-contained single-file executable is genuinely hard for heuristics to distinguish from packed malware. Expect occasional quarantines, and expect them to differ between schools' antivirus products. Do not respond by telling schools to add a permanent exclusion folder — that is worse than the problem.

### 4.3 What compensates

**SHA-256 checksums stop being a nicety and become the integrity control** (§6). With no signature, the checksum is the *only* way a school can verify what it received. That imposes two obligations:

1. **Publish the checksum through a different channel than the installer.** A hash file sitting on the same pendrive as the installer proves nothing — anyone who can alter one can alter the other. Send it separately: a message to the coordinator, a page on the school site, a printed line on the handover sheet.
2. **`Panduan_Pemasangan.pdf` must teach how to check it**, in Bahasa Melayu, with the exact command:

   ```powershell
   Get-FileHash .\DELIMaLauncher-Setup-2.0.0.exe -Algorithm SHA256
   ```

   Be realistic: most coordinators will not do this. It is still worth documenting, because the ones handling something sensitive on a school's behalf are exactly the ones who might.

**Deliver by hand, in person, wherever possible.** Given the tamper-evidence gap, physical handover by someone the school knows is doing real work — it substitutes a human trust relationship for the cryptographic one that isn't there.

### 4.4 When to revisit

This decision is proportionate at pilot scale — a handful of schools, installers handed over personally by someone they know. **It does not scale.** Revisit when any of these becomes true:

- distribution passes roughly five schools, or reaches any school the author does not deal with directly;
- the installer needs to be downloadable rather than hand-delivered;
- T0.1 comes back permitting broad deployment.

At that point the cheapest route is **[SignPath Foundation](https://signpath.org/about)** — free OV-level signing for open-source projects, private key held in their HSM. It requires a public repository under a recognised open-source licence, which is the same undecided question as PRD §8.5. One decision would settle both.

### 4.5 If a certificate is obtained later

Sign the executables **before** packaging, then sign the installer — Inno Setup embeds the executables as payload, so signing them afterwards means rebuilding.

```powershell
$ts = "http://timestamp.digicert.com"      # or your CA's timestamp service
$sign = "signtool sign /fd SHA256 /td SHA256 /tr $ts /a"

& cmd /c "$sign publish\Launcher\Delima.Launcher.exe"
& cmd /c "$sign publish\Admin\Delima.Admin.exe"
& cmd /c "$sign publish\Provision\Delima.Provision.exe"
& cmd /c "$sign dist\DELIMaLauncher-Setup-2.0.0.exe"    # after §5

signtool verify /pa /v dist\DELIMaLauncher-Setup-2.0.0.exe
```

**`/tr` (timestamp) is not optional.** Without it, every distributed copy stops validating the day the certificate expires — including installers already on coordinators' desks. With it, the signature stays valid because it proves the code was signed while the certificate was live. This is the most common code-signing mistake and it surfaces a year later, when every school has to re-download.

**Do not self-sign and ask schools to trust the certificate.** It is tempting and it is worse than shipping unsigned: it trains an ICT coordinator to install an unknown root or publisher certificate on lab machines, which is a habit with much larger consequences than this application. Unsigned is the honest state; a self-signed certificate only dresses it up.

---

## 5. The installer

`installer\DelimaLauncher.iss`, built with:

```powershell
iscc /DMyAppVersion=2.0.0 installer\DelimaLauncher.iss
```

The version comes from the command line so it cannot disagree with §2.

```pascal
#define MyAppName "DELIMa Smart Launcher"
#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-dev"
#endif

[Setup]
AppId={{PUT-A-FIXED-GUID-HERE-NEVER-CHANGE-IT}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=SK Seksyen 24
DefaultDirName={autopf}\DELIMa Launcher
DefaultGroupName=DELIMa Launcher
OutputDir=..\dist
OutputBaseFilename=DELIMaLauncher-Setup-{#MyAppVersion}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=assets\LESEN.rtf
MinVersion=10.0.17763          ; Windows 10 1809, per PRD §8

[Languages]
Name: "ms"; MessagesFile: "compiler:Default.isl"

[Types]
Name: "lab";   Description: "PC Makmal"
Name: "admin"; Description: "Alat Pentadbir"
Name: "full";  Description: "Kedua-duanya"

[Components]
Name: "lab";   Description: "Aplikasi murid + Provision"; Types: lab full;   Flags: fixed
Name: "admin"; Description: "Alat Pentadbir (satu PC sahaja)"; Types: admin full

[Files]
Source: "..\publish\Launcher\Delima.Launcher.exe";   DestDir: "{app}"; Components: lab;   Flags: ignoreversion
Source: "..\publish\Provision\Delima.Provision.exe"; DestDir: "{app}"; Components: lab;   Flags: ignoreversion
Source: "..\publish\Admin\Delima.Admin.exe";         DestDir: "{app}"; Components: admin; Flags: ignoreversion
Source: "assets\avatars\*";                          DestDir: "{app}\avatars"; Components: lab; Flags: recursesubdirs
Source: "assets\Panduan_Pemasangan.pdf";             DestDir: "{app}\docs"; Flags: isreadme
Source: "assets\Panduan_Import.pdf";                 DestDir: "{app}\docs"; Components: admin
Source: "assets\contoh_roster.csv";                  DestDir: "{app}\docs"; Components: admin
Source: "assets\contoh_kata_laluan.csv";             DestDir: "{app}\docs"; Components: admin

[Dirs]
; Per-machine store. Interactive users must not be able to read it (arch §3.5).
Name: "{commonappdata}\DELIMa Launcher"; Permissions: everyone-none admins-full

[Icons]
Name: "{group}\DELIMa";           Filename: "{app}\Delima.Launcher.exe"; Components: lab
Name: "{group}\Alat Pentadbir";   Filename: "{app}\Delima.Admin.exe";    Components: admin

[Tasks]
Name: "startup";      Description: "Mulakan semasa log masuk (mod kiosk)"; Components: lab; Flags: unchecked
Name: "chromepolicy"; Description: "Guna dasar Chrome sekolah — melumpuhkan pengurus kata laluan, DevTools dan mod inkognito pada SELURUH PC ini"; Components: lab; Flags: unchecked

[Registry]
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "PasswordManagerEnabled"; ValueData: 0; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "DeveloperToolsAvailability"; ValueData: 2; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "IncognitoModeAvailability"; ValueData: 1; Tasks: chromepolicy; Flags: uninsdeletevalue
Root: HKLM; Subkey: "SOFTWARE\Policies\Google\Chrome"; ValueType: dword; \
  ValueName: "BrowserSignin"; ValueData: 0; Tasks: chromepolicy; Flags: uninsdeletevalue
```

**Notes on the choices above, since several encode decisions made elsewhere:**

- **`AppId` is a fixed GUID, generated once, never changed.** It is how Windows knows 2.0.1 is an upgrade of 2.0.0 rather than a second product. Change it and every school ends up with two installations and one orphaned credential store. Generate it once, paste it in, and add a comment saying never to touch it.
- **`{commonappdata}`, never `{userappdata}`.** The store is per-machine (arch §3.3) — a per-user path would give every pupil profile its own broken copy.
- **`Permissions: everyone-none admins-full`** is the ACL from arch §3.5 expressed in Inno's syntax. Be honest about what it buys: it stops a pupil browsing to the file in Explorer. It does not stop someone who can run arbitrary code in a lab session. That is what AppLocker is for, and AppLocker is not in this script — see below.
- **The Chrome policy task is `unchecked` by default and its description says it affects the whole PC.** It writes to `HKLM` and changes Chrome for every user on the machine, including the teacher's own browsing. A checkbox that quietly does that is not acceptable; the wording is the mitigation.
- **`isreadme` on the install guide** means a coordinator finishing setup is offered the PDF. G3 — 90 minutes, unaided (PRD §4) — is not reachable without it.
- **AppLocker is deliberately absent.** Restricting which programs the pupil account may run is what actually protects the store (PRD §8.3, arch §9), but it depends on the school's Windows edition and existing group policy. A checkbox that silently fails on Windows Home would be worse than no checkbox, because the coordinator would believe he was protected. It ships as a documented snippet and a **required** line on the lab checklist.

---

## 6. Checksums and the release manifest

```powershell
Get-FileHash dist\DELIMaLauncher-Setup-2.0.0.exe -Algorithm SHA256 |
  ForEach-Object { "$($_.Hash.ToLower())  $(Split-Path $_.Path -Leaf)" } |
  Out-File dist\DELIMaLauncher-2.0.0-checksums.txt -Encoding ascii
```

**Because releases are unsigned (§4), this checksum is the only integrity control the product has.** Publish it through a channel separate from the installer — a message to the coordinator, the school's own site, a printed line on the handover sheet. A hash file sitting on the same pendrive as the installer proves nothing, since anyone who can replace one can replace both.

**Release contents** (PRD §8.6):

```
DELIMaLauncher-Setup-2.0.0.exe          signed installer
DELIMaLauncher-2.0.0-checksums.txt      SHA-256
DELIMaLauncher-2.0.0-fx-dependent.zip   secondary, needs .NET 10 installed
Panduan_Pemasangan.pdf                  BM install guide, screenshots, 10 pages
Panduan_Import.pdf                      BM import guide with worked examples
contoh_roster.csv / contoh_kata_laluan.csv
```

The two PDFs are deliverables, not documentation debt.

---

## 7. Pre-release verification

Do not skip this because the build succeeded. A build succeeding proves the compiler was happy, not that the product works — and the self-contained single-file WPF path in particular fails at runtime, not at build time.

| # | Check | Why |
| :-- | :--- | :--- |
| 1 | `git status --short` empty, on a tag | Reproducibility |
| 2 | Checksum generated and recorded **outside** the release folder | Unsigned, this is the only integrity control (§4.3) |
| 3 | Installer runs on a **clean** Win10 1809 VM with no .NET installed | The entire reason for self-contained |
| 4 | Launcher opens, class + name screens render, fonts embedded correctly | Single-file resource loading fails here first |
| 5 | Admin opens, imports `contoh_roster.csv` end to end | Import is the feature schools adopt on |
| 6 | `Provision.exe` runs from a pendrive on a second PC | Proves single-file actually is one file |
| 7 | Upgrade 2.0.0 → 2.0.1 over the top; `credentials.dat` survives | `AppId` correctness |
| 8 | Uninstall; confirm the store is removed only on confirmation | Audit log may be required evidence |
| 9 | A pupil-account user cannot read `%ProgramData%\DELIMa Launcher` | The ACL in §5 |
| 10 | Injection still passes on lab hardware, ≥ 50 runs | Never regress T0.3 |
| 11 | Install **from a FAT32/exFAT pendrive**, not the build folder — confirm no SmartScreen dialog | The unsigned path depends on this, and NTFS sticks break it (§4.1) |
| 12 | Scan the installer with the antivirus the target schools actually run | Unsigned single-file exes draw false positives (§4.2) |

**Checks 3 and 10 need real hardware or a real VM.** Both have already bitten this project once — the .NET runtime assumption and the injection behaviour are exactly where a developer machine lies to you (arch §11: *never on a developer machine, never over RDP*).

---

## 8. Failures you should expect, and what they mean

| Symptom | Cause | Fix |
| :--- | :--- | :--- |
| `MissingMethodException` / XAML fails at runtime, fine in Debug | Trimming enabled | `PublishTrimmed=false`, §3 |
| `.dll` files appear beside the exe after single-file publish | Missing self-extract flag | `IncludeNativeLibrariesForSelfExtract=true` |
| Publish fails on macOS/Linux with WPF targets missing | Wrong OS | Build on Windows, §1 |
| SmartScreen warns on an unsigned build | It arrived with Mark-of-the-Web — downloaded, emailed, or copied from an untrusted share | Deliver by pendrive instead, §4.1 |
| UAC prompt is amber, says "Unknown Publisher" | No signature | Expected and unavoidable; show it in the install guide, §4.2 |
| Antivirus quarantines the installer at a school | Unsigned self-contained single-file exe | Submit as a false positive to the vendor; do **not** tell schools to add exclusions, §4.2 |
| Upgrade creates a second entry in Programs & Features | `AppId` changed | Restore the original GUID, §5 |
| App starts, no theme, default fonts | Embedded resource pack URI wrong under single-file | Arch §6.2/§6.5 |
| `MSB1011: more than one project` | Building a folder, not a `.csproj` | Give the full `.csproj` path, as in §3 |

---

## 9. What this document does not authorise

Building an installer is not permission to distribute one. **T0.1 — the written MOE/BSTP position on storing and replaying pupil passwords — remains open** (PRD §2.1, README). A signed installer sent to a second school before that answer exists moves a policy question the project has not resolved onto schools who don't know they're being asked it.

Build it, test it, keep it internal. Ship it when T0.1 says you may.
