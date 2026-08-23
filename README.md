# DELIMa Sign-In Tools

Two related products that solve the same problem for Malaysian primary schools: pupils aged 7–9 cannot type a 26-character MOE email address (`m-XXXXXXXX@moe-dl.edu.my`), and a computer-lab lesson loses 15–20 minutes to it.

They differ in one decision — **whether the software handles the pupil's password** — and that decision changes everything downstream.

| | **Normal SSO** | **Visual SSO** |
| :--- | :--- | :--- |
| Form | Web page, any device | Windows desktop app |
| Solves | The email only | Email **and** password |
| Stores passwords | **No** | Yes, encrypted, locally |
| Google ToS / MOE policy | Ordinary link, no gate | **Needs written sign-off** |
| Effort | Weeks | Months |
| Status | Specified; **primary mechanism broken** (see below) | **Built, not released.** All 15 build steps implemented; T0.2 and T0.3 passed |

If only one ships, it should be Normal SSO. It is the smaller product with most of the benefit and none of the policy risk.

> **⚠ That recommendation no longer holds.** Normal SSO's only mechanism is Google's `AccountChooser` URL, and T0.2 found it **returns HTTP 400 in live testing — with or without a `continue` parameter**, which points at the endpoint having been retired rather than merely restricted. Normal SSO has no fallback: the pre-filled hint *is* the product. **It needs re-planning around a documented flow before any further work** — and the obvious substitute is not simple either, since DELIMa drops `login_hint` from its own login URL. See `Normal_SSO/Technical_Architecture_Normal_SSO.md` §5.2.
>
> **Visual SSO is unaffected**, and is now the more viable of the two. It types the email itself, so it degrades to a two-step injection rather than losing its mechanism (arch §4.5, route C).

---

## Repository map

```
.
├── Normal_SSO/                     Web launcher — prefills email, stores no passwords
│   ├── PRD_Normal_SSO.md
│   ├── Technical_Architecture_Normal_SSO.md
│   ├── Stitch_UI_Build_Guide.md
│   └── stitch-wireframes/          Design system + 3 reference screens
│
├── Visual_SSO/                     Windows app — CURRENT SPEC (v2, multi-school)
│   ├── PRD_Visual_SSO_v2.md        ← start here
│   ├── Technical_Architecture_Visual_SSO.md
│   ├── T0.3_Tutorial_Step_By_Step.md     ← then do this (full walkthrough)
│   ├── T0.3_Injection_Test_Protocol.md   ← the short version, once you've done it
│   ├── T0.2_URL_Confirmation.md    Sign-in route — COMPLETE, route C selected
│   ├── T0.4_UIA_Verification.md    UIA field check — COMPLETE, passed, gate enabled
│   ├── E2E_First_Run.md            ← next: the first end-to-end run, one pupil
│   ├── AI_Build_Guide.md           ← start building here (novice walkthrough)
│   ├── Build_Prompts.md            Ready-to-paste prompts for an AI coding assistant
│   ├── Build_Machine_Setup.md      Set up a Windows PC to compile the release
│   ├── Build_And_Release.md        Publish flags, Inno Setup script, signing, checklist
│   └── mockups/
│       ├── DELIMa_Screen_Mockups.html         Launcher (pupil-facing), all 8 screens
│       └── DELIMa_Admin_Wizard_Mockups.html   Admin Wizard (ICT coordinator), all 7 steps
│
├── src/                            Application code
│   ├── Delima.Core/                Crypto, credential store, roster, audit — cross-platform
│   ├── Delima.Import/              Roster importer (APDM parsing, dry run) — cross-platform
│   ├── Delima.Win32/               P/Invoke, injection engine, DPAPI store, kiosk guard
│   ├── Delima.Launcher/            WPF, pupil-facing — all screens + Mod Guru
│   ├── Delima.Admin/               WPF, the seven-step setup wizard
│   └── Delima.Provision/           Per-lab-PC provisioning, runs from a pendrive
│
├── tests/                          xUnit — Core, Import, Win32, Launcher
│
├── installer/                      Inno Setup script + assets (guides, avatars, samples)
├── .github/workflows/release.yml   Tag-triggered build → sign → draft release
│
├── InjectionSpike/                 T0.3 harness — PASSED, 17 Aug 2026 (see Visual_SSO/T0.3_Injection_Test_Protocol.md)
│
├── spike-results/                  T0.3 + T0.4 evidence, from real lab hardware
│
└── PRD_Gap_Analysis.md             Review of Visual SSO v1; source of the v2 blockers
```

---

## Where things actually stand

