using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.App;

public sealed class DesktopFileSavePicker : IFileSavePicker
{
    public async Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string defaultExtension,
        CancellationToken cancellationToken = default)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return null;

        var ext = defaultExtension.TrimStart('.');
        if (string.IsNullOrWhiteSpace(ext))
            ext = "jpg";

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export team image",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = ext,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType("Image")
                {
                    Patterns = [$"*.{ext}", "*.jpg", "*.jpeg", "*.png", "*.webp"]
                }
            ]
        });
        return file?.TryGetLocalPath();
    }
}
