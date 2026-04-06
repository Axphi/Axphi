using System.Windows;
using Axphi.Data.Abstraction;

namespace Axphi.Components
{
    public class KeyFrameToggleButton : EleCho.WpfSuite.Controls.Button
    {
        static KeyFrameToggleButton()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(KeyFrameToggleButton), new FrameworkPropertyMetadata(typeof(KeyFrameToggleButton)));
        }
    }

    public class KeyFrameToggleButton<TValue> : KeyFrameToggleButton
        where TValue : struct
    {
        public ChartTimeline Context
        {
            get { return (ChartTimeline)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public IAnimatableProperty<TValue> PropertyToToggle
        {
            get { return (IAnimatableProperty<TValue>)GetValue(PropertyToToggleProperty); }
            set { SetValue(PropertyToToggleProperty, value); }
        }


        public static readonly DependencyProperty ContextProperty =
            DependencyProperty.Register(nameof(Context), typeof(ChartTimeline), typeof(KeyFrameToggleButton<TValue>), new PropertyMetadata(null));

        public static readonly DependencyProperty PropertyToToggleProperty =
            DependencyProperty.Register(nameof(PropertyToToggle), typeof(IAnimatableProperty<TValue>), typeof(KeyFrameToggleButton<TValue>), new PropertyMetadata(null));

        private bool TimeEquals(ChartTimeline context, TimeSpan time1, TimeSpan time2)
        {


            return Math.Abs(time1.TotalMilliseconds - time2.TotalMilliseconds) < 50;
        }

        protected override void OnClick()
        {
            if (Context is { } context &&
                PropertyToToggle is { } propertyToToggle)
            {
                var time = context.PlayTime;
                int existKeyFrameIndex = -1;
                if (propertyToToggle.KeyFrames.Count > 0)
                {
                    var keyFrames = propertyToToggle.KeyFrames;
                    for (int i = 0; i < keyFrames.Count; i++)
                    {
                        if (TimeEquals(context, keyFrames[i].Time, time))
                        {
                            existKeyFrameIndex = i;
                            break;
                        }
                    }
                }

                if (existKeyFrameIndex != -1)
                {
                    propertyToToggle.RemoveKeyFrameByIndex(existKeyFrameIndex);
                }
                else
                {
                    propertyToToggle.AddKeyFrame(time, default, null);
                }
            }

            base.OnClick();
        }
    }

    public class VectorKeyFrameToggleButton : KeyFrameToggleButton<Vector>;
    public class Float64KeyFrameToggleButton : KeyFrameToggleButton<double>;
}
