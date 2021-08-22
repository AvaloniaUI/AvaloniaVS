open System
open Avalonia
open Avalonia.ReactiveUI
open $safeprojectname$

[<CompiledName "BuildAvaloniaApp">] 
let buildAvaloniaApp () = 
    AppBuilder.Configure<App>()
              .UsePlatformDetect()
              .LogToTrace(level = Logging.LogEventLevel.Warning)
              .UseReactiveUI()

[<STAThread>][<EntryPoint>]
let main argv =
    buildAvaloniaApp().StartWithClassicDesktopLifetime(argv)
