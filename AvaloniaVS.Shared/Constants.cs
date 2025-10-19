using System;

namespace AvaloniaVS;

internal static class Constants
{
    public const string PackageGuidString = "57F96315-438D-4BB2-9D03-F2527301A1D3"; // "865ba8d5-1180-4bf8-8821-345f72a4cb79";
    public static readonly Guid PackageGuid = new (PackageGuidString);
    public const string PackageName = "Avalonia-Lite Xaml Editor";
    public const string axaml = nameof(axaml);
    public const string xaml = nameof(xaml);
    public const string paml = nameof(paml);

    public const string AvaloviaFactoryEditorGuidString = @"4DC5468E-D6D5-4393-818C-BC82F347E8FD"; // @"6D5344A2-2FCD-49DE-A09D-6A14FD1B1224";
    public static readonly Guid AvaloviaFactoryEditorGuid = new (AvaloviaFactoryEditorGuidString);

    public const string AvaloniaCapability = nameof(Avalonia);
}
