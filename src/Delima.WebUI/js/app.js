/**
 * DELIMa Windows 11 Web UI — Main Application Shell Controller
 */

class DelimaApp {
  constructor() {
    this.currentMode = "admin"; // "admin" | "student"
    this.theme = "light";
    this.isMaximized = false;
  }

  init() {
    // Check system color scheme preference
    if (window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches) {
      this.setTheme("dark");
    } else {
      this.setTheme("light");
    }

    // Initialize sub-controllers
    window.adminWizard = new AdminWizard(this);
    window.studentLauncher = new StudentLauncher(this);

    // Initial render
    this.switchMode(this.currentMode);
    this.bindWindowControls();
  }

  setTheme(theme) {
    this.theme = theme;
    document.documentElement.setAttribute("data-theme", theme);
    const themeIcon = document.getElementById("themeToggleIcon");
    if (themeIcon) {
      themeIcon.textContent = theme === "dark" ? "☀️" : "🌙";
    }
  }

  toggleTheme() {
    const nextTheme = this.theme === "light" ? "dark" : "light";
    this.setTheme(nextTheme);
    this.showToast(`Mod ${nextTheme === 'dark' ? 'Gelap (Dark Mica)' : 'Cerah (Light Mica)'} diaktifkan`, "info");
  }

  switchMode(mode) {
    this.currentMode = mode;

    const adminContainer = document.getElementById("adminViewContainer");
    const studentContainer = document.getElementById("studentViewContainer");
    const btnAdmin = document.getElementById("modeBtnAdmin");
    const btnStudent = document.getElementById("modeBtnStudent");
    const windowTitle = document.getElementById("windowTitleText");

    if (mode === "admin") {
      if (adminContainer) adminContainer.style.display = "flex";
      if (studentContainer) studentContainer.style.display = "none";
      if (btnAdmin) btnAdmin.classList.add("active");
      if (btnStudent) btnStudent.classList.remove("active");
      if (windowTitle) windowTitle.textContent = "DELIMa Admin — Wizard Persediaan Sekolah";
      window.adminWizard.init();
    } else {
      if (adminContainer) adminContainer.style.display = "none";
      if (studentContainer) studentContainer.style.display = "flex";
      if (btnAdmin) btnAdmin.classList.remove("active");
      if (btnStudent) btnStudent.classList.add("active");
      if (windowTitle) windowTitle.textContent = "DELIMa Smart Launcher — Kiosk Makmal Murid";
      window.studentLauncher.init();
    }
  }

  bindWindowControls() {
    const btnMin = document.getElementById("winBtnMin");
    const btnMax = document.getElementById("winBtnMax");
    const btnClose = document.getElementById("winBtnClose");
    const win = document.getElementById("mainWindowFrame");

    if (btnMin) {
      btnMin.addEventListener("click", () => {
        this.showToast("Tetingkap diminimakan (Simulasi Windows 11)", "info");
      });
    }

    if (btnMax) {
      btnMax.addEventListener("click", () => {
        this.isMaximized = !this.isMaximized;
        if (win) win.classList.toggle("maximized", this.isMaximized);
      });
    }

    if (btnClose) {
      btnClose.addEventListener("click", () => {
        this.showToast("Aplikasi ditutup (Simulasi)", "info");
      });
    }
  }

  showToast(message, type = "info") {
    const container = document.getElementById("toastContainer");
    if (!container) return;

    const toast = document.createElement("div");
    toast.className = `fluent-toast toast-${type}`;
    
    let icon = "ℹ️";
    if (type === "success") icon = "✅";
    if (type === "error") icon = "⚠️";

    toast.innerHTML = `
      <div style="font-size:16px;">${icon}</div>
      <div style="font-size:12.5px; font-weight:600; color:var(--text-primary); flex:1;">${message}</div>
    `;

    container.appendChild(toast);

    setTimeout(() => {
      toast.style.opacity = "0";
      toast.style.transform = "translateY(20px)";
      toast.style.transition = "all 0.3s ease";
      setTimeout(() => toast.remove(), 300);
    }, 3200);
  }
}

// Bootstrap on DOM Ready
document.addEventListener("DOMContentLoaded", () => {
  window.app = new DelimaApp();
  window.app.init();
});
