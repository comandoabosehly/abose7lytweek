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
        _log.AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
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
            try { if (Directory.Exists(path)) _foundPaths.Add(path); } catch { }
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

    private sealed record FolderScan(string Path, int Files, long Bytes, int Skipped);
    private static FolderScan ScanFiles(string path, bool recursive = true)
    {
        int count = 0, skipped = 0; long bytes = 0;
        if (!Directory.Exists(path)) return new(path, 0, 0, 0);
        var options = new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
                try { var f = new FileInfo(file); if (f.Exists) { count++; bytes += f.Length; } }
                catch { skipped++; }
        }
        catch { skipped++; }
        return new(path, count, bytes, skipped);
    }

    private void OpenTemp_Click(object sender, RoutedEventArgs e)
    {
        var targets = new[]
        {
            Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
            Environment.GetEnvironmentVariable("TMP") ?? Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp")
        }.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var scans = targets.Select(p => ScanFiles(p)).ToArray();
        var report = string.Join("\n", scans.Select(s => $"{s.Path}: {s.Files} ملف، {s.Bytes / 1024d / 1024d:F1} MB"));
        var total = scans.Sum(s => s.Bytes);
        var answer = MessageBox.Show("فحص مجلدات الملفات المؤقتة (قد تتداخل بعض المسارات):\n\n" + report +
            $"\n\nالحذف سيقتصر على الملفات الأقدم من 48 ساعة، مع تخطي الملفات المستخدمة. إجمالي الحجم الظاهر قبل إزالة التداخل: {total / 1024d / 1024d:F1} MB. هل تريد التنظيف؟",
            "مراجعة وتنظيف الملفات المؤقتة", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) { Log("ألغى المستخدم تنظيف الملفات المؤقتة."); return; }
        int deleted = 0, skipped = 0; long freed = 0;
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in targets)
        {
            if (!Directory.Exists(folder)) continue;
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    if (!seen.Add(file)) continue;
                    try
                    {
                        var info = new FileInfo(file);
                        if (!info.Exists || info.LastWriteTimeUtc > cutoff) continue;
                        long size = info.Length;
                        info.Delete();
                        deleted++; freed += size;
                    }
                    catch { skipped++; }
                }
                // Remove only empty directories under selected temp folders.
                foreach (var dir in Directory.EnumerateDirectories(folder, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).OrderByDescending(x => x.Length))
                    try { if (!Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir, false); } catch { }
            }
            catch { skipped++; }
        }
        var summary = $"انتهى التنظيف: حذف {deleted} ملفًا، تخطّي {skipped} ملفًا، وتحرير {freed / 1024d / 1024d:F1} MB تقريبًا. لم يتم حذف ملفات حديثة.";
        Log(summary);
        MessageBox.Show(summary, "نتيجة التنظيف", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ScanRepair_Click(object sender, RoutedEventArgs e)
    {
        Log("بدء فحص الصيانة الشامل.");
        Detect_Click(sender, e);
        var paths = new[]
        {
            Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch")
        }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            var s = ScanFiles(p);
            Log($"فحص: {s.Path} — {s.Files} ملف، {s.Bytes / 1024d / 1024d:F1} MB.");
        }
        Log("Prefetch تم فحصه فقط: لا يُنصح بحذفه دوريًا، لأن Windows يديره وقد يؤدي الحذف إلى بطء مؤقت عند إعادة بناء البيانات.");
        Log("بدء التشغيل والخدمات: لم يتم تعطيل أي شيء تلقائيًا. راجع إدارة المهام لتجنب إيقاف خدمات النظام أو برامج الأمان.");
        Log("Key Mapping والجرافيك: لم يتم تعديل ملفات اللعبة دون تحديد صيغة مدعومة ومختبرة.");
        MessageBox.Show("اكتمل فحص الصيانة. تمت مراجعة مسارات Temp وPrefetch والمسارات المعروفة للمحاكي. التنظيف يحتاج موافقتك من زر تنظيف الملفات المؤقتة. لم يتم حذف Prefetch أو تعطيل خدمات النظام أو فرض FPS.", "abose7ly - نتيجة الفحص", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Performance_Click(object sender, RoutedEventArgs e)
    {
        var text = "تحسينات آمنة:\n• أغلق البرامج الثقيلة غير الضرورية من Task Manager قبل اللعب.\n• اترك مساحة حرة مناسبة على قرص Windows وقرص المحاكي.\n• حدّث تعريف كارت الشاشة من المصدر الرسمي.\n• على 8 GB وIntel HD: استخدم أقل جودة متاحة وأغلق المتصفح والتطبيقات الثقيلة.\n• على جهاز بكارت منفصل وRAM أكبر: ابدأ بإعداد متوسط واختبر الثبات.\n• لا تحذف Prefetch ولا تعطل خدمات Windows عشوائيًا؛ لا تضمن هذه الخطوات زيادة FPS.\n\nالتطبيق لا يغيّر خطة الطاقة أو خدمات بدء التشغيل تلقائيًا حاليًا.";
        Log("تم عرض قائمة تحسين الأداء الآمنة.");
        MessageBox.Show(text, "تحسين أداء الجهاز");
    }

    private void Safety_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("• التنظيف يطلب موافقتك ويتخطى الملفات المقفلة.\n• Prefetch يُفحص ولا يُحذف.\n• لا يتم تعطيل Defender أو خدمات Windows الأساسية.\n• لا يتم تعديل ملفات anti-cheat أو تجاوز الحماية.\n• نزّل اللعبة والمحاكي من مصادر رسمية.", "إرشادات السلامة");

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