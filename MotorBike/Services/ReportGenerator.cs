using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MotorBike.Models;

namespace MotorBike.Services;

public class ReportGenerator
{
    static ReportGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static IDocument CreatePdfDocument(Company company, string reportTitle, System.Data.DataView dataView, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null, System.Data.DataView? extraTable = null, string? extraTableTitle = null)
    {
        var dt = dataView.Table;
        var columns = new List<string>();
        foreach (System.Data.DataColumn col in dt.Columns)
        {
            columns.Add(col.ColumnName);
        }

        // Reverse columns to display them from Right-To-Left
        columns.Reverse();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => ComposeHeader(c, company, reportTitle, headerInfo));
                page.Content().Element(c => ComposeContent(c, columns, dataView, footerTotals, extraTable, extraTableTitle));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    public static byte[] GeneratePdf(Company company, string reportTitle, System.Data.DataView dataView, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null, System.Data.DataView? extraTable = null, string? extraTableTitle = null)
    {
        return CreatePdfDocument(company, reportTitle, dataView, headerInfo, footerTotals, extraTable, extraTableTitle).GeneratePdf();
    }

    public static IDocument CreateDetailedPdfDocument(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => ComposeHeader(c, company, reportTitle, headerInfo));
                page.Content().Element(c => ComposeDetailedContent(c, data.ToList(), footerTotals));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    public static byte[] GenerateDetailedPdf(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return CreateDetailedPdfDocument(company, reportTitle, data, headerInfo, footerTotals).GeneratePdf();
    }

    public static IDocument CreateInvoiceDetailedPdfDocument(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => ComposeHeader(c, company, reportTitle, headerInfo));
                page.Content().Element(c => ComposeInvoiceDetailedContent(c, data.ToList(), footerTotals));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    public static byte[] GenerateInvoiceDetailedPdf(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return CreateInvoiceDetailedPdfDocument(company, reportTitle, data, headerInfo, footerTotals).GeneratePdf();
    }

