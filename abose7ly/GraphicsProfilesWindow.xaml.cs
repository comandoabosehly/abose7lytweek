using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Abose7ly;

public partial class GraphicsProfilesWindow : Window
{
    private readonly List<string> _detected = new();

    public GraphicsProfilesWindow() => InitializeComponent();

    private static readonly string[] PubgPackages =
    {
        "com.tencent.ig", "com.pubg.krmobile", "com.rekoo.pubgm",
        "com.vng.pubgmobile", "com.pubg.imobile", "com.tencent.tmgp.pubgmhd"
    };

    private static string? FindAdb(IEnumerable<string> roots)
    {
        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TxGameAssistant", "ui", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TxGameAssistant", "ui", "adb.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "TxGameAssistant", "ui", "AndroidEmulator.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "TxGameAssistant", "ui", "AndroidEmulator.exe")
        };
        foreach (var root in roots)
        {
            candidates.Add(Path.Combine(root, "ui", "adb.exe"));
            candidates.Add(Path.Combine(root, "adb.exe"));
            candidates.Add(Path.Combine(root, "Engine", "adb.exe"));
            candidates.Add(Path.Combine(root, "EmulatorShell", " adb.exe"));
        }
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var folder in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            candidates.Add(Path.Combine(folder.Trim(), "adb.exe"));
        return candidates.FirstOrDefault(File.Exists);
    }

    private static (int ExitCode, string Output, string Error) RunAdb(string adb, string args)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(adb, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(adb) ?? Environment.CurrentDirectory
        };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10000))
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException("انتهت مهلة ADB بعد 10 ثوانٍ.");
        }
        Task.WaitAll(outputTask, errorTask);
        return (process.ExitCode, outputTask.Result, errorTask.Result);
    }

    private void AdbScan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_detected.Count == 0) Scan_Click(sender, e);
            var adb = FindAdb(_detected);
            if (adb is null)
            {
                ReportText.Text += "\\n\\nلم يتم العثور على adb.exe في المسارات المعروفة أو PATH. شغّل المحاكي أولًا وحدد مجلده يدويًا إذا كان مثبتًا بمسار مخصص.";
                MessageBox.Show("لم يتم العثور على ADB. تأكد من تثبيت المحاكي ثم أعد الفحص.", "ADB");
                return;
            }

            var start = RunAdb(adb, "start-server");
            var devices = RunAdb(adb, "devices");
            var deviceLines = devices.Output.Split(new[] { '\\r', '\\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Contains("\\tdevice", StringComparison.OrdinalIgnoreCase)).ToArray();

            if (deviceLines.Length == 0)
            {
                ReportText.Text += $"\\n\\nADB: {adb}\\nلا يوجد جهاز محاكي متصل. شغّل GameLoop/Tenstore ثم أعد المحاولة.\\n{devices.Output}";
                MessageBox.Show("تم تشغيل ADB لكن لم يظهر جهاز متصل. افتح المحاكي ثم أعد الفحص.", "لم يتم الاتصال");
                return;
            }

            var packageResult = RunAdb(adb, "shell pm list packages");
            var installed = PubgPackages.Where(pkg => packageResult.Output.Contains("package:" + pkg, StringComparison.OrdinalIgnoreCase)).ToArray();
            var report = new StringBuilder();
            report.AppendLine("تم العثور على جهاز ADB:");
            foreach (var line in deviceLines) report.AppendLine("• " + line);
            report.AppendLine();
            report.AppendLine("نسخ PUBG المكتشفة:");
            report.AppendLine(installed.Length == 0 ? "لم يتم العثور على حزم PUBG المعروفة على الجهاز المتصل." : string.Join("\\n", installed.Select(p => "• " + p)));
            report.AppendLine();
            report.AppendLine("لم يتم تعديل أي ملف أو إعداد داخل اللعبة.");
            ReportText.Text += "\\n\\n" + report;
            MessageBox.Show(report.ToString(), "فحص ADB وPUBG", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportText.Text += "\\n\\nخطأ ADB: " + ex.Message;
            MessageBox.Show(ex.Message, "تعذر فحص ADB");
        }
    }

    private void Scan_Click(object sender, RoutedEventArgs e)
    {
        _detected.Clear();
        var roots = new[]
        {
            @"C:\Program Files\TxGameAssistant", @"C:\Program Files (x86)\TxGameAssistant",
            @"C:\Program Files\GameLoop", @"C:\Program Files (x86)\GameLoop",
            @"C:\Program Files\腾讯手游助手", @"C:\Program Files (x86)\腾讯手游助手",
            @"C:\Program Files\Tenstore", @"C:\Program Files (x86)\Tenstore",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tencent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tenstore Android Connect"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tenstore Android Connect")
        };
        foreach (var p in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            try { if (Directory.Exists(p)) _detected.Add(p); } catch { }

        var ram = "غير متاح";
        try { ram = $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024d / 1024d / 1024d:F1} GB (حد الذاكرة المتاح للعملية)"; } catch { }
        var gpus = new List<string>();
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (key is not null)
                foreach (var sub in key.GetSubKeyNames())
                    using (var k = key.OpenSubKey(sub))
                        if (k is not null)
                            foreach (var child in k.GetSubKeyNames())
                                using (var d = k.OpenSubKey(child))
                                    if (d?.GetValue("DriverDesc") is string name && !gpus.Contains(name)) gpus.Add(name);
        }
        catch { }

        var selected = (EmulatorBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "اكتشاف تلقائي";
        var list = _detected.Count == 0 ? "لم يتم العثور على مسارات معروفة. قد تحتاج لاختيار مجلد المحاكي من الشاشة الرئيسية." : string.Join("\n• ", _detected);
        ReportText.Text = $"المحاكي المختار: {selected}\nالمسارات المكتشفة:\n• {list}\n\nالمعالج: {Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "غير معروف"}\nعدد المعالجات المنطقية: {Environment.ProcessorCount}\nالذاكرة الفعلية: {ram}\nكروت الشاشة المكتشفة من سجل Windows: {(gpus.Count == 0 ? "لم تُقرأ" : string.Join(", ", gpus))}\n\nملاحظة: هذا فحص أولي؛ لا يؤكد دعم 120 FPS أو Ultra HD. يلزم التحقق من خيارات اللعبة والمحاكي وتعريف GPU.";
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_detected.Count == 0) Scan_Click(sender, e);
        var selected = (EmulatorBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
        var path = _detected.FirstOrDefault(p =>
            selected.Contains("Tenstore", StringComparison.OrdinalIgnoreCase) ? p.Contains("Tenstore", StringComparison.OrdinalIgnoreCase) :
            selected.Contains("Classic", StringComparison.OrdinalIgnoreCase) || selected.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) ? p.Contains("GameLoop", StringComparison.OrdinalIgnoreCase) || p.Contains("TxGameAssistant", StringComparison.OrdinalIgnoreCase) :
            true);
        if (path is null) { MessageBox.Show("لم يتم العثور على مجلد لهذا المحاكي. حدده من الشاشة الرئيسية أولًا.", "abose7ly"); return; }
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "تعذر فتح المجلد"); }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        var mode = UltraRadio.IsChecked == true ? "Ultra HD + 120 FPS" :
                   SmoothRadio.IsChecked == true ? "Smooth + 120 FPS" : "Balanced";
        var msg = $"الوضع المختار: {mode}\n\nلن يغيّر abose7ly ملفات إعدادات مجهولة أو يكتب قيمة FPS غير مؤكدة. افتح إعدادات المحاكي ثم اختر أعلى FPS وجودة تظهران رسميًا في نسختك. إذا لم يظهر 120 FPS أو Ultra HD فذلك يعني أن اللعبة/المحاكي/الجهاز لا يتيحها حاليًا.\n\nبعد تطبيق الإعدادات داخل المحاكي، أعد الفحص وقارن الأداء والحرارة.";
        MessageBox.Show(msg, "تطبيق الوضع — خطوة آمنة", MessageBoxButton.OK, MessageBoxImage.Information);
        ReportText.Text += $"\n\nالوضع المطلوب: {mode}. لم يتم تعديل ملفات اللعبة أو المحاكي.";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}