using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using FluentAssertions;
using Inkoova.Academy.Application.Abstractions;
using Inkoova.Academy.Domain.Billing;
using Inkoova.Academy.Infrastructure.Invoicing;

namespace Inkoova.Academy.Integration.Tests;

/// <summary>
/// El XML que se remite, validado contra los XSD OFICIALES de la AEAT.
///
/// Es lo único que permite saber que el registro está bien antes de mandarlo. La alternativa es
/// enterarse por un rechazo, que llega horas después por el reintento diario y no dice qué campo
/// está mal, sino que el documento no valida.
///
/// Los esquemas están en <c>docs/verifactu/esquemas/</c>, descargados del portal de
/// desarrolladores. Se cargan desde ahí y no de una copia en el proyecto de test: si alguien los
/// actualiza, estos tests empiezan a validar contra la versión nueva sin tocarlos.
/// </summary>
public sealed class VerifactuXmlSchemaTests
{
    private static readonly XmlSchemaSet Schemas = LoadSchemas();

    [Fact]
    public void Un_alta_encadenada_valida_contra_el_esquema()
    {
        var previous = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash);
        var current = Alta("INK-2026-0002", new DateOnly(2026, 1, 11), previous.Hash);

        var xml = VerifactuXmlBuilder.BuildSubmission(
        [
            Submission(current, previous.SeriesNumber, previous.IssueDate)
        ]);

