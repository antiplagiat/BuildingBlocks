using System.Linq;
using System;
using System.IO;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace BuildingBlocks.Exp01
{
    public static class GraphBuilder
    {
        const string DataDirPath = "../../../../data/";
        public static void RenderAllGraphs()
        {
            foreach (var type in new[]{"raw", "file"})
            {
                var ssdGraph = GraphData.Load(Path.Combine(DataDirPath, $"ssd-{type}.graphData"));
                var nvmGraph = GraphData.Load(Path.Combine(DataDirPath, $"nvm-{type}.graphData"));
                var hddGraph = GraphData.Load(Path.Combine(DataDirPath, $"hdd-{type}.graphData"));
                PlotSingleLine($"ssd-{type}", ssdGraph, OxyColors.Green);
                PlotCaching($"ssd-{type}", ssdGraph, OxyColors.Green);
                PlotSingleLine($"nvm-{type}", nvmGraph, OxyColors.Red);
                PlotCaching($"nvm-{type}", nvmGraph, OxyColors.Red);
                PlotSingleLine($"hdd-{type}", hddGraph, OxyColors.Blue);
                PlotCaching($"hdd-{type}", hddGraph, OxyColors.Blue);
                
                PlotCompose(type, (hddGraph, OxyColors.Blue, "hdd"), (ssdGraph, OxyColors.Green, "ssd"), (nvmGraph, OxyColors.Red, "nvm"));

                PlotCaching($"hdd-{type}", hddGraph, OxyColors.Blue, 2);
                PlotCaching($"hdd-{type}", hddGraph, OxyColors.Blue, 3);
            }
        }

        public static void PlotCompose(string name, params (GraphData graphData, OxyColor color, string title)[] data)
        {
            var model = new PlotModel
            {
                Title = "compose/" + name,
                LegendTitle = "devices",
                //LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
            };
            for(int k=0;k<data.Length;k++)
            {
                var graph = data[k];
                var lineSeries = new LineSeries
                {
                    MarkerType = MarkerType.Circle,
                    LineStyle = LineStyle.Dot,
                    Color = graph.color,
                    Title = graph.title,
                };
                for (int i = 0; i < graph.graphData.BlockSizes.Length; i++)
                {
                    var x = graph.graphData.BlockSizes[i];
                    var y = TimeSpan.FromTicks(graph.graphData.Ticks[0][i]).TotalMilliseconds;
                    lineSeries.Points.Add(new DataPoint(x, y));
                }
                model.Series.Add(lineSeries);
            }
            
            
            model.Axes.Add(new LogarithmicAxis
            {
                ExtraGridlineStyle = LineStyle.LongDashDotDot,
                MajorGridlineStyle = LineStyle.DashDot,
                MinorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Top,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = data[0].graphData.BlockSizes[0]/1.4,
                Maximum = data[0].graphData.BlockSizes.Last()*1.4,
                Base = 2,
                MajorStep = 2,
                MinorStep = 1.1,       
            });
            model.Axes.Add(new LogarithmicAxis
            {
                MajorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Left,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = 0.01,
                Maximum = 100,
            });

            using var f = File.OpenWrite($"{DataDirPath}/{name}-compose.pdf");
            f.Position = 0;
            PdfExporter.Export(model, f, 640, 480);
        }

        public static void PlotCaching(string name, GraphData graphData, OxyColor color, int? itersCount = null)
        {
            var subName = graphData.Path.StartsWith("/dev") ? graphData.Path : Path.GetFileName(graphData.Path);
            var model = new PlotModel
            {
                Title = graphData.Device,
                Subtitle = subName,
                LegendTitle = "iters",
                LegendPlacement = LegendPlacement.Outside,
            };
            var maxIters = itersCount ?? graphData.Ticks.Length;
            for(int k=0;k<maxIters;k++)
            {
                var factor = k / (double)graphData.Ticks.Length;
                var lineSeries = new LineSeries
                {
                    MarkerType = MarkerType.Circle,
                    LineStyle = LineStyle.Dot,
                    Color = color.ChangeIntensity(1 - factor),
                    Title = k.ToString(),
                };
                for (int i = 0; i < graphData.BlockSizes.Length; i++)
                {
                    var x = graphData.BlockSizes[i];
                    var y = TimeSpan.FromTicks(graphData.Ticks[k][i]).TotalMilliseconds;
                    lineSeries.Points.Add(new DataPoint(x, y));
                }
                model.Series.Add(lineSeries);
            }
            
            
            model.Axes.Add(new LogarithmicAxis
            {
                ExtraGridlineStyle = LineStyle.LongDashDotDot,
                MajorGridlineStyle = LineStyle.DashDot,
                MinorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Top,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = graphData.BlockSizes[0]/1.4,
                Maximum = graphData.BlockSizes.Last()*1.4,
                Base = 2,
                MajorStep = 2,
                MinorStep = 1.1,       
            });
            model.Axes.Add(new LinearAxis
            {
                MajorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Left,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = 0,
            });
            
            var fn = itersCount.HasValue ? $"{DataDirPath}/{name}-caching-{itersCount.Value}.pdf" : $"{DataDirPath}/{name}-caching.pdf";
            using var f = File.OpenWrite(fn);
            f.Position = 0;
            PdfExporter.Export(model, f, 740, 480);
        }

        public static void PlotSingleLine(string name, GraphData graphData, OxyColor color, int? iter = null)
        {
            var subName = graphData.Path.StartsWith("/dev") ? graphData.Path : Path.GetFileName(graphData.Path);
            var model = new PlotModel
            {
                Title = graphData.Device,
                Subtitle = subName,
            };
            var lineSeries = new LineSeries
            {
                MarkerType = MarkerType.Circle,
                LineStyle = LineStyle.Dot,
                Color = color,
            };
            for (int i = 0; i < graphData.BlockSizes.Length; i++)
            {
                var x = graphData.BlockSizes[i];
                var iterId = 0;
                if(iter.HasValue)
                    iterId = iter.Value;
                var y = TimeSpan.FromTicks(graphData.Ticks[iterId][i]).TotalMilliseconds;
                lineSeries.Points.Add(new DataPoint(x, y));
            }
            model.Series.Add(lineSeries);
            
            model.Axes.Add(new LogarithmicAxis
            {
                ExtraGridlineStyle = LineStyle.LongDashDotDot,
                MajorGridlineStyle = LineStyle.DashDot,
                MinorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Top,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = graphData.BlockSizes[0]/1.4,
                Maximum = graphData.BlockSizes.Last()*1.4,
                Base = 2,
                MajorStep = 2,
                MinorStep = 1.1,       
            });
            model.Axes.Add(new LinearAxis
            {
                MajorGridlineStyle = LineStyle.DashDot,
                Position = AxisPosition.Left,
                AxislineStyle = LineStyle.Solid,
                MinimumPadding = 0,
                MaximumPadding = 0,
                Minimum = 0,
            });
            
            var fn = iter.HasValue
                    ? $"{DataDirPath}/{name}-{iter.Value}.pdf"
                    : $"{DataDirPath}/{name}-single.pdf";
            using var f = File.OpenWrite(fn);
            f.Position = 0;
            PdfExporter.Export(model, f, 640, 480);
        }
    }
}