using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// Boots the real API against the throwaway Postgres container.
///
/// La configuración va por VARIABLES DE ENTORNO y no por <c>ConfigureAppConfiguration</c>.
/// Con top-level statements, <c>Program.cs</c> llama a <c>AddAcademyInfrastructure</c>
/// mientras construye el builder, es decir antes de que la factory pueda inyectar nada: lo
/// que se añada ahí llega tarde y la API arranca con "Falta ConnectionStrings:Academy".
/// <c>CreateBuilder</c> sí lee el entorno, así que ese es el único canal que funciona.
///
/// Los valores son secretos visiblemente falsos: nunca deben confundirse con producción.
/// </summary>
public sealed class AcademyApiFactory : WebApplicationFactory<Program>
{
    private static readonly object EnvironmentLock = new();

    public AcademyApiFactory(string connectionString)
    {
        // Las variables de entorno son globales al proceso y xUnit ejecuta colecciones en
        // paralelo. Todas las instancias comparten el mismo contenedor y la misma
        // configuración, así que escribir bajo un cerrojo basta para que ninguna lea valores
        // a medio escribir.
        lock (EnvironmentLock)
        {
            var settings = new Dictionary<string, string>
            {
                ["ConnectionStrings__Academy"] = connectionString,
                ["Academy__PublicBaseUrl"] = "https://academy.test.local",
                ["Academy__ContentRoot"] = Path.Combine(Path.GetTempPath(), "inkoova-academy-tests-content"),
                ["Academy__ContentTokenKey"] = "test-content-token-key-not-for-production-use",
                ["Academy__Jwt__SigningKey"] = "test-jwt-signing-key-at-least-32-bytes-long!!",
                ["Academy__Certificates__SigningSecret"] = "test-certificate-secret",
                ["Academy__Stripe__SecretKey"] = "sk_test_dummy",
                ["Academy__Stripe__WebhookSecret"] = "whsec_dummy",
                // Nada aquí debe alcanzar a Stripe: el seed crearía precios de verdad.
                ["Academy__Seed__Enabled"] = "false",
                ["Academy__Auth__RequireConfirmedEmail"] = "false"
            };

            foreach (var (key, value) in settings)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            Directory.CreateDirectory(settings["Academy__ContentRoot"]);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing");
}
