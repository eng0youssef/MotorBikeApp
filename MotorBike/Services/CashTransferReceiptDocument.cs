using System;
using System.Collections.Generic;
using System.Linq;
using MotorBike.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorBike.Services;

public class CashTransferReceiptModel
{
    public string ReceiptNo { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;

    // ── Currency info ─────────────────────────────────────────────────────────
    public string CurrencyName { get; set; } = "جنية مصري";
    public string ToCurrencyName { get; set; } = "جنية مصري";
    public double ExchangeRate { get; set; } = 1.0;
    public double FromRate { get; set; } = 1.0;
    public double ToRate { get; set; } = 1.0;
    public double AmountInLocalCurrency { get; set; }   // المبلغ للمحول اليه

    // ── Cash names ────────────────────────────────────────────────────────────
    public string FromCashName { get; set; } = string.Empty;
    public string ToCashName { get; set; } = string.Empty;

    // ── Main amount ───────────────────────────────────────────────────────────
    public double Amount { get; set; }

    public string Notes { get; set; } = string.Empty;

    // ── Balances ──────────────────────────────────────────────────────────────
    public double FromCashPreviousBalance { get; set; }
    public double FromCashBalanceAfter { get; set; }
    public double ToCashPreviousBalance { get; set; }
    public double ToCashBalanceAfter { get; set; }
}

public class CashTransferReceiptDocument : IDocument
{
    private readonly CashTransferReceiptModel _model;
    private readonly Company _company;

