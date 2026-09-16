using System.Net.Http.Json;
using FluentAssertions;
using Inkoova.Academy.Infrastructure.Persistence;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// Existe por un fallo real: <c>X-Frame-Options: DENY</c> se aplicaba a toda la API,
/// incluido <c>/api/content</c>, que es exactamente lo que el player mete en un iframe. El
/// navegador rechazaba el documento y el player salía en blanco en TODAS las lecciones, sin
/// que fallara ninguna petición: la lección se servía con 200 y el fallo solo aparecía en la
/// consola. Ningún test que mirase códigos de estado podía verlo.
///
/// Las dos aserciones van juntas a propósito: relajar la cabecera del contenido no debe
/// relajarla en el resto.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SecurityHeaderTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private AcademyApiFactory _factory = null!;
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        DapperTypeHandlers.Register();
        _factory = new AcademyApiFactory(fixture.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task The_content_endpoint_can_be_framed_by_the_player()
    {
        // El token es inválido a propósito: la cabecera la pone el middleware y viaja también
        // con el 403, así que no hace falta contenido sembrado para comprobarla.
        var response = await _client.GetAsync("/api/content/token-invalido", Ct);

        FrameOptionsOf(response).Should().Be(
            "SAMEORIGIN",
            "el contenido importado se sirve dentro del iframe del player; con DENY el navegador lo rechaza");
    }

    [Fact]
    public async Task Every_other_endpoint_still_refuses_to_be_framed()
    {
        var response = await _client.GetAsync("/api/courses", Ct);

        FrameOptionsOf(response).Should().Be("DENY");
    }

    [Fact]
    public async Task A_refresh_without_session_does_not_leave_a_session_hint_behind()
    {
        var response = await _client.PostAsync("/api/auth/refresh", content: null, Ct);

        // La marca es lo que decide si el SPA vuelve a intentarlo. Si sobreviviera a un
        // refresco fallido, un visitante anónimo pediría una renovación en cada carga.
        SetCookies(response).Should().NotContain(
            value => value.StartsWith("ink_session=1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Logging_in_publishes_the_session_hint_next_to_the_refresh_cookie()
    {
        const string password = "Contrasena-De-Prueba-1";
        var email = $"cabeceras-{Guid.CreateVersion7().ToString("N")[^12..]}@test.local";

        (await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, password, displayName = "Alumno de prueba" },
            Ct)).EnsureSuccessStatusCode();

        var login = await _client.PostAsJsonAsync("/api/auth/login", new { email, password }, Ct);
        login.EnsureSuccessStatusCode();

        var cookies = SetCookies(login).ToList();

        cookies.Should().Contain(value => value.StartsWith("ink_rt=", StringComparison.Ordinal));

        // Legible por JavaScript y en la raíz: si fuera HttpOnly o quedara bajo /api/auth el
        // SPA no podría leerla y no serviría para nada.
        var hint = cookies.Should()
            .ContainSingle(value => value.StartsWith("ink_session=", StringComparison.Ordinal))
            .Subject;

        var attributes = hint.ToLowerInvariant();
        attributes.Should().NotContain("httponly", "el SPA tiene que poder leerla");
        attributes.Should().NotContain("path=/api/auth", "fuera de esa ruta el SPA no la vería");
        attributes.Should().Contain("path=/");
    }

    private static string? FrameOptionsOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues("X-Frame-Options", out var values)
            ? values.Single()
            : null;

    private static IEnumerable<string> SetCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
}
