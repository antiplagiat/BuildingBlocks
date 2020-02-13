using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BuildingBlocks.Exp01
{
    /// <summary>
    /// Первый эксперимент по чтению двух байт.
    ///
    /// Пути к файлам захардкожены. Перед запуском их нужно вписать явно.
    ///
    /// Код не для продакшена, написан на скорую руку и конечно же не соответствует многим критериям прекрасного!
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.WriteLine("Hello");
            Console.WriteLine(
                $"OS:{Environment.OSVersion}," +
                $" Page:{Environment.SystemPageSize}," +
                $" Domain:{Environment.UserDomainName}," +
                $" User:{Environment.UserName}," +
                $" Ver:{Environment.Version}");

            if (args.Length == 0)
            {
                PrintHelp();
                return -1;
            }

            switch (args[0])
            {
                case "file":
                    GatherDataFromFile();
                    return 0;
                case "device":
                    GatherDataFromDevice();
                    return 0;
                case "render":
                    GraphBuilder.RenderAllGraphs();
                    return 0;
                default:
                    PrintHelp();
                    return -1;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("USAGE: BuildingBlocks.Exp01 command\n" +
                              "where command one of:\n" +
                              "\tfile - experiment on files\n" +
                              "\tdevice - experiment on devices\n" +
                              "\trender - build pdfs form graphData-files");
        }

        private static void GatherDataFromFile()
        {
            var seed = Guid.NewGuid().GetHashCode();
            Bench2("nvm-file", "SSD.NVMe", seed, "/mnt/fast/fg-01.bin", 1000, 20);
            Bench2("ssd-file", "SSD.SATA", seed, "/mnt/sdc1/fg-01.bin", 1000, 20);
            Bench2("hdd-file", "HDD.SATA", seed, "/mnt/sdd1/fg-01.bin", 40, 20);
        }

        private static void GatherDataFromDevice()
        {
            var seed = Guid.NewGuid().GetHashCode();
            Bench2("nvm-raw", "SSD.NVMe", seed, "/dev/nvme0n1", 1000, 20); // Samsung 970 PRO, 512GB, NVME PCIeV2X1 (MZ-V7P512BW)
            Bench2("ssd-raw", "SSD.SATA", seed, "/dev/sdc", 1000, 20); // Samsung 850 EVO 500GB, SATA-600 (MZ-75E500BW)
            Bench2("hdd-raw", "HDD.SATA", seed, "/dev/sdd", 40, 20); // Hitachi 500GB, SATA-300 (HTS545050B9A300) 
        }

        private static void Bench2(string name, string device, int seed, string path, int maxChecks, int iters)
        {
            var tmp = new byte[8];
            var rnd = new Random(seed);
            var blockSizes = Enumerable.Range(8, 13).Select(p => 1 << (p - 1)).ToArray(); // 128 B - 512 KiB
            var maxBlockSize = blockSizes.Last() * 2;
            
            // init unised groups
            var fInfo = new FileInfo(path);
            var len = path.StartsWith("/dev") ? maxChecks * (long)maxBlockSize * blockSizes.Length: fInfo.Length;

            // correct maxChecks
            if((len / maxBlockSize)/blockSizes.Length < maxChecks)
            {
                Console.WriteLine($"MaxChecks reduced from {maxChecks} to {(len / maxBlockSize)/blockSizes.Length}");
                maxChecks = (int)(len / maxBlockSize)/blockSizes.Length;
            }
            Console.WriteLine($"Starting benchmark with file {path} of length {len / 1024 / 1024}MB, groupSize={maxBlockSize / 1024 / 1024}MB");

            var finalStats = new List<long[]>();

            var sw = Stopwatch.StartNew();
            var sw2 = TimeSpan.Zero;
            for(int iter = 0; iter < iters; iter++)
            {
                var unusedGroups = Enumerable.Range(0, (int)(len / maxBlockSize)).ToArray();
                var unusedGroupsCount = (int)(len / maxBlockSize);

                // Куда собирать статистики
                var stats = new Dictionary<int, List<long>>();
                foreach(var bs in blockSizes)
                    stats.Add(bs, new List<long>(maxChecks));

                // Open file and read sync in a single thread
                using(var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess))
                {
                    fs.Read(tmp, 0, 2);

                    for(int i=0;i<maxChecks;i++)
                        foreach(var blockSize in blockSizes)
                        {
                            Debug.Assert(unusedGroupsCount > 0);

                            // Prepare position
                            var p = rnd.Next(0, unusedGroupsCount);
                            var r = rnd.Next(0, maxBlockSize / blockSize) / 2;
                            var pos = unusedGroups[p] * (long)maxBlockSize + blockSize * (1 + r * 2);
                            unusedGroups[p] = unusedGroups[--unusedGroupsCount];

                            // Seek and read
                            fs.Position = pos - 1;
                            var tick1 = Stopwatch.GetTimestamp();
                            var readed = fs.Read(tmp, 0, 2);
                            var tick2 = Stopwatch.GetTimestamp();
                            Debug.Assert(readed == 2);

                            stats[blockSize].Add(tick2 - tick1);
                            sw2 += TimeSpan.FromTicks(tick2 - tick1);
                        }
                }

                // Aggregate stats and print
                var finalStat = new List<long>();
                foreach(var blockSize in blockSizes)
                {
                    var line = stats[blockSize];
                    var avgTicks = (long)line.OrderBy(p => p).Skip(maxChecks/20).Take(maxChecks - maxChecks/10).Average();
                    finalStat.Add(avgTicks);
                    Console.WriteLine($"[{blockSize:#########}] avg:{TimeSpan.FromTicks(avgTicks).TotalMilliseconds:F4}ms or {avgTicks}ticks");
                    
                }
                finalStats.Add(finalStat.ToArray());
                Console.Write("+");
            }
            sw.Stop();
            Console.WriteLine($"Experiment elapsed: {sw.Elapsed.TotalMilliseconds}ms or {sw2.TotalMilliseconds}ms");

            Console.WriteLine();
            var ret = new GraphData(DateTime.Now, Environment.OSVersion.Platform.ToString(), Environment.MachineName, device, path, maxChecks, blockSizes, finalStats.ToArray());
            Console.WriteLine(ret);
            if(!Directory.Exists("../data"))
                Directory.CreateDirectory("../data");
            ret.Save(Path.Combine("../data", $"{name}.graphData"));
        }
    }
}
