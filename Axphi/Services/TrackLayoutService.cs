using Axphi.Data;
using Axphi.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Axphi.Services
{
    public class TrackLayoutService
    {
        // 常量定义：轨道基础高度、属性行高度、轨道间距
        public const double BaseTrackHeight = 20.0;
        public const double PropertyRowHeight = 18.0;
        public const double TrackSpacing = 2.5;


        // 给 xaml 用的
        public static GridLength BaseTrackGridHeight => new GridLength(BaseTrackHeight);
        public static double PropertyRowHeightValue => PropertyRowHeight;

        private readonly Dictionary<JudgementLine, double> _lineYCoordinates = new();

        // 存具体的属性名字列表
        private readonly Dictionary<JudgementLine, List<string>> _lineActiveProperties = new();

        private List<JudgementLine> _orderedLines = new();

        public event EventHandler? LayoutUpdated;


        // ================= 🌟 相机 Y 轴联动系统 =================
        private double _viewportY = 0;
        public double ViewportY => _viewportY;

        // 当 Y 轴相机改变时触发，传递 (发送者, 新的Y坐标)
        public event EventHandler<double>? ViewportYChanged;

        /// <summary>
        /// 更新相机的 Y 坐标
        /// </summary>
        /// <param name="newY">新的 Y 坐标</param>
        /// <param name="sender">是谁发起的修改（防止死循环）</param>
        public void UpdateViewportY(double newY, object sender)
        {
            // 如果差值极小（浮点数误差）就忽略，防止无限微调抖动
            if (Math.Abs(_viewportY - newY) > 0.01)
            {
                _viewportY = newY;
                // 广播给所有人：相机 Y 变了！
                ViewportYChanged?.Invoke(sender, _viewportY);
            }
        }



        public void InitializeLines(IEnumerable<JudgementLine> lines)
        {
            _orderedLines = new List<JudgementLine>(lines);
            _lineYCoordinates.Clear();
            _lineActiveProperties.Clear();

            foreach (var line in _orderedLines)
            {
                _lineYCoordinates[line] = 0;
                _lineActiveProperties[line] = new List<string>(); // 初始为空列表
            }

            RecalculateLayout();
        }

        // 🌟 左侧表头调用：直接把最新的属性列表塞进来
        public void UpdateActiveProperties(JudgementLine line, IEnumerable<string> activeProperties)
        {
            _lineActiveProperties[line] = activeProperties.ToList();
            RecalculateLayout();
        }

        public double GetYCoordinate(JudgementLine line)
        {
            return _lineYCoordinates.TryGetValue(line, out double y) ? y : 0;
        }

        // 🌟 预留给右侧时间轴调用的接口：查询这个轨道当前展开了哪些属性？
        public IReadOnlyList<string> GetActiveProperties(JudgementLine line)
        {
            return _lineActiveProperties.TryGetValue(line, out var props) ? props : new List<string>();
        }

        private void RecalculateLayout()
        {
            double currentY = 0;

            foreach (var line in _orderedLines)
            {
                _lineYCoordinates[line] = currentY;

                // 高度计算：直接看列表里有几个元素
                int activeCount = _lineActiveProperties.TryGetValue(line, out var props) ? props.Count : 0;
                double currentTrackTotalHeight = BaseTrackHeight + (activeCount * PropertyRowHeight);

                currentY += currentTrackTotalHeight + TrackSpacing;
            }

            LayoutUpdated?.Invoke(this, EventArgs.Empty);
        }
    }
}