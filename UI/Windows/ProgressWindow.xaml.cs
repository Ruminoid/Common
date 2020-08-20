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
using Ruminoid.Common.Utilities;

namespace Ruminoid.Common.UI.Windows
{
    /// <summary>
    /// ProgressWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProgressWindow
    {
        #region Constructor

        private ProgressWindow()
        {
            Progress = new Progress();
            InitializeComponent();
        }

        private ProgressWindow(IProgressable progressable)
        {
            Progress = progressable.Progress;
            InitializeComponent();
        }

        private ProgressWindow(Progress progress)
        {
            Progress = progress;
            InitializeComponent();
        }

        public static ProgressWindow CreateAndShowDialog()
        {
            ProgressWindow window = new ProgressWindow();
            window.Progress.Completed += (sender, args) => window.Close();
            window.Show();
            return window;
        }

        public static ProgressWindow CreateAndShowDialog(IProgressable progressable)
        {
            ProgressWindow window = new ProgressWindow(progressable);
            window.Progress.Completed += (sender, args) => window.Close();
            window.Show();
            return window;
        }

        public static ProgressWindow CreateAndShowDialog(Progress progress)
        {
            ProgressWindow window = new ProgressWindow(progress);
            window.Progress.Completed += (sender, args) => window.Close();
            window.Show();
            return window;
        }

        #endregion

        #region Data Context

        public Progress Progress { get; }

        #endregion
    }
}
