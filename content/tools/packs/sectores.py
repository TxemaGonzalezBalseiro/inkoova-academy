#!/usr/bin/env python3
"""Los seis packs: qué del Anexo III les toca, qué norma sectorial añade y su caso resuelto.

Lo común —el árbol del art. 6, las obligaciones del 26, el registro del 12, el acuerdo del 25—
está en `fuentes.py` y sale igual en los seis. Aquí solo va lo que cambia de un sector a otro,
que es sobre todo DÓNDE cae cada uno en el Anexo III y qué matiz tiene ese encaje.

El matiz es lo que más valor tiene. «Seguros» no está entero en el Anexo III: solo vida y salud.
«Retail» casi nunca está, salvo si financia. Saber que NO estás en la lista, y poder defenderlo,
ahorra un expediente entero.
"""
from __future__ import annotations

from dataclasses import dataclass, field


@dataclass(frozen=True)
class Pack:
    slug: str
    sector: str
    anexo_iii: list[str]
    fuentes: list[str]
    caso_titulo: str
    caso: list[tuple[str, list]]
    matiz_anexo: str = ""
    riesgos_ejemplo: list[tuple[str, str, str]] = field(default_factory=list)
    notas_registro: list[str] = field(default_factory=list)
    clausulas_sector_intro: str = ""
    clausulas_sector: list[tuple[str, str]] = field(default_factory=list)


AI_ACT_FUENTE = (
    "Reglamento (UE) 2024/1689 (Reglamento de Inteligencia Artificial), DOUE L de 12.7.2024. "
    "Texto consultado en EUR-Lex."
)

# ── C-15 · Seguros y financiero ──────────────────────────────────────────────────────────

