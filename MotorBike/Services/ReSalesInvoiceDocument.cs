using System;
using System.Collections.Generic;
using System.Linq;
using MotorBike.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorBike.Services;

public class ReSalesInvoiceItemModel
{
    public string ItemName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public double Price { get; set; }
    public double Discount { get; set; }
    public double Total { get; set; }
}

public class ReSalesInvoiceModel
{
    public string InvoiceNo { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsCash { get; set; }
    public List<ReSalesInvoiceItemModel> Items { get; set; } = new();

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

    // Payments table (for credit invoices)
    public List<(double Amount, string CashName, string Notes)> Payments { get; set; } = new();
}

public class ReSalesInvoiceDocument : IDocument
{
    private readonly ReSalesInvoiceModel _model;
    private readonly Company _company;

    public ReSalesInvoiceDocument(ReSalesInvoiceModel model, Company company)
    {
        _model = model;
        _company = company;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());
            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().Text(_company?.NameEn ?? "Company Name").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(_company?.AdressEn)) col.Item().Text(_company.AdressEn).FontSize(10).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(_company?.Whatsapp)) col.Item().Text($"Wa: {_company.Whatsapp}").FontSize(10).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                });
                row.ConstantItem(65).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (_company?.Logo != null && _company.Logo.Length > 0) c.Image(_company.Logo);
                    else c.Text("شعار").FontSize(12).FontColor(Colors.Grey.Lighten1);
                });
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(_company?.NameAr ?? "اسم الشركة").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(_company?.AdressAr)) col.Item().Text(_company.AdressAr).FontSize(10).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(_company?.Tel))
                    {
                        col.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(3).Text(_company.Tel).FontSize(10).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                            r.AutoItem().Text(": ت ").FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    }
                });
            });
            column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(8).Row(titleRow =>
            {
                titleRow.RelativeItem().AlignLeft().Element(c =>
                {
                    var badgeText = _model.IsCash ? "كاش" : "آجل";
                    var bgColor = _model.IsCash ? "#16A34A" : "#D97706";
                    c.AlignLeft().Background(bgColor).CornerRadius(10).PaddingVertical(3).PaddingHorizontal(10)
                     .Text(badgeText).FontSize(11).Bold().FontColor(Colors.White);
                });
                titleRow.RelativeItem(3).AlignCenter().Text("مرتجع مبيعات").FontSize(18).SemiBold().FontColor(Colors.Black);
                titleRow.RelativeItem();
            });
            column.Item().PaddingTop(10).PaddingBottom(5).Element(ComposeMetaFields);
            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeMetaFields(IContainer container)
    {
        var fields = new List<KeyValuePair<string, string>> { new("رقم المرتجع", _model.InvoiceNo), new("التاريخ", _model.IssueDate) };
        if (!string.IsNullOrWhiteSpace(_model.Time)) fields.Add(new("الوقت", _model.Time));
        fields.Add(new("العميل", _model.CustomerName));
        if (!string.IsNullOrWhiteSpace(_model.Notes)) fields.Add(new("ملاحظات", _model.Notes));
        container.Table(table =>
        {
            table.ColumnsDefinition(def => { for (int i = 0; i < fields.Count; i++) def.RelativeColumn(); });
            foreach (var field in fields) { var f = field; table.Cell().Padding(4).Element(c => RenderHeaderField(c, f)); }
        });
    }

    private static void RenderHeaderField(IContainer container, KeyValuePair<string, string> item)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter().Text(" : " + item.Key).SemiBold().FontSize(11).FontColor(Colors.Blue.Darken4);
            bool containsArabic = item.Value?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;
            var valueCell = col.Item().PaddingTop(2).BorderBottom(1).BorderColor(Colors.Teal.Darken2).AlignCenter();
            if (!containsArabic && !string.IsNullOrWhiteSpace(item.Value))
                valueCell.Text(item.Value ?? "").FontSize(11).FontColor(Colors.Black).SemiBold().DirectionFromLeftToRight();
            else valueCell.Text(item.Value ?? "").FontSize(11).FontColor(Colors.Black).SemiBold();
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2); columns.RelativeColumn(1.5f); columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f); columns.RelativeColumn(4); columns.RelativeColumn(1);
                });
                table.Header(header =>
                {
                    foreach (var title in new[] { "الإجمالي", "خصم", "السعر", "الكمية", "الصنف", "م" })
                        header.Cell().Background("#F1F5F9").Border(1).BorderColor(Colors.Grey.Medium).Padding(5).AlignCenter().Text(title).SemiBold();
                });
                int index = 1;
                foreach (var item in _model.Items)
                {
                    RenderCell(table, item.Total.ToString("N2"), true);
                    RenderCell(table, item.Discount.ToString("N2"), true);
                    RenderCell(table, item.Price.ToString("N2"), true);
                    RenderCell(table, item.Quantity.ToString(), false);
                    RenderCell(table, item.ItemName, false);
                    RenderCell(table, index++.ToString(), true);
                }
            });
            colContainer.Item().PaddingTop(10).Element(ComposeTotals);
        });
    }

    private static void RenderCell(TableDescriptor table, string val, bool ltr)
    {
        var cell = table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(5).AlignCenter();
        bool containsArabic = val?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;
        if (ltr || (!containsArabic && !string.IsNullOrWhiteSpace(val))) cell.Text(val).FontSize(10).DirectionFromLeftToRight();
        else cell.Text(val).FontSize(10);
    }

    private void ComposeTotals(IContainer container)
    {
        double totalAccount = _model.PreviousBalance - _model.NetAmount;
        double remaining = totalAccount + _model.PaidAmount;

        var leftItems = new[]
        {
            ("الإجمالي",      _model.NetAmount.ToString("N2")),
            ("الرصيد السابق", _model.PreviousBalance.ToString("N2")),
            ("الإجمالي",      totalAccount.ToString("N2")),
            ("المدفوع",       _model.PaidAmount.ToString("N2")),
            ("المتبقي",       remaining.ToString("N2")),
        };

        var rightItems = BuildRightTotals();

        container.Padding(5).Column(col =>
        {
            col.Item().Row(mainRow =>
            {
                // ── Left totals column (unchanged) ──
                mainRow.ConstantItem(130).Column(leftCol =>
                {
                    foreach (var (label, value) in leftItems)
                    {
                        leftCol.Item().PaddingBottom(5).Column(c =>
                        {
                            c.Item().AlignCenter()
                                .Text(label).SemiBold().FontSize(10).FontColor("#334155");
                            c.Item().PaddingTop(2)
                                .Border(0.5f).BorderColor(Colors.Black).CornerRadius(12)
                                .PaddingVertical(3).PaddingHorizontal(6).AlignCenter()
                                .Text(value).FontSize(11).FontColor("#1E293B")
                                .DirectionFromLeftToRight();
                        });
                    }
                });

                mainRow.ConstantItem(8).AlignCenter().AlignMiddle()
                    .LineVertical(1).LineColor(Colors.Grey.Lighten2);

                // ── Right side: price fields + payments table ──
                mainRow.RelativeItem().PaddingHorizontal(4).Column(rightCol =>
                {
                    // Price / tax fields (top)
                    rightCol.Item().Row(rightRow =>
                    {
                        foreach (var (label, value) in rightItems)
                        {
                            rightRow.RelativeItem().PaddingHorizontal(3).Column(c =>
                            {
                                c.Item().AlignCenter()
                                    .Text(label).SemiBold().FontSize(10).FontColor("#334155");
                                c.Item().PaddingTop(3)
                                    .Border(0.5f).BorderColor(Colors.Black).CornerRadius(12)
                                    .PaddingVertical(3).PaddingHorizontal(6).AlignCenter()
                                    .Text(value).FontSize(11).FontColor("#1E293B")
                                    .DirectionFromLeftToRight();
                            });
                        }
                    });

                    // ── Payments table rendered in the same right area ──
                    if (!_model.IsCash && _model.Payments.Any())
                    {
                        rightCol.Item().PaddingTop(8)
                            .Text("المدفوعات").SemiBold().FontSize(11)
                            .FontColor(Colors.Blue.Darken3).AlignCenter();

                        rightCol.Item().PaddingTop(4).Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                def.RelativeColumn(2);
                                def.RelativeColumn(2);
                                def.RelativeColumn(3);
                            });
                            table.Header(header =>
                            {
                                foreach (var title in new[] { "الملاحظات", "الخزينة", "المبلغ" })
                                    header.Cell().Background("#F1F5F9")
                                        .Border(1).BorderColor(Colors.Grey.Medium)
                                        .Padding(4).AlignCenter()
                                        .Text(title).SemiBold().FontSize(10);
                            });
                            foreach (var (amt, cash, notes) in _model.Payments)
                            {

                                table.Cell().Border(1).BorderColor(Colors.Grey.Medium)
                                    .Padding(4).AlignCenter()
                                    .Text(notes ?? "").FontSize(10);
                                table.Cell().Border(1).BorderColor(Colors.Grey.Medium)
                                    .Padding(4).AlignCenter()
                                    .Text(cash).FontSize(10);
                                table.Cell().Border(1).BorderColor(Colors.Grey.Medium)
                                    .Padding(4).AlignCenter()
                                    .Text(amt.ToString("N2")).FontSize(10).DirectionFromLeftToRight();
                            }
                        });
                    }
                });
            });
        });
    }

    private List<(string Label, string Value)> BuildRightTotals()
    {
        var list = new List<(string, string)>();

        if (_model.IsTax)
        {
            double netBase = _model.Total - _model.Discount + _model.AddMoney;
            double vatPct = netBase > 0 ? Math.Round((_model.VatTax / netBase) * 100, 2) : 0;
            double whtPct = netBase > 0 ? Math.Round((_model.WhtTax / netBase) * 100, 2) : 0;

            list.Add(($"ض. قيمة مضافة {vatPct:0.##}%", _model.VatTax.ToString("N2")));
            list.Add(($"ض. الأرباح {whtPct:0.##}%", _model.WhtTax.ToString("N2")));
        }

        list.Add(("إضافة", _model.AddMoney.ToString("N2")));
        list.Add(("إجمالي بعد الخصم", (_model.Total - _model.Discount).ToString("N2")));
        list.Add(("الخصم", _model.Discount.ToString("N2")));
        list.Add(("إجمالي قبل الخصم", _model.Total.ToString("N2")));

        return list;
    }

    private static void ComposeFooter(IContainer container)
    {
        const string color = "#000000";
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"Print Time : {DateTime.Now:dd-MM-yyyy hh:mm:ss tt}").FontSize(8).FontColor(color).DirectionFromLeftToRight();
                row.RelativeItem(2).AlignCenter().Text("Mazaya For Programing 01118152828   01000503370").FontSize(8).FontColor(color).DirectionFromLeftToRight();
                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(10).FontColor(color).DirectionFromLeftToRight())
                    .Text(text => { text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages(); });
            });
        });
    }
}
