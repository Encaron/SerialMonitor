using OxyPlot;
using OxyPlot.Axes;

namespace 串口助手
{
    /// <summary>
    /// OxyPlot 主题色归一化入口。
    ///
    /// v2.6 归一化之前，OxyPlot 颜色散在三处：
    ///   - PlotViewModel.UpdateThemeColors
    ///   - Sensors.cs RegisterThemePanel 波形卡
    ///   - CreateWaveformCard 初始创建
    /// 漏一处 = 暗色白底。现在全走这一个方法。
    /// </summary>
    internal static class OxyPlotTheme
    {
        /// <summary>将 PlotModel 的网格/轴/文字/Legend/背景全部切为暗色或亮色。</summary>
        public static void Apply(PlotModel pm, bool isDark)
        {
            if (pm == null) return;

            pm.Background = isDark
                ? OxyColor.Parse("#252526")
                : OxyColor.Parse("#FFFFFF");
            pm.TextColor = isDark
                ? OxyColor.Parse("#CCCCCC")
                : OxyColor.Parse("#333333");
            pm.PlotAreaBackground = isDark
                ? OxyColor.Parse("#1E1E1E")
                : OxyColor.Parse("#FFFFFF");

            var majorGridColor = OxyColor.Parse(isDark ? "#3A3A3D" : "#E0E0E0");
            var minorGridColor = OxyColor.Parse(isDark ? "#2A2A2D" : "#F0F0F0");
            var tickColor = OxyColor.Parse(isDark ? "#555555" : "#CCCCCC");
            var axisTextColor = OxyColor.Parse(isDark ? "#888888" : "#666666");

            foreach (var ax in pm.Axes)
            {
                ax.MajorGridlineColor = majorGridColor;
                ax.MinorGridlineColor = minorGridColor;
                ax.TicklineColor = tickColor;
                ax.TextColor = axisTextColor;
            }

            foreach (var leg in pm.Legends)
            {
                leg.LegendTextColor = isDark
                    ? OxyColor.Parse("#CCCCCC")
                    : OxyColor.Parse("#333333");
                leg.LegendBackground = isDark
                    ? OxyColor.Parse("#2D2D30")
                    : OxyColor.Parse("#F5F5F5");
            }

            pm.InvalidatePlot(false);
        }
    }
}
