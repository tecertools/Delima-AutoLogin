# PRD — DELIMa Normal SSO (Web Launcher)

**Product:** DELIMa Quick Sign-In — a web page that takes a pupil from "I don't know my email" to a Google password prompt with their address already filled in.
**Version:** 1.0 (Draft)
**Date:** 6 August 2026
**Owner:** King
**Related:** `../Visual_SSO/PRD_Visual_SSO_v2.md` (Visual SSO desktop app — separate product, password injection)

---

## 1. Summary

Roughly 2,000 primary pupils lose 15–20 minutes of every computer-lab lesson typing an eight-digit MOE email (`m-XXXXXXXX@moe-dl.edu.my`) they cannot remember and cannot spell. The password they *can* remember — it's short, and their teacher drilled it.

This product removes only the hard half. A pupil opens one bookmarked page, taps their class, taps their name, taps DELIMa or Google Classroom, and lands on Google's real sign-in screen with the email already populated. They type their password. Nothing else changes.

**Explicit non-goal:** this product does not store, transmit, or type passwords. That is what makes it deployable in weeks instead of quarters, and what keeps it inside both Google's Terms of Service and MOE's acceptable-use posture.

---

## 2. Problem

| Observation | Consequence |
| :--- | :--- |
| Pupils in Tahap 1 (ages 7–9) type 4–8 characters per minute *(est.)* | A 26-character email takes 3–6 minutes with errors |
| The local part is a random 8-digit ID with no mnemonic value | Pupils cannot recall it; they carry paper slips that get lost |
| `@moe-dl.edu.my` contains a hyphen and two dots | The single most common failure point in observed lessons |
| Lab PCs are shared; previous pupil's Google session persists | Pupil signs in as a classmate, or sees "Choose an account" and freezes |
| Teacher becomes a full-time typing assistant | One teacher, 30 pupils, no instruction happens for 20 minutes |

Password entry, by contrast, is not the bottleneck. School-assigned passwords are short and rehearsed, and pupils get visual feedback (dots appear) that they are making progress.

---

## 3. Goals & Success Metrics

### Goals

1. **G1** — Cut time-to-signed-in from ~15 minutes (class-wide) to under 3 minutes.
2. **G2** — Eliminate teacher intervention for email entry entirely.
3. **G3** — Ship with zero stored credentials, so no security exception or MOE data-processing agreement is needed beyond the roster itself.
4. **G4** — Work on any device with a browser: lab PC, Chromebook, teacher's phone, home PC.

### Success Metrics

**Every baseline below is currently an estimate, not a measurement.** They came from describing the problem, not from timing a lesson. That is a real weakness: the product's headline claim — 15 minutes down to 3 — is unverified, and if the true baseline is 6 minutes the business case changes shape. **Phase −1 of the rollout exists to replace these figures with observed ones before any code ships.**

| Metric | Baseline | Target | How measured |
| :--- | :--- | :--- | :--- |
| Median class-wide time to all-pupils-signed-in | 15 min *(est.)* | ≤ 3 min | Teacher stopwatch, 5 lessons pre/post |
| Pupils requiring teacher help to sign in | ~70% *(est.)* | ≤ 10% | Teacher tally sheet |
| Wrong-account sign-ins per lesson | 3–5 *(est.)* | 0 | Teacher observation |
| Taps from page load to password field | n/a | 3 typical, 5 cold | Manual count on a lab PC |
| Visits where both dropdowns are already correct | n/a | ≥ 90% | Teacher observation during pilot |
| Pupils who scroll on the name screen | n/a | **0** at 1366 × 768 | On-site check at every lab resolution |
| Wrong-pupil card taps (duplicate names) | n/a | 0 | Teacher tally during pilot |
| Page load on lab hardware (cold, school Wi-Fi) | n/a | ≤ 2 s | Lighthouse + on-site test |

### Non-Goals

