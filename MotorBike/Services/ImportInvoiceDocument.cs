using System;
using System.Collections.Generic;
using System.Linq;
using MotorBike.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace MotorBike.Services;

public class ImportInvoiceItemModel
{
    public string ItemName { get; set; } = string.Empty;
    public double Qty { get; set; }
    public double Price { get; set; }
    public double Total { get; set; }   // بالعملة
    public double TotalLocal { get; set; }   // بالمحلي
    public decimal CostPer { get; set; }   // النسبة %
    public double ExpShareLocal { get; set; }  // تحمّل مصاريف
    public double CostTotal { get; set; }   // الإجمالي النهائي
    public double CostUnit { get; set; }   // تكلفة الوحدة
}

public class ImportInvoiceCarModel
{
    public string ChassisNo { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string ColorName { get; set; } = string.Empty;
    public double Total { get; set; }
    public double TotalLocal { get; set; }
    public decimal CostPer { get; set; }
    public double ExpShareLocal { get; set; }
    public double CostTotal { get; set; }
}

public class ImportInvoiceExpModel
{
    public string ExpName { get; set; } = string.Empty;
    public string CashName { get; set; } = string.Empty;
    public double PayTotal { get; set; }
    public DateTime PayDate { get; set; }
}

public class ImportInvoicePaymentModel
{
    public double PayMoney { get; set; }
    public double OmlaRate { get; set; }
    public string CashName { get; set; } = string.Empty;
    public DateTime PayDate { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class ImportInvoiceModel
{
    // ── Header info ──────────────────────────────────────────────
    public string InvoiceNo { get; set; } = string.Empty;
    public string InvName { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string MadeIn { get; set; } = string.Empty;
    public string ShipPort { get; set; } = string.Empty;
    public string InvType { get; set; } = "FOB";   // FOB | CIF
    public string OmlaName { get; set; } = string.Empty;
    public string OmlaRate { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    // ── Sub-collections ──────────────────────────────────────────
    public List<ImportInvoiceItemModel> Items { get; set; } = new();
    public List<ImportInvoiceCarModel> Cars { get; set; } = new();
    public List<ImportInvoiceExpModel> Expenses { get; set; } = new();
    public List<ImportInvoicePaymentModel> Payments { get; set; } = new();

    // ── Totals ───────────────────────────────────────────────────
    public double InvTotal { get; set; }   // إجمالي الفاتورة (بالعملة)
    public double InvTotalLocal { get; set; }   // إجمالي الفاتورة (بالمحلي)
    public double ExpTotal { get; set; }   // إجمالي المصروفات
    public double FrokOmla { get; set; }   // فرق العملات
    public double TotalCost { get; set; }   // التكلفة الإجمالية
}

// ════════════════════════════════════════════════════════════════
//  QuestPDF Document
// ════════════════════════════════════════════════════════════════

public class ImportInvoiceDocument : IDocument
{
    private readonly ImportInvoiceModel _model;
    private readonly Company _company;

    // Section header colors
    private const string ClrItems = "#1E3A5F";
    private const string ClrCars = "#4C1D95";
    private const string ClrExp = "#92400E";
    private const string ClrPay = "#064E3B";

    public ImportInvoiceDocument(ImportInvoiceModel model, Company company)
    {
        _model = model;
        _company = company;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    // ── Entry point ──────────────────────────────────────────────
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.2f, QuestPDF.Infrastructure.Unit.Centimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(x =>
                x.FontFamily("Arial").FontSize(9).DirectionFromRightToLeft());

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ════════════════════════════════════════════════════════════
    //  HEADER
    // ════════════════════════════════════════════════════════════
    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            // ── Company row: English LEFT | Logo CENTER | Arabic RIGHT ──
            column.Item().Row(row =>
            {
                // Left: English info
                row.RelativeItem().AlignLeft().Column(col =>
                {
                    col.Item().Text(_company?.NameEn ?? "Company Name")
                        .FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2)
                        .DirectionFromLeftToRight();

                    if (!string.IsNullOrEmpty(_company?.AdressEn))
                        col.Item().Text(_company.AdressEn)
                            .FontSize(9).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();

                    if (!string.IsNullOrEmpty(_company?.Whatsapp))
                        col.Item().Text($"Wa: {_company.Whatsapp}")
                            .FontSize(9).FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                });

                // Center: Logo
                row.ConstantItem(60).AlignCenter().AlignMiddle().Element(c =>
                {
                    if (_company?.Logo != null && _company.Logo.Length > 0)
                        c.Image(_company.Logo);
                    else
                        c.Text("شعار").FontSize(11).FontColor(Colors.Grey.Lighten1);
                });

                // Right: Arabic info
                row.RelativeItem().AlignRight().Column(col =>
                {
                    col.Item().Text(_company?.NameAr ?? "اسم الشركة")
                        .FontSize(14).SemiBold().FontColor(Colors.Blue.Darken2);

                    if (!string.IsNullOrEmpty(_company?.AdressAr))
                        col.Item().Text(_company.AdressAr)
                            .FontSize(9).FontColor(Colors.Grey.Medium);

                    if (!string.IsNullOrEmpty(_company?.Tel))
                        col.Item().Row(r =>
                        {
                            r.AutoItem().PaddingRight(3)
                                .Text(_company.Tel).FontSize(9)
                                .FontColor(Colors.Grey.Medium).DirectionFromLeftToRight();
                            r.AutoItem().Text(": ت ")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                        });
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

            // ── Title row + FOB/CIF badge ────────────────────────────
            column.Item().PaddingTop(6).Row(titleRow =>
            {
                // Left: badge
                titleRow.RelativeItem().AlignLeft().Element(c =>
                {
                    bool isFob = _model.InvType == "FOB";
                    string bgClr = isFob ? "#2563EB" : "#7C3AED";
                    c.Background(bgClr).CornerRadius(10)
                     .PaddingVertical(3).PaddingHorizontal(12)
                     .Text(_model.InvType).FontSize(11).Bold().FontColor(Colors.White);
                });

                // Center: title
                titleRow.RelativeItem(3).AlignCenter()
                    .Text("فاتورة الاستيراد")
                    .FontSize(17).SemiBold().FontColor(Colors.Black);

                // Right: spacer
                titleRow.RelativeItem();
            });

            // ── Meta fields ──────────────────────────────────────────
            column.Item().PaddingTop(10).Element(ComposeMetaFields);
            column.Item().PaddingBottom(6);
        });
    }

    private void ComposeMetaFields(IContainer container)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("رقم الفاتورة", _model.InvoiceNo),
            new("التاريخ",       _model.IssueDate),
            new("اسم الشحنة",   _model.InvName),
            new("المورد",        _model.SupplierName),
        };

        if (!string.IsNullOrWhiteSpace(_model.MadeIn))
            fields.Add(new("بلد المنشأ", _model.MadeIn));
        if (!string.IsNullOrWhiteSpace(_model.ShipPort))
            fields.Add(new("ميناء الشحن", _model.ShipPort));

        fields.Add(new("العملة", _model.OmlaName));
        fields.Add(new("سعر الصرف", _model.OmlaRate));

        if (!string.IsNullOrWhiteSpace(_model.Notes))
            fields.Add(new("ملاحظات", _model.Notes));

        // Render in rows of 4
        const int perRow = 4;
        container.Column(col =>
        {
            foreach (var rowFields in fields.Chunk(perRow))
            {
                col.Item().PaddingBottom(6).Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        for (int i = 0; i < perRow; i++) def.RelativeColumn();
                    });

                    foreach (var field in rowFields)
                    {
                        var f = field;
                        table.Cell().Padding(4).Element(c => RenderMetaField(c, f));
                    }
                    // Fill empty cells if row is not full
                    for (int i = rowFields.Length; i < perRow; i++)
                        table.Cell();
                });
            }
        });
    }

