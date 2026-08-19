# Technical Architecture — DELIMa Normal SSO (Web Launcher)

**Version:** 1.0 (Draft)
**Date:** 6 August 2026
**Companion to:** `PRD_Normal_SSO.md`

---

## 1. Architectural Position

The design goal is *not* to build a good system. It is to build the smallest system that disappears.

There is no login. There is no session. There is no server-side state. The application is a static, cacheable page holding a read-only roster, whose entire job is to construct one URL and navigate to it. Everything security-critical — authentication, credential handling, session issuance — stays where it belongs: with Google.

This yields properties that matter more than features here:

- **Nothing to breach at runtime.** No credential store, no token store, no session store.
- **Nothing to operate.** No backend process, no database connection pool, no on-call rotation for a school ICT coordinator.
- **Nothing to break during a lesson.** After first load it runs from cache.

The only genuinely sensitive asset is the roster: names and email addresses of minors. Section 7 treats it accordingly.

---

## 2. Component Overview

```
┌──────────────────────────────────────────────────────────────┐
│  Lab PC / Chromebook / Phone — Browser                        │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Launcher SPA  (static HTML + CSS + JS, no framework    │  │
│  │  required; React/Preact optional)                       │  │
│  │                                                          │  │
│  │  Views:  Landing(2-col) → NameGrid(adaptive) → Destination│  │
│  │  Services:  RosterStore · UrlBuilder · SessionCleaner   │  │
│  │  Local:  IndexedDB (roster cache) · localStorage (prefs)│  │
│  └────────────────────────────────────────────────────────┘  │
│  ┌────────────────────────────────────────────────────────┐  │
│  │  Service Worker — precache shell + roster, offline-first │  │
│  └────────────────────────────────────────────────────────┘  │
└───────────────┬──────────────────────────────┬───────────────┘
                │ fetch roster.json (on load)  │ top-level navigation
                ▼                              ▼
     ┌────────────────────────┐      ┌──────────────────────────┐
     │  Static Host + CDN     │      │  accounts.google.com     │
     │  (Firebase Hosting /   │      │  (Google's own sign-in)  │
     │   Cloudflare Pages /   │      └────────────┬─────────────┘
     │   school intranet)     │                   │
     │                        │                   ▼
     │  /index.html           │      ┌──────────────────────────┐
     │  /roster.json (signed) │      │  d3.delima.edu.my  OR    │
     │  /avatars/*.svg        │      │  classroom.google.com    │
     └────────────────────────┘      └──────────────────────────┘

     ┌────────────────────────┐
     │  Admin CLI (Node)      │  APDM CSV ──► validate ──► roster.json
     │  run by ICT, offline   │              (build-time, not runtime)
     └────────────────────────┘
```

The admin tool is deliberately **build-time**. Roster changes are a weekly-at-most event; making them a deployment rather than a runtime write removes an entire class of authorisation problems.

---

## 3. Technology Choices

| Layer | Choice | Rationale |
| :--- | :--- | :--- |
| UI | Vanilla TS + Web Components, or Preact (~4 KB) | 2,000 records and three views. A framework here buys nothing and costs load time on 2015 lab hardware. |
| Styling | Plain CSS with custom properties | No build-time CSS toolchain to maintain. |
| Build | Vite | Fast, produces a static bundle, nothing exotic. |
| Offline | Workbox service worker | Precache shell + roster; stale-while-revalidate on roster. |
| Roster cache | IndexedDB | Survives restarts; handles 2,000 records without blocking the main thread. |
| Prefs | localStorage | Last **tahun**, last class, language, pinned destination, teacher PIN hash. Device-scoped, non-sensitive. |
| Hosting | Firebase Hosting or Cloudflare Pages (or school intranet nginx) | Static, free tier, HTTPS, edge-cached. Intranet option keeps roster inside the school network. |
| Admin tool | Node.js CLI (`npx delima-roster build`) | Runs on the coordinator's machine. No hosted admin panel to secure. |

**On Firestore:** the visual/injection product uses Firestore because it needs encrypted credentials fetched at runtime. This product does not. A signed static JSON is faster, cheaper, offline by default, and has no read rules to misconfigure. If the school later wants live roster editing across devices, Firestore can be introduced behind the same `RosterStore` interface (§4.4) without touching the views.

---

## 4. Data

### 4.1 Roster Schema (`roster.json`)

