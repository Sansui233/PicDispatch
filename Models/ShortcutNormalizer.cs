using System.Windows.Input;

namespace PicDispatch.Models
{
    public static class ShortcutNormalizer
    {
        public static string NormalizeKey(Key key)
        {
            if (key >= Key.A && key <= Key.Z)
            {
                return key.ToString().ToUpperInvariant();
            }

            if (key >= Key.D0 && key <= Key.D9)
            {
                return ((int)(key - Key.D0)).ToString();
            }

            if (key >= Key.NumPad0 && key <= Key.NumPad9)
            {
                return ((int)(key - Key.NumPad0)).ToString();
            }

            return string.Empty;
        }

        public static string NormalizeText(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var text = value.Trim().ToUpperInvariant();
            if (text.Length != 1)
            {
                return string.Empty;
            }

            var c = text[0];
            return (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') ? text : string.Empty;
        }
    }
}
