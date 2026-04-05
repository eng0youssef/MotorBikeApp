using System;
using System.Collections.Generic;
using System.Linq;
using MotorBike.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorBike.Services;

public class BuyInvoiceItemModel
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Discount { get; set; }
    public double Total { get; set; }
}

public class BuyInvoiceModel
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsCash { get; set; }
    public List<BuyInvoiceItemModel> Items { get; set; } = new();

    public double Total { get; set; }
    public double Discount { get; set; }
    public double AddMoney { get; set; }
    public bool IsTax { get; set; }
    public double VatTax { get; set; }
    public double WhtTax { get; set; }
    public double NetAmount { get; set; }
    public double PreviousBalance { get; set; }
    public double PaidAmount { get; set; }
    public double RemainingAmount { get; set; }
}

public class BuyInvoiceDocument : IDocument
{
    private readonly BuyInvoiceModel _model;
    private readonly Company _company;

    public BuyInvoiceDocument(BuyInvoiceModel model, Company company)
    {
        _model = model;
        _company = company;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    // ─────────────────────────────────────────────────────────────────────────
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x =>
                x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ── HEADER ────────────────────────────────────────────────────────────────
    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // ── Company row: English LEFT | Logo CENTER | Arabic RIGHT ────────
            column.Item().Row(row =>
            {
                // LEFT: English
                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().Text(_company?.NameEn ?? "Company Name")
                        .FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2)
                        .DirectionFromLeftToRight();

                    if (!string.IsNullOrEmpty(_company?.AdressEn))
                        col.Item().Text(_company.AdressEn)
                            .FontSize(10).FontColor(Colors.Grey.Medium)
                            .DirectionFromLeftToRight();

                    if (!string.IsNullOrEmpty(_company?.Whatsapp))
                        col.Item().Text($"Wa: {_company.Whatsapp}")
                            .FontSize(10).FontColor(Colors.Grey.Medium)
                            .DirectionFromLeftToRight();
                });