```jsonc
{
  "schema_version": 1,
  "school": {
    "code": "SKS24",
    "name": "Sekolah Kebangsaan Sungai 24",
    "domain": "moe-dl.edu.my"
  },
  "generated_at": "2026-08-06T09:00:00+08:00",
  "config": {
    "destinations": [
      {
        "id": "delima",
        "label": "DELIMa 3.0",
        "url": "https://d3.delima.edu.my/",
        "icon": "delima.svg"
      },
      {
        "id": "classroom",
        "label": "Google Classroom",
        "url": "https://classroom.google.com/",
        "icon": "classroom.svg"
      }
    ],
    "force_signout": true,
    "idle_reset_seconds": 90
  },
  "classes": [
    { "id": "2_cemerlang", "name": "2 Cemerlang", "grade": 2, "colour": "#F4A261" }
  ],
  "students": [
    {
      "id": "s_0001",
      "name": "Nur Aishah Binti Ahmad",
      "class_id": "2_cemerlang",
      "email_local": "m-12345678",
      "avatar": "kucing"
    }
  ]
}
```

Notes on deliberate choices:

- **`email_local` only.** The domain lives once, in `school.domain`. A leaked roster file is marginally less useful, the file is ~30% smaller, and there is exactly one place to change if MOE alters the domain.
- **No `encrypted_password` field, and none permitted.** The schema validator in the admin CLI **rejects** any unknown field on a student record. This is enforcement, not documentation: it prevents a well-meaning future contributor from "just adding" a password column and silently converting this product into the other one.
- **`colour` per class**, so pupils navigate by colour before they can read.
- **`avatar`** is a key into a bundled SVG set — no image URLs, no external fetches.

### 4.2 Size & Performance

2,000 students × ~110 bytes ≈ 220 KB raw, ~45 KB gzipped. Fetched once, cached in IndexedDB, indexed by `class_id` on first load. Rendering 44 cards is trivial; no virtualisation needed.

### 4.3 Grid Sizing & Display Names

Screen 2 must fit a full class — **30 to 44 pupils, plus one "Nama saya tiada" card** — on one screen without scrolling. On a 1366 × 768 lab display, after the top bar, heading and search box, that leaves 1318 × 536 px.

**Rows are fixed at 5; columns follow the class size.** Sizing every class for the worst case would give a class of 30 the same cramped cards as a class of 44, for no reason.

```ts
/** Grid for a class, at the primary lab resolution. Rows fixed at 5. */
function gridFor(pupils: number) {
  const items = pupils + 1;              // + "Nama saya tiada"
  const rows = 5, gap = 10, usableW = 1318, usableH = 536;
  const cols = Math.ceil(items / rows);
  return {
    cols, rows,
    cardW: Math.floor((usableW - (cols - 1) * gap) / cols),
    cardH: Math.floor((usableH - (rows - 1) * gap) / rows),  // 99 px, constant
  };
}
```

| Pupils | Grid | Card | Chars/line @ 18 px |
| ---: | :--- | :--- | ---: |
| 30–34 | 7 × 5 | 179 × 99 | ~19 |
| 35–39 | 8 × 5 | 156 × 99 | ~16 |
| 40–44 | 9 × 5 | 137 × 99 | ~14 |

Card height is constant, so the page keeps the same rhythm whichever class loads.

**The card renders the longest form of the name that fits two lines at the current width**, with the unique calling name as the floor. At 179 px that is usually the full name ("Muhammad Danial / Bin Rahim"); at 137 px it falls back to the calling name plus initials. Small classes get full names for free — the strongest recognition cue available, and it removes the ambiguity risk entirely.

**Known limit below ~1200 px.** At 1024 × 768 a class over 36 cannot show 9 columns at a readable width — 99 px cards hold about 11 characters. On those machines the grid keeps readable cards and scrolls vertically instead. Unreadable names are worse than a scrollbar.

This creates a collision problem that is a near-certainty rather than an edge case: in a Malaysian class of 45, several pupils named Muhammad or Nur is the norm. Two identical cards means wrong-pupil taps and failed sign-ins.

The display name is therefore **computed per class at build time** — never stored in the roster, never hand-maintained.

A naive "shortest unique word prefix" is the obvious approach and it is wrong here: it produces `Muhammad Danial Bin Rahim` at 25 characters, which cannot fit the card. The rule instead keeps the pupil's *calling name* whole and disambiguates with initials — which is also how a teacher does it aloud.

