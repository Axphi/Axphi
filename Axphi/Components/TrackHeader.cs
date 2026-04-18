using Nodify;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace Axphi.Components
{
    public class TrackHeader: NodifyEditor
    {
        static TrackHeader()
        {
            // 覆盖 ViewportLocation 的元数据，添加强制转换回调 (CoerceValueCallback)
            ViewportLocationProperty.OverrideMetadata(
                typeof(TrackHeader),
                new FrameworkPropertyMetadata(
                    new Point(0, 0), // 默认值
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    null, // PropertyChangedCallback
                    CoerceViewportLocation // 在这里拦截并修改坐标
                )
            );
        }

        private static object CoerceViewportLocation(DependencyObject d, object baseValue)
        {
            if (baseValue is Point point)
            {
                // 强制 X 永远为 0，Y 永远大于等于 0
                return new Point(0, Math.Max(0, point.Y));
            }
            return new Point(0, 0);
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            e.Handled = true;

            
            double scrollSpeed = 0.1; // 滚动速度倍率
            double deltaY = e.Delta * scrollSpeed;

            
            double newY = ViewportLocation.Y - deltaY;

            
            ViewportLocation = new Point(ViewportLocation.X, newY);
        }
    }
}
