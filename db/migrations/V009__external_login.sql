-- V009 · Inicio de sesión con Google y con Apple.
--
-- Una fila por cuenta externa enlazada. La clave primaria es (proveedor, identificador del
-- proveedor) y NO el email: el email de una cuenta de Google puede cambiar, y en Apple puede
-- ser un alias de reenvío distinto en cada aplicación. Lo único estable es el `sub` del
-- proveedor, así que es lo que manda.
--
-- Esta tabla NO sustituye a `identity_user`: el alumno sigue siendo el mismo usuario de la
-- academia, con su matrícula y sus certificados, entre por donde entre. Por eso hay una clave
-- ajena a `identity_user` y no una copia de sus datos.

CREATE TABLE IF NOT EXISTS external_login (
    -- 'google' o 'apple'. Minúsculas, y es lo que viaja en la ruta del endpoint.
    provider     varchar(32)  NOT NULL,

    -- El `sub` del proveedor. 255 porque Apple no documenta un máximo y los de Google rondan
    -- los 21 caracteres: sobra sitio sin llegar al límite de índice de Postgres.
    provider_key varchar(255) NOT NULL,

    user_id      uuid         NOT NULL REFERENCES identity_user (id) ON DELETE CASCADE,

    -- El email tal y como lo dio el proveedor la última vez. Es informativo —para que el panel
    -- pueda explicar con qué cuenta entra alguien— y nunca se usa para identificar: para eso
    -- está provider_key.
    email        varchar(256) NULL,

    linked_at    timestamptz  NOT NULL DEFAULT now(),
    last_login_at timestamptz NULL,

    PRIMARY KEY (provider, provider_key)
);

-- Para responder "¿con qué cuentas externas entra este usuario?" sin recorrer la tabla.
CREATE INDEX IF NOT EXISTS ix_external_login_user ON external_login (user_id);

-- Un mismo usuario no puede tener dos cuentas del MISMO proveedor enlazadas: si Google ya está
-- enlazado y alguien entra con otra cuenta de Google, es una cuenta distinta de la academia, no
-- un segundo enlace de la misma. Sin esta restricción, dos identidades de Google acabarían
-- apuntando al mismo alumno y ninguna de las dos podría desenlazarse sin ambigüedad.
CREATE UNIQUE INDEX IF NOT EXISTS ux_external_login_user_provider
    ON external_login (user_id, provider);
