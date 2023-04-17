[![2022 marketplace](https://img.shields.io/visual-studio-marketplace/v/AvaloniaTeam.AvaloniaVS.svg?label=2022-Marketplace)](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS)
[![2019 marketplace](https://img.shields.io/visual-studio-marketplace/v/AvaloniaTeam.AvaloniaforVisualStudio.svg?label=2019-Marketplace)](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaforVisualStudio)
# Avalonia for Visual Studio
This repository is used to generate Avalonia Visual Studio extensions.
Avalonia Visual Studio extension adds such capabilities to your Visual Studio:
- XAML code completion.
- XAML previewer.
- It bundles Avalonia templates in your Visual Studio.
- Icons for axaml files.

### VSIX packages for Visual Studio
| [VS2019/VS2017](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaforVisualStudio) | 
| ------------- |

| [VS2022](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.AvaloniaVS) |
| ------------- |

# Debugging
If you want to debug Avalonia previewer extension the *easiest* way to do that is [VS Experimental instance](https://docs.microsoft.com/en-us/visualstudio/extensibility/the-experimental-instance?view=vs-2019).
To run it you simply need to set **AvaloniaVS.csproj** as startup project and run it,it will open VS Experimental instance,you can run here your repro and put the breakpoints in the original VS in AvaloniaVS project.

**Note:**

This way to debug application will only help you if your issue is directly in AvaloniaVS project,if your issue is somewhere in Avalonia code,but it is reproducible only with Avalonia Previewer please consider this article -
https://docs.avaloniaui.net/guides/developer-guides/debugging-previewer
