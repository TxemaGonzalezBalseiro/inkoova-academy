using System.Net;
using FluentAssertions;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// Qué abre un token de contenido y qué no.
///
/// Existe por un fallo que no rompía ninguna petición: el documento se servía en
/// <c>/api/content/{token}</c>, sin profundidad de ruta, y un bloque que pide
/// <c>../assets/css/curso.css</c> resolvía ese <c>..</c> fuera del token. Las lecciones
/// salían sin estilos y sin JS con un 200 en el player y 404 en la consola.
///
/// La ruta canónica arregla eso ampliando lo que el token alcanza, y por eso el límite se
/// fija aquí: los assets compartidos del curso entran, los documentos ajenos no. Si entraran,
/// el token de una lección gratuita abriría los bloques de pago del mismo curso.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class ContentScopeTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string LessonRef = "curso-alcance/bloques/B0.html#slide-3";

    private AcademyApiFactory _factory = null!;
    private HttpClient _client = null!;
    private string _token = null!;

    public Task InitializeAsync()
    {
        DapperTypeHandlers.Register();
        _factory = new AcademyApiFactory(fixture.ConnectionString);

        // Sin seguir redirecciones: una de las aserciones es sobre la redirección misma.
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        using var scope = _factory.Services.CreateScope();
        _token = scope.ServiceProvider
            .GetRequiredService<IContentTokenService>()
            .Issue(LessonRef, TimeSpan.FromMinutes(5));

        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    [Fact]
    public async Task The_short_url_redirects_to_the_real_path_of_the_file()
    {
        var response = await _client.GetAsync($"/api/content/{_token}", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Found);

        // Con la ruta real, `../assets/…` cae dentro del token en vez de salirse.
        response.Headers.Location!.ToString()
            .Should().Be($"/api/content/{_token}/curso-alcance/bloques/B0.html#slide-3");
    }

    [Fact]
    public async Task The_shared_chrome_of_the_course_is_within_reach()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}/curso-alcance/assets/css/curso.css", Ct);

        // 404 y no 403: el fichero no está sembrado, pero el token sí lo ampara. Lo que se
        // comprueba es el permiso, no que exista.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_lesson_reaches_its_own_data_folder()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}/curso-alcance/bloques/B0.data/quizzes.js", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_lesson_does_not_reach_the_data_folder_of_another_lesson()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}/curso-alcance/bloques/B7.data/quizzes.js", Ct);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "ahí están las preguntas y los model_answer de un bloque que no se ha pagado");
    }

    [Fact]
    public async Task Another_lesson_of_the_same_course_is_not()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}/curso-alcance/bloques/B7.html", Ct);

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "si bastara con ser del mismo curso, una lección gratuita abriría los bloques de pago");
    }

    [Fact]
    public async Task Nothing_of_another_course_is_within_reach()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}/otro-curso/assets/css/curso.css", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_forged_token_opens_nothing()
    {
        var response = await _client.GetAsync(
            $"/api/content/{_token}x/curso-alcance/assets/css/curso.css", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ---------------------------------------------------------------------------------------
    // /authorize: lo mismo que arriba, pero sin bytes. Es lo que pregunta Caddy antes de
    // servir el fichero él (ADR-012), así que el alcance tiene que ser exactamente el mismo:
    // si estas respuestas y las de arriba se separan, el muro de pago dice dos cosas distintas
    // según quién sirva el fichero, que es un fallo que no se ve hasta que alguien lo explota.
    // ---------------------------------------------------------------------------------------

    private Task<HttpResponseMessage> Authorize(string forwardedUri)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/content/authorize");
        request.Headers.Add("X-Forwarded-Uri", forwardedUri);
        return _client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Authorize_answers_with_the_file_that_Caddy_has_to_serve()
    {
        var response = await Authorize($"/api/content/{_token}/curso-alcance/bloques/B0.html?theme=dark");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("X-Content-File").Single()
            .Should().Be("/curso-alcance/bloques/B0.html");
    }

    [Fact]
    public async Task Authorize_reaches_the_shared_chrome_of_the_course()
    {
        var response = await Authorize($"/api/content/{_token}/curso-alcance/assets/css/curso.css");

        // 204 aunque el fichero no exista: aquí se decide el permiso y nada más. Si no está,
        // el 404 lo da el file_server, que es quien mira el disco.
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Authorize_refuses_a_lesson_of_the_same_course()
    {
        var response = await Authorize($"/api/content/{_token}/curso-alcance/bloques/B7.html");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authorize_refuses_the_data_folder_of_another_lesson()
    {
        var response = await Authorize($"/api/content/{_token}/curso-alcance/bloques/B7.data/quizzes.js");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authorize_refuses_a_forged_token()
    {
        var response = await Authorize($"/api/content/{_token}x/curso-alcance/assets/css/curso.css");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Authorize_refuses_a_request_that_did_not_come_through_the_proxy()
    {
        var response = await _client.GetAsync("/api/content/authorize", Ct);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// El <c>..</c> ya no lo detiene el disco: lo que aprueba esta ruta es lo que Caddy sirve.
    /// Un cliente HTTP normaliza la ruta antes de enviarla, así que este vector solo se puede
    /// probar aquí, donde la URI original llega en una cabecera y sin tocar.
    /// </summary>
    [Theory]
    [InlineData("curso-alcance/assets/../../../etc/passwd")]
    [InlineData("curso-alcance/assets/..%2f..%2f..%2fetc%2fpasswd")]
    [InlineData("curso-alcance/assets/%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("curso-alcance/assets/./../../etc/passwd")]
    public async Task Authorize_refuses_anything_that_climbs_out_of_the_volume(string path)
    {
        var response = await Authorize($"/api/content/{_token}/{path}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        response.Headers.Contains("X-Content-File").Should().BeFalse();
    }
}
