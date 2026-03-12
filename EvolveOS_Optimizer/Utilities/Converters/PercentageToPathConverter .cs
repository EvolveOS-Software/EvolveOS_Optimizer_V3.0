using System.Globalization;
using Windows.Foundation;
using Microsoft.UI.Xaml.Data;

namespace EvolveOS_Optimizer.Utilities.Converters
{
    public class PercentageToPathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is double percentage))
            {
                return string.Empty;
            }

            double radius = 25;

            Point centerPoint = new Point(radius, radius);

            double normalizedPercentage = percentage / 100.0;
            double angle = normalizedPercentage * 360;

            CultureInfo invariantCulture = CultureInfo.InvariantCulture;

            Point startPoint = new Point(radius, 0);

            if (angle >= 360)
            {
                return string.Format(invariantCulture,
                    "M {0},{1} L {2},{3} A {4},{5} 0 1 1 {6},{7} Z",
                    centerPoint.X, centerPoint.Y,
                    startPoint.X, startPoint.Y,
                    radius, radius,
                    radius - 0.001, 0);
            }

            double endAngle = angle - 90;
            double x = radius + radius * Math.Cos(endAngle * Math.PI / 180);
            double y = radius + radius * Math.Sin(endAngle * Math.PI / 180);

            Point endPoint = new Point(x, y);

            bool isLargeArc = angle > 180.0;

            string pathData = string.Format(invariantCulture,
                "M {0},{1} L {2},{3} A {4},{5} 0 {6} 1 {7},{8} Z",
                centerPoint.X, centerPoint.Y,
                startPoint.X, startPoint.Y,
                radius, radius,
                (isLargeArc ? 1 : 0),
                endPoint.X, endPoint.Y);

            return pathData;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}