using System;
using System.Collections.Generic;
using System.Linq;
using MotorBike.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorBike.Services;

public class CusPaymentReceiptModel
{
    public string ReceiptNo { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CashName { get; set; } = string.Empty;
    public string PayTypeName { get; set; } = string.Empty;
    public double Amount { get; set; }
    public string Notes { get; set; } = string.Empty;
    public double PreviousBalance { get; set; }
    public double BalanceAfter { get; set; }
}

public class CusPaymentReceiptDocument : IDocument
{
    private readonly CusPaymentReceiptModel _model;
    private readonly Company _company;

    public CusPaymentReceiptDocument(CusPaymentReceiptModel model, Company company)
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
            column.Item().PaddingTop(8).AlignCenter().Text(_model.PayTypeName).FontSize(18).SemiBold().FontColor(Colors.Black);
            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(5).Column(col =>
        {
            var fields = new List<KeyValuePair<string, string>>
            {
                new("رقم الإيصال", _model.ReceiptNo),
                new("التاريخ", _model.IssueDate),
                new("العميل", _model.CustomerName),
                new("الخزينة", _model.CashName),
            };

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def => { for (int i = 0; i < fields.Count; i++) def.RelativeColumn(); });
                foreach (var field in fields) { var f = field; table.Cell().Padding(4).Element(c => RenderField(c, f)); }
            });

            col.Item().PaddingTop(15).Element(ComposeTotals);
        });
    }

    private static void RenderField(IContainer container, KeyValuePair<string, string> item)
    {
        container.Column(c =>
        {
            c.Item().AlignCenter().Text(" : " + item.Key).SemiBold().FontSize(11).FontColor(Colors.Blue.Darken4);
            bool containsArabic = item.Value?.Any(ch => ch >= 0x0600 && ch <= 0x06FF) ?? false;
            var valueCell = c.Item().PaddingTop(2).BorderBottom(1).BorderColor(Colors.Teal.Darken2).AlignCenter();
            if (!containsArabic && !string.IsNullOrWhiteSpace(item.Value))
                valueCell.Text(item.Value ?? "").FontSize(11).FontColor(Colors.Black).SemiBold().DirectionFromLeftToRight();
            else valueCell.Text(item.Value ?? "").FontSize(11).FontColor(Colors.Black).SemiBold();
        });
    }

    private void ComposeTotals(IContainer container)
    {
        var totals = new List<KeyValuePair<string, string>>
        {
            new("المبلغ", _model.Amount.ToString("N2")),
            new("الرصيد السابق", _model.PreviousBalance.ToString("N2")),
            new("الرصيد بعد العملية", _model.BalanceAfter.ToString("N2")),
        };
        if (!string.IsNullOrWhiteSpace(_model.Notes)) totals.Add(new("ملاحظات", _model.Notes));

        const int itemsPerRow = 3;
        container.Padding(5).Column(column =>
        {
            foreach (var rowTotals in totals.Chunk(itemsPerRow))
            {
                var items = rowTotals.Reverse().ToList();
                column.Item().PaddingBottom(8).Row(row =>
                {
                    foreach (var kv in items)
                    {
                        row.RelativeItem().PaddingHorizontal(3).Column(c =>
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(10).FontColor("#334155");
                            c.Item().PaddingTop(3).Border(0.5f).BorderColor(Colors.Black).CornerRadius(12)
                                .PaddingVertical(3).PaddingHorizontal(6).AlignCenter()
                                .Text(kv.Value).FontSize(11).FontColor("#1E293B").DirectionFromLeftToRight();
                        });
                    }
                    for (int i = items.Count; i < itemsPerRow; i++) row.RelativeItem();
                });
            }
        });
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
