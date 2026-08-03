using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public interface IUpdateService
    {
        Task CheckForUpdatesAsync();
    }
}
