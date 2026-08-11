# Google Stitch — UI Build Guide

**For:** DELIMa Normal SSO (Web Launcher)
**Tool:** [stitch.withgoogle.com](https://stitch.withgoogle.com) — free, Google account only
**Version:** 1.0 · 6 August 2026
**Companion to:** `PRD_Normal_SSO.md`, `Technical_Architecture_Normal_SSO.md`

---

## 0. How to Use This Document

Every fenced block below is **copy-paste ready**. Don't paraphrase them — Stitch responds to specificity, and these prompts encode decisions already made in the PRD (touch-target sizes, Bahasa Melayu copy, masked emails, contrast ratios).

Recommended order:

1. Read §1 so you know what you're steering toward.
2. Paste **§2 Design System** into Stitch first, as its own prompt. This anchors the visual language for everything after.
3. Paste **§3 Prompt A** (multi-screen) to get the whole flow in one shot.
4. Use **§4** refinement prompts to fix what comes back wrong — and something always does.
5. Run the **§6 checklist** before exporting.
6. Export per **§7**.

Stitch generates up to five interconnected screens per generation, so the entire core flow fits in one prompt. Generate the flow first, polish second. Fighting details on screen 1 before you've seen screen 3 wastes generations.

**A caution worth stating up front:** Stitch produces convincing-looking output fast. That's the value and the trap. This interface is used by 7-year-olds who cannot read fluently, on aging lab PCs, and a plausible-looking design that fails a contrast check or shrinks a tap target is worse than an ugly one that works. The §6 checklist is not optional polish — it's the part that decides whether this ships.

---

## 1. What You Are Designing

A pupil aged 7–9 opens a bookmarked page in a school computer lab. In three taps they reach Google's sign-in screen with their email already filled in.

```
Screen 1              Screen 2          Screen 3           → Google sign-in
Landing           →   Cari Nama    →    Pergi ke Mana?       (not designed by us)
Tahun▾ Kelas▾         30–44 cards       DELIMa 3.0 /         email prefilled,
+ info panel          one screen        Google Classroom     pupil types password
```

Two counts drive the whole layout, and both are unusual:

- **Up to 54 classes** (6 tahun × 8 or 9 — the count differs by year). Screen 1 narrows them with **two dropdowns**, not cards.
- **30–44 pupils per class, all on one screen with no scrolling**, plus one spare card. Rows are fixed at 5 and the column count follows the class size, so cards are 137–179 px wide. They stay well above the 48 px touch minimum — what varies is text width, not tappability.

Three design constraints override everything else, including aesthetics:

| Constraint | Why | Consequence for the UI |
| :--- | :--- | :--- |
| The user may not read fluently | Tahap 1 pupils | Colour, avatars and position carry meaning before text does. Their **given name** is the one string they reliably recognise — make it the largest element on the card, and show only that. |
| The user has poor motor control | Age 7–9, unfamiliar mouse | Nothing interactive smaller than 48 px. Class-level controls ≥ 240 × 180 px; pupil cards 137–179 × 99 px — small, but still well above the minimum. |
| The screen is shared and semi-public | Lab PC, sometimes projected | Never render a full email address. Always mask: `m-1234••••@moe-dl.edu.my`. |

Two frames of reference that produce better output than "make it fun": **a children's picture book** (large friendly type, flat colour, generous white space, one idea per page) and **a physical classroom name chart** (the wall of laminated cards each pupil already finds their own name on every morning). Avoid anything that reads as a dashboard, a SaaS product, or a corporate portal.

---

## 2. Design System Prompt

Paste this into Stitch **first**, on its own.

```
I am designing a web app for Malaysian primary school pupils aged 7 to 9.
Establish this design system and apply it to everything I ask for next.

BRAND FEELING
Warm, calm, playful but not noisy. Like a well-made children's picture book:
large friendly shapes, flat colour, lots of breathing room, one clear idea per
screen. NOT a dashboard. NOT a corporate portal. NOT a SaaS admin panel.
No gradients on text, no glassmorphism, no dark mode, no dense data tables.

COLOUR — derived from the SK Seksyen 24 Shah Alam school crest

The crest uses four colours. These are sampled from the badge, exact:
  Oren  #F7941D   Merah #ED1B24   Hijau #056839   Krim  #FAF7B2
plus black outlines and white.

IMPORTANT RULE about these three brand colours, follow it exactly:
  - Oren #F7941D takes DARK text only. It is far too light for white text
    (2.3:1, fails badly). The crest itself puts black lettering on the orange
    ribbon — follow the crest.
  - Merah #ED1B24 takes NO text at all, light or dark. It is a fill and accent
    colour only: icon shapes, illustration, thin rules, selected-state borders.
  - Hijau #056839 takes WHITE text and is safe (6.9:1). It is the sturdiest
    brand colour and carries the primary action.

System palette:
  Page background:   #FFFDF7  (warm off-white, pulled toward the crest cream)
  Surface / cards:   #FFFFFF
  Soft surface:      #FDF9DC  (pale wash of the crest cream, for tinted areas)
  Primary text:      #1A1208  (warm near-black, matching the crest outlines)
  Secondary text:    #5C5344  (warm grey-brown)
  Primary action:    #056839  brand green, WHITE text
  Secondary action:  #F7941D  brand orange, DARK #1A1208 text
  Back / cancel:     white fill, #1A1208 text, 2px #E5DCC8 border
  Border:            #E5DCC8  1px
  Focus ring:        #056839  3px, 2px offset
  Accent / decor:    #ED1B24  brand red — shapes and rules only, never text

Class accent palette (card backgrounds, always with WHITE text on them).
These are darkened members of the crest's own warm family, so the page stays
recognisably the school's without failing contrast:
  #C41118  deep crest red
  #9E2B0E  brick
  #A85200  deep crest orange
  #8A6100  ochre
  #056839  crest green
  #2F6B12  leaf green
  #0A6265  teal
  #7A4A21  brown
Every one passes 4.5:1 against white text (verified: 5.4 to 7.5).
Do not lighten, tint, or add opacity to them — the contrast depends on the
exact values. Do not substitute the raw brand orange or red for any of them.

Overall the page should read as warm, sunny and slightly earthy — orange and
cream dominant, green for action, red used sparingly as a spark. No blues,
no purples, no cool greys anywhere.

LOGO
The school crest appears ONCE per screen, on the left of the header, at 56px on
the landing page and 36px on the compact top bar of the inner screens. Never
enlarge it beyond 56px, never centre it, never recolour it, never use it as a
background pattern or watermark. The crest is dense with fine detail and black
outlines — above ~72px it visually overpowers the pupil name cards, which must
stay the focus.

HEADER AND FOOTER
The landing page has a full 88px header: crest at 56px on the left, then the
school name "SK Seksyen 24 Shah Alam" at 20px bold with the motto "Berilmu
Berdisiplin" beneath it at 14px warm grey. On the right, a BM/EN toggle and a
"Guru" button. The header is closed by a 3px #ED1B24 rule.

The inner screens do NOT get this header — they keep a compact 80px top bar
(Kembali on the left, class pill centred, crest at 36px on the right). Their
card grid needs the vertical space and an 88px header would break it.

Every screen has a quiet 64px footer with no primary actions: roster date on
the left, "Masalah? Panggil cikgu." on the right.

TYPOGRAPHY
Rounded, friendly sans-serif (Nunito, Quicksand, or Baloo 2).
  Screen heading   40px  bold
  Class name       28px  bold
  Pupil name       18px  bold      <- the largest thing on a name card
  Button label     20px  bold
  Helper text      16px  regular
Never use text below 16px anywhere. Never use ALL CAPS.
Line height 1.4 minimum. Never truncate a name with an ellipsis — wrap to two
lines instead.

SHAPE & SPACING
Corner radius 20px on large cards, 16px on small cards and buttons.
Soft shadow: 0 2px 8px rgba(0,0,0,0.08).
Spacing scale: 8 / 16 / 24 / 32 / 48px.

TOUCH TARGETS — non-negotiable
Every interactive element is at least 48x48px. These users are 7 years old with
unsteady hands on an unfamiliar mouse.
Two card sizes exist and they are very different — do not average them:
  - Class-level cards and buttons: large, 240x180px or bigger, 24px gaps.
  - Pupil name cards on screen 2: SMALL, 137-179px wide by 99px tall with 10px
    gaps, because a whole class (30-44) must fit one 1366x768 screen without
    scrolling. Card height is constant; only width changes with class size.

LANGUAGE
All interface text is Bahasa Melayu, written for a 7-year-old.
Never use the words: SSO, portal, autentikasi, sesi, log masuk tunggal.
Use instead: masuk, kelas, nama, kata laluan.
Back buttons are labelled "Kembali" with an arrow icon — never a bare arrow.

LAYOUT
Design for a 1366x768 desktop school lab PC first. Content centred, max width
1200px. Must also work at 1024x768. Keep the primary action above the fold.

ACCESSIBILITY
All text meets WCAG 2.2 AA contrast (4.5:1 minimum).
Never use colour alone to carry meaning — always pair it with text or an icon.
Every card shows a clear focus ring for keyboard users.
```

---

## 3. Screen Prompts

### Prompt A — Generate the whole flow (start here)

```
Using the design system above, generate 3 connected screens for a school
computer-lab sign-in helper called "DELIMa Quick Sign-In".

SCREEN 1 — Landing page, TWO COLUMNS
A two-column layout with a 48px gutter, content max-width 1200px, centred.
The school has 6 tahun (year groups), each with 8 or 9 classes — up to 54 in
total. They are chosen with two dropdowns. Do NOT put class cards on screen 1.

LEFT COLUMN (about 45% width) — the form:
  - Heading, 40px bold, left-aligned: "Pilih Kelas Anda"
  - A "Kelas Terakhir" shortcut card at the top: 100px tall, full column width,
    filled #C41118 with white text reading "Tahun 2 Cemerlang", with a small
    16px label "Kelas Terakhir" above it. Then a divider.
  - Dropdown 1, labelled "Tahun" in 16px above it: full column width, 72px
    tall, white fill, 1px #E5DCC8 border, 16px radius, showing "Tahun 2" in
    28px bold dark text with a large chevron on the right.
  - Dropdown 2, labelled "Kelas": identical size, but shown in its DISABLED
    state — #FDF9DC fill, muted #5C5344 placeholder text "Pilih tahun dahulu",
    faded chevron. Some tahun have 8 classes and some have 9, so this list is
    variable-length; when it is open and longer than 8 rows the panel scrolls
    internally rather than growing off the bottom of the screen.
  - A primary button, full column width, 64px tall, filled #056839 with white
    28px bold text: "Teruskan →"
  Also draw ONE dropdown in its OPEN state as a second variation: a panel
  listing six rows "Tahun 1" to "Tahun 6", each row 64px tall with 20px text
  and a 24px round colour swatch on the left. The selected row has a #FDF9DC
  background and a 4px #056839 bar on its left edge. Draw a slim scrollbar on
  the panel to show it handles a 9-item list.
  These are CUSTOM dropdowns, not native browser selects. Rows must be large
  and finger-friendly. Do not draw a small OS-style select popup.

RIGHT COLUMN (about 55% width) — an information panel explaining the page.
This is for ADULTS: teachers, parents and administrators. It is calm, quiet
and contains no controls.
  - A rounded 20px panel filled #FDF9DC with 40px padding.
  - Heading, 28px bold: "Apa itu laman ini?"
  - One line of 18px body text: "Masuk ke DELIMa dan Google Classroom tanpa
    perlu menaip alamat e-mel yang panjang."
  - Three numbered steps, stacked, each with a 48px circular numbered badge on
    the left in #F7941D with dark text, and 18px label text on the right:
      1. Pilih kelas anda
      2. Cari nama anda
      3. Pilih DELIMa atau Google Classroom
  - Below them, a reassurance block on white with a 4px #056839 left bar,
    16px text: "Laman ini tidak menyimpan kata laluan. Anda akan menaip kata
    laluan di halaman rasmi Google."
  - At the bottom, the DELIMa and Google Classroom logos side by side at 48px.

HEADER, full width, 88px tall, white, sitting above both columns and closed by
a 3px #ED1B24 rule. On the left: the school crest at 56px, then a two-line
lockup — "SK Seksyen 24 Shah Alam" at 20px bold with "Berilmu Berdisiplin"
below it at 14px in warm grey #5C5344. On the right: a small BM/EN segmented
language toggle and an outlined "Guru" button, both 48px tall.

FOOTER, full width, 64px, very quiet, separated by a 1px #E5DCC8 line. On the
left "Senarai dikemas kini 6 Ogos 2026" in 14px grey; on the right "Masalah?
Panggil cikgu." in 14px grey. No buttons, no links, nothing tappable — a pupil
should never hit anything by reaching for the bottom of the page.

At the bottom, a quiet footer bar with, on the left, the school crest at 56px
tall beside the school name "SK Seksyen 24 Shah Alam" in 16px warm grey, and
on the right a small outlined text button labelled "Guru" and the text
"Senarai dikemas kini 6 Ogos 2026".
The footer must be visually much quieter than the cards. It is for adults.
Above the footer, a thin 3px rule in #ED1B24 spanning the content width — the
only place the bright crest red appears on this screen.

SCREEN 2 — "Cari Nama Anda"
Compact 80px top bar — NOT the tall landing-page header. A "Kembali" button on
the far left (white background, 20px bold text, left arrow icon, 48px tall),
the class name "Tahun 2 Cemerlang" centred as a coloured pill badge in that
class's accent colour with white text, and the school crest at 36px on the far
right. Include the tahun in the badge so a pupil who picked the wrong year sees
it immediately.
Do not add the tall header to this screen — the card grid needs the height.
Heading below: "Cari Nama Anda" (36px bold, centred).
A search input, 480px wide, centred, 44px tall, with placeholder "Taip nama..."
and a magnifier icon. Style it as SECONDARY — thin border, no fill, visually
quieter than the cards below. It is for the teacher, not the pupil.

Below: a DENSE grid of pupil cards. Classes hold 30 to 44 pupils, and the
WHOLE CLASS plus one extra card must fit on one 1366x768 screen with NO
SCROLLING — this is a hard requirement. Rows are always 5; the column count
follows the class size. Card height is always 99px; only the width changes.

Draw the LARGEST case: 44 pupils plus one extra card = 45 items, 9 columns x
5 rows, each card 137x99px with 10px gaps, centred.

Each pupil card is a WHITE card, 16px radius, 1px #E5DCC8 border, containing:
  - a small flat-illustration animal avatar, 40x40px, centred at the top in a
    soft circular #FDF9DC background
  - the pupil's name below it, 18px bold, dark #1A1208, centred, wrapping to a
    maximum of two lines
At this width a card fits about 14 characters per line, so use short calling
names: Aishah, Danial, Siti, Wei Ming, Arjun, Iman, Haziq, Mei Ling, Farhan,
Zara, Amirul, Hana, Ravi, Syafiq, Nabila, Aqil, Puteri, Irfan, Alia, and so on.
Where two pupils share a calling name, disambiguate with an initial — "Nur
Aishah A." and "Nur Aishah O." No two cards may read alike.

The FINAL card in the grid is different and must be drawn: an OUTLINED card
(no fill, 2px dashed #E5DCC8 border) with a question-mark icon instead of an
animal avatar and the label "Nama saya tiada" in 16px. It is the escape hatch
for a pupil whose name is missing from the list.

The grid should look calm and orderly, like a wall of laminated name cards in
a classroom, not like a cramped data table.

SCREEN 3 — "Pergi ke Mana?"
Same "Kembali" top bar.
Centred at the top, the selected pupil: their avatar at 120x120px, their FULL
name "Nur Aishah Binti Ahmad" at 32px bold below it (the full name appears
here, not on the small cards of screen 2 — this is the identity check before
handoff), and beneath that in 16px grey the MASKED email
"m-1234••••@moe-dl.edu.my". The mask dots are essential —
never show a full email address.
Heading below: "Hai, Aishah! Nak pergi ke mana?" (32px bold, centred).
Then TWO large destination buttons stacked vertically, each 480px wide and
120px tall, 24px apart, centred, each containing a product logo on the left
(64px) and a label on the right in 28px bold:
  - "DELIMa 3.0" — filled #056839 brand green with WHITE text
  - "Google Classroom" — filled #F7941D brand orange with DARK #1A1208 text
Both buttons carry equal visual weight and identical size. The colour split is
so a pre-reader can tell them apart at a glance, not to rank them.
Below the two buttons, a small quiet text link, #5C5344, 16px: "Bukan saya"

The three screens should feel like one continuous product. Keep the heading
position, the back button position, and the card rhythm identical across them.
```

### Prompt B — Teacher panel (generate separately)

Generate this **after** the pupil flow is settled. It is a different audience and a different visual register, and mixing it into Prompt A tends to drag the pupil screens toward looking administrative.

```
Using the same design system, generate 2 screens for the teacher/admin area of
the same app. These are for ADULTS, so they can be denser and more utilitarian
— but keep the same colours, typography and rounded shapes so it clearly
belongs to the same product.

SCREEN 4 — PIN entry
A centred modal card, 420px wide, on a dimmed overlay of the class grid.
Title "Mod Guru" (28px bold). Helper text "Masukkan PIN 4 angka" (16px grey).
Four large square PIN input boxes, 64x64px, 16px apart, centred.
Below them a numeric keypad, 3 columns of 72x72px round buttons (1-9, then a
clear button, 0, and a backspace).
A text button "Batal" at the bottom.

SCREEN 5 — Teacher settings
A page with a "Kembali" top bar and the heading "Tetapan Guru" (40px bold).
Below, four grouped setting cards stacked vertically, each 800px wide, white,
20px radius, 24px padding, with a bold label, a short grey description, and a
control on the right:
  1. "Destinasi Lalai" — description "Terus pergi ke laman ini selepas pilih
     nama" — segmented control with three options: Tiada / DELIMa / Classroom
  2. "Kelas Lalai" — description "Kelas yang ditunjuk dahulu di skrin utama"
     — dropdown showing "2 Cemerlang"
  3. "Bahasa" — segmented control: Bahasa Melayu / English
  4. "Tukar PIN" — a right-pointing chevron
Below the cards, two full-width outlined buttons, 56px tall:
  "Cetak Senarai Kelas" and "Muat Semula Senarai Pelajar"
At the very bottom, small grey 14px text: "Versi senarai: 6 Ogos 2026"
```

### Prompt C — States and edge cases

Design these explicitly. If you skip them, they get invented badly during implementation, and the error state is the one a stressed teacher sees in front of 30 children.

```
Using the same design system, generate 4 small state screens:

1. EMPTY SEARCH — the pupil grid area showing a friendly centred illustration
   with the text "Tiada nama dijumpai" (24px bold) and below it
   "Cuba taip nama lain, atau tekan Kembali" (16px grey).

2. OFFLINE — the class grid, unchanged and fully usable, with a slim
   non-alarming banner pinned to the top of the page using the brand orange
   #F7941D as background with DARK #1A1208 text (48px tall, 16px text) reading
   "Tiada internet — nama masih boleh dipilih". It must look like information,
   not an error. The pupil can still complete their task.

3. ERROR — a centred card, 480px wide, with a simple sad-cloud illustration,
   heading "Alamak, ada masalah." (28px bold) and one single instruction
   below in 20px: "Panggil cikgu." Include one primary button "Cuba Lagi".
   No error codes, no technical language, nothing in English.

4. LOADING — screen 1 with the Tahun dropdown already drawn and 8 skeleton
   placeholder cards below it in the soft crest cream (#FDF9DC), same
   240x180px size and 24px gaps as the real cards, with a gentle shimmer.
   No spinner.
```

---

## 4. Refinement Prompts

Stitch's first output will be reasonable and wrong in predictable ways. These are the corrections you will most likely need — apply them one at a time, not in a batch, so you can see what each one did.

| Problem you'll see | Paste this |
| :--- | :--- |
| Cards too small / too dense | `Make every class card and pupil card at least 200px wide and 180px tall, and increase the gap between cards to 32px. These users are 7 years old with unsteady hands — err heavily toward too large.` |
| Name text too small | `Increase the pupil name on each card to 26px bold. The name is the single most important element on the card — it should be the first thing the eye lands on, larger than the avatar feels. Never truncate it; wrap to two lines and let the card grow taller.` |
| Search bar dominating | `Make the search input visually quieter and secondary: thin 1px border, no fill, no shadow, 60% width. It is a tool for the teacher, not the pupil. The pupil cards must be the obvious focus of the screen.` |
| Looks like a dashboard | `Remove all dashboard characteristics: no sidebars, no stat tiles, no breadcrumbs, no dense headers, no icon-only toolbars. This should feel like a page from a children's picture book — one heading, one grid, lots of warm empty space.` |
| Pale washed-out colours | `The class card colours are too pale. Use the accent palette exactly as specified with WHITE text on top. Do not tint, lighten, or add opacity to them — the contrast requirement depends on the exact values.` |
| **White text on the brand orange** (most likely failure) | `Never place white text on #F7941D. It fails contrast at 2.3:1 and is unreadable. On brand orange, text is always dark #1A1208 — exactly as the school crest puts black lettering on its orange ribbon. If a surface needs white text, use the brand green #056839 instead.` |
| Text sitting on the brand red | `Remove all text from #ED1B24 surfaces. The bright crest red carries no text in either direction — it fails against both white and dark. Use it only for icon shapes, illustrations, thin rules and selected-state borders. For a red surface that needs a label, use #C41118 with white text.` |
| Blues or purples appearing | `Remove every blue, purple and cool grey. The palette is strictly the school crest family: warm orange, cream, deep green, and red as a spark. Greys must be warm (#5C5344), never cool.` |
| Crest too large or centred | `Keep the school crest at 56px on the left of the landing-page header, and 36px on the right of the inner top bars. It is dense with fine detail and must never compete with the pupil name cards. Do not recolour it, do not use it as a background watermark.` |
| **Tall header added to screen 2 or 3** | `Remove the tall header from this screen. Inner screens use a compact 80px top bar only — Kembali left, class pill centred, 36px crest right. An 88px header would shrink the card rows from 99px to 81px and break the one-screen grid.` |
| Footer carries buttons | `The footer is informational only: roster date on the left, "Masalah? Panggil cikgu." on the right, 14px grey, nothing tappable. Move the Guru button up into the header.` |
| Class cards appearing on screen 1 | `Screen 1 has no class cards. Classes are chosen with two dropdowns, Tahun then Kelas, in the left column. The only card on screen 1 is the single "Kelas Terakhir" shortcut. Remove any class grid.` |
| Native-looking select popup | `Redraw both dropdowns as large custom controls, not native browser selects. Rows are 64px tall with 20px text and a colour swatch. The default OS select popup has rows under 20px tall and is unusable for these users.` |
| Right column looks interactive | `Make the right-hand information panel clearly non-interactive: no buttons, no links, no input fields, flat #FDF9DC surface. It explains the page to adults. The only controls on screen 1 are the two dropdowns and the Teruskan button in the left column.` |
| Columns unbalanced or stacked | `Restore the two-column layout on screen 1: form on the left at about 45% width, information panel on the right at about 55%, 48px gutter, both starting at the same top edge.` |
| **Too few pupil cards** | `Screen 2 must show 45 cards — 44 pupils plus one outlined "Nama saya tiada" card — in a 9 column by 5 row grid, all visible on one 1366x768 screen with no scrolling. Do not reduce the count and do not add a scrollbar.` |
| Pupil cards too big | `Shrink the pupil cards to 137x99px with 10px gaps. They are deliberately small — a whole class must fit one screen. Avatar 40px, name 18px bold on up to two lines. This is the one place in the design where cards are small.` |
| Full names on pupil cards | `Pupil cards show the GIVEN NAME ONLY — "Aishah", not "Nur Aishah Binti Ahmad". There is no room for full names at 136px wide. The full name appears on screen 3 instead.` |
| Name grid looks like a table | `Keep the 45-card grid feeling like a wall of laminated classroom name cards: white rounded cards, soft borders, even gaps, calm rhythm. Not a spreadsheet, not a data table, no row striping, no grid lines.` |
| Full email visible | `The email address must always be masked as "m-1234••••@moe-dl.edu.my". Never display a full pupil email anywhere in the interface, on any screen, at any size.` |
| Generic stock avatars | `Replace the avatars with flat, friendly, single-colour illustrated animals — cat, rabbit, elephant, fish, bird, tiger, turtle, bee. Simple bold shapes, no gradients, no photorealism, no 3D. Each sits inside a soft circular tinted background.` |
| English creeping in | `Every visible string must be Bahasa Melayu. Replace any English UI text. Keep only the proper product names "DELIMa" and "Google Classroom" untranslated.` |
| Screens feel unrelated | `Make all screens share identical structure: heading in the same position, Kembali button always top-left at the same coordinates, same card grid rhythm, same footer treatment. A pupil should never have to relearn where anything is.` |
| Too much on screen 3 | `Simplify screen 3 to exactly four elements: the pupil's avatar and name, the masked email, two large destination buttons, and one small "Bukan saya" link. Remove everything else. This screen has one job.` |

---

## 5. Working With Stitch Effectively

**Generate the flow before polishing any single screen.** Screens generated together share visual logic. Screens generated separately drift, and reconciling them costs more than starting over.

**Change one thing per iteration.** Batched corrections make it impossible to tell which instruction caused a regression, and Stitch will sometimes trade one fix for another.

**Reference the design system explicitly** in follow-ups — "using the design system above" — or it decays over a long session.

**Feed it a wireframe if you have one.** Stitch accepts sketches and screenshots as input, and a rough hand-drawn layout constrains it far more reliably than another paragraph of prose.

**Keep the generations you like.** Stitch will happily regress a good screen while fixing a bad one. Export or screenshot anything you're happy with before the next iteration.

**Don't ask it to design the Google sign-in page.** That screen is Google's and must remain untouched and unmistakably theirs — that is precisely what keeps this product inside Google's Terms of Service, and what makes the password prompt look trustworthy to a child who has seen it before. Your design ends the moment the browser navigates away.

---

## 6. Pre-Export Checklist

Run this against every screen before you export. Items marked **blocker** must pass — they are the ones that fail in a real lab with real children, and they are the ones the PRD commits to.

### Blockers

- [ ] Every interactive element is ≥ 48 × 48 px — measure, don't eyeball
- [ ] Class-level cards and buttons ≥ 240 × 180 px; pupil cards 137–179 × 99 px
- [ ] Screen 1 is **two columns** — form left, information panel right
- [ ] Screen 1 has **no class grid**; classes come from the Tahun + Kelas dropdowns
- [ ] Dropdown rows are ≥ 48 px tall and clearly custom, not a native `<select>`
- [ ] Screen 2 shows **45 cards (44 pupils + escape hatch), 9 × 5, no scrollbar** at 1366 × 768
- [ ] The final card is the outlined **"Nama saya tiada"** escape hatch
- [ ] Pupil names fit two lines at the card width — no ellipsis truncation
- [ ] No two pupil cards read identically
- [ ] No text anywhere below 16px
- [ ] Pupil names are the largest element on their card, never truncated
- [ ] **No full email address appears on any screen** — masked everywhere
- [ ] All text passes 4.5:1 contrast — check every class colour individually
- [ ] **No white text anywhere on `#F7941D`** — the single most likely failure, and it fails hard at 2.3:1
- [ ] **No text of any colour on `#ED1B24`** — bright crest red fails both ways
- [ ] No blues, purples or cool greys have crept in
- [ ] School crest in the **header** — 56 px on screen 1, 36 px on screens 2–3, unrecoloured
- [ ] Screens 2 and 3 use the **compact 80 px top bar**, not the tall header
- [ ] Footer is informational only — no buttons, nothing tappable
- [ ] Every visible string is Bahasa Melayu (except "DELIMa" and "Google Classroom")
- [ ] The word SSO, portal, autentikasi or sesi appears nowhere
- [ ] "Kembali" is labelled with text, not a bare arrow icon
- [ ] Screen 3 offers exactly two destinations — **DELIMa 3.0** and Google Classroom — plus "Bukan saya"
- [ ] Screen 3 shows the pupil's **full** name (the only screen that does)
- [ ] Error state says only "Alamak, ada masalah. / Panggil cikgu." — no codes, no English

### Should pass

- [ ] Layout works at 1366×768 and 1024×768 without horizontal scroll
- [ ] Primary action visible above the fold on both sizes
- [ ] Back button in the identical position on every screen
- [ ] Footer and teacher affordances visually quieter than pupil affordances
- [ ] Focus rings present and clearly visible on all cards
- [ ] Offline banner reads as information, not alarm
- [ ] Nothing on screen resembles a dashboard

### Sanity check — do this one for real

- [ ] Show screen 2 to someone for three seconds, then ask what they'd tap. If the answer isn't "my name," the hierarchy is wrong.
- [ ] Open screen 2 at exactly 1366 × 768 and confirm no scrollbar appears. This is the requirement most likely to be quietly missed.
- [ ] Read every string aloud as if to a 7-year-old. Anything you'd have to explain gets rewritten.

---

## 7. Export & Handoff

Stitch exports to Figma and to HTML/CSS. Both are useful; neither is the final artefact.

**Recommended path:** export HTML/CSS as a **visual reference**, and rebuild in the project's own stack per the architecture document. Take the Figma export too — it's the durable record for future roster or destination changes.

Stitch output must be treated as a design comp, not a deliverable, for reasons the architecture document makes non-negotiable:

| Stitch gives you | What the build requires |
| :--- | :--- |
| Static markup | Data-driven rendering from `roster.json` |
| Absolute or fixed positioning | Fluid grid that survives 40 pupils and long names |
| Inline styles | CSS custom properties per §3 of the architecture doc |
| Possible external font/image links | Everything self-hosted — the CSP forbids external origins |
| No routing | Hash routing, `student_id` only, never an email in the URL |
| No service worker | Offline-first precache |
| Decorative-only accessibility | Real semantics: `<button>`, ARIA labels in Bahasa Melayu, keyboard order |
| No press states, transitions, or motion | Interaction & Motion spec — §8 below |

Two things to carry across carefully: the **exact colour and spacing tokens** into `styles/tokens.css`, and the **exact Bahasa Melayu strings** into `i18n/ms.json`. Those are the parts of the Stitch output with real, transferable value. The markup is scaffolding.

**One hard rule at handoff:** whatever the export contains, no password field, no credential input, and no email address ever reaches the deployed markup. If Stitch invents a password box — it may, since it has seen ten thousand login screens — delete it. This product's entire compliance position rests on that absence.

---

## 8. Interaction & Motion (For the Build)

Stitch generates static comps — no press states, no transitions, no motion. These aren't optional polish for this audience: misclicks are a named risk (§1), and pupils need to *see* a tap land before they've finished making it. Spec this now so it doesn't get invented ad hoc during implementation, the same argument §3C already makes for empty/offline/error states.

- **Press feedback fires on pointer-down, not on release.** Class and pupil cards highlight and scale the instant the pointer contacts them — `transform: scale(0.97)`, ~150ms `ease-out` — not after `click` resolves. Waiting for release reads as unresponsive, and these are already the users most likely to miss their target.
- **Kembali reverses the exact path forward took.** If Screen 1 → 2 shifts content left / fades in from the right, Kembali mirrors that exactly (right / fades from the left) rather than using an unrelated transition. This reinforces the three-step mental model (§1) without needing text.
- **Respect `prefers-reduced-motion`.** All card and screen transitions fall back to a plain opacity crossfade — no slides, no scale — under reduced motion. Treat this as the same hard bar as the WCAG contrast blockers in §6, not optional polish.
- **No translucency or blur anywhere.** Flat opaque surfaces only. `backdrop-filter` is GPU-expensive, and this product's own constraint is "aging lab PCs" (§1). Don't let a later pass "upgrade" the footer, modal, or PIN sheet to glass.
- **Card grid entrance:** stagger fade + `translateY(8px)→0`, ~200ms `ease-out`, ~40ms stagger between cards. Skip entirely under reduced motion. Never blocks interaction — a pupil can tap before the stagger finishes.
- **Loading skeleton shimmer** (§3 Prompt C #4) uses `linear` easing on a looping sweep, ~1.5s. Constant, repeating motion should never ease.
- **Offline banner** enters and exits by sliding from the top, ~200ms `ease-out` — never snaps in or out.
- **Keep the existing wins.** "Bukan saya" (Screen 3) as an ungated escape hatch, and the persistent Kembali position plus class pill badge for wayfinding, are already right. Don't lose either during further Stitch iteration.

---

## Appendix A — Colour Tokens

Sampled directly from the school crest. Copy into `styles/tokens.css`. Contrast figures are computed, not estimated.

```css
:root {
  /* Brand — exact crest values */
  --brand-orange:  #F7941D;  /* dark text only — 2.28:1 vs white, FAILS */
  --brand-red:     #ED1B24;  /* no text either way — 4.39 / 4.22, FAILS */
  --brand-green:   #056839;  /* white text safe — 6.90:1 */
  --brand-cream:   #FAF7B2;  /* dark text — 16.77:1 */

  /* Surfaces */
  --bg:            #FFFDF7;
  --surface:       #FFFFFF;
  --surface-soft:  #FDF9DC;
  --border:        #E5DCC8;

  /* Text */
  --text:          #1A1208;  /* 18.21:1 on --bg */
  --text-muted:    #5C5344;  /*  7.44:1 on --bg */
  --text-on-green: #FFFFFF;
  --text-on-orange:#1A1208;  /*  8.12:1 */

  /* Class accents — all pass ≥4.5:1 with white text */
  --class-1: #C41118;  /* 6.10 */
  --class-2: #9E2B0E;  /* 7.48 */
  --class-3: #A85200;  /* 5.42 */
  --class-4: #8A6100;  /* 5.54 */
  --class-5: #056839;  /* 6.90 */
  --class-6: #2F6B12;  /* 6.50 */
  --class-7: #0A6265;  /* 7.12 */
  --class-8: #7A4A21;  /* 7.42 */

  --focus: #056839;    /* 6.78:1 on --bg */
}
```

### Why the palette is not simply the crest's three colours

The crest is built for print at large sizes with heavy black outlines separating every field. On screen, at small sizes, without those outlines, two of its three colours cannot carry text:

| Crest colour | vs white | vs dark ink | Usable as |
| :--- | ---: | ---: | :--- |
| Oren `#F7941D` | 2.28 ✗ | 8.12 ✓ | Surface for **dark** text — as the crest's own ribbon does |
| Merah `#ED1B24` | 4.39 ✗ | 4.22 ✗ | **Fill and accent only** — no text |
| Hijau `#056839` | 6.90 ✓ | 2.69 ✗ | Surface for **white** text; primary action |
| Krim `#FAF7B2` | 1.10 ✗ | 16.77 ✓ | Soft surface for dark text |

The eight class accents are darkened members of the same warm family, so the page still reads as this school's while every card passes AA. The bright crest orange and red remain visible where they work — orange on secondary buttons and the offline banner with dark text, red as the footer rule and in illustration.

---

## Appendix B — String Sheet

Copy these verbatim. They're already written for the reading level.

| Key | Bahasa Melayu | English |
| :--- | :--- | :--- |
| `header.motto` | Berilmu Berdisiplin | *(school motto — leave untranslated)* |
| `footer.help` | Masalah? Panggil cikgu. | Problem? Ask your teacher. |
| `screen1.title` | Pilih Kelas Anda | Choose Your Class |
| `screen1.about` | Apa itu laman ini? | What is this page? |
| `screen1.aboutBody` | Masuk ke DELIMa dan Google Classroom tanpa perlu menaip alamat e-mel yang panjang. | Get into DELIMa and Google Classroom without typing a long email address. |
| `screen1.step1` | Pilih kelas anda | Choose your class |
| `screen1.step2` | Cari nama anda | Find your name |
| `screen1.step3` | Pilih DELIMa atau Google Classroom | Choose DELIMa or Google Classroom |
| `screen1.noPassword` | Laman ini tidak menyimpan kata laluan. Anda akan menaip kata laluan di halaman rasmi Google. | This page does not store passwords. You will type your password on Google's official page. |
| `screen1.classLabel` | Kelas | Class |
| `screen1.classPlaceholder` | Pilih tahun dahulu | Choose a year first |
| `screen1.continue` | Teruskan | Continue |
| `screen1.yearLabel` | Tahun | Year |
| `screen1.yearOption` | Tahun {n} | Year {n} |
| `screen1.recent` | Kelas Terakhir | Last Class |
| `screen1.updated` | Senarai dikemas kini {date} | Roster updated {date} |
| `screen2.title` | Cari Nama Anda | Find Your Name |
| `screen2.search` | Taip nama... | Type a name... |
| `screen2.empty` | Tiada nama dijumpai | No names found |
| `screen2.emptyHelp` | Cuba taip nama lain, atau tekan Kembali | Try another name, or press Back |
| `screen3.greeting` | Hai, {name}! Nak pergi ke mana? | Hi, {name}! Where do you want to go? |
| `screen3.notMe` | Bukan saya | Not me |
| `nav.back` | Kembali | Back |
| `nav.teacher` | Guru | Teacher |
| `offline.banner` | Tiada internet — nama masih boleh dipilih | No internet — you can still pick a name |
| `error.title` | Alamak, ada masalah. | Something went wrong. |
| `error.help` | Panggil cikgu. | Call your teacher. |
| `error.retry` | Cuba Lagi | Try Again |
| `teacher.title` | Mod Guru | Teacher Mode |
| `teacher.pin` | Masukkan PIN 4 angka | Enter 4-digit PIN |
| `teacher.settings` | Tetapan Guru | Teacher Settings |
| `teacher.destination` | Destinasi Lalai | Default Destination |
| `teacher.defaultClass` | Kelas Lalai | Default Class |
| `teacher.language` | Bahasa | Language |
| `teacher.changePin` | Tukar PIN | Change PIN |
| `teacher.print` | Cetak Senarai Kelas | Print Class List |
| `teacher.refresh` | Muat Semula Senarai Pelajar | Refresh Pupil List |
| `common.cancel` | Batal | Cancel |
| `common.none` | Tiada | None |

---

**References:**

- [Stitch — Google Labs](https://stitch.withgoogle.com)
- [Design UI using AI with Stitch — Google blog](https://blog.google/innovation-and-ai/models-and-research/google-labs/stitch-ai-ui-design/)
- [Google Stitch AI Design Tool: Features & Updates 2026 — UXPin](https://www.uxpin.com/studio/blog/google-stitch-ai-design-tool-updates-ui-ux/)
