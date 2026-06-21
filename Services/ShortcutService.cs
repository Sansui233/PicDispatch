using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PicDispatch.Models;

namespace PicDispatch.Services
{
    public enum AppInteractionState
    {
        Browsing,
        Moving,
        SettingsOpen,
        DialogOpen
    }

    public class ShortcutService
    {
        public bool CanUseBrowserShortcut(AppInteractionState state)
        {
            return state == AppInteractionState.Browsing && !IsTextInputFocused();
        }

        public string Normalize(KeyEventArgs args)
        {
            var key = args.Key == Key.System ? args.SystemKey : args.Key;
            return ShortcutNormalizer.NormalizeKey(key);
        }

        private static bool IsTextInputFocused()
        {
            var focused = Keyboard.FocusedElement as DependencyObject;
            return focused is TextBox ||
                   focused is PasswordBox ||
                   HasTextBoxAncestor(focused);
        }

        private static bool HasTextBoxAncestor(DependencyObject focused)
        {
            while (focused != null)
            {
                if (focused is TextBox || focused is PasswordBox)
                {
                    return true;
                }

                focused = System.Windows.Media.VisualTreeHelper.GetParent(focused);
            }

            return false;
        }
    }
}
