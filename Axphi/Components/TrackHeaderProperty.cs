using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Axphi.Components
{
    public class TrackHeaderProperty : ContentControl
    {
        static TrackHeaderProperty()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(TrackHeaderProperty), new FrameworkPropertyMetadata(typeof(TrackHeaderProperty)));
        }

        // 依赖属性：属性名 (例如 "Position")
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(nameof(Header), typeof(string), typeof(TrackHeaderProperty), new PropertyMetadata(string.Empty));

        public string Header
        {
            get => (string)GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        // 依赖属性：添加关键帧的命令
        public static readonly DependencyProperty AddKeyframeCommandProperty =
            DependencyProperty.Register(nameof(AddKeyframeCommand), typeof(ICommand), typeof(TrackHeaderProperty), new PropertyMetadata(null));

        public ICommand AddKeyframeCommand
        {
            get => (ICommand)GetValue(AddKeyframeCommandProperty);
            set => SetValue(AddKeyframeCommandProperty, value);
        }

        // 依赖属性：命令参数（比如传个字符串 "Position" 过去）
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register(nameof(CommandParameter), typeof(object), typeof(TrackHeaderProperty), new PropertyMetadata(null));

        public object CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }
    }
}