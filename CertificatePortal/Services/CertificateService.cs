using System;
using System.IO;
using System.Threading.Tasks;
using CertificatePortal.Models;
using CertificatePortal.Models.ViewModels;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QRCoder;
using PuppeteerSharp;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace CertificatePortal.Services
{
    public class CertificateService : ICertificateService
    {
        private readonly AppSettings _settings;
        private readonly IWebHostEnvironment _env;

        public CertificateService(IOptions<AppSettings> settings, IWebHostEnvironment env)
        {
            _settings = settings.Value;
            _env = env;
        }

        public async Task<byte[]> GenerateDocxAsync(CertificateViewModel model, string host)
        {
            using (var mem = new MemoryStream())
            {
                using (var wordDocument = WordprocessingDocument.Create(mem, WordprocessingDocumentType.Document))
                {
                    var mainPart = wordDocument.AddMainDocumentPart();
                    
                    var styleDefinitionsPart = mainPart.AddNewPart<StyleDefinitionsPart>();
                    var styles = new Styles();
                    var docDefaults = new DocDefaults(
                        new RunPropertiesDefault(
                            new RunPropertiesBaseStyle(
                                new RunFonts() 
                                { 
                                    Ascii = "Times New Roman", 
                                    HighAnsi = "Times New Roman",
                                    ComplexScript = "Times New Roman"
                                },
                                new FontSize() { Val = "22" },
                                new FontSizeComplexScript() { Val = "22" }
                            )
                        )
                    );
                    styles.Append(docDefaults);
                    styleDefinitionsPart.Styles = styles;

                    mainPart.Document = new Document();
                    var body = new Body();
                    mainPart.Document.Append(body);
                    SetupPage(body);
                    AddHeader(mainPart, body);
                    AddContent(body, model);
                    AddFooter(mainPart, body, model);
                }
                return mem.ToArray();
            }
        }

        public async Task<byte[]> GeneratePdfAsync(CertificateViewModel model, string host)
        {
            var executablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH") ?? "";

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { 
                    "--no-sandbox", 
                    "--disable-setuid-sandbox", 
                    "--disable-dev-shm-usage",
                    "--disable-gpu",
                    "--no-zygote",
                    "--single-process"
                }
            };

            if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                launchOptions.ExecutablePath = executablePath;
            else
                await new BrowserFetcher().DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            using var page = await browser.NewPageAsync();
            
            // Set timeout to 0 (disabled) to prevent the error you saw
            page.DefaultNavigationTimeout = 0;
            
            await page.SetViewportAsync(new ViewPortOptions { Width = 794, Height = 1123 });

            string html = GenerateHtmlContent(model);
            
            // Wait ONLY for the content to be loaded, not for external network resources
            await page.SetContentAsync(html, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.Load } });

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PuppeteerSharp.Media.PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new PuppeteerSharp.Media.MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" }
            });
        }

        private string GenerateHtmlContent(CertificateViewModel model)
        {
            // Prepare Logo Base64
            string logoBase64 = "";
            var logoPath = Path.Combine(_env.WebRootPath, "images", "auca-logo.png");
            if (File.Exists(logoPath)) logoBase64 = Convert.ToBase64String(File.ReadAllBytes(logoPath));

            // Prepare Signature Base64
            string sigBase64 = "";
            var sigPath = Path.Combine(_env.WebRootPath, "images", "signature.png");
            if (File.Exists(sigPath)) sigBase64 = Convert.ToBase64String(File.ReadAllBytes(sigPath));

            // Prepare QR Code Base64
            var qrUrl = $"https://auca.ac.rw/verify/{model.Record.StudentID}";
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(qrUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            var qrBase64 = Convert.ToBase64String(qrCode.GetGraphic(5));

            return $@"
            <html>
            <head>
                <style>
                    body {{ font-family: 'Times New Roman', serif; padding: 0; margin: 0; background: white; color: black; }}
                    .cert-card {{ width: 210mm; height: 297mm; padding: 2.5cm; box-sizing: border-box; position: relative; }}
                    .header {{ display: flex; align-items: center; border-bottom: 2px solid black; padding-bottom: 10px; margin-bottom: 20px; }}
                    .logo-img {{ width: 80px; height: 80px; margin-right: 20px; }}
                    .header-text {{ flex: 1; text-align: center; }}
                    .header-text h2 {{ margin: 0; font-size: 1.4rem; font-weight: bold; }}
                    .header-text p {{ margin: 2px 0; font-size: 0.85rem; }}
                    .date {{ text-align: right; margin-bottom: 30px; font-size: 1.1rem; }}
                    .title {{ text-align: center; margin: 40px 0; font-size: 1.6rem; text-decoration: underline; font-weight: bold; }}
                    .body-p {{ text-align: justify; font-size: 1.15rem; line-height: 1.6; margin: 15px 0; }}
                    .student-name {{ font-size: 1.3rem; font-weight: bold; margin: 20px 0 10px 0; }}
                    .field {{ margin: 5px 0; font-size: 1.1rem; }}
                    .footer {{ position: absolute; bottom: 2.5cm; left: 2.5cm; right: 2.5cm; display: flex; justify-content: space-between; align-items: flex-end; }}
                    .sig-block {{ text-align: left; }}
                    .qr-block {{ text-align: right; font-size: 0.8rem; }}
                    .qr-img {{ width: 85px; height: 85px; }}
                    .sig-img {{ width: 140px; height: auto; margin-bottom: -15px; }}
                </style>
            </head>
            <body>
                <div class='cert-card'>
                    <div class='header'>
                        <img src='data:image/png;base64,{logoBase64}' class='logo-img'>
                        <div class='header-text'>
                            <h2>Adventist University of Central Africa</h2>
                            <p>P.O. Box 2461 Kigali, Rwanda | www.auca.ac.rw | info@auca.ac.rw</p>
                            <p style='font-style:italic; font-size:1.1rem;'>Directorate for Admissions and Academic Records</p>
                            <p>Mobile: (+250)724 796 996 / 724 474 805 / 788 473 035</p>
                            <p>Email: registrar@auca.ac.rw</p>
                        </div>
                    </div>
                    <div class='date'>{model.CityAndDate}</div>
                    <div class='title'>CERTIFICATE OF GOOD STANDING</div>
                    <div class='body-p'>
                        I, the undersigned, Eng. Nsengiyumva Juvenal, Director for Admissions and Academic Records of Adventist University of Central Africa, hereby certify that:
                    </div>
                    <div class='student-name'>{model.Record.StudentName}</div>
                    <div class='body-p'>
                        Born on {model.FormattedBirthDate}<br>
                        has been a regular student of this University, registered under ID No. {model.Record.StudentID},<br>
                        From {model.Record.StudiedFrom} to {model.Record.StudiedTo}.
                    </div>
                    <div class='field'><b>Year:</b> {model.Record.Year}</div>
                    <div class='field'><b>Faculty:</b> {model.Record.Faculty}</div>
                    <div class='field'><b>Major:</b> {model.Record.Major}</div>
                    <div class='field'><b>Academic year:</b> {model.Record.AcademicYear}</div>
                    <div class='field'><b>Validity:</b> {model.Record.AcademicYear}</div>
                    <div class='body-p' style='margin-top: 30px; font-style: italic;'>
                        This certificate is issued for any legal or administrative purpose it may serve
                    </div>
                    <div class='footer'>
                        <div class='sig-block'>
                            <img src='data:image/png;base64,{sigBase64}' class='sig-img'><br>
                            <p style='margin: 0;'>______________________________</p>
                            <b>Eng. Nsengiyumva Juvenal</b><br>
                            Director for Admissions and Academic Records<br>
                            Adventist University of Central Africa
                        </div>
                        <div class='qr-block'>
                            <img src='data:image/png;base64,{qrBase64}' class='qr-img'><br>
                            Scan to verify validity
                        </div>
                    </div>
                </div>
            </body>
            </html>";
        }

        private void SetupPage(Body body)
        {
            var sectionProps = new SectionProperties();
            var pageSize = new PageSize() { Width = 11906U, Height = 16838U };
            var pageMargin = new PageMargin() { Top = 1417, Bottom = 1417, Left = 1417, Right = 1417 };
            sectionProps.Append(pageSize, pageMargin);
            body.Append(sectionProps);
        }

        private void AddHeader(MainDocumentPart mainPart, Body body)
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();
            var table = new Table();
            var tableProps = new TableProperties(new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" }, new TableBorders(new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None }, new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));
            table.AppendChild(tableProps);
            var row = new TableRow();
            var leftCell = new TableCell();
            leftCell.AppendChild(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "1000" }, new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            var logoPath = Path.Combine(_env.WebRootPath, "images", "auca-logo.png");
            if (File.Exists(logoPath))
            {
                var logoBytes = File.ReadAllBytes(logoPath);
                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(logoBytes)) { imagePart.FeedData(stream); }
                var drawing = new Drawing(new DW.Inline(new DW.Extent() { Cx = 800000L, Cy = 800000L }, new DW.EffectExtent() { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 }, new DW.DocProperties() { Id = 1U, Name = "Logo" }, new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }), new A.Graphic(new A.GraphicData(new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "auca-logo.png" }, new PIC.NonVisualPictureDrawingProperties()), new PIC.BlipFill(new A.Blip() { Embed = mainPart.GetIdOfPart(imagePart) }, new A.Stretch(new A.FillRectangle())), new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = 800000L, Cy = 800000L }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
                leftCell.Append(new Paragraph(new Run(drawing)));
            }
            row.Append(leftCell);
            var rightCell = new TableCell();
            rightCell.AppendChild(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "4000" }, new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            rightCell.Append(CreateStyledParagraph("Adventist University of Central Africa", JustificationValues.Center, true, 28, "Times New Roman"));
            rightCell.Append(CreateStyledParagraph("P.O. Box 2461 Kigali, Rwanda  |  www.auca.ac.rw  |  info@auca.ac.rw", JustificationValues.Center, false, 18, "Times New Roman"));
            rightCell.Append(CreateStyledParagraph("Directorate for Admissions and Academic Records", JustificationValues.Center, false, 22, "Times New Roman", italic: true));
            rightCell.Append(CreateStyledParagraph("Mobile Phone: (+250)724 796 996 / 724 474 805/ 788 473 035", JustificationValues.Center, false, 18, "Times New Roman"));
            rightCell.Append(CreateStyledParagraph("Email: registrar@auca.ac.rw  ||  juvenal.nsengiyumva@auca.ac.rw", JustificationValues.Center, false, 18, "Times New Roman"));
            row.Append(rightCell);
            table.Append(row);
            header.Append(table);
            header.Append(new Paragraph(new ParagraphProperties(new ParagraphBorders(new BottomBorder() { Val = BorderValues.Single, Size = 6U, Space = 1U, Color = "000000" }))));
            headerPart.Header = header;
            var sectionProps = body.Elements<SectionProperties>().LastOrDefault() ?? new SectionProperties();
            sectionProps.PrependChild(new HeaderReference() { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(headerPart) });
        }

        private void AddContent(Body body, CertificateViewModel model)
        {
            // After header table
            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines() { After = "200" })));

            body.Append(CreateStyledParagraph(model.CityAndDate, JustificationValues.Right, false, 22));
            
            var titleP = CreateStyledParagraph("CERTIFICATE OF GOOD STANDING", JustificationValues.Center, true, 28);
            titleP.GetFirstChild<Run>().RunProperties.Append(new Underline() { Val = UnderlineValues.Single });
            // Title spacing
            titleP.GetFirstChild<ParagraphProperties>().Append(new SpacingBetweenLines() { Before = "400", After = "400" });
            body.Append(titleP);

            body.Append(CreateStyledParagraph($"I, the undersigned, Eng. Nsengiyumva Juvenal, Director for Admissions and Academic Records of Adventist University of Central Africa, hereby certify that:", JustificationValues.Both, false, 22));
            
            // One empty paragraph after intro
            body.Append(new Paragraph(new Run(new Text(""))));

            body.Append(CreateStyledParagraph(model.Record.StudentName, JustificationValues.Left, true, 24));
            body.Append(CreateStyledParagraph($"Born on {model.FormattedBirthDate}", JustificationValues.Left, false, 22));
            body.Append(CreateStyledParagraph($"has been a regular student of this University, registered under ID No. {model.Record.StudentID},", JustificationValues.Both, false, 22));
            body.Append(CreateStyledParagraph($"From {model.Record.StudiedFrom} to {model.Record.StudiedTo}.", JustificationValues.Left, false, 22));
            
            // One empty paragraph before fields
            body.Append(new Paragraph(new Run(new Text(""))));

            body.Append(CreateMixedParagraph("Year: ", model.Record.Year));
            body.Append(CreateMixedParagraph("Faculty: ", model.Record.Faculty));
            body.Append(CreateMixedParagraph("Major: ", model.Record.Major));
            body.Append(CreateMixedParagraph("Academic year: ", model.Record.AcademicYear));
            body.Append(CreateMixedParagraph("Validity: ", model.Record.AcademicYear));
            
            // One empty paragraph after fields with After="200"
            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines() { After = "200" })));

            body.Append(CreateStyledParagraph("This certificate is issued for any legal or administrative purpose it may serve", JustificationValues.Left, false, 22, italic: true));
        }

        private void AddFooter(MainDocumentPart mainPart, Body body, CertificateViewModel model)
        {
            // 4 empty paragraphs before the signature table
            for (int i = 0; i < 4; i++) body.Append(new Paragraph(new Run(new Text(""))));

            var table = new Table();
            var tableProps = new TableProperties(new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" }, new TableBorders(new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None }, new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));
            table.AppendChild(tableProps);
            var row = new TableRow();
            var leftCell = new TableCell(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            var sigPath = Path.Combine(_env.WebRootPath, "images", "signature.png");
            if (File.Exists(sigPath))
            {
                var sigBytes = File.ReadAllBytes(sigPath);
                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(sigBytes)) { imagePart.FeedData(stream); }
                leftCell.Append(new Paragraph(new Run(new Drawing(GetImageElement(mainPart.GetIdOfPart(imagePart), 1143000L, 476250L)))));
            }
            leftCell.Append(CreateStyledParagraph("Eng. Nsengiyumva Juvenal", JustificationValues.Left, true, 22));
            leftCell.Append(CreateStyledParagraph("Director for Admissions and Academic Records", JustificationValues.Left, false, 22));
            leftCell.Append(CreateStyledParagraph("Adventist University of Central Africa", JustificationValues.Left, false, 22));
            row.Append(leftCell);
            var rightCell = new TableCell(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            var qrBytes = GenerateQrCode($"https://auca.ac.rw/verify/{model.Record.StudentID}");
            var qrPart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(qrBytes)) { qrPart.FeedData(ms); }
            rightCell.Append(new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }), new Run(new Drawing(GetImageElement(mainPart.GetIdOfPart(qrPart), 750000L, 750000L)))));
            rightCell.Append(CreateStyledParagraph("Scan to verify validity", JustificationValues.Right, false, 18));
            row.Append(rightCell);
            table.Append(row);
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(table);
        }

        private byte[] GenerateQrCode(string url)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(5);
        }

        private Paragraph CreateStyledParagraph(string text, JustificationValues justify, bool bold, int fontSize, string fontName = "Times New Roman", bool italic = false)
        {
            var run = new Run();
            var rp = new RunProperties();
            rp.Append(new RunFonts() 
            { 
                Ascii = fontName, 
                HighAnsi = fontName,
                ComplexScript = fontName
            });
            rp.Append(new FontSize() { Val = fontSize.ToString() });
            rp.Append(new FontSizeComplexScript() { Val = fontSize.ToString() });
            rp.Append(new Color() { Val = "000000" });
            if (bold) rp.Append(new Bold());
            if (italic) rp.Append(new Italic());
            run.Append(rp);
            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            var para = new Paragraph();
            para.Append(new ParagraphProperties(
                new Justification() { Val = justify },
                new SpacingBetweenLines() { After = "0" }));
            para.Append(run);
            return para;
        }

        private Paragraph CreateMixedParagraph(string boldLabel, string normalValue, int fontSize = 22)
        {
            var para = new Paragraph();
            para.Append(new ParagraphProperties(
                new Justification() { Val = JustificationValues.Left },
                new SpacingBetweenLines() { After = "0" }));

            var boldRun = new Run();
            var boldRp = new RunProperties();
            boldRp.Append(new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
            boldRp.Append(new FontSize() { Val = fontSize.ToString() });
            boldRp.Append(new FontSizeComplexScript() { Val = fontSize.ToString() });
            boldRp.Append(new Bold());
            boldRp.Append(new Color() { Val = "000000" });
            boldRun.Append(boldRp);
            boldRun.Append(new Text(boldLabel) { Space = SpaceProcessingModeValues.Preserve });

            var normalRun = new Run();
            var normalRp = new RunProperties();
            normalRp.Append(new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman", ComplexScript = "Times New Roman" });
            normalRp.Append(new FontSize() { Val = fontSize.ToString() });
            normalRp.Append(new FontSizeComplexScript() { Val = fontSize.ToString() });
            normalRp.Append(new Color() { Val = "000000" });
            normalRun.Append(normalRp);
            normalRun.Append(new Text(normalValue) { Space = SpaceProcessingModeValues.Preserve });

            para.Append(boldRun);
            para.Append(normalRun);
            return para;
        }

        private Drawing GetImageElement(string relationshipId, long w, long h)
        {
            return new Drawing(new DW.Inline(new DW.Extent() { Cx = w, Cy = h }, new DW.EffectExtent() { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 }, new DW.DocProperties() { Id = 1U, Name = "Picture" }, new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }), new A.Graphic(new A.GraphicData(new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "img.png" }, new PIC.NonVisualPictureDrawingProperties()), new PIC.BlipFill(new A.Blip() { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())), new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = w, Cy = h }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        }
    }
}