SEGUROS = Pack(
    slug="pack-seguros-financiero",
    sector="Seguros y financiero",
    anexo_iii=["5.b", "5.c"],
    matiz_anexo=(
        "el Anexo III.5.c cubre la evaluación de riesgos y la fijación de precios ÚNICAMENTE en "
        "los seguros de vida y de salud. Un modelo que tarifica un seguro de hogar o de "
        "automóvil no entra por esa vía. Y el 5.b excluye expresamente los sistemas destinados a "
        "detectar fraudes financieros: un motor antifraude no es alto riesgo por ser antifraude, "
        "aunque puntúe a personas. Las dos son delimitaciones que se pasan por alto a menudo, en "
        "las dos direcciones."
    ),
    fuentes=[
        AI_ACT_FUENTE,
        "Reglamento (UE) 2022/2554 (DORA), art. 30 «Cláusulas contractuales fundamentales». "
        "Texto consultado en EUR-Lex.",
    ],
    riesgos_ejemplo=[
        ("El modelo de tarificación de vida penaliza sistemáticamente a un colectivo por una "
         "variable correlacionada con el estado de salud que no se usa de forma explícita",
         "Solicitantes de seguro de vida",
         "Prueba de disparidad por grupo en cada reentrenamiento, con umbral acordado y bloqueo "
         "del despliegue si se supera"),
        ("El scoring rechaza y nadie sabe explicar por qué a la persona que lo pide",
         "Solicitantes de crédito",
         "Registrar los factores que más pesaron en cada decisión y entregarlos al equipo de "
         "atención, no solo la puntuación"),
        ("El sistema se usa fuera de su finalidad prevista: se contrató para priorizar y se "
         "acaba usando para decidir",
         "Personas evaluadas",
         "Revisión trimestral del uso real contra la finalidad declarada, con acta"),
    ],
    notas_registro=[
        "Entidades financieras: el art. 26.5 y el 26.6 tienen un párrafo propio. La vigilancia se "
        "considera cumplida al respetar las normas de gobernanza interna del Derecho de la Unión "
        "en servicios financieros, y los registros se mantienen dentro de la documentación que ya "
        "se conserva por esa normativa. No exime del resto de obligaciones.",
        "",
        "Y hay una obligación que se olvida: quien despliega sistemas del Anexo III.5.b y 5.c "
        "tiene que hacer la evaluación de impacto sobre derechos fundamentales del art. 27, igual "
        "que un organismo público. Es de las pocas veces que el Reglamento pone a una entidad "
        "privada en el mismo sitio que a la Administración.",
    ],
    clausulas_sector_intro=(
        "Si la entidad está sujeta a DORA, el contrato con el proveedor de servicios de TIC "
        "tiene que incluir además los elementos del art. 30 de ese Reglamento. Estos son los que "
        "se solapan con un contrato de IA y conviene redactar una sola vez:"
    ),
    clausulas_sector=[
        ("Asignación por escrito y nivel de servicio (DORA art. 30.1 y 30.2.e)",
         "Los derechos y obligaciones de las partes se asignan por escrito. El contrato completo "
         "incluye los acuerdos de nivel de servicio, en documento descargable, duradero y "
         "accesible."),
        ("Localización y subcontratación (DORA art. 30.2.a y 30.2.b)",
         "Se describen las funciones y servicios prestados, si se permite subcontratar los que "
         "sustenten una función esencial o importante y en qué condiciones, y los países donde "
         "se prestan y donde se tratan y almacenan los datos, con notificación por adelantado de "
         "cualquier cambio de ubicación."),
        ("Recuperación de los datos (DORA art. 30.2.d)",
         "EL CLIENTE podrá acceder a los datos personales y no personales tratados, recuperarlos "
         "y que le sean devueltos en formato fácilmente accesible en caso de insolvencia, "
         "resolución o interrupción de la actividad del proveedor, o de terminación del contrato."),
        ("Asistencia ante incidentes (DORA art. 30.2.f)",
         "El PROVEEDOR prestará asistencia sin coste adicional —o a un coste fijado de antemano— "
         "cuando se produzca un incidente de TIC relacionado con el servicio prestado."),
        ("Cooperación con autoridades y terminación (DORA art. 30.2.g y 30.2.h)",
         "El PROVEEDOR cooperará plenamente con las autoridades competentes y de resolución. Se "
         "fijan los derechos de terminación y sus plazos mínimos de notificación."),
    ],
    caso_titulo="Caso resuelto · Meridiana Seguros",
    caso=[
        ("El caso",
         ["Meridiana es una aseguradora generalista. Quiere usar un sistema que lee el parte de "
          "un siniestro y propone una de tres vías: pago directo, peritación o revisión "
          "antifraude. La propuesta la revisa siempre un tramitador, que la confirma o la cambia.",
          "La pregunta del expediente no es «¿usamos IA?». Es: <b>¿es esto un sistema de alto "
          "riesgo?</b> Y la respuesta cambia según cómo esté montado, no según lo que haga el "
          "modelo por dentro."]),
        ("Paso 1 · ¿Producto regulado?",
         ["No. El sistema no es componente de seguridad de ningún producto de los actos de "
          "armonización del Anexo I, ni es él mismo uno de esos productos. Descartado el art. 6.1."]),
        ("Paso 2 · ¿Anexo III?",
         ["Aquí es donde se equivocan casi todos. El reflejo es: «es un seguro, luego 5.c».",
          "Pero el 5.c dice <b>seguros de vida y de salud</b>. Meridiana tramita hogar y "
          "automóvil. Por esa letra no entra.",
          "¿Y el 5.b, solvencia y calificación crediticia? Tampoco: no se evalúa la solvencia de "
          "nadie, se clasifica un siniestro ya ocurrido de un cliente que ya tiene póliza.",
          "Queda la vía antifraude. El 5.b <b>excluye</b> expresamente los sistemas destinados a "
          "detectar fraudes financieros, así que ni siquiera esa rama lo mete en la lista.",
          "<b>Conclusión provisional: el sistema no está en el Anexo III.</b>"]),
        ("Paso 3 · Lo que hay que hacer con esa conclusión",
         ["No es «entonces no hay nada que hacer». El art. 6.4 solo obliga a documentar la "
          "evaluación cuando el sistema SÍ está en el Anexo III y se concluye que no es de alto "
          "riesgo. Aquí ni siquiera está en la lista.",
          "Pero la conclusión hay que poder defenderla, y eso exige haber escrito por qué: qué "
          "ramos tramita, qué decide exactamente el sistema y qué decide la persona. Sin ese "
          "papel, la respuesta a la autoridad es una opinión.",
          "Y hay una trampa: <b>si mañana Meridiana lanza un ramo de salud y conecta el mismo "
          "sistema, el encaje cambia.</b> La clasificación no es del sistema para siempre: es "
          "del sistema con su finalidad prevista de hoy."]),
        ("Paso 4 · Qué sí aplica de todas formas",
         ["Que no sea de alto riesgo no lo deja fuera del resto del ordenamiento:",
          ["El RGPD sigue aplicando entero: hay datos personales y hay decisiones que afectan a "
           "personas.",
           "Si en algún momento el sistema decidiera solo, sin revisión humana efectiva, entra "
           "el art. 22 del RGPD sobre decisiones individuales automatizadas.",
           "La normativa de seguros y la supervisión propia del sector no dependen del AI Act.",
           "Y si Meridiana está sujeta a DORA por su perfil, el contrato con el proveedor del "
           "modelo tiene que cumplir el art. 30 aunque el sistema no sea de alto riesgo."]]),
        ("Paso 5 · Qué habría cambiado la respuesta",
         ["Tres cambios, cualquiera de ellos, lo habrían metido en alto riesgo:",
          ["Que el ramo fuera vida o salud (Anexo III.5.c).",
           "Que el sistema puntuara la solvencia del asegurado para decidir la renovación "
           "(Anexo III.5.b, y ahí el antifraude ya no sirve de excusa porque la finalidad sería "
           "otra).",
           "Que el sistema elaborara perfiles de personas físicas. Esto es lo más importante del "
           "artículo 6 y lo que menos se cita: si hay elaboración de perfiles, el filtro del 6.3 "
           "<b>no se puede usar</b>. Un sistema del Anexo III que perfila es de alto riesgo, "
           "aunque solo prepare el trabajo de una persona."]]),
        ("Lo que se lleva el expediente",
         ["Una clasificación documentada, con fecha, que dice qué se evaluó y por qué se "
          "concluyó lo que se concluyó. Y una fecha de revisión: la clasificación caduca cuando "
          "cambia el producto, no cuando cambia la ley."]),
    ],
)

