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

    public async Task<PickedFile?> PickOpenFileAsync(
        string title,
        IReadOnlyList<string> extensions,
        CancellationToken cancellationToken = default)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is null)
            return null;

        var patterns = extensions
            .Select(ext =>
            {
                var trimmed = ext.Trim();
                if (trimmed.StartsWith('*'))
                    return trimmed;
                return trimmed.StartsWith('.') ? $"*{trimmed}" : $"*.{trimmed}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (patterns.Length == 0)
            patterns = ["*.*"];

        var files = await desktop.MainWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Image") { Patterns = patterns }
            ]
        });
        if (files.Count == 0)
            return null;

        var file = files[0];
        await using var stream = await file.OpenReadAsync();
        using var memory = new MemoryStream();
        var cap = TeamPortraitImage.MaxBytes + 1;
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
                break;
            var remaining = cap - (int)memory.Length;
            if (remaining <= 0)
                break;
            memory.Write(buffer, 0, Math.Min(read, remaining));
        }

        return new PickedFile
        {
            FileName = file.Name,
            Bytes = memory.ToArray()
        };
    }
}
