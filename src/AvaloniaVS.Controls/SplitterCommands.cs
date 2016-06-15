// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System.Windows.Input;

namespace AvaloniaVS.Controls
{
    public static class SplitterCommands
    {
        private static RoutedUICommand _expandCollapsePaneCommand;
        private static RoutedUICommand _splitVerticalCommand;
        private static RoutedUICommand _splitHorizontalCommand;
        private static RoutedUICommand _swapPanesCommand;
        private static RoutedUICommand _activateViewCommand;

        public static RoutedUICommand ActivateViewCommand
        {
            get
            {
                if (_activateViewCommand == null)
                {
                    _activateViewCommand = new RoutedUICommand("Activate View", "ActivateView", typeof(SplitterCommands));
                }

                return _activateViewCommand;
            }
        }

        public static RoutedUICommand ExpandCollapsePaneCommand
        {
            get
            {
                if (_expandCollapsePaneCommand == null)
                {
                    _expandCollapsePaneCommand = new RoutedUICommand("Expand/Collapse Pane", "ExpandCollapsePaneCommand", typeof(SplitterCommands));
                }
                return _expandCollapsePaneCommand;
            }
        }

        public static RoutedUICommand SplitVerticalCommand
        {
            get
            {
                if (_splitVerticalCommand == null)
                {
                    _splitVerticalCommand = new RoutedUICommand("Split Vertical", "SplitVerticalCommand", typeof(SplitterCommands));
                }
                return _splitVerticalCommand;
            }
        }

        public static RoutedUICommand SplitHorizontalCommand
        {
            get
            {
                if (_splitHorizontalCommand == null)
                {
                    _splitHorizontalCommand = new RoutedUICommand("Split Horizontal", "SplitHorizontalCommand", typeof(SplitterCommands));
                }
                return _splitHorizontalCommand;
            }
        }

        public static RoutedUICommand SwapPanesCommand
        {
            get
            {
                if (_swapPanesCommand == null)
                {
                    _swapPanesCommand = new RoutedUICommand("Swap Panes", "SwapPanesCommand", typeof(SplitterCommands));
                }
                return _swapPanesCommand;
            }
        }
    }
}