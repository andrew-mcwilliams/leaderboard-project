
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderboardWPF
{
    public class ServerConfiguration : ViewModelBase
    {
        private string _host;
        public string Host
        {
            get => _host;
            set => SetField(ref _host, value);
        }

        private uint _port;
        public uint Port
        {
            get => _port;
            set => SetField(ref _port, value);
        }

        private string _databaseFile;
        public string DatabaseFile
        {
            get => _databaseFile;
            set => SetField(ref _databaseFile, value);
        }

        private string _pythonFile;
        public string PythonFile
        {
            get => _pythonFile;
            set => SetField(ref _pythonFile, value);
        }
    }
}
