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

## 4. Signing

**Sign before packaging, then sign the package.** Order matters: Inno Setup embeds the executables as payload, so signing them afterwards is impossible without rebuilding the installer.

```powershell
$ts = "http://timestamp.digicert.com"      # or your CA's timestamp service
$sign = "signtool sign /fd SHA256 /td SHA256 /tr $ts /a"

# 1. the three executables
& cmd /c "$sign publish\Launcher\Delima.Launcher.exe"
& cmd /c "$sign publish\Admin\Delima.Admin.exe"
& cmd /c "$sign publish\Provision\Delima.Provision.exe"

# 2. build the installer (§5), then sign it
& cmd /c "$sign dist\DELIMaLauncher-Setup-2.0.0.exe"
```

**`/tr` (timestamp) is not optional.** Without a timestamp, every copy of your software stops validating the day the certificate expires — including installers already sitting on a coordinator's desk. With one, the signature remains valid indefinitely because it proves the code was signed while the certificate was live. Forgetting this is the single most common code-signing mistake and it surfaces a year later, at which point every school has to re-download.

Verify before releasing:

```powershell
signtool verify /pa /v dist\DELIMaLauncher-Setup-2.0.0.exe
```

**On the certificate itself** (PRD §8.5): OV is the budget line, roughly USD 200–400/year. Unsigned, SmartScreen shows *"Windows protected your PC"* to every coordinator who downloads it — for software asking a school to entrust it with children's passwords, that is fatal to adoption, and correctly so. OV still requires building SmartScreen reputation over the first weeks; EV bypasses that at roughly double the cost. Budget OV, expect first-month friction, and do not treat the warning as a cosmetic issue to fix later.

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

Publish the checksum somewhere other than beside the download if you can — the value of a hash sitting next to the file it describes is limited, since anyone who can replace one can replace both. A tagged release page, or the school's own site, is enough.

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
| 2 | `signtool verify /pa` passes on all four binaries | Signing order mistakes are silent |
| 3 | Installer runs on a **clean** Win10 1809 VM with no .NET installed | The entire reason for self-contained |
| 4 | Launcher opens, class + name screens render, fonts embedded correctly | Single-file resource loading fails here first |
| 5 | Admin opens, imports `contoh_roster.csv` end to end | Import is the feature schools adopt on |
| 6 | `Provision.exe` runs from a pendrive on a second PC | Proves single-file actually is one file |
| 7 | Upgrade 2.0.0 → 2.0.1 over the top; `credentials.dat` survives | `AppId` correctness |
| 8 | Uninstall; confirm the store is removed only on confirmation | Audit log may be required evidence |
| 9 | A pupil-account user cannot read `%ProgramData%\DELIMa Launcher` | The ACL in §5 |
| 10 | Injection still passes on lab hardware, ≥ 50 runs | Never regress T0.3 |

**Checks 3 and 10 need real hardware or a real VM.** Both have already bitten this project once — the .NET runtime assumption and the injection behaviour are exactly where a developer machine lies to you (arch §11: *never on a developer machine, never over RDP*).

---

## 8. Failures you should expect, and what they mean

| Symptom | Cause | Fix |
| :--- | :--- | :--- |
| `MissingMethodException` / XAML fails at runtime, fine in Debug | Trimming enabled | `PublishTrimmed=false`, §3 |
| `.dll` files appear beside the exe after single-file publish | Missing self-extract flag | `IncludeNativeLibrariesForSelfExtract=true` |
| Publish fails on macOS/Linux with WPF targets missing | Wrong OS | Build on Windows, §1 |
| SmartScreen warns despite a valid signature | Reputation not yet built | Expected with OV for the first weeks, §4 |
| Signature invalid after a year | No timestamp | `/tr`, §4 — requires re-release to fix |
| Upgrade creates a second entry in Programs & Features | `AppId` changed | Restore the original GUID, §5 |
| App starts, no theme, default fonts | Embedded resource pack URI wrong under single-file | Arch §6.2/§6.5 |
| `MSB1011: more than one project` | Building a folder, not a `.csproj` | Give the full `.csproj` path, as in §3 |

---

## 9. What this document does not authorise

Building an installer is not permission to distribute one. **T0.1 — the written MOE/BSTP position on storing and replaying pupil passwords — remains open** (PRD §2.1, README). A signed installer sent to a second school before that answer exists moves a policy question the project has not resolved onto schools who don't know they're being asked it.

Build it, test it, keep it internal. Ship it when T0.1 says you may.
