// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AvaloniaVS.Controls.Internals;

namespace AvaloniaVS.Controls
{
    public class FocusableContentPresenter : ContentPresenter
    {
        static FocusableContentPresenter()
        {
            FocusableProperty.OverrideMetadata(typeof (FocusableContentPresenter), new FrameworkPropertyMetadata(BooleanBoxes.True));
            FocusVisualStyleProperty.OverrideMetadata(typeof(FocusableContentPresenter), new FrameworkPropertyMetadata(null));
        }

        protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
        {
            base.OnGotKeyboardFocus(e);

            if (e.NewFocus == this)
            {
                var content = this.Content as IInputElement;
                if (content != null)
                {
                    Keyboard.Focus(content);
                }
            }
        }
    }
}