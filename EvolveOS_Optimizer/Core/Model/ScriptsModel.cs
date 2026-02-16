using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EvolveOS_Optimizer.Core.Model
{
    public partial class ScriptsModel : ObservableObject
    {
        public string FilePath { get; }
        public string FileName { get; }
        public ImageSource IconImage { get; }
        public ICommand RunCommand { get; }
        public bool RequiresElevation { get; }

        [ObservableProperty]
        public partial bool IsSelected { get; set; }

        public ScriptsModel(string filePath, string fileName, ImageSource iconImage, ICommand runCommand, bool requiresElevation = false)
        {
            FilePath = filePath;
            FileName = fileName;
            IconImage = iconImage;
            RunCommand = runCommand;
            RequiresElevation = requiresElevation;
        }
    }
}