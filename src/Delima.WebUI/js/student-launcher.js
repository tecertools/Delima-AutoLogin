/**
 * DELIMa Windows 11 Web UI — Student Launcher (Kiosk Portal) Controller
 */

class StudentLauncher {
  constructor(app) {
    this.app = app;
    this.currentView = "classes"; // "classes" | "students" | "picture-pass" | "destinations" | "signing-in"
    this.selectedClass = null;
    this.selectedStudent = null;
    this.enteredSymbols = [];
    this.studentSearchQuery = "";
    this.failedAttempts = 0;
  }

  init() {
    this.render();
  }

  render() {
    const container = document.getElementById("studentLauncherContainer");
    if (!container) return;

    switch (this.currentView) {
      case "classes":
        container.innerHTML = this.renderClassSelection();
        break;

      case "students":
        container.innerHTML = this.renderStudentSelection();
        this.bindStudentEvents();
        break;

      case "picture-pass":
        container.innerHTML = this.renderPicturePassword();
        break;

      case "destinations":
        container.innerHTML = this.renderDestinations();
        break;

      case "signing-in":
        container.innerHTML = this.renderSigningIn();
        break;
    }
  }

  /* SCREEN 1: PILIH KELAS */
  renderClassSelection() {
    const tahap1Classes = DelimaData.classes.filter(c => c.grade <= 3);
    const tahap2Classes = DelimaData.classes.filter(c => c.grade >= 4);

    return `
      <div class="kiosk-header">
        <div class="kiosk-brand">
          <div class="kiosk-crest" style="background:${DelimaData.school.crestBg}">
            ${DelimaData.school.crestText}
          </div>
          <div class="kiosk-school-info">
            <h2>${DelimaData.school.name}</h2>
            <p>Sila pilih kelas anda untuk memulakan sesi pembelajaran</p>
          </div>
        </div>
        <div class="kiosk-header-actions">
          <button class="btn btn-secondary" onclick="window.studentLauncher.openTeacherPinModal()">
            🔒 Mod Guru
          </button>
        </div>
      </div>

      <div class="class-select-container">
        <div class="class-section-header">
          <h2>Pilih Kelas Anda Hari Ini</h2>
          <p>Klik pada nama kelas anda di bawah</p>
        </div>

        <div class="grade-group">
          <div class="grade-title">🌱 Tahap 1 (Tahun 1, 2 & 3) — Log Masuk Simbol Gambar</div>
          <div class="class-card-grid">
            ${tahap1Classes.map(c => `
              <div class="class-card" onclick="window.studentLauncher.selectClass('${c.id}')">
                <div class="class-icon-badge">${c.icon}</div>
                <div class="class-name">${c.name}</div>
                <div class="class-count">${c.studentCount} Murid Berdaftar</div>
              </div>
            `).join("")}
          </div>
        </div>

        <div class="grade-group">
          <div class="grade-title">🚀 Tahap 2 (Tahun 4, 5 & 6)</div>
          <div class="class-card-grid">
            ${tahap2Classes.map(c => `
              <div class="class-card" onclick="window.studentLauncher.selectClass('${c.id}')">
                <div class="class-icon-badge">${c.icon}</div>
                <div class="class-name">${c.name}</div>
                <div class="class-count">${c.studentCount} Murid Berdaftar</div>
              </div>
            `).join("")}
          </div>
        </div>
      </div>
    `;
  }

  selectClass(classId) {
    this.selectedClass = DelimaData.classes.find(c => c.id === classId);
    this.currentView = "students";
    this.studentSearchQuery = "";
    this.render();
  }

