# Despliegue en Azure Container Apps

Alternativa gestionada al VPS de `infra/docker-compose.prod.yml`. Misma topología, piezas
de Azure:

| En el VPS | En Azure |
| --- | --- |
| caddy (TLS + SPA + proxy) | Container App `ca-caddy-<env>` (el TLS lo termina el ingress de ACA; la SPA va dentro de la imagen `caddy-azure`) |
| api | Container App `ca-api-<env>` (ingress interno, solo Caddy le habla) |
| jobs | Container App `ca-jobs-<env>` (min=max=1; en staging no existe) |
| migrator (one-shot) | Container Apps **Job** `job-migrator-<env>`, arranque manual |
| pgbouncer | **No hay**: el PgBouncer integrado de Flexible Server no existe en el tier Burstable (B1ms). Conexión directa al 5432 con pools recortados; `enable_pgbouncer=true` lo recupera si algún día se sube a General Purpose |
| postgres | Azure Database for PostgreSQL Flexible Server B1ms, 32 GB, PG17, backups 7 días |
| volumen content-data | Azure Files share `content` montado como `/srv/content` (rw en api/jobs, ro en caddy) |
| volumen web-dist | Desaparece: `Dockerfile.caddy` copia el build de la web dentro de la imagen |
| `infra/.env` (secretos) | **Key Vault** `kv-academy-<env>-xxxx`: terraform genera las claves de firma y la contraseña de Postgres, guarda ahí todos los secretos, y las apps los leen con una identidad administrada |

Un solo root module; `prod` y `staging` se separan con **workspaces de terraform** más un
tfvars por entorno.

## Prerequisitos