# ── C-16 · Salud ─────────────────────────────────────────────────────────────────────────

SALUD = Pack(
    slug="pack-salud",
    sector="Salud",
    anexo_iii=["5.a", "5.d"],
    matiz_anexo=(
        "en salud la vía que más pesa NO suele ser el Anexo III, sino el art. 6.1: si el software "
        "es un producto sanitario y necesita evaluación de la conformidad por un tercero, ya es "
        "de alto riesgo por esa puerta. El MDR define «producto sanitario» incluyendo "
        "expresamente el «programa informático» destinado a diagnóstico, prevención, seguimiento, "
        "predicción, pronóstico, tratamiento o alivio de una enfermedad. Un modelo que predice "
        "una complicación clínica cae ahí antes de que nadie mire el Anexo III."
    ),
    fuentes=[
        AI_ACT_FUENTE,
        "Reglamento (UE) 2017/745 (MDR), art. 2.1 «producto sanitario». Texto consultado en "
        "EUR-Lex.",
        "Reglamento (UE) 2017/745 (MDR), Anexo VIII, Regla 11 «Reglas de clasificación». "
        "Texto consultado en EUR-Lex.",
        "Reglamento (UE) 2017/745 (MDR), conservación de la documentación técnica. Texto "
        "consultado en EUR-Lex.",
    ],
    riesgos_ejemplo=[
        ("El modelo se entrenó con población de un perfil y se despliega sobre otro, y rinde peor "
         "en el segundo sin que nadie lo mida",
         "Pacientes del grupo infrarrepresentado",
         "Medir el rendimiento por subgrupo antes de desplegar y en cada revisión, no solo el "
         "agregado"),
        ("El clínico deja de revisar porque el sistema acierta casi siempre",
         "Pacientes de los casos en que falla",
         "Medir la tasa de discrepancia entre propuesta y decisión final; si baja a cero, la "
         "supervisión ha dejado de existir"),
        ("Se usa un sistema con finalidad de investigación para tomar decisiones asistenciales",
         "Pacientes",
         "Separar entornos y credenciales; el sistema de investigación no debe ser alcanzable "
         "desde la estación clínica"),
    ],
    notas_registro=[
        "Si el sistema es producto sanitario, la trazabilidad que exige el MDR y la del art. 12 "
        "del AI Act se solapan: se diseña un registro que sirva a las dos y no dos registros.",
        "",
        "El MDR obliga a mantener la documentación a disposición de las autoridades durante al "
        "menos DIEZ años desde que el último producto se introdujo en el mercado —quince en "
        "productos implantables—. Es mucho más que los seis meses del art. 26.6 del AI Act, y "
        "manda el plazo mayor.",
    ],
    clausulas_sector_intro=(
        "Cuando el sistema es o forma parte de un producto sanitario, el reparto con el "
        "fabricante cambia de naturaleza. Estas cláusulas cubren ese solape:"
    ),
    clausulas_sector=[
        ("Condición de producto sanitario",
         "El PROVEEDOR declara si el sistema constituye producto sanitario conforme al art. 2.1 "
         "del Reglamento (UE) 2017/745, su clase y si ha sido sometido a evaluación de la "
         "conformidad por organismo notificado, aportando la documentación acreditativa."),
        ("Fabricante del producto",
         "Las partes reconocen que, cuando el sistema sea componente de seguridad de un producto "
         "del Anexo I sección A del AI Act y se comercialice bajo la marca del fabricante del "
         "producto, este es considerado proveedor del sistema de alto riesgo (art. 25.3)."),
        ("Vigilancia y notificación",
         "El PROVEEDOR informará a EL CLIENTE de cualquier acción correctiva de seguridad o "
         "notificación a autoridades sanitarias que afecte al sistema, en [PLAZO] desde que la "
         "adopte o la reciba."),
    ],
    caso_titulo="Caso resuelto · triaje asistido en un servicio de urgencias",
    caso=[
        ("El caso",
         ["Un hospital quiere usar un sistema que, a partir de los datos de admisión, propone un "
          "nivel de prioridad para cada paciente que llega a urgencias. La enfermera de triaje ve "
          "la propuesta y asigna el nivel definitivo."]),
        ("Paso 1 · Aquí hay que empezar por el final",
         ["El reflejo es ir al Anexo III. En salud eso es un error de orden: hay que preguntar "
          "primero si el software es <b>producto sanitario</b>, porque si lo es y necesita "
          "evaluación por un tercero, ya es de alto riesgo por el art. 6.1 y lo demás sobra.",
          "El MDR define producto sanitario incluyendo el «programa informático» destinado, entre "
          "otros fines, a «diagnóstico, prevención, seguimiento, predicción, pronóstico, "
          "tratamiento o alivio de una enfermedad». Un sistema que <b>predice</b> la gravedad "
          "para priorizar la atención encaja en esa definición con naturalidad.",
          "La clase sale de la Regla 11 del Anexo VIII: los programas informáticos destinados a "
          "proporcionar información que se usa para tomar decisiones con fines terapéuticos o de "
          "diagnóstico se clasifican en <b>clase IIa</b>, salvo si esas decisiones pueden causar "
          "la muerte o un deterioro irreversible —<b>clase III</b>— o un deterioro grave o una "
          "intervención quirúrgica —<b>clase IIb</b>—.",
          "Un triaje de urgencias mal priorizado puede retrasar la atención de un paciente "
          "crítico. La conversación con el organismo notificado no empieza en la clase I."]),
        ("Paso 2 · Y además está en el Anexo III",
         ["Con independencia de lo anterior, el Anexo III.5.d cubre expresamente «los sistemas de "
          "triaje de pacientes en el contexto de la asistencia sanitaria de urgencia».",
          "Es decir: por las dos vías. Y cuando entra por el art. 6.1, el filtro del 6.3 no "
          "aplica —ese filtro es solo para los del apartado 2, los del Anexo III—."]),
        ("Paso 3 · ¿Salva el filtro del 6.3, por la vía del Anexo III?",
         ["Supongamos, a efectos del ejercicio, que no fuera producto sanitario. ¿Serviría "
          "argumentar que solo «mejora el resultado de una actividad humana previamente "
          "realizada» (art. 6.3.b)?",
          "Difícilmente. La propuesta llega ANTES de que la enfermera decida, no después de una "
          "valoración previa que el sistema mejore. Y sobre todo: influye sustancialmente en el "
          "resultado, que es justo lo que el 6.3 exige descartar.",
          "El argumento honesto es el contrario: en urgencias, una propuesta de prioridad influye "
          "aunque haya una persona firmando."]),
        ("Paso 4 · Lo que toca hacer",
         ["Siendo de alto riesgo, y como responsable del despliegue:",
          ["Supervisión humana con competencia, formación y <b>autoridad</b> para apartarse de la "
           "propuesta (art. 26.2). Autoridad significa que apartarse no sea algo que haya que "
           "justificar ante nadie.",
           "Registros conservados al menos seis meses (art. 26.6), y más si el MDR o la normativa "
           "de historia clínica exigen más.",
           "Informar a los pacientes de que están expuestos al sistema (art. 26.11).",
           "Un hospital público, o privado prestando servicio público, tiene que hacer además la "
           "evaluación de impacto sobre derechos fundamentales del art. 27."]]),
        ("Lo que se lleva el expediente",
         ["La conclusión de que es de alto riesgo por dos vías independientes, y la más "
          "importante de las dos —producto sanitario— con su expediente MDR abierto, que es un "
          "trabajo mucho mayor que el del AI Act y que hay que empezar antes."]),
    ],
)

