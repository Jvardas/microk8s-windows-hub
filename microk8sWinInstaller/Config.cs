using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace microk8sWinInstaller
{
    class Config
    {
        public string DefaultInstance { get; set; }
        public List<Snap> Snaps { get; set; }
    }

    class Snap
    {
        public string SnapName { get; set; }
        public Dictionary<string, string> Commands { get; set; }
    }
}
