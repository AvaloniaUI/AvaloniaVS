namespace $rootnamespace$

open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml

type $safeitemrootname$ () as this = 
    inherit UserControl ()

    do this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
