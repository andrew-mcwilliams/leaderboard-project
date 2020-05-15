using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderboardWPF
{
	// Class for display a Leaderboard Score retrieved from the server when calling GetScores
    public class LeaderboardScoreViewModel : ViewModelBase
    {
		private int _score;
		public int Score
		{
			get => _score;
			set => SetField(ref _score, value);
		}


		private string _username;
		public string Username
		{
			get => _username;
			set => SetField(ref _username, value);
		}


		private DateTime _dateAchieved;
		public DateTime DateAchieved
		{
			get => _dateAchieved;
			set => SetField(ref _dateAchieved, value);
		}
	}
}