    public CashTransferReceiptDocument(CashTransferReceiptModel model, Company company)
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
            // Company row: English LEFT | Logo CENTER | Arabic RIGHT
            column.Item().Row(row =>
            {
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

                row.ConstantItem(65).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (_company?.Logo != null && _company.Logo.Length > 0)
                        c.Image(_company.Logo);
                    else
                        c.Text("شعار").FontSize(12).FontColor(Colors.Grey.Lighten1);
                });

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
                            r.AutoItem().PaddingRight(3).Text(_company.Tel)
                                .FontSize(10).FontColor(Colors.Grey.Medium)
                                .DirectionFromLeftToRight();
                            r.AutoItem().Text(": ت ")
                                .FontSize(10).FontColor(Colors.Grey.Medium);
                        });
                    }
                });
            });

            column.Item().PaddingTop(12).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            column.Item().PaddingTop(8).AlignCenter()
                .Text("إيصال تحويل نقدية")
                .FontSize(18).SemiBold().FontColor(Colors.Black);

            column.Item().PaddingBottom(8);
        });
    }

    // ── CONTENT ───────────────────────────────────────────────────────────────
    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            // ── Divider ───────────────────────────────────────────────────────
            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // ── Row 1: رقم | التاريخ ──────────────────────────────────────────
            col.Item().PaddingVertical(6).Row(row =>
            {


                // MIDDLE: التاريخ
                row.AutoItem().Text(t =>
                {
                    t.Span("التاريخ : ").SemiBold().FontSize(11);
                    t.Span(_model.IssueDate).FontSize(11).DirectionFromLeftToRight();
                });

                row.RelativeItem(); // spacer

                // RIGHT: رقم
                row.AutoItem().Text(t =>
                {
                    t.Span("رقم : ").SemiBold().FontSize(11);
                    t.Span(_model.ReceiptNo).FontSize(11)
                        .FontColor(Colors.Blue.Darken3).Bold();
                });
            });

            col.Item().LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // ── Row 2: المبلغ | سعر الصرف | الإيصال بقيمة ──────────────
            col.Item().PaddingVertical(8).Row(row =>
            {
                // RIGHT: الإيصال بقيمة (الخزينة الوجهة)
                row.AutoItem().PaddingLeft(20).Text(t =>
                {
                    t.Span("قيمة التحويل : ").SemiBold().FontSize(11).FontColor(Colors.Grey.Darken2);
                    t.Span(_model.AmountInLocalCurrency.ToString("N2"))
                        .FontSize(11).Bold().DirectionFromLeftToRight();
                    t.Span($" {_model.ToCurrencyName}").FontSize(11).FontColor(Colors.Grey.Darken2);
                });

                // MIDDLE: سعر الصرف (يظهر فقط لو العملتين مختلفتين)
                if (_model.CurrencyName != _model.ToCurrencyName)
                {
                    row.AutoItem().PaddingLeft(20).Text(t =>
                    {
                        bool showTo = _model.ToRate != 1.0 || _model.ToCurrencyName != "جنية مصري" && _model.ToCurrencyName != "ج.م";
                        bool showFrom = _model.FromRate != 1.0 || _model.CurrencyName != "جنية مصري" && _model.CurrencyName != "ج.م";

                        if (showFrom)
                        {
                            t.Span($"سعر ({_model.CurrencyName}) : ").SemiBold().FontSize(11).FontColor(Colors.Grey.Darken2);
                            t.Span(_model.FromRate.ToString("N2")).FontSize(11).DirectionFromLeftToRight();
                        }

                        if (showFrom && showTo)
                        {
                            t.Span("      ").FontSize(11); // مسافة فاصلة بين السعرين
                        }

                        if (showTo)
                        {
                            t.Span($"سعر ({_model.ToCurrencyName}) : ").SemiBold().FontSize(11).FontColor(Colors.Grey.Darken2);
                            t.Span(_model.ToRate.ToString("N2")).FontSize(11).DirectionFromLeftToRight();
                        }
                        
                    });
                }

                row.RelativeItem(); // spacer

                // LEFT: المبلغ الرئيسي (الخزينة المصدر)
                row.AutoItem().Text(t =>
                {
                    t.Span("المبلغ : ").SemiBold().FontSize(11);
                    t.Span(_model.Amount.ToString("N2"))
                        .FontSize(11).Bold().FontColor(Colors.Black)
                        .DirectionFromLeftToRight();
                    t.Span($" {_model.CurrencyName}").FontSize(11).FontColor(Colors.Grey.Darken2);
                });
            });

            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // ── Row 4: تحويل من ───────────────────────────────────────────────
            col.Item().PaddingVertical(10)
                .Element(c => ComposeCashRow(c, "تحويل من",
                    _model.FromCashName,
                    _model.FromCashPreviousBalance,
                    _model.FromCashBalanceAfter));

            col.Item().PaddingBottom(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten3);

            // ── Row 5: تحويل الي ──────────────────────────────────────────────
            col.Item().PaddingVertical(10)
                .Element(c => ComposeCashRow(c, "تحويل الي",
                    _model.ToCashName,
                    _model.ToCashPreviousBalance,
                    _model.ToCashBalanceAfter));

            col.Item().PaddingBottom(6).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);

            // ── Row 6: البيان ─────────────────────────────────────────────────
            col.Item().PaddingTop(10).Row(row =>
            {
                row.RelativeItem()
                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                    .PaddingBottom(2)
                    .AlignRight()
                    .Text(_model.Notes ?? "")
                    .FontSize(11);
                row.AutoItem().Text(": البيان  ").SemiBold().FontSize(11);

            });
        });
    }

    // ── Cash row: [label : name ________] [السابق pill] [الحالي pill] ─────────
    private static void ComposeCashRow(
        IContainer container,
        string label,
        string cashName,
        double prevBal,
        double currentBal)
    {
        container.Row(row =>
        {
           


            // LEFT: two balance pills (fixed width — no overflow)
            row.ConstantItem(230).Row(balRow =>
            {
                // الرصيد الحالي (left of the pair)
                balRow.RelativeItem().PaddingHorizontal(5).Column(c =>
                {
                    c.Item().AlignCenter()
                        .Text("الرصيد الحالي")
                        .FontSize(9).SemiBold().FontColor("#334155");

                    c.Item().PaddingTop(3)
                        .Border(0.5f).BorderColor(Colors.Teal.Darken3)
                        .CornerRadius(15)
                        .PaddingVertical(4).PaddingHorizontal(8)
                        .AlignCenter()
                        .Text(currentBal.ToString("N2"))
                        .FontSize(10).FontColor("#1E293B")
                        .DirectionFromLeftToRight();
                });
                // الرصيد السابق (right of the pair)
                balRow.RelativeItem().PaddingHorizontal(5).Column(c =>
                {
                    c.Item().AlignCenter()
                        .Text("الرصيد السابق")
                        .FontSize(9).SemiBold().FontColor("#334155");

                    c.Item().PaddingTop(3)
                        .Border(0.5f).BorderColor(Colors.Teal.Darken3)
                        .CornerRadius(15)
                        .PaddingVertical(4).PaddingHorizontal(8)
                        .AlignCenter()
                        .Text(prevBal.ToString("N2"))
                        .FontSize(10).FontColor("#1E293B")
                        .DirectionFromLeftToRight();
                });


            });

            // RIGHT: label + name with underline
            row.RelativeItem().AlignMiddle().Row(nameRow =>
            {


                nameRow.RelativeItem()
                    .BorderBottom(0.5f).BorderColor(Colors.Grey.Medium)
                    .PaddingBottom(2)
                    .AlignRight()
                    .Text(cashName)
                    .FontSize(12).FontColor(Colors.Black);

                nameRow.AutoItem()
                    .Text($": {label}  ")
                    .SemiBold().FontSize(12).FontColor(Colors.Black);
            });
        });
    }

    // ── FOOTER ────────────────────────────────────────────────────────────────
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

    // ── Arabic number-to-words helper ─────────────────────────────────────────
    public static string ToArabicWords(double amount)
    {
        long pounds = (long)Math.Floor(amount);
        int piasters = (int)Math.Round((amount - pounds) * 100);

        string result = WholeToArabic(pounds) + " جنية";
        if (piasters > 0)
            result += " و" + WholeToArabic(piasters) + " قرش";
        return result;
    }

    private static readonly string[] _ones =
    [
        "", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة",
        "ستة", "سبعة", "ثمانية", "تسعة", "عشرة",
        "أحد عشر", "اثنا عشر", "ثلاثة عشر", "أربعة عشر", "خمسة عشر",
        "ستة عشر", "سبعة عشر", "ثمانية عشر", "تسعة عشر"
    ];
    private static readonly string[] _tens =
    [
        "", "عشرة", "عشرون", "ثلاثون", "أربعون", "خمسون",
        "ستون", "سبعون", "ثمانون", "تسعون"
    ];
    private static readonly string[] _hundreds =
    [
        "", "مائة", "مئتان", "ثلاثمائة", "أربعمائة", "خمسمائة",
        "ستمائة", "سبعمائة", "ثمانمائة", "تسعمائة"
    ];

    private static string WholeToArabic(long n)
    {
        if (n == 0) return "صفر";
        if (n < 0) return "سالب " + WholeToArabic(-n);

        var parts = new List<string>();

        if (n >= 1_000_000_000)
        {
            parts.Add(WholeToArabic(n / 1_000_000_000) + " مليار");
            n %= 1_000_000_000;
        }
        if (n >= 1_000_000)
        {
            parts.Add(WholeToArabic(n / 1_000_000) + " مليون");
            n %= 1_000_000;
        }
        if (n >= 1_000)
        {
            long thousands = n / 1_000;
            parts.Add(thousands == 1 ? "ألف"
                    : thousands == 2 ? "ألفان"
                    : WholeToArabic(thousands) + " آلاف");
            n %= 1_000;
        }

        int h = (int)(n / 100);
        int rem = (int)(n % 100);

        if (h > 0) parts.Add(_hundreds[h]);

        if (rem > 0)
        {
            if (rem < 20)
            {
                parts.Add(_ones[rem]);
            }
            else
            {
                int t = rem / 10;
                int o = rem % 10;
                parts.Add(o > 0 ? _ones[o] + " و" + _tens[t] : _tens[t]);
            }
        }

        return string.Join(" و", parts);
    }
}