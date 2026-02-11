using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class BoolToBrushConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Red);
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

        public object Convert(object v, Type t, object p, string l) => (v is bool b && b) ? TrueBrush : FalseBrush;
        public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
    }

    public class BoolToOpacityConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, string l) => (v is bool b && b) ? 0.6d : 0.0d;
        public object ConvertBack(object v, Type t, object p, string l) => throw new NotImplementedException();
    }
}