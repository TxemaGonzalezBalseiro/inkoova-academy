-- V002 · Planes, suscripciones, compras y entitlements.

CREATE TABLE plan (
    id               uuid PRIMARY KEY,
    code             varchar(32)  NOT NULL,
    name             varchar(80)  NOT NULL,
    billing_interval varchar(16)  NOT NULL
        CHECK (billing_interval IN ('monthly', 'quarterly', 'biannual', 'yearly', 'lifetime')),
    price_cents      bigint       NOT NULL CHECK (price_cents > 0),
    currency         char(3)      NOT NULL DEFAULT 'EUR',
    benefits_json    jsonb        NOT NULL DEFAULT '[]'::jsonb,
    stripe_price_id  varchar(64)  NULL,
    includes_packs   boolean      NOT NULL DEFAULT false,
    display_order    integer      NOT NULL DEFAULT 0,
    is_active        boolean      NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX ux_plan_code ON plan (code);
CREATE UNIQUE INDEX ux_plan_stripe_price ON plan (stripe_price_id) WHERE stripe_price_id IS NOT NULL;

CREATE TABLE subscription (
    id                      uuid PRIMARY KEY,
    user_id                 uuid        NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    plan_id                 uuid        NOT NULL REFERENCES plan (id) ON DELETE RESTRICT,
    status                  varchar(16) NOT NULL CHECK (status IN ('active', 'pastdue', 'canceled', 'unpaid')),
    current_period_end      timestamptz NOT NULL,
    cancel_at_period_end    boolean     NOT NULL DEFAULT false,
    stripe_subscription_id  varchar(64) NOT NULL,
    past_due_since          timestamptz NULL,
    created_at              timestamptz NOT NULL DEFAULT now(),
    updated_at              timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX ux_subscription_stripe_id ON subscription (stripe_subscription_id);
-- Invariante de T-01: una sola suscripción viva por usuario.
CREATE UNIQUE INDEX ux_subscription_active_per_user ON subscription (user_id)
    WHERE status IN ('active', 'pastdue');
CREATE INDEX ix_subscription_period_end ON subscription (current_period_end)
    WHERE status IN ('active', 'pastdue');

CREATE TABLE purchase (
    id                        uuid PRIMARY KEY,
    user_id                   uuid        NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    -- Referencia lógica: una compra puede apuntar a un product o, en renovaciones, a un plan.
    product_id                uuid        NOT NULL,
    gross_amount_cents        bigint      NOT NULL CHECK (gross_amount_cents > 0),
    tax_amount_cents          bigint      NOT NULL DEFAULT 0 CHECK (tax_amount_cents >= 0),
    processing_fee_cents      bigint      NOT NULL DEFAULT 0 CHECK (processing_fee_cents >= 0),
    refunded_amount_cents     bigint      NOT NULL DEFAULT 0 CHECK (refunded_amount_cents >= 0),
    currency                  char(3)     NOT NULL DEFAULT 'EUR',
    status                    varchar(20) NOT NULL
        CHECK (status IN ('paid', 'refunded', 'partiallyrefunded', 'chargedback')),
    stripe_payment_intent_id  varchar(64) NOT NULL,
    discount_code_used        varchar(40) NULL,
    paid_at                   timestamptz NOT NULL,
    refunded_at               timestamptz NULL
);

-- Idempotencia de webhooks: un PaymentIntent produce como mucho una compra.
CREATE UNIQUE INDEX ux_purchase_payment_intent ON purchase (stripe_payment_intent_id);
CREATE INDEX ix_purchase_user ON purchase (user_id, paid_at DESC);

CREATE TABLE entitlement (
    id           uuid PRIMARY KEY,
    user_id      uuid        NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    product_id   uuid        NOT NULL REFERENCES product (id) ON DELETE CASCADE,
    source       varchar(16) NOT NULL CHECK (source IN ('purchase', 'planincluded', 'manual')),
    valid_from   timestamptz NOT NULL,
    valid_until  timestamptz NULL,
    revoked_at   timestamptz NULL,
    note         text        NULL
);

-- Un entitlement vivo por (usuario, producto): el servicio extiende en vez de apilar.
CREATE UNIQUE INDEX ux_entitlement_user_product_live ON entitlement (user_id, product_id)
    WHERE revoked_at IS NULL;
-- Consulta caliente de IAccessPolicy.
CREATE INDEX ix_entitlement_lookup ON entitlement (user_id, product_id) WHERE revoked_at IS NULL;
-- Entrada del job diario de revocación.
CREATE INDEX ix_entitlement_expiring ON entitlement (valid_until)
    WHERE source = 'planincluded' AND revoked_at IS NULL;

-- Libro de eventos de Stripe. Hace idempotente el webhook (T-04).
CREATE TABLE stripe_event (
    event_id      varchar(64) PRIMARY KEY,
    event_type    varchar(80) NOT NULL,
    received_at   timestamptz NOT NULL,
    processed_at  timestamptz NULL,
    error         text        NULL
);

CREATE INDEX ix_stripe_event_unprocessed ON stripe_event (received_at) WHERE processed_at IS NULL;
