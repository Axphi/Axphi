using System.Runtime.InteropServices;
using System.Windows;

namespace Axphi.Utilities;

public static class CursorUtils
{
    // 引入系统底层函数：设置鼠标位置
    [DllImport("User32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    // 引入系统底层函数：获取鼠标位置
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    /// <summary>
    /// 将鼠标瞬移到屏幕指定像素坐标
    /// </summary>
    public static void SetPosition(int x, int y)
    {
        SetCursorPos(x, y);
    }

    /// <summary>
    /// 获取当前鼠标在屏幕上的绝对坐标
    /// </summary>
    public static Point GetPosition()
    {
        GetCursorPos(out POINT p);
        return new Point(p.X, p.Y);
    }
}