        Validate(xml).Should().BeEmpty();
    }

    [Fact]
    public void El_primer_registro_de_un_emisor_valida_y_va_como_PrimerRegistro()
    {
        var first = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash);

        var xml = VerifactuXmlBuilder.BuildSubmission([Submission(first)]);

        Validate(xml).Should().BeEmpty();

        // Los 64 ceros son marca interna nuestra: no pueden viajar como si fueran una huella.
        xml.ToString().Should().NotContain(VerifactuRecord.GenesisHash);
        xml.Descendants(VerifactuXmlBuilder.Sf + "PrimerRegistro")
            .Should().ContainSingle().Which.Value.Should().Be("S");
    }

    [Fact]
    public void Una_rectificativa_valida_y_declara_la_factura_que_rectifica()
    {
        var previous = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash);

        var corrective = VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Alta,
            "B12345674", "Inkoova SL", "R5", "INK-2026-0002", new DateOnly(2026, 2, 1),
            "Reembolso del plan Starter", -2299, -399, "EUR",
            rectifies: "INK-2026-0001",
            previous.Hash, new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(1)), true).Value;

        var xml = VerifactuXmlBuilder.BuildSubmission(
        [
            Submission(corrective, previous.SeriesNumber, previous.IssueDate, taxCents: -399, baseCents: -1900)
        ]);

        Validate(xml).Should().BeEmpty();

        xml.Descendants(VerifactuXmlBuilder.Sf + "NumSerieFactura")
            .Select(e => e.Value).Should().Contain("INK-2026-0001");
    }

    [Fact]
    public void Una_anulacion_valida_contra_su_propio_tipo()
    {
        var previous = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash);

        var cancellation = VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Anulacion,
            "B12345674", "Inkoova SL", "F2", "INK-2026-0001", new DateOnly(2026, 1, 10),
            "Anulación", 0, 0, "EUR", null,
            previous.Hash, new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(1)), true).Value;

        var xml = VerifactuXmlBuilder.BuildSubmission(
        [
            Submission(cancellation, previous.SeriesNumber, previous.IssueDate)
        ]);

        Validate(xml).Should().BeEmpty();
    }

    [Fact]
    public void Las_fechas_van_en_dd_MM_yyyy_y_no_en_ISO()
    {
        var record = Alta("INK-2026-0001", new DateOnly(2026, 3, 4), VerifactuRecord.GenesisHash);

        var xml = VerifactuXmlBuilder.BuildSubmission([Submission(record)]);

        // El tipo `sf:fecha` del esquema NO es ISO. Es el error más fácil de cometer aquí,
        // porque todo lo demás del código usa ISO y sale solo.
        xml.Descendants(VerifactuXmlBuilder.Sf + "FechaExpedicionFactura")
            .Should().ContainSingle().Which.Value.Should().Be("04-03-2026");
    }

    [Fact]
    public void La_marca_temporal_del_XML_es_la_misma_que_se_firmo_en_la_huella()
    {
        var moment = new DateTimeOffset(2026, 1, 10, 9, 30, 0, TimeSpan.FromHours(1));
        var record = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash, moment);

        var xml = VerifactuXmlBuilder.BuildSubmission([Submission(record)]);

        var declared = xml.Descendants(VerifactuXmlBuilder.Sf + "FechaHoraHusoGenRegistro")
            .Single().Value;

        // El mismo instante escrito como Z y como +01:00 da huellas distintas. Si el XML lleva
        // una y la huella se calculó sobre la otra, la AEAT rehace la huella y no le cuadra.
        record.HashInput.Should().Contain($"FechaHoraHusoGenRegistro={declared}");
    }

    [Fact]
    public void Un_lote_con_dos_emisores_no_se_compone()
    {
        var uno = Alta("INK-2026-0001", new DateOnly(2026, 1, 10), VerifactuRecord.GenesisHash);

        var otro = VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Alta,
            "B87654321", "Otra Marca SL", "F2", "OTR-2026-0001", new DateOnly(2026, 1, 10),
            "Curso", 2299, 399, "EUR", null,
            VerifactuRecord.GenesisHash, DateTimeOffset.UtcNow, true).Value;

        // El NIF del obligado va en la CABECERA, una por envío: un lote mezclado declararía las
        // facturas de un emisor a nombre de otro, y el esquema lo aceptaría tan contento.
        var mezclado = () => VerifactuXmlBuilder.BuildSubmission([Submission(uno), Submission(otro)]);

        mezclado.Should().Throw<InvalidOperationException>().WithMessage("*mezclar emisores*");
    }

    [Fact]
    public void Sin_identificar_el_registro_anterior_no_se_compone_el_encadenamiento()
    {
        var record = Alta("INK-2026-0002", new DateOnly(2026, 1, 11), new string('A', 64));

        // Rellenar el bloque a ojo pasaría la validación y declararía una cadena que apunta a
        // una factura que no existe. Eso no se detecta luego.
        var incompleto = () => VerifactuXmlBuilder.BuildSubmission([Submission(record)]);

        incompleto.Should().Throw<InvalidOperationException>().WithMessage("*registro anterior*");
    }

    // ── andamiaje ────────────────────────────────────────────────────────────────────────

    private static VerifactuRecord Alta(
        string number, DateOnly date, string previousHash, DateTimeOffset? generatedAt = null) =>
        VerifactuRecord.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), VerifactuKind.Alta,
            "B12345674", "Inkoova SL", "F2", number, date,
            "Suscripción al plan Starter", 2299, 399, "EUR", null,
            previousHash,
            generatedAt ?? new DateTimeOffset(date, new TimeOnly(9, 30), TimeSpan.FromHours(1)),
            true).Value;

    private static VerifactuSubmission Submission(
        VerifactuRecord record,
        string? previousNumber = null,
        DateOnly? previousDate = null,
        long taxCents = 399,
        long baseCents = 1900) =>
        new(record,
            new InvoiceIssuer(Guid.Empty, record.IssuerName, record.IssuerTaxId, "Calle Mayor 1"),
            new VerifactuSoftware(
                "Inkoova SL", "B12345674", "Inkoova Academy", "01", "1.0.0", "001",
                OnlyVerifactu: true, MultiTaxpayerCapable: false, MultiTaxpayerInUse: false),
            [new TaxBreakdownLine(21m, baseCents, taxCents)],
            CustomerName: null,
            CustomerTaxId: null,
            CustomerCountry: "ES",
            PreviousSeriesNumber: previousNumber,
            PreviousIssueDate: previousDate);

    private static IReadOnlyList<string> Validate(XDocument document)
    {
        var problems = new List<string>();

        document.Validate(Schemas, (_, e) => problems.Add($"{e.Severity}: {e.Message}"));

        return problems;
    }

    /// <summary>
    /// Carga los esquemas oficiales desde <c>docs/verifactu/esquemas/</c>.
    ///
    /// <c>SuministroInformacion.xsd</c> importa el esquema de firma XML de la W3C por URL,
    /// porque <c>RegistroAlta</c> admite una <c>ds:Signature</c> opcional —la que usan los
    /// sistemas NO Veri*Factu—. Sin ese esquema el conjunto no compila, aunque nosotros no
    /// firmemos nada. Está bajado junto a los demás para que estos tests no dependan de que
    /// w3.org conteste: un test que se cae porque una web de fuera está lenta deja de mirarse.
    /// </summary>
    private static XmlSchemaSet LoadSchemas()
    {
        var folder = FindSchemaFolder();

        var set = new XmlSchemaSet { XmlResolver = new LocalOnlyResolver(folder) };

        foreach (var file in new[]
                 {
                     "xmldsig-core-schema.xsd", "SuministroInformacion.xsd", "SuministroLR.xsd"
                 })
        {
            // DTD procesado: el esquema de la W3C trae una declaración interna con las entidades
            // que usan sus propios tipos, y sin ella no se lee.
            using var reader = XmlReader.Create(
                Path.Combine(folder, file),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Parse });

            set.Add(null, reader);
        }

        set.Compile();

        return set;
    }

    private static string FindSchemaFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);

        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "docs", "verifactu", "esquemas");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            folder = folder.Parent;
        }

        throw new DirectoryNotFoundException(
            "No se encuentra docs/verifactu/esquemas. Son los esquemas oficiales de la AEAT y " +
            "sin ellos este test no comprueba nada.");
    }

    /// <summary>Resuelve solo lo que está en la carpeta; lo de fuera se ignora, no se descarga.</summary>
    private sealed class LocalOnlyResolver(string folder) : XmlUrlResolver
    {
        public override object? GetEntity(Uri absoluteUri, string? role, Type? typeOfObjectToReturn)
        {
            var local = Path.Combine(folder, Path.GetFileName(absoluteUri.LocalPath));

            return File.Exists(local) && absoluteUri.IsFile
                ? base.GetEntity(new Uri(local), role, typeOfObjectToReturn)
                : null;
        }
    }
}