Malaysian names need three parsing cases, and getting them wrong silently mislabels a whole demographic:

| Form | Example | Calling name |
| :--- | :--- | :--- |
| Malay — particle `bin`/`binti` | Nur Aishah **Binti** Ahmad | words **before** the particle → *Nur Aishah* |
| Indian — particle `a/l`/`a/p` | Arjun **A/L** Kumaran | words **before** the particle → *Arjun* |
| Chinese — no particle, surname **first** | **Tan** Wei Ming | words **after** the first → *Wei Ming* |

```ts
const PARTICLE = /^(bin|binti|bt|bte|a\/l|a\/p|s\/o|d\/o)$/i;

/** Split a Malaysian name into the part a pupil is called by, and the rest. */
function split(name: string) {
  const w = name.trim().split(/\s+/);
  const i = w.findIndex(x => PARTICLE.test(x));
  if (i > 0)          return { given: w.slice(0, i), rest: w.slice(i + 1) }; // Malay / Indian
  if (w.length >= 3)  return { given: w.slice(1),    rest: [w[0]] };         // Chinese
  return { given: w, rest: [] };                                            // 1–2 words
}

const initials = (a: string[]) => a.map(x => x[0].toUpperCase() + ".").join("");

export function computeDisplayNames(students: Student[]): Map<string, string> {
  const P = new Map(students.map(s => [s.id, split(s.name)]));
  const label = (id: string, d: number) => {
    const p = P.get(id)!;
    return d === 0 ? p.given.join(" ")
                   : p.given.join(" ") + " " + initials(p.rest.slice(0, d));
  };
  const out = new Map<string, string>();
  for (const s of students) {
    let d = 0;
    const max = P.get(s.id)!.rest.length;
    while (d <= max) {
      const mine = label(s.id, d);
      if (!students.some(o => o.id !== s.id && label(o.id, d) === mine)) break;
      d++;
    }
    out.set(s.id, label(s.id, Math.min(d, max)));
  }
  return out;
}
```

Verified output on a mixed class:

| Full name | Card shows | Chars |
| :--- | :--- | ---: |
| Muhammad Danial Bin Rahim | Muhammad Danial R. | 18 |
| Muhammad Danial Bin Salleh | Muhammad Danial S. | 18 |
| Muhammad Amirul Bin Zaki | Muhammad Amirul | 15 |
| Nur Aishah Binti Ahmad | Nur Aishah A. | 13 |
| Nur Aishah Binti Osman | Nur Aishah O. | 13 |
| Tan Wei Ming | Wei Ming | 8 |
| Chong Mei Ling | Mei Ling | 8 |
| Arjun A/L Kumaran | Arjun | 5 |

All unique, longest 18 characters — two comfortable lines on a 136 px card, against 26 for the naive approach.

Two consequences to enforce:

- **The admin CLI warns** when any computed display name exceeds 18 characters, so the coordinator can set a manual short name before it reaches a lab.
- **Search matches the full name**, not the display name. A teacher typing "Rahim" must find Danial even though the card reads "Muhammad Danial R."

### 4.4 `RosterStore` Interface

```ts
interface RosterStore {
  getSchool(): Promise<School>;
  getConfig(): Promise<AppConfig>;
  listYears(): Promise<number[]>;                    // [1..6]
  listClasses(year?: number): Promise<ClassInfo[]>;  // 8–9 when year given, ≤54 when not
  listStudents(classId: string): Promise<Student[]>;   // 30–44, with computed display_name
  getStudent(id: string): Promise<Student | null>;
  lastUpdated(): Promise<Date>;
}
```

**Scale note.** 6 tahun × 8–9 classes = up to 54 classes, ~2,000 pupils, 30–44 pupils per class. `listClasses(year)` is what Screen 1 calls; the unfiltered form exists only for the teacher panel and the printable sheets. Index classes by `grade` at load — it costs nothing at 54 records and keeps the dropdown's filter instant. **Class counts differ per tahun (8 or 9), so nothing may assume a fixed length.**

Two implementations: `StaticJsonRosterStore` (v1) and, if ever needed, `FirestoreRosterStore`. Views depend only on the interface.

---

## 5. The Handoff — Core Mechanism

This is the only part of the system that is not obvious, so it is specified precisely.

### 5.1 Requirement

Navigate the top-level browser window to Google's sign-in page such that (a) the email field is pre-populated with the pupil's address, (b) the pupil is prompted for a password, and (c) after success they land on the chosen destination.

