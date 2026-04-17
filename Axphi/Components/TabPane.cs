using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Axphi.Components
{
    public class TabPane : HeaderedContentControl
    {
        static TabPane()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TabPane), new FrameworkPropertyMetadata(typeof(TabPane)));
        }
    }
}
