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
    
    public List<BuyInvoiceItemModel> Items { get; set; } = new();
    
    public double Total { get; set; }
    public double Discount { get; set; }
    public double AddMoney { get; set; }
    public bool IsTax { get; set; }
    public double VatTax { get; set; }
    public double WhtTax { get; set; }
    public double NetAmount { get; set; }
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

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).DirectionFromRightToLeft());

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
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(_company?.NameAr ?? "اسم الشركة").FontSize(16).SemiBold().FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(_company?.AdressAr))
                        col.Item().Text(_company.AdressAr).FontSize(10).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(_company?.Tel))
                        col.Item().Text("ت: " + _company.Tel).FontSize(10).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight().AlignRight();
                });

                row.ConstantItem(60).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (_company?.Logo != null && _company.Logo.Length > 0)
                    {
                        c.Image(_company.Logo);
                    }
                });

                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().Text("فاتورة مشتريات").FontSize(18).SemiBold().FontColor(Colors.Black).DirectionFromRightToLeft();
                    col.Item().Text($"رقم الفاتورة: {_model.InvoiceNo}").FontSize(12).FontColor(Colors.Red.Medium).Bold().DirectionFromRightToLeft();
                });
            });

            column.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("التاريخ: ").SemiBold().FontSize(10);
                        text.Span(_model.IssueDate).FontSize(10);
                    });
                    if (!string.IsNullOrWhiteSpace(_model.Time))
                    {
                         col.Item().Text(text =>
                        {
                            text.Span("الوقت: ").SemiBold().FontSize(10);
                            text.Span(_model.Time).FontSize(10);
                        });
                    }
                });

                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(text =>
                    {
                        text.Span("المورد: ").SemiBold().FontSize(10);
                        text.Span(_model.SupplierName).FontSize(10);
                    });
                    if (!string.IsNullOrWhiteSpace(_model.Notes))
                    {
                        col.Item().Text(text =>
                        {
                            text.Span("ملاحظات: ").SemiBold().FontSize(10);
                            text.Span(_model.Notes).FontSize(10);
                        });
                    }
                });
            });
            
            column.Item().PaddingBottom(10);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1); // م
                    columns.RelativeColumn(4); // الصنف
                    columns.RelativeColumn(1.5f); // الكمية
                    columns.RelativeColumn(1.5f); // السعر
                    columns.RelativeColumn(1.5f); // الخصم
                    columns.RelativeColumn(2); // الاجمالي
                });

                table.Header(header =>
                {
                    header.Cell().Element(CellStyle).Text("م");
                    header.Cell().Element(CellStyle).Text("الصنف");
                    header.Cell().Element(CellStyle).Text("الكمية");
                    header.Cell().Element(CellStyle).Text("السعر");
                    header.Cell().Element(CellStyle).Text("خصم");
                    header.Cell().Element(CellStyle).Text("الإجمالي");

                    static IContainer CellStyle(IContainer container) => container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(5).BorderBottom(1).BorderColor(Colors.Black).AlignCenter();
                });

                int index = 1;
                foreach (var item in _model.Items)
                {
                    table.Cell().Element(CellStyle).Text(index++.ToString());
                    table.Cell().Element(CellStyle).AlignRight().PaddingRight(4).Text(item.ItemName);
                    table.Cell().Element(CellStyle).Text(item.Quantity.ToString());
                    table.Cell().Element(CellStyle).Text(item.Price.ToString("N2")).DirectionFromLeftToRight();
                    table.Cell().Element(CellStyle).Text(item.Discount.ToString("N2")).DirectionFromLeftToRight();
                    table.Cell().Element(CellStyle).Text(item.Total.ToString("N2")).SemiBold().DirectionFromLeftToRight();

                    static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).AlignCenter();
                }
            });

            col.Item().PaddingTop(10).Element(ComposeTotals);
        });
    }

    private void ComposeTotals(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem(); // spacer 

            row.ConstantItem(180).Column(col => 
            {
                col.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Column(innerCol =>
                {
                    DrawTotalRow(innerCol, "الإجمالي:", _model.Total);
                    if (_model.Discount > 0)
                        DrawTotalRow(innerCol, "الخصم:", _model.Discount);
                    if (_model.AddMoney > 0)
                        DrawTotalRow(innerCol, "مصاريف إضافية:", _model.AddMoney);
                    
                    if (_model.IsTax)
                    {
                        DrawTotalRow(innerCol, "ض. قيمة مضافة:", _model.VatTax);
                        DrawTotalRow(innerCol, "ض. أ.ت.ص:", _model.WhtTax);
                    }

                    innerCol.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    
                    innerCol.Item().Padding(4).Row(r =>
                    {
                        r.RelativeItem().Text("الصافي:").SemiBold().FontSize(12);
                        r.RelativeItem().AlignLeft().Text(_model.NetAmount.ToString("N2")).SemiBold().FontSize(12).DirectionFromLeftToRight();
                    });
                });

                col.Item().PaddingTop(4).Border(1).BorderColor(Colors.Grey.Lighten2).Column(innerCol =>
                {
                     DrawTotalRow(innerCol, "المدفوع:", _model.PaidAmount);
                     DrawTotalRow(innerCol, "المتبقي:", _model.RemainingAmount);
                });
            });
        });
    }

    private void DrawTotalRow(ColumnDescriptor col, string label, double value)
    {
        col.Item().Padding(2).PaddingHorizontal(4).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(10);
            r.RelativeItem().AlignLeft().Text(value.ToString("N2")).FontSize(10).DirectionFromLeftToRight();
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }
}
