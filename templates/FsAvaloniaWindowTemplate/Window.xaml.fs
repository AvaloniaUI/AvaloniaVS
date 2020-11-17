namespace Views

open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

type $safeitemname$ () as self = 
    inherit Window ()
    #if DEBUG
    do self.AttachDevTools()
    #endif
    do AvaloniaXamlLoader.Load self
