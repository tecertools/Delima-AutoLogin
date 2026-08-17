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
| Status | Specified, not built | Specified, not built |

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
│   └── T0.3_Injection_Test_Protocol.md   ← the short version, once you've done it
│
├── InjectionSpike/                 T0.3 harness — decides whether Visual SSO is viable
│                                   WRITTEN BUT NEVER COMPILED OR RUN
│
├── PRD_Gap_Analysis.md             Review of Visual SSO v1; source of the v2 blockers
└── DELIMa_Visual_SSO_PRD_TechArch.md   v1, SUPERSEDED by Visual_SSO/PRD_Visual_SSO_v2.md
```

---

## Where things actually stand

**No application code exists.** The only C# in the repository is `InjectionSpike/` — a console test harness that measures whether password injection works. Its own README records that it has never been compiled.

Three de-risking tasks were defined in `PRD_Gap_Analysis.md` §5 and none have been completed:

- **T0.1** — written ToS/policy position from BSTP or state ICT on storing and replaying pupil passwords
- **T0.2** — confirm the live `d3.delima.edu.my` SSO entry URL and that `login_hint` is honoured
- **T0.3** — run the injection spike, 50 runs, on representative lab hardware

**T0.3 decides whether Visual SSO is viable at all.** It costs about a day. Everything in `Visual_SSO/` is contingent on its result.

## Next step

Run T0.3. **Follow `Visual_SSO/T0.3_Injection_Test_Protocol.md`** — it covers the control run, cold-vs-warm timing, and the adversarial focus-steal test, none of which are obvious from the commands alone.

```powershell
cd InjectionSpike
dotnet build -c Release                                    # never been compiled
dotnet run -c Release -- fidelity --method sendkeys  --runs 50   # control, run first
dotnet run -c Release -- fidelity --method sendinput --runs 50
dotnet run -c Release -- timing --runs 50
```

On a lab PC, not a developer machine, and not over Remote Desktop.

---

## Language

All pupil-facing text is Bahasa Melayu. Specification documents are in English. The words *SSO*, *portal*, *autentikasi*, *sesi* and *log masuk tunggal* never appear in the interface — see `Normal_SSO/PRD_Normal_SSO.md` §8.

## Licence

Not yet decided. See `Visual_SSO/PRD_Visual_SSO_v2.md` §8.5 — this needs answering before the software is given to a second school.
