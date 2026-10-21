using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace Abose7ly;

public partial class MainWindow : Window
{
    private readonly List<string> _foundPaths = new();
    private readonly StringBuilder _log = new();

    public MainWindow() => InitializeComponent();

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        _log.AppendLine(line);
        if (LogText is not null) LogText.Text = _log.ToString();
    }

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        _foundPaths.Clear();
        var candidates = new[]
        {
            @"C:\Program Files\腾讯手游助手", @"C:\Program Files (x86)\腾讯手游助手",
            @"C:\Program Files\TxGameAssistant", @"C:\Program Files (x86)\TxGameAssistant",
            @"C:\Program Files\GameLoop", @"C:\Program Files (x86)\GameLoop",
            @"C:\Program Files\Tenstore", @"C:\Program Files (x86)\Tenstore",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tencent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tenstore Android Connect"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tenstore Android Connect")
        };
        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            try { if (Directory.Exists(path)) _foundPaths.Add(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }

        HardwareText.Text = GetHardwareSummary();
        DetectionText.Text = _foundPaths.Count == 0
            ? "لم يتم العثور على مسار معروف. اختر مجلد المحاكي يدويًا؛ عدم العثور لا يعني أن المحاكي غير مثبت."
            : "المسارات التي تم العثور عليها:\n• " + string.Join("\n• ", _foundPaths);
        StatusBadge.Text = _foundPaths.Count == 0 ? "المحاكي غير مكتشف" : $"عُثر على {_foundPaths.Count} مسار";
        RegionText.Text = $"نسخة اللعبة المختارة: {RegionBox.Text}. الاختيار لا يغيّر ملفات اللعبة.";
        Log($"فحص المسارات انتهى: {_foundPaths.Count} مسار. النسخة المختارة: {RegionBox.Text}.");
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "اختر مجلد تثبيت GameLoop أو Tenstore", Multiselect = false };
        if (dialog.ShowDialog(this) == true && Directory.Exists(dialog.FolderName))
        {
            if (!_foundPaths.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase)) _foundPaths.Insert(0, dialog.FolderName);
            DetectionText.Text = "المجلد المحدد يدويًا:\n" + dialog.FolderName;
            StatusBadge.Text = "تم تحديد مجلد";
            Log($"تم تحديد مجلد يدويًا: {dialog.FolderName}");
        }
    }

    private void RegionBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RegionText is null || RegionBox.SelectedItem is null) return;
        RegionText.Text = $"النسخة المحددة: {RegionBox.Text}. ستستخدم للتشخيص فقط؛ لا يتم تنزيل أو تعديل ملفات اللعبة.";
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private void OpenDetected_Click(object sender, RoutedEventArgs e)
    {
        if (_foundPaths.Count == 0) Detect_Click(sender, e);
        if (_foundPaths.Count > 0) OpenFolder(_foundPaths[0]);
        else MessageBox.Show("لم يتم العثور على مجلد محاكي معروف. حدّد المجلد يدويًا.", "abose7ly");
    }

    private void OpenTemp_Click(object sender, RoutedEventArgs e)
    {
        var temp = Path.GetTempPath();
        int count = 0, skipped = 0;
        long bytes = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.TopDirectoryOnly))
            {
                try { var info = new FileInfo(file); if (info.Exists) { count++; bytes += info.Length; } }
                catch { skipped++; }
            }
        }
        catch (Exception ex) { Log("فشل فحص Temp: " + ex.Message); MessageBox.Show(ex.Message, "abose7ly"); return; }
        var mb = bytes / 1024d / 1024d;
        var answer = MessageBox.Show($"تم العثور على {count} ملفًا في المستوى الرئيسي من Temp ({mb:F1} MB).\n\nيمكن حذف الملفات الأقدم من 24 ساعة فقط. سيتم تخطي الملفات المستخدمة أو التي يرفض Windows الوصول إليها. هل تريد بدء التنظيف؟",
            "تأكيد تنظيف الملفات المؤقتة", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) { Log("ألغى المستخدم تنظيف Temp."); return; }

        int deleted = 0; long freed = 0;
        foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-24)) continue;
                var size = info.Length;
                info.Delete();
                deleted++; freed += size;
            }
            catch { skipped++; }
        }
        var summary = $"اكتمل تنظيف Temp: حذف {deleted} ملفًا، وتخطّي {skipped} ملفًا، وتحرير {freed / 1024d / 1024d:F1} MB تقريبًا.";
        Log(summary);
        MessageBox.Show(summary, "نتيجة التنظيف", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanRepair_Click(object sender, RoutedEventArgs e)
    {
        Log("بدء الفحص العام.");
        Detect_Click(sender, e);
        var temp = Path.GetTempPath();
        int oldFiles = 0; long oldBytes = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.TopDirectoryOnly))
                try { var f = new FileInfo(file); if (f.Exists && f.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-24)) { oldFiles++; oldBytes += f.Length; } } catch { }
        }
        catch (Exception ex) { Log("تعذر فحص Temp: " + ex.Message); }
        Log($"Temp: {oldFiles} ملفًا أقدم من 24 ساعة ({oldBytes / 1024d / 1024d:F1} MB) يمكن مراجعتها.");
        Log("Key Mapping: لا يمكن تعديل ملفات التحكم بأمان دون تحديد صيغة مدعومة خاصة بإصدار المحاكي؛ لم يتم تغييرها.");
        Log("الجرافيك وFPS: لم يتم فرض قيم غير مدعومة؛ استخدم إعدادات المحاكي/اللعبة الرسمية.");
        MessageBox.Show("اكتمل الفحص الأولي. تم فحص المسارات وملفات Temp القديمة. لم تُعدّل ملفات اللعبة أو Key Mapping أو FPS؛ هذه الخطوة تحتاج تحققًا من إصدار المحاكي وصيغة الإعدادات المدعومة.", "abose7ly - نتيجة الفحص", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Performance_Click(object sender, RoutedEventArgs e)
    {
        var text = "اقتراحات آمنة حسب الجهاز:\n• ذاكرة 8 GB وIntel HD: أقل جودة رسومية، أغلق البرامج الثقيلة وتأكد من وجود مساحة فارغة على القرص.\n• ذاكرة 16 GB وكارت منفصل: ابدأ بإعداد متوسط واختبر الاستقرار.\n• حدّث تعريف الرسوميات من المصدر الرسمي.\n• لا تفرض FPS أو دقة لا يدعمها المحاكي.\n\nالأداة لا تطبّق إعدادات الرسوميات تلقائيًا لأن أسماء الخيارات وملفاتها تختلف بين إصدارات اللعبة والمحاكي.";
        Log("تم عرض توصيات الأداء.");
        MessageBox.Show(text, "توصيات الأداء");
    }

    private void Safety_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("• تنظيف Temp يقتصر على الملفات الأقدم من 24 ساعة ويطلب موافقتك.\n• لا يتم تعديل ملفات anti-cheat أو تجاوز الحماية.\n• لا يتم تعديل Key Mapping أو FPS تلقائيًا دون صيغة مدعومة ومختبرة.\n• احتفظ بنسخة احتياطية قبل أي تعديل يدوي.\n• نزّل اللعبة والمحاكي من مصادر رسمية.", "إرشادات السلامة");

    private void Rollback_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("استخدم أداة التحديث أو الإزالة الرسمية. قد تمنع الخوادم الإصدارات القديمة؛ لا تستخدم ملفات مجهولة أو طرق تجاوز التحديث.", "الرجوع الآمن");

    private static string GetHardwareSummary()
    {
        string cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "غير معروف";
        string arch = RuntimeInformation.OSArchitecture.ToString();
        string os = $"{Environment.OSVersion.VersionString} ({arch})";
        return $"النظام: {os}\nالمعالج: {cpu}\nالرامات: راجع Task Manager > Performance للحصول على القيمة الدقيقة.\nكارت الشاشة: راجع Task Manager > Performance؛ قد تختلف البيانات حسب التعريف.";
    }

    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abose7ly", "logs");
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(file, _log.ToString(), Encoding.UTF8);
        Log("تم حفظ سجل الجلسة: " + file);
        MessageBox.Show("تم حفظ السجل هنا:\n" + file, "abose7ly");
    }

    private static void OpenFolder(string path)
    {
        try { if (!Directory.Exists(path)) Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"تعذر فتح المجلد: {ex.Message}", "abose7ly"); }
    }
}