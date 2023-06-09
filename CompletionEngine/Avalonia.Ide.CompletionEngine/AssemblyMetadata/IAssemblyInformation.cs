using System.Collections.Generic;
using System.IO;
#nullable enable

namespace Avalonia.Ide.CompletionEngine.AssemblyMetadata;

public interface IAssemblyInformation
{
    string Name { get; }
    IEnumerable<ITypeInformation> Types { get; }
    IEnumerable<ICustomAttributeInformation> CustomAttributes { get; }
    IEnumerable<string> ManifestResourceNames { get; }
    Stream GetManifestResourceStream(string name);
    IEnumerable<string> InternalsVisibleTo { get; }
    string AssemblyName { get; }
    string PublicKey { get; }
}

public interface ICustomAttributeInformation
{
    string TypeFullName { get; }
    IList<IAttributeConstructorArgumentInformation> ConstructorArguments { get; }
}

public interface IAttributeConstructorArgumentInformation
{
    object? Value { get; }
}

public interface ITypeInformation
{
    string FullName { get; }
    string Name { get; }
    string Namespace { get; }
    string AssemblyQualifiedName { get; }

    ITypeInformation? GetBaseType();
    IEnumerable<IMethodInformation> Methods { get; }
    IEnumerable<IPropertyInformation> Properties { get; }
    IEnumerable<IEventInformation> Events { get; }
    IEnumerable<IFieldInformation> Fields { get; }
    IEnumerable<string> Pseudoclasses { get; }
    IEnumerable<(ITypeInformation Type,string Name)> TemplateParts { get; }

    bool IsEnum { get; }
    bool IsStatic { get; }
    bool IsInterface { get; }
    bool IsPublic { get; }
    bool IsGeneric { get; }
    bool IsAbstract { get; }
    bool IsInternal { get; }
    IEnumerable<string> EnumValues { get; }
    IEnumerable<ITypeInformation> NestedTypes { get; }
}

public interface IMethodInformation
{
    bool IsStatic { get; }
    bool IsPublic { get; }
    string Name { get; }
    IList<IParameterInformation> Parameters { get; }
    string ReturnTypeFullName { get; }
    string QualifiedReturnTypeFullName { get; }
}

public interface IFieldInformation
{
    bool IsStatic { get; }
    bool IsPublic { get; }
    string Name { get; }
    string ReturnTypeFullName { get; }
    string QualifiedTypeFullName { get; }
    bool IsRoutedEvent { get; }
}

public interface IParameterInformation
{
    string TypeFullName { get; }
    string QualifiedTypeFullName { get; }
}

public interface IPropertyInformation
{
    bool IsStatic { get; }
    bool IsPublic { get; }
    bool IsInternal { get; }
    bool HasPublicSetter { get; }
    bool HasPublicGetter { get; }
    string TypeFullName { get; }
    string QualifiedTypeFullName { get; }
    string Name { get; }
    bool IsVisbleTo(IAssemblyInformation assembly);
}

public interface IEventInformation
{
    string Name { get; }
    string TypeFullName { get; }
    string QualifiedTypeFullName { get; }
    bool IsPublic { get; }
    bool IsInternal { get; }
}
