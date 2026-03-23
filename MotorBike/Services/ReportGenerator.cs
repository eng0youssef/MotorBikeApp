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
        // Require QuestPDF license configuration
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

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.PageColor(Colors.White);
                
                // Using a safe Arabic supporting font, default to Arial
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
                // Right part: Company Info in Arabic
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text(company?.NameAr ?? "اسم الشركة").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2);
                    if (!string.IsNullOrEmpty(company?.AdressAr))
                        col.Item().Text(company.AdressAr).FontSize(12).FontColor(Colors.Grey.Medium);
                    if (!string.IsNullOrEmpty(company?.Tel))
                        col.Item().Text($"ت: {company.Tel}").FontSize(12).FontColor(Colors.Grey.Medium);
                });

                // Center part: Logo
                row.ConstantItem(100).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (company?.Logo != null && company.Logo.Length > 0)
                    {
                        c.Image(company.Logo);
                    }
                    else
                    {
                        c.Text("شعار").FontSize(14).FontColor(Colors.Grey.Lighten1);
                    }
                });

                // Left part: Company Info in English
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(company?.NameEn ?? "Company Name").FontSize(18).SemiBold().FontColor(Colors.Blue.Darken2).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(company?.AdressEn))
                        col.Item().Text(company.AdressEn).FontSize(12).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                    if (!string.IsNullOrEmpty(company?.Whatsapp))
                        col.Item().Text($"Wa: {company.Whatsapp}").FontSize(12).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                });
            });

            column.Item().PaddingTop(15).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().PaddingTop(10).AlignCenter().Text(reportTitle)
                .FontSize(20).SemiBold().FontColor(Colors.Black);
                
            if (headerInfo != null && headerInfo.Count > 0)
            {
                column.Item().PaddingTop(10).PaddingBottom(10).Column(col => 
                {
                    var items = headerInfo.ToList();
                    for(int i = 0; i < items.Count; i+=2)
                    {
                         col.Item().PaddingBottom(8).AlignCenter().Row(r => 
                         {
                             // Right side item (First in RTL)
                             r.AutoItem().Row(innerR => 
                             {
                                 innerR.AutoItem().PaddingLeft(5).Text(items[i].Key + " : ").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken4);
                                 var targetText1 = innerR.ConstantItem(120).BorderBottom(1).BorderColor(Colors.Teal.Darken2)
                                     .AlignCenter().Text(items[i].Value).FontSize(12).FontColor(Colors.Black).SemiBold();
                                     
                                 if (items[i].Value != null && items[i].Value.Any(c => char.IsDigit(c) && !char.IsLetter(c)) && !items[i].Value.Any(c => c >= 0x0600 && c <= 0x06FF))
                                     targetText1.DirectionFromLeftToRight();
                             });

                             // Left side item (Second in RTL) if exists
                             if(i + 1 < items.Count)
                             {
                                 r.ConstantItem(40); // Spacer
                                 r.AutoItem().Row(innerR => 
                                 {
                                     innerR.AutoItem().PaddingLeft(5).Text(items[i+1].Key + " : ").SemiBold().FontSize(12).FontColor(Colors.Blue.Darken4);
                                     var targetText2 = innerR.ConstantItem(120).BorderBottom(1).BorderColor(Colors.Teal.Darken2)
                                         .AlignCenter().Text(items[i+1].Value).FontSize(12).FontColor(Colors.Black).SemiBold();
                                         
                                     if (items[i+1].Value != null && items[i+1].Value.Any(c => char.IsDigit(c) && !char.IsLetter(c)) && !items[i+1].Value.Any(c => c >= 0x0600 && c <= 0x06FF))
                                         targetText2.DirectionFromLeftToRight();
                                 });
                             }
                         });
                    }
                });
            }

            column.Item().PaddingBottom(15);
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

                // Header Row
                table.Header(header =>
                {
                    foreach (var col in columns)
                    {
                        header.Cell().Background("#F1F5F9").BorderBottom(1).BorderColor(Colors.Grey.Lighten2)
                            .Padding(5).AlignCenter().Text(col).SemiBold();
                    }
                });

                // Data Rows
                foreach (System.Data.DataRowView rowView in dataView)
                {
                    var row = rowView.Row;
                    foreach (var col in columns)
                    {
                        var val = row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";
                        var cell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(5).AlignCenter();
                        
                        // If the text does not contain Arabic characters (is mostly numbers/dates/English), force LTR to avoid reversing like 2026-03 -> 30-6202
                        bool containsArabic = val?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;
                        
                        if (!containsArabic && !string.IsNullOrWhiteSpace(val))
                        {
                            cell.Text(val).FontSize(10).DirectionFromLeftToRight();
                        }
                        else
                        {
                            cell.Text(val).FontSize(10);
                        }
                    }
                }
            });

            if (footerTotals != null && footerTotals.Count > 0)
            {
                colContainer.Item().PaddingTop(15).Background("#F1F5F9").Padding(10).Border(1).BorderColor(Colors.Grey.Lighten2).Row(r =>
                {
                    foreach (var kv in footerTotals)
                    {
                        r.RelativeItem().Column(c => 
                        {
                            c.Item().AlignCenter().Text(kv.Key).SemiBold().FontSize(12).FontColor("#334155");
                            
                            bool isNumericOrDate = kv.Value?.Any(ch => char.IsDigit(ch) && !char.IsLetter(ch)) ?? false;
                            if (isNumericOrDate && !string.IsNullOrWhiteSpace(kv.Value))
                                c.Item().AlignCenter().Text(kv.Value).FontSize(13).FontColor("#1E293B").DirectionFromLeftToRight();
                            else
                                c.Item().AlignCenter().Text(kv.Value).FontSize(13).FontColor("#1E293B");
                        });
                    }
                });
            }
        });
    }

    private static void ComposeFooter(IContainer container)
    {
        // Custom purple color as requested
        string purpleColor = "#000000";

        container.Column(col =>
        {
            // Separator Line
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            col.Item().Row(row =>
            {
                // Left: Print Time
                row.RelativeItem().AlignLeft().Text($"Print Time : {DateTime.Now:dd-MM-yyyy hh:mm:ss tt}")
                    .FontSize(8).FontColor(purpleColor).DirectionFromLeftToRight();

                // Center: Mazaya For Programing
                row.RelativeItem(2).AlignCenter().Text("Mazaya For Programing 01118152828   01000503370")
                    .FontSize(8).FontColor(purpleColor).DirectionFromLeftToRight();

                // Right: Page Number
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
