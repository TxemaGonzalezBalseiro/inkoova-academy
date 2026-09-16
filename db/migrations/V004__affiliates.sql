-- V004 · Códigos de descuento y programa de afiliados (T-16).

CREATE TABLE affiliate (
    id                          uuid PRIMARY KEY,
    user_id                     uuid          NOT NULL REFERENCES app_user (id) ON DELETE CASCADE,
    code                        varchar(20)   NOT NULL,
    commission_percent          numeric(5, 2) NOT NULL CHECK (commission_percent > 0 AND commission_percent <= 50),
    status                      varchar(16)   NOT NULL CHECK (status IN ('pending', 'active', 'suspended')),
    recurring_months            integer       NOT NULL DEFAULT 12 CHECK (recurring_months BETWEEN 0 AND 36),
    tax_id                      varchar(40)   NULL,
    country_code                char(2)       NULL,
    iban                        varchar(40)   NULL,
    invoicing_mode              varchar(20)   NOT NULL DEFAULT 'affiliateissues'
        CHECK (invoicing_mode IN ('affiliateissues', 'selfbilling')),
    -- Referencia al acuerdo de autofactura firmado (art. 5 RD 1619/2012).
    self_billing_agreement_ref  text          NULL,
    created_at                  timestamptz   NOT NULL DEFAULT now(),
    CONSTRAINT ck_affiliate_self_billing CHECK (
        invoicing_mode <> 'selfbilling' OR self_billing_agreement_ref IS NOT NULL
    )
);

CREATE UNIQUE INDEX ux_affiliate_code ON affiliate (code);
CREATE UNIQUE INDEX ux_affiliate_user ON affiliate (user_id);

CREATE TABLE discount_code (
    id                        uuid PRIMARY KEY,
    code                      varchar(40)   NOT NULL,
    kind                      varchar(16)   NOT NULL CHECK (kind IN ('percentage', 'fixedamount')),
    percent                   numeric(5, 2) NOT NULL DEFAULT 0,
    fixed_amount_cents        bigint        NULL,
    currency                  char(3)       NOT NULL DEFAULT 'EUR',
    -- Vacío significa "cualquier producto".
    applicable_product_ids    uuid[]        NOT NULL DEFAULT '{}',
    valid_from                timestamptz   NOT NULL,
    valid_until               timestamptz   NULL,
    max_redemptions           integer       NULL CHECK (max_redemptions IS NULL OR max_redemptions > 0),
    redemptions               integer       NOT NULL DEFAULT 0,
    affiliate_id              uuid          NULL REFERENCES affiliate (id) ON DELETE SET NULL,
    stripe_promotion_code_id  varchar(64)   NULL,
    is_active                 boolean       NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX ux_discount_code ON discount_code (code);
CREATE INDEX ix_discount_code_affiliate ON discount_code (affiliate_id) WHERE affiliate_id IS NOT NULL;

CREATE TABLE referral (
    id                     uuid PRIMARY KEY,
    -- Cookie de origen propio. No identifica a una persona.
    visitor_id             varchar(64) NOT NULL,
    affiliate_id           uuid        NOT NULL REFERENCES affiliate (id) ON DELETE CASCADE,
    first_click_at         timestamptz NOT NULL,
    last_click_at          timestamptz NOT NULL,
    expires_at             timestamptz NOT NULL,
    converted_purchase_id  uuid        NULL REFERENCES purchase (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX ux_referral_visitor ON referral (visitor_id);
CREATE INDEX ix_referral_affiliate ON referral (affiliate_id, first_click_at);

CREATE TABLE payout (
    id                       uuid PRIMARY KEY,
    affiliate_id             uuid        NOT NULL REFERENCES affiliate (id) ON DELETE CASCADE,
    period_start             date        NOT NULL,
    period_end               date        NOT NULL,
    total_cents              bigint      NOT NULL CHECK (total_cents >= 0),
    carried_over_in_cents    bigint      NOT NULL DEFAULT 0 CHECK (carried_over_in_cents >= 0),
    currency                 char(3)     NOT NULL DEFAULT 'EUR',
    status                   varchar(20) NOT NULL
        CHECK (status IN ('awaitinginvoice', 'readytopay', 'paid', 'cancelled')),
    invoice_ref              text        NULL,
    statement_content_ref    text        NULL,
    created_at               timestamptz NOT NULL DEFAULT now(),
    paid_at                  timestamptz NULL,
    CONSTRAINT ck_payout_period CHECK (period_end >= period_start)
);

-- La liquidación mensual es reproducible: un periodo, una liquidación por afiliado.
CREATE UNIQUE INDEX ux_payout_affiliate_period ON payout (affiliate_id, period_start);

CREATE TABLE commission (
    id               uuid PRIMARY KEY,
    purchase_id      uuid          NOT NULL REFERENCES purchase (id) ON DELETE CASCADE,
    affiliate_id     uuid          NOT NULL REFERENCES affiliate (id) ON DELETE CASCADE,
    net_base_cents   bigint        NOT NULL CHECK (net_base_cents > 0),
    percent          numeric(5, 2) NOT NULL,
    amount_cents     bigint        NOT NULL CHECK (amount_cents >= 0),
    currency         char(3)       NOT NULL DEFAULT 'EUR',
    status           varchar(16)   NOT NULL CHECK (status IN ('pending', 'approved', 'paid', 'reversed')),
    created_at       timestamptz   NOT NULL,
    approvable_at    timestamptz   NOT NULL,
    approved_at      timestamptz   NULL,
    payout_id        uuid          NULL REFERENCES payout (id) ON DELETE SET NULL,
    reversal_reason  text          NULL
);

-- Una comisión por (compra, afiliado): evita duplicar al reprocesar un webhook.
CREATE UNIQUE INDEX ux_commission_purchase_affiliate ON commission (purchase_id, affiliate_id);
-- Entrada del job de aprobación diaria.
CREATE INDEX ix_commission_approvable ON commission (approvable_at) WHERE status = 'pending';
CREATE INDEX ix_commission_affiliate ON commission (affiliate_id, created_at DESC);
CREATE INDEX ix_commission_payable ON commission (affiliate_id, approved_at) WHERE status = 'approved';
