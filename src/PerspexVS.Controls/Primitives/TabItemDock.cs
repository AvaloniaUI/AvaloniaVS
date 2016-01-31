// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System;

namespace PerspexVS.Controls.Primitives
{
    [Flags]
    public enum TabItemDock
    {
        None = 0,
        Bottom = 1,
        Top = Bottom << 1,
        Left = Top << 1,
        Right = Left << 1
    }
}