Constraints: no iframe, no WebView, no scripting of Google pages, no keystroke automation. Only a link.

### 5.2 Primary Method — Account Chooser Hint

> ## ⚠ Unverified as of August 2026 — observed returning HTTP 400
>
> While running T0.2 for the sibling product, this exact URL shape —
> `AccountChooser?Email=…&hd=…&continue=https://d3.delima.edu.my/landing` —
> **returned a 400 error against live Google.** Two candidate causes, not yet distinguished:
>
> 1. Google restricts `continue` to Google-owned domains as open-redirect protection, so a `continue` pointing at a school or portal domain is rejected outright; or
> 2. the `/AccountChooser` endpoint has been retired.
>
> **Either would break this method, and with it this product's core mechanism.** §5.3 below already warned that `AccountChooser` "is not a contractually documented API surface" while `login_hint` on the OAuth 2.0 authorization endpoint is — that risk appears to have arrived.
>
> **Verify before building anything on this section.** Open the URL above in a clean browser profile with a real address. If it 400s, this method is dead and the product needs re-planning around a documented flow. See `../Visual_SSO/T0.2_URL_Confirmation.md` for the test method.
>
> Note that `Visual_SSO` is **not** blocked by this: it types the email itself, so it degrades to a two-step injection (arch §4.5, route C). `Normal_SSO` has no such fallback — the hint *is* the product.

```ts
const BASE = "https://accounts.google.com/AccountChooser";

function buildSignInUrl(student: Student, dest: Destination, school: School): string {
  const email = `${student.email_local}@${school.domain}`;
  const u = new URL(BASE);
  u.searchParams.set("Email", email);          // pre-populates the identifier field
  u.searchParams.set("hd", school.domain);     // hosted-domain hint; suppresses consumer accounts
  u.searchParams.set("continue", dest.url);    // where Google sends them after auth
  return u.toString();
}
```

Produces, for example:

```
https://accounts.google.com/AccountChooser
  ?Email=m-12345678%40moe-dl.edu.my
  &hd=moe-dl.edu.my
  &continue=https%3A%2F%2Fclassroom.google.com%2F
```

`URL.searchParams` handles percent-encoding, which matters: an unencoded `continue` value is the most common way this breaks.

### 5.3 Session Cleaning — Not Optional on Shared PCs

On a lab PC the previous pupil's Google cookie is still present. Without intervention the account chooser may show a stale account, or `continue` may resolve straight into it. The result is a pupil silently working inside a classmate's Classroom — the worst failure mode this product can produce, and a data-protection incident.

Therefore, when `config.force_signout` is true, the handoff chains through Google's sign-out endpoint:

```ts
function buildHandoffUrl(student, dest, school, config) {
  const signIn = buildSignInUrl(student, dest, school);
  if (!config.force_signout) return signIn;
  const out = new URL("https://accounts.google.com/Logout");
  out.searchParams.set("continue", signIn);
  return out.toString();
}
```

One extra redirect, roughly 300 ms, and every pupil gets a clean password prompt for their own account.

**Defence in depth.** Chained sign-out is a convention Google may alter. The lab build should *also* be configured so the browser clears cookies on exit, and the teacher's **Selesai** action should close the browser rather than merely navigating away. Correctness must not rest on the URL trick alone.

**At home, forced sign-out is wrong.** It would sign the parent out of their own Gmail — a hostile act on a family device, and one that gets the bookmark deleted. So `force_signout` is set per deployment profile (§7.2): `true` in the lab, `false` at home.

This leaves the home case relying on the `Email` hint alone to select the right account when a personal Gmail is already signed in. Be realistic about what that achieves: `Email` reliably *pre-selects* the intended account in the chooser, but where a personal session is already active Google may still resolve `continue` into it — producing the familiar **"You need permission"** Classroom error. The hint substantially reduces this; it does not eliminate it.

The home build therefore treats wrong-account as an expected state with a designed exit, not an error:

- A persistent, plainly worded **"Bukan akaun anda? Tukar akaun"** link sits beneath the destination buttons, pointing at the same handoff URL wrapped in the sign-out chain. One tap resolves it.
- On first setup, the parent is told once, in plain language, that the school account and the family Gmail are different accounts and both can stay signed in.
- The printable fallback sheet (FR-14) carries the same instruction.

A one-tap recovery the parent understands beats a clever redirect they don't.

