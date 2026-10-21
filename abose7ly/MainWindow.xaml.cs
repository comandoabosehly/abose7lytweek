using System.Diagnostics;
using System.IO;
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

        if (_foundPaths.Count == 0)
        {
            DetectionText.Text = "لم يتم العثور على المسارات الشائعة. قد يكون المحاكي مثبتاً في مكان مخصص؛ ابحث عنه في Windows أو افتح مساره يدوياً.";
            StatusBadge.Text = "لم يتم العثور";
        }
        else
        {
            DetectionText.Text = "المسارات التي تم العثور عليها:\n• " + string.Join("\n• ", _foundPaths) +
                "\n\nالفحص مبدئي وقد لا يكتشف التثبيتات المخصصة أو كل إصدارات Tenstore.";
            StatusBadge.Text = $"عُثر على {_foundPaths.Count} مسار";
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e) =>
        OpenFolder(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private void OpenDetected_Click(object sender, RoutedEventArgs e)
    {
        if (_foundPaths.Count == 0) Detect_Click(sender, e);
        if (_foundPaths.Count > 0) OpenFolder(_foundPaths[0]);
        else MessageBox.Show("لم يتم العثور على مجلد محاكي معروف. افتح مكان التثبيت يدوياً.", "abose7ly");
    }

    private void OpenTemp_Click(object sender, RoutedEventArgs e) => OpenFolder(Path.GetTempPath());

    private void Performance_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("اقتراحات آمنة:\n1. ابدأ بإعداد رسومات منخفض أو متوسط لاختبار الثبات.\n2. أغلق البرامج الثقيلة بالخلفية.\n3. استخدم تعريف بطاقة الرسوم الرسمي.\n4. غيّر خياراً واحداً في كل مرة.\n\nFPS الفعلي يعتمد على الجهاز وإصدار المحاكي واللعبة.", "توصيات الأداء");

    private void Safety_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("• لا يوجد aimbot أو تعديل تصويب داخل اللعبة.\n• لا يتم تعطيل anti-cheat أو تجاوز الحماية.\n• لا يوجد حذف تلقائي أو تعديل صامت للملفات.\n• احتفظ بنسخة احتياطية قبل أي تعديل يدوي.\n• حمّل النسخ القديمة من مصدر رسمي موثوق فقط.", "إرشادات السلامة");

    private void Rollback_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("للرجوع إلى إصدار سابق: استخدم الإزالة الرسمية، أعد تشغيل الجهاز، ثم ثبّت نسخة محفوظة من مصدر رسمي موثوق. تحقّق من توافقها مع اللعبة؛ لا تستخدم مثبتات مجهولة.", "الرجوع الآمن");

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
