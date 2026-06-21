using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using PicDispatch.Models;
using PicDispatch.Services;

namespace PicDispatch
{
    public partial class ShortcutInputDialog : Window, INotifyPropertyChanged
    {
        private string _shortcut = string.Empty;
        private string _validationMessage = string.Empty;
        private readonly HashSet<string> _reservedShortcuts;

        public ShortcutInputDialog(string folderPath, IEnumerable<string> reservedShortcuts, string currentShortcut = null)
        {
            InitializeComponent();
            _reservedShortcuts = new HashSet<string>(
                reservedShortcuts.Where(shortcut => !string.IsNullOrWhiteSpace(shortcut)),
                System.StringComparer.OrdinalIgnoreCase);
            _shortcut = ShortcutNormalizer.NormalizeText(currentShortcut);
            Prompt = $"为 {folderPath} 按一个 A-Z 或 0-9 快捷键。";
            DataContext = this;
        }

        public string Prompt { get; }

        public string Shortcut => _shortcut;

        public string ShortcutText => string.IsNullOrWhiteSpace(_shortcut) ? "Press key" : _shortcut;

        public string ValidationMessage
        {
            get => _validationMessage;
            set
            {
                if (_validationMessage == value)
                {
                    return;
                }

                _validationMessage = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            WindowEffects.ApplyWindows11Chrome(this);
            WindowEffects.CenterOnCurrentScreen(this);
            Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                e.Handled = true;
                return;
            }

            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            var shortcut = ShortcutNormalizer.NormalizeKey(key);
            if (string.IsNullOrWhiteSpace(shortcut))
            {
                return;
            }

            _shortcut = shortcut;
            ValidationMessage = string.Empty;
            OnPropertyChanged(nameof(ShortcutText));
            e.Handled = true;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_shortcut))
            {
                ValidationMessage = "必须绑定 A-Z 或 0-9 的单个快捷键。";
                return;
            }

            if (_reservedShortcuts.Contains(_shortcut))
            {
                ValidationMessage = $"快捷键 {_shortcut} 已被使用。";
                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
