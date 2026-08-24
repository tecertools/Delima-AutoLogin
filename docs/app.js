/**
 * DELIMa Smart Launcher — Panduan Interaktif Setup & Pemasangan
 * Interactive JavaScript Engine
 */

document.addEventListener('DOMContentLoaded', () => {
  // Total steps in the guide
  const TOTAL_STEPS = 14;
  
  // State variables
  let currentStep = 1;
  let completedSteps = new Set();
  let isViewAllMode = false;
  let checklistState = {};

  // DOM Elements
  const stepContainers = document.querySelectorAll('.step-container');
  const navStepItems = document.querySelectorAll('.nav-step-item');
  const topProgressFill = document.getElementById('topProgressFill');
  const progressPercentText = document.getElementById('progressPercentText');
  const currentStepCounterText = document.getElementById('currentStepCounterText');
  const viewModeToggleBtn = document.getElementById('viewModeToggleBtn');
  const themeToggleBtn = document.getElementById('themeToggleBtn');
  const searchInput = document.getElementById('guideSearchInput');
  const btnResetProgress = document.getElementById('btnResetProgress');
  const toastNotice = document.getElementById('toastNotice');
  const toastText = document.getElementById('toastText');
  const lightboxModal = document.getElementById('lightboxModal');
  const lightboxImg = document.getElementById('lightboxImg');
  const lightboxCaption = document.getElementById('lightboxCaption');
  const lightboxClose = document.getElementById('lightboxClose');

  // Load Saved State from localStorage
  function loadSavedState() {
    try {
      const savedCompleted = localStorage.getItem('delima_guide_completed_steps');
      if (savedCompleted) {
        completedSteps = new Set(JSON.parse(savedCompleted));
      }
      
      const savedCurrent = localStorage.getItem('delima_guide_current_step');
      if (savedCurrent) {
        const parsed = parseInt(savedCurrent, 10);
        if (parsed >= 1 && parsed <= TOTAL_STEPS) {
          currentStep = parsed;
        }
      }

      const savedChecklist = localStorage.getItem('delima_guide_checklists');
      if (savedChecklist) {
        checklistState = JSON.parse(savedChecklist);
      }

      const savedTheme = localStorage.getItem('delima_guide_theme');
      if (savedTheme === 'dark') {
        document.documentElement.setAttribute('data-theme', 'dark');
        updateThemeToggleIcon('dark');
      }
    } catch (e) {
      console.warn('Gagal memuatkan status dari storan setempat:', e);
    }
  }

  // Save State to localStorage
  function saveState() {
    try {
      localStorage.setItem('delima_guide_completed_steps', JSON.stringify(Array.from(completedSteps)));
      localStorage.setItem('delima_guide_current_step', currentStep.toString());
      localStorage.setItem('delima_guide_checklists', JSON.stringify(checklistState));
    } catch (e) {
      console.warn('Gagal menyimpan status ke storan setempat:', e);
    }
  }

  // Show Toast Notification
  function showToast(message, duration = 3000) {
    if (!toastNotice || !toastText) return;
    toastText.textContent = message;
    toastNotice.classList.add('show');
    setTimeout(() => {
      toastNotice.classList.remove('show');
    }, duration);
  }

  // Calculate & Update Progress Visuals
  function updateProgress() {
    const completedCount = completedSteps.size;
    const percentage = Math.round((completedCount / TOTAL_STEPS) * 100);

    if (topProgressFill) {
      topProgressFill.style.width = `${percentage}%`;
    }
    if (progressPercentText) {
      progressPercentText.textContent = `${percentage}% Selesai`;
    }
    if (currentStepCounterText) {
      currentStepCounterText.textContent = `Langkah ${currentStep} daripada ${TOTAL_STEPS}`;
    }

    // Update Navigation Items
    navStepItems.forEach((item) => {
      const stepNum = parseInt(item.getAttribute('data-step'), 10);
      
      // Active state
      if (stepNum === currentStep) {
        item.classList.add('active');
      } else {
        item.classList.remove('active');
      }

      // Completed state
      if (completedSteps.has(stepNum)) {
        item.classList.add('completed');
      } else {
        item.classList.remove('completed');
      }
    });

    // Update Step Finish Buttons
    document.querySelectorAll('.btn-finish-step').forEach((btn) => {
      const stepNum = parseInt(btn.getAttribute('data-step'), 10);
      if (completedSteps.has(stepNum)) {
        btn.classList.add('is-finished');
        btn.innerHTML = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg> Telah Selesai (Klik Seterusnya →)`;
      } else {
        btn.classList.remove('is-finished');
        btn.innerHTML = `<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg> Saya Telah Selesai Langkah Ini (Teruskan)`;
      }
    });

    // Check if user completed all steps
    const completionBanner = document.getElementById('stepCompletionBanner');
    if (completionBanner) {
      if (completedCount === TOTAL_STEPS) {
        completionBanner.style.display = 'block';
      } else {
        completionBanner.style.display = 'none';
      }
    }
  }

  // Go to a specific step
  function goToStep(stepNum, smoothScroll = true) {
    if (stepNum < 1 || stepNum > TOTAL_STEPS) return;

    currentStep = stepNum;
    saveState();

    // Show appropriate step container in interactive mode
    if (!isViewAllMode) {
      stepContainers.forEach((container) => {
        const containerStep = parseInt(container.getAttribute('data-step'), 10);
        if (containerStep === currentStep) {
          container.classList.add('active-step');
        } else {
          container.classList.remove('active-step');
        }
      });
    }

    updateProgress();

    if (smoothScroll) {
      const activeCard = document.getElementById(`step-card-${currentStep}`);
      if (activeCard) {
        const headerOffset = 110;
        const elementPosition = activeCard.getBoundingClientRect().top;
        const offsetPosition = elementPosition + window.pageYOffset - headerOffset;
        window.scrollTo({
          top: offsetPosition,
          behavior: 'smooth'
        });
      }
    }
  }

  // Finish current step and move to next
  function completeAndAdvance(stepNum) {
    completedSteps.add(stepNum);
    saveState();
    updateProgress();

    showToast(`✓ Langkah ${stepNum} ditandakan sebagai selesai!`);

    if (stepNum < TOTAL_STEPS) {
      setTimeout(() => {
        goToStep(stepNum + 1);
      }, 350);
    } else {
      // Reached final step
      goToStep(TOTAL_STEPS);
      showToast('🎉 Tahniah! Anda telah melengkapkan semua 14 langkah persediaan makmal!', 4500);
    }
  }

  // Setup Event Listeners for Nav Step Items
  navStepItems.forEach((item) => {
    item.addEventListener('click', (e) => {
      e.preventDefault();
      const targetStep = parseInt(item.getAttribute('data-step'), 10);
      goToStep(targetStep);
    });
  });

  // Setup Finish Step Buttons
  document.querySelectorAll('.btn-finish-step').forEach((btn) => {
    btn.addEventListener('click', () => {
      const stepNum = parseInt(btn.getAttribute('data-step'), 10);
      completeAndAdvance(stepNum);
    });
  });

  // Setup Previous Step Buttons
  document.querySelectorAll('.btn-nav-prev').forEach((btn) => {
    btn.addEventListener('click', () => {
      const stepNum = parseInt(btn.getAttribute('data-step'), 10);
      if (stepNum > 1) {
        goToStep(stepNum - 1);
      }
    });
  });

  // Checklist Items Click Handler
  document.querySelectorAll('.checklist-item').forEach((item) => {
    const id = item.getAttribute('data-chk-id');
    
    // Restore state
    if (id && checklistState[id]) {
      item.classList.add('checked');
    }

    item.addEventListener('click', () => {
      item.classList.toggle('checked');
      if (id) {
        checklistState[id] = item.classList.contains('checked');
        saveState();
      }
    });
  });

  // 1-Click Code & Path Copy Engine
  document.querySelectorAll('.btn-copy').forEach((btn) => {
    btn.addEventListener('click', () => {
      const targetId = btn.getAttribute('data-copy-target');
      const textToCopy = btn.getAttribute('data-copy-text');
      
      let finalContent = '';
      if (targetId) {
        const el = document.getElementById(targetId);
        if (el) finalContent = el.textContent.trim();
      } else if (textToCopy) {
        finalContent = textToCopy;
      }

      if (finalContent) {
        navigator.clipboard.writeText(finalContent).then(() => {
          const originalHTML = btn.innerHTML;
          btn.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="20 6 9 17 4 12"></polyline></svg> Disalin!`;
          btn.style.background = 'var(--primary)';
          btn.style.borderColor = 'var(--primary)';
          btn.style.color = '#fff';

          setTimeout(() => {
            btn.innerHTML = originalHTML;
            btn.style.background = '';
            btn.style.borderColor = '';
            btn.style.color = '';
          }, 2000);
        }).catch(err => {
          console.error('Gagal menyalin teks:', err);
        });
      }
    });
  });

  // View Mode Toggle (Interactive Stepper vs All Steps View)
  if (viewModeToggleBtn) {
    viewModeToggleBtn.addEventListener('click', () => {
      isViewAllMode = !isViewAllMode;
      const mainLayout = document.querySelector('.main-layout');

      if (isViewAllMode) {
        mainLayout.classList.add('view-all-mode');
        viewModeToggleBtn.classList.add('active');
        viewModeToggleBtn.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M4 6h16M4 12h16m-7 6h7"/></svg> Mod Interaktif`;
        showToast('Mod Paparan Penuh Diaktifkan (Sesuai untuk rujukan dan cetakan).');
      } else {
        mainLayout.classList.remove('view-all-mode');
        viewModeToggleBtn.classList.remove('active');
        viewModeToggleBtn.innerHTML = `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z"></path><path d="M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z"></path></svg> Papar Semua Langkah`;
        goToStep(currentStep);
      }
    });
  }

  // Dark / Light Theme Toggle
  function updateThemeToggleIcon(theme) {
    if (!themeToggleBtn) return;
    if (theme === 'dark') {
      themeToggleBtn.innerHTML = `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="5"></circle><line x1="12" y1="1" x2="12" y2="3"></line><line x1="12" y1="21" x2="12" y2="23"></line><line x1="4.22" y1="4.22" x2="5.64" y2="5.64"></line><line x1="18.36" y1="18.36" x2="19.78" y2="19.78"></line><line x1="1" y1="12" x2="3" y2="12"></line><line x1="21" y1="12" x2="23" y2="12"></line><line x1="4.22" y1="19.78" x2="5.64" y2="18.36"></line><line x1="18.36" y1="5.64" x2="19.78" y2="4.22"></line></svg> Mod Cerah`;
    } else {
      themeToggleBtn.innerHTML = `<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z"></path></svg> Mod Gelap`;
    }
  }

  if (themeToggleBtn) {
    themeToggleBtn.addEventListener('click', () => {
      const isDark = document.documentElement.getAttribute('data-theme') === 'dark';
      if (isDark) {
        document.documentElement.removeAttribute('data-theme');
        localStorage.setItem('delima_guide_theme', 'light');
        updateThemeToggleIcon('light');
      } else {
        document.documentElement.setAttribute('data-theme', 'dark');
        localStorage.setItem('delima_guide_theme', 'dark');
        updateThemeToggleIcon('dark');
      }
    });
  }

  // Reset Progress Button
  if (btnResetProgress) {
    btnResetProgress.addEventListener('click', () => {
      if (confirm('Adakah anda pasti ingin mengeset semula semua kemajuan panduan ini?')) {
        completedSteps.clear();
        checklistState = {};
        currentStep = 1;
        saveState();
        document.querySelectorAll('.checklist-item').forEach(item => item.classList.remove('checked'));
        goToStep(1);
        showToast('Kemajuan panduan telah diset semula.');
      }
    });
  }

  // Search & Filter Box
  if (searchInput) {
    searchInput.addEventListener('input', (e) => {
      const query = e.target.value.toLowerCase().trim();
      if (!query) {
        // Reset view to normal
        navStepItems.forEach(item => item.style.display = 'flex');
        return;
      }

      navStepItems.forEach(item => {
        const text = item.textContent.toLowerCase();
        const stepNum = item.getAttribute('data-step');
        const card = document.getElementById(`step-card-${stepNum}`);
        const cardText = card ? card.textContent.toLowerCase() : '';

        if (text.includes(query) || cardText.includes(query)) {
          item.style.display = 'flex';
        } else {
          item.style.display = 'none';
        }
      });
    });
  }

  // Interactive CSV Validator Component
  const btnValidateCsv = document.getElementById('btnValidateCsv');
  const csvInputArea = document.getElementById('csvInputArea');
  const csvResultBox = document.getElementById('csvResultBox');

  if (btnValidateCsv && csvInputArea && csvResultBox) {
    btnValidateCsv.addEventListener('click', () => {
      const rawText = csvInputArea.value.trim();
      if (!rawText) {
        csvResultBox.className = 'csv-result-box error';
        csvResultBox.innerHTML = '⚠️ Sila tampal baris tajuk (header) atau contoh rekod fail CSV anda di dalam kotak di atas.';
        return;
      }

      const lines = rawText.split('\n').map(l => l.trim()).filter(l => l.length > 0);
      const headerLine = lines[0].toUpperCase();
      
      const hasNama = headerLine.includes('NAMA') || headerLine.includes('MURID');
      const hasKelas = headerLine.includes('KELAS');
      const hasIdDelima = headerLine.includes('DELIMA') || headerLine.includes('ID') || headerLine.includes('EMEL');
      const hasTahun = headerLine.includes('TAHUN') || headerLine.includes('TINGKATAN');

      if (hasNama && hasKelas && hasIdDelima) {
        csvResultBox.className = 'csv-result-box success';
        csvResultBox.innerHTML = `
          <strong>✓ Format CSV Sah & Sedia untuk Diimport!</strong><br>
          • Lajur Nama Murid dikesan: <span class="mono">DIKENAL PASTI</span><br>
          • Lajur Kelas dikesan: <span class="mono">DIKENAL PASTI</span><br>
          • Lajur ID Pengguna DELIMa dikesan: <span class="mono">DIKENAL PASTI</span><br>
          <em>Wizard Delima.Admin akan memetakan lajur ini secara automatik semasa Langkah 3.</em>
        `;
      } else {
        csvResultBox.className = 'csv-result-box error';
        let missing = [];
        if (!hasNama) missing.push('Nama Murid (NAMA MURID / NAMA)');
        if (!hasKelas) missing.push('Kelas (KELAS)');
        if (!hasIdDelima) missing.push('ID DELIMa (ID DELIMA / ID PENGGUNA)');

        csvResultBox.innerHTML = `
          <strong>⚠️ Lajur Wajib Tidak Dikesan Lengkap</strong><br>
          Sila pastikan fail CSV anda mengandungi tajuk lajur berikut: <strong>${missing.join(', ')}</strong>.<br>
          <em>Anda boleh merujuk templat fail <span class="mono">contoh_roster.csv</span> dalam folder pemasang.</em>
        `;
      }
    });
  }

  // Fallback and Error Handling for Screenshots
  document.querySelectorAll('.screenshot-img').forEach((img) => {
    const parent = img.closest('.mockup-body');
    const fallback = parent ? parent.querySelector('.image-placeholder-fallback') : null;

    const handleSuccess = () => {
      img.style.display = 'block';
      if (parent) parent.classList.add('has-image');
      if (fallback) fallback.style.display = 'none';
    };

    const handleError = () => {
      img.style.display = 'none';
      if (parent) parent.classList.remove('has-image');
      if (fallback) fallback.style.display = 'flex';
    };

    // Check if image already loaded (e.g. from cache)
    if (img.complete) {
      if (img.naturalWidth > 0 && img.naturalHeight > 0) {
        handleSuccess();
      } else if (img.src && !img.src.endsWith('#') && img.naturalWidth === 0) {
        handleError();
      }
    }

    img.addEventListener('load', handleSuccess);
    img.addEventListener('error', handleError);

    // Lightbox on click
    img.addEventListener('click', () => {
      if (lightboxModal && lightboxImg) {
        lightboxImg.src = img.src;
        if (lightboxCaption) {
          lightboxCaption.textContent = img.alt || 'Tangkapan Skrin Panduan';
        }
        lightboxModal.classList.add('active');
      }
    });
  });

  // Lightbox Close Handlers
  if (lightboxClose) {
    lightboxClose.addEventListener('click', () => {
      lightboxModal.classList.remove('active');
    });
  }
  if (lightboxModal) {
    lightboxModal.addEventListener('click', (e) => {
      if (e.target === lightboxModal) {
        lightboxModal.classList.remove('active');
      }
    });
  }

  // Print Guide Trigger
  const btnPrintGuide = document.getElementById('btnPrintGuide');
  if (btnPrintGuide) {
    btnPrintGuide.addEventListener('click', () => {
      window.print();
    });
  }

  // Initialize
  loadSavedState();
  goToStep(currentStep, false);
});
