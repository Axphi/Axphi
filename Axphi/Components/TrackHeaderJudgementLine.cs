using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Text;
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
    public class TrackHeaderJudgementLine : Control
    {
        static TrackHeaderJudgementLine()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TrackHeaderJudgementLine), new FrameworkPropertyMetadata(typeof(TrackHeaderJudgementLine)));
        }



        public JudgementLine Line
        {
            get { return (JudgementLine)GetValue(LineProperty); }
            set { SetValue(LineProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Line.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LineProperty =
            DependencyProperty.Register(nameof(Line), typeof(JudgementLine), typeof(TrackHeaderJudgementLine), new PropertyMetadata(null)); // 这 null 为啥不报错 ??



        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsExpanded.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TrackHeaderJudgementLine), new PropertyMetadata(false));



    }
}