  /* SCREEN 2: CARI NAMA MURID */
  renderStudentSelection() {
    const studentsInClass = DelimaData.students.filter(s => {
      const isClassMatch = s.classId === this.selectedClass.id;
      const isSearchMatch = s.name.toLowerCase().includes(this.studentSearchQuery.toLowerCase());
      return isClassMatch && isSearchMatch;
    });

    return `
      <div class="kiosk-header">
        <div class="kiosk-brand">
          <button class="btn btn-secondary" onclick="window.studentLauncher.backToClasses()">
            ← Tukar Kelas
          </button>
          <span class="class-current-pill">${this.selectedClass.name}</span>
        </div>
        <div class="kiosk-header-actions">
          <button class="btn btn-subtle" onclick="window.studentLauncher.openTeacherPinModal()">
            🔒 Bantuan Guru
          </button>
        </div>
      </div>

      <div class="student-grid-container">
        <div class="student-search-bar">
          <input type="text" id="studentSearchInput" class="input-text" placeholder="🔍 Cari nama anda di sini..." value="${this.studentSearchQuery}" autofocus>
        </div>

        <div class="student-grid">
          ${studentsInClass.map(s => `
            <div class="student-card" onclick="window.studentLauncher.selectStudent('${s.id}')">
              <div class="student-avatar">${s.avatar}</div>
              <div class="student-name">${s.name}</div>
            </div>
          `).join("")}
        </div>
      </div>
    `;
  }

  bindStudentEvents() {
    const input = document.getElementById("studentSearchInput");
    if (input) {
      input.addEventListener("input", (e) => {
        this.studentSearchQuery = e.target.value;
        const grid = document.querySelector(".student-grid");
        if (grid) {
          const studentsInClass = DelimaData.students.filter(s => {
            const isClassMatch = s.classId === this.selectedClass.id;
            const isSearchMatch = s.name.toLowerCase().includes(this.studentSearchQuery.toLowerCase());
            return isClassMatch && isSearchMatch;
          });
          grid.innerHTML = studentsInClass.map(s => `
            <div class="student-card" onclick="window.studentLauncher.selectStudent('${s.id}')">
              <div class="student-avatar">${s.avatar}</div>
              <div class="student-name">${s.name}</div>
            </div>
          `).join("");
        }
      });
    }
  }

  backToClasses() {
    this.currentView = "classes";
    this.selectedClass = null;
    this.render();
  }

  selectStudent(studentId) {
    this.selectedStudent = DelimaData.students.find(s => s.id === studentId);
    this.enteredSymbols = [];
    this.failedAttempts = 0;
    this.currentView = "picture-pass";
    this.render();
  }

  /* SCREEN 3: KATA LALUAN GAMBAR (3-SYMBOL UNLOCK) */
  renderPicturePassword() {
    return `
      <div class="kiosk-header">
        <div class="kiosk-brand">
          <button class="btn btn-secondary" onclick="window.studentLauncher.backToStudents()">
            ← Bukan Saya
          </button>
          <span class="class-current-pill">${this.selectedClass.name}</span>
        </div>
        <div class="kiosk-header-actions">
          <button class="btn btn-subtle" onclick="window.studentLauncher.openTeacherPinModal()">
            🔒 Bantuan Guru
          </button>
        </div>
      </div>

      <div class="picture-password-container">
        <div class="pupil-target-card">
          <div style="font-size:32px;">${this.selectedStudent.avatar}</div>
          <div style="text-align:left;">
            <div class="pupil-target-name">${this.selectedStudent.name}</div>
            <div class="pupil-target-email">${this.selectedStudent.email}</div>
          </div>
        </div>

        <h3 style="font-size:20px; font-weight:800; margin-bottom:6px;">Tekan 3 Simbol Gambar Rahsia Anda</h3>
        <p style="font-size:13px; color:var(--text-secondary); margin-bottom:18px;">Tekan simbol satu persatu mengikut turutan yang betul.</p>

        <div class="password-dots-wrap">
          <div class="password-dot ${this.enteredSymbols.length >= 1 ? 'filled' : ''}" id="dot0">
            ${this.enteredSymbols[0] || ''}
          </div>
          <div class="password-dot ${this.enteredSymbols.length >= 2 ? 'filled' : ''}" id="dot1">
            ${this.enteredSymbols[1] || ''}
          </div>
          <div class="password-dot ${this.enteredSymbols.length >= 3 ? 'filled' : ''}" id="dot2">
            ${this.enteredSymbols[2] || ''}
          </div>
        </div>

        <div class="symbol-grid">
          ${DelimaData.symbolsCatalog.map(sym => `
            <div class="symbol-tile" onclick="window.studentLauncher.tapSymbol('${sym.emoji}')">
              <div>${sym.emoji}</div>
              <div class="symbol-name">${sym.name}</div>
            </div>
          `).join("")}
        </div>

        <div class="keypad-actions">
          <button class="btn btn-secondary btn-lg" onclick="window.studentLauncher.clearSymbols()">
            ↺ Padam Semua
          </button>
          <button class="btn btn-subtle btn-lg" onclick="window.studentLauncher.showHint()">
            💡 Beri Petunjuk
          </button>
        </div>
      </div>
    `;
  }

