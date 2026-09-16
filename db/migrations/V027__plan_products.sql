-- Qué productos incluye cada plan.
--
-- Hasta aquí lo decidía el código: TODO plan daba TODOS los cursos publicados, en duro, y
-- `includes_packs` daba además todos los packs. No había forma de vender un plan barato con dos
-- cursos sin tocar `PlanEntitlementService` y desplegar.
--
-- Ahora son dos interruptores y una lista. Los interruptores existen —en vez de dejar solo la
-- lista— porque «todo el catálogo» y «estos tres cursos» se configuran igual pero se comportan
-- distinto el día que se publica algo nuevo: el primero lo incluye solo, el segundo no. Con solo
-- la lista, publicar un curso se lo quitaría en silencio a los suscriptores anuales hasta que
-- alguien repasara plan por plan.

ALTER TABLE plan
    ADD COLUMN IF NOT EXISTS includes_all_courses boolean NOT NULL DEFAULT true;

-- Renombre de `includes_packs`, para que las dos familias se lean igual. Se hace condicionado
-- porque la columna puede no existir en una base creada después de este cambio.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'plan' AND column_name = 'includes_packs'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'plan' AND column_name = 'includes_all_packs'
    ) THEN
        ALTER TABLE plan RENAME COLUMN includes_packs TO includes_all_packs;
    END IF;
END $$;

ALTER TABLE plan
    ADD COLUMN IF NOT EXISTS includes_all_packs boolean NOT NULL DEFAULT false;

-- La lista explícita. Se borra en cascada con el plan: una fila que apunta a un plan que ya no
-- existe no concede nada y solo estorba al leer.
CREATE TABLE IF NOT EXISTS plan_product (
    plan_id    uuid NOT NULL REFERENCES plan (id) ON DELETE CASCADE,
    product_id uuid NOT NULL REFERENCES product (id) ON DELETE CASCADE,
    PRIMARY KEY (plan_id, product_id)
);

CREATE INDEX IF NOT EXISTS ix_plan_product_product ON plan_product (product_id);

-- Relleno: `includes_all_courses` arranca en true para TODOS los planes existentes, que es
-- exactamente lo que hacía el código. Nadie pierde acceso con esta migración, y el día que se
-- quiera estrechar un plan se hace desde el panel, a la vista y con auditoría.
UPDATE plan SET includes_all_courses = true WHERE includes_all_courses IS NULL;