    private static void RenderMetaField(IContainer container, KeyValuePair<string, string> item)
    {
        container.Column(col =>
        {
            col.Item().AlignCenter()
                .Text(" : " + item.Key)
                .SemiBold().FontSize(9).FontColor(Colors.Blue.Darken4);

            bool isLtr = !(item.Value?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false)
                         && !string.IsNullOrWhiteSpace(item.Value);

            var cell = col.Item().PaddingTop(2)
                .BorderBottom(1).BorderColor(Colors.Teal.Darken2)
                .AlignCenter();

            if (isLtr)
                cell.Text(item.Value ?? "").FontSize(9).FontColor(Colors.Black).SemiBold().DirectionFromLeftToRight();
            else
                cell.Text(item.Value ?? "").FontSize(9).FontColor(Colors.Black).SemiBold();
        });
    }

    // ════════════════════════════════════════════════════════════
    //  CONTENT
    // ════════════════════════════════════════════════════════════
    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            if (_model.Items.Any())
            {
                col.Item().Element(ComposeItemsSection);
                col.Item().PaddingTop(10);
            }

            if (_model.Cars.Any())
            {
                col.Item().Element(ComposeCarsSection);
                col.Item().PaddingTop(10);
            }

            if (_model.Expenses.Any())
            {
                col.Item().Element(ComposeExpensesSection);
                col.Item().PaddingTop(10);
            }

