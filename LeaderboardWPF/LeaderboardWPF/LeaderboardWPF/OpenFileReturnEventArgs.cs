using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace LeaderboardWPF
{
    public class OpenFileReturnEventArgs : ReturnEventArgs<string>
    {
        public string FileFormat { get; set; }
    }
}
