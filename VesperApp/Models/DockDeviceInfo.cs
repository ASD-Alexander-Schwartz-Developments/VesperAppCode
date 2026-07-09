using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Models
{
    public class DockDeviceInfo : INotifyPropertyChanged
    {
        public string? Id { get; set; }
        public string? Text { get; set; }
        public string? Description { get; set; }

        /// <summary>The FTDI serial number, when discovery could read it. Null on Linux,
        /// where libusb discovery deliberately does not open devices — there <see cref="Id"/>
        /// is a synthetic discovery key and must NOT be used to filter the open.</summary>
        public string? SerialNumber { get; set; }

        protected bool SetProperty<T>(ref T backingStore, T value,
        [CallerMemberName] string propertyName = "",
        Action onChanged = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            onChanged?.Invoke();
            OnPropertyChanged(propertyName);
            return true;
        }

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            var changed = PropertyChanged;
            if (changed == null)
                return;

            changed.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

    }
}