  backToStudents() {
    this.currentView = "students";
    this.selectedStudent = null;
    this.enteredSymbols = [];
    this.render();
  }

  tapSymbol(emoji) {
    if (this.enteredSymbols.length >= 3) return;

    this.enteredSymbols.push(emoji);
    this.render();

    if (this.enteredSymbols.length === 3) {
      setTimeout(() => {
        this.verifySymbols();
      }, 300);
    }
  }

  clearSymbols() {
    this.enteredSymbols = [];
    this.render();
  }

  showHint() {
    if (!this.selectedStudent) return;
    // Show only the category of the first symbol, not the symbol itself,
    // so classmates looking at the screen cannot observe the answer.
    const firstSymbolId = DelimaData.symbolsCatalog.find(
      s => s.emoji === this.selectedStudent.passSymbols[0]
    );
    const hint = firstSymbolId ? `kategori '${firstSymbolId.category}'` : 'simbol pertama';
    this.app.showToast(`Petunjuk: Simbol pertama anda berada dalam ${hint}`, "info");
  }

  verifySymbols() {
    const isMatch = this.enteredSymbols.every((sym, idx) => sym === this.selectedStudent.passSymbols[idx]);

    if (isMatch) {
      this.app.showToast(`Kata laluan tepat! Selamat datang, ${this.selectedStudent.name.split(" ")[0]}!`, "success");
      this.currentView = "destinations";
      this.render();
    } else {
      this.failedAttempts++;
      const dots = document.querySelectorAll(".password-dot");
      dots.forEach(d => d.classList.add("error"));

      if (this.failedAttempts >= 3) {
        this.app.showToast("3 cubaan tidak berjaya. Sila minta bantuan guru.", "error");
      } else {
        this.app.showToast(`Simbol tidak tepat. (Cubaan ${this.failedAttempts}/3)`, "error");
      }

      setTimeout(() => {
        this.enteredSymbols = [];
        this.render();
      }, 700);
    }
  }

  /* SCREEN 4: DESTINASI */
  renderDestinations() {
    return `
      <div class="kiosk-header">
        <div class="kiosk-brand">
          <div class="kiosk-crest" style="background:${DelimaData.school.crestBg}">
            ${DelimaData.school.crestText}
          </div>
          <div class="kiosk-school-info">
            <h2>${DelimaData.school.name}</h2>
            <p>Sesi Pembelajaran Aktif</p>
          </div>
        </div>
        <div class="kiosk-header-actions">
          <button class="btn btn-danger btn-sm" onclick="window.studentLauncher.logout()">
            🚪 Keluar / Log Keluar
          </button>
        </div>
      </div>

      <div class="destination-container">
        <div class="dest-profile-badge">
          <div class="dest-avatar">${this.selectedStudent.avatar}</div>
          <div class="dest-student-name">${this.selectedStudent.name}</div>
          <div class="dest-student-email">${this.selectedStudent.email} • ${this.selectedClass.name}</div>
        </div>

        <h3 style="font-size:22px; font-weight:800; margin-bottom:8px;">Pilih Tempat Yang Anda Ingin Buka</h3>
        <p style="font-size:13px; color:var(--text-secondary); margin-bottom:28px;">Sistem akan memasukkan ID DELIMa dan kata laluan anda secara automatik.</p>

        <div class="destination-cards-grid">
          <div class="dest-card delima" onclick="window.studentLauncher.launchDestination('DELIMa 3.0 Portal', 'https://d3.delima.edu.my')">
            <div class="dest-icon">🎓</div>
            <div class="dest-title">DELIMa 3.0 Portal</div>
            <div class="dest-desc">Akses ke aplikasi utama Kementerian Pendidikan Malaysia</div>
          </div>

          <div class="dest-card classroom" onclick="window.studentLauncher.launchDestination('Google Classroom', 'https://classroom.google.com')">
            <div class="dest-icon">📚</div>
            <div class="dest-title">Google Classroom</div>
            <div class="dest-desc">Buka tugasan dan bahan pembelajaran guru anda</div>
          </div>

          <div class="dest-card ains" onclick="window.studentLauncher.launchDestination('AINS (NILAM)', 'https://ains.moe.gov.my')">
            <div class="dest-icon">📖</div>
            <div class="dest-title">AINS (NILAM)</div>
            <div class="dest-desc">Pangkalan data bahan bacaan & perekodan NILAM KPM</div>
          </div>

          <div class="dest-card canva" onclick="window.studentLauncher.launchDestination('Canva for Education', 'https://www.canva.com/login/')">
            <div class="dest-icon">🎨</div>
            <div class="dest-title">Canva for Education</div>
            <div class="dest-desc">Perekaan grafik & persembahan kreatif murid</div>
          </div>

          <div class="dest-card textbook" onclick="window.studentLauncher.launchDestination('Buku Teks Digital', 'https://textbook.moe.gov.my')">
            <div class="dest-icon">📕</div>
            <div class="dest-title">Buku Teks Digital</div>
            <div class="dest-desc">Baca buku teks KSSR interaktif Tahun ${this.selectedClass.grade}</div>
          </div>
        </div>
      </div>
    `;
  }

