using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Inkoova.Academy.Api;
using Inkoova.Academy.Api.Auth;
using Inkoova.Academy.Api.Common;
using Inkoova.Academy.Api.Endpoints;
using Inkoova.Academy.Domain.Users;
using Inkoova.Academy.Infrastructure;
using Inkoova.Academy.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAcademyInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();

// Toda fecha del cliente entra en UTC, venga con el desplazamiento que venga. Ver
// UtcDateTimeOffsetConverter: sin esto, un `+02:00` acababa en un 500 al tocar Postgres.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter()));

// …y que el contrato lo diga, que era la otra mitad del problema: OpenAPI no contaba en qué
// huso había que mandar las fechas.
builder.Services.AddOpenApi(options => options.AddSchemaTransformer((schema, context, _) =>
{
    if (schema.Format == "date-time")
    {
        schema.Description = string.IsNullOrWhiteSpace(schema.Description)
            ? UtcDateTimeOffsetConverter.SchemaNote
            : $"{schema.Description} {UtcDateTimeOffsetConverter.SchemaNote}";
    }

    return Task.CompletedTask;
}));

// Caddy terminates TLS in front of the API, so the scheme and the client IP arrive in
// headers. Without this, rate limiting would see one client and links would say http.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // Se vacían las listas de proxies conocidos porque la IP de Caddy dentro de la red de
    // Docker no es fija y no se puede declarar de antemano.
    //
    // Eso hace que se acepte `X-Forwarded-For` de quien sea, así que la protección contra una
    // IP falsificada —con la que se saltarían todos los límites de peticiones— descansa en dos
    // hechos, y solo en ellos:
    //
    //   1. La API no publica puertos: en `docker-compose.prod.yml` no hay `ports:`, así que solo
    //      se llega a ella por Caddy.
    //   2. Caddy AÑADE la IP real al final de la cabecera en vez de reemplazarla, y con
    //      ForwardLimit = 1 se lee el último valor, que es justo ese.
    //
    // Si algún día se publica el puerto de la API o se mete otro proxy por delante, esto pasa a
    // ser un saltador de límites. Lo fija `ForwardedHeadersTests`.
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// ── auth ────────────────────────────────────────────────────────────────────────────────

var jwt = new JwtOptions
{
    Issuer = builder.Configuration["Academy:Jwt:Issuer"] ?? "inkoova-academy",
    Audience = builder.Configuration["Academy:Jwt:Audience"] ?? "inkoova-academy",
    SigningKey = builder.Configuration["Academy:Jwt:SigningKey"]
                 ?? throw new InvalidOperationException("Falta el secreto 'Academy:Jwt:SigningKey'.")
};

// ── proveedores externos (Google, Apple) ────────────────────────────────────────────────
//
// Se registran solo si están configurados. Un handler con el client_id vacío arrancaría igual y
// fallaría al primer clic con un error del proveedor, que es el peor momento para enterarse.
var externalAuth = builder.Configuration.GetSection("Academy:ExternalAuth").Get<ExternalAuthOptions>()
                   ?? new ExternalAuthOptions();

builder.Services.AddSingleton(externalAuth);

builder.Services.AddSingleton(new AcademyUrls(
    builder.Configuration["Academy:PublicBaseUrl"] ?? "http://localhost:5173"));

var authentication = builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme);

if (externalAuth.Google.IsConfigured || externalAuth.Apple.IsConfigured)
{
    // Cookie de un solo uso donde el handler externo deja el resultado hasta que nuestro
    // callback lo recoge. Vive segundos y NO es la sesión: la sesión sigue siendo el JWT más la
    // cookie de refresco, igual que en el login con contraseña.
    authentication.AddCookie(ExternalAuthEndpoints.ExternalScheme, options =>
    {
        options.Cookie.Name = "ink_ext";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
    });
}

if (externalAuth.Google.IsConfigured)
{
    authentication.AddGoogle(options =>
    {
        options.ClientId = externalAuth.Google.ClientId!;
        options.ClientSecret = externalAuth.Google.ClientSecret!;
        options.SignInScheme = ExternalAuthEndpoints.ExternalScheme;
        options.CallbackPath = "/api/auth/external/google/signin";
        options.SaveTokens = false;

        // `email_verified` no llega como claim: viene en el JSON del userinfo y hay que sacarlo
        // a mano. Sin él no se puede decidir si es seguro enlazar con una cuenta que ya existe,
        // que es la comprobación que impide quedarse con la cuenta de otro.
        options.Events.OnCreatingTicket = context =>
        {
            if (context.User.TryGetProperty("email_verified", out var verified))
            {
                context.Identity?.AddClaim(
                    new Claim(ExternalAuthEndpoints.EmailVerifiedClaim, verified.ToString()));
            }

            return Task.CompletedTask;
        };
    });
}

