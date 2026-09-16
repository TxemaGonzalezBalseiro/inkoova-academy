-- V028 · pg_stat_statements, cuando la instancia lo tenga cargado.
--
-- Sin esto, «qué consulta está costando el p95» se responde mirando trazas de una en una.
-- Con esto se responde ordenando una tabla por tiempo acumulado, que es la diferencia entre
-- optimizar lo que pesa y optimizar lo que se recuerda.
--
-- La extensión no se puede crear si el binario no se precargó al arrancar el servidor, y eso
-- se decide en la línea de comandos de Postgres, no aquí. En producción lo pone
-- `docker-compose.prod.yml`; en la máquina de desarrollo y en los Testcontainers de los tests
-- de integración, no. Por eso la creación va condicionada: una migración que solo funciona en
-- un entorno no es una migración, es una avería con fecha.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_available_extensions WHERE name = 'pg_stat_statements')
       AND coalesce(current_setting('shared_preload_libraries', true), '')
           LIKE '%pg_stat_statements%'
    THEN
        CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
    END IF;
END
$$;
