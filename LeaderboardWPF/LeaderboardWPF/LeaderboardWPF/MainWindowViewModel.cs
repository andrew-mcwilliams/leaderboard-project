using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace LeaderboardWPF
{
    public class MainWindowViewModel : ViewModelBase
    {
        public MainWindowViewModel()
        {
            Height = System.Windows.SystemParameters.WorkArea.Height -
             System.Windows.SystemParameters.WorkArea.Height / 4;

            Width = System.Windows.SystemParameters.WorkArea.Width -
                    System.Windows.SystemParameters.WorkArea.Width / 4;

            //Top = System.Windows.SystemParameters.WorkArea.Top -
            //      System.Windows.SystemParameters.WorkArea.Top / 2;

            //Left = System.Windows.SystemParameters.WorkArea.Left -
            //       System.Windows.SystemParameters.WorkArea.Left / 2;

            Commands.AddCommand("OnLoadDatabaseFile", OnLoadDatabaseFile);
            Commands.AddCommand("OnClickAPISend", OnClickAPISend);
            Commands.AddCommand("OnRefreshDatabase", OnRefreshDatabase);

            //RequestedLeaderboard = new ObservableCollection<LeaderboardScoreViewModel>();

            RequestDefinitions = new ObservableCollection<RequestDefinition>();

            var subclasses = from assembly in AppDomain.CurrentDomain.GetAssemblies()
                             from type in assembly.GetTypes()
                             where type.IsSubclassOf(typeof(RequestDefinition))
                             select type;

            foreach(var subclass in subclasses)
            {
                RequestDefinitions.Add((RequestDefinition)Activator.CreateInstance(subclass));
            }


            Host = DefaultHost;

            HttpClient.DefaultRequestHeaders.ConnectionClose = true;

            IsBusy = false;
        }
 
        private HttpClient HttpClient { get; } = new HttpClient(new WinHttpHandler());


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

        private double _top;
        public double Top
        {
            get => _top;
            set => SetField(ref _top, value);
        }

        private double _left;
        public double Left
        {
            get => _left;
            set => SetField(ref _left, value);
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


        private string _databaseJSON;
        public string DatabaseJSON
        {
            get => _databaseJSON;
            set => SetField(ref _databaseJSON, value);
        }


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

        private string DatabaseFile { get; set; }

        public string DefaultHost { get; } = "http://localhost:8080";


        public delegate ReturnEventArgs<string> OpenFileDialog(object sender, EventArgs args);
        public event ReturnEventHandler<string> OnOpenFileDialog;

        public void OnLoadDatabaseFile(object obj)
        {
            if (OnOpenFileDialog != null)
            {
                var returnEventArgs = new ReturnEventArgs<string>();
                OnOpenFileDialog(this, returnEventArgs);

                DatabaseFile = returnEventArgs.Result;
                RefreshDatabase();
            }
        }

        public async void OnClickAPISend(object obj)
        {
            if(SelectedRequest != null && !string.IsNullOrWhiteSpace(Host))
            {
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
                    }
                    catch(HttpRequestException ex)
                    {
                        ServerResponse = ex.ToString();
                        Console.WriteLine(ex);
                    }
                }

                RefreshDatabase();

                if(SelectedRequest.Action == "GetScores")
                {
                    CreateLeaderboard(ServerResponse);
                }    

                IsBusy = false;
            }
        }

        private void CreateLeaderboard(string serverResponse)
        {
            var getScoresResponse = JsonSerializer.Deserialize(serverResponse, typeof(GetScoresResponse)) as GetScoresResponse;
            if(getScoresResponse != null && getScoresResponse.Success && getScoresResponse.Scores.Count > 0)
            {
                RequestedLeaderboard = new ObservableCollection<LeaderboardScoreViewModel>();
                foreach(var scoreResponse in getScoresResponse.Scores)
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

        private void RefreshDatabase()
        {
            if (DatabaseFile != null)
            {
                using (var stream = File.Open(DatabaseFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        object deserialized = JsonSerializer.Deserialize<object>(reader.ReadToEnd());

                        var options = new JsonSerializerOptions()
                        {
                            WriteIndented = true
                        };

                        DatabaseJSON = JsonSerializer.Serialize(deserialized, typeof(object), options);
                    }
                }
            }
        }
        public void OnRefreshDatabase(object obj)
        {
            RefreshDatabase();
        }

    }


}
