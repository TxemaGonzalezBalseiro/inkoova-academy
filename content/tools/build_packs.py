#!/usr/bin/env python3
"""Genera los cinco documentos de cada pack sectorial.

    python content/tools/build_packs.py                 # los seis
    python content/tools/build_packs.py pack-salud      # solo uno

Los ficheros salen a `content/dist/packs/<slug>/`, que es el volumen que sirve la API. El
origen —lo que se edita— son `packs/fuentes.py` y `packs/sectores.py`: aquí no se escribe
contenido, solo se orquesta.

Al terminar avisa de los `TODO(verificar)` que queden, con su pack. Un pack con marcadores sin
cerrar se puede generar, pero NO se debería publicar: quien lo compra lo aplica a un expediente.
"""
from __future__ import annotations

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from packs import documentos, fuentes, sectores  # noqa: E402

RAIZ = Path(__file__).resolve().parents[2]
DESTINO = RAIZ / "content" / "dist" / "packs"

# El nombre del quinto fichero cambia por pack porque lo hace el README de cada uno; los otros
# cuatro se llaman igual en los seis.
CASOS = {
    "pack-seguros-financiero": "caso-resuelto-meridiana.pdf",
    "pack-salud": "caso-resuelto-salud.pdf",
    "pack-rrhh": "caso-resuelto-rrhh.pdf",
    "pack-administracion-publica": "caso-resuelto-administracion.pdf",
    "pack-retail-ecommerce": "caso-resuelto-retail.pdf",
    "pack-industria-energia": "caso-resuelto-industria.pdf",
}


def generar(pack) -> list[Path]:
    carpeta = DESTINO / pack.slug
    carpeta.mkdir(parents=True, exist_ok=True)

    trabajos = [
        ("checklist-clasificacion-riesgo.docx", documentos.checklist_clasificacion),
        ("plantilla-evaluacion-riesgos.xlsx", documentos.evaluacion_riesgos),
        ("registro-logs-exigible.xlsx", documentos.registro_logs),
        ("clausulas-modelo-proveedor-deployer.docx", documentos.clausulas_modelo),
        (CASOS[pack.slug], documentos.caso_resuelto),
    ]

    generados = []
    for nombre, constructor in trabajos:
        ruta = carpeta / nombre
        constructor(ruta, pack)
        generados.append(ruta)

    return generados


def pendientes(pack) -> list[str]:
    """Los `TODO(verificar)` del pack, que es lo que impide publicarlo."""
    marcadores = [f for f in pack.fuentes if "TODO(verificar)" in f]
    marcadores += [n for n in pack.notas_registro if "TODO(verificar)" in n]

    for _, parrafos in pack.caso:
        for parrafo in parrafos:
            textos = parrafo if isinstance(parrafo, list) else [parrafo]
            marcadores += [t for t in textos if "TODO(verificar)" in t]

    return marcadores


def main() -> int:
    pedidos = sys.argv[1:]
    elegidos = [p for p in sectores.TODOS if not pedidos or p.slug in pedidos]

    if not elegidos:
        print(f"No hay ningún pack con ese slug. Disponibles: "
              f"{', '.join(p.slug for p in sectores.TODOS)}")
        return 1

    print(f"Verificación normativa: {fuentes.FECHA_VERIFICACION} · versión {fuentes.VERSION}\n")

    sin_cerrar = {}

    for pack in elegidos:
        generados = generar(pack)
        total = sum(f.stat().st_size for f in generados)

        print(f"{pack.slug}")
        for fichero in generados:
            print(f"    {fichero.name:<44} {fichero.stat().st_size / 1024:>7.1f} KB")
        print(f"    {'':<44} {total / 1024:>7.1f} KB en total")

        restantes = pendientes(pack)
        if restantes:
            sin_cerrar[pack.slug] = restantes

        print()

    if sin_cerrar:
        print("─" * 88)
        print("PACKS CON VERIFICACIÓN PENDIENTE — generados, pero NO publicables tal cual:\n")

        for slug, marcadores in sin_cerrar.items():
            print(f"  {slug}  ({len(marcadores)})")
            for marcador in marcadores:
                limpio = " ".join(marcador.replace("TODO(verificar):", "").split())
                print(f"      · {limpio[:96]}")
            print()

        print("Cada uno es una fuente que hay que abrir. Publicar con estos marcadores dentro")
        print("pone «pendiente de verificar» delante de alguien que ha pagado por el pack.")
    else:
        print("Sin marcadores pendientes: los packs se pueden publicar.")

    return 0


if __name__ == "__main__":
    sys.exit(main())
