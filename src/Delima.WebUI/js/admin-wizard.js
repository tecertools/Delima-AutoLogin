/**
 * DELIMa Windows 11 Web UI — Admin Wizard Controller
 */

class AdminWizard {
  constructor(app) {
    this.app = app;
    this.currentStep = 1;
    this.totalSteps = 7;
    this.passwordsRevealed = false;
    this.searchQuery = "";
    this.selectedClassFilter = "ALL";
    this.provisioningInProgress = false;

    this.steps = [
      { num: 1, id: "identity", label: "Identiti Sekolah", icon: "🏫" },
      { num: 2, id: "passphrase", label: "Kata Laluan Pentadbir", icon: "🔒" },
      { num: 3, id: "import", label: "Import Senarai Murid (CSV)", icon: "📥" },
      { num: 4, id: "passwords", label: "Matriks Kata Laluan", icon: "🔑" },
      { num: 5, id: "avatars", label: "Avatar & Kata Laluan Gambar", icon: "🎨" },
      { num: 6, id: "settings", label: "Destinasi & Tetapan", icon: "⚙️" },
      { num: 7, id: "provision", label: "Penyediaan & Eksport USB", icon: "💾" }
    ];
  }

  init() {
    this.renderNavRail();
    this.renderStep(this.currentStep);
  }

  renderNavRail() {
    const navMenu = document.getElementById("adminNavMenu");
    if (!navMenu) return;

    navMenu.innerHTML = this.steps.map(s => {
      let stateClass = "";
      let badgeContent = s.num;
      if (s.num < this.currentStep) {
        stateClass = "done";
        badgeContent = "✓";
      } else if (s.num === this.currentStep) {
        stateClass = "active";
      }

      return `
        <div class="nav-item ${stateClass}" onclick="window.adminWizard.goToStep(${s.num})">
          <div class="step-badge">${badgeContent}</div>
          <span>${s.label}</span>
        </div>
      `;
    }).join("");
  }

  goToStep(stepNum) {
    if (stepNum < 1 || stepNum > this.totalSteps) return;
    this.currentStep = stepNum;
    this.renderNavRail();
    this.renderStep(stepNum);
  }

  nextStep() {
    if (this.currentStep < this.totalSteps) {
      this.goToStep(this.currentStep + 1);
    }
  }

  prevStep() {
    if (this.currentStep > 1) {
      this.goToStep(this.currentStep - 1);
    }
  }

  renderStep(stepNum) {
    const stage = document.getElementById("adminStageContent");
    const titleEl = document.getElementById("adminStageTitle");
    const subEl = document.getElementById("adminStageSub");
    const actionsBar = document.getElementById("adminActionsBar");

    if (!stage) return;

    switch (stepNum) {
      case 1:
        titleEl.textContent = "1. Identiti & Penjenamaan Sekolah";
        subEl.textContent = "Tetapkan nama sekolah, kod APDM/MOE, dan palet warna lencana sekolah untuk antaramuka makmal.";
        stage.innerHTML = this.renderStep1();
        this.bindStep1Events();
        break;

      case 2:
        titleEl.textContent = "2. Kata Laluan Pentadbir & Keselamatan";
        subEl.textContent = "Cipta frasa laluan pentadbir untuk melindungi kelayakan murid dan membuka kunci 'Mod Guru' di makmal.";
        stage.innerHTML = this.renderStep2();
        this.bindStep2Events();
        break;

      case 3:
        titleEl.textContent = "3. Import Senarai Murid (CSV APDM)";
        subEl.textContent = "Muat naik fail CSV senarai murid dari APDM atau Google Workspace. Sistem memadankan lajur secara automatik.";
        stage.innerHTML = this.renderStep3();
        this.bindStep3Events();
        break;

      case 4:
        titleEl.textContent = "4. Matriks Kata Laluan & Kelayakan Murid";
        subEl.textContent = "Urus kata laluan teks DELIMa dan padankan dengan 3 simbol kata laluan gambar untuk murid Tahap 1.";
        stage.innerHTML = this.renderStep4();
        this.bindStep4Events();
        break;

      case 5:
        titleEl.textContent = "5. Avatar & Padanan Simbol Gambar";
        subEl.textContent = "Konfigurasi ikon avatar murid dan periksa kebolehbacaan simbol gambar untuk murid tahun 1–3.";
        stage.innerHTML = this.renderStep5();
        break;

      case 6:
        titleEl.textContent = "6. Destinasi & Pilihan Kiosk";
        subEl.textContent = "Pilih portal lalai selepas log masuk berjaya dan tetapkan masa henti keselamatan makmal.";
        stage.innerHTML = this.renderStep6();
        break;

      case 7:
        titleEl.textContent = "7. Penyediaan & Eksport Pemacu USB Makmal";
        subEl.textContent = "Eksport pakej konfigurasi selamat dan fail pengesahan ke pemacu kilat USB untuk semua 28 PC makmal.";
        stage.innerHTML = this.renderStep7();
        this.bindStep7Events();
        break;
    }

    // Render Bottom Actions
    actionsBar.innerHTML = `
      <button class="btn btn-secondary" onclick="window.adminWizard.prevStep()" ${stepNum === 1 ? "disabled" : ""}>
        ← Kembali
      </button>
      <div style="display: flex; gap: 10px;">
        <button class="btn btn-subtle" onclick="window.app.showToast('Draf disimpan secara automatik', 'info')">
          💾 Simpan Draf
        </button>
        ${stepNum === 7 ? `
          <button class="btn btn-accent" onclick="window.adminWizard.startProvisioning()">
            🚀 Mulakan Eksport USB
          </button>
        ` : `
          <button class="btn btn-accent" onclick="window.adminWizard.nextStep()">
            Seterusnya →
          </button>
        `}
      </div>
    `;
  }

