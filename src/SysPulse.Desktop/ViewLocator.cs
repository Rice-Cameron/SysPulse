using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SysPulse.Desktop.ViewModels;

namespace SysPulse.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url="https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate{
    public Control? Build(object? param){
        if(param is null){
            return null;
        }

        string name=param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        Type? type=Type.GetType(name);

        if(type != null){
            return (Control)Activator.CreateInstance(type)!;
        }

        TextBlock notFoundTextBlock=new TextBlock();
        notFoundTextBlock.Text="Not Found: " + name;
        return notFoundTextBlock;
    }

    public bool Match(object? data){
        return data is ViewModelBase;
    }
}
