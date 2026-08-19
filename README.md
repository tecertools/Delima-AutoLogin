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
| Status | Specified, not built | **In build.** T0.3 passed 17 Aug 2026; `Delima.Core` and the importer built and tested |

If only one ships, it should be Normal SSO. It is the smaller product with most of the benefit and none of the policy risk.

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
│   ├── T0.2_URL_Confirmation.md    ← the remaining blocker, ~1 hour in a browser
│   ├── AI_Build_Guide.md           ← start building here (novice walkthrough)
│   ├── Build_Prompts.md            Ready-to-paste prompts for an AI coding assistant
│   ├── Build_Machine_Setup.md      Set up a Windows PC to compile the release
│   ├── Build_And_Release.md        Publish flags, Inno Setup script, signing, checklist
│   └── mockups/
│       ├── DELIMa_Screen_Mockups.html         Launcher (pupil-facing), all 8 screens
│       └── DELIMa_Admin_Wizard_Mockups.html   Admin Wizard (ICT coordinator), all 7 steps
│
├── src/                            Application code
│   ├── Delima.Core/                Crypto, credential store, roster, display names — cross-platform
│   ├── Delima.Import/              Roster importer (APDM parsing, dry run) — cross-platform
│   └── Delima.Admin/               WPF wizard (not yet built)
│
├── tests/                          xUnit — Delima.Core.Tests, Delima.Import.Tests
│
├── InjectionSpike/                 T0.3 harness — PASSED, 17 Aug 2026 (see Visual_SSO/T0.3_Injection_Test_Protocol.md)
│
├── spike-results/                  T0.3 evidence — 4 CSVs from real lab hardware
│
└── PRD_Gap_Analysis.md             Review of Visual SSO v1; source of the v2 blockers
```

---

## Where things actually stand

**The cross-platform half of the application is built and tested.** `Delima.Core` (credential store, Argon2id/AES-256-GCM crypto, roster model, display names) and the roster importer compile and pass tests on macOS, Linux and Windows — arch §2 keeps them free of any Win32 or UI reference precisely so they can. `InjectionSpike/` remains as the T0.3 harness.

**Not built yet:** `Delima.Win32` (build step 8), the WPF Launcher and Admin UIs (steps 9–13), kiosk hardening (14) and the installer (15). All of those need Windows.

Three de-risking tasks were defined in `PRD_Gap_Analysis.md` §5:

- **T0.3** — run the injection spike, 50 runs, on representative lab hardware. **Passed, 17 August 2026.** `SendInput` scored 100/100 across two independent 50-run batches on real lab hardware; the `SendKeys` control failed exactly as predicted. Full results in `Visual_SSO/T0.3_Injection_Test_Protocol.md`.
- **T0.1** — written ToS/policy position from BSTP or state ICT on storing and replaying pupil passwords. **Not started, and no longer a blocker.** Requirement G-1 was consciously relaxed: the project publishes without it and places the responsibility on each downloading school instead, via the statement specified in PRD §8.7. Still worth pursuing — see below.
- **T0.2** — confirm the live `d3.delima.edu.my` SSO entry URL and that `login_hint` is honoured. **Partly answered, Aug 2026.** DELIMa signs in via Google OAuth 2.0 using its own Cloud project, which confirms arch §4.5's assumption and removes the cost that made the `login_hint` route look expensive. Still open: whether any pre-fill route works, and the password-screen window title that arch §4.2 verifies against. About an hour with one account you control — `Visual_SSO/T0.2_URL_Confirmation.md`. Blocks build step 11.

**Nothing blocks starting.** T0.3 answered the question the programme was contingent on. T0.1 has been routed around deliberately (PRD §2.2), and T0.2 doesn't bite until step 11.

## Next step

**Start building** — `Visual_SSO/AI_Build_Guide.md` is the walkthrough, `Visual_SSO/Build_Prompts.md` the prompts. First target is `Delima.Core`: the credential store, crypto and tamper tests (arch §12 step 3), then the roster model (step 4). Neither is blocked, and both build and unit-test on macOS or Linux — arch §2 keeps `Delima.Core` free of any Win32 or UI reference precisely so it can. Windows is first needed at step 8.

**Do T0.2 in parallel.** It needs one real pupil account and an afternoon, and leaving it undone is the kind of thing that stalls step 11 for no reason.

**Keep pursuing T0.1 anyway.** It no longer gates anything, but an actual answer would be strictly better than a disclosure — the difference between telling schools they are responsible and telling them it is permitted. If it comes back negative, the release is withdrawn and schools are told (PRD §2.2).

Optionally, round out T0.3's adversarial test to the full 5/5 (currently 2/5, both clean — see `Visual_SSO/T0.3_Injection_Test_Protocol.md`, "Actual results"), and take a quick look at the one anomalous `sendkeys` run before the pilot phase, though neither blocks moving forward.

**On compiling a distributable `.exe`:** the procedure is fully specified in `Visual_SSO/Build_And_Release.md`, and the machine to run it on in `Visual_SSO/Build_Machine_Setup.md` — but it is the *last* of 15 build steps (arch §12) and the first fourteen produce code that does not exist yet.

**Distribution, licence and signing are now settled** (PRD §8.5): free public download, open source, and free OV-level signing from [SignPath Foundation](https://signpath.org/). Two consequences that shape the build:

- **Releases are built by GitHub Actions, not by hand.** SignPath signs only artefacts from a trusted build system whose configuration is under source control. The convenient side effect is that **no Windows machine is needed to cut a release** — only to test one.
- **Ship one unsigned release first, then apply.** The Foundation requires a project already be released in the form to be signed, and it reviews applications. Software that stores passwords and injects keystrokes deserves a plain-spoken application; expect questions.

---

## Language

All pupil-facing text is Bahasa Melayu. Specification documents are in English. The words *SSO*, *portal*, *autentikasi*, *sesi* and *log masuk tunggal* never appear in the interface — see `Normal_SSO/PRD_Normal_SSO.md` §8.

## Licence

**GPL-3.0** — see `LICENSE`, and `Visual_SSO/PRD_Visual_SSO_v2.md` §8.5 for the reasoning. Briefly: the software is given away free, so there is no revenue to protect; an open licence is what makes free code signing available; and copyleft means a fork that weakens the credential store or drops the picture-password requirement has to publish its source, which keeps the difference visible.

Still outstanding before the first public release: the T0.1 responsibility statement (PRD §8.7) on the release page, the installer licence page, and `Delima.Admin` first run.
