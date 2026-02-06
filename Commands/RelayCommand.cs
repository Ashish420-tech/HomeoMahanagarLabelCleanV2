using System;
using System.Windows.Input;

namespace HomeoMahanagarLabelCleanV2.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?>? _executeWithParam;
        private readonly Action? _executeWithoutParam;
        private readonly Func<object?, bool>? _canExecute;

        // 🔹 Constructor for parameterless commands
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _executeWithoutParam = execute;
            if (canExecute != null)
                _canExecute = _ => canExecute();
        }

        // 🔹 Constructor for parameterized commands
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _executeWithParam = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object? parameter)
        {
            try
            {
                if (_executeWithoutParam != null)
                    _executeWithoutParam();
                else
                    _executeWithParam?.Invoke(parameter);
            }
            catch (System.Exception ex)
            {
                try
                {
                    HomeoMahanagarLabelCleanV2.Logging.AppLogger.Log(ex);
                }
                catch { }
                // Swallow to prevent command exceptions from crashing UI thread
            }
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
