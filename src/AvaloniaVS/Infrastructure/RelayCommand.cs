using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace AvaloniaVS.Infrastructure
{
    class RelayCommand : ICommand
    {
        private readonly Action _cb;

        public RelayCommand(Action cb)
        {
            _cb = cb;
        }
        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            _cb();
        }

        public event EventHandler CanExecuteChanged;
    }
}
