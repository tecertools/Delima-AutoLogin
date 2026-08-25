<!-- Improved compatibility of back to top link: See: https://github.com/othneildrew/Best-README-Template/pull/73 -->
<a id="readme-top"></a>

<!-- PROJECT SHIELDS -->
[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![project_license][license-shield]][license-url]
[![Latest Release][release-shield]][release-url]
[![Build & Test Status][build-shield]][build-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/tecertools/Delima-AutoLogin">
    <img src="icon.png" alt="DELIMa Smart Launcher Logo" width="100" height="100">
  </a>

  <h2 align="center">DELIMa Smart Launcher</h2>

  <p align="center">
    A zero-cloud, privacy-first Windows desktop sign-in assistant designed for Malaysian primary school computer labs (Tahap 1, Ages 7–9).
    <br />
    <a href="https://github.com/tecertools/Delima-AutoLogin/releases/latest"><strong>📥 Download Latest Installer (.exe) »</strong></a>
    ·
    <a href="docs/"><strong>Explore the docs »</strong></a>
    <br />
    <br />
    <a href="Visual_SSO/mockups/DELIMa_Screen_Mockups.html">View Launcher Mockup</a>
    ·
    <a href="Visual_SSO/mockups/DELIMa_Admin_Wizard_Mockups.html">View Admin Wizard Mockup</a>
    ·
    <a href="https://github.com/tecertools/Delima-AutoLogin/issues/new?labels=bug&template=bug-report---.md">Report Bug</a>
    ·
    <a href="https://github.com/tecertools/Delima-AutoLogin/issues/new?labels=enhancement&template=feature-request---.md">Request Feature</a>
  </p>
</div>

<!-- TABLE OF CONTENTS -->
<details>
  <summary>Table of Contents</summary>
  <ol>
    <li>
      <a href="#about-the-project">About The Project</a>
      <ul>
        <li><a href="#the-problem">The Problem</a></li>
        <li><a href="#the-solution">The Solution</a></li>
        <li><a href="#key-features">Key Features</a></li>
        <li><a href="#built-with">Built With</a></li>
      </ul>
    </li>
    <li><a href="#repository-structure">Repository Structure</a></li>
    <li>
      <a href="#getting-started">Getting Started</a>
      <ul>
        <li><a href="#download--quick-install">Download & Quick Install</a></li>
        <li><a href="#prerequisites-for-development">Prerequisites for Development</a></li>
        <li><a href="#building-from-source">Building from Source</a></li>
        <li><a href="#running-tests">Running Tests</a></li>
        <li><a href="#publishing-executables">Publishing Executables</a></li>
        <li><a href="#building-the-installer">Building the Installer</a></li>
      </ul>
    </li>
    <li>
      <a href="#usage--workflow">Usage & Workflow</a>
      <ul>
        <li><a href="#1-setup-and-roster-export-admin-pc">1. Setup & Roster Export (Admin PC)</a></li>
        <li><a href="#2-lab-workstation-provisioning-lab-pcs">2. Lab Workstation Provisioning (Lab PCs)</a></li>
        <li><a href="#3-pupil-sign-in-session">3. Pupil Sign-In Session</a></li>
        <li><a href="#4-teacher-override-mod-guru">4. Teacher Override (Mod Guru)</a></li>
      </ul>
    </li>
    <li><a href="#security-privacy--responsibility">Security, Privacy & Responsibility</a></li>
    <li><a href="#roadmap">Roadmap</a></li>
    <li><a href="#contributing">Contributing</a></li>
    <li><a href="#license">License</a></li>
    <li><a href="#contact">Contact</a></li>
    <li><a href="#acknowledgments">Acknowledgments</a></li>
  </ol>
</details>

<!-- ABOUT THE PROJECT -->
## About The Project

### The Problem

In Malaysian primary schools (Tahap 1 / Year 1 to Year 3, ages 7–9), pupils are assigned a 26-character Ministry of Education (MOE) Google Workspace email address formatted as:

```text
m-XXXXXXXX@moe-dl.edu.my
```

For young children who cannot easily locate special characters like `@` or `-` on a keyboard, manually typing their email and password takes significant time. In a typical 30-to-40-minute computer lab period with 30–40 pupils, **teachers often lose 15–20 minutes simply getting everyone signed into DELIMa**.

### The Solution

**DELIMa Smart Launcher** eliminates keyboard frustration by replacing manual email and password entry with a visual, touch-friendly interface:

1. **Pilih Kelas**: The pupil taps their class name.
2. **Pilih Nama & Avatar**: The pupil finds their name accompanied by a colourful animal avatar.
3. **Masukkan Kata Laluan Gambar**: The pupil inputs a simple 3-symbol visual picture-PIN (e.g., 🐱 Cat + 🐬 Dolphin + ⭐ Star).
4. **Auto Sign-In**: The launcher opens Google Chrome or Microsoft Edge, waits for the DELIMa Google sign-in page, and automatically fills the credentials using low-level Win32 input events and UI Automation verification.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Key Features

* 🎨 **Pupil-First Visual UX (Bahasa Melayu)**: High-contrast, large-target WPF interface tailored for young learners, with 20 animal avatars and intuitive picture PINs.
* 🔒 **Zero-Cloud & Privacy Preserving**: Zero external servers, zero telemetry, and zero credentials transmitted across the internet. Everything remains on the local school computers.
* 🛡️ **Hardware-Level DPAPI & AES-256-GCM Encryption**: Credentials stored on lab PCs are protected using Windows Data Protection API (machine-scope DPAPI) and AES-256-GCM. Roster files copied to unauthorized computers cannot be decrypted.
* ⚡ **Accurate Win32 Replay Engine (`SendInput`)**: Hardware-level keystroke injection tested at 100% accuracy on real school lab hardware (scoring 100/100 across test batches), handling complex reserved symbols effortlessly.
* 👁️ **UI Automation (`IsPassword`) Verification**: Proactively verifies that the active browser element is an authentic password field before typing credentials, preventing accidental exposure in plaintext fields.
* 🛑 **Topmost Focus-Stealing Guard**: A transparent barrier overlays the screen during the brief injection window to ensure background keystroke leakage cannot occur if focus shifts.
* 🧙 **7-Step Admin Wizard (`Delima.Admin`)**: A guided GUI for ICT Coordinators (Guru Penyelaras ICT) to import CSV rosters, match passwords, configure school crest and colors, and export encrypted USB provisioning bundles.
* 🚀 **Rapid Lab Provisioning (`Delima.Provision`)**: Deploy the encrypted bundle across 20–40 lab PCs from a single USB drive in under 90 minutes.
* 🏫 **Kiosk Lockdown & Mod Guru**: Optional full-screen kiosk guard to prevent young pupils from roaming the Windows desktop, accompanied by an instant 4-digit PIN override for teachers (`Mod Guru`).

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Built With

* [![.NET 10][DotNet-shield]][DotNet-url]
* [![C#][CSharp-shield]][CSharp-url]
* [![WPF][WPF-shield]][WPF-url]
* [![Win32 API][Win32-shield]][Win32-url]
* [![CommunityToolkit MVVM][CommunityToolkit-shield]][CommunityToolkit-url]
* [![Inno Setup][InnoSetup-shield]][InnoSetup-url]
* [![xUnit][xUnit-shield]][xUnit-url]

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- REPOSITORY STRUCTURE -->
## Repository Structure

```
Delima-AutoLogin/
├── .github/workflows/         # CI/CD: Release pipeline with SignPath signing
├── docs/                      # Documentation, branding assets, avatar SVGs, AppLocker guides
├── installer/                 # Inno Setup compilation script & bundled assets
├── src/
│   ├── Delima.Admin/          # 7-Step setup wizard for School ICT Coordinators (WPF)
│   ├── Delima.Core/           # Domain models, AES-GCM crypto, DPAPI store, audit log
│   ├── Delima.Import/         # Student roster CSV parser, validator & dry-run engine
│   ├── Delima.Launcher/       # Student-facing kiosk launcher with visual PIN & Mod Guru (WPF)
│   ├── Delima.Provision/      # Pendrive-based lab workstation provisioning tool
│   └── Delima.Win32/          # Native P/Invoke, SendInput injection & UI Automation verifier
├── tests/                     # Comprehensive xUnit test suite (410+ tests)
│   ├── Delima.Admin.Tests/
│   ├── Delima.Core.Tests/
│   ├── Delima.Import.Tests/
│   ├── Delima.Launcher.Tests/
│   └── Delima.Win32.Tests/
├── Visual_SSO/                # PRD specifications, injection spike logs, UI mockups
├── Normal_SSO/                # Web-based SSO prototype & investigation docs
└── DelimaLauncher.sln         # Main Visual Studio / .NET Solution
```

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- GETTING STARTED -->
## Getting Started

### Download & Quick Install

If you are a School ICT Coordinator or Teacher looking to install DELIMa Smart Launcher:

1. **[📥 Download Latest Installer from GitHub Releases](https://github.com/tecertools/Delima-AutoLogin/releases/latest)**
2. Run the signed installer package (`DELIMaLauncher-Setup-2.2.0.exe`) on your Admin PC and Lab PCs.
3. Follow the interactive setup guide in [docs/](docs/) or open the [Online Setup Guide](https://tecertools.github.io/Delima-AutoLogin/).

---

### Prerequisites for Development

* **Operating System**: Windows 10 or Windows 11 (x64)
* **.NET SDK**: [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* **Inno Setup** (Optional, for building the installer): [Inno Setup 6.x](https://jrsoftware.org/isinfo.php)
* **Browser**: Google Chrome or Microsoft Edge installed on target machines

### Building from Source

1. Clone the repository:
   ```sh
   git clone https://github.com/tecertools/Delima-AutoLogin.git
   cd Delima-AutoLogin
   ```

2. Restore dependencies and build the solution:
   ```sh
   dotnet build -c Release
   ```

### Running Tests

Run the complete xUnit test suite:
```sh
dotnet test -c Release --no-build
```

### Publishing Executables

Publish the three self-contained single-file executables for 64-bit Windows:

```powershell
$cfg = "-c Release -r win-x64 --self-contained true /p:PublishSingleFile=true"

dotnet publish src/Delima.Launcher/Delima.Launcher.csproj $cfg -o publish/Launcher
dotnet publish src/Delima.Admin/Delima.Admin.csproj $cfg -o publish/Admin
dotnet publish src/Delima.Provision/Delima.Provision.csproj $cfg -o publish/Provision
```

### Building the Installer

Compile the Inno Setup installer package:

```powershell
& "C:\Program Files (x86)\Inno Setup 6\iscc.exe" /DMyAppVersion=2.0.0 installer/DelimaLauncher.iss
```

The compiled installer will be output to `dist/DELIMaLauncher-Setup-2.2.0.exe`.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- USAGE EXAMPLES -->
## Usage & Workflow

### 1. Setup and Roster Export (Admin PC)

The ICT Coordinator runs `Delima.Admin.exe` to configure the school environment:

1. **Maklumat Sekolah**: Enter the school name, MOE school code (e.g. `BBA8230`), and select branding colors/crest.
2. **Import Roster**: Select the student roster CSV export (`contoh_roster.csv`).
3. **Import Kata Laluan**: Provide the class DELIMa password list (`contoh_kata_laluan.csv`).
4. **Tetapan Mod Guru**: Set the 4-digit teacher override master PIN.
5. **Eksport Bundle**: Export the encrypted package (`delima-bundle.bin` + `key.dat`) directly to a USB pendrive.

### 2. Lab Workstation Provisioning (Lab PCs)

On each lab PC:

1. Plug in the USB pendrive containing the exported bundle.
2. Run `Delima.Provision.exe`.
3. The tool imports the class roster and locks the credentials to that specific workstation using machine-scope DPAPI.
4. Remove the pendrive.

### 3. Pupil Sign-In Session

1. The pupil opens **DELIMa Smart Launcher** (or it starts automatically upon login).
2. The pupil selects their class (e.g. `1 Amanah`), finds their avatar/name, and enters their 3-symbol picture password.
3. The launcher automatically opens the browser, navigates to the DELIMa portal, injects the credentials, and signs the pupil in.

### 4. Teacher Override (Mod Guru)

If a pupil forgets their picture password or encounters an issue:
* Press <kbd>F12</kbd> or click the teacher icon in the top corner.
* Enter the 4-digit teacher master PIN to access the manual bypass or reset visual PINs on the fly.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- SECURITY & PRIVACY -->
## Security, Privacy & Responsibility

DELIMa Smart Launcher was designed specifically to prioritize pupil privacy and security:

* **Zero-Cloud Design**: No student information or credentials ever leave the school premises. No backend servers, databases, or analytics tracking exist.
* **Hardware-Bound Protection**: Stored rosters are protected via Windows DPAPI bound to the specific PC. If a file is copied elsewhere, it cannot be decrypted.
* **Transient Memory Handling**: Credentials in memory are stored using `SecureString` structures and zeroed immediately after injection.
* **School Responsibility Statement**: In accordance with Malaysian PDPA 2010 guidelines, each school retains full administrative authority and ownership of their stored pupil credentials.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- ROADMAP -->
## Roadmap

- [x] Win32 `SendInput` hardware injection spike passed (100/100 runs)
- [x] UI Automation (`IsPassword`) field validation passed (49/49 runs)
- [x] Topmost focus-stealing overlay barrier implementation
- [x] 7-Step Admin Setup Wizard (`Delima.Admin`) with CSV roster reconciliation
- [x] Multi-school visual theming and 20 animal avatars
- [x] Inno Setup single-installer script & CI/CD pipeline
- [ ] Measure Microsoft Edge window title strings in live lab environments
- [ ] Multi-monitor full-screen kiosk handling
- [ ] Engage BSTP / State ICT regarding official policy alignment (T0.1)
- [ ] Re-evaluate web-based Normal SSO when upstream OAuth prefill parameters become available

See the [open issues](https://github.com/tecertools/Delima-AutoLogin/issues) for proposed features and known issues.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CONTRIBUTING -->
## Contributing

Contributions make the open-source educational software community an extraordinary place to learn, inspire, and create. Any contributions you make are **greatly appreciated**.

If you have a suggestion that would improve this project, please fork the repository and create a pull request. You can also open an issue with the tag `"enhancement"`.

1. Fork the Project
2. Create your Feature Branch (`git checkout -b feature/AmazingFeature`)
3. Commit your Changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the Branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

<p align="right">(<a href="#readme-top">back to top</a>)</p>

### Top Contributors

<a href="https://github.com/tecertools/Delima-AutoLogin/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=tecertools/Delima-AutoLogin" alt="Contributors" />
</a>

<!-- LICENSE -->
## License

Distributed under the **GNU General Public License v3.0 (GPL-3.0)**. See [`LICENSE`](LICENSE) for more information.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- CONTACT -->
## Contact

**tecertools Digital Solutions**  
Project Link: [https://github.com/tecertools/Delima-AutoLogin](https://github.com/tecertools/Delima-AutoLogin)

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- ACKNOWLEDGMENTS -->
## Acknowledgments

* [SK Seksyen 24 ICT Team](https://skseksyen24.edu.my/) — For real-world primary school computer lab testing, telemetry, and invaluable teacher feedback.
* [Inno Setup](https://jrsoftware.org/isinfo.php) — The legendary Windows installer creator by Jordan Russell and Martijn Laan.
* [SignPath Foundation](https://about.signpath.io/) — For supporting open-source software with free code signing certificates.
* [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — Fast, modern MVVM toolkit for .NET.
* [Best-README-Template](https://github.com/othneildrew/Best-README-Template) — For the stellar GitHub README layout and design.

<p align="right">(<a href="#readme-top">back to top</a>)</p>

<!-- MARKDOWN LINKS & IMAGES -->
[contributors-shield]: https://img.shields.io/github/contributors/tecertools/Delima-AutoLogin.svg?style=for-the-badge
[contributors-url]: https://github.com/tecertools/Delima-AutoLogin/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/tecertools/Delima-AutoLogin.svg?style=for-the-badge
[forks-url]: https://github.com/tecertools/Delima-AutoLogin/network/members
[stars-shield]: https://img.shields.io/github/stars/tecertools/Delima-AutoLogin.svg?style=for-the-badge
[stars-url]: https://github.com/tecertools/Delima-AutoLogin/stargazers
[issues-shield]: https://img.shields.io/github/issues/tecertools/Delima-AutoLogin.svg?style=for-the-badge
[issues-url]: https://github.com/tecertools/Delima-AutoLogin/issues
[license-shield]: https://img.shields.io/github/license/tecertools/Delima-AutoLogin.svg?style=for-the-badge
[license-url]: https://github.com/tecertools/Delima-AutoLogin/blob/main/LICENSE
[release-shield]: https://img.shields.io/github/v/release/tecertools/Delima-AutoLogin.svg?style=for-the-badge&logo=github&color=blue
[release-url]: https://github.com/tecertools/Delima-AutoLogin/releases/latest
[build-shield]: https://img.shields.io/github/actions/workflow/status/tecertools/Delima-AutoLogin/release.yml?branch=main&style=for-the-badge&logo=dotnet&label=Build%20%26%20Tests
[build-url]: https://github.com/tecertools/Delima-AutoLogin/actions/workflows/release.yml

[DotNet-shield]: https://img.shields.io/badge/.NET_10-512BD4?style=for-the-badge&logo=dotnet&logoColor=white
[DotNet-url]: https://dotnet.microsoft.com/
[CSharp-shield]: https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=csharp&logoColor=white
[CSharp-url]: https://learn.microsoft.com/en-us/dotnet/csharp/
[WPF-shield]: https://img.shields.io/badge/WPF-0078D4?style=for-the-badge&logo=windows&logoColor=white
[WPF-url]: https://learn.microsoft.com/en-us/dotnet/desktop/wpf/
[Win32-shield]: https://img.shields.io/badge/Win32_API-00599E?style=for-the-badge&logo=windows&logoColor=white
[Win32-url]: https://learn.microsoft.com/en-us/windows/win32/
[CommunityToolkit-shield]: https://img.shields.io/badge/CommunityToolkit.Mvvm-00589C?style=for-the-badge&logo=nuget&logoColor=white
[CommunityToolkit-url]: https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/
[InnoSetup-shield]: https://img.shields.io/badge/Inno_Setup-2980B9?style=for-the-badge&logo=windows&logoColor=white
[InnoSetup-url]: https://jrsoftware.org/isinfo.php
[xUnit-shield]: https://img.shields.io/badge/xUnit.net-512BD4?style=for-the-badge&logo=xunit&logoColor=white
[xUnit-url]: https://xunit.net/
