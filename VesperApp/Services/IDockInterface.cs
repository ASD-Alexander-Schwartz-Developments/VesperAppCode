using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VesperApp.Services
{
    public interface IDockInterface<T>
    {
        Task<IEnumerable<T>> ScanDocksAsync(bool forceRefresh = false);

        Task StopDocksScanAsync();

        Task<T?> GetDockBySerialNumberAsync(string serialnum);

        Task<bool> DockConnect(T d);
        Task<bool> DockDisconnect();
        Task<string> GetManufacturerName();
        //Task<string> GetModelNumber();
        Task<string> GetSerialNumber();
        //Task<string> GetSerialPort();

        //bool IsConnected;

        //event EventHandler<Markdown> ConnectionChangedEvent;
    }
}
