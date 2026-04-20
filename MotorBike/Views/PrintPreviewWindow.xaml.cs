using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using QuestPDF.Infrastructure;

namespace MotorBike.Views;

/// <summary>
/// نافذة معاينة قبل الطباعة مع اختيار الطابعة والمقاس ونطاق الصفحات وحفظ PDF اختياري.
/// </summary>
public partial class PrintPreviewWindow : Window
{
    // ─── State ────────────────────────────────────────────────────────────
    private readonly IDocument _document;
    private List<BitmapImage> _renderedPages = [];   // صور الصفحات المولّدة
    private int _totalPages = 0;

    // ─── Paper sizes ──────────────────────────────────────────────────────
    private static readonly List<PaperSizeEntry> KnownPaperSizes =
    [
        new("A4",     210, 297),
        new("A5",     148, 210),
        new("Letter", 216, 279),
        new("Legal",  216, 356),
        new("A3",     297, 420),
    ];

    // ─── Constructor ──────────────────────────────────────────────────────
    public PrintPreviewWindow(IDocument document, string title = "معاينة البيان")
    {
        InitializeComponent();
        _document = document;
        Title     = title;

        Loaded += OnWindowLoaded;
    }

    // ─── Window Loaded ────────────────────────────────────────────────────

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadPrinters();
        await GenerateAndShowPreviewAsync();
    }

    // ─── Printer & Paper ──────────────────────────────────────────────────

    private void LoadPrinters()
    {
        try
        {
            using var server = new PrintServer();
            var queues = server.GetPrintQueues(
                [EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections]);
            var names = queues.Select(q => q.FullName).OrderBy(n => n).ToList();

            CmbPrinters.ItemsSource  = names;
            var defaultName          = LocalPrintServer.GetDefaultPrintQueue()?.FullName;
            CmbPrinters.SelectedItem = names.Contains(defaultName ?? "") ? defaultName : names.FirstOrDefault();
        }
        catch (Exception ex)
        {
            TxtError.Text = "تعذّر تحميل قائمة الطابعات: " + ex.Message;
        }
    }

    // ─── Preview Generation ───────────────────────────────────────────────

    private async System.Threading.Tasks.Task GenerateAndShowPreviewAsync()
    {
        try
        {
            LoadingOverlay.Visibility     = Visibility.Visible;
            TxtError.Text                 = "";
            PageImagesControl.ItemsSource = null;

            _renderedPages = await System.Threading.Tasks.Task.Run(RenderPageImages);

            _totalPages = _renderedPages.Count;
            TxtPageCount.Text       = $"إجمالي الصفحات: {_totalPages}";
            TxtPreviewPageInfo.Text = $"({_totalPages} صفحة)";

            PageImagesControl.ItemsSource = _renderedPages;
        }
        catch (Exception ex)
        {
            TxtError.Text = "خطأ في تحميل المعاينة: " + ex.Message;
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private List<BitmapImage> RenderPageImages()
    {
        var pageBytes = QuestPDF.Fluent.GenerateExtensions.GenerateImages(_document,
            new ImageGenerationSettings
            {
                ImageFormat             = ImageFormat.Png,
                ImageCompressionQuality = ImageCompressionQuality.High,
                RasterDpi               = 150
            });

        var result = new List<BitmapImage>();
        foreach (var bytes in pageBytes)
        {
            using var ms = new MemoryStream(bytes);
            var bmp       = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption  = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();   // مطلوب للاستخدام على UI thread
            result.Add(bmp);
        }
        return result;
    }

    // ─── Event Handlers ───────────────────────────────────────────────────

    private void CmbPrinters_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void CmbPaperSize_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    private void PageRange_Changed(object sender, RoutedEventArgs e)
    {
        if (PanelCustomRange == null) return;
        PanelCustomRange.Visibility = RbCustom.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TxtPageRange_TextChanged(object sender, TextChangedEventArgs e)
        => TxtError.Text = "";

    private void TxtCopies_PreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !int.TryParse(e.Text, out _);

    // ─── Print ────────────────────────────────────────────────────────────

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "";
        TxtError.Text  = "";

        if (_renderedPages.Count == 0)
        {
            TxtError.Text = "لا توجد صفحات للطباعة. انتظر انتهاء المعاينة.";
            return;
        }

        var printerName = CmbPrinters.SelectedItem as string;
        if (string.IsNullOrEmpty(printerName))
        {
            TxtError.Text = "يرجى اختيار طابعة.";
            return;
        }

        if (!int.TryParse(TxtCopies.Text, out int copies) || copies < 1)
            copies = 1;

        // تحليل نطاق الصفحات
        List<int>? pageList = null;
        if (RbCustom.IsChecked == true)
        {
            pageList = ParsePageRange(TxtPageRange.Text, _totalPages);
            if (pageList == null || pageList.Count == 0)
            {
                TxtError.Text = "نطاق الصفحات غير صحيح. مثال: 1-3, 5, 7";
                return;
            }
        }

        // اختيار الصفحات المطلوبة (0-indexed)
        var pagesToPrint = pageList != null
            ? pageList.Select(p => _renderedPages[p - 1]).ToList()
            : _renderedPages;

        try
        {
            BtnPrint.IsEnabled = false;

            // ── بناء PrintDialog مع الطابعة المختارة ──────────────────
            var pd = new PrintDialog();

            // تعيين الطابعة المختارة مباشرةً
            try
            {
                pd.PrintQueue = new PrintQueue(new PrintServer(), printerName);
            }
            catch
            {
                // إذا فشل الاتصال بالطابعة نجرّب local print server
                try { pd.PrintQueue = new PrintQueue(new LocalPrintServer(), printerName); }
                catch { /* سيبقى على الافتراضية */ }
            }

            pd.PrintTicket.CopyCount = copies;

            // ── بناء FixedDocument من صور الصفحات ────────────────────
            var doc = BuildFixedDocument(pagesToPrint, pd.PrintableAreaWidth, pd.PrintableAreaHeight);

            pd.PrintDocument(doc.DocumentPaginator, "بيان");

            var pageInfo = pageList != null
                ? $"الصفحات: {string.Join(", ", pageList)}"
                : "كل الصفحات";
            TxtStatus.Text = $"✅ تم الإرسال للطابعة «{printerName}» ({copies} نسخة — {pageInfo}).";
        }
        catch (Exception ex)
        {
            TxtError.Text = "خطأ أثناء الطباعة: " + ex.Message;
        }
        finally
        {
            BtnPrint.IsEnabled = true;
        }
    }

    /// <summary>بناء FixedDocument من قائمة الصور لإرسالها مباشرةً للطابعة.</summary>
    private static FixedDocument BuildFixedDocument(List<BitmapImage> pages,
                                                     double areaWidth, double areaHeight)
    {
        var doc = new FixedDocument();

        foreach (var bmp in pages)
        {
            // احسب الأبعاد مع الحفاظ على النسبة
            var (w, h) = FitSize(bmp.PixelWidth, bmp.PixelHeight, areaWidth, areaHeight);

            var page = new FixedPage
            {
                Width  = areaWidth,
                Height = areaHeight,
                Background = Brushes.White
            };

            var img = new System.Windows.Controls.Image
            {
                Source  = bmp,
                Width   = w,
                Height  = h,
                Stretch = Stretch.Uniform
            };

            // توسيط الصورة في الصفحة
            FixedPage.SetLeft(img, (areaWidth  - w) / 2);
            FixedPage.SetTop (img, (areaHeight - h) / 2);
            page.Children.Add(img);

            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            doc.Pages.Add(content);
        }

        return doc;
    }

    private static (double W, double H) FitSize(int srcW, int srcH, double maxW, double maxH)
    {
        double ratio = Math.Min(maxW / srcW, maxH / srcH);
        return (srcW * ratio, srcH * ratio);
    }

    private static PageMediaSize MapToMediaSize(PaperSizeEntry paper) => paper.Name switch
    {
        "A4"     => new PageMediaSize(PageMediaSizeName.ISOA4),
        "A5"     => new PageMediaSize(PageMediaSizeName.ISOA5),
        "Letter" => new PageMediaSize(PageMediaSizeName.NorthAmericaLetter),
        "Legal"  => new PageMediaSize(PageMediaSizeName.NorthAmericaLegal),
        "A3"     => new PageMediaSize(PageMediaSizeName.ISOA3),
        _        => new PageMediaSize(PageMediaSizeName.ISOA4)
    };

    // ─── Save as PDF ──────────────────────────────────────────────────────

    private async void BtnSavePdf_Click(object sender, RoutedEventArgs e)
    {
        TxtStatus.Text = "";
        TxtError.Text  = "";

        var dlg = new SaveFileDialog
        {
            Filter     = "PDF Document (*.pdf)|*.pdf",
            DefaultExt = "pdf",
            Title      = "حفظ البيان كـ PDF",
            FileName   = $"بيان_{DateTime.Now:yyyyMMdd_HHmm}"
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            BtnSavePdf.IsEnabled = false;
            TxtStatus.Text       = "جارٍ الحفظ...";

            await System.Threading.Tasks.Task.Run(() =>
                QuestPDF.Fluent.GenerateExtensions.GeneratePdf(_document, dlg.FileName));

            TxtStatus.Text = "✅ تم حفظ الملف بنجاح.";

            if (MessageBox.Show("تم الحفظ بنجاح.\nهل تريد فتح الملف الآن؟",
                    "تم الحفظ", MessageBoxButton.YesNo, MessageBoxImage.Question)
                == MessageBoxResult.Yes)
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = dlg.FileName, UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            TxtError.Text = "خطأ أثناء الحفظ: " + ex.Message;
        }
        finally
        {
            BtnSavePdf.IsEnabled = true;
        }
    }

    // ─── Close ────────────────────────────────────────────────────────────

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    // ─── Page Range Parser ────────────────────────────────────────────────

    private static List<int>? ParsePageRange(string input, int totalPages)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var result = new SortedSet<int>();
        var parts  = input.Split([',', '،', ' '], StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var t = part.Trim();
            if (t.Contains('-'))
            {
                var b = t.Split('-');
                if (b.Length == 2
                    && int.TryParse(b[0].Trim(), out int f)
                    && int.TryParse(b[1].Trim(), out int to))
                {
                    for (int i = Math.Max(1, f); i <= Math.Min(totalPages, to); i++)
                        result.Add(i);
                }
                else return null;
            }
            else
            {
                if (int.TryParse(t, out int p) && p >= 1 && p <= totalPages)
                    result.Add(p);
                else return null;
            }
        }
        return result.Count > 0 ? result.ToList() : null;
    }
}

/// <summary>مقاس ورق مبسّط.</summary>
public record PaperSizeEntry(string Name, int WidthMm, int HeightMm)
{
    public override string ToString() => Name;
}