  launchDestination(name, url) {
    this.targetDestName = name;
    this.targetDestUrl = url;
    this.currentView = "signing-in";
    this.render();

    setTimeout(() => {
      this.app.showToast(`Berjaya log masuk ke ${name}!`, "success");
    }, 2000);
  }

  /* SCREEN 5: SEDANG MASUK */
  renderSigningIn() {
    return `
      <div class="signing-in-overlay" style="height: 100%;">
        <div class="fluent-spinner"></div>
        <h2 style="font-size:24px; font-weight:800; margin-bottom:8px;">Sedang Membuka ${this.targetDestName}...</h2>
        <p style="font-size:14px; color:var(--text-secondary); margin-bottom:24px;">
          Menyuntik kelayakan masuk Google Workspace untuk <strong>${this.selectedStudent.name}</strong> secara selamat.
        </p>
        <div class="badge badge-success" style="font-size:13px; padding:6px 14px; margin-bottom:28px;">
          🔒 Sesi Disulitkan (Kiosk Lab Mode)
        </div>
        <button class="btn btn-secondary" onclick="window.studentLauncher.logout()">
          Batal & Kembali
        </button>
      </div>
    `;
  }

  logout() {
    this.currentView = "classes";
    this.selectedClass = null;
    this.selectedStudent = null;
    this.enteredSymbols = [];
    this.render();
  }

  openTeacherPinModal() {
    const modal = document.getElementById("teacherPinModal");
    const input = document.getElementById("teacherPinInput");
    if (modal) {
      modal.classList.add("active");
      if (input) {
        input.focus();
        input.onkeydown = (e) => {
          if (e.key === "Enter") this.verifyTeacherPin();
          if (e.key === "Escape") this.closeTeacherPinModal();
        };
      }
    }
  }

  closeTeacherPinModal() {
    const modal = document.getElementById("teacherPinModal");
    if (modal) modal.classList.remove("active");
  }

  verifyTeacherPin() {
    const input = document.getElementById("teacherPinInput");
    const entered = input ? input.value.trim() : "";
    // NOTE: In this Web UI prototype the passphrase is sourced from DelimaData.
    // The real WPF application validates against the DPAPI-protected bundle.
    const configured = DelimaData.school.adminPassphrase;
    if (entered.length > 0 && configured.length > 0 && entered === configured) {
      this.closeTeacherPinModal();
      this.app.switchMode("admin");
      this.app.showToast("Mod Guru diaktifkan. Dialihkan ke DELIMa Admin.", "success");
    } else if (entered.length === 0) {
      this.app.showToast("Sila masukkan kata laluan guru.", "error");
    } else {
      this.app.showToast("PIN / Kata laluan guru tidak tepat.", "error");
    }
  }
}