            if (_model.Payments.Any())
            {
                col.Item().Element(ComposePaymentsSection);
                col.Item().PaddingTop(10);
            }

            col.Item().Element(ComposeTotals);
        });
    }

    // ── Shared: section title bar ────────────────────────────────
    private static void SectionHeader(IContainer c, string title, string bgColor)
    {
        c.Background(bgColor).CornerRadius(4).Padding(6)
         .Text(title).FontSize(11).Bold().FontColor(Colors.White).AlignCenter();
    }

    // ── Shared: table data cell ──────────────────────────────────
    private static void DataCell(
        TableDescriptor table,
        string val,
        string rowBg,
        bool ltr,
        string? textColor = null,
        bool bold = false,
        int fontSize = 8)
    {
        var cell = table.Cell()
            .Background(rowBg)
            .Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(4).AlignCenter();

        bool containsAr = val?.Any(c => c >= 0x0600 && c <= 0x06FF) ?? false;
        string color = textColor ?? Colors.Black;

        if (ltr || (!containsAr && !string.IsNullOrWhiteSpace(val)))
        {
            if (bold) cell.Text(val ?? "").FontSize(fontSize).FontColor(color).SemiBold().DirectionFromLeftToRight();
            else cell.Text(val ?? "").FontSize(fontSize).FontColor(color).DirectionFromLeftToRight();
        }
        else
        {
            if (bold) cell.Text(val ?? "").FontSize(fontSize).FontColor(color).SemiBold();
            else cell.Text(val ?? "").FontSize(fontSize).FontColor(color);
        }
    }

    // ── Shared: table header cell ────────────────────────────────
    private static void HeaderCell(TableDescriptor table, string title)
    {
        table.Cell()
            .Background("#F1F5F9")
            .Border(1).BorderColor(Colors.Grey.Medium)
            .Padding(4).AlignCenter()
            .Text(title).SemiBold().FontSize(8).FontColor("#1E293B");
    }

    // ════════════════════════════════════════════════════════════
    //  ITEMS TABLE
    // ════════════════════════════════════════════════════════════
    private void ComposeItemsSection(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Element(c => SectionHeader(c, "📦  الأصناف المستوردة", ClrItems));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    // Reversed widths
                    def.RelativeColumn(1f);     // تكلفة الوحدة
                    def.RelativeColumn(1f);     // الإجمالي النهائي
                    def.RelativeColumn(1f);     // مصاريف محملة
                    def.RelativeColumn(0.8f);   // النسبة %
                    def.RelativeColumn(1f);     // إجمالي (محلي)
                    def.RelativeColumn(1f);     // إجمالي (عملة)
                    def.RelativeColumn(0.9f);   // السعر
                    def.RelativeColumn(0.7f);   // الكمية
                    def.RelativeColumn(2.5f);   // الصنف
                    def.RelativeColumn(0.5f);   // م
                });

                // Reversed Headers
                var headers = new[] { "تكلفة الوحدة", "الإجمالي النهائي", "مصاريف محملة", "النسبة %", "إجمالي (محلي)", "إجمالي (عملة)", "السعر", "الكمية", "الصنف", "م" };
                foreach (var h in headers) HeaderCell(table, h);

                int idx = 1;
                foreach (var item in _model.Items)
                {
                    string bg = idx % 2 == 0 ? "#F8FAFC" : Colors.White;

                    // Add cells in reversed order
                    DataCell(table, item.CostUnit.ToString("N2"), bg, ltr: true, textColor: "#0284C7", bold: true);
                    DataCell(table, item.CostTotal.ToString("N2"), bg, ltr: true, textColor: "#059669", bold: true);
                    DataCell(table, item.ExpShareLocal.ToString("N2"), bg, ltr: true, textColor: "#DC2626");
                    DataCell(table, item.CostPer.ToString("N4") + " %", bg, ltr: true, textColor: "#6366F1");
                    DataCell(table, item.TotalLocal.ToString("N2"), bg, ltr: true);
                    DataCell(table, item.Total.ToString("N2"), bg, ltr: true);
                    DataCell(table, item.Price.ToString("N2"), bg, ltr: true);
                    DataCell(table, item.Qty.ToString("N0"), bg, ltr: true);
                    DataCell(table, item.ItemName, bg, ltr: false);
                    DataCell(table, (idx++).ToString(), bg, ltr: true);
                }

                // Items subtotal row (Reversed)
                if (_model.Items.Count > 1)
                {
                    const string ftBg = "#EFF6FF";
                    DataCell(table, "", ftBg, ltr: true); // Unit cost total (empty)
                    DataCell(table, _model.Items.Sum(x => x.CostTotal).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#059669");
                    DataCell(table, _model.Items.Sum(x => x.ExpShareLocal).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#DC2626");
                    DataCell(table, _model.Items.Sum(x => (double)x.CostPer).ToString("N4") + " %", ftBg, ltr: true, bold: true, textColor: "#6366F1");
                    DataCell(table, _model.Items.Sum(x => x.TotalLocal).ToString("N2"), ftBg, ltr: true, bold: true);
                    DataCell(table, _model.Items.Sum(x => x.Total).ToString("N2"), ftBg, ltr: true, bold: true);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "الإجمالي", ftBg, ltr: false, bold: true, fontSize: 9);
                    DataCell(table, "", ftBg, ltr: true);
                }
            });
        });
    }

    // ════════════════════════════════════════════════════════════
    //  CARS TABLE
    // ════════════════════════════════════════════════════════════
    private void ComposeCarsSection(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Element(c => SectionHeader(c, "🏍️  الموتوسيكلات", ClrCars));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    def.RelativeColumn(1f);     // الإجمالي النهائي
                    def.RelativeColumn(1f);     // مصاريف محملة
                    def.RelativeColumn(0.8f);   // النسبة %
                    def.RelativeColumn(1f);     // السعر (محلي)
                    def.RelativeColumn(1f);     // السعر (عملة)
                    def.RelativeColumn(1f);     // اللون
                    def.RelativeColumn(2f);     // الموديل
                    def.RelativeColumn(2.5f);   // رقم الشاسيه
                    def.RelativeColumn(0.5f);   // م
                });

                var headers = new[] { "الإجمالي النهائي", "مصاريف محملة", "النسبة %", "السعر (محلي)", "السعر (عملة)", "اللون", "الموديل", "رقم الشاسيه", "م" };
                foreach (var h in headers) HeaderCell(table, h);

                int idx = 1;
                foreach (var car in _model.Cars)
                {
                    string bg = idx % 2 == 0 ? "#FAF5FF" : Colors.White;
                    DataCell(table, car.CostTotal.ToString("N2"), bg, ltr: true, textColor: "#059669", bold: true);
                    DataCell(table, car.ExpShareLocal.ToString("N2"), bg, ltr: true, textColor: "#DC2626");
                    DataCell(table, car.CostPer.ToString("N4") + " %", bg, ltr: true, textColor: "#6366F1");
                    DataCell(table, car.TotalLocal.ToString("N2"), bg, ltr: true);
                    DataCell(table, car.Total.ToString("N2"), bg, ltr: true);
                    DataCell(table, car.ColorName, bg, ltr: false);
                    DataCell(table, car.ModelName, bg, ltr: false);
                    DataCell(table, car.ChassisNo, bg, ltr: true);
                    DataCell(table, (idx++).ToString(), bg, ltr: true);
                }

                if (_model.Cars.Count > 1)
                {
                    const string ftBg = "#F5F3FF";
                    DataCell(table, _model.Cars.Sum(x => x.CostTotal).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#059669");
                    DataCell(table, _model.Cars.Sum(x => x.ExpShareLocal).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#DC2626");
                    DataCell(table, _model.Cars.Sum(x => (double)x.CostPer).ToString("N4") + " %", ftBg, ltr: true, bold: true, textColor: "#6366F1");
                    DataCell(table, _model.Cars.Sum(x => x.TotalLocal).ToString("N2"), ftBg, ltr: true, bold: true);
                    DataCell(table, _model.Cars.Sum(x => x.Total).ToString("N2"), ftBg, ltr: true, bold: true);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "الإجمالي", ftBg, ltr: false, bold: true, fontSize: 9);
                    DataCell(table, "", ftBg, ltr: true);
                }
            });
        });
    }

    // ════════════════════════════════════════════════════════════
    //  EXPENSES TABLE
    // ════════════════════════════════════════════════════════════
    private void ComposeExpensesSection(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Element(c => SectionHeader(c, "🧾  المصروفات الجمركية", ClrExp));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    // Reversed order
                    def.RelativeColumn(1.5f);   // تاريخ الدفع
                    def.RelativeColumn(1.5f);   // المبلغ المدفوع
                    def.RelativeColumn(2.5f);   // الخزينة
                    def.RelativeColumn(3f);     // بند المصروف
                    def.RelativeColumn(0.5f);   // م
                });

                var headers = new[] { "تاريخ الدفع", "المبلغ المدفوع", "الخزينة", "بند المصروف", "م" };
                foreach (var h in headers) HeaderCell(table, h);

                int idx = 1;
                foreach (var exp in _model.Expenses)
                {
                    string bg = idx % 2 == 0 ? "#FFFBEB" : Colors.White;

                    DataCell(table, exp.PayDate.ToString("yyyy/MM/dd"), bg, ltr: true);
                    DataCell(table, exp.PayTotal.ToString("N2"), bg, ltr: true, bold: true, textColor: "#92400E");
                    DataCell(table, exp.CashName, bg, ltr: false);
                    DataCell(table, exp.ExpName, bg, ltr: false);
                    DataCell(table, (idx++).ToString(), bg, ltr: true);
                }

                if (_model.Expenses.Count > 0)
                {
                    const string ftBg = "#FEF3C7";
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, _model.Expenses.Sum(x => x.PayTotal).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#92400E");
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "الإجمالي", ftBg, ltr: false, bold: true, fontSize: 9);
                    DataCell(table, "", ftBg, ltr: true);
                }
            });
        });
    }

    // ════════════════════════════════════════════════════════════
    //  PAYMENTS TABLE
    // ════════════════════════════════════════════════════════════
    private void ComposePaymentsSection(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().PaddingBottom(6).Element(c => SectionHeader(c, "💰  الدفعات للمورد", ClrPay));

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(def =>
                {
                    // Reversed order
                    def.RelativeColumn(3f);     // ملاحظات
                    def.RelativeColumn(1.5f);   // تاريخ الدفع
                    def.RelativeColumn(2.5f);   // الخزينة
                    def.RelativeColumn(1.2f);   // سعر الصرف
                    def.RelativeColumn(1.5f);   // المبلغ
                    def.RelativeColumn(0.5f);   // م
                });

                var headers = new[] { "ملاحظات", "تاريخ الدفع", "الخزينة", "سعر الصرف", "المبلغ", "م" };
                foreach (var h in headers) HeaderCell(table, h);

                int idx = 1;
                foreach (var pay in _model.Payments)
                {
                    string bg = idx % 2 == 0 ? "#F0FDF4" : Colors.White;

                    DataCell(table, pay.Notes, bg, ltr: false);
                    DataCell(table, pay.PayDate.ToString("yyyy/MM/dd"), bg, ltr: true);
                    DataCell(table, pay.CashName, bg, ltr: false);
                    DataCell(table, pay.OmlaRate.ToString("N4"), bg, ltr: true, textColor: "#6366F1");
                    DataCell(table, pay.PayMoney.ToString("N2"), bg, ltr: true, bold: true, textColor: "#064E3B");
                    DataCell(table, (idx++).ToString(), bg, ltr: true);
                }

                if (_model.Payments.Count > 0)
                {
                    const string ftBg = "#DCFCE7";
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, "إجمالي المدفوع", ftBg, ltr: false, bold: true, fontSize: 9);
                    DataCell(table, "", ftBg, ltr: true);
                    DataCell(table, _model.Payments.Sum(x => x.PayMoney).ToString("N2"), ftBg, ltr: true, bold: true, textColor: "#064E3B");
                    DataCell(table, "", ftBg, ltr: true);
                }
            });
        });
    }

    // ════════════════════════════════════════════════════════════
    //  TOTALS SUMMARY
    // ════════════════════════════════════════════════════════════
    private void ComposeTotals(IContainer container)
    {
        // (Label, Value, Color)
        var totals = new List<(string Label, string Value, string Color)>
        {
            ("إجمالي الفاتورة (بالعملة)",  _model.InvTotal.ToString("N2"),      "#1D4ED8"),
            ("إجمالي الفاتورة (بالمحلي)",  _model.InvTotalLocal.ToString("N2"), "#1D4ED8"),
            ("إجمالي المصروفات (محلي)",    _model.ExpTotal.ToString("N2"),      "#DC2626"),
            ("فرق العملات",                _model.FrokOmla.ToString("N2"),      _model.FrokOmla >= 0 ? "#059669" : "#DC2626"),
            ("التكلفة الإجمالية (محلي)",   _model.TotalCost.ToString("N2"),     "#059669"),
        };

        container
            .Background("#F8FAFC")
            .CornerRadius(8)
            .Border(1).BorderColor("#C7D2FE")
            .Padding(14)
            .Column(col =>
            {
                col.Item().PaddingBottom(10)
                    .Text("ملخص التكاليف")
                    .FontSize(12).Bold().FontColor("#1E293B");

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(def =>
                    {
                        foreach (var _ in totals) def.RelativeColumn();
                    });

                    // Labels
                    foreach (var (label, _, _) in totals)
                    {
                        table.Cell().Padding(5).AlignCenter()
                            .Text(label).SemiBold().FontSize(9).FontColor("#334155");
                    }

                    // Values
                    foreach (var (_, value, color) in totals)
                    {
                        table.Cell().Padding(4)
                            .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .CornerRadius(6).Background(Colors.White)
                            .AlignCenter()
                            .Text(value).FontSize(13).SemiBold()
                            .FontColor(color).DirectionFromLeftToRight();
                    }
                });

                // Final cost highlight
                col.Item().PaddingTop(10)
                    .Background("#EEF2FF").CornerRadius(6)
                    .Padding(10)
                    .Row(row =>
                    {
                        row.AutoItem().Text("التكلفة الإجمالية النهائية بالمحلي: ")
                            .FontSize(13).SemiBold().FontColor("#3730A3");
                        row.AutoItem().Text(_model.TotalCost.ToString("N2"))
                            .FontSize(15).Bold().FontColor("#059669").DirectionFromLeftToRight();
                    });
            });
    }

    // ════════════════════════════════════════════════════════════
    //  FOOTER
    // ════════════════════════════════════════════════════════════
    private static void ComposeFooter(IContainer container)
    {
        const string clr = "#000000";
        container.Column(col =>
        {
            col.Item().PaddingBottom(5).LineHorizontal(1).LineColor(Colors.Grey.Medium);
            col.Item().Row(row =>
            {
                row.RelativeItem().AlignLeft()
                    .Text($"Print Time : {DateTime.Now:dd-MM-yyyy hh:mm:ss tt}")
                    .FontSize(7).FontColor(clr).DirectionFromLeftToRight();

                row.RelativeItem(2).AlignCenter()
                    .Text("Mazaya For Programing  01118152828   01000503370")
                    .FontSize(7).FontColor(clr).DirectionFromLeftToRight();

                row.RelativeItem().AlignRight()
                    .DefaultTextStyle(x => x.FontSize(9).FontColor(clr).DirectionFromLeftToRight())
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