### 5.4 Fallback Method — OAuth 2.0 `login_hint`

`AccountChooser` is long-standing and widely relied upon, but it is not a contractually documented API surface. `login_hint` on the OAuth 2.0 / OpenID Connect authorization endpoint **is** documented and supported.

If Google ever changes account-chooser behaviour, or the school prefers a fully supported path:

1. Register an OAuth 2.0 Web client in a Google Cloud project owned by the school, restricted to `hd=moe-dl.edu.my`.
2. Build the authorization URL with `login_hint` set to the pupil's email and `prompt=login` to force credential entry.
3. Handle the redirect on a thin callback page whose only job is to forward to the destination. Request `openid email` scope only; discard the token immediately — the useful side effect is the established Google session, not the token.

```
https://accounts.google.com/o/oauth2/v2/auth
  ?client_id=<school_client_id>
  &redirect_uri=<launcher>/auth/callback
  &response_type=code
  &scope=openid%20email
  &login_hint=m-12345678%40moe-dl.edu.my
  &hd=moe-dl.edu.my
  &prompt=login
  &state=<csrf_nonce>
```

This costs a callback endpoint and a Cloud project. Ship §5.2, keep §5.4 specified and tested behind a config flag.

### 5.5 Degradation Ladder

| Condition | Behaviour |
| :--- | :--- |
| Hint honoured | Password prompt, email filled. Ideal. |
| Hint ignored by Google | Standard Google sign-in page, email empty. Pupil calls teacher; product is no worse than today. |
| `continue` rejected | Pupil lands on Google home, signed in. Teacher-visible bookmark to DELIMa in the lab covers this. |
| No network | Launcher still renders from cache; handoff fails at the browser level with a plain-language message. |

Failure is always visible and always recoverable by a human. Nothing fails silently into a wrong account.

---

## 6. Application Structure

```
src/
  main.ts                  bootstrap, router, idle timer
  views/
    Landing.ts             two-column: Tahun+Kelas dropdowns | info panel
    NameGrid.ts            adaptive grid, 30–44 pupils, no scroll
    DestinationPicker.ts
    TeacherPanel.ts
  components/
    BigSelect.ts           custom dropdown, 64px rows — never native <select>
  services/
    RosterStore.ts         interface + StaticJsonRosterStore
    DisplayName.ts         shortest-unique-prefix rule (§4.3)
    UrlBuilder.ts          §5 — pure functions, fully unit-tested
    Prefs.ts               localStorage wrapper
    Idle.ts                inactivity → reset to ClassGrid
  i18n/
    ms.json                default
    en.json
  styles/tokens.css        colour, spacing, type scale
sw.ts                      service worker
public/
  roster.json              generated artefact — never hand-edited
  avatars/*.svg
tools/
  roster-build.ts          CSV → validated roster.json
  roster-validate.ts       schema + field-allowlist enforcement
```

Routing is hash-based (`#/kelas/2_cemerlang`) so it works from `file://` and from an intranet share without server rewrite rules. Route state carries **`student_id` only, never an email** — hash fragments end up in bookmarks, screenshots, and projector screens.

### 6.1 Teacher Panel

Reachable from Screen 1, gated by a 4-digit PIN stored as a salted hash in `localStorage`. To be clear about what that PIN is worth: it is a *lid*, not a lock. It stops a curious pupil, not an adversary. It is acceptable precisely because there is nothing behind it worth stealing — no passwords, and the same roster the browser already holds.

Capabilities: pin default destination, set/clear last-class memory, switch language, add or correct a pupil for the current session (local-only overlay, cleared on next roster deploy), show roster version, print the class fallback sheet.

Deliberately absent: bulk export, full-email display, anything that writes to the deployed roster.

---

## 7. Security & Privacy

### 7.1 Threat Model

| Asset | Threat | Control |
| :--- | :--- | :--- |
| Pupil passwords | Theft, misuse | **Not present in the system.** No storage, transit, logging, or automation. The strongest control available. |
| Roster (names + emails of minors) | Public exposure → harvested as a phishing/contact list for minors | Solved at the data layer, not the display layer: the lab build is network-restricted, the public build serves no roster (§7.2). Plus `noindex`, no external requests, on-screen masking. |
| Google session on a shared PC | Wrong-account access to a classmate's work | Forced sign-out chain (§5.3), browser cookie clearing on exit, 90 s idle reset. |
| Roster integrity | Tampered file redirects pupils to a phishing host | Destinations validated against an allowlist at runtime (§7.3); roster served over HTTPS; SRI on the bundle. |
| Teacher PIN | Pupil edits roster | Bounded blast radius — PIN gates no credentials. Rotatable. |

