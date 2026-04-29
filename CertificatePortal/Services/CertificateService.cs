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
            // Download browser engine ONLY when requested
            await new PuppeteerSharp.BrowserFetcher().DownloadAsync();

            using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { 
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage" } 
            });
            using var page = await browser.NewPageAsync();
            
            await page.SetViewportAsync(new ViewPortOptions { Width = 794, Height = 1123 });

            // In production, we try to use the public host name provided by the request
            string targetUrl = $"https://{host}/Certificate/Preview/{model.Record.StudentID}";
            
            await page.GoToAsync(targetUrl, WaitUntilNavigation.Networkidle0);
            await Task.Delay(1000);

            return await page.PdfDataAsync(new PdfOptions
            {
                Format = PuppeteerSharp.Media.PaperFormat.A4,
                PrintBackground = true,
                MarginOptions = new PuppeteerSharp.Media.MarginOptions { Top = "0", Bottom = "0", Left = "0", Right = "0" }
            });
        }

        private void SetupPage(Body body)
        {
            var sectionProps = new SectionProperties();
            var pageSize = new PageSize() { Width = 11906U, Height = 16838U }; // A4
            var pageMargin = new PageMargin() { Top = 1417, Bottom = 1417, Left = 1417, Right = 1417 }; // 2.5cm
            sectionProps.Append(pageSize, pageMargin);
            body.Append(sectionProps);
        }

        private void AddHeader(MainDocumentPart mainPart, Body body)
        {
            var headerPart = mainPart.AddNewPart<HeaderPart>();
            var header = new Header();

            var table = new Table();
            var tableProps = new TableProperties(
                new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" },
                new TableBorders(
                    new TopBorder { Val = BorderValues.None },
                    new BottomBorder { Val = BorderValues.None },
                    new LeftBorder { Val = BorderValues.None },
                    new RightBorder { Val = BorderValues.None },
                    new InsideHorizontalBorder { Val = BorderValues.None },
                    new InsideVerticalBorder { Val = BorderValues.None }
                )
            );
            table.AppendChild(tableProps);

            var row = new TableRow();

            var leftCell = new TableCell();
            leftCell.AppendChild(new TableCellProperties(
                new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "1000" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            ));

            var logoPath = Path.Combine(_env.WebRootPath, "images", "auca-logo.png");
            if (File.Exists(logoPath))
            {
                var logoBytes = File.ReadAllBytes(logoPath);
                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(logoBytes)) { imagePart.FeedData(stream); }

                long widthEmu = 800000L;
                long heightEmu = 800000L;

                var drawing = new Drawing(
                    new DW.Inline(
                        new DW.Extent() { Cx = widthEmu, Cy = heightEmu },
                        new DW.EffectExtent() { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                        new DW.DocProperties() { Id = 1U, Name = "Logo" },
                        new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }),
                        new A.Graphic(new A.GraphicData(new PIC.Picture(
                            new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "logo.png" }, new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(new A.Blip() { Embed = mainPart.GetIdOfPart(imagePart) }, new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = widthEmu, Cy = heightEmu }),
                                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                    { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });

                leftCell.Append(new Paragraph(new Run(drawing)));
            }
            row.Append(leftCell);

            var rightCell = new TableCell();
            rightCell.AppendChild(new TableCellProperties(
                new TableWidth { Type = TableWidthUnitValues.Pct, Width = "4000" },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            ));

            rightCell.Append(CreateStyledParagraph("Adventist University of Central Africa", JustificationValues.Center, true, 28));
            rightCell.Append(CreateStyledParagraph("P.O. Box 2461 Kigali, Rwanda  |  www.auca.ac.rw  |  info@auca.ac.rw", JustificationValues.Center, false, 18));
            rightCell.Append(CreateStyledParagraph("Directorate for Admissions and Academic Records", JustificationValues.Center, false, 22, italic: true));
            rightCell.Append(CreateStyledParagraph("Mobile Phone: (+250)724 796 996 / 724 474 805/ 788 473 035", JustificationValues.Center, false, 18));
            rightCell.Append(CreateStyledParagraph("Email: registrar@auca.ac.rw  ||  juvenal.nsengiyumva@auca.ac.rw", JustificationValues.Center, false, 18));

            row.Append(rightCell);
            table.Append(row);
            header.Append(table);

            var borderParagraph = new Paragraph(new ParagraphProperties(new ParagraphBorders(new BottomBorder() { Val = BorderValues.Single, Size = 6U, Space = 1U, Color = "000000" })));
            header.Append(borderParagraph);

            headerPart.Header = header;
            var headerId = mainPart.GetIdOfPart(headerPart);
            var sectionProps = body.Elements<SectionProperties>().LastOrDefault() ?? new SectionProperties();
            sectionProps.PrependChild(new HeaderReference() { Type = HeaderFooterValues.Default, Id = headerId });
        }

        private void AddContent(Body body, CertificateViewModel model)
        {
            body.Append(CreateStyledParagraph(model.CityAndDate, JustificationValues.Right, false, 22));
            body.Append(new Paragraph(new Run(new Text(""))));
            var titleP = CreateStyledParagraph("CERTIFICATE OF GOOD STANDING", JustificationValues.Center, true, 28);
            titleP.GetFirstChild<Run>().RunProperties.Append(new Underline() { Val = UnderlineValues.Single });
            body.Append(titleP);
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(CreateStyledParagraph($"I, the undersigned, Eng. Nsengiyumva Juvenal, Director for Admissions and Academic Records of Adventist University of Central Africa, hereby certify that:", JustificationValues.Both, false, 22));
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(CreateStyledParagraph(model.Record.StudentName, JustificationValues.Left, true, 24));
            body.Append(CreateStyledParagraph($"Born on {model.FormattedBirthDate}", JustificationValues.Left, false, 22));
            body.Append(CreateStyledParagraph($"has been a regular student of this University, registered under ID No. {model.Record.StudentID},", JustificationValues.Both, false, 22));
            body.Append(CreateStyledParagraph($"From {model.Record.StudiedFrom} to {model.Record.StudiedTo}.", JustificationValues.Left, false, 22));
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(CreateLabeledParagraph("Year: ", model.Record.Year));
            body.Append(CreateLabeledParagraph("Faculty: ", model.Record.Faculty));
            body.Append(CreateLabeledParagraph("Major: ", model.Record.Major));
            body.Append(CreateLabeledParagraph("Academic year: ", model.Record.AcademicYear));
            body.Append(CreateLabeledParagraph("Validity: ", model.Record.AcademicYear));
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(CreateStyledParagraph("This certificate is issued for any legal or administrative purpose it may serve", JustificationValues.Left, false, 22, italic: true));
        }

        private void AddFooter(MainDocumentPart mainPart, Body body, CertificateViewModel model)
        {
            var table = new Table();
            var tableProps = new TableProperties(new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" }, new TableBorders(new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None }, new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));
            table.AppendChild(tableProps);
            var row = new TableRow();

            var leftCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            var sigPath = Path.Combine(_env.WebRootPath, "images", "signature.png");
            if (File.Exists(sigPath))
            {
                var sigBytes = File.ReadAllBytes(sigPath);
                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(sigBytes)) { imagePart.FeedData(stream); }
                var sigDrawing = new Drawing(GetImageElement(mainPart.GetIdOfPart(imagePart), 1143000L, 476250L));
                leftCell.Append(new Paragraph(new Run(sigDrawing)));
            }
            leftCell.Append(CreateStyledParagraph("Eng. Nsengiyumva Juvenal", JustificationValues.Left, true, 22));
            leftCell.Append(CreateStyledParagraph("Director for Admissions and Academic Records", JustificationValues.Left, false, 22));
            leftCell.Append(CreateStyledParagraph("Adventist University of Central Africa", JustificationValues.Left, false, 22));
            row.Append(leftCell);

            var rightCell = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Pct, Width = "2500" }));
            var qrUrl = $"https://auca.ac.rw/verify/{model.Record.StudentID}";
            var qrBytes = GenerateQrCode(qrUrl);
            var qrImagePart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(qrBytes)) { qrImagePart.FeedData(ms); }
            var qrDrawing = new Drawing(GetImageElement(mainPart.GetIdOfPart(qrImagePart), 750000L, 750000L));
            rightCell.Append(new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }), new Run(qrDrawing)));
            rightCell.Append(CreateStyledParagraph("Scan to verify validity", JustificationValues.Right, false, 18));
            row.Append(rightCell);

            table.Append(row);
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(new Paragraph(new Run(new Text(""))));
            body.Append(table);
        }

        private byte[] GenerateQrCode(string url)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(5);
        }

        private Paragraph CreateStyledParagraph(string text, JustificationValues justify, bool bold, int fontSize, bool italic = false)
        {
            var run = new Run();
            var rp = new RunProperties();
            rp.Append(new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" });
            rp.Append(new FontSize() { Val = fontSize.ToString() });
            rp.Append(new Color() { Val = "000000" });
            if (bold) rp.Append(new Bold());
            if (italic) rp.Append(new Italic());
            run.Append(rp);
            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            var para = new Paragraph(new ParagraphProperties(new Justification() { Val = justify }, new SpacingBetweenLines() { After = "0" }), run);
            return para;
        }

        private Paragraph CreateLabeledParagraph(string label, string value)
        {
            var rLabel = new Run(new RunProperties(new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" }, new FontSize() { Val = "22" }, new Color() { Val = "000000" }, new Bold()), new Text(label) { Space = SpaceProcessingModeValues.Preserve });
            var rValue = new Run(new RunProperties(new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman" }, new FontSize() { Val = "22" }, new Color() { Val = "000000" }), new Text(value));
            return new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }, new SpacingBetweenLines() { After = "0" }), rLabel, rValue);
        }

        private Drawing GetImageElement(string relationshipId, long widthEmus, long heightEmus)
        {
            return new Drawing(new DW.Inline(new DW.Extent() { Cx = widthEmus, Cy = heightEmus }, new DW.EffectExtent() { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 }, new DW.DocProperties() { Id = 1U, Name = "Picture" }, new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }), new A.Graphic(new A.GraphicData(new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "image.png" }, new PIC.NonVisualPictureDrawingProperties()), new PIC.BlipFill(new A.Blip() { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())), new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = widthEmus, Cy = heightEmus }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        }
    }
}
