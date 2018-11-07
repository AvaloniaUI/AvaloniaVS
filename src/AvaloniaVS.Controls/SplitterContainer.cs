// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AvaloniaVS.Controls.Internals;

namespace AvaloniaVS.Controls
{
    /// <summary>
    /// An enum that represents the views supported by the <see cref="SplitterContainer"/>.
    /// </summary>
    public enum SplitterViews
    {
        Design,
        Editor
    }

    /// <summary>
    /// 
    /// </summary>
    public sealed class SplitterContainer : FrameworkElement
    {
        private static Rect s_EmptyRect = new Rect();

        /// <summary>
        /// Identifies the <see cref="Orientation"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
            "Orientation",
            typeof(Orientation),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(Orientation.Horizontal,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                (o, e) =>
                {
                    var splitContainer = o as SplitterContainer;
                    if (splitContainer == null || splitContainer._grip == null)
                        return;

                    splitContainer._grip.Orientation = (Orientation)e.NewValue;
                    splitContainer.InvalidateOrientationProperties();
                }),
            IsValidOrientation);

        /// <summary>
        /// Identifies the <see cref="IsReversed"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty IsReversedProperty = DependencyProperty.Register(
            "IsReversed",
            typeof(bool),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(BooleanBoxes.False,
                FrameworkPropertyMetadataOptions.AffectsArrange,
                (o, args) =>
                {
                    var container = (SplitterContainer)o;
                    container.InvalidateActiveView(false);
                }));

        private static readonly DependencyPropertyKey IsDesignerActivePropertyKey = DependencyProperty.RegisterReadOnly(
            "IsDesignerActive",
            typeof (bool),
            typeof (SplitterContainer),
            new FrameworkPropertyMetadata(BooleanBoxes.False));

        /// <summary>
        /// Identifies the <see cref="IsDesignerActive"/> read-only dependency property.
        /// </summary>
        public static readonly DependencyProperty IsDesignerActiveProperty = IsDesignerActivePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey IsEditorActivePropertyKey = DependencyProperty.RegisterReadOnly(
            "IsEditorActive",
            typeof(bool),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(BooleanBoxes.False));

        /// <summary>
        /// Identifies the <see cref="IsEditorActive"/> read-only dependency property.
        /// </summary>
        public static readonly DependencyProperty IsEditorActiveProperty = IsEditorActivePropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey IsCollapsedPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsCollapsed",
            typeof(bool),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(
                BooleanBoxes.False,
                (o, args) =>
                {
                    var splitContainer = o as SplitterContainer;
                    if (splitContainer?._grip == null)
                        return;

                    splitContainer.OnIsCollapsedChanged();
                }));

        /// <summary>
        /// Identifies the <see cref="IsCollapsed"/> read-only property.
        /// </summary>
        public static readonly DependencyProperty IsCollapsedProperty = IsCollapsedPropertyKey.DependencyProperty;

        /// <summary>
        /// Identifies the <see cref="DesignerHeader"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DesignerHeaderProperty = DependencyProperty.Register(
            "DesignerHeader",
            typeof(object),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Identifies the <see cref="ExtraPanel"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty ExtraPanelProperty = DependencyProperty.Register(
            "ExtraPanel",
            typeof(object),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(null));

        /// <summary>
        /// Identifies the <see cref="EditorHeader"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty EditorHeaderProperty = DependencyProperty.Register(
            "EditorHeader",
            typeof(object),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(null));

        /// <summary>
        /// IsHorizontal Read-Only Dependency Property
        /// </summary>
        private static readonly DependencyPropertyKey IsHorizontalPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsHorizontal",
            typeof(bool),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(BooleanBoxes.False));

        public static readonly DependencyProperty IsHorizontalProperty
            = IsHorizontalPropertyKey.DependencyProperty;

        /// <summary>
        /// IsVertical Read-Only Dependency Property
        /// </summary>
        private static readonly DependencyPropertyKey IsVerticalPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsVertical",
            typeof(bool),
            typeof(SplitterContainer),
            new FrameworkPropertyMetadata(BooleanBoxes.False));

        public static readonly DependencyProperty IsVerticalProperty
            = IsVerticalPropertyKey.DependencyProperty;

        /// <summary>
        /// Identifies the <see cref="ActiveViewChanged"/> routed event.
        /// </summary>
        public static readonly RoutedEvent ActiveViewChangedEvent = EventManager.RegisterRoutedEvent(
            "ActiveViewChanged",
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SplitterContainer));

        private FrameworkElement _designerContainer;
        private FrameworkElement _editorContainer;
        private SplitterGrip _grip;
        private readonly UIElementCollection _visualChildren;
        private double _gripOffset = double.NaN; // in percent
        private double _previousGripOffset;

        /// <summary>
        /// a flag that indicates that the view must be reversed after exiting the collapse
        /// mode, due to the user swaping views.
        /// </summary>
        private bool _reverseAfterCollapse = false;

        /// <summary>
        /// Initializes static members of the <see cref="SplitterContainer"/> class.
        /// </summary>
        static SplitterContainer()
        {
            ClipToBoundsProperty.OverrideMetadata(typeof(SplitterContainer), new FrameworkPropertyMetadata(BooleanBoxes.True));
            InitCommands();
            InitEventHandlers();
        }

        /// <summary>
        /// Initializes instance members of the <see cref="SplitterContainer"/> class.
        /// </summary>
        public SplitterContainer()
        {
            InitSplitterHandle();

            _visualChildren = new UIElementCollection(this, this)
            {
                _grip
            };
            InvalidateOrientationProperties();
        }

        /// <summary>
        /// Gets or sets the <see cref="System.Windows.Controls.Orientation"/> of the control.
        /// </summary>
        /// <value>The default value is <see cref="System.Windows.Controls.Orientation.Horizontal"/></value>
        [Bindable(true)]
        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates whether <see cref="Designer"/> and <see cref="Editor"/> are arranged in reverse.
        /// The default order is <see cref="Designer"/> at the top/left and the <see cref="Editor"/> is bottom/right.
        /// </summary>
        [Bindable(true)]
        public bool IsReversed
        {
            get { return (bool)GetValue(IsReversedProperty); }
            set { SetValue(IsReversedProperty, BooleanBoxes.Box(value)); }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="Designer"/> is the currently active view.
        /// </summary>
        public bool IsDesignerActive
        {
            get { return (bool)GetValue(IsDesignerActiveProperty); }
            private set { SetValue(IsDesignerActivePropertyKey, BooleanBoxes.Box(value)); }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="Editor"/> is the currently active view.
        /// </summary>
        public bool IsEditorActive
        {
            get { return (bool)GetValue(IsEditorActiveProperty); }
            private set { SetValue(IsEditorActivePropertyKey, BooleanBoxes.Box(value)); }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="SplitterContainer"/> is collapsed
        /// and only a single view is displayed at once.
        /// </summary>
        public bool IsCollapsed
        {
            get { return (bool)GetValue(IsCollapsedProperty); }
            internal set { SetValue(IsCollapsedPropertyKey, BooleanBoxes.Box(value)); }
        }

        /// <summary>
        /// Gets or sets the content of the <see cref="Designer"/> tab-item header.
        /// </summary>
        [Bindable(true)]
        public object DesignerHeader
        {
            get { return (object)GetValue(DesignerHeaderProperty); }
            set { SetValue(DesignerHeaderProperty, value); }
        }

        /// <summary>
        /// Gets or sets the content of the extra panel
        /// </summary>
        [Bindable(true)]
        public object ExtraPanel
        {
            get { return (object)GetValue(ExtraPanelProperty); }
            set { SetValue(ExtraPanelProperty, value); }
        }


        /// <summary>
        /// Gets or sets the content of the <see cref="Editor"/> tab-item header.
        /// </summary>
        [Bindable(true)]
        public object EditorHeader
        {
            get { return (object)GetValue(EditorHeaderProperty); }
            set { SetValue(EditorHeaderProperty, value); }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="SplitterContainer"/> views are arranged horizontally.
        /// </summary>
        public bool IsHorizontal
        {
            get { return (bool)GetValue(IsHorizontalProperty); }
            private set { SetValue(IsHorizontalPropertyKey, BooleanBoxes.Box(value)); }
        }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="SplitterContainer"/> views are arranged vertically.
        /// </summary>
        public bool IsVertical
        {
            get { return (bool)GetValue(IsVerticalProperty); }
            private set { SetValue(IsVerticalPropertyKey, BooleanBoxes.Box(value)); }
        }

        public FrameworkElement Designer
        {
            get { return _designerContainer; }
            set
            {
                if (_designerContainer == value)
                    throw new NotSupportedException("");

                UpdateView(_designerContainer, value);
                _designerContainer = value;
            }
        }

        public FrameworkElement Editor
        {
            get { return _editorContainer; }
            set
            {
                if (_editorContainer == value)
                    throw new NotSupportedException("");

                UpdateView(_editorContainer, value);
                _editorContainer = value;
            }
        }

        private FrameworkElement InternalView1
        {
            get { return IsReversed ? Editor : Designer; }
        }

        private FrameworkElement InternalView2
        {
            get { return IsReversed ? Designer : Editor; }
        }

        private double Density { get; set; }

        private double InternalOffset
        {
            get
            {
                return double.IsNaN(_gripOffset) ? 50 : _gripOffset;
            }
        }

        protected override int VisualChildrenCount
        {
            get
            {
                if (_visualChildren == null)
                    return 0;

                return _visualChildren.Count;
            }
        }

        /// <summary>
        /// Occurs when the active view changes, the active view is the one that has keybaord focus.
        /// </summary>
        public event RoutedEventHandler ActiveViewChanged
        {
            add { AddHandler(ActiveViewChangedEvent, value); }
            remove { RemoveHandler(ActiveViewChangedEvent, value); }
        }

        private static bool IsValidOrientation(object value)
        {
            var orientation = (Orientation)value;
            return orientation == Orientation.Horizontal || orientation == Orientation.Vertical;
        }

        private void OnIsCollapsedChanged()
        {
            _grip.IsCollapsed = IsCollapsed;

            if (IsCollapsed)
            {
                _reverseAfterCollapse = false;
                ActivateView(SplitterViews.Design);
            }

            if (_reverseAfterCollapse)
            {
                IsReversed = !IsReversed;
            }

            InvalidateFocus();
        }

        /// <summary>
        /// A static helper method to raise the <see cref="ActiveViewChanged"/>
        /// event on a target <see cref="SplitterContainer"/>.
        /// </summary>
        private static void RaiseActiveViewChangedEvent(SplitterContainer target)
        {
            if (target == null)
            {
                return;
            }

            var rags = new RoutedEventArgs { RoutedEvent = ActiveViewChangedEvent };
            target.RaiseEvent(rags);
        }

        private static void InitEventHandlers()
        {
            EventManager.RegisterClassHandler(typeof(SplitterContainer), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnGotKeyboardFocusHandler), true);
        }

        private static void OnGotKeyboardFocusHandler(object sender, KeyboardFocusChangedEventArgs e)
        {
            var container = (SplitterContainer)sender;
            container.OnGotKeyboardFocusPrivate(e);
        }

        private void OnGotKeyboardFocusPrivate(KeyboardFocusChangedEventArgs e)
        {
            if (e.NewFocus is Hyperlink)
                return;
            var focus = e.NewFocus as DependencyObject;
            if (focus == null)
                return;


            var view1 = InternalView1;
            if (view1 != null && view1.IsAncestorOf(focus))
            {
                IsDesignerActive = true;
                IsEditorActive = false;
            }

            var view2 = InternalView2;
            if (view2 != null && view2.IsAncestorOf(focus))
            {
                IsEditorActive = true;
                IsDesignerActive = false;
            }
        }

        private static void InitCommands()
        {
            CommandManager.RegisterClassCommandBinding(typeof(SplitterContainer),
                new CommandBinding(SplitterCommands.ExpandCollapsePaneCommand, OnExpandCollapseExecuted));

            CommandManager.RegisterClassCommandBinding(typeof(SplitterContainer),
                new CommandBinding(SplitterCommands.SplitHorizontalCommand, OnSplitHorizontalExecuted));

            CommandManager.RegisterClassCommandBinding(typeof(SplitterContainer),
                new CommandBinding(SplitterCommands.SplitVerticalCommand, OnSplitVerticalExecuted));

            CommandManager.RegisterClassCommandBinding(typeof(SplitterContainer),
                new CommandBinding(SplitterCommands.SwapPanesCommand, OnSwapPanesExecuted, OnSwapPanesCanExecute));

            CommandManager.RegisterClassCommandBinding(typeof(SplitterContainer), new CommandBinding(SplitterCommands.ActivateViewCommand, OnActivateView));
        }

        private static void OnActivateView(object sender, ExecutedRoutedEventArgs e)
        {
            if (!(e.Parameter is SplitterViews))
            {
                return;
            }

            var container = (SplitterContainer)sender;
            container.ActivateView((SplitterViews)e.Parameter);
        }

        FrameworkElement _toActivate;
        private void ActivateView(SplitterViews views)
        {
            if (views == SplitterViews.Design)
            {
                if (IsCollapsed)
                {
                    _reverseAfterCollapse = false;
                }

                MoveFocusTo(InternalView1);
                _toActivate = InternalView1;
                InvalidateActiveView();
            }
            else
            {
                if (IsCollapsed)
                {
                    _reverseAfterCollapse = true;
                }

                MoveFocusTo(InternalView2);
                _toActivate = InternalView2;
                InvalidateActiveView();
            }

            // in case we're collapsed, we need to invalidate the arrange to make
            // the currently activated view visible.
            if (IsCollapsed)
            {
                InvalidateArrange();
            }
        }

        private static void OnSwapPanesCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            var splitContainer = sender as SplitterContainer;
            if (splitContainer == null)
                return;

            e.CanExecute = !splitContainer.IsCollapsed;
        }

        private static void OnSplitHorizontalExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var splitContainer = sender as SplitterContainer;
            splitContainer?.SwitchOrientation(Orientation.Horizontal);
        }

        private static void OnSplitVerticalExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var splitContainer = sender as SplitterContainer;
            splitContainer?.SwitchOrientation(Orientation.Vertical);
        }

        private void SwitchOrientation(Orientation orientation)
        {
            if (IsCollapsed)
            {
                _gripOffset = 50;
                IsCollapsed = false;
            }

            Orientation = orientation;
        }

        private static void OnSwapPanesExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var splitContainer = sender as SplitterContainer;
            splitContainer?.OnSwapPanes();
        }

        private static void OnExpandCollapseExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            var splitContainer = sender as SplitterContainer;
            splitContainer?.OnExpandCollapse();
        }

        private void OnExpandCollapse()
        {
            if (!IsCollapsed)
            {
                _previousGripOffset = double.IsNaN(_gripOffset) ? 50 : _gripOffset;
                _gripOffset = 100;
                IsCollapsed = true;
            }
            else
            {
                if (DoubleUtilties.AreClose(_previousGripOffset, 0.0))
                {
                    _previousGripOffset = 50;
                }

                _gripOffset = _previousGripOffset;
                IsCollapsed = false;
            }
        }

        private void OnSwapPanes()
        {
            IsReversed = !IsReversed;
        }

        public void SwapActiveView()
        {
            ActivateView( IsDesignerActive ? SplitterViews.Editor : SplitterViews.Design);
        }

        private void InvalidateOrientationProperties()
        {
            IsHorizontal = Orientation == Orientation.Horizontal;
            IsVertical = Orientation == Orientation.Vertical;
        }

        private void InitSplitterHandle()
        {
            _grip = new SplitterGrip
            {
                Orientation = Orientation,
                IsCollapsed = IsCollapsed
            };
            _grip.DragDelta += OnGripDragDelta;
            _grip.DragCompleted += OnDragCompleted;
        }

        private void OnGripDragDelta(object sender, DragDeltaEventArgs e)
        {
            var isHorizontal = Orientation == Orientation.Horizontal;
            var change = isHorizontal ? e.VerticalChange : e.HorizontalChange;

            if (_grip.Popup != null)
            {
                InitializePopup(_grip.Popup);
                InvalidatePopupPlacement(_grip.Popup, change);
            }

            e.Handled = true;
        }

        private void InvalidatePopupPlacement(Popup popup, double change)
        {
            var rect = LayoutInformation.GetLayoutSlot(_grip);
            var isHorizontal = Orientation == Orientation.Horizontal;
            var offset = (isHorizontal ? rect.Top : rect.Left) + change;
            var maxOffset = isHorizontal ? (this.ActualHeight - 5) : (this.ActualWidth - 5);

            offset = offset + 5;
            offset = Math.Max(0, offset);
            offset = Math.Min(offset, maxOffset);

            if (isHorizontal)
                popup.VerticalOffset = offset;
            else
                popup.HorizontalOffset = offset;
        }

        private void InitializePopup(Popup popup)
        {
            if (popup == null || popup.IsOpen)
                return;

            popup.Placement = PlacementMode.Relative;
            popup.PlacementTarget = this;
            popup.IsOpen = true;
        }

        private void ComputeOffset(double change)
        {
            var offset = InternalOffset;
            offset += Density * change;

            CoerceOffset(ref offset);

            _gripOffset = offset;
            InvalidateMeasure();
            InvalidateArrange();
        }

        private void CoerceOffset(ref double offset)
        {
            var isCollapsed = false;

            if (offset < 0.0)
            {
                offset = 100;
                isCollapsed = true;
            }
            else if (offset > 100)
            {
                offset = 100;
                isCollapsed = true;
            }

            IsCollapsed = isCollapsed;
        }

        private void OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            var change = Orientation == Orientation.Horizontal ? e.VerticalChange : e.HorizontalChange;
            ComputeOffset(change);
            _grip.Popup.IsOpen = false;
        }

        private void UpdateView(FrameworkElement oldView, FrameworkElement newView)
        {
            if (oldView == newView)
                return;

            if (oldView != null)
                _visualChildren.Remove(oldView);

            if (newView != null)
                _visualChildren.Add(newView);

            InvalidateMeasure();
            InvalidateArrange();
        }

        protected override Visual GetVisualChild(int index)
        {
            if (_visualChildren == null || (index < 0 || index > (_visualChildren.Count - 1)))
                throw new ArgumentOutOfRangeException(nameof(index));

            return _visualChildren[index];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var isHorizontal = Orientation == Orientation.Horizontal;

            if (isHorizontal && (double.IsPositiveInfinity(availableSize.Height) || double.IsInfinity(availableSize.Height)))
                return base.MeasureOverride(availableSize);

            if (!isHorizontal && (double.IsPositiveInfinity(availableSize.Width) || double.IsInfinity(availableSize.Width)))
                return base.MeasureOverride(availableSize);

            double startPos, handleSize, designerSize, editorSize;
            ComputePartsSizes(availableSize, isHorizontal, out designerSize, out editorSize, out handleSize, out startPos);

            var designer = InternalView1;
            designer?.Measure(isHorizontal
                ? new Size(availableSize.Width, designerSize)
                : new Size(designerSize, availableSize.Height));

            var editor = InternalView2;
            editor?.Measure(isHorizontal
                ? new Size(availableSize.Width, editorSize)
                : new Size(editorSize, availableSize.Height));

            _grip.Measure(availableSize);
            return _grip.DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // initialization
            var isHorizontal = Orientation == Orientation.Horizontal;
            var designer = InternalView1;
            var editor = InternalView2;

            double designerSize, editorSize, gripSize, startPos;
            ComputePartsSizes(finalSize, isHorizontal, out designerSize, out editorSize, out gripSize, out startPos);


            var finalRect = isHorizontal
                ? new Rect
                {
                    Location = new Point(0, startPos),
                    Width = finalSize.Width
                }
                : new Rect
                {
                    Location = new Point(startPos, 0),
                    Height = finalSize.Height
                };


            if (IsCollapsed)
            {
                // in case we're collapsed, the view arranged is the one active.

                // Designer
                if (isHorizontal)
                    finalRect.Height = designerSize;
                else
                    finalRect.Width = designerSize;
                designer?.Arrange(IsDesignerActive ? finalRect : s_EmptyRect);

                // Editor
                editor?.Arrange(IsEditorActive ? finalRect : s_EmptyRect);

                // Grip
                if (isHorizontal)
                {
                    finalRect.Y += designerSize;
                    finalRect.Height = gripSize;
                }
                else
                {
                    finalRect.X += designerSize;
                    finalRect.Width = gripSize;
                }
                _grip.Arrange(finalRect);
            }
            else
            {
                // designer
                if (isHorizontal)
                    finalRect.Height = designerSize;
                else
                    finalRect.Width = designerSize;
                designer?.Arrange(finalRect);

                // grip
                if (isHorizontal)
                {
                    finalRect.Y += designerSize;
                    finalRect.Height = gripSize;
                }
                else
                {
                    finalRect.X += designerSize;
                    finalRect.Width = gripSize;
                }
                _grip.Arrange(finalRect);

                // editor
                if (isHorizontal)
                {
                    finalRect.Y += gripSize;
                    finalRect.Height = editorSize;
                }
                else
                {
                    finalRect.X += gripSize;
                    finalRect.Width = editorSize;
                }
                editor?.Arrange(finalRect);
            }

            return finalSize;
        }

        private void ComputePartsSizes(Size arrangeSize,
            bool isHorizontal,
            out double designerSize,
            out double editorSize,
            out double handleSize,
            out double startPos)
        {
            var offset = InternalOffset;
            startPos = 0;

            double length;
            if (isHorizontal)
            {
                length = arrangeSize.Height;
                handleSize = _grip.DesiredSize.Height;
            }
            else
            {
                length = arrangeSize.Width;
                handleSize = _grip.DesiredSize.Width;
            }

            var remainingLength = length - handleSize;
            if (IsCollapsed) // if either view is collapsed
            {
                designerSize = editorSize = remainingLength;
            }
            else
            {
                designerSize = remainingLength * (offset / 100);
                editorSize = remainingLength - designerSize;
            }

            Density = 100 / remainingLength;
        }

        private void InvalidateFocus()
        {
            if (!IsCollapsed)
                return;

            var activeView = InternalView1;
            if (activeView != null && activeView.IsKeyboardFocusWithin)
                MoveFocusTo(activeView);
        }

        private void InvalidateActiveView(bool raiseEvent = true)
        {
            var view1 = InternalView1;
            IsDesignerActive = view1 != null && view1 == _toActivate;

            var view2 = InternalView2;
            IsEditorActive = view2 != null && view2 == _toActivate;

            if (raiseEvent)
            {
                RaiseActiveViewChangedEvent(this);
            }
        }

        private static void MoveFocusTo(FrameworkElement element)
        {
            if (element != null)
                Keyboard.Focus(element);
        }

        public void Collapse(SplitterViews activeView)
        {
            OnExpandCollapse();

            _requstedActiveView = activeView;
            Loaded += OnContainerLoaded;
        }

        // this is a hack, I should stop relying on keyboard focus
        // to know whether the designer or editor is active, for example
        // during initialization, keyboard focus is not present 
        // and the active view is not properly set.
        private SplitterViews _requstedActiveView;
        private void OnContainerLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            ActivateView(_requstedActiveView);
            Loaded -= OnContainerLoaded;
        }
    }
}