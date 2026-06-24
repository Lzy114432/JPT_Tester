using MarkingMachineFeeder.Viewmodel;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MarkingMachineFeeder
{
    public class EmptyToBrushConverter : IValueConverter
    {
        // 非空 -> 绿色 (#228B22)，空或 null -> 黑色 (#000000)
        private static readonly Brush GreenBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#228B22"));
        private static readonly Brush BlackBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#000000"));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var s = value as string;
                if (!string.IsNullOrWhiteSpace(s))
                    if (s == WorkOrderStatusProvider.Mid || s == WorkOrderStatusProvider.Rear)
                        return GreenBrush;
            }
            catch { }
            return BlackBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}