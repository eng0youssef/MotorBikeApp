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

    public static byte[] GeneratePdf(Company company, string reportTitle, System.Data.DataView dataView, Dictionary<string, string>? headerInfo = null, Dictionary<string, string>? footerTotals = null)
    {
        var dt = dataView.Table;
        var columns = new List<string>();
        foreach (System.Data.DataColumn col in dt.Columns)
        {
            columns.Add(col.ColumnName);
        }

        // Reverse columns to display them from Right-To-Left
        columns.Reverse();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(11).DirectionFromRightToLeft());

                page.Header().Element(c => ComposeHeader(c, company, reportTitle, headerInfo));
                page.Content().Element(c => ComposeContent(c, columns, dataView, footerTotals));
                page.Footer().Element(ComposeFooter);
            });
        });

        return document.GeneratePdf();
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
                column.Item().PaddingTop(10).PaddingBottom(10).Row(r =>
                {
                    // ── Spacer left edge ──────────────────────────────────
                    r.RelativeItem();

                    var items = headerInfo.ToList();
                    for (int i = items.Count - 1; i >= 0; i--)
                    {
                        r.AutoItem().Element(c => RenderHeaderField(c, items[i]));
                        
                        if (i > 0)
                        {
                            r.ConstantItem(20); // gap between fields
                        }
                    }

                    // ── Spacer right edge ─────────────────────────────────
                    r.RelativeItem();
                });
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

    private static void ComposeContent(IContainer container, List<string> columns, System.Data.DataView dataView, Dictionary<string, string>? footerTotals)
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
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show("عذراً، لا يمكن الطباعة التلقائية يرجى طباعة الملف المصدّر يدوياً.\nالخطأ: " + ex.Message, "خطأ في الطباعة", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}