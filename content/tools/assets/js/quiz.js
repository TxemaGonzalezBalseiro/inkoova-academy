// ════════════════════════════════════════════════════════════════════
// Quiz interactivo + ejercicios autocorregidos
// ════════════════════════════════════════════════════════════════════

const Quiz = {
  // Renderiza un quiz en el elemento dado
  // questions: array de { q, options: [{text, correct}], explanation }
  render(containerEl, questions, bloqueId, onComplete) {
    let currentQ = 0;
    let answers = []; // {questionIdx, selectedIdx, isCorrect}

    function renderQuestion() {
      const q = questions[currentQ];
      const total = questions.length;
      containerEl.innerHTML = `
        <div class="quiz-progress">
          <span>Pregunta ${currentQ + 1} de ${total}</span>
          <div class="quiz-progress-bar">
            <div class="quiz-progress-bar-fill" style="width: ${((currentQ) / total) * 100}%"></div>
          </div>
          <span>${currentQ}/${total} respondidas</span>
        </div>
        <div class="quiz-question">
          <span class="q-num">${currentQ + 1}</span>${q.q}
        </div>
        <div class="quiz-options" id="quiz-opts">
          ${q.options.map((opt, i) => `
            <button class="quiz-option" data-idx="${i}">
              <span class="letter">${String.fromCharCode(65 + i)}</span>
              <span>${opt.text}</span>
            </button>
          `).join("")}
        </div>
        <div id="quiz-feedback"></div>
        <div class="quiz-actions">
          <span></span>
          <button class="btn btn-primary" id="quiz-next" disabled>
            ${currentQ < total - 1 ? "Siguiente →" : "Ver resultado →"}
          </button>
        </div>
      `;

      const optionsEls = containerEl.querySelectorAll(".quiz-option");
      const feedbackEl = containerEl.querySelector("#quiz-feedback");
      const nextBtn = containerEl.querySelector("#quiz-next");

      optionsEls.forEach(opt => {
        opt.addEventListener("click", () => {
          const idx = parseInt(opt.dataset.idx);
          const correctIdx = q.options.findIndex(o => o.correct);
          const isCorrect = idx === correctIdx;

          optionsEls.forEach(o => o.classList.add("disabled"));
          opt.classList.add(isCorrect ? "correct" : "incorrect");
          if (!isCorrect) {
            optionsEls[correctIdx].classList.add("correct");
          }

          feedbackEl.innerHTML = `
            <div class="quiz-feedback ${isCorrect ? "correct" : "incorrect"}">
              <strong>${isCorrect ? "✓ Correcto." : "✗ Incorrecto."}</strong>
              ${q.explanation || ""}
            </div>
          `;

          answers[currentQ] = { questionIdx: currentQ, selectedIdx: idx, isCorrect };
          nextBtn.disabled = false;
        });
      });

      nextBtn.addEventListener("click", () => {
        if (currentQ < questions.length - 1) {
          currentQ++;
          renderQuestion();
        } else {
          renderResults();
        }
      });
    }

    function renderResults() {
      const correctCount = answers.filter(a => a && a.isCorrect).length;
      const total = questions.length;
      const pct = Math.round((correctCount / total) * 100);
      const passed = pct >= 60;

      containerEl.innerHTML = `
        <div class="quiz-results">
          <div class="quiz-score-circle ${passed ? "pass" : "fail"}">
            ${correctCount}/${total}
          </div>
          <h3 style="margin-bottom: 8px;">${passed ? "¡Quiz superado!" : "Casi lo logras"}</h3>
          <p style="color: var(--color-text-soft); margin-bottom: 24px;">
            ${passed
              ? `Has acertado ${correctCount} de ${total} (${pct}%). El siguiente bloque te espera.`
              : `Has acertado ${correctCount} de ${total} (${pct}%). Te recomiendo repasar las preguntas falladas antes de continuar.`}
          </p>
          <div style="display: flex; gap: 12px; justify-content: center;">
            <button class="btn btn-secondary" onclick="location.reload()">Repetir quiz</button>
            <button class="btn btn-primary" id="quiz-finish">Continuar al siguiente bloque</button>
          </div>
        </div>
      `;

      // Persistir
      if (bloqueId) {
        Progreso.guardarQuiz(bloqueId, correctCount, total);
      }

      const finishBtn = containerEl.querySelector("#quiz-finish");
      finishBtn.addEventListener("click", () => {
        if (onComplete) onComplete(correctCount, total);
        else window.location.href = "../index.html";
      });
    }

    renderQuestion();
  },
};

