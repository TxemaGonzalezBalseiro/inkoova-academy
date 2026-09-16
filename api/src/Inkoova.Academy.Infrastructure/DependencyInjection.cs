using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Application.Access;
using Inkoova.Academy.Application.Affiliates;
using Inkoova.Academy.Application.Auth;
using Inkoova.Academy.Application.Billing;
using Inkoova.Academy.Application.Catalog;
using Inkoova.Academy.Application.Learning;
using Inkoova.Academy.Infrastructure.Caching;
using Inkoova.Academy.Infrastructure.Community;
using Inkoova.Academy.Infrastructure.Content;
using Inkoova.Academy.Infrastructure.Documents;
using Inkoova.Academy.Infrastructure.Identity;
using Inkoova.Academy.Infrastructure.Invoicing;
using Inkoova.Academy.Infrastructure.Messaging;
using Inkoova.Academy.Infrastructure.Payments;
using Inkoova.Academy.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Inkoova.Academy.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Wires everything the API and the job host need. Options are read once here so a
    /// missing setting fails at startup instead of on the first request that needs it.
    /// </summary>
    public static IServiceCollection AddAcademyInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        DapperTypeHandlers.Register();

        var connectionString = configuration.GetConnectionString("Academy")
                               ?? throw new InvalidOperationException(
                                   "Falta ConnectionStrings:Academy en la configuración.");

        services.AddSingleton<IDbConnectionFactory>(new NpgsqlConnectionFactory(connectionString));
        services.AddSingleton<IClock, SystemClock>();
        services.AddMemoryCache(o => o.SizeLimit = 1024);
        services.AddSingleton<ICatalogCache, MemoryCatalogCache>();

        AddOptions(services, configuration);
        AddRepositories(services);
        AddServices(services, configuration);
        AddHandlers(services);

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        var publicBaseUrl = Require(configuration, "Academy:PublicBaseUrl");

        services.AddSingleton(new ContentStorageOptions
        {
            RootPath = Require(configuration, "Academy:ContentRoot")
        });

        services.AddSingleton(new ContentTokenOptions
        {
            SigningKey = RequireSecret(configuration, "Academy:ContentTokenKey")
        });

        services.AddSingleton(new JwtOptions
        {
            Issuer = configuration["Academy:Jwt:Issuer"] ?? "inkoova-academy",
            Audience = configuration["Academy:Jwt:Audience"] ?? "inkoova-academy",
            SigningKey = RequireSecret(configuration, "Academy:Jwt:SigningKey"),
            AccessTokenMinutes = configuration.GetValue("Academy:Jwt:AccessTokenMinutes", 15),
            RefreshTokenDays = configuration.GetValue("Academy:Jwt:RefreshTokenDays", 30)
        });

        services.AddSingleton(new AccountOptions
        {
            PublicBaseUrl = publicBaseUrl,
            RequireConfirmedEmail = configuration.GetValue("Academy:Auth:RequireConfirmedEmail", true),
            OwnerEmails = ReadOwnerEmails(configuration)
        });

        services.AddSingleton(new CertificateOptions
        {
            SigningSecret = RequireSecret(configuration, "Academy:Certificates:SigningSecret"),
            PublicBaseUrl = publicBaseUrl
        });

        services.AddSingleton(new BillingOptions
        {
            GracePeriodDays = configuration.GetValue("Academy:Billing:GracePeriodDays", 3),
            CheckoutSuccessUrl = configuration["Academy:Billing:SuccessUrl"] ?? $"{publicBaseUrl}/cuenta/suscripcion?ok=1",
            CheckoutCancelUrl = configuration["Academy:Billing:CancelUrl"] ?? $"{publicBaseUrl}/precios",
            BillingPortalReturnUrl = configuration["Academy:Billing:PortalReturnUrl"] ?? $"{publicBaseUrl}/cuenta",
            InvoiceSeries = configuration["Academy:Invoicing:Series"] ?? "INK"
        });

        services.AddSingleton(new StripeOptions
        {
            SecretKey = RequireSecret(configuration, "Academy:Stripe:SecretKey"),
            WebhookSecret = RequireSecret(configuration, "Academy:Stripe:WebhookSecret"),
            EnableAutomaticTax = configuration.GetValue("Academy:Stripe:AutomaticTax", true)
        });

        services.AddSingleton(new EmailTemplateOptions
        {
            TemplatesPath = configuration["Academy:Email:TemplatesPath"] ?? "emails"
        });

        // La razón social, el NIF y el domicilio ya no están aquí: salen de los datos legales de
        // la marca, que es donde se editan desde el panel y de donde los lee el aviso legal.
        // Tenerlos en dos sitios acabaría con una factura y un aviso legal que se contradicen.
        services.AddSingleton(new InvoicingOptions
        {
            Series = configuration["Academy:Invoicing:Series"] ?? "INK",
            // Producción solo si alguien lo escribe. Por defecto, pruebas.
            Environment = string.Equals(
                configuration["Academy:Invoicing:Environment"], "produccion",
                StringComparison.OrdinalIgnoreCase)
                ? Domain.Billing.AeatEnvironment.Produccion
                : Domain.Billing.AeatEnvironment.Pruebas,
            Verifiable = configuration.GetValue("Academy:Invoicing:Verifiable", false)
        });

        // Quién es el SISTEMA que factura. El registro lo exige aparte del emisor: son siete
        // campos que describen el programa, constantes de esta instalación, y la AEAT los cruza
        // con la declaración responsable del software.
        //
        // Vacíos por defecto. Sin ellos no se remite nada, y el panel de Facturación lo dice:
        // un registro sin este bloque lo rechaza la AEAT entero.
        services.AddSingleton(new VerifactuSoftware(
            DeveloperName: configuration["Academy:Verifactu:Software:DeveloperName"] ?? string.Empty,
            DeveloperTaxId: configuration["Academy:Verifactu:Software:DeveloperTaxId"] ?? string.Empty,
            SystemName: configuration["Academy:Verifactu:Software:SystemName"] ?? "Inkoova Academy",
            SystemId: configuration["Academy:Verifactu:Software:SystemId"] ?? string.Empty,
            Version: configuration["Academy:Verifactu:Software:Version"] ?? string.Empty,
            InstallationNumber: configuration["Academy:Verifactu:Software:InstallationNumber"] ?? string.Empty,
            OnlyVerifactu: configuration.GetValue("Academy:Verifactu:Software:OnlyVerifactu", true),
            MultiTaxpayerCapable: configuration.GetValue("Academy:Verifactu:Software:MultiTaxpayerCapable", false),
            MultiTaxpayerInUse: configuration.GetValue("Academy:Verifactu:Software:MultiTaxpayerInUse", false)));
    }

    private static void AddRepositories(IServiceCollection services)
    {
        services.AddScoped<ProductRepository>();
        services.AddScoped<IProductRepository>(sp => sp.GetRequiredService<ProductRepository>());
        services.AddScoped<IProductCatalog>(sp => sp.GetRequiredService<ProductRepository>());

        services.AddScoped<ICourseRepository, CourseRepository>();
        services.AddScoped<IPackRepository, PackRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IEntitlementRepository, EntitlementRepository>();
        services.AddScoped<IPurchaseRepository, PurchaseRepository>();
        services.AddScoped<IProgressRepository, ProgressRepository>();
        services.AddScoped<IQuizRepository, QuizRepository>();
        services.AddScoped<ICertificateRepository, CertificateRepository>();
        services.AddScoped<IAffiliateRepository, AffiliateRepository>();
        services.AddScoped<IDiscountCodeRepository, DiscountCodeRepository>();
        services.AddScoped<IReferralRepository, ReferralRepository>();
        services.AddScoped<ICommissionRepository, CommissionRepository>();
        services.AddScoped<IPayoutRepository, PayoutRepository>();
        services.AddScoped<ITutoringPackageRepository, TutoringPackageRepository>();
        services.AddScoped<ITutoringGrantRepository, TutoringGrantRepository>();
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<ITutoringAppointmentRepository, TutoringAppointmentRepository>();
        services.AddScoped<ITutorEarningRepository, TutorEarningRepository>();
        services.AddScoped<IStripeEventStore, StripeEventStore>();
        services.AddScoped<IRoadmapRepository, RoadmapRepository>();
        services.AddScoped<IWaitlistRepository, WaitlistRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDownloadLogRepository, DownloadLogRepository>();
        services.AddScoped<ICommunitySessionRepository, CommunitySessionRepository>();
        services.AddScoped<IDiscordLinkRepository, DiscordLinkRepository>();
        services.AddScoped<IFiscalInvoiceRepository, FiscalInvoiceRepository>();
        services.AddScoped<IVerifactuRepository, VerifactuRepository>();
        services.AddScoped<IConsentRepository, ConsentRepository>();
        services.AddScoped<IdentityStore>();
    }

    private static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IContentStorage, LocalDiskContentStorage>();
        services.AddSingleton<IContentTokenService, ContentTokenService>();
        services.AddSingleton<ICertificatePdfGenerator, CertificatePdfGenerator>();
        services.AddSingleton<IAffiliateStatementGenerator, AffiliateStatementGenerator>();
        // Singleton como el renderizador que lo consume: la fábrica de conexiones y la caché en
        // memoria también lo son, así que no hay dependencia capturada de un ámbito más corto.
        services.AddScoped<IAcademyIdentityRepository, AcademyIdentityRepository>();

        // La clave con la que se cifran las contraseñas de los buzones. Va aparte de la del JWT
        // a propósito: rotar la de sesión no debe obligar a volver a escribir cada contraseña
        // SMTP, y al revés tampoco.
        services.AddSingleton(new SecretProtector(
            configuration["Academy:SecretsKey"]
            ?? configuration["Academy:Jwt:SigningKey"]
            ?? throw new InvalidOperationException(
                "Falta 'Academy:SecretsKey' para cifrar las credenciales guardadas.")));

        services.AddSingleton<IAcademyBrandingReader, AcademyBrandingReader>();
        // Scoped y ya no singleton: el renderizador consulta plantillas y marcas en la base, y
        // esos repositorios viven por petición. Dejarlo singleton capturaría una conexión.
        services.AddScoped<IEmailTemplateStore, EmailTemplateStore>();
        services.AddScoped<ICertificateStyleStore, CertificateStyleStore>();
        services.AddScoped<IEmailTemplateRenderer, FileEmailTemplateRenderer>();
        services.AddSingleton<JwtTokenService>();

        services.AddScoped<IPaymentGateway, StripePaymentGateway>();
        services.AddScoped<StripeEventMapper>();
        services.AddScoped<IInvoiceDocumentRenderer, InvoiceDocumentRenderer>();
        AddVerifactuSubmitter(services, configuration);
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<AccountService>();

        // SMTP is optional: without a host, emails go to the log so local runs still work.
        var smtpHost = configuration["Academy:Email:Host"];
        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }
        else
        {
            services.AddSingleton(new EmailOptions
            {
                Host = smtpHost,
                Port = configuration.GetValue("Academy:Email:Port", 587),
                Username = configuration["Academy:Email:Username"] ?? string.Empty,
                Password = configuration["Academy:Email:Password"] ?? string.Empty,
                FromAddress = configuration["Academy:Email:From"] ?? "hola@inkoova.com",
                // Sin valor por defecto a propósito: vacío significa «usa el nombre de la
                // academia», que se lee de Identidad. Poner aquí uno fijo volvería a congelar la
                // marca en la configuración, que es justo lo que se estaba arreglando.
                FromNameOverride = configuration["Academy:Email:FromName"],
                RedirectAllTo = configuration["Academy:Email:RedirectAllTo"],
                Security = configuration["Academy:Email:Security"] ?? "auto"
            });

            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }

        // Same for Discord: unconfigured means the jobs run and log instead of failing.
        var discordToken = configuration["Academy:Discord:BotToken"];
        if (string.IsNullOrWhiteSpace(discordToken))
        {
            services.AddSingleton<IDiscordGateway, NullDiscordGateway>();
        }
        else
        {
            services.AddSingleton(new DiscordOptions
            {
                BotToken = discordToken,
                GuildId = configuration["Academy:Discord:GuildId"] ?? string.Empty,
                ClientId = configuration["Academy:Discord:ClientId"] ?? string.Empty,
                ClientSecret = configuration["Academy:Discord:ClientSecret"] ?? string.Empty,
                RoleIdsByName = configuration
                    .GetSection("Academy:Discord:RoleIds")
                    .GetChildren()
                    .ToDictionary(c => c.Key, c => c.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            });

            services.AddHttpClient<IDiscordGateway, DiscordGateway>();
        }
    }

    private static void AddHandlers(IServiceCollection services)
    {
        services.AddScoped<IAccessPolicy, AccessPolicy>();
        services.AddScoped<EntitlementService>();
        services.AddScoped<PlanEntitlementService>();

        services.AddScoped<Application.Content.ImportManifestHandler>();
        services.AddScoped<Application.Content.CourseAuthoringHandlers>();
        services.AddScoped<Application.Billing.PlanAuthoringHandlers>();
        services.AddScoped<GetCatalogHandler>();
        services.AddScoped<GetCourseDetailHandler>();
        services.AddScoped<GetCourseProgressHandler>();
        services.AddScoped<CompleteLessonHandler>();
        services.AddScoped<SaveLessonPositionHandler>();
        services.AddScoped<GetMyCoursesHandler>();
        services.AddScoped<GetPlayerLessonHandler>();
        services.AddScoped<GetPackDownloadsHandler>();
        services.AddScoped<RequestPackFileDownloadHandler>();
        services.AddScoped<GetQuizHandler>();
        services.AddScoped<SubmitQuizHandler>();
        services.AddScoped<ClaimQuizAttemptsHandler>();
        services.AddScoped<GetMyQuizAttemptsHandler>();
        services.AddScoped<IssueCertificateHandler>();
        services.AddScoped<IssueProgramCertificateHandler>();
        services.AddScoped<VerifyCertificateHandler>();
        services.AddScoped<RenderCertificateImageHandler>();
        services.AddScoped<GetMyCertificatesHandler>();

        services.AddScoped<Application.Tutoring.PlanAccessProbe>();
        services.AddScoped<Application.Tutoring.ListTutoringPackagesHandler>();
        services.AddScoped<Application.Tutoring.SaveTutoringPackageHandler>();
        services.AddScoped<Application.Tutoring.SetTutoringPackageActiveHandler>();
        services.AddScoped<Application.Tutoring.ListTutoringBalancesHandler>();
        services.AddScoped<Application.Tutoring.GetStudentTutoringHandler>();
        services.AddScoped<Application.Tutoring.GrantTutoringHandler>();
        services.AddScoped<Application.Tutoring.RecordTutoringSessionHandler>();
        services.AddScoped<Application.Tutoring.DeleteTutoringSessionHandler>();
        services.AddScoped<Application.Tutoring.RevokeTutoringGrantHandler>();
        services.AddScoped<Application.Tutoring.AdjustTutoringGrantHandler>();
        services.AddScoped<Application.Tutoring.GetMyTutoringHandler>();
        services.AddScoped<Application.Tutoring.SyncTutoringWithStripeHandler>();

        services.AddScoped<Application.Tutoring.TutorNames>();
        services.AddScoped<Application.Tutoring.ListTutorsHandler>();
        services.AddScoped<Application.Tutoring.GetTutorHandler>();
        services.AddScoped<Application.Tutoring.SaveTutorHandler>();
        services.AddScoped<Application.Tutoring.PayTutorEarningsHandler>();

        services.AddScoped<ITutoringInvitationSender, Messaging.TutoringInvitationSender>();
        services.AddScoped<Application.Tutoring.ScheduleTutoringHandler>();
        services.AddScoped<Application.Tutoring.RescheduleTutoringHandler>();
        services.AddScoped<Application.Tutoring.CancelTutoringAppointmentHandler>();
        services.AddScoped<Application.Tutoring.ConfirmTutoringAppointmentHandler>();
        services.AddScoped<Application.Tutoring.ListUpcomingAppointmentsHandler>();

        services.AddScoped<StartPlanCheckoutHandler>();
        services.AddScoped<StartProductCheckoutHandler>();
        services.AddScoped<OpenBillingPortalHandler>();
        services.AddScoped<GetMySubscriptionHandler>();
        services.AddScoped<GetPlansHandler>();
        services.AddScoped<FiscalInvoiceService>();
        services.AddScoped<VerifactuService>();
        services.AddScoped<VerifyVerifactuChainHandler>();
        services.AddScoped<StripeWebhookProcessor>();

        services.AddScoped<CommissionService>();
        services.AddScoped<SettlementService>();
        services.AddScoped<TrackReferralHandler>();
        services.AddScoped<UpdateAffiliateTaxDataHandler>();
        services.AddScoped(sp => new GetAffiliateDashboardHandler(
            sp.GetRequiredService<IAffiliateRepository>(),
            sp.GetRequiredService<ICommissionRepository>(),
            sp.GetRequiredService<IPayoutRepository>(),
            sp.GetRequiredService<IPurchaseRepository>(),
            sp.GetRequiredService<IProductRepository>(),
            sp.GetRequiredService<IReferralRepository>(),
            sp.GetRequiredService<IClock>(),
            sp.GetRequiredService<CertificateOptions>().PublicBaseUrl));
    }

    private static string Require(IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException($"Falta la configuración '{key}'.");

    /// <summary>
    /// Administradores globales. Se admiten las dos formas que produce la configuración de
    /// .NET: una lista (<c>Academy:Auth:OwnerEmails:0</c>, típico en appsettings) y una cadena
    /// separada por comas (típico en una variable de entorno del contenedor).
    /// </summary>
    private static IReadOnlyList<string> ReadOwnerEmails(IConfiguration configuration)
    {
        var section = configuration.GetSection("Academy:Auth:OwnerEmails");

        var listed = section.GetChildren().Select(child => child.Value).OfType<string>().ToList();
        if (listed.Count > 0)
        {
            return listed;
        }

        return (section.Value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// El envío a la AEAT, si hay certificado.
    ///
    /// Sin certificado se queda <see cref="DisabledVerifactuSubmitter"/> y los asientos nacen
    /// como «no hay que remitir», que dice la verdad: hoy no hay a dónde. Arrancar con el envío
    /// a medias sería peor, porque los asientos nacerían pendientes y nadie los remitiría.
    ///
    /// El certificado se carga UNA vez y vive en el handler. Es una clave que firma
    /// declaraciones fiscales: no se pasa por la aplicación ni se guarda en un servicio que
    /// alguien pueda inyectar sin darse cuenta.
    /// </summary>
    private static void AddVerifactuSubmitter(IServiceCollection services, IConfiguration configuration)
    {
        var certificatePath = configuration["Academy:Verifactu:Certificate:Path"];

        if (string.IsNullOrWhiteSpace(certificatePath))
        {
            services.AddScoped<IVerifactuSubmitter, DisabledVerifactuSubmitter>();
            return;
        }

        if (!File.Exists(certificatePath))
        {
            // Se para el arranque. Con el fichero mal puesto, seguir dejaría los asientos
            // naciendo pendientes y acumulándose sin que nadie los remita, y eso no se ve hasta
            // que Hacienda pregunta.
            throw new InvalidOperationException(
                $"No existe el certificado de Veri*Factu en '{certificatePath}'. Es el que " +
                "autentica la conexión con la AEAT; sin él, quita " +
                "'Academy:Verifactu:Certificate:Path' para no remitir.");
        }

        var options = new AeatSubmitterOptions(
            string.Equals(
                configuration["Academy:Invoicing:Environment"], "produccion",
                StringComparison.OrdinalIgnoreCase)
                ? Domain.Billing.AeatEnvironment.Produccion
                : Domain.Billing.AeatEnvironment.Pruebas,
            // Un sello de entidad va a un host distinto que un certificado de representante.
            // Acertar aquí es lo que separa «rechazo de autenticación» de que funcione.
            configuration.GetValue("Academy:Verifactu:Certificate:IsSeal", true));

        services.AddSingleton(options);

        services.AddHttpClient<IVerifactuSubmitter, AeatVerifactuSubmitter>(client =>
            {
                // Un lote de 1.000 registros no se responde en los 100 s de por defecto, y una
                // petición cortada a medias deja sin saber qué entró y qué no.
                client.Timeout = TimeSpan.FromMinutes(5);
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader
                    .LoadPkcs12FromFile(
                        certificatePath,
                        configuration["Academy:Verifactu:Certificate:Password"],
                        System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.EphemeralKeySet);

                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(certificate);

                // Explícito aunque sea el valor por defecto: si alguien lo pone en Manual sin
                // añadir el certificado, la AEAT contesta un rechazo de autenticación que
                // parece un problema del propio certificado.
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;

                return handler;
            });
    }

    /// <summary>
    /// Secrets get their own accessor so the error message can say where they belong: in
    /// user-secrets locally and in the environment in production, never in appsettings.json.
    /// </summary>
    private static string RequireSecret(IConfiguration configuration, string key)
    {
        var value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Falta el secreto '{key}'. Defínelo con dotnet user-secrets en local "
                + $"o como variable de entorno '{key.Replace(':', '_')}' en producción.");
        }

        return value;
    }
}
