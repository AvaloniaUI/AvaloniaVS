# Avalonia for Visual Studio

XAML previewer and templates for the [Avalonia UI framework](https://github.com/AvaloniaUI/Avalonia).

### Extension for Visual Studio 2019,2017
https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaforVisualStudio

### Extension for Visual Studio 2022
https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS

# Building and debugging
Before building project you will need to restore all submodules.
This command will help you to restore submodules.

```git submodule update --init --recursive```

If you want to debug Avalonia previewer extension the *easiest* way to do that is [VS Experimental instance](https://docs.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance?view=vs-2019).
To run it you simply need to set **AvaloniaVS.csproj** as startup project and run it,it will open VS Experimental instance,you can run here your repro and put the breakpoints in the original VS in AvaloniaVS project.

**Note:**

This way to debug application will only help you if your issue is directly in AvaloniaVS project,if your issue is somewhere in Avalonia code,but it is reproducible only with Avalonia Previewer please consider this article -
https://docs.avaloniaui.net/guides/developer-guides/debugging-previewer