// ────────────────────────────────────────────────────────────────────
// Ejercicios autocorregidos (open-text)
// criteria: array de { type, pattern, label, weight? }
//   type: "contains" | "regex" | "mentions_any"
// ────────────────────────────────────────────────────────────────────

const Ejercicio = {
  render(containerEl, exercise, bloqueId, onSubmit) {
    containerEl.innerHTML = `
      <div class="exercise-title">Ejercicio · ${exercise.title || ""}</div>
      <div class="exercise-prompt">${exercise.prompt}</div>
      <textarea class="exercise-textarea" id="ex-input" placeholder="${exercise.placeholder || "Escribe tu respuesta aquí..."}"></textarea>
      <div class="exercise-criteria">
        <strong>Criterios de evaluación automática:</strong>
        <ul>
          ${exercise.criteria.map(c => `<li>${c.label}</li>`).join("")}
        </ul>
      </div>
      <div style="display: flex; gap: 10px; margin-top: 16px;">
        <button class="btn btn-primary" id="ex-submit">Comprobar respuesta</button>
        <button class="btn btn-secondary" id="ex-clear">Limpiar</button>
      </div>
      <div id="ex-feedback" style="margin-top: 16px;"></div>
    `;

    const inputEl = containerEl.querySelector("#ex-input");
    const submitBtn = containerEl.querySelector("#ex-submit");
    const clearBtn = containerEl.querySelector("#ex-clear");
    const feedbackEl = containerEl.querySelector("#ex-feedback");

    submitBtn.addEventListener("click", () => {
      const text = inputEl.value.trim();
      if (!text) {
        feedbackEl.innerHTML = `
          <div class="quiz-feedback incorrect">
            <strong>Respuesta vacía.</strong> Escribe algo antes de comprobar.
          </div>`;
        return;
      }

      // Evaluar criterios
      const results = exercise.criteria.map(c => {
        let pass = false;
        const lowerText = text.toLowerCase();
        if (c.type === "contains") {
          pass = lowerText.includes(c.pattern.toLowerCase());
        } else if (c.type === "regex") {
          pass = new RegExp(c.pattern, "i").test(text);
        } else if (c.type === "mentions_any") {
          pass = c.pattern.some(p => lowerText.includes(p.toLowerCase()));
        } else if (c.type === "min_length") {
          pass = text.length >= c.pattern;
        }
        return { ...c, pass };
      });

      const passedCount = results.filter(r => r.pass).length;
      const totalCriteria = results.length;
      const allPass = passedCount === totalCriteria;

      feedbackEl.innerHTML = `
        <div class="quiz-feedback ${allPass ? "correct" : (passedCount > totalCriteria / 2 ? "correct" : "incorrect")}">
          <strong>${allPass ? "✓ Respuesta completa." : `${passedCount}/${totalCriteria} criterios cumplidos.`}</strong>
          <ul style="margin: 8px 0 0; padding-left: 20px;">
            ${results.map(r => `
              <li style="color: ${r.pass ? "var(--color-success)" : "var(--color-error)"};">
                ${r.pass ? "✓" : "✗"} ${r.label}
              </li>
            `).join("")}
          </ul>
          ${exercise.model_answer && allPass ? `
            <div style="margin-top: 14px; padding-top: 12px; border-top: 1px solid #86efac;">
              <strong>Respuesta modelo (referencia):</strong>
              <div style="margin-top: 6px; font-style: italic;">${exercise.model_answer}</div>
            </div>
          ` : ""}
          ${!allPass && exercise.hint ? `
            <div style="margin-top: 12px; font-style: italic; color: var(--color-text-soft);">
              💡 Pista: ${exercise.hint}
            </div>
          ` : ""}
        </div>
      `;

      if (allPass && bloqueId && exercise.id) {
        Progreso.marcarEjercicioCompletado(bloqueId, exercise.id);
      }
      if (onSubmit) onSubmit(passedCount, totalCriteria);
    });

    clearBtn.addEventListener("click", () => {
      inputEl.value = "";
      feedbackEl.innerHTML = "";
    });
  },
};

window.Quiz = Quiz;
window.Ejercicio = Ejercicio;
