# Build & Release — DELIMa Smart Launcher v2

**Target:** .NET 10 (LTS), WPF, win-x64, self-contained single-file
**Packaging:** Inno Setup 6 → one signed `.exe`
**Audience of this document:** whoever runs the build. Not the ICT coordinator — see `Panduan_Pemasangan.pdf` for them.

This is **step 15 of 15** in `Technical_Architecture_Visual_SSO.md` §12. Nothing here can be executed until steps 1–14 produce the projects in arch §2. It is written now, ahead of the code, so that packaging is a checklist at the end rather than a week of surprises — single-file WPF, code signing and per-machine ACLs each have a failure mode that is much cheaper to design around than to discover.

For setting up the machine that runs these commands, see `Build_Machine_Setup.md`.

---

## 1. Where builds happen

**Releases are built by GitHub Actions, not by a person.** This is not a preference — SignPath Foundation signs only artefacts produced by a *trusted build system*, where the build is fully determined by configuration under source control and cannot be manually overridden (§4.2). A binary compiled on someone's laptop cannot be signed, however carefully it was made.

That constraint turns out to be convenient. **You do not need a Windows machine to produce a release.** GitHub's `windows-latest` runner is free for public repositories, and the repository must be public anyway for SignPath eligibility. The specs in this repository were written on a Mac; the releases can be built from one too, because the Mac never touches the compiler.

**A local Windows machine is still needed for development**, though not for releases and not immediately:

1. **Testing.** Arch §11 requires a clean Win10 1809 VM, real lab hardware for injection, and pupil-account ACL checks. CI cannot do any of that.
2. **Iterating.** Waiting on a CI round-trip to find a XAML typo is miserable.
3. **Reproducing faults** a school reports.

`Build_Machine_Setup.md` covers setting that machine up. Note it does **not** need `signtool`, a certificate, or the Windows SDK — signing happens in the pipeline.

**But not yet, and not for everything.** `Delima.Core` — the credential store, crypto, tamper tests, roster model and importer, which is build steps 3, 4 and 5 — has no UI and no Win32 and **builds and unit-tests on macOS or Linux**. Arch §2 enforces that boundary deliberately: *"`Delima.Core` must not reference `Delima.Win32`."* Windows becomes necessary at step 8 (`Delima.Win32`) and step 9 (the first WPF screens), which is a substantial way into the build order.

**A school lab PC is acceptable for development builds**, with one caveat. The earlier objection — that a code-signing private key must never sit on a shared machine — no longer applies, because signing happens in the pipeline and no key exists locally. What remains is that lab PCs are locked down in ways that break builds; the T0.3 spike hit "insufficient access to delete" on one and was resolved by copying the folder to the Desktop and building from there (`T0.3_Tutorial_Step_By_Step.md`, troubleshooting). Do not put the repository in a restricted location.

**On Apple Silicon**, a Windows 11 ARM VM (Parallels, UTM) is fine for writing and running WPF, since x64 is emulated — but the release target is `win-x64` and injection testing requires real lab hardware regardless, so treat the VM as an editor, not as a test environment.

**Releases are cut from a tag, never from a working tree.** The workflow triggers on `v*` tags (§4.4), which makes this structural rather than a matter of discipline — there is no way to publish an unreproducible build, because there is no manual publish path at all.

```bash
git tag v2.0.0
git push origin v2.0.0     # this is the entire release procedure
```

**Locally, WPF requires Windows.** `dotnet publish` for a WPF project fails on macOS and Linux regardless of `-r win-x64`, because the WPF targets aren't installed. This is why local testing needs a Windows box or VM even though releases don't.

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
$cfg = @("-c", "Release", "-r", "win-x64", "--self-contained", "true", "/p:PublishSingleFile=true")

dotnet publish src\Delima.Launcher\Delima.Launcher.csproj  @cfg -o publish\Launcher
dotnet publish src\Delima.Admin\Delima.Admin.csproj        @cfg -o publish\Admin
dotnet publish src\Delima.Provision\Delima.Provision.csproj @cfg -o publish\Provision
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

## 4. Signing — SignPath Foundation

