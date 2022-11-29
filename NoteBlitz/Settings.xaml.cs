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
using System.Windows.Shapes;

namespace NoteBlitz
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class Settings : Window
    {
        public Data data { get; set; }

        public Settings(Data setupData)
        {
            this.data = setupData;
            DataContext = this;

            InitializeComponent();

            Loaded += Settings_Loaded;
        }

        private void Settings_Loaded(object sender, RoutedEventArgs e)
        {
            InitHotkey();
        }

        bool waitingForHotkey = false;

        private void InitHotkey()
        {
            btnHotKey.Content = data.HotKey;
        }

        private void btnHotKey_Click(object sender, RoutedEventArgs e)
        {
            if (!waitingForHotkey)
            {
                //listen for hotkey event 
                btnHotKey.Content = "Press a key";
                waitingForHotkey = true;
            }
        }

        private void btnHotKey_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (waitingForHotkey)
            {
                e.Handled = true;

                waitingForHotkey = false;
                btnHotKey.Content = e.Key;
                data.HotKey = e.Key;
            }
        }
    }
}
