open System
open Avalonia
open Avalonia.Logging.Serilog
open Avalonia.ReactiveUI
open $safeprojectname$

[<CompiledName "BuildAvaloniaApp">] 
let buildAvaloniaApp () = 
    AppBuilder.Configure<App>()
              .UsePlatformDetect()
              .LogToDebug()
              .UseReactiveUI()

[<STAThread>][<EntryPoint>]
let main argv =
    buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
