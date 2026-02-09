using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace EvolveOS_Optimizer.Utilities.Services
{
    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        public event PropertyChangedEventHandler? PropertyChanged;

        private Dictionary<string, string> _stringCache = new();

        public string Get(string key) => this[key];

        public string this[string key]
        {
            get
            {
                if (_stringCache != null && _stringCache.TryGetValue(key, out var cachedValue))
                {
                    return cachedValue;
                }

                return $"[{key}]";
            }
        }

        public void LoadLanguage(string langCode)
        {
            try
            {
                string filePath = Path.Combine(AppContext.BaseDirectory, "Languages", $"{langCode}.xaml");
                Debug.WriteLine($"[Localization] Attempting to load: {filePath}");

                if (!File.Exists(filePath))
                {
                    Debug.WriteLine($"[Localization] FILE NOT FOUND at {filePath}");
                    return;
                }

                XDocument doc = XDocument.Load(filePath);
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";

                _stringCache.Clear();

                foreach (var element in doc.Descendants())
                {
                    var keyAttr = element.Attribute(x + "Key");
                    if (keyAttr != null)
                    {
                        _stringCache[keyAttr.Value] = element.Value;
                    }
                }

                Refresh();
                Debug.WriteLine($"[Localization] Successfully cached {langCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Localization] XML Read Error: {ex.Message}");
            }
        }

        public void Refresh()
        {
            OnPropertyChanged("Item[]");
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}