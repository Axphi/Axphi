using Axphi.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Windows.Input; // 必须引入这个才能读取键盘状态

namespace Axphi.Utilities
{
    public interface ISelectionNode
    {
        bool IsSelected { get; set; }
    }

    public static class SelectionHelper
    {
        private static readonly Dictionary<string, List<WeakReference<ISelectionNode>>> SelectedNodesByGroup = new();

        private static SelectionGroup ParseSelectionGroup(string groupName)
        {
            if (Enum.TryParse(groupName, out SelectionGroup group))
            {
                return group;
            }

            return SelectionGroup.Keyframes;
        }

        /// <summary>
        /// 处理专业级的修饰键选中逻辑
        /// </summary>
        /// <param name="groupName">分组名（如 "Keyframes"）</param>
        /// <param name="sender">触发点击的对象（把自己传进来）</param>
        /// <param name="isCurrentlySelected">当前是否已经被选中</param>
        /// <param name="setIsSelected">用于修改自身选中状态的回调</param>
        public static void HandleSelection(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected)
            => HandleSelection(groupName, sender, isCurrentlySelected, setIsSelected, null);

        public static void HandleSelection(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected, IMessenger? messenger)
        {
            messenger ??= WeakReferenceMessenger.Default;

            // 1. 读取当前键盘按下的键
            bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (isCtrlDown)
            {
                // Ctrl：仅仅切换自己的状态，绝对不碰其他人（筛选）
                ApplySelectionState(groupName, sender, !isCurrentlySelected, setIsSelected);
            }
            else if (isShiftDown)
            {
                // Shift：加选，不影响其他人
                ApplySelectionState(groupName, sender, true, setIsSelected);
            }
            else
            {
                // 什么都没按：排他选中（独占）
                bool hasManagedSelection = ClearManagedSelection(groupName, sender);
                if (!hasManagedSelection)
                {
                    messenger.Send(new ClearSelectionMessage(ParseSelectionGroup(groupName), sender));
                }

                // 再把自己点亮
                ApplySelectionState(groupName, sender, true, setIsSelected);
            }
        }

        public static bool BeginSelectionGesture(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected)
            => BeginSelectionGesture(groupName, sender, isCurrentlySelected, setIsSelected, null);

        public static bool BeginSelectionGesture(string groupName, object sender, bool isCurrentlySelected, Action<bool> setIsSelected, IMessenger? messenger)
        {
            bool wasSelectedBeforeGesture = isCurrentlySelected;

            if (!wasSelectedBeforeGesture)
            {
                HandleSelection(groupName, sender, isCurrentlySelected, setIsSelected, messenger);
            }

            return wasSelectedBeforeGesture;
        }

        public static void CompleteSelectionGesture(string groupName, object sender, bool wasSelectedBeforeGesture, double interactionDistance, Action<bool> setIsSelected, params Action[] extraClearActions)
            => CompleteSelectionGesture(groupName, sender, wasSelectedBeforeGesture, interactionDistance, setIsSelected, null, extraClearActions);

        public static void CompleteSelectionGesture(string groupName, object sender, bool wasSelectedBeforeGesture, double interactionDistance, Action<bool> setIsSelected, IMessenger? messenger, params Action[] extraClearActions)
        {
            messenger ??= WeakReferenceMessenger.Default;

            if (!wasSelectedBeforeGesture || interactionDistance >= 2.0)
            {
                return;
            }

            bool isShiftDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            bool isCtrlDown = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (isCtrlDown)
            {
                ApplySelectionState(groupName, sender, false, setIsSelected);
                return;
            }

            if (isShiftDown)
            {
                ApplySelectionState(groupName, sender, true, setIsSelected);
                return;
            }

            bool hasManagedSelection = ClearManagedSelection(groupName, sender);
            if (!hasManagedSelection)
            {
                messenger.Send(new ClearSelectionMessage(ParseSelectionGroup(groupName), sender));
            }

            foreach (var clearAction in extraClearActions)
            {
                clearAction();
            }

            ApplySelectionState(groupName, sender, true, setIsSelected);
        }

        private static void ApplySelectionState(string groupName, object sender, bool isSelected, Action<bool> setIsSelected)
        {
            setIsSelected(isSelected);
            TrackManagedSelection(groupName, sender, isSelected);
        }

        private static void TrackManagedSelection(string groupName, object sender, bool isSelected)
        {
            if (sender is not ISelectionNode node)
            {
                return;
            }

            if (!SelectedNodesByGroup.TryGetValue(groupName, out var entries))
            {
                if (!isSelected)
                {
                    return;
                }

                entries = new List<WeakReference<ISelectionNode>>();
                SelectedNodesByGroup[groupName] = entries;
            }

            RemoveNode(entries, node);

            if (isSelected)
            {
                entries.Add(new WeakReference<ISelectionNode>(node));
            }
        }

        private static bool ClearManagedSelection(string groupName, object senderToIgnore)
        {
            if (!SelectedNodesByGroup.TryGetValue(groupName, out var entries) || entries.Count == 0)
            {
                return false;
            }

            bool hasManagedSelection = false;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].TryGetTarget(out var node))
                {
                    entries.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(node, senderToIgnore))
                {
                    continue;
                }

                hasManagedSelection = true;
                node.IsSelected = false;
                entries.RemoveAt(i);
            }

            if (entries.Count == 0)
            {
                SelectedNodesByGroup.Remove(groupName);
            }

            return hasManagedSelection;
        }

        private static void RemoveNode(List<WeakReference<ISelectionNode>> entries, ISelectionNode target)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].TryGetTarget(out var node) || ReferenceEquals(node, target))
                {
                    entries.RemoveAt(i);
                }
            }
        }
    }
}