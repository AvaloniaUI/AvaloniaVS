using System.Windows.Controls;
using System.Windows.Media;

namespace AvaloniaVS.Shared.Views
{
    public sealed class GridLines : Control
    {
        private Pen _pen;
        private Pen _penBold;

        public GridLines()
        {
            _pen = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255.0 * 0.1), 14, 94, 253)), 1); 
            _penBold = new Pen(new SolidColorBrush(Color.FromArgb((byte)(255.0 * 0.3), 14, 94, 253)), 1);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            for(int i = 1; i < ActualHeight / 10; i++)
            {
                drawingContext.DrawLine((i % 10 == 0) ? _penBold : _pen, new System.Windows.Point(0, i * 10), new System.Windows.Point(ActualWidth, i * 10));
            }

            for (int i = 1; i < ActualWidth / 10; i++)
            {
                drawingContext.DrawLine((i % 10 == 0) ? _penBold : _pen, new System.Windows.Point(i * 10, 0), new System.Windows.Point(i * 10, ActualHeight));
            }

        }
    }
}