# ── C-17 · Recursos humanos ──────────────────────────────────────────────────────────────

RRHH = Pack(
    slug="pack-rrhh",
    sector="Recursos humanos",
    anexo_iii=["4.a", "4.b"],
    matiz_anexo=(
        "el punto 4 es de los más amplios del Anexo III y de los que menos escapatoria dejan. "
        "Cubre no solo seleccionar, sino «publicar anuncios de empleo específicos» y «analizar y "
        "filtrar las solicitudes». Y el 4.b llega a la asignación de tareas y a la supervisión "
        "del rendimiento. Casi cualquier herramienta de RR. HH. con IA que toque a una persona "
        "concreta entra por alguna de las dos letras."
    ),
    fuentes=[
        AI_ACT_FUENTE,
        "Real Decreto Legislativo 2/2015, Estatuto de los Trabajadores, art. 64.4.d. Texto "
        "consolidado consultado en el BOE.",
    ],
    riesgos_ejemplo=[
        ("El filtro de currículos reproduce el sesgo de las contrataciones anteriores, porque se "
         "entrenó con ellas",
         "Candidatos del grupo históricamente menos contratado",
         "Medir la tasa de paso por grupo en cada versión y comparar con la distribución de "
         "candidaturas recibidas, no con la de contratados"),
        ("El sistema puntúa el rendimiento y esa puntuación acaba pesando en una decisión de "
         "rescisión sin que estuviera previsto",
         "Personas empleadas",
         "Declarar por escrito para qué decisiones se puede usar la puntuación y auditarlo"),
        ("Se despliega sin informar a la representación de los trabajadores",
         "Plantilla",
         "Incluir la información previa del art. 26.7 en el procedimiento de despliegue, como "
         "requisito bloqueante"),
    ],
    notas_registro=[
        "El art. 26.7 impone algo que no está en los demás sectores: antes de poner en servicio "
        "el sistema en el lugar de trabajo hay que informar a los representantes de los "
        "trabajadores y a los trabajadores afectados. Es previo, no simultáneo.",
        "",
        "Conviene registrar esa comunicación con su fecha: es lo que se pide para acreditar que "
        "se hizo antes y no después.",
        "",
        "Y en España hay una obligación PROPIA que va más lejos. El art. 64.4.d del Estatuto de "
        "los Trabajadores reconoce al comité de empresa el derecho a «ser informado por la "
        "empresa de los parámetros, reglas e instrucciones en los que se basan los algoritmos o "
        "sistemas de inteligencia artificial que afectan a la toma de decisiones que pueden "
        "incidir en las condiciones de trabajo, el acceso y mantenimiento del empleo, incluida la "
        "elaboración de perfiles».",
        "",
        "Ojo a la diferencia: ese derecho NO depende de que el sistema sea de alto riesgo. Un "
        "sistema que quede fuera del Anexo III, o que salve el filtro del art. 6.3, sigue "
        "obligando a informar de sus parámetros y reglas si afecta a las condiciones de trabajo.",
    ],
    caso_titulo="Caso resuelto · criba de candidaturas",
    caso=[
        ("El caso",
         ["Una empresa recibe cientos de candidaturas por vacante. Quiere un sistema que ordene "
          "las candidaturas por ajuste al puesto. Recursos Humanos revisa las primeras y descarta "
          "el resto sin abrirlas."]),
        ("Paso 1 · Anexo III, sin discusión",
         ["El 4.a cubre «analizar y filtrar las solicitudes de empleo y evaluar a los "
          "candidatos». Es exactamente esto."]),
        ("Paso 2 · El filtro del 6.3, y por qué no salva",
         ["El argumento que se intenta siempre: «no decide, solo ordena; decide la persona».",
          "Falla por dos sitios. Primero, si el resto no se abre, el orden <b>es</b> la decisión: "
          "influye sustancialmente en el resultado, que es lo que el 6.3 exige descartar. Ordenar "
          "y descartar es la misma acción cuando nadie mira debajo de la línea.",
          "Y segundo, el corte definitivo: ordenar candidatos por ajuste es <b>elaboración de "
          "perfiles de personas físicas</b>. El art. 6.3 dice que los sistemas del Anexo III "
          "siempre son de alto riesgo cuando hay elaboración de perfiles. No hay filtro que "
          "aplicar.",
          "<b>Es de alto riesgo.</b>"]),
        ("Paso 3 · Lo que cambia en la práctica",
         [["Informar a la representación de los trabajadores <b>antes</b> de ponerlo en servicio "
           "(art. 26.7).",
           "Informar a los candidatos de que están expuestos al sistema (art. 26.11).",
           "Supervisión humana real: alguien con autoridad para mirar por debajo de la línea de "
           "corte y con tiempo asignado para hacerlo. Si la carga de trabajo hace imposible "
           "revisar, la supervisión existe en el papel y no en el proceso.",
           "Registros seis meses como mínimo (art. 26.6): qué candidaturas entraron, qué orden "
           "salió, quién revisó y qué cambió."]]),
        ("Paso 4 · La medida que más ahorra",
         ["Medir la tasa de paso por grupo comparándola con la distribución de <b>candidaturas "
          "recibidas</b>, no con la de personas contratadas históricamente. Compararse con el "
          "histórico de contratación es medir el sesgo con la regla que lo produjo."]),
        ("Lo que se lleva el expediente",
         ["Una clasificación que no intenta escaparse por el 6.3, porque intentarlo y fallar "
          "delante de la autoridad es peor que no haberlo intentado."]),
    ],
)

