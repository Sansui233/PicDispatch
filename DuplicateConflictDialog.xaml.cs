using System.Windows;
using PicDispatch.Services;

namespace PicDispatch
{
    public partial class DuplicateConflictDialog : Window
    {
        public DuplicateConflictDialog(string fileName, string targetName)
        {
            InitializeComponent();
            Message = $"{fileName} 在 {targetName} 中已存在。可以自动追加序号后移动，或取消本次移动。";
            DataContext = this;
        }

        public string Message { get; }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.ApplyWindows11Chrome(this);
            WindowEffects.CenterOnCurrentScreen(this);
        }

        private void Append_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
