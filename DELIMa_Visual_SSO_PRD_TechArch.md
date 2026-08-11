# Product Requirement Document (PRD)

## Project Title
**DELIMa Smart Launcher for Primary Schools (Visual SSO Desktop App)**

---

## 1. Executive Summary & Problem Statement
* **Problem:** 2,000 primary school pupils (*Tahap 1*) struggle with typing their lengthy Ministry of Education DELIMa emails (`m-XXXXXXXX@moe-dl.edu.my`) and passwords on computer lab Windows PCs. Manual login consumes 15–20 minutes of class time.
* **Solution:** A desktop Windows application installed on lab PCs. Pupils select their class, tap their avatar card, and the app triggers native Chrome execution with Google's official `login_hint` parameter, followed by secure OS-level password injection.
* **Goal:** 1-tap login experience for pupils in **< 5 seconds**, complying 100% with Google’s Terms of Service (no embedded WebViews or blocked DOM injections).

---

## 2. Technical Stack & Dependencies

| Layer | Recommended Technology | Purpose |
| :--- | :--- | :--- |
| **Desktop UI & OS Core** | C# (.NET 8 WPF) or Python (PyQt6) | Native Windows application running in borderless / kiosk mode. |
| **Backend / Database** | Firebase Firestore | Central cloud database storing 2,000 student profiles. |
| **Authentication Flow** | Google Chrome Process + Windows `SendKeys` | Triggers real `chrome.exe` with `login_hint` URL parameter. |
| **Data Sync** | CSV Upload Utility | Allows admin/teachers to bulk-import APDM / DELIMa rosters into Firestore. |

---

## 3. Database Schema (Firebase Firestore)

### Collection: `classes`
* Document ID: `class_id` (e.g., `2_cemerlang`)
* Fields:
  * `class_name`: `string` ("2 Cemerlang")
  * `grade`: `number` (2)

### Collection: `students`
* Document ID: `student_id` (e.g., `std_001`)
* Fields:
  * `full_name`: `string` ("Nur Aishah Binti Ahmad")
  * `class_id`: `string` ("2_cemerlang")
  * `delima_email`: `string` ("m-12345678@moe-dl.edu.my")
  * `encrypted_password`: `string` (AES-256 encrypted password string)
  * `avatar_id`: `string` ("cat_icon")

---

## 4. User Flow & UI Requirements
┌─────────────────┐      ┌─────────────────────┐      ┌─────────────────────────┐
│  Screen 1:      │ ───► │  Screen 2:          │ ───► │  Automated Trigger:     │
│  Class Selector │      │  Student Card Grid  │      │  Chrome + Password Auto │
└─────────────────┘      └─────────────────────┘      └─────────────────────────┘

### Screen 1: Class Selection (Home)
* Display a clean, colorful grid of all classes fetched from Firestore (e.g., *1 Cemerlang*, *2 Dinamik*, *3 Bestari*).
* Include a **Teacher Mode** unlock button (requires 4-digit PIN) for administrative tasks.

### Screen 2: Student Roster Grid
* When a class card is clicked, display student profile cards containing:
  * Pupil's Full Name
  * Assigned Avatar / Icon
* A search bar at the top for quick name filtering.

### Screen 3: Floating Reset Bar ("Selesai / Next Student")
* Once a student clicks their card, the main UI minimizes to a persistent, top-right floating toolbar.
* Contains a single prominent red button: **"🔴 Selesai (Log Out & Next Student)"**.
* Clicking this button executes `taskkill /F /IM chrome.exe` and restores Screen 1 for the next pupil.

---

## 5. Core System Logic & Execution Pipeline

When a pupil clicks their name card:

1. **Fetch & Decrypt Credential:**
   * Read `delima_email` and `encrypted_password` for the selected `student_id`.
   * Decrypt the password in-memory using the application's secret key.

2. **Construct Official Google SSO URL:**
   ```text
   [https://accounts.google.com/AccountChooser?Email=](https://accounts.google.com/AccountChooser?Email=){delima_email}&continue=[https://d3.delima.edu.my](https://d3.delima.edu.my)

3. Launch Native Chrome Process:

	C#
	Process.Start(@"C:\Program Files\Google\Chrome\Application\chrome.exe", googleUrl);
	Native OS Password Injection:

	Wait 1,500ms (configurable) for Google Chrome to load the focused password input field.

	Send native OS keystrokes to the active window:

	C#
	SendKeys.SendWait(decryptedPassword);
    SendKeys.SendWait("{ENTER}");

   
4. Non-Functional & Security Requirements
	Google Policy Compliance: Absolutely NO WebViews, Puppeteer, or Selenium DOM manipulation inside embedded frames. Must launch real system Chrome.

	Latency Target: Complete login execution from click to DELIMa portal load in < 5 seconds.

	Security:

	Student passwords must never be stored in plain text in Firestore or local log files.

	Local memory variables holding decrypted credentials must be wiped immediately after SendKeys execution.

	Offline Fallback: Cache the latest fetched student roster JSON locally on the PC so the app can render student names even during brief network drops.

5. Implementation Tasks for Antigravity Agents
  Task 1 (Database): Create a Node.js script to initialize Firebase Firestore schema and write a bulk-importer function for APDM student CSV files.
  
  Task 2 (Desktop UI): Scaffold a C# WPF (or PyQt6) borderless desktop app with two main views: ClassSelectionView and StudentRosterView.
  
  Task 3 (Launcher Logic): Implement the ProcessLauncher service using System.Diagnostics.Process to launch chrome.exe with login_hint parameters and handle SendKeys timing.
  
  Task 4 (Reset Widget): Create a topmost floating window utility that manages Chrome process termination (taskkill) and session resets.