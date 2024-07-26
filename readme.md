[![2022 marketplace](https://img.shields.io/visual-studio-marketplace/v/AvaloniaTeam.AvaloniaVS.svg?label=2022-Marketplace)](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS)
# Avalonia for Visual Studio
This repository is used to generate Avalonia Visual Studio extensions.
Avalonia Visual Studio extension adds such capabilities to your Visual Studio:
- XAML code completion.
- XAML previewer.
- It bundles Avalonia templates in your Visual Studio.
- Icons for axaml files.

### VSIX packages for Visual Studio

[Avalonia for Visual Studio 2022](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS)

For VS2017 and VS2019 you need to download another plugin [Avalonia for Visual Studio 2019](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaforVisualStudio)
- Latest Release supporting VS2019 is **11.5**
- Latest Release supporting VS2017 is **11.2**

If you are interested in VSCode extension, visit https://github.com/AvaloniaUI/AvaloniaVSCode.

# Debugging
If you want to debug Avalonia previewer extension the *easiest* way to do that is [VS Experimental instance](https://docs.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance?view=vs-2019).
To run it you simply need to set **AvaloniaVS.csproj** as startup project and run it,it will open VS Experimental instance,you can run here your repro and put the breakpoints in the original VS in AvaloniaVS project.

**Note:**

This way to debug application will only help you if your issue is directly in AvaloniaVS project,if your issue is somewhere in Avalonia code,but it is reproducible only with Avalonia Previewer please consider this article - [https://docs.avaloniaui.net/docs/0.10.x/guides/developer-guides/debugging-previewer](https://docs.avaloniaui.net/docs/0.10.x/guides/developer-guides/debugging-previewer)