**Decision: releases are signed, free, via [SignPath Foundation](https://signpath.org/), which provides OV-level code signing at no cost to open-source projects.** This follows from two other decisions: the project is distributed free to any school that wants it, and it is open-source under the licence recorded in PRD §8.5.

The private key never exists on any machine you control — it lives in SignPath's hardware security module, and artefacts are submitted to be signed rather than signed locally. For a one-person project that is strictly safer than a certificate on a laptop, which is the usual failure mode: the key gets copied to a second machine, then a backup, then it is on a drive in a drawer.

### 4.1 Why this matters more than it did

Free public download makes signing close to mandatory, for a reason that is easy to miss:

**Mark-of-the-Web applies to everything downloaded.** The *"Windows protected your PC"* dialog is triggered by a tag Windows attaches to files arriving from the internet. A pendrive formatted FAT32 cannot carry that tag, so hand-delivered installers avoid the warning — but a download always carries it. **Every install, at every school, on every release.**

**Unsigned reputation never accumulates.** SmartScreen tracks reputation for unsigned files by *file hash*. Every new release is a new hash and starts from zero, so the warning never stops appearing no matter how many schools install it. Signed, reputation attaches to the certificate and new releases inherit it — the warning fades after the first weeks and stays gone.

**The warning is a friction, not a wall** — a coordinator can click *More info* → *Run anyway*. But teaching ICT coordinators to click past that specific dialog, on software that writes children's passwords to disk, is training exactly the wrong reflex. Two exceptions where it *is* a hard block: schools with SmartScreen set to Block rather than Warn by group policy, and coordinators who are not local administrators.

### 4.2 What SignPath requires, and what that forces

Eligibility, from the [Foundation's conditions](https://signpath.org/terms.html):

| Requirement | Status for this project |
| :--- | :--- |
| OSI-approved licence, no commercial dual-licensing | Set by PRD §8.5 |
| Publicly available codebase | Repository must be made public |
| No proprietary or non-open-source component | Check fonts and any icon set before applying — see below |
| Actively maintained | True while the pilot runs |
| Already released in the form to be signed | **Ship one unsigned release first**, then apply |
| Functionality described on the download page | The release page must explain what the app does |
| Signing team = development team, owning the repo | True |

**Origin verification is the constraint with teeth.** SignPath verifies that a signed binary is an automated build of the source at the stated repository. In practice that means the build must run on a supported CI system — GitHub Actions, Azure DevOps, Jenkins — with build settings fully determined by a file under source control, no manual overrides in the CI job, and no reuse of cached artefacts from earlier unverified builds. Origin metadata comes from GitHub itself, so it cannot be forged by the build script. **This is why §1 moves releases to CI.**

**Two risks worth naming before applying:**

- **The Foundation reviews applications and rejects malware or potentially unwanted programs.** This application stores passwords and injects keystrokes into a browser — behaviourally, that is not far from what a credential stealer does, and a reviewer may well pause on it. **Describe the purpose plainly in the application**: a school-managed kiosk launcher for seven-year-olds who cannot type a 26-character MOE address, deployed by the school on its own machines, with the credential store and threat model documented in `Technical_Architecture_Visual_SSO.md` §3. Do not discover this objection after building the whole pipeline around it — apply early, expect questions.
- **"No proprietary component" includes assets.** Nunito and Baloo 2 are OFL-1.1 (arch §6.5) and fine. Audit the avatar artwork and any icons for licences that are free-to-use but not open-source before applying, because a single non-OSS asset disqualifies the project.

### 4.3 Fallbacks, if the application is refused

- **[Certum open-source certificate](https://shop.certum.eu/code-signing.html)** — from €25/year, but the first purchase needs a smartcard and reader (~€69 plus shipping, ~€29 to renew). Also requires open source, but no CI or origin verification, so it works with local builds. Roughly RM 350 the first year.
- **Ordinary OV certificate** — DigiCert, Sectigo, SSL.com and similar, roughly RM 900/year, no open-source requirement.
- **Ship unsigned** — see §4.5 for what that would then require.

> **Azure Artifact Signing** ($9.99/month) is widely recommended and is **not currently available to individual developers outside the US and Canada**. Unlikely to apply here.

**Never self-sign as a substitute.** It requires teaching an ICT coordinator to install an unknown publisher certificate on lab machines — a far more dangerous habit than the one it fixes, and worse than shipping unsigned.

### 4.4 The release pipeline

One workflow, triggered by pushing a `v*` tag, running on `windows-latest`:

1. **Check out** the tagged commit.
2. **Set up .NET 10**, restore, build `-c Release`.
3. **Run the unit tests** (arch §11 crypto, display-name and importer suites). Fail the release here, not later.
4. **Publish** the three executables with the §3 flags.
5. **Build the installer** with Inno Setup (`iscc`, §5), version passed from the tag.
6. **Upload** the installer as a workflow artefact.
7. **Submit to SignPath** for signing via their GitHub Action, and wait for the signed artefact to return.
8. **Generate SHA-256 checksums** of the *signed* installer (§6) — order matters, since signing changes the file.
9. **Create a draft GitHub Release**, attaching installer, checksums, and the two BM guides.

**Leave it as a draft.** A human should read the release notes and confirm the T0.1 responsibility statement (PRD §8.5) is present on the release page before it goes public. Everything upstream of that is automated; the decision to publish is not.

**Three properties this pipeline must preserve**, because they are what SignPath's origin verification is checking:

- Every build setting lives in the repository. Nothing configured only in the GitHub UI.
- No step reuses a cached build output from an earlier run.
- The workflow does not accept inputs that change what gets compiled.

### 4.5 If signing is ever unavailable

Should the project need to ship unsigned — application refused, and no budget for a fallback certificate — these stop being optional:

- **The SHA-256 checksum becomes the only integrity control**, so publish it somewhere other than beside the installer. A hash file next to the file it describes proves nothing.
- **`Panduan_Pemasangan.pdf` must teach verification** in BM, with the exact command: `Get-FileHash .\DELIMaLauncher-Setup-2.0.0.exe -Algorithm SHA256`.
- **The install guide must show a screenshot of the amber "Unknown Publisher" UAC prompt** so coordinators expect it rather than abandon the install.
- **Expect antivirus false positives.** A large unsigned self-contained single-file exe is hard for heuristics to distinguish from packed malware. Report them to the vendor; never tell schools to add an exclusion folder on a machine holding credentials.

Signing is worth real effort to avoid landing here.

## 5. The installer

`installer\DelimaLauncher.iss`, built with:

```powershell
iscc /DMyAppVersion=2.0.0 installer\DelimaLauncher.iss
```

The version comes from the command line so it cannot disagree with §2.

> **Pin the Inno Setup version in CI. Do not install "latest".**
>
> Two current lines exist: **6.7.3** (May 2026) and **7.1.0** (August 2026). Inno Setup 7 is backward compatible with 6 and its `.iss` scripts, adds a 64-bit compiler edition and extended-length path support — **neither of which this project needs**, since the installer targets `%ProgramFiles%\DELIMa Launcher` and a 32-bit installer stub is normal. Either line works. Both can be installed side by side.
>
> What matters is that the release pipeline pins one:
>
> ```yaml
> - run: winget install --id JRSoftware.InnoSetup -e -v 6.7.3 --silent
> #  or: winget install --id JRSoftware.InnoSetup.7 -e -v 7.1.0 --silent
> ```
>
> An unpinned installer toolchain means a release you cannot reproduce, and SignPath's origin verification (§4.2) requires the build to be fully determined by the repository. A compiler version that changes underneath you is exactly what that rule exists to prevent.
>
> **Upgrade deliberately, between releases, and re-run §7's checklist afterwards** — never mid-release-cycle.

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

**Hash the *signed* installer, not the one that went to SignPath.** Signing rewrites the file, so a checksum taken before signing describes something no school will ever download. This is step 8 of the pipeline (§4.4) for that reason.

The signature is the primary integrity control; the checksum is a second, independent one that does not depend on Windows trusting a certificate chain. Publish it on the release page alongside the download — with a signature present, co-location is acceptable, since the two controls fail independently.

**Release contents** (PRD §8.6):

```
DELIMaLauncher-Setup-2.0.0.exe          signed installer (SignPath, §4)
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
| 1 | Release was produced by the tagged CI run, not a local build | SignPath will not sign anything else (§4.2) |
| 2 | `signtool verify /pa` passes on the downloaded installer | Confirms the signature survived the release pipeline |
| 3 | Installer runs on a **clean** Win10 1809 VM with no .NET installed | The entire reason for self-contained |
| 4 | Launcher opens, class + name screens render, fonts embedded correctly | Single-file resource loading fails here first |
| 5 | Admin opens, imports `contoh_roster.csv` end to end | Import is the feature schools adopt on |
| 6 | `Provision.exe` runs from a pendrive on a second PC | Proves single-file actually is one file |
| 7 | Upgrade 2.0.0 → 2.0.1 over the top; `credentials.dat` survives | `AppId` correctness |
| 8 | Uninstall; confirm the store is removed only on confirmation | Audit log may be required evidence |
| 9 | A pupil-account user cannot read `%ProgramData%\DELIMa Launcher` | The ACL in §5 |
| 10 | Injection still passes on lab hardware, ≥ 50 runs | Never regress T0.3 |
| 11 | **Download the installer from the release page** and install from there | The real path a school takes; catches Mark-of-the-Web and signature problems together (§4.1) |
| 12 | Scan the installer with the antivirus the target schools actually run | Self-contained single-file exes draw false positives even signed (§4.5) |
| 13 | Release page carries the T0.1 responsibility statement | PRD §8.5 — the duty-shift only works if it is actually there |

**Checks 3 and 10 need real hardware or a real VM.** Both have already bitten this project once — the .NET runtime assumption and the injection behaviour are exactly where a developer machine lies to you (arch §11: *never on a developer machine, never over RDP*).

---

## 8. Failures you should expect, and what they mean

| Symptom | Cause | Fix |
| :--- | :--- | :--- |
| `MissingMethodException` / XAML fails at runtime, fine in Debug | Trimming enabled | `PublishTrimmed=false`, §3 |
| `.dll` files appear beside the exe after single-file publish | Missing self-extract flag | `IncludeNativeLibrariesForSelfExtract=true` |
| Publish fails on macOS/Linux with WPF targets missing | Wrong OS | Local testing needs Windows; releases build on CI, §1 |
| SignPath refuses to sign the artefact | Build did not come from the trusted build system, or a setting was overridden in the CI job | Origin verification, §4.2 |
| SmartScreen still warns after signing | Certificate reputation not yet established | Normal for the first weeks; it accrues to the certificate and does not reset per release, §4.1 |
| UAC prompt is amber, says "Unknown Publisher" | The installer was not signed, or signing ran before packaging | Sign the payload exes first, then the installer, §4.4 |
| Antivirus quarantines the installer at a school | Self-contained single-file exe, heuristics | Report as a false positive to the vendor; do **not** tell schools to add exclusions, §4.5 |
| Upgrade creates a second entry in Programs & Features | `AppId` changed | Restore the original GUID, §5 |
| App starts, no theme, default fonts | Embedded resource pack URI wrong under single-file | Arch §6.2/§6.5 |
| `MSB1011: more than one project` | Building a folder, not a `.csproj` | Give the full `.csproj` path, as in §3 |

---

## 9. The T0.1 obligation on every release

**T0.1 — a written MOE/BSTP position on storing and replaying pupil passwords — remains unanswered** (PRD §2.1, README). The project has chosen to publish anyway, and to place the policy responsibility explicitly on each downloading school (PRD §8.5).

That choice only works if the statement is actually in front of people. **Three placements are required on every release, and check 13 in §7 exists to enforce the first:**

1. **The release page** — above the download link, not below it.
2. **The installer's licence page** — which the coordinator must scroll and accept.
3. **`Delima.Admin` first run** — before Step 1 of the wizard, requiring acknowledgement.

Publishing without these is not the decision that was made; it is a different and worse one.

Be clear-eyed about what the duty-shift achieves: it makes the responsibility explicit and documented, which is worth real something. It does not make the underlying question answered, and a school that clicks through has still not consulted anyone. **Getting T0.1 answered remains the better outcome** and is still worth pursuing in parallel — it is the difference between schools being told they are responsible and schools being told it is permitted.
