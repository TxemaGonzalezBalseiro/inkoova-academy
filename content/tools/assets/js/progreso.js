// ════════════════════════════════════════════════════════════════════
// Progreso del curso — persistencia en localStorage
// ════════════════════════════════════════════════════════════════════

const STORAGE_KEY = "agent-engineering-v3-progreso";

const Progreso = {
  // Carga el estado completo
  load() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      return raw ? JSON.parse(raw) : { bloques: {}, version: "1.0" };
    } catch (e) {
      console.warn("Error cargando progreso:", e);
      return { bloques: {}, version: "1.0" };
    }
  },

  save(state) {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    } catch (e) {
      console.warn("Error guardando progreso:", e);
    }
  },

  // Devuelve estado de un bloque concreto
  bloque(bloqueId) {
    const state = this.load();
    if (!state.bloques[bloqueId]) {
      state.bloques[bloqueId] = {
        slides_vistos: [],
        ultimo_slide: 0,
        quiz_completado: false,
        quiz_score: null,
        quiz_intentos: 0,
        ejercicios_completados: [],
        ultima_visita: null,
      };
    }
    return state.bloques[bloqueId];
  },

  // Marca un slide como visto
  marcarSlideVisto(bloqueId, slideIdx) {
    const state = this.load();
    const b = state.bloques[bloqueId] || this.bloque(bloqueId);
    if (!b.slides_vistos.includes(slideIdx)) {
      b.slides_vistos.push(slideIdx);
    }
    b.ultimo_slide = slideIdx;
    b.ultima_visita = new Date().toISOString();
    state.bloques[bloqueId] = b;
    this.save(state);
  },

  // Marca quiz completado con score
  guardarQuiz(bloqueId, score, total) {
    const state = this.load();
    const b = state.bloques[bloqueId] || this.bloque(bloqueId);
    b.quiz_completado = true;
    b.quiz_score = { aciertos: score, total: total };
    b.quiz_intentos = (b.quiz_intentos || 0) + 1;
    state.bloques[bloqueId] = b;
    this.save(state);
  },

  // Marca un ejercicio como completado
  marcarEjercicioCompletado(bloqueId, ejercicioId) {
    const state = this.load();
    const b = state.bloques[bloqueId] || this.bloque(bloqueId);
    if (!b.ejercicios_completados.includes(ejercicioId)) {
      b.ejercicios_completados.push(ejercicioId);
    }
    state.bloques[bloqueId] = b;
    this.save(state);
  },

  // % de progreso de un bloque (slides + quiz + ejercicios)
  porcentajeBloque(bloqueId, totalSlides, totalEjercicios = 0) {
    const b = this.bloque(bloqueId);
    const slidesPeso = 0.7;
    const quizPeso = 0.2;
    const ejerciciosPeso = 0.1;

    const slidesPct = totalSlides > 0
      ? (b.slides_vistos.length / totalSlides)
      : 0;
    const quizPct = b.quiz_completado ? 1 : 0;
    const ejerciciosPct = totalEjercicios > 0
      ? (b.ejercicios_completados.length / totalEjercicios)
      : (totalEjercicios === 0 ? 1 : 0);

    const total = (slidesPct * slidesPeso) +
                  (quizPct * quizPeso) +
                  (ejerciciosPct * ejerciciosPeso);
    return Math.round(total * 100);
  },

  reset() {
    if (confirm("¿Borrar todo el progreso del curso? Esta acción no se puede deshacer.")) {
      localStorage.removeItem(STORAGE_KEY);
      location.reload();
    }
  },
};

// Exponer en window para acceso global desde la consola y otros scripts
window.Progreso = Progreso;
