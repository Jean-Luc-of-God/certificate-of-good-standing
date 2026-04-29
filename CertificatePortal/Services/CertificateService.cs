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
                    .header-text h2 {{ margin: 0; font-size: 1.4rem; font-weight: bold; color: #003399; }}
                    .header-text p {{ margin: 2px 0; font-size: 0.85rem; }}
                    .cursive-line {{ font-family: 'Edwardian Script ITC', cursive; font-size: 2.5rem !important; margin: 5px 0 !important; font-weight: normal; }}
                    .email-link {{ color: blue; text-decoration: underline; }}
                    .date {{ text-align: left; margin-bottom: 30px; font-size: 1.1rem; }}
                    .title {{ text-align: center; margin: 40px 0; font-size: 1.6rem; font-weight: bold; }}
                    .body-p {{ text-align: justify; font-size: 1.15rem; line-height: 1.6; margin: 15px 0; }}
                    .student-name {{ font-size: 1.3rem; font-weight: bold; margin: 20px 0 10px 0; }}
                    .field {{ margin: 2px 0; font-size: 1.1rem; }}
                    .field-label {{ font-weight: normal; }}
                    .field-value {{ font-weight: bold; }}
                    .footer-hr {{ border: none; border-top: 2px solid black; margin-bottom: 10px; }}
                    .footer {{ position: absolute; bottom: 2.5cm; left: 2.5cm; right: 2.5cm; display: flex; justify-content: space-between; align-items: flex-end; }}
                    .sig-block {{ text-align: left; flex: 1; }}
                    .qr-block {{ text-align: left; width: 150px; }}
                    .qr-img {{ width: 85px; height: 85px; }}
                    .sig-img {{ width: 140px; height: auto; margin-bottom: -15px; float: right; }}
                    .no-wrap {{ white-space: nowrap; }}
                </style>
            </head>
            <body>
                <div class='cert-card'>
                    <div class='header'>
                        <img src='data:image/png;base64,{logoBase64}' class='logo-img'>
                        <div class='header-text'>
                            <h2>Adventist University of Central Africa</h2>
                            <p>P.O. Box 2461 Kigali, Rwanda | www.auca.ac.rw | info@auca.ac.rw</p>
                            <p class='cursive-line'>Directorate for Admissions and Academic Records</p>
                            <p>{model.MobilePhoneLabel}(+250)724 796 996 / 724 474 805 / 788 473 035</p>
                            <p><a class='email-link'>registrar@auca.ac.rw</a> || <a class='email-link'>juvenal.nsengiyumva@auca.ac.rw</a></p>
                        </div>
                    </div>
                    <div class='date'>{model.CityAndDate}</div>
                    <div class='title'>CERTIFICATE OF GOOD STANDING</div>
                    <div class='body-p'>
                        I, the undersigned, <b>Eng. Nsengiyumva Juvenal</b>, <b>Director for Admissions and Academic Records</b> of the Adventist University of Central Africa, hereby certify that:
                    </div>
                    <div class='student-name'>{model.FormattedStudentName}</div>
                    <div class='body-p'>
                        Born on <b>{model.FormattedBirthDate}</b>,<br>
                        <span class='no-wrap'>has been a regular student of this University, registered under <b>ID No. {model.Record.StudentID}</b>,</span><br>
                        From <b>{model.Record.StudiedFrom}</b> to <b>{model.Record.StudiedTo}</b>.
                    </div>
                    <div class='field'><span class='field-label'>Year:</span> <span class='field-value'>{model.Record.Year}</span></div>
                    <div class='field'><span class='field-label'>Faculty:</span> <span class='field-value'>{model.Record.Faculty}</span></div>
                    <div class='field'><span class='field-label'>Major:</span> <span class='field-value'>{model.Record.Major}</span></div>
                    <div class='field'><span class='field-label'>Academic year:</span> <span class='field-value'>{model.Record.AcademicYear}</span></div>
                    <div class='field'><span class='field-label'>Validity:</span> <span class='field-value'>{model.Record.AcademicYear}</span></div>
                    <div class='body-p' style='margin-top: 30px; font-style: italic;'>
                        This certificate is issued for any legal or administrative purpose it may serve
                    </div>
                    
                    <div class='footer'>
                        <div class='qr-block'>
                            <img src='data:image/png;base64,{qrBase64}' class='qr-img'><br>
                            Scan to verify my Validity
                        </div>
                        <div class='sig-block'>
                            <img src='data:image/png;base64,{sigBase64}' class='sig-img'>
                            <div style='clear:both;'></div>
                            <hr class='footer-hr'>
                            <b>Eng. Nsengiyumva Juvenal</b><br>
                            Director for Admissions and Academic Records<br>
                            Adventist University of Central Africa
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
            var pageMargin = new PageMargin() { Top = 450, Bottom = 1440, Left = 1440, Right = 1440 };
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
            
            // University Name: Blue #003399
            var uniNameRun = CreateRun("Adventist University of Central Africa", true, "Times New Roman", 28, "003399");
            rightCell.Append(new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }), uniNameRun));

            rightCell.Append(CreateStyledParagraph("P.O. Box 2461 Kigali, Rwanda  |  www.auca.ac.rw  |  info@auca.ac.rw", JustificationValues.Center, false, 18, "Times New Roman"));
            
            // Directorate: Edwardian Script ITC, 44pt
            rightCell.Append(CreateStyledParagraph("Directorate for Admissions and Academic Records", JustificationValues.Center, false, 44, "Edwardian Script ITC", italic: false));
            
            // Mobile: Space before colon
            rightCell.Append(CreateStyledParagraph("Mobile Phone : (+250)724 796 996 / 724 474 805/ 788 473 035", JustificationValues.Center, false, 18, "Times New Roman"));
            
            // Email: Blue and Underlined, || separator
            var emailPara = new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Center }));
            var email1 = CreateRun("registrar@auca.ac.rw", false, "Times New Roman", 18, "0000FF");
            email1.RunProperties.Append(new Underline() { Val = UnderlineValues.Single });
            var separator = CreateRun("  ||  ", false, "Times New Roman", 18, "000000");
            var email2 = CreateRun("juvenal.nsengiyumva@auca.ac.rw", false, "Times New Roman", 18, "0000FF");
            email2.RunProperties.Append(new Underline() { Val = UnderlineValues.Single });
            emailPara.Append(email1, separator, email2);
            rightCell.Append(emailPara);

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
            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines() { After = "200" })));

            // Date: Left-Aligned
            body.Append(CreateStyledParagraph(model.CityAndDate, JustificationValues.Left, true, 24, lineSpacing: "360"));
            
            // Title: Bold, Center, NO Underline
            var titleP = CreateStyledParagraph("CERTIFICATE OF GOOD STANDING", JustificationValues.Center, true, 27);
            titleP.GetFirstChild<ParagraphProperties>().Append(new SpacingBetweenLines() { Before = "400", After = "400" });
            body.Append(titleP);

            // Intro: Helvetica 21pt, Color #443742. Added "the"
            body.Append(CreateComplexParagraph(JustificationValues.Both, "360",
                CreateRun("I, the undersigned, ", false, "Helvetica", 21, "443742"),
                CreateRun("Eng. Nsengiyumva Juvenal", true, "Helvetica", 21, "443742"),
                CreateRun(", ", false, "Helvetica", 21, "443742"),
                CreateRun("Director for Admissions and Academic Records", true, "Helvetica", 21, "443742"),
                CreateRun(" of the Adventist University of Central Africa, hereby certify that:", false, "Helvetica", 21, "443742")
            ));

            // Student Name: Formatted (Title Case + Comma)
            body.Append(CreateComplexParagraph(JustificationValues.Left, "360",
                CreateRun(model.FormattedStudentName, true, "Helvetica", 21, "443742")
            ));

            // Born on: Normal, Date is Bold
            body.Append(CreateComplexParagraph(JustificationValues.Left, "360",
                CreateRun("Born on ", false, "Times New Roman", 24, "000000"),
                CreateRun(model.FormattedBirthDate + ",", true, "Times New Roman", 24, "000000")
            ));

            // Registered under: ID is Bold. Single line enforced.
            body.Append(CreateComplexParagraph(JustificationValues.Left, "360",
                CreateRun("has been a regular student of this University, registered under ", false, "Times New Roman", 24, "000000"),
                CreateRun("ID No. ", true, "Times New Roman", 24, "000000"),
                CreateRun(model.Record.StudentID + ",", true, "Times New Roman", 24, "000000")
            ));

            // From: Dates are Bold
            body.Append(CreateComplexParagraph(JustificationValues.Left, "360",
                CreateRun("From ", false, "Times New Roman", 24, "000000"),
                CreateRun(model.Record.StudiedFrom, true, "Times New Roman", 24, "000000"),
                CreateRun(" to ", true, "Times New Roman", 24, "000000"),
                CreateRun(model.Record.StudiedTo + ".", true, "Times New Roman", 24, "000000")
            ));

            // Fields: Label Normal, Value Bold
            body.Append(CreateComplexParagraph(JustificationValues.Left, "240", CreateRun("Year: ", false, "Times New Roman", 24, "000000"), CreateRun(model.Record.Year, true, "Times New Roman", 24, "000000")));
            body.Append(CreateComplexParagraph(JustificationValues.Left, "240", CreateRun("Faculty: ", false, "Times New Roman", 24, "000000"), CreateRun(model.Record.Faculty, true, "Times New Roman", 24, "000000")));
            body.Append(CreateComplexParagraph(JustificationValues.Left, "240", CreateRun("Major: ", false, "Times New Roman", 24, "000000"), CreateRun(model.Record.Major, true, "Times New Roman", 24, "000000")));
            body.Append(CreateComplexParagraph(JustificationValues.Left, "240", CreateRun("Academic year: ", false, "Times New Roman", 24, "000000"), CreateRun(model.Record.AcademicYear, true, "Times New Roman", 24, "000000")));
            body.Append(CreateComplexParagraph(JustificationValues.Left, "240", CreateRun("Validity: ", false, "Times New Roman", 24, "000000"), CreateRun(model.Record.AcademicYear, true, "Times New Roman", 24, "000000")));

            body.Append(new Paragraph(new ParagraphProperties(new SpacingBetweenLines() { After = "200" })));

            var italicRun = CreateRun("This certificate is issued for any legal or administrative purpose it may serve", false, "Times New Roman", 24, "000000");
            italicRun.RunProperties.Append(new Italic());
            body.Append(CreateComplexParagraph(JustificationValues.Left, "360", italicRun));
        }

        private void AddFooter(MainDocumentPart mainPart, Body body, CertificateViewModel model)
        {
            for (int i = 0; i < 4; i++) body.Append(new Paragraph(new Run(new Text(""))));

            var table = new Table();
            var tableProps = new TableProperties(new TableWidth() { Type = TableWidthUnitValues.Pct, Width = "5000" }, new TableBorders(new TopBorder { Val = BorderValues.None }, new BottomBorder { Val = BorderValues.None }, new LeftBorder { Val = BorderValues.None }, new RightBorder { Val = BorderValues.None }, new InsideHorizontalBorder { Val = BorderValues.None }, new InsideVerticalBorder { Val = BorderValues.None }));
            table.AppendChild(tableProps);
            var row = new TableRow();
            
            // Left Cell: QR Code
            var leftCell = new TableCell(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "2000" }));
            var qrBytes = GenerateQrCode($"https://auca.ac.rw/verify/{model.Record.StudentID}");
            var qrPart = mainPart.AddImagePart(ImagePartType.Png);
            using (var ms = new MemoryStream(qrBytes)) { qrPart.FeedData(ms); }
            leftCell.Append(new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Left }), new Run(new Drawing(GetImageElement(mainPart.GetIdOfPart(qrPart), 750000L, 750000L)))));
            leftCell.Append(CreateStyledParagraph("Scan to verify my Validity", JustificationValues.Left, false, 18));
            row.Append(leftCell);

            // Right Cell: Signature + Info
            var rightCell = new TableCell(new TableCellProperties(new TableWidth { Type = TableWidthUnitValues.Pct, Width = "3000" }));
            var sigPath = Path.Combine(_env.WebRootPath, "images", "signature.png");
            if (File.Exists(sigPath))
            {
                var sigBytes = File.ReadAllBytes(sigPath);
                var imagePart = mainPart.AddImagePart(ImagePartType.Png);
                using (var stream = new MemoryStream(sigBytes)) { imagePart.FeedData(stream); }
                // Signature aligned right
                rightCell.Append(new Paragraph(new ParagraphProperties(new Justification() { Val = JustificationValues.Right }), new Run(new Drawing(GetImageElement(mainPart.GetIdOfPart(imagePart), 1143000L, 476250L)))));
            }
            
            // Horizontal rule: Solid black
            var hrPara = new Paragraph(new ParagraphProperties(new ParagraphBorders(new TopBorder() { Val = BorderValues.Single, Size = 12U, Color = "000000" })));
            rightCell.Append(hrPara);

            rightCell.Append(CreateStyledParagraph("Eng. Nsengiyumva Juvenal", JustificationValues.Left, true, 22));
            rightCell.Append(CreateStyledParagraph("Director for Admissions and Academic Records", JustificationValues.Left, false, 22));
            rightCell.Append(CreateStyledParagraph("Adventist University of Central Africa", JustificationValues.Left, false, 22));
            row.Append(rightCell);
            
            table.Append(row);
            body.Append(table);
        }

        private byte[] GenerateQrCode(string url)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrData);
            return qrCode.GetGraphic(5);
        }

        private Run CreateRun(string text, bool bold = false, string font = "Times New Roman", int size = 24, string color = "000000")
        {
            var run = new Run();
            var rp = new RunProperties();
            rp.Append(new RunFonts() { Ascii = font, HighAnsi = font, ComplexScript = font });
            rp.Append(new FontSize() { Val = size.ToString() });
            rp.Append(new FontSizeComplexScript() { Val = size.ToString() });
            rp.Append(new Color() { Val = color });
            if (bold) rp.Append(new Bold());
            run.Append(rp);
            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            return run;
        }

        private Paragraph CreateComplexParagraph(JustificationValues justify, string lineSpacing, params Run[] runs)
        {
            var para = new Paragraph();
            var pp = new ParagraphProperties(new Justification() { Val = justify });
            if (!string.IsNullOrEmpty(lineSpacing))
            {
                pp.Append(new SpacingBetweenLines() { Line = lineSpacing, LineRule = LineSpacingRuleValues.Auto });
            }
            para.Append(pp);
            foreach (var run in runs) para.Append(run);
            return para;
        }

        private Paragraph CreateStyledParagraph(string text, JustificationValues justify, bool bold, int fontSize, string fontName = "Times New Roman", bool italic = false, string lineSpacing = null)
        {
            var run = CreateRun(text, bold, fontName, fontSize, "000000");
            if (italic) run.RunProperties.Append(new Italic());
            return CreateComplexParagraph(justify, lineSpacing, run);
        }

        private Drawing GetImageElement(string relationshipId, long w, long h)
        {
            return new Drawing(new DW.Inline(new DW.Extent() { Cx = w, Cy = h }, new DW.EffectExtent() { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 }, new DW.DocProperties() { Id = 1U, Name = "Picture" }, new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks() { NoChangeAspect = true }), new A.Graphic(new A.GraphicData(new PIC.Picture(new PIC.NonVisualPictureProperties(new PIC.NonVisualDrawingProperties() { Id = 0U, Name = "img.png" }, new PIC.NonVisualPictureDrawingProperties()), new PIC.BlipFill(new A.Blip() { Embed = relationshipId }, new A.Stretch(new A.FillRectangle())), new PIC.ShapeProperties(new A.Transform2D(new A.Offset() { X = 0, Y = 0 }, new A.Extents() { Cx = w, Cy = h }), new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }))) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })) { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U });
        }
    }
}