  /* STEP 1 */
  renderStep1() {
    return `
      <div class="identity-grid">
        <div class="card">
          <div class="card-header">
            <h3 class="card-title">Maklumat Rasmi Sekolah</h3>
          </div>
          <div class="form-group">
            <label class="form-label">Nama Penuh Sekolah</label>
            <input type="text" id="schNameInput" class="input-text" value="${DelimaData.school.name}">
          </div>
          <div class="row2" style="display:grid; grid-template-columns:1fr 1fr; gap:14px;">
            <div class="form-group">
              <label class="form-label">Kod Sekolah (MOE/APDM)</label>
              <input type="text" id="schCodeInput" class="input-text" value="${DelimaData.school.code}">
            </div>
            <div class="form-group">
              <label class="form-label">Teks Lencana (Singkatan)</label>
              <input type="text" id="schCrestInput" class="input-text" value="${DelimaData.school.crestText}">
            </div>
          </div>
          <div class="form-group">
            <label class="form-label">Cogan Kata / Moto Sekolah</label>
            <input type="text" id="schMottoInput" class="input-text" value="${DelimaData.school.motto}">
          </div>
          <div class="form-group">
            <label class="form-label">Warna Tema Lencana Sekolah</label>
            <div class="color-palette-grid">
              ${[
                { name: "Hijau Zamrud", hex: "#007A3D" },
                { name: "Biru Diraja", hex: "#0055A5" },
                { name: "Merah Menyala", hex: "#C41118" },
                { name: "Oren Senja", hex: "#E06D00" },
                { name: "Ungu Gemilang", hex: "#6B2C91" }
              ].map((c, i) => `
                <div class="color-swatch-item ${c.hex === DelimaData.school.crestBg ? 'selected' : ''}" onclick="window.adminWizard.setThemeColor('${c.hex}')">
                  <div class="swatch-color-box" style="background:${c.hex}"></div>
                  <div class="swatch-label">${c.name}</div>
                </div>
              `).join("")}
            </div>
          </div>
        </div>

        <div class="preview-card-wrap">
          <div style="font-size:12px; font-weight:700; color:var(--text-secondary); margin-bottom:14px; text-transform:uppercase; letter-spacing:0.04em;">
            Pratonton Antaramuka Kiosk Murid
          </div>
          <div class="preview-kiosk-frame">
            <div class="preview-kiosk-header" style="border-bottom-color: ${DelimaData.school.crestBg}">
              <div class="preview-crest" style="background: ${DelimaData.school.crestBg}">
                ${DelimaData.school.crestText}
              </div>
              <div style="text-align:left;">
                <div style="font-weight:800; font-size:14px; color:var(--text-primary);">${DelimaData.school.name}</div>
                <div style="font-size:11px; color:var(--text-secondary);">${DelimaData.school.motto}</div>
              </div>
            </div>
            <div style="padding:24px 18px; text-align:center;">
              <div style="font-size:16px; font-weight:800; margin-bottom:6px;">Selamat Datang ke Makmal Komputer</div>
              <div style="font-size:12px; color:var(--text-secondary); margin-bottom:16px;">Sila pilih kelas anda untuk memulakan sesi pembelajaran.</div>
              <div class="badge badge-success">✓ Penjenamaan Tersedia</div>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  setThemeColor(hex) {
    DelimaData.school.crestBg = hex;
    document.documentElement.style.setProperty("--delima-green", hex);
    this.renderStep(1);
    this.app.showToast("Warna tema sekolah dikemaskini", "info");
  }

  bindStep1Events() {
    const nameInput = document.getElementById("schNameInput");
    const codeInput = document.getElementById("schCodeInput");
    const crestInput = document.getElementById("schCrestInput");
    const mottoInput = document.getElementById("schMottoInput");

    const updatePreview = () => {
      // Update Titlebar Tag
      const titleTag = document.querySelector(".titlebar-school-tag");
      if (titleTag) titleTag.textContent = DelimaData.school.name;

      // Update Left Nav Brand
      const brandCrest = document.getElementById("adminBrandCrest");
      const brandInfo = document.querySelector(".brand-info p");
      if (brandCrest) brandCrest.textContent = DelimaData.school.crestText;
      if (brandInfo) brandInfo.textContent = DelimaData.school.name;

      // Update Step 1 Preview Card
      const previewCrest = document.querySelector(".preview-crest");
      const previewName = document.querySelector(".preview-kiosk-header div div:first-child");
      const previewMotto = document.querySelector(".preview-kiosk-header div div:last-child");
      if (previewCrest) previewCrest.textContent = DelimaData.school.crestText;
      if (previewName) previewName.textContent = DelimaData.school.name;
      if (previewMotto) previewMotto.textContent = DelimaData.school.motto;
    };

    if (nameInput) {
      nameInput.addEventListener("input", (e) => {
        DelimaData.school.name = e.target.value;
        updatePreview();
      });
    }
    if (codeInput) {
      codeInput.addEventListener("input", (e) => {
        DelimaData.school.code = e.target.value;
      });
    }
    if (crestInput) {
      crestInput.addEventListener("input", (e) => {
        DelimaData.school.crestText = e.target.value;
        updatePreview();
      });
    }
    if (mottoInput) {
      mottoInput.addEventListener("input", (e) => {
        DelimaData.school.motto = e.target.value;
        updatePreview();
      });
    }
  }

  /* STEP 2 */
  renderStep2() {
    return `
      <div style="max-width: 680px; margin: 0 auto;">
        <div class="card" style="margin-bottom: 20px;">
          <div class="card-header">
            <h3 class="card-title">Frasa Laluan Keselamatan Pentadbir</h3>
          </div>
          <p style="font-size:13px; color:var(--text-secondary); margin-bottom:16px;">
            Frasa laluan ini digunakan untuk:
            <br>• Melindungi fail kelayakan kata laluan semasa eksport USB.
            <br>• Menyahkunci <strong>Mod Guru</strong> di makmal sekiranya murid terlupa kata laluan gambar.
          </p>
          <div class="form-group">
            <label class="form-label">Frasa Laluan Pentadbir Baharu</label>
            <input type="password" id="adminPassInput" class="input-text" value="admin12345">
          </div>
          <div class="form-group">
            <label class="form-label">Sahkan Frasa Laluan</label>
            <input type="password" id="adminPassConfirmInput" class="input-text" value="admin12345">
          </div>
          <div style="display: flex; gap: 10px; margin-top: 10px; align-items: center; flex-wrap: wrap;">
            <div id="passStrengthBadge" class="badge badge-success">
              🔒 Tahap Kekuatan: Sangat Baik (Kunci AES-256 GCM)
            </div>
            <div id="passMatchBadge" class="badge badge-success">
              ✓ Sepadan
            </div>
          </div>
        </div>

        <div class="card" style="background:var(--bg-layer-2); border-left:4px solid var(--delima-orange);">
          <h4 style="font-size:13px; font-weight:700; margin-bottom:6px; color:var(--text-primary);">Pemberitahuan Dasar Tempatan</h4>
          <p style="font-size:12px; color:var(--text-secondary); line-height:18px;">
            Semua kata laluan murid disimpan secara disulitkan secara setempat (DPAPI & AES-256). Tiada sebarang kelayakan murid dihantar ke pelayan awan pihak ketiga.
          </p>
        </div>
      </div>
    `;
  }

  bindStep2Events() {
    const pInput = document.getElementById("adminPassInput");
    const cInput = document.getElementById("adminPassConfirmInput");
    const strengthBadge = document.getElementById("passStrengthBadge");
    const matchBadge = document.getElementById("passMatchBadge");

    const updateStatus = () => {
      const p = pInput ? pInput.value : "";
      const c = cInput ? cInput.value : "";
      DelimaData.school.adminPassphrase = p;

      if (!strengthBadge || !matchBadge) return;

      // Strength Check
      if (p.length >= 8) {
        strengthBadge.className = "badge badge-success";
        strengthBadge.textContent = "🔒 Tahap Kekuatan: Sangat Baik (Kunci AES-256 GCM)";
      } else if (p.length >= 5) {
        strengthBadge.className = "badge badge-warning";
        strengthBadge.textContent = "⚠️ Tahap Kekuatan: Sederhana";
      } else {
        strengthBadge.className = "badge badge-danger";
        strengthBadge.textContent = "❌ Tahap Kekuatan: Terlalu Lemah";
      }

      // Match Check
      if (p === c && p.length > 0) {
        matchBadge.className = "badge badge-success";
        matchBadge.textContent = "✓ Sepadan";
      } else {
        matchBadge.className = "badge badge-danger";
        matchBadge.textContent = "❌ Tidak Sepadan";
      }
    };

    if (pInput) pInput.addEventListener("input", updateStatus);
    if (cInput) cInput.addEventListener("input", updateStatus);
  }

  /* STEP 3 */
  renderStep3() {
    return `
      <div class="csv-dropzone" id="csvDropzone">
        <div class="dropzone-icon">📁</div>
        <div class="dropzone-text">Seret & Lepaskan fail CSV APDM di sini, atau klik untuk memilih</div>
        <div class="dropzone-hint">Menyokong format CSV dari APDM MOE, Google Workspace Admin, atau format Excel tersuai.</div>
      </div>

      <div class="csv-metrics-grid">
        <div class="metric-card">
          <div class="metric-icon-wrap" style="background:var(--delima-green-subtle); color:var(--delima-green);">👥</div>
          <div>
            <div class="metric-value">20</div>
            <div class="metric-label">Jumlah Murid Diimport</div>
          </div>
        </div>
        <div class="metric-card">
          <div class="metric-icon-wrap" style="background:var(--status-info-bg); color:var(--status-info-fg);">🏫</div>
          <div>
            <div class="metric-value">3</div>
            <div class="metric-label">Kelas Dikesan</div>
          </div>
        </div>
        <div class="metric-card">
          <div class="metric-icon-wrap" style="background:var(--status-success-bg); color:var(--status-success-fg);">✓</div>
          <div>
            <div class="metric-value">100%</div>
            <div class="metric-label">Integriti Emel DELIMa</div>
          </div>
        </div>
        <div class="metric-card">
          <div class="metric-icon-wrap" style="background:var(--status-warning-bg); color:var(--status-warning-fg);">🔑</div>
          <div>
            <div class="metric-value">20/20</div>
            <div class="metric-label">Simbol Gambar Siap Dicipta</div>
          </div>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <h3 class="card-title">Pemetaan Lajur CSV</h3>
          <span class="badge badge-success">✓ Dikesan Automatik</span>
        </div>
        <div class="mapping-table">
          <div class="mapping-row">
            <div class="mapping-target required">Nama Penuh Murid</div>
            <div><input type="text" class="input-text" value="NAMA_MURID (Lajur A)" disabled></div>
            <div><span class="badge badge-success">Padan 100%</span></div>
          </div>
          <div class="mapping-row">
            <div class="mapping-target required">ID Emel DELIMa</div>
            <div><input type="text" class="input-text" value="EMEL_DELIMA (Lajur B)" disabled></div>
            <div><span class="badge badge-success">Padan 100%</span></div>
          </div>
          <div class="mapping-row">
            <div class="mapping-target required">Nama Kelas / Tingkatan</div>
            <div><input type="text" class="input-text" value="KELAS (Lajur C)" disabled></div>
            <div><span class="badge badge-success">Padan 100%</span></div>
          </div>
          <div class="mapping-row">
            <div class="mapping-target">Kata Laluan Teks Asal</div>
            <div><input type="text" class="input-text" value="KATA_LALUAN (Lajur D)" disabled></div>
            <div><span class="badge badge-success">Disulitkan</span></div>
          </div>
        </div>
      </div>
    `;
  }

  bindStep3Events() {
    const dropzone = document.getElementById("csvDropzone");
    if (!dropzone) return;

    dropzone.addEventListener("click", () => {
      this.app.showToast("Fail CSV 'Senarai_Murid_SK24_2026.csv' berjaya dimuatkan & disahkan (20 murid)", "success");
    });

    dropzone.addEventListener("dragover", (e) => {
      e.preventDefault();
      dropzone.classList.add("dragover");
    });

    dropzone.addEventListener("dragleave", () => {
      dropzone.classList.remove("dragover");
    });

    dropzone.addEventListener("drop", (e) => {
      e.preventDefault();
      dropzone.classList.remove("dragover");
      const files = e.dataTransfer.files;
      const fileName = files.length > 0 ? files[0].name : "senarai_murid.csv";
      this.app.showToast(`Fail '${fileName}' berjaya dimuat naik dan diproses.`, "success");
    });
  }

  /* STEP 4 */
  renderStep4() {
    const filteredStudents = DelimaData.students.filter(s => {
      const matchSearch = s.name.toLowerCase().includes(this.searchQuery.toLowerCase()) || s.email.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchClass = this.selectedClassFilter === "ALL" || s.classId === this.selectedClassFilter;
      return matchSearch && matchClass;
    });

    return `
      <div class="password-matrix-toolbar">
        <div class="search-filter-wrap">
          <input type="text" id="matrixSearchInput" class="input-text" placeholder="Cari nama murid atau ID emel..." value="${this.searchQuery}">
          <select id="matrixClassFilter" class="select-control" style="width: 140px;">
            <option value="ALL">Semua Kelas</option>
            <option value="1A" ${this.selectedClassFilter === '1A' ? 'selected' : ''}>1 Amanah</option>
            <option value="1B" ${this.selectedClassFilter === '1B' ? 'selected' : ''}>1 Bestari</option>
            <option value="2C" ${this.selectedClassFilter === '2C' ? 'selected' : ''}>2 Cemerlang</option>
          </select>
        </div>
        <div style="display:flex; gap:8px;">
          <button class="btn btn-secondary" onclick="window.adminWizard.togglePasswordVisibility()">
            ${this.passwordsRevealed ? '🔒 Sembunyikan Kata Laluan' : '👁️ Papar Kata Laluan Teks'}
          </button>
          <button class="btn btn-accent" onclick="window.adminWizard.bulkGenerateSymbols()">
            ✨ Jana Rawak Simbol Gambar
          </button>
        </div>
      </div>

      <div class="table-container">
        <table class="fluent-table">
          <thead>
            <tr>
              <th style="width: 40px;">#</th>
              <th>Murid</th>
              <th>Kelas</th>
              <th>ID Emel DELIMa</th>
              <th>Kata Laluan Gambar (3 Simbol)</th>
              <th>Kata Laluan Teks</th>
              <th>Tindakan</th>
            </tr>
          </thead>
          <tbody>
            ${filteredStudents.map((s, idx) => `
              <tr>
                <td>${idx + 1}</td>
                <td>
                  <div style="display:flex; align-items:center; gap:10px;">
                    <div class="matrix-row-avatar">${s.avatar}</div>
                    <div style="font-weight:700;">${s.name}</div>
                  </div>
                </td>
                <td><span class="badge badge-neutral">${s.classId}</span></td>
                <td class="mono" style="font-size:12px; color:var(--text-secondary);">${s.email}</td>
                <td>
                  <div class="picture-symbols-chip">
                    ${s.passSymbols.join(" ")}
                  </div>
                </td>
                <td>
                  ${this.passwordsRevealed ? `
                    <span class="mono" style="font-weight:700; color:var(--delima-green);">${s.passwordText}</span>
                  ` : `
                    <span class="masked-pass">••••••••</span>
                  `}
                </td>
                <td>
                  <button class="btn btn-sm btn-subtle" onclick="window.adminWizard.editStudentSymbols('${s.id}')">
                    ✏️ Tukar Simbol
                  </button>
                </td>
              </tr>
            `).join("")}
          </tbody>
        </table>
      </div>
    `;
  }

  bindStep4Events() {
    const searchInput = document.getElementById("matrixSearchInput");
    const classFilter = document.getElementById("matrixClassFilter");

    if (searchInput) {
      searchInput.addEventListener("input", (e) => {
        this.searchQuery = e.target.value;
        this.renderStep(4);
      });
    }

    if (classFilter) {
      classFilter.addEventListener("change", (e) => {
        this.selectedClassFilter = e.target.value;
        this.renderStep(4);
      });
    }
  }

  togglePasswordVisibility() {
    this.passwordsRevealed = !this.passwordsRevealed;
    this.renderStep(4);
    this.app.showToast(this.passwordsRevealed ? "Kata laluan teks dipaparkan" : "Kata laluan disembunyikan", "info");
  }

  bulkGenerateSymbols() {
    DelimaData.students.forEach(s => {
      const shuffled = [...DelimaData.symbolsCatalog].sort(() => 0.5 - Math.random());
      s.passSymbols = [shuffled[0].emoji, shuffled[1].emoji, shuffled[2].emoji];
    });
    this.renderStep(4);
    this.app.showToast("Simbol kata laluan gambar dijana semula untuk semua murid", "success");
  }

  editStudentSymbols(studentId) {
    const student = DelimaData.students.find(s => s.id === studentId);
    if (!student) return;

    const shuffled = [...DelimaData.symbolsCatalog].sort(() => 0.5 - Math.random());
    student.passSymbols = [shuffled[0].emoji, shuffled[1].emoji, shuffled[2].emoji];
    this.renderStep(4);
    this.app.showToast(`Simbol baharu ditetapkan untuk ${student.name.split(" ")[0]}`, "success");
  }

  /* STEP 5 */
  renderStep5() {
    return `
      <div class="card" style="margin-bottom: 20px;">
        <div class="card-header">
          <h3 class="card-title">Katalog 16 Simbol Kata Laluan Gambar Kanak-Kanak</h3>
        </div>
        <p style="font-size:13px; color:var(--text-secondary); margin-bottom:16px;">
          Simbol-simbol ini dipilih khas supaya mudah dikenali oleh murid Tahun 1 tanpa kekeliruan optik.
        </p>
        <div style="display:grid; grid-template-columns:repeat(8, 1fr); gap:12px;">
          ${DelimaData.symbolsCatalog.map(sym => `
            <div class="card" style="text-align:center; padding:12px 6px;">
              <div style="font-size:32px; margin-bottom:4px;">${sym.emoji}</div>
              <div style="font-size:11px; font-weight:700; color:var(--text-primary);">${sym.name}</div>
              <div style="font-size:9.5px; color:var(--text-tertiary); text-transform:capitalize;">${sym.category}</div>
            </div>
          `).join("")}
        </div>
      </div>
    `;
  }

  /* STEP 6 */
  renderStep6() {
    return `
      <div style="max-width:720px; margin:0 auto;">
        <div class="card" style="margin-bottom:20px;">
          <div class="card-header">
            <h3 class="card-title">Destinasi Log Masuk Utama</h3>
          </div>
          <div class="form-group">
            <label class="form-label">Portal Lalai Selepas Log Masuk Berjaya</label>
            <select class="select-control">
              <option>🎓 DELIMa 3.0 Portal (d3.delima.edu.my)</option>
              <option>📚 Google Classroom (classroom.google.com)</option>
              <option>📖 AINS (NILAM) (ains.moe.gov.my)</option>
              <option>📕 Buku Teks Digital KSSR</option>
            </select>
          </div>
          <div class="form-group" style="margin-top:16px;">
            <label class="fluent-switch">
              <input type="checkbox" checked>
              <span class="switch-track"><span class="switch-thumb"></span></span>
              <span>Papar Butang 'Tamat Sesi' terapung semasa murid melayari web</span>
            </label>
          </div>
        </div>
      </div>
    `;
  }

  /* STEP 7 */
  renderStep7() {
    return `
      <div class="provision-layout">
        <div>
          <div class="card" style="margin-bottom:20px;">
            <div class="card-header">
              <h3 class="card-title">Pemacu USB Dikesan</h3>
              <span class="badge badge-success">🟢 Bersedia</span>
            </div>
            <div class="drive-select-card">
              <div class="drive-icon">💾</div>
              <div class="drive-details">
                <h4>Kingston DataTraveler 3.0 (E:)</h4>
                <p>14.8 GB Tersedia • Sistem Fail NTFS • Format Sah</p>
              </div>
            </div>
            <button class="btn btn-secondary btn-sm" onclick="window.app.showToast('Mengimbas semula port USB...', 'info')">
              🔄 Imbas Semula Pemacu
            </button>
          </div>

          <div class="card">
            <div class="card-header">
              <h3 class="card-title">Status Pakej Penyediaan</h3>
            </div>
            <div class="checklist-step">
              <div class="checklist-icon">✓</div>
              <div class="checklist-text">
                <h5>Fail Manifes Sekolah & Penjenamaan</h5>
                <p>manifest.json dicipta dengan tema SK Seksyen 24</p>
              </div>
            </div>
            <div class="checklist-step">
              <div class="checklist-icon">✓</div>
              <div class="checklist-text">
                <h5>Fail Kelayakan Murid Disulitkan (AES-256)</h5>
                <p>roster_secure.dat disulitkan dengan frasa laluan pentadbir</p>
              </div>
            </div>
            <div class="checklist-step">
              <div class="checklist-icon">✓</div>
              <div class="checklist-text">
                <h5>Skrip Konfigurasi ACL & AppLocker Makmal</h5>
                <p>Deploy-DelimaLab.ps1 disertakan untuk konfigurasi automatik 28 PC</p>
              </div>
            </div>
          </div>
        </div>

        <div class="card" style="display:flex; flex-direction:column; justify-content:center; text-align:center; padding:32px 24px;">
          <div style="font-size:52px; margin-bottom:12px;">🚀</div>
          <h3 style="font-size:18px; font-weight:800; margin-bottom:8px;">Sedia Untuk Mengeksport ke USB Makmal</h3>
          <p style="font-size:13px; color:var(--text-secondary); margin-bottom:24px; line-height:20px;">
            Pakej ini membolehkan anda membawa pemacu USB ke komputer makmal dan menjalankan pemasangan pantas dalam masa kurang dari 1 minit setiap PC.
          </p>

          <div id="provisionProgressWrap" style="display:none; margin-bottom:16px;">
            <div class="fluent-progress-bar">
              <div class="fluent-progress-fill" id="provisionProgressBar" style="width: 0%;"></div>
            </div>
            <div style="font-size:12px; font-weight:700; color:var(--text-secondary);" id="provisionStatusText">Menyediakan fail...</div>
          </div>

          <button class="btn btn-accent btn-lg" id="btnStartExport" onclick="window.adminWizard.startProvisioning()">
            💾 Eksport Pakej ke Pemacu USB (E:)
          </button>
        </div>
      </div>
    `;
  }

  bindStep7Events() {}

  startProvisioning() {
    const progressWrap = document.getElementById("provisionProgressWrap");
    const progressBar = document.getElementById("provisionProgressBar");
    const statusText = document.getElementById("provisionStatusText");
    const btn = document.getElementById("btnStartExport");

    if (!progressWrap || !progressBar) return;

    progressWrap.style.display = "block";
    if (btn) btn.disabled = true;

    let progress = 0;
    const interval = setInterval(() => {
      progress += 25;
      progressBar.style.width = progress + "%";

      if (progress === 25) statusText.textContent = "Menyulitkan senarai kelayakan (AES-256)...";
      if (progress === 50) statusText.textContent = "Menjana skrip Deploy-DelimaLab.ps1...";
      if (progress === 75) statusText.textContent = "Menulis ke pemacu Kingston (E:)...";
      if (progress >= 100) {
        clearInterval(interval);
        statusText.textContent = "✓ Selesai! Pemacu USB sedia digunakan.";
        if (btn) btn.disabled = false;
        this.app.showToast("Pakej makmal berjaya dieksport ke Pemacu USB (E:)", "success");
      }
    }, 400);
  }
}
