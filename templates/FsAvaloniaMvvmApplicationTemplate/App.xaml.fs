namespace $safeprojectname$

open Avalonia
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Markup.Xaml
open ViewModels
open Views

type App() as self =
    inherit Application()

    do AvaloniaXamlLoader.Load self

    override x.OnFrameworkInitializationCompleted() =
        match x.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
             desktop.MainWindow <- new MainWindow(DataContext=MainWindowViewModel())
        | _ -> ()

        base.OnFrameworkInitializationCompleted()
