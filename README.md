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
| Status | Specified, not built | Specified; **T0.3 passed 17 Aug 2026**, not built |

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
│   ├── Build_Machine_Setup.md      Set up a Windows PC to compile the release
│   ├── Build_And_Release.md        Publish flags, Inno Setup script, signing, checklist
│   └── mockups/
│       ├── DELIMa_Screen_Mockups.html         Launcher (pupil-facing), all 8 screens
│       └── DELIMa_Admin_Wizard_Mockups.html   Admin Wizard (ICT coordinator), all 7 steps
│
├── InjectionSpike/                 T0.3 harness — PASSED, 17 Aug 2026 (see Visual_SSO/T0.3_Injection_Test_Protocol.md)
│
├── PRD_Gap_Analysis.md             Review of Visual SSO v1; source of the v2 blockers
└── DELIMa_Visual_SSO_PRD_TechArch.md   v1, SUPERSEDED by Visual_SSO/PRD_Visual_SSO_v2.md
```

---

## Where things actually stand

**No application code exists yet.** The only C# in the repository is `InjectionSpike/` — the console test harness that measures whether password injection works. It has been compiled and run on real lab hardware.

Three de-risking tasks were defined in `PRD_Gap_Analysis.md` §5:

- **T0.3** — run the injection spike, 50 runs, on representative lab hardware. **Passed, 17 August 2026.** `SendInput` scored 100/100 across two independent 50-run batches on real lab hardware; the `SendKeys` control failed exactly as predicted. Full results in `Visual_SSO/T0.3_Injection_Test_Protocol.md`.
- **T0.1** — written ToS/policy position from BSTP or state ICT on storing and replaying pupil passwords. **Not started — now the active blocker.**
- **T0.2** — confirm the live `d3.delima.edu.my` SSO entry URL and that `login_hint` is honoured. **Not started.**

**T0.3 no longer blocks the programme.** The question of whether password injection works reliably on real hardware is answered. T0.1 is next, and it's slower, since it depends on someone outside this project.

## Next step

Start **T0.1** — a written policy position on storing and replaying pupil passwords, from BSTP or state ICT (`Visual_SSO/PRD_Visual_SSO_v2.md` §2.1–2.2). This doesn't block on code; it can run in parallel with anything else.

Optionally, round out T0.3's adversarial test to the full 5/5 (currently 2/5, both clean — see `Visual_SSO/T0.3_Injection_Test_Protocol.md`, "Actual results"), and take a quick look at the one anomalous `sendkeys` run before the pilot phase, though neither blocks moving forward.

**On compiling a distributable `.exe`:** the procedure is fully specified in `Visual_SSO/Build_And_Release.md`, and the machine to run it on in `Visual_SSO/Build_Machine_Setup.md` — but it is the *last* of 15 build steps (arch §12) and the first fourteen produce code that does not exist yet.

**Releases will be unsigned** (`Build_And_Release.md` §4, PRD §8.5). SmartScreen turns out to be a smaller obstacle than it looks: its warning depends on Mark-of-the-Web, which FAT32 and exFAT cannot store, and the pendrive is already the primary provisioning route — though an NTFS-formatted stick breaks that, so it has to be tested. The real cost is that a school cannot verify the installer it received is the one that was built, which makes **published SHA-256 checksums and hand delivery load-bearing rather than decorative**. Proportionate for a pilot; it does not scale past roughly five schools. Free OV signing via SignPath Foundation is available if the project ever goes open-source — the same undecided question as the licence.

---

## Language

All pupil-facing text is Bahasa Melayu. Specification documents are in English. The words *SSO*, *portal*, *autentikasi*, *sesi* and *log masuk tunggal* never appear in the interface — see `Normal_SSO/PRD_Normal_SSO.md` §8.

## Licence

Not yet decided. See `Visual_SSO/PRD_Visual_SSO_v2.md` §8.5 — this needs answering before the software is given to a second school.
