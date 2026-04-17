using Nodify;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;


namespace Axphi.Components
{
    public class TimeLine: NodifyEditor
    {


        public IEnumerable JudgmentLines
        {
            get { return (IEnumerable)GetValue(JudgmentLinesProperty); }
            set { SetValue(JudgmentLinesProperty, value); }
        }
 
        public static readonly DependencyProperty JudgmentLinesProperty =
            DependencyProperty.Register(
                nameof(JudgmentLines),
                typeof(IEnumerable),
                typeof(TimeLine),
                new PropertyMetadata(null, OnJudgmentLinesChanged));

        
        private static void OnJudgmentLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TimeLine timeLine)
            {
                
            }
        }





        public double PixelPerTick
        {
            get { return (double)GetValue(PixelPerTickProperty); }
            set { SetValue(PixelPerTickProperty, value); }
        }

        public static readonly DependencyProperty PixelPerTickProperty =
            DependencyProperty.Register(nameof(PixelPerTick), typeof(double), typeof(TimeLine), new PropertyMetadata(100.0));




        public double Zoom
        {
            get { return (double)GetValue(ZoomProperty); }
            set { SetValue(ZoomProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Zoom.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ZoomProperty =
            DependencyProperty.Register(nameof(Zoom), typeof(double), typeof(TimeLine), new PropertyMetadata(1.0));



        public int StartTick
        {
            get { return (int)GetValue(StartTickProperty); }
            set { SetValue(StartTickProperty, value); }
        }

        // Using a DependencyProperty as the backing store for StartTick.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty StartTickProperty =
            DependencyProperty.Register(nameof(StartTick), typeof(int), typeof(TimeLine), new PropertyMetadata(0));



        public int PlayHeadTick
        {
            get { return (int)GetValue(PlayHeadTickProperty); }
            set { SetValue(PlayHeadTickProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PlayHeadTick.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlayHeadTickProperty =
            DependencyProperty.Register(nameof(PlayHeadTick), typeof(int), typeof(TimeLine), new PropertyMetadata(0));





        public double CameraY
        {
            get { return (double)GetValue(CameraYProperty); }
            set { SetValue(CameraYProperty, value); }
        }

        // Using a DependencyProperty as the backing store for CameraY.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CameraYProperty =
            DependencyProperty.Register(nameof(CameraY), typeof(double), typeof(TimeLine), new PropertyMetadata(0.0));






    }
}
