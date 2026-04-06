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
    /// <summary>
    /// 按照步骤 1a 或 1b 操作，然后执行步骤 2 以在 XAML 文件中使用此自定义控件。
    ///
    /// 步骤 1a) 在当前项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components"
    ///
    ///
    /// 步骤 1b) 在其他项目中存在的 XAML 文件中使用该自定义控件。
    /// 将此 XmlNamespace 特性添加到要使用该特性的标记文件的根
    /// 元素中:
    ///
    ///     xmlns:MyNamespace="clr-namespace:Axphi.Components;assembly=Axphi.Components"
    ///
    /// 您还需要添加一个从 XAML 文件所在的项目到此项目的项目引用，
    /// 并重新生成以避免编译错误:
    ///
    ///     在解决方案资源管理器中右击目标项目，然后依次单击
    ///     “添加引用”->“项目”->[浏览查找并选择此项目]
    ///
    ///
    /// 步骤 2)
    /// 继续操作并在 XAML 文件中使用控件。
    ///
    ///     <MyNamespace:NumberField/>
    ///
    /// </summary>
    public class NumberField : Control
    {
        static NumberField()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberField), new FrameworkPropertyMetadata(typeof(NumberField)));
        }

        private EleCho.WpfSuite.Controls.TextBox? _editor;



        public double Value
        {
            get { return (double)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }

        public bool IsEditing
        {
            get { return (bool)GetValue(IsEditingProperty); }
            private set { SetValue(IsEditingPropertyKey, value); }
        }

        public override void OnApplyTemplate()
        {
            if (_editor is { })
            {
                _editor.LostFocus -= Editor_LostFocus;
            }

            base.OnApplyTemplate();

            if (GetTemplateChild("PART_Editor") is EleCho.WpfSuite.Controls.TextBox editor)
            {
                _editor = editor;
                _editor.LostFocus += Editor_LostFocus;
            }
        }

        private void Editor_LostFocus(object sender, RoutedEventArgs e)
        {
            IsEditing = false;
        }

        protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
        {
            IsEditing = true;
            _editor?.Focus();
            base.OnMouseDoubleClick(e);
        }

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumberField),
                new PropertyMetadata(0.0));

        private static readonly DependencyPropertyKey IsEditingPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(IsEditing), typeof(bool), typeof(NumberField),
                new PropertyMetadata(false));

        public static readonly DependencyProperty IsEditingProperty =
            IsEditingPropertyKey.DependencyProperty;


    }
}
