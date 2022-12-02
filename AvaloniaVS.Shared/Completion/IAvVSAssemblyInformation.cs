using Avalonia.Ide.CompletionEngine.AssemblyMetadata;

namespace AvaloniaVS.Shared.Completion
{
    // NOTE: These types are added now to reuse the existing interfaces but allow
    //       needed additions later, if needed for a feature

    public interface IAvVSAssemblyInformation : IAssemblyInformation
    {

    }

    public interface IAvVSTypeInformation : ITypeInformation
    {

    }

    public interface IAvVSMethodInformation : IMethodInformation
    {

    }

    public interface IAvVSFieldInformation : IFieldInformation
    {

    }

    public interface IAvVSPropertyInformation : IPropertyInformation
    {

    }

    public interface IAvVSEventInformation : IEventInformation
    {

    }
}
