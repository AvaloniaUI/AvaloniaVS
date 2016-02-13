// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See license.md file in the project root for full license information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PerspexVS.Controls.Internals;

namespace PerspexVS.Controls.Primitives
{
    public class TabItemBorder : Decorator
    {
        private Pen _cachedBoderPen;
        private Geometry _cachedBackgroundGeometry;
        private Geometry _cachedBorderGeometry;

        /// <summary>
        /// Identifies the <see cref="Dock"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty DockProperty = DependencyProperty.Register(
            "Dock",
            typeof (TabItemDock),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(TabItemDock.None,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Identifies the <see cref="CornerRadius"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            "CornerRadius",
            typeof (double),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                (o, args) =>
                {
                    var border = (TabItemBorder) o;
                    border.InvalidateGeometries();
                }));

        /// <summary>
        /// Identifies the <see cref="BorderBrush"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty BorderBrushProperty = DependencyProperty.Register(
            "BorderBrush",
            typeof (Brush),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender,
                (o, args) =>
                {
                    var border = (TabItemBorder) o;
                    border._cachedBoderPen = null;
                }));

        /// <summary>
        /// Identifies the <see cref="BorderThickness"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty BorderThicknessProperty = DependencyProperty.Register(
            "BorderThickness",
            typeof (double),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(
                0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                (o, args) =>
                {
                    var border = (TabItemBorder) o;
                    border._cachedBoderPen = null;
                    border.InvalidateGeometries();
                }),
            value =>
            {
                // you can't have a thickness lest than 0
                // we don't coerce we validate.
                var thickness = (double) value;
                return thickness >= 0;
            });

        /// <summary>
        /// Identifies the <see cref="Padding"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty PaddingProperty = DependencyProperty.Register(
            "Padding",
            typeof (Thickness),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(
                new Thickness(),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Identifies the <see cref="Background"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register(
            "Background",
            typeof (Brush),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Identifies the <see cref="LeftSlopeOffset"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty LeftSlopeOffsetProperty = DependencyProperty.Register(
            "LeftSlopeOffset",
            typeof (double),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnSlopeOffsetValueChanged,
                OnCoerceSlopeOffsetValue));

        /// <summary>
        /// Identifies the <see cref="RightSlopeOffset"/> dependency property.
        /// </summary>
        public static readonly DependencyProperty RightSlopeOffsetProperty = DependencyProperty.Register(
            "RightSlopeOffset",
            typeof (double),
            typeof (TabItemBorder),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnSlopeOffsetValueChanged,
                OnCoerceSlopeOffsetValue));

        /// <summary>
        /// Gets or sets a value that indicates the location of the border open side.
        /// </summary>
        [Bindable(true)]
        public TabItemDock Dock
        {
            get { return (TabItemDock) GetValue(DockProperty); }
            set { SetValue(DockProperty, value); }
        }

        /// <summary>
        /// Gets or sets the corner radius, our border is open on one side indicates by the <see cref="Dock"/> value.
        /// so even the start and the end of the border are curved using this value.
        /// </summary>
        [Bindable(true)]
        public double CornerRadius
        {
            get { return (double) GetValue(CornerRadiusProperty); }
            set { SetValue(CornerRadiusProperty, value); }
        }

        /// <summary>
        /// Gets or sets the brush used to render the border.
        /// </summary>
        [Bindable(true)]
        public Brush BorderBrush
        {
            get { return (Brush) GetValue(BorderBrushProperty); }
            set { SetValue(BorderBrushProperty, value); }
        }

        /// <summary>
        /// Gets or sets the thickness of the border.
        /// </summary>
        [Bindable(true)]
        public double BorderThickness
        {
            get { return (double) GetValue(BorderThicknessProperty); }
            set { SetValue(BorderThicknessProperty, value); }
        }

        /// <summary>
        /// Gets or sets the padding of the border child.
        /// </summary>
        [Bindable(true)]
        public Thickness Padding
        {
            get { return (Thickness) GetValue(PaddingProperty); }
            set { SetValue(PaddingProperty, value); }
        }

        /// <summary>
        /// Gets or sets the background brush.
        /// </summary>
        public Brush Background
        {
            get { return (Brush) GetValue(BackgroundProperty); }
            set { SetValue(BackgroundProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates the distance of the left border from the edge in pixels.
        /// </summary>
        [Bindable(true)]
        public double LeftSlopeOffset
        {
            get { return (double) GetValue(LeftSlopeOffsetProperty); }
            set { SetValue(LeftSlopeOffsetProperty, value); }
        }

        /// <summary>
        /// Gets or sets a value that indicates the distance of the right border from the edge in pixels.
        /// </summary>
        [Bindable(true)]
        public double RightSlopeOffset
        {
            get { return (double) GetValue(RightSlopeOffsetProperty); }
            set { SetValue(RightSlopeOffsetProperty, value); }
        }

        private static object OnCoerceSlopeOffsetValue(DependencyObject o, object value)
        {
            var baseValue = (double) value;
            if (baseValue < 0.0)
            {
                return 0.0;
            }

            return value;
        }

        private static void OnSlopeOffsetValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            var border = (TabItemBorder) o;
            border.InvalidateGeometries();
        }

        protected override Size MeasureOverride(Size constraint)
        {
            var child = Child;
            var borderThickness = BorderThickness;
            var padding = Padding;
            var cornerRadius = CornerRadius;
            var dock = Dock;
            var isHorizontal = (dock & TabItemDock.Bottom) == TabItemDock.Bottom || (dock & TabItemDock.Top) == TabItemDock.Top;

            var reservedWidth = isHorizontal
                ? (padding.Left + padding.Right) + (borderThickness * 2) + (cornerRadius * 2) + LeftSlopeOffset + RightSlopeOffset
                : (padding.Left + padding.Right) + (borderThickness);

            var reservedHeight = isHorizontal
                ? (padding.Top + padding.Bottom) + (borderThickness)
                : (padding.Top + padding.Bottom) + (borderThickness * 2) + (cornerRadius * 2) + LeftSlopeOffset + RightSlopeOffset;

            if (child == null)
            {
                return new Size(reservedWidth, reservedHeight);
            }

            var availableWidth = Math.Max(0.0, constraint.Width - reservedWidth);
            var availableHeight = Math.Max(0.0, constraint.Height - reservedHeight);

            child.Measure(new Size(availableWidth, availableHeight));
            var desiredSize = child.DesiredSize;
            return new Size(desiredSize.Width + reservedWidth, desiredSize.Height + reservedHeight);
        }

        protected override Size ArrangeOverride(Size arrangeSize)
        {
            var child = Child;

            if (child != null)
            {
                var childRect = new Rect(arrangeSize);
                var borderThickness = BorderThickness;
                var padding = Padding;
                var cornerRadius = CornerRadius;
                var dock = Dock;
                var isHorizontal = (dock & TabItemDock.Bottom) == TabItemDock.Bottom || (dock & TabItemDock.Top) == TabItemDock.Top;

                var reservedWidth = isHorizontal
                    ? (padding.Left + padding.Right) + (borderThickness * 2) + (cornerRadius * 2) + LeftSlopeOffset + RightSlopeOffset
                    : (padding.Left + padding.Right) + (borderThickness);

                var reservedHeight = isHorizontal
                    ? (padding.Top + padding.Bottom) + (borderThickness)
                    : (padding.Top + padding.Top) + (borderThickness * 2) + (cornerRadius * 2) + LeftSlopeOffset + RightSlopeOffset;

                childRect.Width = Math.Max(0.0, arrangeSize.Width - reservedWidth);
                childRect.Height = Math.Max(0.0, arrangeSize.Height - reservedHeight);


                childRect.X = isHorizontal
                    ? padding.Left + borderThickness + cornerRadius + LeftSlopeOffset
                    : padding.Left + borderThickness;

                childRect.Y = isHorizontal
                    ? padding.Top + borderThickness
                    : padding.Top + borderThickness + cornerRadius + LeftSlopeOffset;

                child.Arrange(childRect);
            }

            InvalidateGeometries();

            return arrangeSize;
        }

        private void InvalidateGeometries()
        {
            InvalidateCache();
        }

        protected override void OnRender(DrawingContext dc)
        {
            EnsureBorderPen();
            Render(dc);
        }

        private void EnsureBorderPen()
        {
            if (_cachedBoderPen != null) return;
            _cachedBoderPen = new Pen(BorderBrush, BorderThickness);
        }


        public void InvalidateCache()
        {
            // they will be re-generated on the render pass
            _cachedBackgroundGeometry = null;
            _cachedBorderGeometry = null;
        }

        public void Render(DrawingContext dc)
        {
            var brush = Background;
            if (brush != null)
            {
                EnsureBackgroundGeometry(dc);
                dc.DrawGeometry(brush, null, _cachedBackgroundGeometry);
            }

            if (BorderBrush != null)
            {
                EnsureBorderGeometry(dc);
                dc.DrawGeometry(null, _cachedBoderPen, _cachedBorderGeometry);
            }
        }

        private void EnsureBackgroundGeometry(DrawingContext dc)
        {
            if (_cachedBackgroundGeometry == null)
            {
                _cachedBackgroundGeometry = GenerateGeometry(RenderSize, CornerRadius, true, dc);
            }
        }

        private void EnsureBorderGeometry(DrawingContext dc)
        {
            if (_cachedBorderGeometry == null)
            {
                _cachedBorderGeometry = GenerateGeometry(RenderSize, CornerRadius, false, dc);
            }
        }

        // will return the origin point for the various cases.
        private Point GetP0(Rect rect, bool isBackground)
        {
            var dock = Dock;
            var hasCornerRadius = CornerRadius > 0;
            var hbt = BorderThickness * 0.5;

            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Left - (hasCornerRadius ? hbt : 0), rect.Bottom + (isBackground || hasCornerRadius ? 0 : hbt));
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Left - (hasCornerRadius ? hbt : 0), rect.Top - (isBackground || hasCornerRadius ? 0 : hbt));
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Left + (isBackground || hasCornerRadius ? 0 : hbt), rect.Top - (hasCornerRadius ? hbt : 0.0));
            }

            var isRight = (dock & TabItemDock.Right) == TabItemDock.Right;
            Debug.Assert(isRight);
            return new Point(rect.Right + (isBackground || hasCornerRadius ? 0 : hbt), rect.Top - (hasCornerRadius ? hbt : 0.0));
        }

        private Point GetP1(Rect rect, double cr)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Left + cr, rect.Bottom - cr);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Left + cr, rect.Top + cr);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Left + cr, rect.Top + cr);
            }

            return new Point(rect.Right - cr, rect.Top + cr);
        }

        private Point GetP2(Rect rect, double cr, double leftSlopeOffset)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Left + cr + leftSlopeOffset, rect.Top + cr);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Left + cr + leftSlopeOffset, rect.Bottom - cr);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Right - cr, rect.Top + leftSlopeOffset + cr);
            }

            return new Point(rect.Left + cr, rect.Top + leftSlopeOffset + cr);
        }

        private Point GetP3(Rect rect, double cr, double leftSlopeOffset)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Left + leftSlopeOffset + cr * 2, rect.Top);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Left + leftSlopeOffset + cr * 2, rect.Bottom);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Right, rect.Top + leftSlopeOffset + cr * 2);
            }

            return new Point(rect.Left, rect.Top + leftSlopeOffset + cr * 2);
        }

        private Point GetP4(Rect rect, double cr, double rightSlopeOffset)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Right - rightSlopeOffset - (cr * 2), rect.Top);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Right - rightSlopeOffset - (cr * 2), rect.Bottom);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Right, rect.Bottom - rightSlopeOffset - (cr * 2));
            }
            
            return new Point(rect.Left, rect.Bottom - rightSlopeOffset - (cr * 2));
        }

        private Point GetP5(Rect rect, double cr, double rightSlopeOffset)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Right - rightSlopeOffset - cr, rect.Top + cr);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Right - rightSlopeOffset - cr, rect.Bottom - cr);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Right - cr, rect.Bottom - rightSlopeOffset - cr);
            }

            return new Point(rect.Left + cr, rect.Bottom - rightSlopeOffset - cr);
        }

        private Point GetP6(Rect rect, double cr, bool isBackground, bool hasCornerRadius, double hbt)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Right - cr, rect.Bottom - cr + (isBackground || hasCornerRadius ? 0 : hbt));
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Right - cr, rect.Top + cr + (isBackground || hasCornerRadius ? 0 : hbt));
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Left + cr + (isBackground || hasCornerRadius ? 0 : hbt), rect.Bottom - cr);
            }

            return new Point(rect.Right - cr - (isBackground || hasCornerRadius ? 0 : hbt), rect.Bottom - cr);
        }

        private Point GetP7(Rect rect, double hbt)
        {
            var dock = Dock;
            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return new Point(rect.Right + hbt, rect.Bottom);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return new Point(rect.Right + hbt, rect.Top);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return new Point(rect.Left, rect.Bottom + hbt);
            }

            return new Point(rect.Right, rect.Bottom + hbt);
        }

        private Point GetCp1(Rect rect, Point p1, Point p2)
        {
            var leftLineEquation = new LineEquation(p1, p2);
            var dock = Dock;

            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                // the 1st control point is the intersection between the slope line and the base line.
                return leftLineEquation.IntersectWithHorizontalLine(p1.X, rect.Bottom);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return leftLineEquation.IntersectWithHorizontalLine(p1.X, rect.Top);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return leftLineEquation.IntersectWithVerticalLine(p1.Y, rect.Left);
            }

            return leftLineEquation.IntersectWithVerticalLine(p1.Y, rect.Right);
        }

        private Point GetCp2(Rect rect, Point p1, Point p2)
        {
            var leftLineEquation = new LineEquation(p1, p2);
            var dock = Dock;

            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                // the 1st control point is the intersection between the slope line and the base line.
                return leftLineEquation.IntersectWithHorizontalLine(p2.X, rect.Top);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return leftLineEquation.IntersectWithHorizontalLine(p2.X, rect.Bottom);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return leftLineEquation.IntersectWithVerticalLine(p2.Y, rect.Right);
            }

            return leftLineEquation.IntersectWithVerticalLine(p2.Y, rect.Left);
        }

        private Point GetCp3(Rect rect, Point p1, Point p2)
        {
            var rightLineEquation = new LineEquation(p1, p2);
            var dock = Dock;

            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return rightLineEquation.IntersectWithHorizontalLine(p1.X, rect.Top);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return rightLineEquation.IntersectWithHorizontalLine(p1.X, rect.Bottom);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return rightLineEquation.IntersectWithVerticalLine(p1.Y, rect.Right);
            }

            return rightLineEquation.IntersectWithVerticalLine(p1.Y, rect.Left);
        }

        private Point GetCp4(Rect rect, Point p1, Point p2)
        {
            var rightLineEquation = new LineEquation(p1, p2);
            var dock = Dock;

            var isBottom = (dock & TabItemDock.Bottom) == TabItemDock.Bottom;
            if (isBottom)
            {
                return rightLineEquation.IntersectWithHorizontalLine(p2.X, rect.Bottom);
            }

            var isTop = (dock & TabItemDock.Top) == TabItemDock.Top;
            if (isTop)
            {
                return rightLineEquation.IntersectWithHorizontalLine(p2.X, rect.Top);
            }

            var isLeft = (dock & TabItemDock.Left) == TabItemDock.Left;
            if (isLeft)
            {
                return rightLineEquation.IntersectWithVerticalLine(p2.Y, rect.Left);
            }

            return rightLineEquation.IntersectWithVerticalLine(p2.Y, rect.Right);
        }

        private Geometry GenerateGeometry(Size renderSize, double cr, bool isBackground, DrawingContext dc)
        {
            var bt = BorderThickness;
            var hbt = bt * 0.5;
            var rect = new Rect(hbt, hbt, renderSize.Width - bt, renderSize.Height - bt);
            var geometry = new StreamGeometry();
            const bool isStroked = true;
            var hasCornerRadius = cr > 0;
            var leftSlopeOffset = LeftSlopeOffset;
            var rightSlopeOffset = RightSlopeOffset;

            using (var ctx = geometry.Open())
            {
                var p0 = GetP0(rect, isBackground);
                ctx.BeginFigure(p0, true /* is filled */, false);

                var p1 = GetP1(rect, cr);
                var p2 = GetP2(rect, cr, leftSlopeOffset);

                // the 1st control point is the intersection between the slope line and the base line.
                var cp1 = GetCp1(rect, p1, p2);

                ctx.QuadraticBezierTo(cp1, p1, isStroked, false);

                ctx.LineTo(p2, isStroked, false);

                var cp2 = GetCp2(rect, p1, p2);

                var p3 = GetP3(rect, cr, leftSlopeOffset);
                ctx.QuadraticBezierTo(cp2, p3, isStroked, false);

                var p4 = GetP4(rect, cr, rightSlopeOffset);
                ctx.LineTo(p4, isStroked, false);

                var p5 = GetP5(rect, cr, rightSlopeOffset);
                var p6 = GetP6(rect, cr, isBackground, hasCornerRadius, hbt);

                var cp3 = GetCp3(rect, p5, p6);
                ctx.QuadraticBezierTo(cp3, p5, isStroked, false);

                ctx.LineTo(p6, isStroked, false);

                var p7 = GetP7(rect, hbt);
                var cp4 = GetCp4(rect, p5, p6);
                ctx.QuadraticBezierTo(cp4, p7, isStroked, false);
            }

            geometry.Freeze();

            // I found it very hard to construct a background geometry
            // that doesn't overlap the border geometry, the issue was
            // always the bottom left/right corner radius.
            // so what I am doing is constructing the background geometry based
            // on the border geometry and stretching to fill the remaining
            // bottom space.
            return isBackground ? ApplyTransform(geometry, bt, renderSize) : geometry;
        }

        private void DrawPoint(DrawingContext dc, params Point[] pts)
        {
            foreach (var pt in pts)
            {
                dc.DrawEllipse(Brushes.Brown, null, new Point(pt.X - 0.5, pt.Y - 0.5), 1, 1);
            }
        }

        private static Geometry ApplyTransform(Geometry geometry, double bt, Size renderSize)
        {
            var pathGeometry = new PathGeometry();
            pathGeometry.AddGeometry(geometry);

            var hbt = bt * 0.5;
            //var scaleY = ((hbt * 100) / renderSize.Height) / 100;
            var scaleY = hbt / renderSize.Height;
            pathGeometry.Transform = new ScaleTransform(1, scaleY + 1, renderSize.Width * 0.5, 0);
            pathGeometry.Freeze();
            return pathGeometry;
        }
    }
}