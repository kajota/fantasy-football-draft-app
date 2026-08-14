using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FantasyDraftAssistant.App;

public sealed class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!
            .Replace("ViewModel", "View", StringComparison.Ordinal)
            .Replace(".ViewModels.", ".Views.", StringComparison.Ordinal);
        var type = Type.GetType(name) ?? typeof(Views.LeaguesView).Assembly.GetType(name);
        if (type is null)
            return new TextBlock { Text = name };

        return (Control)Activator.CreateInstance(type)!;
    }

    public bool Match(object? data) => data is ObservableObject;
}