- Storing, encrypting, or auto-typing pupil passwords.
- Password reset or account provisioning (remains a school/MOE admin function).
- Replacing DELIMa or Classroom — this is a doorway, not a destination.
- Attendance tracking, usage analytics per pupil, or any pupil-level behavioural data.

---

## 4. Users

**Aisyah, 8, Tahap 1.** Reads slowly. Recognises her own name and her avatar reliably. Cannot type an `@` without help. Needs: big targets, her name in a large font, a picture to anchor on, no English jargon.

**Cikgu Farah, teacher.** Has 40 minutes of lab time and 30 pupils. Needs the whole class signed in fast, and needs to fix a wrong roster entry herself without filing a ticket.

**En. Zul, ICT coordinator.** Maintains the roster from student roster exports. Needs a CSV in, a working page out, and no server to babysit. Will be the one asked "is this safe?" by the headmaster.

---

## 5. User Flow

```
┌───────────────┐   ┌───────────────┐   ┌──────────────────┐   ┌─────────────────┐
│ Screen 1      │──►│ Screen 2      │──►│ Screen 3         │──►│ Google sign-in  │
│ Landing       │   │ Cari Nama     │   │ Pergi ke mana?   │   │ email prefilled │
│ Tahun▾ Kelas▾ │   │ 30-44 kad     │   │ DELIMa 3.0 /     │   │ pupil types the │
│ + info panel  │   │ 1 skrin sahaja│   │ Google Classroom │   │ password only   │
└───────────────┘   └───────────────┘   └──────────────────┘   └─────────────────┘
                                                                        │
                                                                        ▼
                                                            DELIMa portal / Classroom
```

**Common case — 3 taps.** Both dropdowns remember their last value per device, so a lab PC serving the same class all week opens with the class already selected: **Teruskan** → name → destination.

**Cold case — 5 taps.** Tahun → Kelas → Teruskan → name → destination. Still no typing until the password field.

Up to 54 classes (6 tahun × 8 or 9) are narrowed by the two dropdowns; selecting a tahun repopulates the Kelas dropdown in place, with no navigation until **Teruskan**.

### Screen 1 — Landing Page

A header, two columns, and a footer. The school has **6 tahun, each with 8 or 9 classes — up to 54 in total**, narrowed by two dropdowns rather than a card grid.

#### Header (Screen 1 only)

Full-width, 88 px tall, white, closed by a 3 px brand-red rule.

- **Left:** the school crest at 56 px, then the school name **SK Seksyen 24 Shah Alam** at 20 px bold, with the crest motto *Berilmu Berdisiplin* beneath it at 14 px in warm grey. The motto is already on the badge; reusing it costs nothing and makes the page unmistakably this school's.
- **Right:** a **BM / EN** language toggle, and the **Guru** button.

Moving **Guru** into the header is a small but useful change: the footer becomes purely informational, so nothing a pupil might tap by accident sits at the bottom of the page.

**This header does not appear on Screens 2 and 3, and that is deliberate.** An 88 px header on the name screen would cut the card grid from 99 px rows to 81 px — below the height needed for a 40 px avatar plus two lines of text, which would break the one-screen requirement the whole design rests on. Screens 2 and 3 keep their compact 80 px top bar (Kembali, class pill), with the crest added at 36 px on the right for continuity. Same identity, a third of the cost.

#### Footer (all screens)

Full-width, 64 px, quiet, no primary actions:

- **Left:** *Senarai dikemas kini 6 Ogos 2026* — the visible check that the annual rollover happened (§9).
- **Right:** *Masalah? Panggil cikgu.*

#### Body — two columns

**Left column — the form (≈ 45% width).**

- Heading: **Pilih Kelas Anda**
- Dropdown 1: **Tahun** — Tahun 1 to Tahun 6
- Dropdown 2: **Kelas** — the classes of the selected tahun. **The count varies by year: some tahun have 8 classes, some 9.** The list is built from the roster, never hard-coded to a fixed length, and the panel scrolls internally past 8 rows rather than growing off-screen.
  Disabled and visibly greyed until a tahun is chosen, so the order of operations is self-evident.
