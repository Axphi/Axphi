using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Windows.Input; // 必须引入这个才能读取键盘状态

namespace Axphi.Utilities
{
    public static class SelectionHelper
    {
        /// <summary>
        /// 处理专业级的修饰键选中逻辑
        /// </summary>
        /// <param name="groupName">分组名（如 "Keyframes"）</param>
        /// <param name="sender">触发点击的对象（把自己传进来）</param>
        /// <param name="isCurrentlySelected">当前是否已经被选中</param>
        /// <param name="setIsSelected">用于修改自身选中状态的回调</param>
        public static void HandleSelection(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected)
        {
            // 1. 读取当前键盘按下的键
            bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (isCtrlDown)
            {
                // Ctrl：仅仅切换自己的状态，绝对不碰其他人（筛选）
                setIsSelected(!isCurrentlySelected);
            }
            else if (isShiftDown)
            {
                // Shift：加选，不影响其他人
                setIsSelected(true);
            }
            else
            {
                // 什么都没按：排他选中（独占）
                // 先发大喇叭，让同组的其他人全部暗下去
                WeakReferenceMessenger.Default.Send(new ClearSelectionMessage(groupName, sender));

                // 再把自己点亮
                setIsSelected(true);
            }
        }

        public static bool BeginSelectionGesture(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected)
        {
            bool wasSelectedBeforeGesture = isCurrentlySelected;

            if (!wasSelectedBeforeGesture)
            {
                HandleSelection(groupName, sender, isCurrentlySelected, setIsSelected);
            }

            return wasSelectedBeforeGesture;
        }

        public static void CompleteSelectionGesture(string groupName, object sender, bool wasSelectedBeforeGesture, double interactionDistance, Action<bool> setIsSelected, params Action[] extraClearActions)
        {
            if (!wasSelectedBeforeGesture || interactionDistance >= 2.0)
            {
                return;
            }

            bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (isCtrlDown)
            {
                setIsSelected(false);
                return;
            }

            if (isShiftDown)
            {
                setIsSelected(true);
                return;
            }

            WeakReferenceMessenger.Default.Send(new ClearSelectionMessage(groupName, sender));

            foreach (var clearAction in extraClearActions)
            {
                clearAction();
            }

            setIsSelected(true);
        }
    }
}