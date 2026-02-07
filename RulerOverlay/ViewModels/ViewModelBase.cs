using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RulerOverlay.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels providing INotifyPropertyChanged implementation
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed (automatically filled by compiler)</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets the property value and raises PropertyChanged if the value actually changed
        /// </summary>
        /// <typeparam name="T">Property type</typeparam>
        /// <param name="storage">Reference to backing field</param>
        /// <param name="value">New value to set</param>
        /// <param name="propertyName">Property name (automatically filled by compiler)</param>
        /// <returns>True if value changed, false if it was the same</returns>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
            {
                return false;
            }

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