- Primary button: **Teruskan →**, full column width, 64 px tall, brand green. Disabled until both dropdowns are set.
- **Kelas Terakhir** shortcut above the form when the device has a remembered class — a single card that skips both dropdowns. This is the fast path for a lab PC serving the same class all week.

Both dropdowns are **custom controls, not native `<select>` elements**. Native select popups render OS-default rows — typically under 20 px tall — which fails the 48 px touch-target requirement that the rest of the design depends on. Each uses 64 px rows and 20 px text, and the Kelas rows carry the class's accent colour as a swatch.

Both remember their last value per device.

**Right column — what this page is for (≈ 55% width).**

A calm explanatory panel on the soft cream surface, aimed at adults — teachers setting up a lab, parents opening it at home, and the headmaster asking what it is. Contents:

- A one-line statement of purpose: *Masuk ke DELIMa dan Google Classroom tanpa perlu menaip alamat e-mel yang panjang.*
- Three numbered steps with icons: **1.** Pilih kelas anda · **2.** Cari nama anda · **3.** Pilih DELIMa atau Google Classroom
- A short reassurance block, which matters more than it looks — it is the answer to the first question every parent and administrator asks: *Laman ini tidak menyimpan kata laluan. Anda akan menaip kata laluan di halaman rasmi Google.*
- The two destination logos, DELIMa and Google Classroom.

This column is informational only. It contains no interactive elements at all — **Guru** now lives in the header.

Selecting a tahun repopulates the Kelas dropdown in place. No navigation until **Teruskan**.

### Screen 2 — Cari Nama Anda

**Classes hold 30 to 44 pupils, and every pupil must fit on one screen without scrolling.** Plus one extra slot for the "Nama saya tiada" card — so the grid must seat between 31 and 45 items.

**The grid adapts to the actual class, not to the worst case.** Sizing every class as if it held 44 would punish a class of 30 with cards a third narrower than they need to be. Rows are fixed at 5 — enough for a 40 px avatar plus two lines of 18 px text — and the column count follows the roll:

| Pupils | Grid | Card | Chars per line |
| ---: | :--- | :--- | ---: |
| 30–34 | 7 × 5 | 179 × 99 px | ~19 |
| 35–39 | 8 × 5 | 156 × 99 px | ~16 |
| 40–44 | 9 × 5 | 137 × 99 px | ~14 |

*(at 1366 × 768, the primary lab display)*

Card height stays constant across all class sizes, so the screen keeps the same rhythm whichever class is loaded. Only the width changes.

**Card contents:** avatar at 40 px top-centred, then the name at 18 px bold on up to two lines.

**The name shown is the longest form that fits.** Rather than always abbreviating, the card renders the fullest version of the pupil's name that fits two lines at the current card width, with the unique calling name as the floor. In practice:

- A class of 32 (179 px cards, ~19 chars per line) shows **full names** — "Muhammad Danial / Bin Rahim" fits comfortably.
- A class of 44 (137 px cards, ~14 chars per line) falls back to the **calling name**, disambiguated where it collides.

Small classes get full names for free. That is worth having, because a full name is the strongest possible recognition cue and it removes the ambiguity risk entirely.

**Duplicate-name rule.** In a Malaysian class of 44, shared given names are near-certain — several pupils named Muhammad or Nur is the norm, not the exception. Two identical cards would cause wrong-pupil taps and failed sign-ins.

Where abbreviation is needed, the card shows the pupil's **calling name** disambiguated with initials — "Nur Aishah A." and "Nur Aishah O.", "Muhammad Danial R." and "Muhammad Danial S." This is how a teacher separates them aloud, and it stays within 18 characters. Computed per class at build time; see architecture §4.3.

