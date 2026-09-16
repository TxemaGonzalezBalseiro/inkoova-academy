#!/usr/bin/env python3
"""Lo que dice la norma, citado por artículo.

═══════════════════════════════════════════════════════════════════════════════════════════
REGLA DE ESTE FICHERO: aquí NO se escribe nada de memoria.
═══════════════════════════════════════════════════════════════════════════════════════════

Cada cita lleva su artículo y sale de haber abierto el texto oficial en EUR-Lex. Los packs son
instrumentos que alguien aplica a un expediente real: un número de artículo equivocado en una
cláusula contractual no es una errata, es un problema del cliente que la firmó.

Si hay que añadir una cita, se abre la fuente. Si no se puede abrir, se deja `TODO(verificar)`
con la fuente que hay que consultar; un hueco se ve y un dato falso se cree.

Fuentes abiertas para escribir esto:

  · Reglamento (UE) 2024/1689 (AI Act) — DOUE L, 12.7.2024
    https://eur-lex.europa.eu/legal-content/ES/TXT/HTML/?uri=OJ:L_202401689
  · Reglamento (UE) 2022/2554 (DORA)
    https://eur-lex.europa.eu/legal-content/ES/TXT/HTML/?uri=CELEX:32022R2554
  · Reglamento (UE) 2017/745 (MDR)
    https://eur-lex.europa.eu/legal-content/ES/TXT/HTML/?uri=CELEX:32017R0745
"""

# La fecha en que se abrieron las fuentes de arriba. Va impresa en la portada de cada documento.
# No se estima: se cambia el día que alguien vuelva a abrirlas y compruebe que siguen vigentes.
FECHA_VERIFICACION = "2026-09-01"

VERSION = "1.0.0"

AI_ACT = "Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial)"

AVISO = (
    "Material formativo. No constituye asesoramiento jurídico. Las referencias normativas se "
    "verificaron en la fecha indicada en portada contra el texto oficial publicado en EUR-Lex; "
    "revísese con la asesoría jurídica antes de aplicarlo a un expediente real."
)

# ── Anexo III · los ámbitos que hacen que un sistema sea de alto riesgo ───────────────────
#
# Literal del Anexo III del AI Act. Se guarda entero aunque cada pack use dos o tres puntos,
# porque lo que más valor tiene en un checklist es poder descartar: saber que tu caso NO está
# en la lista vale tanto como saber que sí.

ANEXO_III = {
    "1.a": "Sistemas de identificación biométrica remota.",
    "1.b": "Categorización biométrica en función de atributos o características sensibles o "
           "protegidos basada en la inferencia de dichos atributos o características.",
    "1.c": "Reconocimiento de emociones.",
    "2": "Infraestructuras críticas: componentes de seguridad en la gestión y el funcionamiento "
         "de las infraestructuras digitales críticas, del tráfico rodado o del suministro de "
         "agua, gas, calefacción o electricidad.",
    "3.a": "Determinar el acceso o la admisión de personas físicas a centros educativos y de "
           "formación profesional, o distribuirlas entre dichos centros.",
    "3.b": "Evaluar los resultados del aprendizaje, también cuando se usen para orientar el "
           "proceso de aprendizaje.",
    "3.c": "Evaluar el nivel de educación adecuado que recibirá una persona o al que podrá "
           "acceder.",
    "3.d": "Seguimiento y detección de comportamientos prohibidos por parte de los estudiantes "
           "durante los exámenes.",
    "4.a": "Contratación o selección de personas físicas, en particular para publicar anuncios "
           "de empleo específicos, analizar y filtrar las solicitudes y evaluar a los "
           "candidatos.",
    "4.b": "Decisiones que afecten a las condiciones de las relaciones laborales, a la promoción "
           "o rescisión de relaciones contractuales, asignación de tareas a partir de "
           "comportamientos individuales o rasgos personales, y supervisión y evaluación del "
           "rendimiento y el comportamiento.",
    "5.a": "Uso por autoridades públicas, o en su nombre, para evaluar la admisibilidad de "
           "personas físicas a servicios y prestaciones esenciales de asistencia pública "
           "—incluidos los de asistencia sanitaria— y para concederlos, reducirlos, retirarlos o "
           "reclamar su devolución.",
    "5.b": "Evaluar la solvencia de personas físicas o establecer su calificación crediticia, "
           "SALVO los sistemas utilizados al objeto de detectar fraudes financieros.",
    "5.c": "Evaluación de riesgos y fijación de precios en relación con personas físicas en el "
           "caso de los seguros DE VIDA Y DE SALUD.",
    "5.d": "Evaluación y clasificación de llamadas de emergencia, envío o priorización de "
           "servicios de primera intervención, y sistemas de triaje de pacientes en urgencias.",
    "6": "Garantía del cumplimiento del Derecho (letras a a e: evaluación del riesgo de "
         "victimización, polígrafos, fiabilidad de las pruebas, riesgo de delinquir o reincidir, "
         "y elaboración de perfiles durante la investigación).",
    "7": "Migración, asilo y gestión del control fronterizo (letras a a d).",
    "8": "Administración de justicia y procesos democráticos.",
}

# ── Artículo 6 · el filtro que decide si de verdad es de alto riesgo ──────────────────────
#
# El apartado 3 es lo que más se ignora y lo que más cambia el resultado: estar en el Anexo III
# no basta. Y su párrafo final es lo que hace que ese filtro no sirva de coartada.

