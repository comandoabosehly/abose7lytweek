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
        try { ram = $"{new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1024d / 1024d / 1024d:F1} GB"; } catch { }
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