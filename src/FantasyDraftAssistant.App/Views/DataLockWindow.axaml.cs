using Avalonia.Controls;
using Avalonia.Interactivity;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.App.Views;

public partial class DataLockWindow : Window
{
    public DataLockWindow()
    {
        InitializeComponent();
    }

    public DataLockWindow(DataLockInspection inspection, string dataRoot) : this()
    {
        DataContext = DataLockEvaluator.Dialog(inspection, dataRoot);
    }

    public bool OpenAnyway { get; private set; }

    private void QuitClick(object? sender, RoutedEventArgs e)
    {
        OpenAnyway = false;
        Close();
    }

    private void OpenAnywayClick(object? sender, RoutedEventArgs e)
    {
        OpenAnyway = true;
        Close();
    }
}