The rule must handle all three Malaysian naming conventions, since getting it wrong silently mislabels a whole demographic: Malay and Indian names put the calling name **before** the particle (*Nur Aishah* Binti Ahmad, *Arjun* A/L Kumaran), while Chinese names put the surname **first**, so the calling name is what follows (Tan *Wei Ming*).

**A known limit at 1024 × 768.** Below roughly 1200 px of width, a class over 36 cannot show 9 columns at a readable width — 1024 px divided nine ways leaves 99 px cards, about 11 characters. On those machines the grid keeps readable cards and **scrolls vertically**, with the search box as the fast path. This is an accepted trade: unreadable names are worse than a scrollbar. Open question 4 asks whether any lab actually runs at that resolution — if none do, this case never arises.

**Also on the screen:** the **Kembali** button top-left, the class shown as a coloured pill including the tahun ("Tahun 2 Cemerlang"), and a search box — visually secondary, for the teacher — that filters on the **full** name even when an abbreviated form is displayed.

Sorted alphabetically by given name — matching how pupils are called in class, not how the roster is filed. Avatars come from a fixed set of ~40 friendly icons (animals, fruit, vehicles), assigned per pupil and stable across the year so recognition is learned.

**When the name isn't there.** A pupil whose name is missing — mid-year transfer, late enrolment, roster not yet rebuilt — is not an edge case. It will happen in the first week, in front of the class, to a child who cannot explain what went wrong. The current design leaves them stranded, which is the most damaging failure the product can produce for one pupil.

So the grid ends with a persistent final card, visually distinct (outlined, not filled, with a question-mark avatar): **"Nama saya tiada"**. Tapping it shows a calm screen with one instruction — *Panggil cikgu* — and, behind the teacher PIN, a field where the teacher types the pupil's 8-digit ID once to sign them in immediately and add them to the device-local overlay. The lesson continues; the roster gets fixed later.

### Screen 3 — Pergi ke Mana?

- Selected pupil shown at top — avatar, **full name** (this is where the patronymic reappears, as the identity check before handoff), and **masked email** (`m-1234••••@moe-dl.edu.my`). Confirms identity without printing a full account handle on a projector-facing screen.
- Two large buttons, each with its official product logo:
  - **DELIMa 3.0** — full learning portal (`d3.delima.edu.my`).
  - **Google Classroom** — straight to class stream.
- Third, quieter option: **Bukan saya** (Not me) → returns to Screen 2.
- A default destination can be pinned per device by the teacher; when pinned, Screen 3 auto-advances after a 1.5 s countdown with a visible **Batal** escape.

### Handoff to Google

Tapping a destination navigates the browser to Google's own sign-in page with the pupil's address supplied as a hint. The pupil sees Google's real, familiar screen. They type their password and continue to the destination.

**Session hygiene is part of the flow, not an afterthought.** On a shared lab PC the previous pupil's Google session is the single largest cause of wrong-account sign-ins. Every handoff therefore routes through a Google sign-out step first, so the pupil always lands on a clean password prompt for *their* account. See the architecture document, §5.

---

## 6. Functional Requirements

