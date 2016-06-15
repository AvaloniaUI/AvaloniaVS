// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AvaloniaVS.Controls.Internals;

namespace AvaloniaVS.Controls
{
    public class SplitterGrip : Thumb
    {
        private static readonly DependencyPropertyKey OrientationPropertyKey = DependencyProperty.RegisterReadOnly(
            "Orientation",
            typeof(Orientation),
            typeof(SplitterGrip),
            new FrameworkPropertyMetadata(Orientation.Horizontal));
        public static readonly DependencyProperty OrientationProperty = OrientationPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey IsCollapsedPropertyKey = DependencyProperty.RegisterReadOnly(
            "IsCollapsed",
            typeof(bool),
            typeof(SplitterGrip),
            new FrameworkPropertyMetadata(BooleanBoxes.False));

        public static readonly DependencyProperty IsCollapsedProperty = IsCollapsedPropertyKey.DependencyProperty;

        /// <summary>
        /// Initializes static members of the <see cref="SplitterGrip"/> class.
        /// </summary>
        static SplitterGrip()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SplitterGrip), new FrameworkPropertyMetadata(typeof(SplitterGrip)));
        }

        public Orientation Orientation
        {
            get { return (Orientation)GetValue(OrientationProperty); }
            internal set { SetValue(OrientationPropertyKey, value); }
        }

        public bool IsCollapsed
        {
            get { return (bool)GetValue(IsCollapsedProperty); }
            internal set { SetValue(IsCollapsedPropertyKey, BooleanBoxes.Box(value)); }
        }

        internal Popup Popup { get; set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Popup = GetTemplateChild("PART_Popup") as Popup;
        }
    }
}