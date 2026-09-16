/** Un curso o un pack que se puede marcar en un plan. El id es de PRODUCTO, no de curso. */
export type SelectableProduct = { id: string; slug: string; title: string };

export type SelectableProducts = { courses: SelectableProduct[]; packs: SelectableProduct[] };

/**
 * Qué entra en el plan, por familia de producto.
 *
 * Dos opciones y no una lista a secas porque «todo el catálogo» y «estos tres cursos» se
 * configuran igual pero se comportan distinto **el día que se publica algo nuevo**: la primera
 * lo incluye sola, la segunda no. Con solo la lista, publicar un curso se lo estaría quitando en
 * silencio a los suscriptores anuales hasta que alguien repasara plan por plan, y nada avisaría.
 *
 * Las casillas se ocultan con «todo» puesto. Dejarlas visibles pero ignoradas invita a marcarlas
 * y a creer que hacen algo.
 */
export function ProductPicker({
  titulo,
  todoLabel,
  todoAyuda,
  sueltosLabel,
  todo,
  onTodo,
  productos,
  marcados,
  onToggle,
  cargando,
  marcadosCount,
}: {
  titulo: string;
  todoLabel: string;
  todoAyuda: string;
  sueltosLabel: string;
  todo: boolean;
  onTodo: (value: boolean) => void;
  productos: SelectableProduct[];
  marcados: string[];
  onToggle: (id: string) => void;
  cargando: boolean;
  marcadosCount: number;
}) {
  // Nombre de grupo propio por bloque: con dos selectores en el mismo formulario, un `name`
  // repetido haría que elegir en uno deseleccionara el otro.
  const grupo = `plan-${titulo.toLowerCase().replace(/[^a-z]+/g, '-')}`;

  return (
    <fieldset className="plan-picker">
      <legend>
        {titulo}
        {!todo && marcadosCount > 0 && (
          <span className="badge badge--brand">{marcadosCount} marcados</span>
        )}
      </legend>

      <label className="check">
        <input type="radio" name={grupo} checked={todo} onChange={() => onTodo(true)} />
        <span>
          {todoLabel}
          <small className="muted">{todoAyuda}</small>
        </span>
      </label>

      <label className="check">
        <input type="radio" name={grupo} checked={!todo} onChange={() => onTodo(false)} />
        <span>{sueltosLabel}</span>
      </label>

      {!todo && (
        <div className="plan-picker__list">
          {cargando && <p className="muted">Cargando…</p>}

          {!cargando && productos.length === 0 && <p className="muted">No hay ninguno todavía.</p>}

          {productos.map((producto) => (
            <label key={producto.id} className="check">
              <input
                type="checkbox"
                checked={marcados.includes(producto.id)}
                onChange={() => onToggle(producto.id)}
              />
              <span>
                {producto.title}
                <small className="muted">{producto.slug}</small>
              </span>
            </label>
          ))}
        </div>
      )}
    </fieldset>
  );
}