| ID | Requirement | Priority |
| :--- | :--- | :--- |
| FR-1 | Two-column landing page: Tahun + Kelas dropdowns and a Teruskan button on the left, an explanatory panel on the right | Must |
| FR-1a | Both dropdowns are custom controls with ≥ 48 px rows — never native `<select>` elements | Must |
| FR-1b | Both dropdowns default to the last value used on that device | Must |
| FR-1c | Kelas dropdown is disabled until a Tahun is selected; Teruskan disabled until both are set | Must |
| FR-2 | Display a full class (30–44 pupils) **plus the "Nama saya tiada" card on one screen without scrolling** at 1366 × 768 | Must |
| FR-2a | Compute a unique display name per class from the pupil's calling name plus initials, handling Malay, Chinese and Indian naming conventions | Must |
| FR-2b | Search filters on the **full** name even though only the given name is shown | Must |
| FR-3 | Offer DELIMa 3.0 (`d3.delima.edu.my`) and Google Classroom as destinations after pupil selection | Must |
| FR-4 | Hand off to Google sign-in with the pupil's email pre-populated | Must |
| FR-5 | Clear any prior Google session before handoff | Must |
| FR-6 | Never display a pupil's full email address on screen; mask the local part | Must |
| FR-7 | Function fully offline after first load (roster cached, service worker) | Must |
| FR-8 | Bahasa Melayu interface as default; English strings behind a toggle | Must |
| FR-9 | Teacher view: pin default destination, reassign avatar, correct a pupil **as a device-local overlay**, print class sheet. Roster *import* is a build-time CLI task owned by ICT, not a teacher action — see architecture §8 | Must |
| FR-10 | Teacher view gated behind a 4-digit PIN | Must |
| FR-11 | Remember last-used class per device and surface it first | Should |
| FR-12 | Auto-return to Screen 1 after 90 s of inactivity | Should |
| FR-13 | Name search filter within a class | Should |
| FR-14 | Printable per-class fallback sheet (name → email) for when the page is unreachable | Should |
| FR-15 | Per-device pinned destination with countdown auto-advance | Could |
| FR-16 | Text-to-speech name readback on card hover for pre-readers | Could |
| FR-17 | Public home build: one-time device setup storing the pupil's ID locally, then one-tap destination access | Must |
| FR-18 | Public home build serves **no pupil roster** — no names, no emails, no IC numbers | Must |
| FR-19 | Persistent "Tukar akaun" recovery link on the home build for wrong-account cases | Must |
| FR-20 | Sibling support on a shared family device (multiple stored identities) | Should |
| FR-21 | Name grid adapts its column count to the actual class size (7/8/9 columns × 5 rows) so smaller classes get wider cards | Must |
| FR-21a | Card shows the longest form of the name that fits two lines at the current card width, with the unique calling name as the floor | Must |
| FR-1d | Kelas dropdown length is read from the roster (8 or 9 per tahun), never hard-coded | Must |
| FR-24 | Screen 1 carries an 88 px header with the school crest, name and motto; Guru and the language toggle sit there | Must |
| FR-25 | Screens 2 and 3 use the compact 80 px top bar with a 36 px crest — **never the full header**, which would break the one-screen grid | Must |
| FR-26 | All screens carry a 64 px informational footer with the roster date and a "Panggil cikgu" line, and no primary actions | Should |
| FR-22 | Persistent **"Nama saya tiada"** card at the end of the grid, with a PIN-gated teacher path to sign in a missing pupil by ID | Must |
| FR-23 | Teacher can clear this device's remembered class and year (needed at annual rollover) | Should |

---

## 7. Non-Functional Requirements

**Performance.** First contentful paint ≤ 1.5 s on a 2015-era lab PC over school Wi-Fi. Total initial payload ≤ 300 KB gzipped excluding avatars. Roster of 2,000 pupils must render a class of 45 in under 100 ms.

**Availability.** Works when the school internet is flaky. Once cached, the page loads and lists names offline — only the final Google handoff needs connectivity, and the pupil will discover that anyway.

**Accessibility.** WCAG 2.2 AA. Contrast ≥ 4.5:1 on all text. Every interactive target ≥ 48 × 48 px. Full keyboard navigation. Screen-reader labels in Bahasa Melayu.

**Browser support.** Chrome/Edge 100+, Firefox 100+, Safari 15+. Graceful degradation on older lab builds: if the service worker is unsupported, the page still works online.

**Two deployment profiles.** The product ships as one codebase in two builds, because home and lab have incompatible privacy requirements (architecture §7.2):

- **Lab** — school network only. Full roster, full names, forced sign-out between pupils.
- **Home** — publicly bookmarkable, and serves **no roster at all**. A parent enters the pupil's 8-digit ID once during setup; it lives in that device's local storage and never touches the server. Every visit after is one tap.

