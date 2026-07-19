using ScottPlot.WinForms;

namespace SkyPilot.Utils;

/// <summary>
/// Helper methods for creating and configuring ScottPlot charts.
/// </summary>
public static class ChartHelper
{
    public static void SetupChart(FormsPlot chart, string title, string xLabel, string yLabel)
    {
        chart.Plot.Title(title);
        chart.Plot.XLabel(xLabel);
        chart.Plot.YLabel(yLabel);
        chart.Plot.FigureBackground.Color = ScottPlot.Color.FromHex("1E1E1E");
        chart.Plot.DataBackground.Color = ScottPlot.Color.FromHex("282828");
    }

    public static double[] GetEmptyData() => new double[0];
}
