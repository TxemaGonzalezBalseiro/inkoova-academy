using System.Globalization;
using System.Text;
using Inkoova.Academy.Application.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Inkoova.Academy.Infrastructure.Documents;

/// <summary>
/// El certificado: PDF para descargar e imagen para enseñar.
///
/// Los dos salen del MISMO documento, rasterizando el PDF. Un diseño paralelo para la imagen
/// acabaría divergiendo, y entonces el certificado que alguien enseña en LinkedIn no sería el
/// que se puede verificar.
///
/// Determinista a propósito (criterio de aceptación de T-10): sin marcas de tiempo, sin
/// identificadores aleatorios y con id de documento fijo, así el mismo modelo produce siempre
/// los mismos bytes y un certificado se puede regenerar y comparar. Todo lo que se dibuja
/// —incluidos el guilloché, el sello y la marca de agua— es geometría calculada a partir de
/// constantes o del propio modelo; nada depende del reloj ni del entorno.
///
/// El diseño se lee en dos escalas. De cerca: banda de marca, marco con filete doble, guilloché
/// de fondo, sello y bloque de verificación. En miniatura (LinkedIn muestra ~300 px de ancho):
/// banda azul maciza arriba, nombre enorme en el centro y sello abajo a la derecha. Si algo solo
/// funcionase a tamaño completo, sobra.
///
/// TODO(modelo): <see cref="CertificatePdfModel"/> no trae el ámbito del certificado
/// (<c>CertificateScope.Course</c> / <c>Program</c>), así que aquí no se puede distinguir un
/// curso de un programa sin adivinarlo por el título, que sería inventar un dato. Mientras no se
/// añada el campo, los textos son neutros ("ha completado con aprovechamiento") y valen para los
/// dos casos. Con el campo, la banda pasaría a decir "Certificado de programa" y el sello podría
/// llevar un anillo distinto.
/// </summary>
public sealed class CertificatePdfGenerator : ICertificatePdfGenerator
{
    // Neutros del documento. No son marca, así que no se configuran: la tinta de un texto y el
    // color del papel no cambian porque cambie la marca que emite.
    private const string Ink = "#0F172A";
    private const string Muted = "#64748B";
    private const string Hairline = "#E2E8F0";
    private const string Paper = "#FCFCFD";

    // Los tres de la academia, cuando la marca no dice otra cosa.
    private const string DefaultPrimary = "#1E3A8A";
    private const string DefaultAccent = "#DB2777";
    private const string DefaultSupport = "#0891B2";

    /// <summary>
    /// Los colores de un certificado concreto.
    ///
    /// Solo se configuran tres —principal, acento y apoyo—. Los demás se CALCULAN a partir del
    /// principal mezclándolo con negro o con blanco. Si se dejaran configurar por separado,
    /// bastaría con cambiar el principal y olvidar el resto para que el marco y la banda
    /// siguieran del color anterior y el documento saliera descosido.
    /// </summary>
    private sealed record Palette(
        string Blue,
        string Magenta,
        string Teal,
        string BlueDeep,
        string BandText,
        string BandLine,
        string BandGuilloche,
        string FrameLine,
        string FrameSoft,
        string Guilloche)
    {
        public static Palette From(CertificateStyle? style)
        {
            var primary = Hex(style?.PrimaryColor, DefaultPrimary);
            var accent = Hex(style?.AccentColor, DefaultAccent);
            var support = Hex(style?.SupportColor, DefaultSupport);

            return new Palette(
                primary,
                accent,
                support,
                // Las mismas proporciones que tenían los tonos originales respecto al azul de
                // marca, para que un principal nuevo produzca el mismo documento en otro color.
                Mix(primary, "#000000", 0.18),
                Mix(primary, "#FFFFFF", 0.72),
                Mix(primary, "#FFFFFF", 0.28),
                Mix(primary, "#FFFFFF", 0.12),
                Mix(primary, "#FFFFFF", 0.62),
                Mix(primary, "#FFFFFF", 0.80),
                Mix(primary, "#FFFFFF", 0.93));
        }

        /// <summary>Un color válido, o el de siempre. Nunca deja pasar algo que QuestPDF no entienda.</summary>
        private static string Hex(string? value, string fallback)
        {
            var candidate = (value ?? string.Empty).Trim();

            return System.Text.RegularExpressions.Regex.IsMatch(candidate, "^#[0-9A-Fa-f]{6}$")
                ? candidate.ToUpperInvariant()
                : fallback;
        }

        /// <summary>Mezcla lineal en RGB. Suficiente para aclarar y oscurecer un tono de marca.</summary>
        private static string Mix(string from, string to, double amount)
        {
            static (int R, int G, int B) Parse(string hex) => (
                Convert.ToInt32(hex.Substring(1, 2), 16),
                Convert.ToInt32(hex.Substring(3, 2), 16),
                Convert.ToInt32(hex.Substring(5, 2), 16));

            var (r1, g1, b1) = Parse(from);
            var (r2, g2, b2) = Parse(to);

            static int Blend(int a, int b, double t) => (int)Math.Round(a + ((b - a) * t));

            return $"#{Blend(r1, r2, amount):X2}{Blend(g1, g2, amount):X2}{Blend(b1, b2, amount):X2}";
        }
    }

