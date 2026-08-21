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

        var isImage = ext.Equals("jpg", StringComparison.OrdinalIgnoreCase)
                      || ext.Equals("jpeg", StringComparison.OrdinalIgnoreCase)
                      || ext.Equals("png", StringComparison.OrdinalIgnoreCase)
                      || ext.Equals("webp", StringComparison.OrdinalIgnoreCase);
        var isMarkdown = ext.Equals("md", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals("markdown", StringComparison.OrdinalIgnoreCase);

        var file = await desktop.MainWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = isImage ? "Export team image" : "Export file",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = ext,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType(isImage ? "Image" : isMarkdown ? "Markdown" : "Text")
                {
                    Patterns = isImage
                        ? [$"*.{ext}", "*.jpg", "*.jpeg", "*.png", "*.webp"]
                        : isMarkdown
                            ? [$"*.{ext}", "*.md", "*.markdown", "*.txt"]
                            : [$"*.{ext}", "*.txt"]
                }
            ]
        });
        return file?.TryGetLocalPath();
    }
}
