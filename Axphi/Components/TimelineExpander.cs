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
    [TemplatePart(Name = "PART_ToggleButton", Type = typeof(Button))]
    public class TimelineExpander : HeaderedContentControl
    {
        static TimelineExpander()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TimelineExpander), new FrameworkPropertyMetadata(typeof(TimelineExpander)));
        }

        public bool IsExpanded
        {
            get { return (bool)GetValue(IsExpandedProperty); }
            set { SetValue(IsExpandedProperty, value); }
        }

        public double IndicatorThickness
        {
            get { return (double)GetValue(IndicatorThicknessProperty); }
            set { SetValue(IndicatorThicknessProperty, value); }
        }

        public Brush IndicatorFill
        {
            get { return (Brush)GetValue(IndicatorFillProperty); }
            set { SetValue(IndicatorFillProperty, value); }
        }

        public object Icon
        {
            get { return (object)GetValue(IconProperty); }
            set { SetValue(IconProperty, value); }
        }

        public DataTemplate IconTemplate
        {
            get { return (DataTemplate)GetValue(IconTemplateProperty); }
            set { SetValue(IconTemplateProperty, value); }
        }

        public Brush HeaderBackground
        {
            get { return (Brush)GetValue(HeaderBackgroundProperty); }
            set { SetValue(HeaderBackgroundProperty, value); }
        }

        public object HeaderBackgroundSlot
        {
            get { return (object)GetValue(HeaderBackgroundSlotProperty); }
            set { SetValue(HeaderBackgroundSlotProperty, value); }
        }

        public DataTemplate HeaderBackgroundSlotTemplate
        {
            get { return (DataTemplate)GetValue(HeaderBackgroundSlotTemplateProperty); }
            set { SetValue(HeaderBackgroundSlotTemplateProperty, value); }
        }

        public Brush ToggleButtonStroke
        {
            get { return (Brush)GetValue(ToggleButtonStrokeProperty); }
            set { SetValue(ToggleButtonStrokeProperty, value); }
        }




        public static readonly DependencyProperty IsExpandedProperty =
            DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty IndicatorThicknessProperty =
            DependencyProperty.Register(nameof(IndicatorThickness), typeof(double), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(4.0));

        public static readonly DependencyProperty IndicatorFillProperty =
            DependencyProperty.Register(nameof(IndicatorFill), typeof(Brush), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(nameof(Icon), typeof(object), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty IconTemplateProperty =
            DependencyProperty.Register(nameof(IconTemplate), typeof(DataTemplate), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty HeaderBackgroundProperty =
            DependencyProperty.Register(nameof(HeaderBackground), typeof(Brush), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty HeaderBackgroundSlotProperty =
            DependencyProperty.Register(nameof(HeaderBackgroundSlot), typeof(object), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty HeaderBackgroundSlotTemplateProperty =
            DependencyProperty.Register(nameof(HeaderBackgroundSlotTemplate), typeof(DataTemplate), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));

        public static readonly DependencyProperty ToggleButtonStrokeProperty =
            DependencyProperty.Register(nameof(ToggleButtonStroke), typeof(Brush), typeof(TimelineExpander), 
                new FrameworkPropertyMetadata(null));


    }
}