    // Geometría del papel (A4 apaisado, en puntos). La banda y el marco se pintan en el fondo de
    // la página, así que necesitan medidas fijas para cuadrar con cabecera, cuerpo y pie.
    private const float BandHeight = 104f;
    private const float RibbonHeight = 5f;
    private const float FrameInsetSide = 30f;
    private const float FrameInsetTop = 16f;
    private const float FrameInsetBottom = 22f;
    private const float Gutter = 66f;

    public byte[] Generate(CertificatePdfModel model) => Build(model).GeneratePdf();

    /// <summary>
    /// PNG del certificado, para compartir. 150 ppp sobre A4 apaisado da ~1754 px de ancho:
    /// suficiente para que LinkedIn no lo vea borroso y bastante menos que un escaneo.
    /// </summary>
    public byte[] GenerateImage(CertificatePdfModel model) =>
        Build(model)
            .GenerateImages(new ImageGenerationSettings
            {
                ImageFormat = ImageFormat.Png,
                RasterDpi = 150
            })
            .First();

    private static IDocument Build(CertificatePdfModel model)
    {
        // Sin esto, QuestPDF sella el PDF con el reloj del sistema y dos generaciones del MISMO
        // certificado se diferencian en los bytes de /CreationDate. La fecha de emisión a
        // medianoche UTC sale del modelo, es la fecha que el documento ya declara y no depende
        // ni del reloj ni de la zona horaria de la máquina que lo genere.
        var issued = model.IssueDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // Una paleta por documento: es lo que permite que dos marcas emitan el mismo
        // certificado en colores distintos sin duplicar una línea de dibujo.
        var p = Palette.From(model.Style);
        var style = model.Style ?? CertificateStyle.Default;
        var issuer = style.Or(style.IssuerName, "Inkoova Academy");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.PageColor(Paper);
                page.DefaultTextStyle(text => text.FontFamily(Fonts.Calibri).FontColor(Ink));

                // Sin márgenes de página: el aire lo ponen los propios elementos y así la banda
                // superior va a sangre, que es lo que hace reconocible el documento en miniatura.
                page.Margin(0);

                // Marco y marca de agua van de fondo para que envuelvan cabecera, cuerpo y pie.
                page.Background().Element(c => Backdrop(c, p));

                // Cabecera, cuerpo y pie de QuestPDF, y no una columna con `Extend`: el pie de
                // página queda anclado abajo por construcción. Con `Extend` el cuerpo se comía
                // el espacio del pie y el código de verificación y el QR desaparecían.
                page.Header().Element(c => Masthead(c, p, style));

                // Un poco más de aire abajo que arriba: centrado matemático, el bloque se ve caído.
                page.Content()
                    .PaddingHorizontal(Gutter)
                    .PaddingTop(18)
                    .PaddingBottom(48)
                    .AlignMiddle()
                    .Column(body => Recipient(body, model, p));

                page.Footer()
                    .PaddingHorizontal(FrameInsetSide + 26)
                    .PaddingBottom(FrameInsetBottom + 16)
                    .Element(footer => Footer(footer, model, p, style));
            });
        })
        .WithMetadata(new DocumentMetadata
        {
            // También las propiedades del PDF: son lo que enseña el lector en «Documento ▸
            // Propiedades», y ahí no puede aparecer una marca distinta de la que firma el papel.
            Title = $"Certificado {model.Code} · {issuer}",
            Author = issuer,
            Subject = model.CourseTitle,
            Creator = issuer,
            Producer = issuer,
            Language = "es-ES",
            CreationDate = issued,
            ModifiedDate = issued
        });
    }

    /// <summary>
    /// Fondo del papel: hueco para la banda superior (que pinta la cabecera) y, debajo, el marco
    /// con el guilloché. Un certificado sin marco se lee como una carta; el borde y la trama son
    /// lo que hace que se reconozca como documento acreditativo de un vistazo.
    /// </summary>
    private static void Backdrop(IContainer container, Palette p) =>
        container.Column(sheet =>
        {
            sheet.Item().Height(BandHeight);

            sheet.Item()
                .Extend()
                .PaddingHorizontal(FrameInsetSide)
                .PaddingTop(FrameInsetTop)
                .PaddingBottom(FrameInsetBottom)
                .Element(c => Frame(c, p));
        });

    /// <summary>
    /// Filete doble, marca de agua de guilloché y cantoneras. Todo muy bajo de contraste:
    /// tiene que notarse que está, sin competir con el nombre.
    /// </summary>
    private static void Frame(IContainer container, Palette p) =>
        container
            .Border(1)
            .BorderColor(p.FrameLine)
            .Padding(4)
            .Border(0.75f)
            .BorderColor(p.FrameSoft)
            .Layers(layers =>
            {
                // Arriba y no en el centro geométrico: así el rosetón queda detrás del nombre,
                // que es donde una marca de agua tiene sentido, y no encima del QR.
                layers.PrimaryLayer()
                    .AlignTop()
                    .AlignCenter()
                    .PaddingTop(26)
                    .Width(258)
                    .Height(258)
                    .Svg(RosetteSvg(p.Guilloche, 0.9f));

                // Las cantoneras van en capas alineadas arriba y abajo, no en una columna con un
                // hueco elástico: una columna reservaría el alto entero para el hueco y el marco
                // no cabría.
                layers.Layer().AlignTop().Padding(8).Element(top => CornerPair(top, flipY: false, p));
                layers.Layer().AlignBottom().Padding(8).Element(low => CornerPair(low, flipY: true, p));
            });

    private static void CornerPair(IContainer container, bool flipY, Palette p) =>
        container.Row(row =>
        {
            row.ConstantItem(34).Height(34).Svg(CornerSvg(flipX: false, flipY: flipY, p));
            row.RelativeItem();
            row.ConstantItem(34).Height(34).Svg(CornerSvg(flipX: true, flipY: flipY, p));
        });

    /// <summary>
    /// Banda de marca a sangre. Es la mancha que identifica el documento cuando se ve pequeño,
    /// así que lleva el emisor y qué es, y nada más.
    /// </summary>
    private static void Masthead(IContainer container, Palette p, CertificateStyle style) =>
        container.Height(BandHeight).Background(p.Blue).Column(band =>
        {
            band.Item().Height(BandHeight - RibbonHeight).Layers(layers =>
            {
                layers.PrimaryLayer().PaddingTop(24).Column(title =>
                {
                    // En mayúsculas siempre: es una banda, no un párrafo. Se hace aquí y no se
                    // le pide a quien lo escribe, que acabaría con una marca en mayúsculas y
                    // otra no.
                    title.Item().AlignCenter()
                        .Text(style.Or(style.Heading, "INKOOVA ACADEMY").ToUpperInvariant())
                        .FontSize(13).LetterSpacing(0.5f).FontColor(Colors.White).Bold();

                    title.Item().PaddingTop(9).AlignCenter().Width(220).Row(rule =>
                    {
                        rule.RelativeItem().PaddingVertical(3).Height(1).Background(p.BandLine);
                        rule.ConstantItem(26).Height(7).Svg(DiamondSvg(p.Magenta));
                        rule.RelativeItem().PaddingVertical(3).Height(1).Background(p.BandLine);
                    });

                    title.Item().PaddingTop(7).AlignCenter()
                        .Text(style.Or(style.Subheading, "CERTIFICADO DE APROVECHAMIENTO").ToUpperInvariant())
                        .FontSize(9.5f).LetterSpacing(0.38f).FontColor(p.BandText).SemiBold();
                });

                // Guilloché de seguridad en los extremos: textura, no dibujo. A tamaño completo se
                // ve la trama; en miniatura solo aporta que la banda no sea un rectángulo plano.
                layers.Layer().AlignMiddle().Row(texture =>
                {
                    texture.ConstantItem(94).Height(94).OffsetX(-32)
                        .Svg(RosetteSvg(p.BandGuilloche, 0.5f));

                    texture.RelativeItem();

                    texture.ConstantItem(94).Height(94).OffsetX(32)
                        .Svg(RosetteSvg(p.BandGuilloche, 0.5f));
                });
            });

            // Cinta de marca al pie de la banda, en proporción 6:3:1. El tramo largo va en un
            // azul más profundo que la banda: con el mismo azul no se vería que hay cinta.
            band.Item().Height(RibbonHeight).Row(bar =>
            {
                bar.RelativeItem(6).Background(p.BlueDeep);
                bar.RelativeItem(3).Background(p.Magenta);
                bar.RelativeItem(1).Background(p.Teal);
            });
        });

    /// <summary>
    /// El bloque que se mira primero. El nombre manda: es lo más grande de la página por un
    /// margen amplio, y todo lo demás está calibrado para no discutirle la jerarquía.
    /// </summary>
    private static void Recipient(ColumnDescriptor body, CertificatePdfModel model, Palette p)
    {
        body.Item().AlignCenter().Text("SE CERTIFICA QUE")
            .FontSize(8.5f).LetterSpacing(0.32f).FontColor(Muted).SemiBold();

        body.Item().PaddingTop(10).AlignCenter().Text(model.StudentName)
            .FontSize(NameSize(model.StudentName)).LineHeight(1.05f).FontColor(p.Blue).Bold();

        body.Item().PaddingTop(10).AlignCenter().Width(260).Height(12).Svg(FlourishSvg(p));

        body.Item().PaddingTop(10).AlignCenter().Text("ha completado con aprovechamiento")
            .FontSize(10.5f).FontColor(Muted);

        body.Item().PaddingTop(7).AlignCenter().Text(model.CourseTitle)
            .FontSize(TitleSize(model.CourseTitle)).LineHeight(1.15f).FontColor(Ink).SemiBold();

        body.Item().PaddingTop(18).AlignCenter().Element(meta => Meta(meta, model));
    }

    /// <summary>
    /// El nombre se ajusta al ancho por tramos, como en cualquier diploma: un nombre largo baja
    /// de cuerpo antes que partirse en dos líneas. Depende solo del modelo, así que no rompe el
    /// determinismo, y evita que un nombre de 50 caracteres empuje el bloque a una segunda página.
    /// </summary>
    private static float NameSize(string name) => name.Length switch
    {
        <= 24 => 46f,
        <= 32 => 40f,
        <= 42 => 33f,
        <= 56 => 27f,
        _ => 22f
    };

    private static float TitleSize(string title) => title.Length switch
    {
        <= 34 => 21f,
        <= 56 => 18f,
        <= 80 => 16f,
        _ => 14f
    };

    /// <summary>
    /// Duración y fecha como dos campos etiquetados, no como una frase. Un dato con etiqueta se
    /// lee como un dato acreditado; la misma información en una línea de texto, como una nota.
    /// </summary>
    private static void Meta(IContainer container, CertificatePdfModel model) =>
        container.Row(row =>
        {
            // Horas a 0 significa "no consta" (le pasa a los certificados de programa que se
            // rasterizan sin el total del programa). Un campo vacío se calla; no se rellena con
            // un cero que parecería un dato.
            if (model.Hours > 0)
            {
                row.AutoItem().Element(cell => MetaField(cell, "DURACIÓN", $"{model.Hours} horas"));

                row.AutoItem().PaddingHorizontal(22).PaddingVertical(2)
                    .LineVertical(0.75f).LineColor(Hairline);
            }

            row.AutoItem().Element(cell => MetaField(
                cell,
                "FECHA DE EMISIÓN",
                model.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
        });

    private static void MetaField(IContainer container, string label, string value) =>
        container.Column(cell =>
        {
            cell.Item().AlignCenter().Text(label)
                .FontSize(6.5f).LetterSpacing(0.3f).FontColor(Muted).SemiBold();

            cell.Item().PaddingTop(3).AlignCenter().Text(value)
                .FontSize(11).FontColor(Ink).SemiBold();
        });

    /// <summary>
    /// Pie en tres columnas: qué acredita, cómo se comprueba y quién lo firma. El QR va en el
    /// centro porque es lo que se escanea, y el código en texto al lado por si no hay cámara.
    /// </summary>
    private static void Footer(IContainer container, CertificatePdfModel model, Palette p, CertificateStyle style) =>
        container.Column(body =>
        {
            body.Item().LineHorizontal(0.75f).LineColor(Hairline);

            body.Item().PaddingTop(14).Row(row =>
            {
                row.RelativeItem(5).AlignBottom().Column(left =>
                {
                    left.Item().Text("CÓDIGO DE VERIFICACIÓN")
                        .FontSize(7).LetterSpacing(0.25f).FontColor(Muted).SemiBold();

                    left.Item().PaddingTop(4).Text(model.Code)
                        .FontSize(19).LetterSpacing(0.04f).FontColor(p.Magenta).Bold();

                    left.Item().PaddingTop(6).Text(model.VerificationUrl)
                        .FontSize(7.5f).FontColor(Muted);

                    // El hash permite recalcular y comparar el certificado sin conexión.
                    left.Item().PaddingTop(3).Text($"SHA-256 {model.Hash[..32]}…")
                        .FontSize(6.5f).FontColor(Muted);
                });

                row.ConstantItem(104).AlignBottom().AlignCenter().Column(middle =>
                {
                    middle.Item()
                        .Border(0.75f)
                        .BorderColor(Hairline)
                        .Padding(4)
                        .Width(76)
                        .Height(76)
                        .Image(QrCode.Generate(model.VerificationUrl));

                    middle.Item().PaddingTop(5).AlignCenter().Text("Escanea para verificar")
                        .FontSize(6.5f).FontColor(Muted);
                });

                row.RelativeItem(5).AlignBottom().Row(right =>
                {
                    right.RelativeItem().PaddingRight(14).AlignBottom().Column(issuer =>
                    {
                        issuer.Item().AlignRight().Width(160).Height(1).Background(Hairline);

                        issuer.Item().PaddingTop(6).AlignRight()
                            .Text(style.Or(style.IssuerName, "Inkoova Academy"))
                            .FontSize(11.5f).FontColor(Ink).SemiBold();

                        issuer.Item().AlignRight()
                            .Text(style.Or(style.IssuerNote, "Entidad emisora"))
                            .FontSize(7.5f).LetterSpacing(0.2f).FontColor(Muted);
                    });

                    right.ConstantItem(86).Element(seal => Seal(seal, model, p, style));
                });
            });

            // Lo que este documento NO es. Decirlo aquí, centrado y en el borde, evita que se
            // presente como un título oficial, que es lo que la ley no permite (T-14).
            body.Item().PaddingTop(14).LineHorizontal(0.75f).LineColor(Hairline);

            body.Item().PaddingTop(6).AlignCenter().Text("Formación privada. No es una titulación oficial.")
                .FontSize(7).LetterSpacing(0.2f).FontColor(Muted);
        });

    /// <summary>
    /// Sello vectorial: anillos, dentado y rosetón, con el emisor y el año dentro. Los textos los
    /// pone QuestPDF y no el SVG, porque el texto dentro de un SVG depende de qué fuentes tenga
    /// la máquina y eso rompería el determinismo.
    /// </summary>
    private static void Seal(
        IContainer container, CertificatePdfModel model, Palette p, CertificateStyle style)
    {
        // El sello lleva el emisor partido en dos líneas. Se parte por la ÚLTIMA palabra:
        // «Inkoova Academy» da «INKOOVA / ACADEMY», y un emisor de una sola palabra deja la
        // segunda línea vacía en vez de repetirse.
        var issuer = style.Or(style.IssuerName, "Inkoova Academy").ToUpperInvariant();
        var cut = issuer.LastIndexOf(' ');
        var (top, bottom) = cut == -1 ? (issuer, string.Empty) : (issuer[..cut], issuer[(cut + 1)..]);

        container.Width(86).Height(86).Layers(layers =>
        {
            layers.PrimaryLayer().Svg(SealSvg(p));

            layers.Layer().AlignMiddle().AlignCenter().Column(mark =>
            {
                mark.Item().AlignCenter().Text(top)
                    .FontSize(8).LetterSpacing(0.16f).FontColor(p.Blue).Bold();

                mark.Item().PaddingTop(2).AlignCenter().Width(26).Height(1).Background(p.Magenta);

                mark.Item().PaddingTop(2).AlignCenter().Text(bottom)
                    .FontSize(6.5f).LetterSpacing(0.16f).FontColor(p.Blue).SemiBold();

                mark.Item().PaddingTop(3).AlignCenter()
                    .Text(model.IssueDate.Year.ToString(CultureInfo.InvariantCulture))
                    .FontSize(7).LetterSpacing(0.2f).FontColor(p.Teal).SemiBold();
            });
        });
    }

    // ── Dibujo vectorial ────────────────────────────────────────────────────────────────────
    // Todo lo de aquí abajo es SVG generado con números fijos: sin recursos externos, sin texto
    // dentro del SVG y sin nada que dependa del entorno. Mismo modelo, mismos bytes.

    private const string SvgHeader =
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">";

    /// <summary>
    /// Hipotrocoide (el "espirógrafo" de toda la vida) muestreado como polilínea cerrada: es el
    /// trazo de guilloché de los documentos de seguridad y sale de dos senos y dos cosenos.
    /// </summary>
    private static readonly string RosettePath = BuildRosettePath(24d, 7d, 5d, 720);

    private static string RosetteSvg(string color, float width) =>
        string.Concat(
            SvgHeader,
            "<g fill=\"none\" stroke=\"", color, "\" stroke-width=\"", Num(width), "\">",
            "<path d=\"", RosettePath, "\"/>",
            // Segunda pasada girada media onda: es lo que teje la trama en vez de dejar una
            // línea suelta, igual que en un guilloché grabado.
            "<path d=\"", RosettePath, "\" transform=\"rotate(7.5 50 50)\"/>",
            "<circle cx=\"50\" cy=\"50\" r=\"47\" stroke-width=\"", Num(width * 0.7f), "\"/>",
            "<circle cx=\"50\" cy=\"50\" r=\"18\" stroke-width=\"", Num(width * 0.7f), "\"/>",
            "</g></svg>");

    private static string BuildRosettePath(double outerR, double innerR, double pen, int steps)
    {
        var span = outerR - innerR;
        var scale = 46d / (span + pen);
        var ratio = span / innerR;

        var path = new StringBuilder("M");

        for (var i = 0; i <= steps; i++)
        {
            var t = 2d * Math.PI * innerR * i / steps;
            var x = 50d + scale * ((span * Math.Cos(t)) + (pen * Math.Cos(ratio * t)));
            var y = 50d + scale * ((span * Math.Sin(t)) - (pen * Math.Sin(ratio * t)));

            if (i > 0)
            {
                path.Append('L');
            }

            path.Append(Num(x)).Append(' ').Append(Num(y));
        }

        return path.Append('Z').ToString();
    }

    /// <summary>Sello: dos anillos, dentado radial y rosetón interior. El texto va encima.</summary>
    private static string SealSvg(Palette p)
    {
        var svg = new StringBuilder(SvgHeader);

        svg.Append("<g fill=\"none\">");
        svg.Append("<circle cx=\"50\" cy=\"50\" r=\"48\" stroke=\"").Append(p.Blue)
            .Append("\" stroke-width=\"1.4\"/>");
        svg.Append("<circle cx=\"50\" cy=\"50\" r=\"42\" stroke=\"").Append(p.Teal)
            .Append("\" stroke-width=\"0.7\"/>");
        svg.Append("<circle cx=\"50\" cy=\"50\" r=\"31\" stroke=\"").Append(p.FrameLine)
            .Append("\" stroke-width=\"0.5\" stroke-dasharray=\"1 3\"/>");

        // Dentado entre los dos anillos exteriores: 48 marcas, como un canto acuñado.
        svg.Append("<g stroke=\"").Append(p.Blue).Append("\" stroke-width=\"0.8\">");

        for (var i = 0; i < 48; i++)
        {
            var angle = 2d * Math.PI * i / 48;
            var cos = Math.Cos(angle);
            var sin = Math.Sin(angle);

            svg.Append("<path d=\"M").Append(Num(50d + (42.8d * cos))).Append(' ')
                .Append(Num(50d + (42.8d * sin))).Append('L')
                .Append(Num(50d + (47.2d * cos))).Append(' ')
                .Append(Num(50d + (47.2d * sin))).Append("\"/>");
        }

        svg.Append("</g>");

        // Rosetón interior, del mismo trazo que la marca de agua para que el documento sea uno.
        svg.Append("<g stroke=\"").Append(p.FrameSoft).Append("\" stroke-width=\"0.55\">");
        svg.Append("<path transform=\"translate(50 50) scale(0.63) translate(-50 -50)\" d=\"")
            .Append(RosettePath).Append("\"/>");
        svg.Append("</g></g></svg>");

        return svg.ToString();
    }

    /// <summary>Filete corto bajo el nombre: dos líneas y un rombo. Cierra el bloque sin gritar.</summary>
    private static string FlourishSvg(Palette p) =>
        string.Concat(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 260 12\">",
            "<g stroke=\"", p.FrameLine, "\" stroke-width=\"1\">",
            "<path d=\"M0 6L104 6\"/><path d=\"M156 6L260 6\"/>",
            "</g>",
            "<path d=\"M112 6L118 2L124 6L118 10Z\" fill=\"", p.Teal, "\" opacity=\"0.55\"/>",
            "<path d=\"M130 6L137 1L144 6L137 11Z\" fill=\"", p.Magenta, "\"/>",
            "<path d=\"M148 6L154 2L160 6L154 10Z\" fill=\"", p.Teal, "\" opacity=\"0.55\"/>",
            "</svg>");

    /// <summary>Rombo suelto, para partir el filete de la banda superior.</summary>
    private static string DiamondSvg(string color) =>
        string.Concat(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 26 7\">",
            "<path d=\"M13 0L17 3.5L13 7L9 3.5Z\" fill=\"", color, "\"/>",
            "</svg>");

    /// <summary>
    /// Cantonera: dos escuadras y un rombo. Se dibuja una vez y se voltea para las otras tres
    /// esquinas, así las cuatro son la misma pieza y no cuatro dibujos que se desincronizan.
    /// </summary>
    private static string CornerSvg(bool flipX, bool flipY, Palette p)
    {
        var transform = string.Concat(
            "translate(", flipX ? "34" : "0", " ", flipY ? "34" : "0", ") ",
            "scale(", flipX ? "-1" : "1", " ", flipY ? "-1" : "1", ")");

        return string.Concat(
            "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 34 34\">",
            "<g transform=\"", transform, "\" fill=\"none\">",
            "<path d=\"M0 24L0 0L24 0\" stroke=\"", p.Blue, "\" stroke-width=\"1.4\"/>",
            "<path d=\"M0 15L15 0\" stroke=\"", p.FrameLine, "\" stroke-width=\"0.7\"/>",
            "<path d=\"M7.5 4.5L10.5 7.5L7.5 10.5L4.5 7.5Z\" fill=\"", p.Magenta, "\" stroke=\"none\"/>",
            "</g></svg>");
    }

    private static string Num(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
