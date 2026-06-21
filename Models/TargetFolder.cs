using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace PicDispatch.Models
{
    [DataContract]
    public class TargetFolder : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _folderPath = string.Empty;
        private string _shortcut = string.Empty;

        [DataMember]
        public string Name
        {
            get => _name;
            set
            {
                if (_name == value)
                {
                    return;
                }

                _name = value;
                OnPropertyChanged();
            }
        }

        [DataMember]
        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (_folderPath == value)
                {
                    return;
                }

                _folderPath = value;
                OnPropertyChanged();
            }
        }

        [DataMember]
        public string Shortcut
        {
            get => _shortcut;
            set
            {
                var normalized = ShortcutNormalizer.NormalizeText(value);
                if (_shortcut == normalized)
                {
                    return;
                }

                _shortcut = normalized;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
