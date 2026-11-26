/**
 * Copyright 2023 Y. Meyer-Norwood
 * Copyright 2020 Dan Tulloh
 * Copyright 2016 Carsten Gehling
 *
 * For a full list of contributing authors, see:
 *
 *     https://jirastopwatch.com/contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Windows.Input;

namespace StopWatch
{
    /// <summary>
    /// The smallest ICommand: Avalonia has no RoutedCommand/CommandBinding, so
    /// the code-behind (or a view model) supplies the two delegates.
    ///
    /// Unlike WPF's CommandManager, nothing re-queries CanExecute by itself:
    /// call <see cref="RaiseCanExecuteChanged"/> whenever what the delegate
    /// reads changes (selection, running state).
    /// </summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action execute;
        private readonly Func<bool> canExecute;


        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }


        public event EventHandler CanExecuteChanged;


        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute();
        }


        public void Execute(object parameter)
        {
            if (CanExecute(parameter))
                execute();
        }


        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
