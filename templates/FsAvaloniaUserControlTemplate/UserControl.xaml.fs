namespace Views

open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

type $safeitemrootname$ () as self = 
    inherit UserControl ()

    do AvaloniaXamlLoader.Load self
