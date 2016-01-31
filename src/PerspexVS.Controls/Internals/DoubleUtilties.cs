// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System;

namespace PerspexVS.Controls.Internals
{
    internal static class DoubleUtilties
    {
        internal const double MAX_DIFF = 0.1;

        public static bool AreClose(double value2, double value1)
        {
            return Math.Abs(value1 - value2) < MAX_DIFF;
        }
    }
}