**All fifteen build steps in arch §12 are implemented.** `Delima.Core`, `Delima.Import`, `Delima.Win32`, `Delima.Provision`, `Delima.Launcher` (all pupil screens, route C login orchestrator, Mod Guru), `Delima.Admin` (the seven-step wizard), the audit log, kiosk hardening, the Inno Setup installer and a tag-triggered GitHub Actions release pipeline.

**Nothing has been released, and several things stand between here and a first release:**

| # | Item | Where |
| :-- | :--- | :--- |
| **1** | **Cleanup after the E2E run** — `katalaluan.csv` holds a plaintext password; change the test account's password | `E2E_First_Run.md` Part 11 |
| **2** | Cancel on the consent screen quits the launcher — must return to `Pilih Kelas`, never exit in kiosk mode | Prompt 21 |
| **3** | **Measure Edge's window titles.** The lists ship empty and fail closed, so Edge does not work yet | `T0.4_UIA_Verification.md`, 20 runs |
| **4** | Outstanding measurements — Chrome consent title, G1 timing, cold start, audit-log verification | `E2E_First_Run.md` Part 9 |
| 5 | Tag a release, ship it unsigned, then apply to SignPath Foundation | `Build_And_Release.md` §4 |

**The whole chain has now run end to end on real lab hardware** — wizard → bundle → provision → sign-in → consent — with all six deliberate failure paths behaving correctly. The Alt-Tab test is the one that matters: T0.3 proved that stealing focus mid-injection leaks keystrokes, and §4.2's answer held against a live attempt.

**Chrome works. Edge does not yet** — Prompt 20 built the support but its title lists are deliberately empty until measured, so Edge fails closed rather than guessing.

Three de-risking tasks were defined in `PRD_Gap_Analysis.md` §5:

- **T0.3** — run the injection spike, 50 runs, on representative lab hardware. **Passed, 17 August 2026.** `SendInput` scored 100/100 across two independent 50-run batches on real lab hardware; the `SendKeys` control failed exactly as predicted. Full results in `Visual_SSO/T0.3_Injection_Test_Protocol.md`.
- **T0.1** — written ToS/policy position from BSTP or state ICT on storing and replaying pupil passwords. **Not started, and no longer a blocker.** Requirement G-1 was consciously relaxed: the project publishes without it and places the responsibility on each downloading school instead, via the statement specified in PRD §8.7. Still worth pursuing — see below.
- **T0.2** — confirm the live SSO entry URL and whether `login_hint` is honoured. **Passed, August 2026.** DELIMa signs in via Google OAuth 2.0 on its own Cloud project. No pre-fill route works — DELIMa drops `login_hint`, and `/AccountChooser` returns 400 — so **route C was selected**: the launcher types the email, then the password. Window titles distinguish the two pages — though T0.4 later found both captured strings were wrong, and that the password page has no fixed title at all. Full record in `Visual_SSO/T0.2_URL_Confirmation.md`.
- **T0.4** — verify Chrome reports `IsPassword` through UI Automation. **Passed, 21 August 2026.** 49/49 runs on lab hardware, zero false positives on any non-password page. The gate is enabled. It also caught two wrong window titles in the config, one of which — the password page containing the pupil's own name — meant no fixed string could ever have matched it. `Visual_SSO/T0.4_UIA_Verification.md`.

## Next step

**Cleanup first.** The E2E run left a plaintext password in `katalaluan.csv`, and the test account has been through software on its first ever run — change it. `E2E_First_Run.md` Part 11.

**Then Prompt 21**, which is a kiosk-mode defect rather than a cosmetic one: pressing Cancel on the consent screen quits the launcher, and in launch-at-logon mode that hands a seven-year-old the Windows desktop.

**Then, for a release:** tag it, ship one unsigned build, and apply to SignPath Foundation — the Foundation only signs projects already released in the form to be signed.

**Still worth pursuing, still not blocking: T0.1.** An actual written position would be strictly better than a disclosure — the difference between telling schools they are responsible and telling them it is permitted. If it comes back negative, the release is withdrawn and schools are told (PRD §2.2).

---

## Language

All pupil-facing text is Bahasa Melayu. Specification documents are in English. The words *SSO*, *portal*, *autentikasi*, *sesi* and *log masuk tunggal* never appear in the interface — see `Normal_SSO/PRD_Normal_SSO.md` §8.

## Licence

**GPL-3.0** — see `LICENSE`, and `Visual_SSO/PRD_Visual_SSO_v2.md` §8.5 for the reasoning. Briefly: the software is given away free, so there is no revenue to protect; an open licence is what makes free code signing available; and copyleft means a fork that weakens the credential store or drops the picture-password requirement has to publish its source, which keeps the difference visible.

The T0.1 responsibility statement (PRD §8.7) is in all three required placements: the release notes, the installer licence page, and `Delima.Admin`'s first run.
