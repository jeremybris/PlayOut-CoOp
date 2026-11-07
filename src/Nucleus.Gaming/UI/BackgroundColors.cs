using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Nucleus.Gaming.UI
{
    public static class BackgroundColors
    {
        public static IDictionary<string, SolidColorBrush> ColorsDictionnary = new Dictionary<string, SolidColorBrush>
        {
                { "Black", Brushes.Black },
                { "Gray", Brushes.DimGray },
                { "White", Brushes.White },
                { "Dark Blue", Brushes.DarkBlue },
                { "Blue", Brushes.Blue },
                { "Purple", Brushes.Purple },
                { "Pink", Brushes.Pink },
                { "Red", Brushes.Red },
                { "Orange", Brushes.Orange },
                { "Yellow", Brushes.Yellow },
                { "Green", Brushes.Green }
        };
    }
}
