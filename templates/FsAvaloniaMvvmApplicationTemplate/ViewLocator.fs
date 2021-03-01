namespace $safeprojectname$

open System
open Avalonia.Controls
open Avalonia.Controls.Templates
open ViewModels

type ViewLocator () =
    member __.SupportsRecycling with get() = false

    interface IDataTemplate with
        member __.Match data = data :? ViewModelBase
        member __.Build data : IControl =
            let name = data.GetType().FullName.Replace("ViewModel", "View")
            let type' = Type.GetType name
            if type' <> null then
                Activator.CreateInstance(type') :?> IControl
            else
                TextBlock(Text = "Not Found: " + name) :> IControl