    public static IDocument CreateImportInvoiceDetailedPdfDocument(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => ComposeHeader(c, company, reportTitle, headerInfo));
                page.Content().Element(c => ComposeImportInvoiceDetailedContent(c, data.ToList(), footerTotals));
                page.Footer().Element(ComposeFooter);
            });
        });
    }

    public static byte[] GenerateImportInvoiceDetailedPdf(Company company, string reportTitle, IEnumerable<DetailedAccountRow> data, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        return CreateImportInvoiceDetailedPdfDocument(company, reportTitle, data, headerInfo, footerTotals).GeneratePdf();
    }



    private static void ComposeHeader(IContainer container, Company company, string reportTitle, Dictionary<string, string>? headerInfo)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                // ✅ FIX: English on LEFT | Logo CENTER | Arabic on RIGHT
                // In RTL layout, items render right-to-left, so Arabic (last) → RIGHT side

                // LEFT: English Company Info
                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().Text(company?.NameEn ?? "Company Name").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(company?.AdressEn))
                        col.Item().Text(company.AdressEn).FontSize(12).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(company?.Whatsapp))
                        col.Item().Text($"Wa: {company.Whatsapp}").FontSize(12).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                });

                // CENTER: Logo — ✅ reduced from 100 to 65
                row.ConstantItem(65).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (company?.Logo != null && company.Logo.Length > 0)
                    {
                        c.Image(company.Logo);
                    }
                    else
                    {
                        c.Text("شعار").FontSize(12).FontColor(Colors.Grey.Lighten1);
                    }
                });

                // RIGHT: Arabic Company Info
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(company?.NameAr ?? "اسم الشركة").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(company?.AdressAr))
                        col.Item().Text(company.AdressAr).FontSize(12).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(company?.Tel))
                    {
                        col.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(5).Text(company.Tel).FontSize(12).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                            r.AutoItem().Text(": ت ").FontSize(12).FontColor(Colors.Grey.Medium);
                        });
                    }
                });
            });

            column.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().PaddingTop(10).AlignCenter().Text(reportTitle)
                .FontSize(20).SemiBold().FontColor(Colors.Black);

            if (headerInfo != null && headerInfo.Count > 0)
            {
                var items = headerInfo.ToList();
                const int maxPerRow = 3; // max fields per row (safe for A4 Portrait)

                // Split into chunks of maxPerRow to wrap onto multiple rows
                for (int chunkStart = items.Count - 1; chunkStart >= 0; chunkStart -= maxPerRow)
                {
                    int chunkEnd = Math.Max(chunkStart - maxPerRow + 1, 0);
                    column.Item().PaddingTop(6).PaddingBottom(2).Row(r =>
                    {
                        r.RelativeItem(); // left spacer

                        for (int i = chunkStart; i >= chunkEnd; i--)
                        {
                            r.AutoItem().Element(c => RenderHeaderField(c, items[i]));
                            if (i > chunkEnd)
                                r.ConstantItem(20); // gap between fields
                        }

                        r.RelativeItem(); // right spacer
                    });
                }
                column.Item().PaddingBottom(4);
            }

            column.Item().PaddingBottom(15);
        });
    }

    // ── Helper: renders a single "Label : [underlined value]" field ──────────────
    private static void RenderHeaderField(IContainer container, KeyValuePair<string, string> item)
    {
        container.Row(r =>
        {
            var valueText = r.ConstantItem(100)
                 .BorderBottom(1).BorderColor(Colors.Teal.Darken2)
                 .AlignCenter()
                 .Text(item.Value ?? "")
                 .FontSize(12).FontColor(Colors.Black).SemiBold();

            // Label on the right (RTL first = rightmost)
            r.AutoItem()
             .PaddingLeft(8)
             .Text(" : " + item.Key  )
             .SemiBold().FontSize(12).FontColor(Colors.Blue.Darken4);

            bool containsArabic = item.Value?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;

            if (!containsArabic && !string.IsNullOrWhiteSpace(item.Value))
                valueText.DirectionFromLeftToRight();
        });
    }

    private static void ComposeContent(IContainer container, List<string> columns, System.Data.DataView dataView, Dictionary<string, string>? footerTotals, System.Data.DataView? extraTable = null, string? extraTableTitle = null)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                table.ColumnsDefinition(columnsDefinition =>
                {
                    foreach (var col in columns)
                    {
                        columnsDefinition.RelativeColumn();
                    }
                });

                // Header Row — ✅ full border on all sides (Excel-style)
                table.Header(header =>
                {
                    foreach (var col in columns)
                    {
                        header.Cell()
                            .Background("#F1F5F9")
                            .Border(1)                          // ✅ full border
                            .BorderColor(Colors.Grey.Medium)    // ✅ visible grid lines
                            .Padding(5)
                            .AlignCenter()
                            .Text(col).SemiBold();
                    }
                });

                // Data Rows — ✅ full border on all sides (Excel-style)
                foreach (System.Data.DataRowView rowView in dataView)
                {
                    var row = rowView.Row;
                    foreach (var col in columns)
                    {
                        var val = row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";

                        var cell = table.Cell()
                            .Border(1)                          // ✅ full border (was BorderBottom only)
                            .BorderColor(Colors.Grey.Medium)  // ✅ light grid color
                            .Padding(5)
                            .AlignCenter();

                        bool containsArabic = val?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;

                        if (!containsArabic && !string.IsNullOrWhiteSpace(val))
                            cell.Text(val).FontSize(10).DirectionFromLeftToRight();
                        else
                            cell.Text(val).FontSize(10);
                    }
                }
            });

            // ✅ Render Extra Table if provided (e.g., Car Status in Movement Report)
            if (extraTable != null && extraTable.Count > 0)
            {
                colContainer.Item().PaddingTop(20).Column(extraCol =>
                {
                    if (!string.IsNullOrEmpty(extraTableTitle))
                    {
                        extraCol.Item().PaddingBottom(5).Text(extraTableTitle).FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2).AlignCenter();
                    }

                    extraCol.Item().Table(table =>
                    {
                        var dtExtra = extraTable.Table;
                        var extraCols = new List<string>();
                        foreach (System.Data.DataColumn col in dtExtra.Columns) extraCols.Add(col.ColumnName);
                        extraCols.Reverse(); // RTL

                        table.ColumnsDefinition(cd => { foreach (var _ in extraCols) cd.RelativeColumn(); });

                        table.Header(header =>
                        {
                            foreach (var col in extraCols)
                            {
                                header.Cell().Background("#F8FAFC").Border(1).BorderColor(Colors.Grey.Medium).Padding(5).AlignCenter().Text(col).SemiBold().FontSize(10);
                            }
                        });

                        foreach (System.Data.DataRowView rowView in extraTable)
                        {
                            var row = rowView.Row;
                            foreach (var col in extraCols)
                            {
                                var val = row[col]?.ToString() ?? "";
                                table.Cell().Border(1).BorderColor(Colors.Grey.Medium).Padding(5).AlignCenter().Text(val).FontSize(10);
                            }
                        }
                    });
                });
            }

            if (footerTotals != null && footerTotals.Count > 0)
            {
                colContainer.Item().PaddingTop(5).Padding(10).Row(r =>
                {
                    // Reverse dictionary to display from Right-To-Left
                    foreach (var kv in footerTotals.Reverse())
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(11).FontColor("#334155");

                            var valueContainer = c.Item().PaddingTop(2).MaxWidth(120).AlignCenter();

                            bool isNumericOrDate = kv.Value?.Any(ch => char.IsDigit(ch) && !char.IsLetter(ch)) ?? false;
                            var textElement = valueContainer
                                .Border(0.5f)
                                .BorderColor(Colors.Black)
                                .CornerRadius(15) 
                                .PaddingVertical(4)
                                .PaddingHorizontal(12)
                                .AlignCenter()
                                .Text(kv.Value)
                                .FontSize(12)
                                .FontColor("#1E293B");

                            if (isNumericOrDate && !string.IsNullOrWhiteSpace(kv.Value))
                                textElement.DirectionFromLeftToRight();
                        });
                    }
                });
            }
        });
    }

    private static void ComposeDetailedContent(IContainer container, List<DetailedAccountRow> data, Dictionary<string, string>? footerTotals)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                table.ColumnsDefinition(columnsDefinition =>
                {
                    columnsDefinition.ConstantColumn(80); // رصيد دائن
                    columnsDefinition.ConstantColumn(80); // رصيد مدين
                    columnsDefinition.ConstantColumn(70); // دائن
                    columnsDefinition.ConstantColumn(70); // مدين
                    columnsDefinition.RelativeColumn();   // البيان
                    columnsDefinition.ConstantColumn(90); // نوع الحركة
                    columnsDefinition.ConstantColumn(60); // رقم الحركة
                    columnsDefinition.ConstantColumn(70); // التاريخ
                });

                table.Header(header =>
                {
                    string[] cols = { "رصيد دائن", "رصيد مدين", "دائن (له)", "مدين (عليه)", "البيان", "نوع الحركة", "رقم الحركة", "التاريخ" };
                    foreach (var col in cols)
                    {
                        header.Cell().Background("#F1F5F9").Border(1).BorderColor(Colors.Grey.Medium).Padding(4).AlignCenter().Text(col).SemiBold().FontSize(11);
                    }
                });

                foreach (var row in data)
                {
                    Func<IContainer, IContainer> cStyle = c => c.Border(1).BorderColor(Colors.Grey.Medium).Padding(4).AlignCenter().AlignMiddle();

                    table.Cell().Element(cStyle).Text(row.RunningCredit > 0 ? row.RunningCredit.ToString("N2") : "0").FontSize(10);
                    table.Cell().Element(cStyle).Text(row.RunningDebit > 0 ? row.RunningDebit.ToString("N2") : "0").FontSize(10);
                    table.Cell().Element(cStyle).Text(row.Credit > 0 ? row.Credit.ToString("N2") : "0").FontSize(10);
                    table.Cell().Element(cStyle).Text(row.Debit > 0 ? row.Debit.ToString("N2") : "0").FontSize(10);
                    table.Cell().Element(cStyle).AlignRight().Text(row.Notes).FontSize(10);
                    table.Cell().Element(cStyle).Text(row.TransType ?? "").FontSize(10);
                    table.Cell().Element(cStyle).Text(row.RefNo ?? "").FontSize(10);
                    table.Cell().Element(cStyle).Text(row.Date ?? "").FontSize(10).DirectionFromLeftToRight();

                    if (row.HasItems)
                    {
                        table.Cell().ColumnSpan(8).Background("#F8FAFC").Border(1).BorderColor(Colors.Grey.Lighten1).PaddingRight(100).PaddingLeft(10).PaddingVertical(4).Table(innerTable =>
                        {
                            if (row.IsCarTransaction)
                            {
                                innerTable.ColumnsDefinition(innerDef =>
                                {
                                    innerDef.ConstantColumn(80); // السعر
                                    innerDef.ConstantColumn(80); // العداد
                                    innerDef.ConstantColumn(80); // اللوحة
                                    innerDef.ConstantColumn(100); // الماتور
                                    innerDef.ConstantColumn(100); // الشاسيه
                                    innerDef.RelativeColumn();   // الماركة والموديل
                                });

                                innerTable.Header(innerHeader =>
                                {
                                    string[] innerCols = { "السعر", "العداد", "اللوحة", "الماتور", "الشاسيه", "الماركة والموديل" };
                                    foreach (var c in innerCols)
                                        innerHeader.Cell().Border(1).BorderColor(Colors.Grey.Medium).Background("#DCFCE7").Padding(3).AlignCenter().Text(c).FontSize(10).FontColor("#166534").SemiBold();
                                });

                                foreach (var item in row.Items)
                                {
                                    Func<IContainer, IContainer> innerStyle = c => c.Border(1).BorderColor(Colors.Grey.Lighten1).Background("#F0FDF4").Padding(3).AlignCenter().AlignMiddle();
                                    innerTable.Cell().Element(innerStyle).Text(item.Price.ToString("N2")).FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.Mileage.ToString()).FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.PlateNo ?? "").FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.MotorNo ?? "").FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.ChassisNo ?? "").FontSize(10);
                                    innerTable.Cell().Element(innerStyle).AlignRight().Text(item.ItemName ?? "").FontSize(10);
                                }
                            }
                            else
                            {
                                innerTable.ColumnsDefinition(innerDef =>
                                {
                                    innerDef.ConstantColumn(80); // إجمالي
                                    innerDef.ConstantColumn(60); // خصم%
                                    innerDef.ConstantColumn(70); // السعر
                                    innerDef.ConstantColumn(50); // الكمية
                                    innerDef.ConstantColumn(60); // الوحدة
                                    innerDef.RelativeColumn();   // الصنف
                                });

                                innerTable.Header(innerHeader =>
                                {
                                    string[] innerCols = { "إجمالي", "خصم%", "السعر", "الكمية", "الوحدة", "الصنف" };
                                    foreach (var c in innerCols)
                                        innerHeader.Cell().Border(1).BorderColor(Colors.Grey.Medium).Background("#FEF3C7").Padding(3).AlignCenter().Text(c).FontSize(10).FontColor("#92400E").SemiBold();
                                });

                                foreach (var item in row.Items)
                                {
                                    Func<IContainer, IContainer> innerStyle = c => c.Border(1).BorderColor(Colors.Grey.Lighten1).Background("#FFFBEB").Padding(3).AlignCenter().AlignMiddle();
                                    innerTable.Cell().Element(innerStyle).Text(item.Total.ToString("N2")).FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.DiscPer.ToString("N0") + "%").FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.Price.ToString("N2")).FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.Qty.ToString("N2")).FontSize(10);
                                    innerTable.Cell().Element(innerStyle).Text(item.Unit ?? "").FontSize(10);
                                    innerTable.Cell().Element(innerStyle).AlignRight().Text(item.ItemName ?? "").FontSize(10);
                                }
                            }
                        });
                    }
                }
            });

            if (footerTotals != null && footerTotals.Count > 0)
            {
                colContainer.Item().PaddingTop(10).Padding(10).Row(r =>
                {
                    foreach (var kv in Enumerable.Reverse(footerTotals))
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(11).FontColor("#334155");

                            var valueContainer = c.Item().PaddingTop(2).MaxWidth(120).AlignCenter();

                            bool isNumericOrDate = kv.Value?.Any(ch => char.IsDigit(ch) && !char.IsLetter(ch)) ?? false;
                            var textElement = valueContainer
                                .Border(0.5f)
                                .BorderColor(Colors.Black)
                                .CornerRadius(15) 
                                .PaddingVertical(4)
                                .PaddingHorizontal(12)
                                .AlignCenter()
                                .Text(kv.Value)
                                .FontSize(12)
                                .FontColor("#1E293B");

                            if (isNumericOrDate && !string.IsNullOrWhiteSpace(kv.Value))
                                textElement.DirectionFromLeftToRight();
                        });
                    }
                });
            }
        });
    }

    private static void ComposeInvoiceDetailedContent(IContainer container, List<DetailedAccountRow> data, Dictionary<string, string>? footerTotals)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);   // 1 صافي الفاتورة
                    columns.ConstantColumn(60);  // 2 اضافة
                    columns.ConstantColumn(60);  // 3 خصم
                    columns.ConstantColumn(60);  // 4 ض.ق.م
                    columns.ConstantColumn(60);  // 5 ض.أ.ت.ص
                    columns.RelativeColumn(1);   // 6 إجمالي الفاتورة
                    columns.RelativeColumn(2);   // 7 العميل
                    columns.ConstantColumn(70);  // 8 نوع الحركة
                    columns.ConstantColumn(60);  // 9 رقم الحركة
                    columns.ConstantColumn(70);  // 10 التاريخ
                });

                uint currentRow = 1;

                string[] headers = { "صافي الفاتورة", "اضافة", "خصم", "ض.ق.م", "ض.أ.ت.ص", "إجمالي الفاتورة", "العميل", "نوع الحركة", "رقم الحركة", "التاريخ" };
                for (uint i = 0; i < headers.Length; i++)
                {
                    table.Cell().Row(currentRow).Column(i + 1).Background("#D1FAE5").Border(1).BorderColor(Colors.Teal.Medium).Padding(5).AlignCenter().Text(headers[i]).SemiBold().FontColor("#065F46");
                }
                currentRow++;

                foreach (var row in data)
                {
                    table.Cell().Row(currentRow).Column(1).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceNet.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(2).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceAdd.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(3).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceDisc.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(4).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.VatTaxDisplay).FontSize(10);
                    table.Cell().Row(currentRow).Column(5).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.TaxDisplay).FontSize(10);
                    table.Cell().Row(currentRow).Column(6).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceTotal.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(7).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignLeft().Text(row.CustomerName).FontSize(10);
                    table.Cell().Row(currentRow).Column(8).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.TransType).FontSize(10);
                    table.Cell().Row(currentRow).Column(9).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.RefNo).FontSize(10);
                    table.Cell().Row(currentRow).Column(10).Border(1).BorderColor(Colors.Teal.Lighten3).Padding(5).AlignCenter().Text(row.Date).FontSize(10);
                    currentRow++;

                    if (row.HasItems)
                    {
                        table.Cell().Row(currentRow).Column(1).ColumnSpan(10).PaddingBottom(10).PaddingRight(40).PaddingLeft(10).PaddingTop(5).Element(c =>
                        {
                            c.Table(innerTable =>
                            {
                                innerTable.ColumnsDefinition(innerCols =>
                                {
                                    innerCols.RelativeColumn(1); // 1 إجمالي
                                    innerCols.RelativeColumn(1); // 2 خصم%
                                    innerCols.RelativeColumn(1); // 3 السعر
                                    innerCols.RelativeColumn(1); // 4 الكمية
                                    innerCols.RelativeColumn(1); // 5 الوحدة
                                    innerCols.RelativeColumn(3); // 6 الصنف
                                });

                                uint innerRow = 1;
                                string[] innerHeaders = { "إجمالي", "خصم%", "السعر", "الكمية", "الوحدة", "الصنف" };
                                for (uint i = 0; i < innerHeaders.Length; i++)
                                {
                                    innerTable.Cell().Row(innerRow).Column(i + 1).Background("#FEF3C7").Border(1).BorderColor(Colors.Orange.Lighten2).Padding(4).AlignCenter().Text(innerHeaders[i]).SemiBold().FontSize(10).FontColor("#92400E");
                                }
                                innerRow++;

                                foreach (var item in row.Items)
                                {
                                    innerTable.Cell().Row(innerRow).Column(1).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Total.ToString("N2")).FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(2).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.DiscPer.ToString("N2")).FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(3).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Price.ToString("N2")).FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(4).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Qty.ToString("N2")).FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(5).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Unit).FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(6).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignLeft().Text(item.ItemName).FontSize(9);
                                    innerRow++;
                                }
                            });
                        });
                        currentRow++;
                    }
                }
            });

            if (footerTotals != null && footerTotals.Count > 0)
            {
                colContainer.Item().PaddingTop(5).Padding(10).Row(r =>
                {
                    foreach (var kv in footerTotals.Reverse())
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(11).FontColor("#334155");

                            var valueContainer = c.Item().PaddingTop(2).MaxWidth(120).AlignCenter();

                            bool isNumericOrDate = kv.Value?.Any(ch => char.IsDigit(ch) && !char.IsLetter(ch)) ?? false;
                            var textElement = valueContainer
                                .Border(0.5f)
                                .BorderColor(Colors.Black)
                                .CornerRadius(15) 
                                .PaddingVertical(4)
                                .PaddingHorizontal(12)
                                .AlignCenter()
                                .Text(kv.Value)
                                .FontSize(12)
                                .FontColor("#1E293B");

                            if (isNumericOrDate && !string.IsNullOrWhiteSpace(kv.Value))
                                textElement.DirectionFromLeftToRight();
                        });
                    }
                });
            }
        });
    }

    private static void ComposeImportInvoiceDetailedContent(IContainer container, List<DetailedAccountRow> data, Dictionary<string, string>? footerTotals)
    {
        container.PaddingVertical(1).Column(colContainer =>
        {
            colContainer.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);   // 1 قيمة الفاتورة (أجنبي)
                    columns.RelativeColumn(1);   // 2 سعر العملة
                    columns.RelativeColumn(1);   // 3 قيمة الفاتورة (محلي)
                    columns.RelativeColumn(1);   // 4 إجمالي المصاريف
                    columns.RelativeColumn(1);   // 5 التكلفة الكلية
                    columns.RelativeColumn(2);   // 6 المورد
                    columns.RelativeColumn(2);   // 7 اسم الشحنة
                    columns.ConstantColumn(70);  // 8 نوع الشحنة
                    columns.ConstantColumn(60);  // 9 رقم الشحنة
                    columns.ConstantColumn(70);  // 10 التاريخ
                });

                uint currentRow = 1;

                string[] headers = { "قيمة الفاتورة (أجنبي)", "سعر العملة", "قيمة الفاتورة (محلي)", "المصروفات", "التكلفة الكلية", "المورد", "اسم الشحنة", "النوع", "رقم الشحنة", "التاريخ" };
                for (uint i = 0; i < headers.Length; i++)
                {
                    table.Cell().Row(currentRow).Column(i + 1).Background("#DBEAFE").Border(1).BorderColor(Colors.Blue.Medium).Padding(5).AlignCenter().Text(headers[i]).SemiBold().FontColor("#1E3A8A");
                }
                currentRow++;

                foreach (var row in data)
                {
                    table.Cell().Row(currentRow).Column(1).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceNet.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(2).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.VatTaxDisplay).FontSize(10);
                    table.Cell().Row(currentRow).Column(3).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceDisc.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(4).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceAdd.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(5).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.InvoiceTotal.ToString("N2")).FontSize(10);
                    table.Cell().Row(currentRow).Column(6).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignLeft().Text(row.CustomerName).FontSize(10);
                    table.Cell().Row(currentRow).Column(7).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignLeft().Text(row.Branch).FontSize(10);
                    table.Cell().Row(currentRow).Column(8).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.TransType).FontSize(10);
                    table.Cell().Row(currentRow).Column(9).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.RefNo).FontSize(10);
                    table.Cell().Row(currentRow).Column(10).Border(1).BorderColor(Colors.Blue.Lighten3).Padding(5).AlignCenter().Text(row.Date).FontSize(10);
                    currentRow++;

                    if (row.HasItems)
                    {
                        table.Cell().Row(currentRow).Column(1).ColumnSpan(10).PaddingBottom(10).PaddingRight(40).PaddingLeft(10).PaddingTop(5).Element(c =>
                        {
                            c.Table(innerTable =>
                            {
                                innerTable.ColumnsDefinition(innerCols =>
                                {
                                    innerCols.RelativeColumn(1); // 1 إجمالي
                                    innerCols.RelativeColumn(1); // 2 سعر الوحدة
                                    innerCols.RelativeColumn(1); // 3 الكمية
                                    innerCols.RelativeColumn(3); // 4 الصنف / الموديل
                                });

                                uint innerRow = 1;
                                string[] innerHeaders = { "إجمالي", "سعر الوحدة", "الكمية", "الصنف / الموديل" };
                                for (uint i = 0; i < innerHeaders.Length; i++)
                                {
                                    innerTable.Cell().Row(innerRow).Column(i + 1).Background("#FEF3C7").Border(1).BorderColor(Colors.Orange.Lighten2).Padding(4).AlignCenter().Text(innerHeaders[i]).SemiBold().FontSize(10).FontColor("#92400E");
                                }
                                innerRow++;

                                foreach (var item in row.Items)
                                {
                                    innerTable.Cell().Row(innerRow).Column(1).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Total > 0 ? item.Total.ToString("N2") : "0").FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(2).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Price > 0 ? item.Price.ToString("N2") : "0").FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(3).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignCenter().Text(item.Qty > 0 ? item.Qty.ToString("N2") : "0").FontSize(9);
                                    innerTable.Cell().Row(innerRow).Column(4).Border(1).BorderColor(Colors.Orange.Lighten3).Padding(4).AlignLeft().Text(item.ItemName).FontSize(9);
                                    innerRow++;
                                }
                            });
                        });
                        currentRow++;
                    }
                }
            });

            if (footerTotals != null && footerTotals.Count > 0)
            {
                colContainer.Item().PaddingTop(5).Padding(10).Row(r =>
                {
                    foreach (var kv in footerTotals.Reverse())
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(11).FontColor("#334155");

                            var valueContainer = c.Item().PaddingTop(2).MaxWidth(120).AlignCenter();

                            bool isNumericOrDate = kv.Value?.Any(ch => char.IsDigit(ch) && !char.IsLetter(ch)) ?? false;
                            var textElement = valueContainer
                                .Border(0.5f)
                                .BorderColor(Colors.Black)
                                .CornerRadius(15) 
                                .PaddingVertical(4)
                                .PaddingHorizontal(12)
                                .AlignCenter()
                                .Text(kv.Value)
                                .FontSize(12)
                                .FontColor("#1E293B");

                            if (isNumericOrDate && !string.IsNullOrWhiteSpace(kv.Value))
                                textElement.DirectionFromLeftToRight();
                        });
                    }
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        string purpleColor = "#000000";

        container.Column(col =>
        {
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft().Text($"Print Time : {DateTime.Now:dd-MM-yyyy hh:mm:ss tt}")
                    .FontSize(8).FontColor(purpleColor).DirectionFromLeftToRight();

                row.RelativeItem(2).AlignCenter().Text("Mazaya For Programing 01118152828   01000503370")
                    .FontSize(8).FontColor(purpleColor).DirectionFromLeftToRight();

                row.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(10).FontColor(purpleColor).DirectionFromLeftToRight()).Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            });
        });
    }

    public static void PrintPdf(string filePath)
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    Verb = "print"
                }
            };
            process.Start();
        }
        catch
        {
            try
            {
                // Fallback: Just open the file and let the user print it manually
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        UseShellExecute = true
                    }
                };
                process.Start();
            }
            catch (Exception exInner)
            {
                System.Windows.MessageBox.Show("عذراً، لا يمكن الطباعة التلقائية أو فتح الملف المصدر يدوياً.\nالخطأ: " + exInner.Message, "خطأ في الطباعة", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}