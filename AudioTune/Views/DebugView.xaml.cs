using System.IO;
using System.Windows;
using System.Windows.Controls;
using AudioTune.Models;
using AudioTune.Services;
using Microsoft.Win32;

namespace AudioTune.Views;

public partial class DebugView : UserControl
{
    public DebugView()
    {
        InitializeComponent();
        Loaded += (_, _) => { AppServices.Log.EntryAdded += OnLog; Refresh(); };
        Unloaded += (_, _) => AppServices.Log.EntryAdded -= OnLog;
    }
    private void OnLog(LogEntry e) => Dispatcher.Invoke(Refresh);
    private void Refresh() { LogText.Text = string.Join(Environment.NewLine, AppServices.Log.Entries.Select(e => $"[{e.Time:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level}] {e.Message}")); LogText.ScrollToEnd(); }
    private void Clear_Click(object sender, RoutedEventArgs e) { AppServices.Log.Clear(); Refresh(); }
    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt", FileName = $"AudioTune-{DateTime.Now:yyyyMMdd-HHmmss}.log" };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, LogText.Text);
    }
}
