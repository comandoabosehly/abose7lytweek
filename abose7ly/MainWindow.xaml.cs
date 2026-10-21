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
    private static FolderScan ScanFiles(string path)
    {
        int count = 0, skipped = 0; long bytes = 0;
        if (!Directory.Exists(path)) return new(path, 0, 0, 0);
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", options))
                try { var f = new FileInfo(file); if (f.Exists) { count++; bytes += f.Length; } } catch { skipped++; }
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
        var scans = targets.Select(ScanFiles).ToArray();
        var report = string.Join("\n", scans.Select(s => $"{s.Path}: {s.Files} ملف، {s.Bytes / 1024d / 1024d:F1} MB"));
        var answer = MessageBox.Show("مراجعة مجلدات Temp (قد تتداخل بعض المسارات):\n\n" + report +
            "\n\nسيُحذف فقط ما مرّ عليه 48 ساعة، مع تخطي الملفات المستخدمة. هل تريد المتابعة؟",
            "تنظيف الملفات المؤقتة", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) { Log("أُلغي تنظيف Temp."); return; }
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
                    try { var info = new FileInfo(file); if (!info.Exists || info.LastWriteTimeUtc > cutoff) continue; long size = info.Length; info.Delete(); deleted++; freed += size; }
                    catch { skipped++; }
                }
            }
            catch { skipped++; }
        }
        var summary = $"تنظيف Temp: حذف {deleted} ملفًا، تخطّي {skipped}، تحرير {freed / 1024d / 1024d:F1} MB تقريبًا.";
        Log(summary); MessageBox.Show(summary, "النتيجة");
    }

    private async void ProcessScan_Click(object sender, RoutedEventArgs e)
    {
        ProcessScanButton.IsEnabled = false;
        try
        {
            var before = Process.GetProcesses().Select(p => { try { return (p.Id, Name: p.ProcessName, Cpu: p.TotalProcessorTime, Memory: p.WorkingSet64); } catch { return (p.Id, Name: p.ProcessName, Cpu: TimeSpan.Zero, Memory: 0L); } }).ToDictionary(x => x.Id);
            await Task.Delay(1200);
            var cpuCount = Math.Max(1, Environment.ProcessorCount);
            var after = Process.GetProcesses().Select(p =>
            {
                try
                {
                    var cpu = p.TotalProcessorTime;
                    var mem = p.WorkingSet64;
                    if (before.TryGetValue(p.Id, out var b))
                        return (p.Id, Name: p.ProcessName, CpuPercent: Math.Max(0, (cpu - b.Cpu).TotalMilliseconds / 1200d / cpuCount * 100d), Memory: mem);
                    return (p.Id, Name: p.ProcessName, CpuPercent: 0d, Memory: mem);
                }
                catch { return (p.Id, Name: p.ProcessName, CpuPercent: 0d, Memory: 0L); }
                finally { p.Dispose(); }
            }).OrderByDescending(p => p.CpuPercent).Take(10).ToArray();
            var lines = after.Select(p => $"{p.Name} (PID {p.Id}) — CPU {p.CpuPercent:F1}% تقريبًا، RAM {p.Memory / 1024d / 1024d:F0} MB");
            var report = "أعلى العمليات حسب قياس مدته 1.2 ثانية (النسبة تقديرية):\n\n" + string.Join("\n", lines) +
                "\n\nلن يغلق البرنامج خدمات Windows أو العمليات تلقائيًا. أغلق فقط تطبيقًا تعرفه ولا تحتاجه.";
            Log("تم فحص العمليات واستهلاك المعالج والذاكرة.");
            MessageBox.Show(report, "مراقبة استهلاك الجهاز");
        }
        catch (Exception ex) { Log("تعذر فحص العمليات: " + ex.Message); MessageBox.Show(ex.Message, "خطأ"); }
        finally { ProcessScanButton.IsEnabled = true; }
    }

    private void IpadView_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show("عرض iPad في المحاكي يعتمد على خيارات الدقة ونسبة العرض التي يدعمها إصدار GameLoop لديك. افتح إعدادات المحاكي لتغيير الدقة يدويًا؛ لن نكتب إعدادات سرية أو نفرض نسبة غير مدعومة. هل تريد فتح مجلد المحاكي الآن؟",
            "إعداد عرض iPad", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes) OpenDetected_Click(sender, e);
        Log("تم فتح إرشادات عرض iPad؛ لم تُفرض دقة غير مدعومة.");
    }

    private void DownloadKorea_Click(object sender, RoutedEventArgs e)
    {
        const string url = "https://play.google.com/store/apps/details?id=com.pubg.krmobile";
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); Log("تم فتح صفحة Google Play الرسمية لنسخة PUBG Mobile KR."); }
        catch (Exception ex) { Log("تعذر فتح صفحة النسخة الكورية: " + ex.Message); MessageBox.Show("افتح الرابط الرسمي يدويًا:\n" + url); }
        MessageBox.Show("تم فتح صفحة Google Play الرسمية لنسخة PUBG Mobile KR. توفر التثبيت يعتمد على المنطقة والحساب والجهاز. لن يتم تنزيل APK من مصدر غير موثوق.", "نسخة PUBG الكورية");
    }

    private void ScanRepair_Click(object sender, RoutedEventArgs e)
    {
        Log("بدء فحص الصيانة الشامل."); Detect_Click(sender, e);
        var paths = new[] { Environment.GetEnvironmentVariable("TEMP") ?? Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch") }.Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths) { var s = ScanFiles(p); Log($"فحص: {s.Path} — {s.Files} ملف، {s.Bytes / 1024d / 1024d:F1} MB."); }
        Log("Prefetch تم فحصه فقط، ولا يُحذف دوريًا."); Log("لم يتم إيقاف خدمات النظام تلقائيًا.");
        Log("Key Mapping والجرافيك لم يتم تعديلهما دون صيغة مدعومة ومختبرة.");
        MessageBox.Show("اكتمل الفحص. استخدم فحص العمليات لمعرفة ما يستهلك CPU/RAM. لم يتم إيقاف خدمات Windows أو تعديل ملفات اللعبة.", "نتيجة الفحص");
    }

    private void Performance_Click(object sender, RoutedEventArgs e)
    {
        var text = "تحسين آمن: أغلق البرامج الثقيلة التي تعرفها، اترك مساحة على القرص، وحدّث تعريف الرسوميات رسميًا. على 8 GB وIntel HD استخدم أقل إعدادات متاحة. لا توقف خدمات Windows عشوائيًا ولا تحذف Prefetch. ارتفاع الحرارة إلى 100°C يستلزم إيقاف اللعب وفحص التبريد؛ البرنامج لا يستطيع تحديد حرارة المعالج بدقة من دون مصدر قياس مناسب.";
        Log("تم عرض توصيات الأداء."); MessageBox.Show(text, "تحسين الأداء");
    }

    private void Safety_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("لا يتم تعطيل Defender أو خدمات Windows الأساسية، ولا يتم تعديل ملفات anti-cheat. أوقف اللعب إذا وصلت الحرارة إلى 100°C وتحقق من التبريد. نزّل اللعبة من المصادر الرسمية.", "إرشادات السلامة");
    private void Rollback_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("استخدم أداة التحديث أو الإزالة الرسمية. قد تمنع الخوادم الإصدارات القديمة؛ لا تستخدم ملفات مجهولة أو طرق تجاوز التحديث.", "الرجوع الآمن");

    private static string GetHardwareSummary()
    {
        string cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "غير معروف";
        return $"النظام: {Environment.OSVersion.VersionString} ({RuntimeInformation.OSArchitecture})\nالمعالج: {cpu}\nالرامات وكارت الشاشة: راجع Task Manager > Performance للتفاصيل الدقيقة.";
    }
    private void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "abose7ly", "logs");
        Directory.CreateDirectory(path); var file = Path.Combine(path, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllText(file, _log.ToString(), Encoding.UTF8); Log("تم حفظ سجل الجلسة: " + file);
        MessageBox.Show("تم حفظ السجل هنا:\n" + file, "abose7ly");
    }
    private static void OpenFolder(string path)
    {
        try { if (!Directory.Exists(path)) Directory.CreateDirectory(path); Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show($"تعذر فتح المجلد: {ex.Message}", "abose7ly"); }
    }
}