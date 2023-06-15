using Xunit;

namespace CompletionEngineTests.Manipulator;

partial class ManipulatorBasicTests
{
    const string nestingRenameSource = """
<UserControl xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
                x:Class="ACTest.Views.UserControl1">
    <UserControl$
    <TextBlock Text="Ciao"/>
</UserControl>
""";

    const string nestingRenameExpected = """
<UserControl xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
                x:Class="ACTest.Views.UserControl1">
    <UserControld
    <TextBlock Text="Ciao"/>
</UserControl>
""";

    const string scenario2Source = """
<UserControl$ xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
                x:Class="ACTest.Views.UserControl1">
    <UserControl
    <TextBlock Text="Ciao"/>
</UserControl>
""";

    const string scenario2Expected = """
<UserControld xmlns="https://github.com/avaloniaui"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
                xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
                mc:Ignorable="d" d:DesignWidth="800" d:DesignHeight="450"
                x:Class="ACTest.Views.UserControl1">
    <UserControl
    <TextBlock Text="Ciao"/>
</UserControld>
""";

    [Theory]
    [Scenario("Adding nested Tag renames parent closing element  GitHub #338 ", nestingRenameExpected, nestingRenameSource)]
    [Scenario("Rename with invalid nested tag", scenario2Expected, scenario2Source)]
    public void SynchronizeStartAndEndTag(Scenario scenario)
    {
        AssertInsertion((string)scenario.Agrument, "d", (string)scenario.Expected);
    }
}