                // CENTER: Logo (65 px — matches ReportGenerator)
                row.ConstantItem(65).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (_company?.Logo != null && _company.Logo.Length > 0)
                        c.Image(_company.Logo);
                    else
                        c.Text("شعار").FontSize(12).FontColor(Colors.Grey.Lighten1);
                });

                // RIGHT: Arabic
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(_company?.NameAr ?? "اسم الشركة")
                        .FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);

                    if (!string.IsNullOrEmpty(_company?.AdressAr))
                        col.Item().Text(_company.AdressAr)
                            .FontSize(10).FontColor(Colors.Grey.Medium);

                    if (!string.IsNullOrEmpty(_company?.Tel))
                    {
                        col.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(3)
                                .Text(_company.Tel)
                                .FontSize(10).FontColor(Colors.Grey.Medium)
                                .DirectionFromLeftToRight();
                            r.AutoItem().Text(": ت ")
                                .FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    }
                });
            });

            // ── Divider ───────────────────────────────────────────────────────
            column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // ── Invoice title + Cash/Credit badge on the same line ────────────
            column.Item().PaddingTop(8).Row(titleRow =>
            {
                // Left spacer so title stays centered
                titleRow.RelativeItem().AlignLeft().Element(c =>
                {
                    // ── Cash / Credit badge ───────────────────────────────────
                    // Green pill for كاش, Orange pill for آجل
                    var isCash = _model.IsCash;
                    var badgeText = isCash ? "كاش" : "آجل";
                    var bgColor = isCash ? "#16A34A" : "#D97706"; // green / amber
                    var textColor = Colors.White;

                    c.AlignLeft()
                     .Background(bgColor)
                     .CornerRadius(10)
                     .PaddingVertical(3)
                     .PaddingHorizontal(10)
                     .Text(badgeText)
                     .FontSize(11).Bold().FontColor(textColor);
                });

                // Center: title
                titleRow.RelativeItem(3).AlignCenter()
                    .Text("فاتورة مشتريات")
                    .FontSize(18).SemiBold().FontColor(Colors.Black);

                // Right spacer (symmetry)
                titleRow.RelativeItem();
            });

            // ── Meta fields — rendered as a Table so they never overflow ──────
            column.Item().PaddingTop(10).PaddingBottom(5).Element(ComposeMetaFields);

            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeMetaFields(IContainer container)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("رقم الفاتورة", _model.InvoiceNo),
            new("التاريخ",      _model.IssueDate),
        };
        if (!string.IsNullOrWhiteSpace(_model.Time))
            fields.Add(new("الوقت", _model.Time));

        fields.Add(new("المورد", _model.SupplierName));

        if (!string.IsNullOrWhiteSpace(_model.Notes))
            fields.Add(new("ملاحظات", _model.Notes));

        // Table with one RelativeColumn per field — widths share the page evenly
        container.Table(table =>
        {
            table.ColumnsDefinition(def =>
            {
                for (int i = 0; i < fields.Count; i++)
                    def.RelativeColumn();
            });

            foreach (var field in fields)
            {
                var f = field; // capture
                table.Cell().Padding(4).Element(c => RenderHeaderField(c, f));
            }
        });
    }

    private static void RenderHeaderField(IContainer container, KeyValuePair<string, string> item)
    {
        container.Column(col =>
        {
            // Label
            col.Item().AlignCenter()
                .Text(" : " + item.Key)
                .SemiBold().FontSize(11).FontColor(Colors.Blue.Darken4);

            // Value with teal underline
            bool containsArabic = item.Value?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;

            var valueCell = col.Item()
                .PaddingTop(2)
                .BorderBottom(1).BorderColor(Colors.Teal.Darken2)
                .AlignCenter();

            if (!containsArabic && !string.IsNullOrWhiteSpace(item.Value))
                valueCell.Text(item.Value ?? "")
                    .FontSize(11).FontColor(Colors.Black).SemiBold()
                    .DirectionFromLeftToRight();
            else
                valueCell.Text(item.Value ?? "")
                    .FontSize(11).FontColor(Colors.Black).SemiBold();
        });
    }

    // ── CONTENT ───────────────────────────────────────────────────────────────
    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                // RTL column order: totals first (right side on page)
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);    // الإجمالي
                    columns.RelativeColumn(1.5f); // خصم
                    columns.RelativeColumn(1.5f); // السعر
                    columns.RelativeColumn(1.5f); // الكمية
                    columns.RelativeColumn(4);    // الصنف
                    columns.RelativeColumn(1);    // م
                });

                // Header — full Excel-style border (matches ReportGenerator)
                table.Header(header =>
                {
                    foreach (var title in new[] { "الإجمالي", "خصم", "السعر", "الكمية", "الصنف", "م" })
                    {
                        header.Cell()
                            .Background("#F1F5F9")
                            .Border(1)
                            .BorderColor(Colors.Grey.Medium)
                            .Padding(5)
                            .AlignCenter()
                            .Text(title).SemiBold();
                    }
                });

                // Data rows — full border on all sides
                int index = 1;
                foreach (var item in _model.Items)
                {
                    RenderCell(table, item.Total.ToString("N2"), ltr: true);
                    RenderCell(table, item.Discount.ToString("N2"), ltr: true);
                    RenderCell(table, item.Price.ToString("N2"), ltr: true);
                    RenderCell(table, item.Quantity.ToString(), ltr: false);
                    RenderCell(table, item.ItemName, ltr: false);
                    RenderCell(table, index++.ToString(), ltr: true);
                }
            });

            colContainer.Item().PaddingTop(10).Element(ComposeTotals);
        });
    }

    private static void RenderCell(TableDescriptor table, string val, bool ltr)
    {
        var cell = table.Cell()
            .Border(1)
            .BorderColor(Colors.Grey.Medium)
            .Padding(5)
            .AlignCenter();

        bool containsArabic = val?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;

        if (ltr || (!containsArabic && !string.IsNullOrWhiteSpace(val)))
            cell.Text(val).FontSize(10).DirectionFromLeftToRight();
        else
            cell.Text(val).FontSize(10);
    }

    // ── TOTALS ────────────────────────────────────────────────────────────────
    private void ComposeTotals(IContainer container)
    {
        // ── بيانات العمود الأيسر (الرأسي) ──────────────────────────────────────
        double netAfterDiscount = _model.Total - _model.Discount + _model.AddMoney;
        double totalAccount = _model.NetAmount + _model.PreviousBalance;
        double remaining = totalAccount - _model.PaidAmount;

        var leftItems = new[]
        {
        ("الإجمالي",       _model.NetAmount.ToString("N2")),
        ("الرصيد السابق",  _model.PreviousBalance.ToString("N2")),
        ("الإجمالي",       totalAccount.ToString("N2")),
        ("المدفوع",        _model.PaidAmount.ToString("N2")),
        ("المتبقي",        remaining.ToString("N2")),
    };

        // ── بيانات الصف الأفقي (اليمين) ─────────────────────────────────────
        var rightItems = BuildRightTotals();

        container.Padding(5).Row(mainRow =>
        {
            // ── أقصى الشمال: العمود الرأسي ─────────────────────────────────
            mainRow.ConstantItem(130).Column(leftCol =>
            {
                foreach (var (label, value) in leftItems)
                {
                    leftCol.Item().PaddingBottom(5).Column(c =>
                    {
                        c.Item().AlignCenter()
                            .Text(label)
                            .SemiBold().FontSize(10).FontColor("#334155");

                        c.Item().PaddingTop(2)
                            .Border(0.5f).BorderColor(Colors.Black)
                            .CornerRadius(12)
                            .PaddingVertical(3).PaddingHorizontal(6)
                            .AlignCenter()
                            .Text(value)
                            .FontSize(11).FontColor("#1E293B")
                            .DirectionFromLeftToRight();
                    });
                }
            });

            // ── فاصل ──────────────────────────────────────────────────────────
            mainRow.ConstantItem(8).AlignCenter().AlignMiddle()
                .LineVertical(1).LineColor(Colors.Grey.Lighten2);

            // ── اليمين: الصف الأفقي ───────────────────────────────────────────
            mainRow.RelativeItem().PaddingHorizontal(4).Row(rightRow =>
            {
                foreach (var (label, value) in rightItems)
                {
                    rightRow.RelativeItem().PaddingHorizontal(3).Column(c =>
                    {
                        c.Item().AlignCenter()
                            .Text(label)
                            .SemiBold().FontSize(10).FontColor("#334155");

                        c.Item().PaddingTop(3)
                            .Border(0.5f).BorderColor(Colors.Black)
                            .CornerRadius(12)
                            .PaddingVertical(3).PaddingHorizontal(6)
                            .AlignCenter()
                            .Text(value)
                            .FontSize(11).FontColor("#1E293B")
                            .DirectionFromLeftToRight();
                    });
                }
            });
        });
    }

    private List<(string Label, string Value)> BuildRightTotals()
    {
        var list = new List<(string, string)>
    {
        ("إضافة",            _model.AddMoney.ToString("N2")),
        ("إجمالي بعد الخصم", (_model.Total - _model.Discount).ToString("N2")),
        ("الخصم",     _model.Discount.ToString("N2")),
        ("إجمالي قبل الخصم", _model.Total.ToString("N2")),

    };

        if (_model.IsTax)
        {
            double netBase = _model.Total - _model.Discount + _model.AddMoney;
            double vatPercent = netBase > 0 ? Math.Round((_model.VatTax / netBase) * 100, 2) : 0;
            double whtPercent = netBase > 0 ? Math.Round((_model.WhtTax / netBase) * 100, 2) : 0;

            list.Add(($"ض. قيمة مضافة {vatPercent:0.##}%", _model.VatTax.ToString("N2")));
            list.Add(($"ض. الأرباح {whtPercent:0.##}%", _model.WhtTax.ToString("N2")));
        }

        return list;
    }

    // ── FOOTER — identical to ReportGenerator.ComposeFooter ──────────────────
    private static void ComposeFooter(IContainer container)
    {
        const string color = "#000000";

        container.Column(col =>
        {
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft()
                    .Text($"Print Time : {DateTime.Now:dd-MM-yyyy hh:mm:ss tt}")
                    .FontSize(8).FontColor(color).DirectionFromLeftToRight();

                row.RelativeItem(2).AlignCenter()
                    .Text("Mazaya For Programing 01118152828   01000503370")
                    .FontSize(8).FontColor(color).DirectionFromLeftToRight();

                row.RelativeItem().AlignRight()
                    .DefaultTextStyle(x =>
                        x.FontSize(10).FontColor(color).DirectionFromLeftToRight())
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                        text.Span(" of ");
                        text.TotalPages();
                    });
            });
        });
    }
}