# ── C-18 · Administración pública ────────────────────────────────────────────────────────

ADMINISTRACION = Pack(
    slug="pack-administracion-publica",
    sector="Administración pública",
    anexo_iii=["5.a", "6", "7", "8"],
    matiz_anexo=(
        "el sector público arrastra una obligación que casi ningún privado tiene: la evaluación "
        "de impacto relativa a los derechos fundamentales del art. 27, obligatoria ANTES de "
        "desplegar para los organismos de Derecho público y para las entidades privadas que "
        "prestan servicios públicos. Se exceptúan los sistemas del Anexo III.2, infraestructuras "
        "críticas."
    ),
    fuentes=[
        AI_ACT_FUENTE,
        "Ley 40/2015, de Régimen Jurídico del Sector Público, art. 41 «Actuación "
        "administrativa automatizada». Texto consolidado consultado en el BOE.",
    ],
    riesgos_ejemplo=[
        ("El sistema deniega una prestación y la resolución no explica el motivo de forma que el "
         "interesado pueda recurrirla",
         "Solicitantes de la prestación",
         "Exigir que la salida incluya los factores determinantes y que la resolución los recoja"),
        ("El sistema se entrena con expedientes históricos que incorporan criterios ya derogados",
         "Solicitantes",
         "Fijar la ventana temporal de los datos de entrenamiento y revisarla ante cada cambio "
         "normativo"),
        ("Se despliega un sistema que no está registrado en la base de datos de la UE",
         "Toda la ciudadanía afectada",
         "Comprobar el registro antes del despliegue: el art. 26.8 obliga a NO usarlo si no está"),
    ],
    notas_registro=[
        "Art. 26.8: si el responsable del despliegue es una autoridad pública y constata que el "
        "sistema no está registrado en la base de datos de la UE del art. 71, NO puede usarlo, y "
        "tiene que informar al proveedor o distribuidor. Es una prohibición, no una recomendación.",
        "",
        "La evaluación del art. 27 incluye seis apartados tasados: procesos donde se usará, "
        "periodo y frecuencia de uso, categorías de personas afectadas, riesgos específicos de "
        "perjuicio, medidas de supervisión humana y medidas si los riesgos se materializan, "
        "incluidos gobernanza interna y mecanismos de reclamación.",
        "",
        "Y en España se suma el art. 41 de la Ley 40/2015. Define la actuación administrativa "
        "automatizada como el acto realizado íntegramente por medios electrónicos «en la que no "
        "haya intervenido de forma directa un empleado público»: si alguien firma de verdad, no "
        "es actuación automatizada.",
        "",
        "Cuando sí lo es, el art. 41.2 exige establecer PREVIAMENTE los órganos competentes para "
        "la definición de especificaciones, programación, mantenimiento, supervisión, control de "
        "calidad y, en su caso, auditoría del sistema Y DE SU CÓDIGO FUENTE; y además indicar el "
        "órgano responsable a efectos de impugnación. Sin ese señalamiento previo, el acto nace "
        "con un defecto que no se arregla después.",
    ],
    caso_titulo="Caso resuelto · ayuda pública con baremo asistido",
    caso=[
        ("El caso",
         ["Un ayuntamiento concede ayudas de emergencia social. Quiere un sistema que ordene las "
          "solicitudes por urgencia estimada, a partir de los datos aportados, para atender antes "
          "a quien más lo necesita. La resolución la firma siempre una persona."]),
        ("Paso 1 · Anexo III",
         ["El 5.a cubre los sistemas usados por autoridades públicas para «evaluar la "
          "admisibilidad de las personas físicas para beneficiarse de servicios y prestaciones "
          "esenciales de asistencia pública» y para concederlos, reducirlos o retirarlos.",
          "Ordenar por urgencia para decidir a quién se atiende antes, cuando el presupuesto es "
          "limitado, es evaluar admisibilidad en la práctica."]),
        ("Paso 2 · El filtro del 6.3",
         ["Se podría alegar el 6.3.d: «tarea preparatoria para una evaluación». Pero si el orden "
          "determina quién cobra antes de que se agote la partida, no es preparatorio: es "
          "determinante.",
          "Y de nuevo el corte: puntuar personas por su situación es elaborar perfiles. Con "
          "elaboración de perfiles, no hay filtro."]),
        ("Paso 3 · Lo específico del sector público",
         [["<b>Evaluación de impacto sobre derechos fundamentales (art. 27), antes de "
           "desplegar.</b> Con sus seis apartados, incluidos los mecanismos de reclamación.",
           "<b>Comprobar el registro en la base de datos de la UE (art. 26.8).</b> Si no está "
           "registrado, no se usa.",
           "Informar a las personas afectadas (art. 26.11).",
           "Y todo lo anterior sin perjuicio del procedimiento administrativo: el AI Act no "
           "sustituye la motivación de la resolución ni el derecho a recurrirla."]]),
        ("Paso 4 · La pregunta que hay que hacerse antes de todo esto",
         ["¿Hace falta el sistema? En una ayuda de emergencia con criterios reglados, un baremo "
          "explícito y auditable puede resolver lo mismo sin ser un sistema de IA de alto riesgo "
          "y sin expediente que mantener.",
          "El pack no empuja a usar IA. Empuja a saber qué cuesta usarla, que es distinto."]),
        ("Lo que se lleva el expediente",
         ["La evaluación del art. 27 hecha antes y no después, que es lo que la hace válida, y la "
          "constancia de haber comprobado el registro del sistema."]),
    ],
)