- [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli) (`az`) con sesión: `az login`
- [Terraform](https://developer.hashicorp.com/terraform/install) >= 1.9
- Docker (para `migrate-db.ps1` y para construir la imagen de Caddy)
- Un PAT de GitHub con **solo** `read:packages`, para que ACA descargue las imágenes de GHCR
- Permiso para asignar roles en la suscripción (Owner o User Access Administrator):
  terraform concede a tu usuario `Key Vault Secrets Officer` y a la identidad de las apps
  `Key Vault Secrets User` sobre el vault. Ser Owner por sí solo no da acceso a los
  secretos: el plano de datos del Key Vault usa sus propios roles.

## La imagen de Caddy (una vez por despliegue de la web)

ACA no tiene el truco del VPS de volcar la SPA en un volumen, así que hay una imagen
propia que empaqueta Caddy + `Caddyfile.azure` + el build de la web:

```powershell
docker login ghcr.io
docker build -f infra/azure/Dockerfile.caddy `
  --build-arg WEB_IMAGE=ghcr.io/inkoova/inkoova-academy/web:latest `
  -t ghcr.io/inkoova/inkoova-academy/caddy-azure:latest .
docker push ghcr.io/inkoova/inkoova-academy/caddy-azure:latest
```

Hay que reconstruirla **cada vez que cambia la web** (el workflow
`.github/workflows/deploy-azure.yml` lo hace solo). Si prefieres etiquetas por SHA
(recomendado en prod), usa el mismo tag en `--build-arg`, en `-t` y en `image_tag` del
tfvars.

## Primer despliegue (producción)

```powershell
cd infra/azure

# 1. Sesión y suscripción
az login
az account set --subscription "<tu-suscripción>"

# 2. Variables: copia el ejemplo y rellena TODOS los CHANGE_ME.
#    Los tfvars reales NO se versionan (.gitignore). Solo hay que traer los secretos
#    EXTERNOS (GHCR, Stripe, SMTP, Discord, OTEL): las claves de firma y la contraseña
#    de Postgres las genera terraform y las deja en el Key Vault.
Copy-Item envs/prod.example.tfvars envs/prod.tfvars

# 3. Terraform: init una vez, un workspace por entorno
terraform init
terraform workspace new prod        # las siguientes veces: terraform workspace select prod
terraform plan  -var-file envs/prod.tfvars
terraform apply -var-file envs/prod.tfvars
#    El primer apply espera 90 s tras asignar roles (propagación de RBAC) antes de
#    escribir secretos; es normal.

# 4. Migraciones de esquema (Container Apps Job, arranque manual)
az containerapp job start --name job-migrator-prod --resource-group rg-inkoova-academy-prod
#    ...o, si vienes de la base local con datos, sáltatelo y ve directo al punto 5:
#    el volcado ya lleva el esquema y la tabla de control de DbUp.

# 5. Datos: volcar la base local y restaurarla en Azure (destructivo en el destino)
$env:AZURE_PGPASSWORD = (terraform output -raw postgres_admin_password)
.\migrate-db.ps1 -AzureHost (terraform output -raw postgres_fqdn) -DryRun   # ensayo
.\migrate-db.ps1 -AzureHost (terraform output -raw postgres_fqdn)

# 6. Contenido de cursos al share de Azure Files
.\sync-content.ps1 -StorageAccount (terraform output -raw storage_account_name)

# 7. Smoke test
$url = terraform output -raw public_url
curl "$url/health/ready"
Start-Process "$url"
```

Para conectarte a la base a mano (psql, o `infra/import-catalog.ps1 -PgHost ...`):

```powershell
terraform output psql_command
$env:PGPASSWORD = (terraform output -raw postgres_admin_password)
```

El firewall de Postgres solo deja pasar servicios de Azure y la IP de `developer_ip` del
tfvars: si tu IP cambia, actualízala y vuelve a aplicar.

## Staging

Lo mismo con el otro workspace y el otro tfvars. Staging corre a mínimos: API y Caddy
escalan a cero (la primera petición tras un rato paga unos segundos de arranque en frío)
y la app de jobs **no existe** (`enable_jobs=false`: sus schedulers son bucles que nunca
terminan, así que la única forma de que no cueste es no crearla).

```powershell
Copy-Item envs/staging.example.tfvars envs/staging.tfvars   # y rellenar
terraform workspace new staging
terraform apply -var-file envs/staging.tfvars
```

Para ahorrar más: `az postgres flexible-server stop -n psql-inkoova-academy-staging -g
rg-inkoova-academy-staging` para la base (Azure la re-arranca sola a los 7 días; parada
no se factura el cómputo).

## Despliegues siguientes

1. El push a `main` ya publica `api/web/jobs/migrator:<sha>` en GHCR (workflow `deploy.yml`).
2. Reconstruye y publica `caddy-azure:<sha>` si cambió la web (o usa el workflow).
3. `terraform apply -var-file envs/prod.tfvars -var image_tag=<sha>` — cambia las
   imágenes de las apps y del job. Lanza el migrator (`az containerapp job start ...`)
   **antes** de que la API nueva reciba tráfico si el despliegue lleva migraciones.
4. Rollback = volver a aplicar con el `image_tag` anterior.

También vale `az containerapp update --image ...` para un cambio rápido sin terraform
(es lo que hace `deploy-azure.yml`); la próxima ejecución de terraform lo reconciliará
con el `image_tag` del tfvars.

## Costes estimados (mensuales, región Spain Central, orientativos)

| Recurso | Prod | Staging |
| --- | --- | --- |
| PostgreSQL Flexible B1ms (1 vCore burstable, 2 GiB) | ~12-14 € | ~12-14 € (0 € cómputo si está parada) |
| Disco Postgres 32 GB + backups 7 días | ~4-5 € | ~4-5 € |
| ACA: api 0.5 vCPU / 1 GiB, min 1 (mayormente idle) | ~5-15 € | ~0-2 € (escala a cero) |
| ACA: caddy 0.25 vCPU / 0.5 GiB, min 1 | ~3-8 € | ~0-1 € (escala a cero) |
| ACA: jobs 0.25 vCPU / 0.5 GiB, siempre encendida | ~3-8 € | 0 € (no existe) |
| ACA: job migrator (segundos por despliegue) | ~0 € | ~0 € |
| Azure Files 5 GB LRS + transacciones | <1 € | <1 € |
| Log Analytics (retención 30 días, tráfico bajo) | ~0-3 € | ~0-2 € |
| Key Vault standard (se paga por operación; ACA sondea cada ~30 min) | <1 € | <1 € |
| **Total** | **~28-50 €** | **~17-25 €** |

Notas: ACA en plan consumo factura por vCPU-s y GiB-s con precio reducido cuando la
réplica está ociosa, y la suscripción trae una franquicia mensual gratuita (180k vCPU-s +
360k GiB-s) que absorbe buena parte de staging. El coste real depende del tráfico; la
horquilla alta supone actividad constante.

## Dominio propio, más adelante

1. En el DNS: `CNAME academy.inkoova.com -> <caddy_fqdn>` (salida `caddy_fqdn` de
   terraform) y el TXT `asuid.academy` con el _custom domain verification id_ del
   environment (`az containerapp env show ... --query properties.customDomainConfiguration.customDomainVerificationId`).
2. Añade el dominio con certificado gestionado (gratis, se renueva solo):
   ```
   az containerapp hostname add  -n ca-caddy-prod -g rg-inkoova-academy-prod --hostname academy.inkoova.com
   az containerapp hostname bind -n ca-caddy-prod -g rg-inkoova-academy-prod --hostname academy.inkoova.com `
     --environment cae-inkoova-academy-prod --validation-method CNAME
   ```
3. Pon `custom_domain = "academy.inkoova.com"` en el tfvars y `terraform apply`: eso
   corrige `Academy__PublicBaseUrl`, el CORS y los enlaces de los emails, que hasta
   entonces apuntan al FQDN de ACA.

(El paso 2 se puede portar a terraform con `azurerm_container_app_custom_domain` +
certificado gestionado cuando el dominio esté decidido; a mano son dos comandos.)

## Secretos: qué va dónde

| Secreto | Origen | Nombre en el Key Vault |
| --- | --- | --- |
| Contraseña admin de Postgres | generada (`random_password`, 32) | `postgres-admin-password` |
| `Academy__ContentTokenKey` | generada (64) o tfvars | `content-token-key` |
| `Academy__Jwt__SigningKey` | generada (64) o tfvars | `jwt-signing-key` |
| `Academy__Certificates__SigningSecret` | generada (64) o tfvars | `cert-signing-secret` |
| Connection strings (api / jobs / migrator) | compuestas por terraform | `connstr-api`, `connstr-jobs`, `connstr-migrator` |
| Stripe, SMTP, GHCR, Discord, OTEL | tfvars (externos) | `stripe-secret-key`, `stripe-webhook-secret`, `smtp-password`, `ghcr-token`, `discord-bot-token`, `discord-client-secret`, `otel-headers` |

- **Key Vault** `kv-academy-<env>-xxxx` (salida `key_vault_name`): la fuente de verdad.
  Autorización por RBAC; las apps leen con la identidad `id-inkoova-academy-<env>`
  (`Key Vault Secrets User`), tu usuario escribe (`Key Vault Secrets Officer`). Leer uno:
  ```powershell
  az keyvault secret show --vault-name (terraform output -raw key_vault_name) --name jwt-signing-key --query value -o tsv
  ```
- **Container Apps**: cada `secret` de la app es una referencia por URL (sin versión) al
  vault, no un valor. ACA lo resuelve con la identidad y lo relee cada ~30 min: cambiar
  el valor en el vault basta para rotar sin reaplicar terraform (o fuerza una revisión
  nueva con `az containerapp update` si no quieres esperar).
- **`envs/*.tfvars`** (no versionados): solo los secretos externos más `owner_emails`.
  Las tres claves de firma van vacías salvo migración (abajo).
- **State de terraform** (`terraform.tfstate.d/`): sigue conteniendo TODO (los
  `random_password` y los `azurerm_key_vault_secret` guardan el valor). No se versiona y
  no se comparte; si el equipo crece, muévelo a un backend `azurerm` (storage account
  con cifrado) antes de compartir nada.

### Rotar una clave generada

```powershell
terraform apply -var-file envs/prod.tfvars -replace=random_password.jwt_signing_key
```

Impacto: `content_token_key` ninguno (tokens de 60 s); `jwt_signing_key` cierra todas
las sesiones; `postgres` (`-replace=random_password.postgres`) rota la contraseña del
servidor y las connection strings a la vez; `certificate_signing_secret` **no se rota**:
invalidaría la verificación de todos los certificados emitidos.

### Migrar desde el VPS conservando claves

Si el VPS ya emitió certificados, el `Academy__Certificates__SigningSecret` de
`infra/.env` tiene que venir tal cual: pon `certificate_signing_secret = "<valor>"` en el
tfvars y terraform lo usa en lugar del generado (el `random_password` se crea igual pero
no se usa). Con `jwt_signing_key` puedes hacer lo mismo para no cerrar las sesiones
abiertas; `content_token_key` no merece la pena.

## Ficheros

- `versions.tf`, `variables.tf`, `main.tf`, `keyvault.tf`, `containerapps.tf`,
  `outputs.tf` — el módulo
- `envs/*.example.tfvars` — plantillas por entorno
- `Dockerfile.caddy`, `Caddyfile.azure` — el Caddy adaptado a ACA
- `migrate-db.ps1` — base local → Flexible Server (destructivo en destino, con -DryRun)
- `sync-content.ps1` — content/dist → Azure Files (idempotente)
