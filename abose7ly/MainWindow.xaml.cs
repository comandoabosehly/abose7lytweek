using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace Abose7ly;

public partial class MainWindow : Window
{
    private readonly List<string> _foundPaths = new();

    public MainWindow() => InitializeComponent();

    private void Detect_Click(object sender, RoutedEventArgs e)
    {
        _foundPaths.Clear();
        var candidates = new[]
        {
            @"C:\Program Files\腾讯手游助手",
            @"C:\Program Files (x86)\腾讯手游助手",
            @"C:\Program Files\TxGameAssistant",
            @"C:\Program Files (x86)\TxGameAssistant",
            @"C:\Program Files\GameLoop",
            @"C:\Program Files (x86)\GameLoop",
            @"C:\Program Files\Tenstore",
            @"C:\Program Files (x86)\Tenstore",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Tencent"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tenstore Android Connect"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tenstore Android Connect")
        };

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try { if (Directory.Exists(path)) _foundPaths.Add(path); }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
        }

        var hardware = GetHardwareSummary();
        HardwareText.Text = hardware;
        if (_foundPaths.Count == 0)
        {
            DetectionText.Text = "لم يتم العثور على المسارات الشائعة. استخدم «اختيار مجلد المحاكي» لتحديد مكان التثبيت يدويًا.";
            StatusBadge.Text = "لم يتم العثور";
        }
        else
        {
            DetectionText.Text = "المسارات التي تم العثور عليها:\n• " + string.Join("\n• ", _foundPaths) +
                "\n\nاختَر مجلد المحاكي يدويًا إذا كان مثبتًا في مكان آخر.";
            StatusBadge.Text = $"عُثر على {_foundPaths.Count} مسار";
        }
        RegionText.Text = $"نسخة اللعبة المختارة: {RegionBox.Text}. الاختيار للتنظيم والتشخيص فقط؛ لا يغيّر ملفات اللعبة.";
    }

    private void ChooseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "اختر مجلد تثبيت GameLoop أو Tenstore",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true && Directory.Exists(dialog.FolderName))
        {
            if (!_foundPaths.Contains(dialog.FolderName, StringComparer.OrdinalIgnoreCase))
                _foundPaths.Insert(0, dialog.FolderName);
            DetectionText.Text = "المجلد المحدد يدويًا:\n" + dialog.FolderName +
                "\n\nلم يتم تعديل أي ملف. استخدم أدوات المحاكي الأصلية لتغيير الإعدادات.";
            StatusBadge.Text = "تم تحديد مجلد";
        }
    }

    private void RegionBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (RegionText is null || RegionBox.SelectedItem is null) return;
        RegionText.Text = $"الملف المحدد: {RegionBox.Text}. الأداة لا تثبّت الألعاب ولا تغيّر ملفاتها تلقائيًا.";
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
        int count = 0;
        long bytes = 0;
        try
        {
            foreach (var file in Directory.EnumerateFiles(temp, "*", SearchOption.TopDirectoryOnly))
            {
                try { var info = new FileInfo(file); if (info.Exists) { count++; bytes += info.Length; } }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (Exception ex) { MessageBox.Show($"تعذر فحص المجلد: {ex.Message}", "abose7ly"); return; }

        var mb = bytes / 1024d / 1024d;
        var answer = MessageBox.Show($"تم العثور على {count} ملفًا في المستوى الرئيسي لمجلد Temp (حوالي {mb:F1} MB).\n\nلن تحذف الأداة أي شيء تلقائيًا. هل تريد فتح المجلد لمراجعة الملفات يدويًا؟",
            "مراجعة الملفات المؤقتة", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (answer == MessageBoxResult.Yes) OpenFolder(temp);
    }

    private void Performance_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("إعدادات بداية مقترحة:\n• جهاز 8 GB وIntel HD: ابدأ بأقل إعدادات رسومية وأغلق البرامج الثقيلة.\n• جهاز 16 GB وGTX 1660 Super: جرّب إعدادًا متوسطًا ثم ارفع خيارًا واحدًا في كل مرة.\n• فعّل Virtualization من BIOS فقط إذا كان المحاكي يوصي بذلك وكان جهازك يدعمها.\n• حدّث تعريف كارت الشاشة من المصدر الرسمي.\n\nالنتائج وFPS يعتمدان على إصدار المحاكي واللعبة؛ لا تفرض الأداة FPS غير مدعوم.",
            "توصيات الأداء");

    private void Safety_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("• لا يوجد aimbot أو تعديل تصويب داخل اللعبة.\n• لا يتم تعطيل anti-cheat أو تجاوز الحماية.\n• لا يوجد حذف تلقائي أو تعديل صامت لملفات اللعبة.\n• احتفظ بنسخة احتياطية قبل أي تعديل يدوي.\n• حمّل النسخ القديمة من مصدر رسمي موثوق فقط.\n• اختيار نسخة PUBG في الأداة لا يثبتها ولا يغيّرها.",
            "إرشادات السلامة");

    private void Rollback_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("للرجوع إلى إصدار سابق: استخدم الإزالة الرسمية، أعد تشغيل الجهاز، ثم ثبّت نسخة محفوظة من مصدر رسمي موثوق. قد تمنع خوادم اللعبة أو المحاكي تشغيل الإصدارات القديمة؛ لا تستخدم مثبتات مجهولة أو طرق تجاوز التحديث.",
            "الرجوع الآمن");

    private static string GetHardwareSummary()
    {
        string cpu = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "غير معروف";
        string arch = RuntimeInformation.OSArchitecture.ToString();
        string os = $"{Environment.OSVersion.VersionString} ({arch})";
        string ram = "غير متاح";
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\RESOURCEMAP\System Resources\Physical Memory");
            if (key?.GetValue(".Translated") is byte[] data && data.Length >= 8)
                ram = "يُعرض حسب بيانات النظام";
        }
        catch { }
        return $"النظام: {os}\nالمعالج: {cpu}\nالرامات: {ram}\nملاحظة: تفاصيل كارت الشاشة والذاكرة المتاحة تختلف حسب التعريفات؛ استخدم Task Manager للتحقق منها.";
    }

    private static void OpenFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show($"تعذر فتح المجلد: {ex.Message}", "abose7ly"); }
    }
}
