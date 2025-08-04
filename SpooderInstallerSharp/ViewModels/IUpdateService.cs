using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    public interface IUpdateService
    {
        Task CheckForUpdatesAsync();
    }
}
