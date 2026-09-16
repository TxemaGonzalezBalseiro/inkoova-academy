// ════════════════════════════════════════════════════════════════════
// CursoViewer — visor del curso autodidacta
//
// Sin reveal.js. Funciona en file:// porque solo usa:
//   - IntersectionObserver (tracking de slide visible)
//   - element.scrollIntoView (navegación)
//   - localStorage (progreso y tema)
//   - hash routing nativo (window.location.hash)
//
// Responsabilidades:
//   1. Aplicar tema persistido (también lo hace un script inline para
//      evitar flash, pero aquí redundamos por si el inline no se ejecutó).
//   2. Construir el header fijo superior con barra de progreso.
//   3. FAQ acordeón.
//   4. Tracking de slides vistos vía IntersectionObserver.
//   5. Restaurar última posición (banner "continúa donde lo dejaste").
//   6. Theme switcher en el header.
//   7. Mounting de quizzes y ejercicios al cargar la página.
//   8. Hash routing: si la URL tiene #/slide-N o #slide-N, scrollea ahí.
// ════════════════════════════════════════════════════════════════════

const CursoViewer = {
  init(bloqueId, totalSlides) {
    this.bloqueId = bloqueId;
    this.totalSlides = totalSlides;
    this.bloqueName = "";

    // el tema ya viene puesto desde el <head>
    this.setupFAQ();
    this.setupProgressBar();
    // selector propio retirado: el tema lo hereda de la academia
    this.setupSlideTracking();
    this.mountChecks();
    this.handleInitialNavigation();
  },

  // ── Tema ───────────────────────────────────────────────────────────
  themes: [] /* el tema lo elige la academia, no el documento */,

  applyStoredTheme() {
    const saved = localStorage.getItem("agent-engineering-v3-theme") || "light";
    document.documentElement.setAttribute("data-theme", saved);
  },

  setTheme(themeId) {
    document.documentElement.setAttribute("data-theme", themeId);
    try { localStorage.setItem("agent-engineering-v3-theme", themeId); } catch (e) {}
    this.updateThemeSwitcherActive();
  },

  // ── Header fijo + barra de progreso ────────────────────────────────
  setupProgressBar() {
    const header = document.createElement("div");
    header.className = "bloque-header";
    header.innerHTML = `
      <a href="../index.html" class="back-link">← Menú</a>
      <div class="block-name" id="block-name-text"></div>
      <div class="progress-bar"><div class="progress-bar-fill" id="progress-fill" style="width: 0%"></div></div>
      <div class="progress-text" id="progress-text">0/${this.totalSlides}</div>
    `;
    document.body.appendChild(header);
    this.updateProgressBar();
  },

  updateProgressBar() {
    const b = Progreso.bloque(this.bloqueId);
    const vistos = b.slides_vistos.length;
    const pct = (vistos / this.totalSlides) * 100;
    const fill = document.getElementById("progress-fill");
    const text = document.getElementById("progress-text");
    const name = document.getElementById("block-name-text");
    if (fill) fill.style.width = pct + "%";
    if (text) text.textContent = `${vistos}/${this.totalSlides} slides`;
    if (name) name.innerHTML = `<strong>${this.bloqueId}</strong> · ${this.bloqueName || ""}`;
  },

  setBloqueName(name) {
    this.bloqueName = name;
    this.updateProgressBar();
  },

  // ── Theme switcher ─────────────────────────────────────────────────
  setupThemeSwitcher() {
    const header = document.querySelector(".bloque-header");
    if (!header) return;

    const wrapper = document.createElement("div");
    wrapper.className = "theme-switcher";
    wrapper.innerHTML = `
      <button class="theme-switcher-btn" id="theme-btn" title="Cambiar tema" aria-label="Cambiar tema">◐</button>
      <div class="theme-switcher-menu" id="theme-menu">
        ${this.themes.map(t => `
          <button class="theme-switcher-option" data-theme-id="${t.id}">
            <span class="theme-swatch ${t.id}"></span>
            ${t.name}
          </button>
        `).join("")}
      </div>
    `;
    header.appendChild(wrapper);

    const btn = wrapper.querySelector("#theme-btn");
    const menu = wrapper.querySelector("#theme-menu");
    btn.addEventListener("click", (e) => {
      e.stopPropagation();
      wrapper.classList.toggle("open");
    });
    document.addEventListener("click", (e) => {
      if (!wrapper.contains(e.target)) wrapper.classList.remove("open");
    });
    menu.querySelectorAll(".theme-switcher-option").forEach(opt => {
      opt.addEventListener("click", () => {
        this.setTheme(opt.dataset.themeId);
        wrapper.classList.remove("open");
      });
    });
    this.updateThemeSwitcherActive();
  },

  updateThemeSwitcherActive() {
    const current = document.documentElement.getAttribute("data-theme") || "light";
    document.querySelectorAll(".theme-switcher-option").forEach(opt => {
      opt.classList.toggle("active", opt.dataset.themeId === current);
    });
  },

  // ── FAQ acordeón ───────────────────────────────────────────────────
  setupFAQ() {
    document.addEventListener("click", (e) => {
      const q = e.target.closest(".faq-question");
      if (!q) return;
      const item = q.closest(".faq-item");
      if (item) item.classList.toggle("open");
    });
  },

  // ── Tracking de slides vistos (IntersectionObserver) ───────────────
  setupSlideTracking() {
    if (typeof IntersectionObserver === "undefined") {
      // Fallback para navegadores muy antiguos: marcar el último visible
      // tras cada scroll (más burdo pero funcional).
      window.addEventListener("scroll", () => this._fallbackScrollTrack(), { passive: true });
      return;
    }

    // Un slide se cuenta como "visto" cuando >= 40% es visible.
    const observer = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting && entry.intersectionRatio >= 0.4) {
          const idx = parseInt(entry.target.dataset.slideIdx, 10);
          if (!isNaN(idx)) {
            Progreso.marcarSlideVisto(this.bloqueId, idx);
            this.updateProgressBar();
          }
        }
      });
    }, { threshold: [0.4] });

    document.querySelectorAll("section[data-slide-idx]").forEach(s => observer.observe(s));
  },

  _fallbackScrollTrack() {
    const sections = document.querySelectorAll("section[data-slide-idx]");
    const midY = window.innerHeight / 2;
    sections.forEach(s => {
      const r = s.getBoundingClientRect();
      if (r.top <= midY && r.bottom >= midY) {
        const idx = parseInt(s.dataset.slideIdx, 10);
        if (!isNaN(idx)) {
          Progreso.marcarSlideVisto(this.bloqueId, idx);
          this.updateProgressBar();
        }
      }
    });
  },

  // ── Navegación inicial: hash o resume prompt ───────────────────────
  handleInitialNavigation() {
    // 1. Si la URL tiene hash (#slide-N), scrollear ahí inmediatamente.
    //    El ancla se sanea antes de buscar: llegó a venir como #slide-0?theme=dark por una
    //    redirección que pegaba la query detrás, y querySelector moría con ese selector.
    if (window.location.hash) {
      const anchor = window.location.hash.slice(1).split("?")[0];
      const target = anchor ? document.getElementById(anchor) : null;
      if (target) {
        setTimeout(() => target.scrollIntoView({ behavior: "instant", block: "start" }), 50);
        return;
      }
    }
    // 2. Si no, comprobar si hay progreso anterior y proponer reanudar
    const b = Progreso.bloque(this.bloqueId);
    if (b.ultimo_slide > 0 && b.slides_vistos.length > 1) {
      this.showResumePrompt(b.ultimo_slide);
    }
  },

  showResumePrompt(slideIdx) {
    const banner = document.createElement("div");
    banner.className = "resume-banner";
    banner.innerHTML = `
      <span>Continúa donde lo dejaste (slide ${slideIdx + 1})</span>
      <button class="btn btn-secondary" id="resume-yes" style="font-size: 12px; padding: 6px 12px;">Continuar</button>
      <button id="resume-no" aria-label="Cerrar" style="background: transparent; color: inherit; border: none; cursor: pointer; font-size: 18px; padding: 0 4px;">×</button>
    `;
    document.body.appendChild(banner);

    document.getElementById("resume-yes").addEventListener("click", () => {
      const target = document.querySelector(`[data-slide-idx="${slideIdx}"]`);
      if (target) target.scrollIntoView({ behavior: "smooth", block: "start" });
      banner.remove();
    });
    document.getElementById("resume-no").addEventListener("click", () => banner.remove());

    // Auto-dismiss tras 12 segundos
    setTimeout(() => banner.remove(), 12000);
  },

  // ── Mount de quizzes y ejercicios ──────────────────────────────────
  mountChecks() {
    // Como todo el bloque vive en el DOM al cargar, montamos todos los
    // quizzes y ejercicios de una vez. Son pocos por bloque.
    document.querySelectorAll("[data-quiz]").forEach(mount => {
      const quizName = mount.dataset.quiz;
      if (window.QUIZZES && window.QUIZZES[quizName] && !mount.dataset.initialized) {
        Quiz.render(mount, window.QUIZZES[quizName], this.bloqueId);
        mount.dataset.initialized = "true";
      }
    });
    document.querySelectorAll("[data-exercise]").forEach(mount => {
      const exName = mount.dataset.exercise;
      if (window.EXERCISES && window.EXERCISES[exName] && !mount.dataset.initialized) {
        Ejercicio.render(mount, window.EXERCISES[exName], this.bloqueId);
        mount.dataset.initialized = "true";
      }
    });
  },
};

window.CursoViewer = CursoViewer;

/* ── tema heredado de la academia ──────────────────────────────────────
   Añadido por el importador. El documento NO elige tema: lo recibe del
   player, que es quien sabe lo que ha elegido la persona. Sin esto, el
   visor tendría un selector propio con los nombres de otra marca. */
(function () {
  function apply(theme) {
    document.documentElement.setAttribute("data-theme", theme === "dark" ? "dark" : "light");
  }

  // El player manda el tema al cargar y cada vez que cambia.
  window.addEventListener("message", function (event) {
    var data = event.data;
    if (data && data.type === "inkoova:theme" && typeof data.theme === "string") {
      apply(data.theme);
    }
  });

  // Y se avisa de que ya se puede recibir: si el documento tarda en cargar, el primer
  // mensaje del player se habría perdido contra una ventana que aún no escuchaba.
  try {
    parent.postMessage({ type: "inkoova:theme-ready", version: 1 }, "*");
  } catch (e) {}
})();