### 7.2 Deployment Profiles — Lab vs Home

There is real demand for parents to bookmark this at home, and the value is obvious: the email is exactly as hard to type on a Sunday evening as it is in the lab. But home access and lab access have incompatible privacy requirements, so they ship as **two builds from one codebase**, not one build with a permission toggle.

#### The constraint that drives the design

Masking the displayed email — showing first names only, hiding the MyKad/IC, organising by class — is correct and necessary, but on a public page **it does not protect the email address**. The address has to reach the `AccountChooser` URL, so it lives in the page: in the `href`, in `roster.json`, or in the JS that builds the link. Any of those is one View Source away.

```html
<!-- Card displays "Aishah". The address is public anyway. -->
<a href="https://accounts.google.com/AccountChooser?Email=m-12345678%40moe-dl.edu.my&...">
```

So the honest statement of the problem is: **a public page that can prefill an email is a public list of email addresses.** A scraper walking 48 class views yields 2,000 verified, active MOE addresses belonging to identified minors — a high-quality phishing and contact list. Display masking does not change this. It must be solved at the data layer.

#### Profile A — `lab` (school network)

| | |
| :--- | :--- |
| Hosting | School intranet, or CDN with IP allowlist on school egress ranges |
| Roster | Full: full names, `email_local`, avatars |
| Display | Full name on card; email masked on screen (§ PRD FR-6) |
| `force_signout` | `true` — shared PCs, see §5.3 |
| Rationale | Not internet-reachable, so the roster is not exposed. Fastest possible flow for a 40-minute lesson. |

#### Profile B — `home` (public, recommended)

**No roster is published at all.** The public build ships the UI and nothing else. The pupil's identity lives on their own family device.

First visit: a single setup card asks for the pupil's 8-digit ID (the digits already printed on the card in their school bag) and their class. The page stores it in `localStorage` and constructs the links client-side from then on. Every subsequent visit goes straight to the two destination buttons — one tap, name and avatar shown, exactly like the lab flow.

```ts
// home profile: identity is device-local, never served
interface HomeIdentity {
  email_local: string;   // "m-12345678"  — entered once by the parent
  display_name: string;  // "Aishah"      — free text, for warmth only
  class_id?: string;     // optional, purely cosmetic
  avatar?: string;
}
```

What this buys:

- The server publishes **zero personal data**. Nothing to scrape, nothing to leak, no PDPA exposure surface, no data-processing agreement needed to host it publicly.
- Parents can bookmark it on any device, exactly as requested.
- The setup step is a one-time cost paid by an adult, not a per-login cost paid by a child.
- A shared family PC handles siblings cleanly: store an array, show a two-card picker.

The cost is that first-visit setup, and it is worth being clear about it: this is the one screen a parent must get right. It needs a worked example (`Contoh: m-12345678`), inline validation against `^m-\d{8}$`, and the printable class sheet from FR-14 as the paper backing.

#### Profile C — `home_roster` (public with class browsing) — only if B is rejected

If browsing by class at home is judged essential, the roster may be published **only in split form**: full local part never served.

```jsonc
// public/roster.home.json — note what is absent
{
  "students": [
    { "id": "s_0001", "first_name": "Aishah", "class_id": "4_cemerlang", "email_prefix": "m-1234" }
  ]
}
```

The pupil taps their first name, then types the **last four digits** of their ID to complete the address. A scraper gets first names and half an address — not a usable contact list. The pupil types four digits instead of twenty-six characters, which still delivers most of the time saving.

Constraints if this profile is used: first names only, never full names; no IC/MyKad, no class register numbers tied to full names, no photographs; `email_prefix` is exactly the first 4 digits, never more; `noindex` and `robots.txt` deny still apply.

Profile C is a compromise, not a recommendation. Profile B is strictly safer and only marginally less convenient.

#### Never ship

Full `email_local` values on a publicly reachable host, under any display masking. This is the specific thing the design exists to prevent.

#### Common to all public profiles

```html
<meta name="robots" content="noindex, nofollow, noarchive">
```

```
# robots.txt
User-agent: *
Disallow: /
```

And a strict CSP, since the page loads nothing from anywhere else:

```
Content-Security-Policy:
  default-src 'self';
  img-src 'self' data:;
  style-src 'self';
  script-src 'self';
  connect-src 'self';
  form-action 'none';
  frame-ancestors 'none';
  base-uri 'self'
```

`frame-ancestors 'none'` prevents the launcher being framed by a look-alike page — the realistic phishing vector against a product whose whole purpose is sending children to a password prompt.

### 7.3 Destination Allowlist

Runtime-enforced, so a tampered or mistyped roster cannot redirect pupils anywhere unexpected:

```ts
const ALLOWED_HOSTS = [
  "delima.edu.my",
  "classroom.google.com",
  "accounts.google.com",
];

function assertAllowed(url: string): void {
  const h = new URL(url).hostname;
  const ok = ALLOWED_HOSTS.some(a => h === a || h.endsWith("." + a));
  if (!ok) throw new DestinationBlockedError(h);
}
```

Runs on every handoff, and again in the admin CLI at build time.

### 7.4 Logging

No pupil-level logging. No analytics SDK. No error-reporting service that would ship a name or email off-device. If usage evidence is needed for the pilot, it is a tally sheet in the teacher's hand — accurate enough for the decision, and it creates no data to protect.

---

## 8. Admin Tooling

### 8.1 CSV Input

APDM/DELIMa exports vary. The CLI accepts a flexible header mapping and validates hard:

```
nama,kelas,emel,avatar
Nur Aishah Binti Ahmad,2 Cemerlang,m-12345678@moe-dl.edu.my,kucing
```

### 8.2 Build Pipeline

```bash
npx delima-roster build \
  --csv ./apdm-export.csv \
  --school SKS24 \
  --out ./public/roster.json
```

Validation gates — all fatal:

1. Every email matches `^m-\d{8}@moe-dl\.edu\.my$` (pattern configurable per school).
2. No duplicate emails; no duplicate pupil IDs.
3. Every `class_id` resolves to a declared class.
4. **No field outside the allowlist** — specifically, any column resembling a password (`password`, `kata_laluan`, `pw`, `pass`, `encrypted_*`) aborts the build with an explicit error. This is the schema-level guarantee behind the product's central privacy claim; it must fail loudly, not warn.
5. Avatar keys exist in the bundled set; unassigned pupils get a deterministic avatar derived from a hash of their ID (stable year-round).

Output is written with a `generated_at` timestamp and a SHA-256 printed to stdout for the coordinator's records.

### 8.3 Deployment

```bash
npm run build            # bundle SPA + roster
npm run deploy           # firebase deploy --only hosting  (or rsync to intranet)
```

One command. Reversible by redeploying the previous roster, which the coordinator keeps as a dated file.

---

## 9. Offline Behaviour

| Asset | Strategy |
| :--- | :--- |
| App shell (HTML/CSS/JS) | Precache; cache-first |
| `roster.json` | Precache; stale-while-revalidate |
| Avatars | Precache; cache-first |
| Google endpoints | Never cached, never intercepted |

The service worker must **not** intercept `accounts.google.com` requests — those are top-level navigations to another origin and are outside its scope by design. Stated explicitly because an over-broad `fetch` handler is an easy and confusing mistake.

Offline state is shown as a small, non-alarming banner: *"Tiada internet — nama masih boleh dipilih."*

---

## 10. Testing

**Unit (Vitest).** `UrlBuilder` is the highest-value target: encoding of `continue`, correct `Email` and `hd`, sign-out chaining on/off, allowlist rejection of a hostile host, and — as a regression guard — snapshot tests pinning the exact generated URL strings so a change to the handoff is never accidental.

**Schema (Vitest).** Roster validator: duplicate detection, malformed email rejection, and an explicit test asserting that a CSV containing a password column **fails the build**.

**Component.** Class grid renders all classes; student grid filters correctly with accents and mixed case; back navigation preserves scroll; idle timer resets to Screen 1.

**E2E (Playwright).** Full three-tap flow, asserting the final navigation URL — stopping short of Google's page, which is neither ours to automate nor stable to assert against.

**Manual, on real hardware.** Cold load timing on a lab PC. Sign-in immediately following a different pupil's session, verifying no account bleed. Offline load after Wi-Fi drop. Two adults attempting the flow as an 8-year-old would — no reading ahead, no hovering for tooltips.

