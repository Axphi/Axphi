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
using Axphi.Data;
using Axphi.Data.KeyFrames;

namespace Axphi.Components
{
    public class KeyFrameToggleButton<TParent, TKeyFrame, TValue> : EleCho.WpfSuite.Controls.Button
        where TParent : class
        where TKeyFrame : KeyFrame<TParent, TValue>
        where TValue : struct
    {
        static KeyFrameToggleButton()
        {

        }


        public ChartTimeline Context
        {
            get { return (ChartTimeline)GetValue(ContextProperty); }
            set { SetValue(ContextProperty, value); }
        }

        public RelationObject<TParent>.Collection<TKeyFrame> KeyFrameCollection
        {
            get { return (RelationObject<TParent>.Collection<TKeyFrame>)GetValue(KeyFrameCollectionProperty); }
            set { SetValue(KeyFrameCollectionProperty, value); }
        }


        public static readonly DependencyProperty ContextProperty =
            DependencyProperty.Register(nameof(Context), typeof(ChartTimeline), typeof(KeyFrameToggleButton<TParent, TKeyFrame, TValue>), new PropertyMetadata(null));

        public static readonly DependencyProperty KeyFrameCollectionProperty =
            DependencyProperty.Register(nameof(KeyFrameCollection), typeof(RelationObject<TParent>.Collection<TKeyFrame>), typeof(KeyFrameToggleButton<TParent, TKeyFrame, TValue>), new PropertyMetadata(null));


    }
}