# ── C-19 · Retail y comercio electrónico ─────────────────────────────────────────────────

RETAIL = Pack(
    slug="pack-retail-ecommerce",
    sector="Retail y comercio electrónico",
    anexo_iii=["5.b"],
    matiz_anexo=(
        "este es el sector donde lo más probable es que NO haya alto riesgo, y saberlo con "
        "fundamento vale dinero. Recomendar productos, personalizar una portada, prever demanda o "
        "fijar precios de artículos no está en el Anexo III. La excepción real llega por el "
        "5.b: en cuanto se evalúa la solvencia de una persona —pago aplazado, financiación en el "
        "punto de venta— se entra en la lista, aunque el negocio sea vender zapatillas."
    ),
    fuentes=[
        AI_ACT_FUENTE,
    ],
    riesgos_ejemplo=[
        ("El sistema de pago aplazado deniega y la persona no puede saber por qué",
         "Compradores",
         "Registrar los factores determinantes y disponer de un canal de revisión humana"),
        ("La personalización acaba discriminando el precio por características personales sin "
         "que nadie lo haya decidido",
         "Clientes",
         "Prohibir explícitamente las variables sensibles y sus correlatos, y auditarlo"),
        ("El sistema de recomendación se usa para segmentar en algo que sí está en el Anexo III "
         "sin que la clasificación se revise",
         "Clientes afectados",
         "Revisar la clasificación ante cada nuevo caso de uso, no una vez al año"),
    ],
    notas_registro=[
        "Si la conclusión es que el sistema NO es de alto riesgo, el registro del art. 12 no es "
        "exigible por esa vía. Pero conviene conservar lo mismo igualmente: es lo que permite "
        "defender la clasificación si alguien la discute, y es barato porque ya se emite.",
        "",
        "En cuanto aparezca financiación o pago aplazado, la clasificación cambia y el registro "
        "pasa a ser obligatorio. Conviene tenerlo ya montado y no descubrirlo ese día.",
    ],
    caso_titulo="Caso resuelto · recomendador y pago aplazado",
    caso=[
        ("El caso",
         ["Una tienda en línea usa dos sistemas: uno recomienda productos en la portada y otro "
          "decide si se ofrece pago aplazado en el momento de la compra. La pregunta es si alguno "
          "es de alto riesgo."]),
        ("Paso 1 · El recomendador",
         ["No está en el Anexo III. No es biometría, ni infraestructura crítica, ni educación, ni "
          "empleo, ni servicios esenciales, ni cumplimiento del Derecho, ni migración, ni "
          "justicia. Recomendar productos no aparece en la lista.",
          "<b>No es de alto riesgo</b>, y no hace falta ni llegar al filtro del 6.3.",
          "Esto no significa que no tenga obligaciones: el RGPD aplica, y las de transparencia "
          "del art. 50 del AI Act pueden aplicar según cómo interactúe con el usuario."]),
        ("Paso 2 · El pago aplazado",
         ["Aquí cambia todo. El Anexo III.5.b cubre «evaluar la solvencia de personas físicas o "
          "establecer su calificación crediticia».",
          "Da igual que la empresa sea una tienda y no un banco: lo que clasifica es la "
          "<b>finalidad del sistema</b>, no el sector de quien lo usa.",
          "¿Salva la excepción de fraude? El 5.b excluye los sistemas destinados a detectar "
          "fraudes financieros. Si el sistema decide solvencia, no; si además de decidir "
          "solvencia hace antifraude, sigue siendo solvencia lo que decide."]),
        ("Paso 3 · Y la obligación que sorprende",
         ["Quien despliega un sistema del Anexo III.5.b tiene que hacer la <b>evaluación de "
          "impacto sobre derechos fundamentales del art. 27</b>, igual que un organismo público. "
          "Una tienda en línea con pago aplazado está en ese supuesto.",
          "Es probablemente el hallazgo más caro de este pack, y el que menos se espera."]),
        ("Lo que se lleva el expediente",
         ["Dos clasificaciones distintas para dos sistemas de la misma empresa, cada una con su "
          "razonamiento. Y la constancia de que el recomendador se evaluó y quedó fuera: eso es "
          "lo que evita tener que volver a discutirlo cada trimestre."]),
    ],
)

