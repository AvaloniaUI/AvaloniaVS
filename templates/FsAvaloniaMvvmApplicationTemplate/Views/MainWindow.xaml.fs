namespace Views

open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

type MainWindow () as self = 
    inherit Window ()
    #if DEBUG
    do self.AttachDevTools()
    #endif
    do AvaloniaXamlLoader.Load self