**Accessibility.** axe-core in CI; manual keyboard traversal; contrast audit on every class colour, since bright child-friendly palettes fail 4.5:1 easily.

---

## 11. Build Sequence

| # | Deliverable | Est. |
| :--- | :--- | :--- |
| 1 | Repo scaffold, Vite + TS, design tokens, avatar set | 0.5 d |
| 2 | `roster-build` CLI + validator (incl. password-column rejection) | 1 d |
| 3 | `RosterStore` + IndexedDB cache | 0.5 d |
| 4 | ClassGrid and StudentGrid views | 1 d |
| 5 | `UrlBuilder` + DestinationPicker + sign-out chain | 1 d |
| 6 | i18n (ms/en), idle reset, last-class memory | 0.5 d |
| 7 | Service worker, offline banner | 0.5 d |
| 8 | Teacher panel + PIN + printable fallback sheet | 1 d |
| 9 | Test suite (unit, component, E2E) | 1 d |
| 10 | Hosting, CSP, access restriction, deploy runbook | 0.5 d |
| | **Total** | **~7.5 days** |

---

## 12. Relationship to the Visual SSO Desktop App

| | Normal SSO (this) | Visual SSO (desktop) |
| :--- | :--- | :--- |
| Form | Web page, any device | Windows app, lab PCs |
| Passwords | None, anywhere | AES-encrypted in Firestore, typed via `SendKeys` |
| Pupil types | Password only | Nothing |
| Google ToS posture | A link. Uncontroversial. | Compliant if strictly native-Chrome, but needs continuous care |
| Data-protection review | Roster only | Roster + credentials of 2,000 minors |
| Failure mode | Blank email field | Wrong password typed into the wrong window |
| Ship time | ~1.5 weeks | Materially longer |

They can share a roster format and an avatar set. They should not share a codebase, and the shared roster schema must remain password-free — that constraint is what lets this product carry a much lighter compliance burden.

Worth stating plainly: **this product should ship first regardless of the desktop app's fate.** It captures most of the time saving, at a small fraction of the risk, and it is the fallback if the injection approach is ever blocked by policy or by a Chrome change.

---

## Appendix A — Reference URLs

```
Sign-in with hint:
https://accounts.google.com/AccountChooser?Email={email}&hd={domain}&continue={dest}

With forced sign-out (shared PCs):
https://accounts.google.com/Logout?continue={urlencoded sign-in URL above}

Destinations:
  DELIMa 3.0        https://d3.delima.edu.my/
  Google Classroom  https://classroom.google.com/

Documented OAuth fallback (§5.4):
https://accounts.google.com/o/oauth2/v2/auth?client_id=...&login_hint={email}&hd={domain}&prompt=login&...
```

## Appendix B — Config Reference

| Key | Default | Purpose |
| :--- | :--- | :--- |
| `school.domain` | `moe-dl.edu.my` | Appended to `email_local`; used as `hd` |
| `config.force_signout` | `true` | Chain through Google sign-out before handoff |
| `config.idle_reset_seconds` | `90` | Return to Screen 1 when unattended |
| `config.destinations[]` | DELIMa, Classroom | Ordered buttons on Screen 3 |
| `config.default_destination` | none | Set per device from teacher panel |
| `config.handoff_mode` | `account_chooser` | Or `oauth` (§5.4) |
| `config.profile` | `lab` | `lab` \| `home` \| `home_roster` (§7.2) |
| `config.show_switch_account` | `false` in lab, `true` at home | Renders the "Tukar akaun" recovery link |

### Profile matrix

| | `lab` | `home` | `home_roster` |
| :--- | :--- | :--- | :--- |
| Publicly reachable | No | Yes | Yes |
| Roster served | Full | **None** | First name + 4-digit prefix |
| Full name shown | Yes | Device-local only | No — first name only |
| Pupil typing | None | None (after one-time setup) | Last 4 digits |
| `force_signout` | `true` | `false` | `false` |
| PDPA exposure | None (not internet-reachable) | **None** | Partial identifiers only |

---

**Sources consulted for the handoff mechanism:**

- [SSO sign-in flow when using login hints — Google Workspace Admin Help](https://support.google.com/a/answer/15544042?hl=en)
- [OpenID Connect — Sign in with Google, Google for Developers](https://developers.google.com/identity/openid-connect/openid-connect)
- [DELIMa — Cara Login](https://cerdik.my/delima/) (school confirms DELIMa 3.0 at `d3.delima.edu.my`)
