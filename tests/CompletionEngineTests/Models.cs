using System.Collections;
using System.Collections.Generic;
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

    public class MyListBoxItem : Avalonia.Controls.ListBoxItem
    {

    }

    public class MyListBox : Avalonia.Controls.Control
        , IList<MyListBoxItem>
    {
        public MyListBoxItem this[int index] { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }

        public int Count => throw new System.NotImplementedException();

        public bool IsReadOnly => throw new System.NotImplementedException();

        public void Add(MyListBoxItem item)
        {
            throw new System.NotImplementedException();
        }

        public void Clear()
        {
            throw new System.NotImplementedException();
        }

        public bool Contains(MyListBoxItem item)
        {
            throw new System.NotImplementedException();
        }

        public void CopyTo(MyListBoxItem[] array, int arrayIndex)
        {
            throw new System.NotImplementedException();
        }

        public IEnumerator<MyListBoxItem> GetEnumerator()
        {
            throw new System.NotImplementedException();
        }

        public int IndexOf(MyListBoxItem item)
        {
            throw new System.NotImplementedException();
        }

        public void Insert(int index, MyListBoxItem item)
        {
            throw new System.NotImplementedException();
        }

        public bool Remove(MyListBoxItem item)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new System.NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new System.NotImplementedException();
        }
    }
}
