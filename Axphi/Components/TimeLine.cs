using Axphi.Data;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Axphi.Components
{
    public class TimeLine : Control
    {

        // 先用 tick 凑合吧, 后期必须加上 "拍号" 关键帧, 比如 "4/4", "3/4", "6/8", "7/4", "2/7" 等, 当然, 它们的关键帧插值只能是 "Constant"
        static TimeLine()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TimeLine), new FrameworkPropertyMetadata(typeof(TimeLine)));
        }





        // 此值代表右侧编辑区的起始 tick, 使用 double 是因为我们可以让视图起始点在两个 tick 之间
        public double ViewportStartTick
        {
            get { return (double)GetValue(ViewportStartTickProperty); }
            set { SetValue(ViewportStartTickProperty, value); }
        }

        public double PixelsPerTick
        {
            get { return (double)GetValue(PixelsPerTickProperty); }
            set { SetValue(PixelsPerTickProperty, value); }
        }


        public double Scale
        {
            get { return (double)GetValue(ScaleProperty); }
            set { SetValue(ScaleProperty, value); }
        }

        public double PlayHeadTick
        {
            get { return (double)GetValue(PlayHeadTickProperty); }
            set { SetValue(PlayHeadTickProperty, value); }
        }

        public double SharedViewportY
        {
            get { return (double)GetValue(SharedViewportYProperty); }
            set { SetValue(SharedViewportYProperty, value); }
        }


        // 已经被注册过了, 等后期把那个删了再加回来
        //public Chart Chart
        //{
        //    get { return (Chart)GetValue(ChartProperty); }
        //    set { SetValue(ChartProperty, value); }
        //}

        // 暂时还不写回调
        public static readonly DependencyProperty ViewportStartTickProperty =
            DependencyProperty.Register(
                nameof(ViewportStartTick),
                typeof(double),
                typeof(TimeLine),
                new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ScaleProperty =
            DependencyProperty.Register(
                nameof(Scale),
                typeof(double),
                typeof(TimeLine),
                new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty PixelsPerTickProperty =
            DependencyProperty.Register(
                nameof(PixelsPerTick),
                typeof(double),
                typeof(TimeLine),
                new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));





        public static readonly DependencyProperty PlayHeadTickProperty =
            DependencyProperty.Register(
                nameof(PlayHeadTick),
                typeof(double),
                typeof(TimeLine),
                new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SharedViewportYProperty =
            DependencyProperty.Register(
                nameof(SharedViewportY),
                typeof(double),
                typeof(TimeLine),
                new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));

        //public static readonly DependencyProperty ChartProperty =
        //    DependencyProperty.Register(
        //        nameof(Chart),
        //        typeof(Chart),
        //        typeof(ChartTimeline),
        //        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    }
}