# ── C-20 · Industria y energía ───────────────────────────────────────────────────────────

INDUSTRIA = Pack(
    slug="pack-industria-energia",
    sector="Industria y energía",
    anexo_iii=["2"],
    matiz_anexo=(
        "el Anexo III.2 es estrecho a propósito: cubre los sistemas usados como COMPONENTES DE "
        "SEGURIDAD en la gestión y el funcionamiento de infraestructuras digitales críticas, "
        "tráfico rodado o suministro de agua, gas, calefacción o electricidad. Optimizar el "
        "consumo o predecir una avería no es un componente de seguridad; lo es aquello cuyo fallo "
        "compromete la seguridad del suministro. Además, este es el único ámbito del Anexo III "
        "exceptuado de la evaluación de impacto del art. 27."
    ),
    fuentes=[
        AI_ACT_FUENTE,
    ],
    riesgos_ejemplo=[
        ("El sistema de mantenimiento predictivo deja de avisar y el fallo llega sin preaviso",
         "Operarios de planta",
         "Alarma por ausencia de señal, no solo por señal anómala: un sistema callado parece un "
         "sistema sin incidencias"),
        ("El modelo se entrenó en condiciones de operación que ya no se dan",
         "Continuidad del suministro",
         "Revisar la validez del modelo ante cada cambio de régimen de operación"),
        ("La automatización lleva al operador a perder la práctica de operar sin ella",
         "Operarios y continuidad",
         "Simulacros periódicos de operación manual"),
    ],
    notas_registro=[
        "Los sistemas del Anexo III.2 están exceptuados de la evaluación de impacto sobre "
        "derechos fundamentales del art. 27. No lo están del resto de obligaciones.",
        "",
        "En industria, la trazabilidad que ya exige la normativa de seguridad suele ser más "
        "estricta que la del art. 12. Se aprovecha esa y se le añade lo que falte, en vez de "
        "montar un registro paralelo.",
    ],
    caso_titulo="Caso resuelto · mantenimiento predictivo en una subestación",
    caso=[
        ("El caso",
         ["Una distribuidora eléctrica usa un modelo que predice el fallo de transformadores a "
          "partir de la telemetría, para programar el mantenimiento. Un segundo sistema decide "
          "automáticamente el aislamiento de un tramo ante una anomalía."]),
        ("Paso 1 · El mantenimiento predictivo",
         ["¿Es un componente de seguridad? Su función es programar el mantenimiento, no evitar un "
          "fallo en curso. Si deja de funcionar, se hace el mantenimiento como se hacía antes.",
          "<b>No es de alto riesgo por el Anexo III.2</b>: no es un componente de seguridad en la "
          "gestión y el funcionamiento del suministro."]),
        ("Paso 2 · El aislamiento automático",
         ["Aquí sí. Es un sistema cuyo funcionamiento correcto es parte de la seguridad de la "
          "operación: si falla, se compromete el suministro eléctrico.",
          "Encaja en el Anexo III.2 y hay que tratarlo como de alto riesgo.",
          "Conviene además comprobar el art. 6.1: si ese sistema forma parte de un equipo sujeto "
          "a legislación de armonización del Anexo I con evaluación por tercero, entra también "
          "por esa vía, que es más exigente."]),
        ("Paso 3 · Lo que este sector se ahorra y lo que no",
         ["Se ahorra la evaluación del art. 27: el Anexo III.2 está expresamente exceptuado.",
          "No se ahorra nada más. Supervisión humana con autoridad para intervenir, registros al "
          "menos seis meses, vigilancia del funcionamiento y suspensión del uso si aparece un "
          "riesgo del art. 79.1."]),
        ("Paso 4 · El riesgo que no está en la norma",
         ["El más real de este sector no es normativo: es que el operador pierda la práctica de "
          "operar sin el sistema. Ninguna obligación del Reglamento lo cubre, y es lo que "
          "convierte una indisponibilidad de dos horas en un incidente."]),
        ("Lo que se lleva el expediente",
         ["Dos sistemas de la misma instalación con clasificaciones opuestas, y el criterio "
          "escrito que las separa: si su fallo compromete la seguridad del suministro o solo la "
          "eficiencia de su mantenimiento."]),
    ],
)

TODOS = [SEGUROS, SALUD, RRHH, ADMINISTRACION, RETAIL, INDUSTRIA]