if (externalAuth.Apple.IsConfigured)
{
    authentication.AddOpenIdConnect(ExternalAuthEndpoints.AppleScheme, options =>
    {
        options.Authority = "https://appleid.apple.com";
        options.ClientId = externalAuth.Apple.ClientId!;
        options.SignInScheme = ExternalAuthEndpoints.ExternalScheme;
        options.CallbackPath = "/api/auth/external/apple/signin";
        options.ResponseType = "code";

        // Apple contesta con un POST de formulario, no con una redirección con query. Sin esto
        // el callback nunca recibe el código.
        options.ResponseMode = "form_post";
        options.UsePkce = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("name");

        options.SaveTokens = false;
        options.GetClaimsFromUserInfoEndpoint = false;

        options.Events.OnAuthorizationCodeReceived = context =>
        {
            // El secreto de Apple es un JWT firmado que caduca. Se genera aquí, en cada
            // intercambio, en lugar de guardarse: uno guardado caduca un día cualquiera y rompe
            // el login sin que nadie haya tocado nada.
            context.TokenEndpointRequest!.ClientSecret =
                AppleClientSecret.Create(externalAuth.Apple, DateTimeOffset.UtcNow);

            return Task.CompletedTask;
        };
    });
}

authentication
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            // The default 5-minute skew would keep a 15-minute token alive for 20.
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("admin", policy => policy.RequireRole(Roles.Admin))
    .AddPolicy("affiliate", policy => policy.RequireRole(Roles.Affiliate, Roles.Admin));

// ── CORS ────────────────────────────────────────────────────────────────────────────────

var allowedOrigins = builder.Configuration.GetSection("Academy:Cors:Origins").Get<string[]>()
                     ?? ["http://localhost:5173"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    // The refresh token lives in a cookie, so the SPA must be allowed to send it.
    .AllowCredentials()));

// ── rate limiting ───────────────────────────────────────────────────────────────────────

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Credential endpoints: tight, per IP. Slows down credential stuffing without making
    // the site unusable behind a shared NAT.
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(5) }));

    // Public certificate verification: unauthenticated and cacheable by scrapers (T-10).
    options.AddPolicy("verification", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));

    options.AddPolicy("quiz", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(10) }));

    // Referral clicks: enough for real navigation, low enough that click inflation is not
    // worth attempting (T-16 antifraud).
    options.AddPolicy("referral", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientKey(context),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromHours(1) }));
});

// ── observability ───────────────────────────────────────────────────────────────────────

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("inkoova-academy-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"))
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());

builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("Academy")!,
        name: "postgres",
        tags: ["ready"]);

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var feature = context.Features.Get<IExceptionHandlerFeature>();

    // Una petición mal formada es culpa de quien llama, no del servidor. Sin esto el
    // manejador se tragaba el 400 del binding y contestaba 500 a algo tan corriente como
    // olvidar un parámetro, mandando a buscar el fallo al sitio equivocado.
    if (feature?.Error is BadHttpRequestException badRequest)
    {
        await Results.Problem(
                title: "Petición no válida",
                detail: badRequest.Message,
                statusCode: badRequest.StatusCode)
            .ExecuteAsync(context);

        return;
    }

    app.Logger.LogError(feature?.Error, "Unhandled exception on {Path}", context.Request.Path);

    // No stack traces to the client: the trace id is enough to find it in the collector.
    await Results.Problem(
            title: "Error inesperado",
            detail: "Ha ocurrido un error. Inténtalo de nuevo en unos segundos.",
            statusCode: StatusCodes.Status500InternalServerError,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier })
        .ExecuteAsync(context);
}));

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

    // El contenido importado se sirve DENTRO del iframe del player (ADR-008), así que es la
    // única ruta que no puede llevar DENY: con DENY el navegador rechaza el documento y el
    // player se queda en blanco en todas las lecciones. SAMEORIGIN sigue impidiendo que un
    // tercero enmarque una lección con el token de otro alumno, que es lo que protege esta
    // cabecera. El resto de la API mantiene DENY: nada más debe enmarcarse nunca.
    headers["X-Frame-Options"] =
        context.Request.Path.StartsWithSegments("/api/content") ? "SAMEORIGIN" : "DENY";

    await next();
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Después de la autenticación, no antes.
//
// `ClientKey` reparte por usuario cuando hay sesión y por IP cuando no. Con el limitador
// delante de `UseAuthentication`, `context.User` todavía es anónimo siempre, así que esa rama
// nunca se ejecutaba y todas las particiones caían a IP. No abría nada —falla cerrado— pero el
// límite de quiz, pensado por alumno, se convertía en un límite por IP: una empresa o un aula
// detrás del mismo NAT se bloqueaban entre sí.
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapAuthEndpoints();
app.MapCatalogEndpoints();
app.MapDiscoveryEndpoints();
app.MapLearningEndpoints();
app.MapContentEndpoints();
app.MapBillingEndpoints();
app.MapRoadmapEndpoints();
app.MapAffiliateEndpoints();
app.MapCommunityEndpoints();
app.MapPrivacyEndpoints();
app.MapAdminEndpoints();
app.MapAuthoringEndpoints();
app.MapBillingAdminEndpoints();
app.MapEmailAdminEndpoints();
app.MapBrandingEndpoints();
app.MapTutoringEndpoints();
app.MapTutorEndpoints();
app.MapInvoicingEndpoints();
app.MapExternalAuthEndpoints();
app.MapAboutEndpoints();
app.MapIdentityEndpoints();

await SeedRunner.RunAsync(app);

app.Run();

/// <summary>Rate-limit partition key: the authenticated user when there is one, the IP otherwise.</summary>
static string ClientKey(HttpContext context) =>
    context.User.Identity?.IsAuthenticated == true
        ? context.User.FindFirst("sub")?.Value ?? "anonymous"
        : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

/// <summary>Exposed so the integration tests can build the same host.</summary>
public partial class Program;
