using System;
using System.Windows.Threading;

namespace AvaloniaVS.Services
{
    public class Throttle<T> : IDisposable
    {
        private readonly Action<T> _execute;
        private readonly DispatcherTimer _timer;
        private T _value;

        public Throttle(TimeSpan interval, Action<T> execute)
        {
            _execute = execute;

            _timer = new DispatcherTimer
            {
                Interval = interval,
            };

            _timer.Tick += Tick;
        }

        public void Queue(T value)
        {
            if (!Equals(value, _value))
            {
                _timer.Stop();
                _value = value;
                _timer.Start();
            }
        }

        public void Dispose() => _timer.Stop();

        private void Tick(object sender, EventArgs e)
        {
            _execute(_value);
            _timer.Stop();
        }
    }
}
