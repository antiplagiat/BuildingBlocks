using System.Text;
using System;
using System.IO;
using System.Linq;

namespace BuildingBlocks.Exp01
{
    public class GraphData
    {
        public readonly DateTime TimeStamp;
        public readonly string Os;
        public readonly string Machine;
        public readonly string Device;
        public readonly string Path;
        public readonly long AvgCount;
        public readonly int[] BlockSizes;
        public long[][] Ticks;

        public GraphData(DateTime timestamp, string os, string machine, string dev, string path, int avgcnt, int[] sizes, long[][] ticks){
                TimeStamp = timestamp;
                Os = os;
                Machine = machine;
                Device = dev;
                Path = path;
                AvgCount = avgcnt;
                BlockSizes = sizes;
                Ticks = ticks;
        }
        public void Save(string path)
        {
            using var fs = File.CreateText(path);
            fs.WriteLine(TimeStamp);
            fs.WriteLine(Os);
            fs.WriteLine(Machine);
            fs.WriteLine(Device);
            fs.WriteLine(Path);
            fs.WriteLine(AvgCount);
            fs.WriteLine(Ticks.Length);
            fs.WriteLine("# blockSize ticks");
            for(int i=0;i<BlockSizes.Length;i++){
                fs.WriteLine($"{BlockSizes[i]} {string.Join(" ", Ticks.Select(p => p[i].ToString()))}");
            }
            fs.Flush();
        }

        public static GraphData Load(string path)
        {
            var lines= File.ReadAllLines(path);
            var timestamp = DateTime.Parse(lines[0]);
            var os = lines[1];
            var machine = lines[2];
            var dev = lines[3];
            var p = lines[4];
            var avgcnt = int.Parse(lines[5]);
            var itersCount = int.Parse(lines[6]);
            if( lines[7][0] != '#')
                throw new InvalidDataException();
            var sizes = new int[lines.Length - 8];
            var ticks = new long[itersCount][];
            for(int k=0;k<itersCount;k++)
                ticks[k] = new long[lines.Length - 8];
            for(int i=0;i<sizes.Length;i++){
                var line = lines[i + 8];
                var parts = line.Split(' ');
                sizes[i] = int.Parse(parts[0]);
                if (parts.Length - 1 != itersCount)
                    throw new InvalidDataException();
                for(int k=0;k<itersCount;k++)
                    ticks[k][i] = long.Parse(parts[1+k]) / 100; // HRES!
            }
            return new GraphData(timestamp, os, machine, dev, p, avgcnt, sizes, ticks);
        }
    
        public override string ToString()
        {
            var sb = new StringBuilder($"TimeStamp={TimeStamp}, Os={Os}, Machine={Machine}\nDevice={Device}, Path={Path}, AvgCount={AvgCount}, ItersCount={Ticks.Length}\n");
            for (int i = 0; i < BlockSizes.Length; i++)
            {
                int bs = (int)BlockSizes[i];
                sb.AppendLine($"\t[{bs.ToString().PadLeft(6)}]: {string.Join("  ", Ticks.Select(q => q[i].ToString()))}");
            }
            return sb.ToString();
        }
    }
}