This is a deliberate correction to the intuitive approach of "publish the page publicly but show only first names." Display masking does not protect the email address, because the address must reach the sign-in URL and therefore lives in the page's HTML. A public page that can prefill an email *is* a public list of emails — 2,000 verified, active MOE addresses belonging to identified minors. The home build solves this at the data layer instead: there is nothing on the server to harvest.

**Privacy & compliance.** Governed by PDPA 2010 (Malaysia) and MOE data-handling policy. The roster is personal data belonging to minors and is treated as such:

- No passwords are stored, transmitted, or logged — anywhere, at any time.
- No pupil roster is ever published on an internet-reachable host. Full email local parts, MyKad/IC numbers, register numbers tied to full names, and pupil photographs are never served publicly under any display masking.
- `noindex, nofollow` and an explicit `robots.txt` deny.
- No third-party analytics, tag managers, ad scripts, or fonts loaded from external CDNs.
- No pupil-level event logging. Aggregate counters only, and only if the school opts in.
- Full email never rendered on screen or placed in a page title or shareable URL.

**Google ToS compliance.** The application never embeds Google sign-in in an iframe or WebView, never scripts or scrapes Google pages, and never automates credential entry. It performs a normal top-level navigation to a Google URL with a documented account-hint parameter — the same thing a bookmark does. This is the entire reason the "normal" variant exists alongside the visual/injection product.

---

## 8. Content & Tone

Interface language is Bahasa Melayu, written for a 7-year-old reader.

| Screen | Heading | Notes |
| :--- | :--- | :--- |
| 1 | **Pilih Kelas Anda** | Left column only. The right-hand panel carries its own heading, **Apa itu laman ini?**, and is written for adults. |
| 2 | **Cari Nama Anda** | Back button labelled **Kembali**, not an unlabelled arrow. |
| 3 | **Hai, {Nama}! Nak pergi ke mana?** | Destination buttons carry official product names, untranslated. |
| Error | **Alamak, ada masalah.** | Followed by one plain instruction: *Panggil cikgu.* |

Never use: SSO, portal, autentikasi, log masuk tunggal, sesi. Use: *masuk*, *kelas*, *nama*, *kata laluan*.

---

## 9. Rollout

| Phase | Scope | Duration | Exit criteria |
| :--- | :--- | :--- | :--- |
| **−1 — Baseline** | Time 5 real lessons before anything is built | 1 week | §3 baselines replaced with measured figures; go/no-go on whether the problem is as large as assumed |
| 0 — Build | Core flow, one hard-coded class | 1 week | Flow works end to end on a real lab PC |
| 1 — Pilot | 2 classes (~90 pupils), one lab | 1 week | Median class sign-in ≤ 3 min (matching G1); zero wrong-account incidents |
| 2 — Grade rollout | All Tahap 1 classes | 2 weeks | Teacher can import a roster unaided |
| 3 — School-wide | All 2,000 pupils, all labs | 2 weeks | Metrics in §3 met; ICT coordinator owns roster updates |
| 4 — Hardening | Offline mode, teacher tools, printable fallback | ongoing | — |

**Pilot instrumentation is manual on purpose.** A stopwatch and a tally sheet in Cikgu Farah's hand beat any analytics package, and add no privacy surface.

### Annual rollover

Every product built around a school roster has one recurring operational moment, and most of them forget to specify it. Each January the roster is wrong in five ways at once: Tahun 6 has left, a new Tahun 1 has arrived, everyone else has moved up a year, class names have been reshuffled, and avatar assignments are stale.

This is not a code change — it is a scheduled task, and it needs an owner and a date or the product quietly rots into a list of last year's classes.

| Step | Owner | When |
| :--- | :--- | :--- |
| Export the new student roster | ICT coordinator | First week of the school year |
| Rebuild and redeploy `roster.json` | ICT coordinator | Same day |
| Clear per-device `Kelas Terakhir` memory in every lab | ICT coordinator | Same day — stale shortcuts point at last year's class |
| Reprint the per-class fallback sheets | Class teachers | First week |
| Verify Tahun 6 leavers are gone from the roster | ICT coordinator | Same day — their accounts may still exist at MOE |

