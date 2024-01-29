using Avalonia;
using Avalonia.Controls;

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

    internal class InternalClass
    {
        public int PublicProperty { get; set; }

        internal int InternalProperty { get; set; }

        internal int MixedInternalProperty
        {
            get;
            private set;
        }

        protected int ProtectedProperty { get; set; }

        private int PrivateProperty { get; set; }
    }

    public class PublicWithInternalPropertiesClass
    {
        public int PublicProperty { get; set; }

        internal int InternalProperty { get; set; }

        internal int MixedInternalProperty
        {
            get;
            private set;
        }

        protected int ProtectedProperty { get; set; }

        private int PrivateProperty { get; set; }
    }

    public class MyButton : Button
    {
    }
}