FILTRO_ART6_3 = [
    ("a", "El sistema está destinado a realizar una tarea de procedimiento limitada."),
    ("b", "El sistema está destinado a mejorar el resultado de una actividad humana previamente "
          "realizada."),
    ("c", "El sistema está destinado a detectar patrones de toma de decisiones o desviaciones "
          "respecto de patrones anteriores, y NO a sustituir la valoración humana previamente "
          "realizada sin una revisión humana adecuada, ni a influir en ella."),
    ("d", "El sistema está destinado a realizar una tarea preparatoria para una evaluación "
          "pertinente a efectos de los casos del Anexo III."),
]

EXCEPCION_PERFILES = (
    "No obstante, los sistemas del Anexo III SIEMPRE se considerarán de alto riesgo cuando el "
    "sistema efectúe la elaboración de perfiles de personas físicas (art. 6.3, párrafo final)."
)

DOCUMENTAR_NO_ALTO_RIESGO = (
    "Si se concluye que un sistema del Anexo III no es de alto riesgo, esa evaluación debe "
    "DOCUMENTARSE antes de introducirlo en el mercado o ponerlo en servicio, y facilitarse a "
    "las autoridades nacionales que la pidan (art. 6.4). La conclusión sin el expediente que la "
    "sostiene no vale: es justamente lo que la autoridad va a pedir."
)

# ── Artículo 26 · lo que tiene que hacer quien USA el sistema ─────────────────────────────
#
# Es el artículo que más aplica a un cliente de la academia: casi nadie fabrica el modelo, casi
# todo el mundo lo despliega.

OBLIGACIONES_DESPLIEGUE = [
    ("26.1", "Usar el sistema conforme a las instrucciones de uso que lo acompañan, con medidas "
             "técnicas y organizativas adecuadas."),
    ("26.2", "Encomendar la supervisión humana a personas con la competencia, la formación y la "
             "AUTORIDAD necesarias. Las tres cosas: alguien formado y sin autoridad para parar "
             "el sistema no es supervisión."),
    ("26.4", "Asegurarse de que los datos de entrada sean pertinentes y suficientemente "
             "representativos, en la medida en que se ejerza control sobre ellos."),
    ("26.5", "Vigilar el funcionamiento. Si hay motivos para creer que el uso conforme a las "
             "instrucciones puede presentar un riesgo del art. 79.1: informar sin demora indebida "
             "al proveedor o distribuidor y a la autoridad de vigilancia del mercado, y SUSPENDER "
             "el uso. Ante un incidente grave: informar primero al proveedor, después al "
             "importador o distribuidor y a la autoridad."),
    ("26.6", "Conservar los archivos de registro que el sistema genere automáticamente, en la "
             "medida en que estén bajo control propio, durante un periodo adecuado a la "
             "finalidad y DE AL MENOS SEIS MESES, salvo que otra norma diga otra cosa."),
    ("26.7", "Si se usa en el lugar de trabajo: informar a los representantes de los trabajadores "
             "y a los trabajadores afectados ANTES de ponerlo en servicio."),
    ("26.9", "Usar la información del art. 13 para la evaluación de impacto relativa a la "
             "protección de datos del art. 35 del RGPD, cuando proceda."),
    ("26.11", "Informar a las personas físicas de que están expuestas al sistema, cuando este "
              "tome o ayude a tomar decisiones que les afecten."),
    ("26.12", "Cooperar con las autoridades competentes."),
]

EXCEPCION_FINANCIERAS = (
    "Entidades financieras sujetas a requisitos de gobernanza interna por el Derecho de la Unión "
    "en materia de servicios financieros: la obligación de vigilancia del art. 26.5 se considera "
    "cumplida al respetar sus normas de gobernanza interna, y los archivos de registro se "
    "mantienen como parte de la documentación que ya conservan por esa normativa (art. 26.5 y "
    "26.6, párrafos segundos). No exime de las demás obligaciones."
)

# ── Artículo 12 · qué tiene que registrar el sistema ─────────────────────────────────────

REGISTROS_ART12 = [
    ("12.1", "El sistema permitirá técnicamente el registro automático de acontecimientos a lo "
             "largo de todo su ciclo de vida."),
    ("12.2.a", "Los registros permitirán detectar situaciones que puedan dar lugar a que el "
               "sistema presente un riesgo en el sentido del art. 79.1, o a una modificación "
               "sustancial."),
    ("12.2.b", "Facilitarán la vigilancia poscomercialización del art. 72."),
    ("12.2.c", "Permitirán vigilar el funcionamiento conforme al art. 26.5."),
]

# ── Artículo 25 · el reparto de responsabilidades con el proveedor ───────────────────────
#
# Es la base legal de las cláusulas modelo: el 25.4 exige acuerdo ESCRITO, y el 25.1 dice las
# tres formas de convertirse en proveedor sin pretenderlo.

CONVERTIRSE_EN_PROVEEDOR = [
    ("25.1.a", "Poner el nombre o la marca propios en un sistema de alto riesgo ya introducido "
               "en el mercado, sin perjuicio de los acuerdos contractuales que asignen las "
               "obligaciones de otro modo."),
    ("25.1.b", "Modificar sustancialmente un sistema de alto riesgo ya introducido, de modo que "
               "siga siendo de alto riesgo."),
    ("25.1.c", "Modificar la finalidad prevista de un sistema —incluido uno de uso general— que "
               "no se consideraba de alto riesgo, de modo que pase a serlo."),
]

ACUERDO_ESCRITO_25_4 = (
    "El proveedor del sistema de alto riesgo y el tercero que le suministre sistemas, "
    "herramientas, servicios, componentes o procesos integrados en él especificarán MEDIANTE "
    "ACUERDO ESCRITO la información, las capacidades, el acceso técnico y la asistencia "
    "necesarios para que el proveedor pueda cumplir el Reglamento (art. 25.4). No se aplica a "
    "terceros que publiquen herramientas o componentes bajo licencia libre y de código abierto, "
    "salvo modelos de IA de uso general."
)