The "Senarai dikemas kini" date on Screen 1 is the visible check: if it still reads last year, the rollover did not happen.

---

## 10. Risks

| Risk | Impact | Mitigation |
| :--- | :--- | :--- |
| Google changes the account-hint URL behaviour | High — core flow degrades | Destination URLs are config, not code. A documented OAuth-based fallback path exists (arch §5.3). Degraded state is a normal Google sign-in page — annoying, not broken. |
| Roster drifts from actual student list | Medium — pupil can't find their name | Teacher can add/edit a pupil in under 30 s without ICT. "Last updated" date shown on Screen 1. |
| Roster of minors exposed publicly | High — PDPA breach; harvestable phishing list | Lab build is network-restricted; home build serves no roster at all. Display masking is explicitly *not* relied upon. Arch §7.2. |
| Parent's own Gmail already signed in at home → "You need permission" on Classroom | Medium — bookmark abandoned | `Email` hint pre-selects the school account; a persistent "Tukar akaun" link gives one-tap recovery; setup explains the two-accounts point once. Arch §5.3. |
| Parent mistypes the 8-digit ID during home setup | Medium — silent failure at Google | Inline validation against `m-` + 8 digits, worked example on screen, printed class sheet as paper backing. |
| Pupils memorise nothing and are helpless elsewhere | Low–medium | Screen 3 shows the masked email each time, building familiarity. Printed fallback sheets stay in the lab. |
| Shared-PC session bleed causes wrong-account sign-in | High — pupil accesses classmate's work | Forced sign-out on every handoff (FR-5), plus 90 s idle reset (FR-12). |
| Teacher PIN shared with pupils | Low | PIN gates edits only; it exposes no passwords. Rotatable from teacher view. |
| Pupil's name missing from the roster mid-lesson | Medium — one child stranded publicly | "Nama saya tiada" card plus a PIN-gated teacher sign-in by ID (FR-22). The lesson continues; the roster is fixed later. |
| Annual rollover not performed | High — product silently serves last year's classes | Named owner and dated checklist in §9. The "Senarai dikemas kini" date on Screen 1 makes the failure visible. |
| Lab screens are not the resolutions assumed | Medium — 45 cards no longer fit | Responsive column count (FR-21); open question 4 asks for an actual walk-through of the labs. |
| Baseline estimates prove wrong at Phase −1 | Medium — weaker case than assumed | Measure before building. Phase −1 is explicitly a go/no-go, not a formality. |

---

## 11. Open Questions

1. ~~Portal host: `d2` or `d3`?~~ **Resolved — DELIMa 3.0 at `d3.delima.edu.my`.** Still worth a single live check on a lab PC before pilot, since it is one config value.
2. Does the school want one deployment for all 2,000 pupils, or one per grade to keep rosters small and lists short?
3. **Confirm the largest class on the roll.** The grid is sized for 44 pupils plus one spare card. A class of 45+ would need an extra column or a scroll — worth checking the actual student figures before build.
4. **What resolutions are actually in the labs?** The grid table assumes 1366 × 768. Classes over 36 will scroll on a 1024 × 768 machine. One walk through the labs, noting each screen setting, tells you whether that case is real or hypothetical.
5. Is a shared per-class avatar sheet needed on the wall so pupils can find their icon before touching the mouse?
6. Should Screen 3 include a third destination (Google Drive, or the school's own site)?
7. Who owns roster updates once ICT hands over — the coordinator, or each class teacher?
8. **Who signs off?** Headmaster, ICT coordinator, and possibly the district MOE office. Naming the approver now prevents the pilot stalling at the point it needs a decision.

---

*Companion document: `Technical_Architecture_Normal_SSO.md`*
