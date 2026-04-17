using Nodify;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace Axphi.Components
{
    public class TrackHeader: NodifyEditor
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
                typeof(TrackHeader),
                new PropertyMetadata(null, OnJudgmentLinesChanged));


        private static void OnJudgmentLinesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TrackHeader trackHeader)
            {

            }
        }
    }
}
