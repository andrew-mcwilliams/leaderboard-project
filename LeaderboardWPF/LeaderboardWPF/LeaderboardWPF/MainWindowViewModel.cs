using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;

namespace LeaderboardWPF
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {

            // set up our window size to be a percentage of the screen we start on
            Height = System.Windows.SystemParameters.WorkArea.Height -
             System.Windows.SystemParameters.WorkArea.Height / 4;

            Width = System.Windows.SystemParameters.WorkArea.Width -
                    System.Windows.SystemParameters.WorkArea.Width / 4;

            Commands.AddCommand("OnLoadDatabaseFile", OnLoadDatabaseFile);
            Commands.AddCommand("OnClickAPISend", OnClickAPISend);
            Commands.AddCommand("OnRefreshDatabase", OnRefreshDatabase);
            Commands.AddCommand("OnStartServer", OnStartServer);
            Commands.AddCommand("OnStopServer", OnStopServer);
            Commands.AddCommand("OnClearOutput", OnClearOutput);
            Commands.AddCommand("OnClickPythonFile", OnClickPythonFile);

            // gather all of our request definitions
            // if a new one is added it gets automatically picked up and added the to list in the UI
            RequestDefinitions = new ObservableCollection<RequestDefinition>();

            var subclasses = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                             from type in assembly.GetTypes()
                             where type.IsSubclassOf(typeof(RequestDefinition))
                             select type;

            foreach (var subclass in subclasses)
            {
                RequestDefinitions.Add((RequestDefinition)Activator.CreateInstance(subclass));
            }

            Host = DefaultHost;
            IsBusy = false;
        }

        // WinHTTPHandler supports GET with a body, under normal circumstances something like HttpRequestMessage would be used instead
        private HttpClient HttpClient { get; } = new HttpClient(new WinHttpHandler());

        // Capture the main synchrocontext so when we update the server log later on we can invoke the change on the main thread
        private SynchronizationContext UIContext { get; } = SynchronizationContext.Current;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetField(ref _isBusy, value);
        }


        private double _height;
        public double Height
        {
            get => _height;
            set => SetField(ref _height, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetField(ref _width, value);
        }

        public ObservableCollection<RequestDefinition> RequestDefinitions { get; set; }
        private RequestDefinition _selectedRequest;
        public RequestDefinition SelectedRequest
        {
            get => _selectedRequest;
            set => SetField(ref _selectedRequest, value);
        }

        private string _host;
        public string Host
        {
            get => _host;
            set => SetField(ref _host, value);
        }
        public string DefaultHost { get; } = "http://localhost:8080";

        private string DatabaseFile { get; set; }

        private string _databaseJSON;
        public string DatabaseJSON
        {
            get => _databaseJSON;
            set => SetField(ref _databaseJSON, value);
        }

        // used to display the response from the server when making API calls
        // also used to display exception output when something wrong happens in the UI
        private string _serverResponse;
        public string ServerResponse
        {
            get => _serverResponse;
            set => SetField(ref _serverResponse, value);
        }

        private ObservableCollection<LeaderboardScoreViewModel> _requestedLeaderboard;
        public ObservableCollection<LeaderboardScoreViewModel> RequestedLeaderboard
        {
            get => _requestedLeaderboard;
            set => SetField(ref _requestedLeaderboard, value);
        }

        public ObservableCollection<string> ServerOutput { get; set; } = new ObservableCollection<string>();

        public ServerConfiguration ServerConfig { get; set; } = new ServerConfiguration() { Host = "localhost", Port = 8080, DatabaseFile = "database.json" };
        private Process ServerProcess { get; set; }

        private bool _serverRunning;
        public bool ServerRunning
        {
            get => _serverRunning;
            set => SetField(ref _serverRunning, value);
        }

        public delegate void OpenFileDialog(object sender, OpenFileReturnEventArgs args);
        public event OpenFileDialog OnOpenFileDialog;

        // convert JSON response from the server when calling GetScores to something that can be displayed in the UI
        private void CreateLeaderboard(string serverResponse)
        {
            if (serverResponse != null && !string.IsNullOrWhiteSpace(serverResponse))
            {
                var getScoresResponse = JsonSerializer.Deserialize(serverResponse, typeof(GetScoresResponse)) as GetScoresResponse;
                if (getScoresResponse != null && getScoresResponse.Success && getScoresResponse.Scores.Count > 0)
                {
                    // always create a new collection on request, this ensures we don't leave old data behind
                    // and also forces the datagrid in the ui to repopulate and abandon any sorting that had been done by the user
                    RequestedLeaderboard = new ObservableCollection<LeaderboardScoreViewModel>();
                    foreach (var scoreResponse in getScoresResponse.Scores)
                    {
                        var leaderboardScore = new LeaderboardScoreViewModel()
                        {
                            Score = scoreResponse.Score,
                            Username = scoreResponse.Username,
                            DateAchieved = DateTimeOffset.FromUnixTimeSeconds(scoreResponse.Timestamp).UtcDateTime
                        };

                        RequestedLeaderboard.Add(leaderboardScore);
                    }
                }
                else
                {
                    RequestedLeaderboard = null;
                }
            }
        }

        // read the database file and repopulate the the textbox in the UI
        // automatically called when any API call is made successfully
        private void RefreshDatabase()
        {
            if (DatabaseFile != null && File.Exists(DatabaseFile))
            {
                // when running locally the server will keep the file open, so we need to make sure our open options are correct
                using (var stream = File.Open(DatabaseFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        try
                        {
                            object deserialized = JsonSerializer.Deserialize<object>(reader.ReadToEnd());

                            var options = new JsonSerializerOptions()
                            {
                                WriteIndented = true
                            };

                            DatabaseJSON = JsonSerializer.Serialize(deserialized, typeof(object), options);
                        }
                        catch (JsonException e)
                        {
                            ServerResponse = e.ToString();
                        }
                    }
                }
            }
        }

        // MVVM way to open a file dialog
        public void OnLoadDatabaseFile(object obj)
        {
            if (OnOpenFileDialog != null)
            {
                var returnEventArgs = new OpenFileReturnEventArgs()
                {
                    FileFormat = "JSON File (.json)|*.json"
                };
                OnOpenFileDialog(this, returnEventArgs);

                DatabaseFile = returnEventArgs.Result;
                RefreshDatabase();
            }
        }

        private void OnClickPythonFile(object obj)
        {
            if (OnOpenFileDialog != null)
            {
                var returnEventArgs = new OpenFileReturnEventArgs()
                {
                    FileFormat = "Python File (.py)|*.py"
                };
                OnOpenFileDialog(this, returnEventArgs);

                ServerConfig.PythonFile = returnEventArgs.Result;
            }
        }

        public async void OnClickAPISend(object obj)
        {
            if (SelectedRequest != null && !string.IsNullOrWhiteSpace(Host))
            {
                // don't allow further calls to be made to the server until the current call is finished
                IsBusy = true;

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IgnoreNullValues = true
                };

                using (var request = new HttpRequestMessage(SelectedRequest.Method, Host))
                {
                    string serialized = JsonSerializer.Serialize(SelectedRequest, SelectedRequest.GetType(), options);

                    request.Content = new StringContent(serialized, Encoding.UTF8, "application/json");

                    try
                    {
                        var response = await HttpClient.SendAsync(request);

                        ServerResponse = await response.Content.ReadAsStringAsync();

                        RefreshDatabase();

                        if (SelectedRequest.Action == "GetScores")
                        {
                            CreateLeaderboard(ServerResponse);
                        }
                    }
                    catch (Exception ex)
                    {
                        ServerResponse = ex.ToString();
                        Console.WriteLine(ex);
                    }
                }



                IsBusy = false;
            }
        }

        public void OnRefreshDatabase(object obj)
        {
            RefreshDatabase();
        }

        public void OnStartServer(object obj)
        {
            if (ServerConfig != null)
            {
                // this assumes python is in the path, it'll fail if it isn't
                // -u here forces python to not buffer its output so we can grab it and display it in the UI
                ProcessStartInfo info = new ProcessStartInfo()
                {
                    FileName = "python",
                    Arguments = $"-u \"{ServerConfig.PythonFile}\" --host {ServerConfig.Host} --port {ServerConfig.Port} --database {ServerConfig.DatabaseFile}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };

                ServerProcess = Process.Start(info);
                ServerProcess.BeginOutputReadLine();
                ServerProcess.BeginErrorReadLine();
                ServerProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
                ServerProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;

                ServerRunning = true;

                Application.Current.MainWindow.Closing += MainWindow_Closing;
            }
        }

        private void OnClearOutput(object obj)
        {
            ServerOutput.Clear();
        }

        private void OnStopServer(object obj)
        {
            if (ServerProcess != null)
            {
                // a shutdown message to the server would be better so it has the ability to clean up
                // but this'll do for now
                ServerProcess.Kill();
            }

            ServerRunning = false;
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            // WPF doesn't allow the UI to be updated outside the mainthread (or at least the thread the UI was created on)
            // so we have to let the main thread handle updating the collection
            UIContext.Send(x => ServerOutput.Add(e.Data), null);
        }

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            UIContext.Send(x => ServerOutput.Add(e.Data), null);
        }
        
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OnStopServer(null);
        }
    }


}
