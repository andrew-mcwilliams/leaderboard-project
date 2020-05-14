using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace LeaderboardWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ViewModel.OnOpenFileDialog += ViewModel_OnOpenFileDialog;
            this.DataContext = ViewModel;
            InitializeComponent();
        }

        private void ViewModel_OnOpenFileDialog(object sender, ReturnEventArgs<string> e)
        {
            var dialog = new OpenFileDialog() { Filter = "JSON File (.json)|*.json" };
            if(dialog.ShowDialog() == true)
            {
                e.Result = dialog.FileName;
            }
        }

        public MainWindowViewModel ViewModel { get; } = new MainWindowViewModel();

    }
}
