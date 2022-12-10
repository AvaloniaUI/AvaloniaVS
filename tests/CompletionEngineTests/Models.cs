using Avalonia;

namespace CompletionEngineTests.Models
{
    public class GenericBaseClass<T>
    {
        public T GenericProperty { get; set; }
    }

    public class EmptyClassDerivedFromGenericClassWithDouble : GenericBaseClass<double>
    {
        
    }

    public static class AttachedBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Avalonia.Controls.UserControl, bool>("IsEnabled",
                typeof(AttachedBehavior),
                false);

        public static bool GetIsEnabled(Avalonia.Controls.UserControl userControl) =>
            userControl.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(Avalonia.Controls.UserControl userControl, bool value) =>
            userControl.SetValue(IsEnabledProperty, value);
    }

    internal static class InternalAttachedBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Avalonia.Controls.UserControl, bool>("IsEnabled",
                typeof(InternalAttachedBehavior),
                false);

        public static bool GetIsEnabled(Avalonia.Controls.UserControl userControl) =>
            userControl.GetValue(IsEnabledProperty);

        public static void SetIsEnabled(Avalonia.Controls.UserControl userControl, bool value) =>
            userControl.SetValue(IsEnabledProperty, value);
    }
}
