using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using Serilog;
using System.Windows;

namespace HJM.Chip8.WPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public static List<System.Windows.Input.Key> _pressedKeys = new List<System.Windows.Input.Key>();

        private GPU gpu;

        public MainWindow()
        {
            InitializeLogging();

            InitializeComponent();

            image.Stretch = Stretch.Fill;

            gpu = new GPU(image);
            gpu.Initialize();
        }

        private static void InitializeLogging()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            //image.Width = mainWindow.Width - Math.Abs((mainWindow.Width - mainPanel.Width));
            //image.Height = mainWindow.ActualHeight;

            image.Width = mainPanel.Width - SystemInformation.BorderSize.Width;
            image.Height = mainWindow.ActualHeight - SystemInformation.BorderSize.Height - SystemInformation.CaptionHeight;
            
        }

        private void mainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (!_pressedKeys.Contains(e.Key))
                _pressedKeys.Add(e.Key);
        }

        private void mainWindow_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            _pressedKeys.Remove(e.Key);
        }

        private void mainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            gpu.Quit();
        }
    }
}
