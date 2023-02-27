#nullable enable
using System.Collections.Generic;
using Avalonia.Ide.CompletionEngine;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Language.Intellisense;

namespace AvaloniaVS.IntelliSense
{
    internal class XamIntellisenseFilter : IntellisenseFilter
    {
        static IDictionary<CompletionKind, XamIntellisenseFilter>? _cache;

        private XamIntellisenseFilter(CompletionKind kind, ImageMoniker moniker, string toolTip, string accessKey, string automationText, bool initialIsChecked = false, bool initialIsEnabled = true) :
            base(moniker, toolTip, accessKey, automationText, initialIsChecked, initialIsEnabled) =>
            Kind = kind;

        public CompletionKind Kind { get; }

        public static XamIntellisenseFilter Create(CompletionKind kind)
        {
            _cache ??= new Dictionary<CompletionKind, XamIntellisenseFilter>();
            if (_cache.TryGetValue(kind, out var filter) == false)
            {
                var name = kind.ToString();
                var accessKey = GetAccessKey(kind);
                filter = new XamIntellisenseFilter(kind
                    , Utils.ImageMonikerUtility.GetImage(kind)
                    , $"{name} (ALT+{accessKey})"
                    , accessKey
                    , name);
            }
            return filter;
        }

        private static string GetAccessKey(CompletionKind kind) =>
            kind switch
            {
                CompletionKind.None => "0",
                CompletionKind.Namespace => "N",
                CompletionKind.Class => "C",
                CompletionKind.VS_XMLNS => "X",
                CompletionKind.AttachedEvent => "A+E",
                CompletionKind.DataProperty => "D",
                CompletionKind.Event => "V",
                CompletionKind.AttachedProperty => "A+P",
                CompletionKind.MarkupExtension => "M",
                CompletionKind.Property => "P",
                CompletionKind.StaticProperty => "F",
                CompletionKind.Enum => "E",
                CompletionKind.TargetTypeClass => "T+C",
                _ => throw new System.NotImplementedException(),
            };

    }
}
