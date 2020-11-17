open System
open Avalonia
open Avalonia.Logging.Serilog
open $safeprojectname$

[<CompiledName "BuildAvaloniaApp">] 
let buildAvaloniaApp () = 
    AppBuilder.Configure<App>()
              .UsePlatformDetect()
              .LogToDebug()

[<STAThread>][<EntryPoint>]
let main argv =
    buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
