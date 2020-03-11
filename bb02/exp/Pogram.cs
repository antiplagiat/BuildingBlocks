using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BuildingBlocks.Exp02
{
    /// <summary>
    /// Эксперимент по выявлению фрагментной структуры файлов на примере NTFS.
    ///
    /// Пути к файлам захардкожены. Перед запуском их нужно вписать явно.
    ///
    /// Код не для продакшена, написан на скорую руку и конечно же не соответствует многим критериям прекрасного!
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Предельный размер файла.
        /// </summary>
        private static long MaxFileSize = 200L * 1024 * 1024 * 1024; // 200GiB

        public static void Main(string[] args)
        {
            Console.WriteLine("Hello");
            Console.WriteLine(
                $"OS:{Environment.OSVersion}," +
                $" Page:{Environment.SystemPageSize}," +
                $" Domain:{Environment.UserDomainName}," +
                $" User:{Environment.UserName}," +
                $" Ver:{Environment.Version}");

            var sizeGB = 160L;
            var itersCount = 20;
            Console.WriteLine("ItersCount:" + itersCount);
            var dir = Environment.CurrentDirectory;
            if (args.Length < 1 || args[0].Trim('/', '?', '-').Equals("help") || (args.Length > 2 && (!long.TryParse(args[2], out sizeGB) || sizeGB <= 0)))
            {
                PrintHelp();
                return;
            }
            Console.WriteLine($"MaxSize: {sizeGB} GiB");
            if (args.Length > 1)
                dir = args[1];
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            Console.WriteLine($"Directory: {dir}");
            var maxSize = sizeGB * 1024 * 1024 * 1024;

            switch (args[0].ToLower())
            {
                case "write":
                    AllWrites(dir, maxSize, itersCount);
                    return;
                case "read":
                    AllReads(dir, 10000, itersCount);
                    return;
                case "base":
                    BaseBench(dir, maxSize);
                    return;
                case "prefill":
                    PrefillBench(dir, maxSize);
                    return;
                case "resize":
                    GrowResizeBench(dir, maxSize);
                    return;
                case "all":
                    ////All(dir, maxSize, 10, 2000, false);
                    AllWrites(dir, maxSize, itersCount);
                    AllReads(dir, 10000, itersCount);
                    return;
                default:
                    PrintHelp();
                    return;
            }
        }

        static void PrintHelp()
        {
            Console.WriteLine("USAGE: BuildingBlocks.Exp02 command dir [size_in_GiB]\n" +
                              "where command one of:\n" +
                              "\tbase - Base grow by 4KiB at write\n" +
                              "\tprefill - Prefill all size befor write\n" +
                              "\tresize - Resize grow by factor 2\n" +
                              "\tall - All commands one by one: base, prefill, resize\n" +
                              "and\n" +
                              "\tdir - Directory to store bench files (default=currentdir)\n" +
                              "\tsize - File size limit in GiB (default=160)");
        }

        private static unsafe void RewritePack(byte[] pack, long len)
        {
            // В середину пачки запишем её номер.
            fixed (byte* pb = pack)
            {
                var ppb = pb + 2048;
                *(long*)ppb = len;
            }
        }

        private static void All(string dir, long maxSize, int itersCount, int maxReads, bool drop)
        {
            var csvPath = Path.Combine(dir, "result.csv");
            if (!File.Exists(csvPath))
                File.AppendAllText(csvPath, "FileSize(GiB); IterNr;" +
                                            " Base4k.GrowSpeed(MiB/s); Prefill.GrowSpeed(MiB/s); Resize.GrowSpeed(MiB/s);" +
                                            " Base4k.ReadCenter(mks); Prefill.ReadCenter(mks); Resize.ReadCenter(mks);" +
                                            " Base4k.ReadEdge(mks); Prefill.ReadEdge(mks); Resize.ReadEdge(mks);\n");
            var rs = new (double speed, string[] paths)[3 * itersCount];
            var ds = new (double center, double edge)[3 * itersCount];
            Func<string, (double speed, string[] paths)>[] ra = new Func<string, (double speed, string[] paths)>[]
            {
                d => BaseBench(d, maxSize),
                d => PrefillBench(d, maxSize),
                d => GrowResizeBench(d, maxSize),
            };
            for (int i = 0; i < itersCount; i++)
            {
                var dir2 = Path.Combine(dir, i.ToString());
                if (!Directory.Exists(dir2))
                    Directory.CreateDirectory(dir2);

                for (int k = 0; k < 3; k++)
                {
                    var id = i * 3 + k;
                    rs[id] = ra[k % 3].Invoke(dir2);
                    Console.Write("Sleep 30 sec...");
                    Thread.Sleep(TimeSpan.FromSeconds(30));
                    Console.WriteLine("ok");
                    if (id >= 2)
                    {
                        ds[id - 2] = EvaluateReadDelay(rs[id - 2].paths[0], maxReads);
                        Console.Write("Sleep 30 sec...");
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        Console.WriteLine("ok");
                    }
                }





                var r1 = BaseBench(dir2, maxSize);
                Console.WriteLine("Sleep 20 sec...");
                Thread.Sleep(TimeSpan.FromSeconds(20));

                var r2 = PrefillBench(dir2, maxSize);
                Console.WriteLine("Sleep 20 sec...");
                Thread.Sleep(TimeSpan.FromSeconds(20));

                var r3 = GrowResizeBench(dir2, maxSize);
                Console.WriteLine("Sleep 20 sec...");
                Thread.Sleep(TimeSpan.FromSeconds(20));

                EvaluateReadDelay(r1.paths[1], maxReads);
                var d1 = EvaluateReadDelay(r1.paths[0], maxReads);
                var d2 = EvaluateReadDelay(r2.paths[0], maxReads);
                var d3 = EvaluateReadDelay(r3.paths[0], maxReads);
                File.AppendAllText(csvPath, $"{maxSize/(1024*1024*1024)}; {i};" +
                                            $" {r1.speed:F2}; {r2.speed:F2}; {r3.speed:F2};" +
                                            $" {d1.msCenter * 1000:F2}; {d2.msCenter * 1000:F2}; {d3.msCenter * 1000:F2};" +
                                            $" {d1.msEdge * 1000:F2}; {d2.msEdge * 1000:F2}; {d3.msEdge * 1000:F2};\n");
                if(drop)
                    Directory.Delete(dir2, true);
            }
        }

        private static void AllWrites(string dir, long maxSize, int itersCount)
        {
            var csvPath = Path.Combine(dir, "writes.csv");
            if (!File.Exists(csvPath))
                File.AppendAllText(csvPath, "FileSize(GiB); IterNr;" +
                                            " Base4k.GrowSpeed(MiB/s); Prefill.GrowSpeed(MiB/s); Resize.GrowSpeed(MiB/s);" +
                                            " Base4k.Fragments; Prefill.Fragments; Resize.Fragments\n");
            Func<string, (double speed, string[] paths)>[] ra =
            {
                d => BaseBench(d, maxSize),
                d => PrefillBench(d, maxSize),
                d => GrowResizeBench(d, maxSize),
            };
            var rs = new (double speed, string[] paths, long? frags)[3];
            for (int i = 0; i < itersCount; i++)
            {
                var dir2 = Path.Combine(dir, i.ToString());
                if (!Directory.Exists(dir2))
                    Directory.CreateDirectory(dir2);

                for (int k = 0; k < 3; k++)
                {
                    var r = ra[k % 3].Invoke(dir2);
                    var frags = CalcFragments(r.paths[0]);
                    Console.WriteLine("Fragments = " + frags);
                    rs[k] = (r.speed, r.paths, frags);
                    Console.Write("Sleep 90 sec...");
                    Thread.Sleep(TimeSpan.FromSeconds(90));
                    Console.WriteLine("ok");
                }
                File.AppendAllText(csvPath, $"{maxSize / (1024 * 1024 * 1024)}; {i};" +
                                            $" {rs[0].speed:F2}; {rs[1].speed:F2}; {rs[2].speed:F2};" +
                                            $" {rs[0].frags}; {rs[1].frags}; {rs[2].frags};\n");
            }
        }

        private static void AllReads(string dir, int readLimit, int itersCount)
        {
            var csvPath = Path.Combine(dir, "reads.csv");
            if (!File.Exists(csvPath))
                File.AppendAllText(csvPath, "IterNr;" +
                                            " Base4k.Center(musec); Prefill.Center(musec); Resize.Center(musec);" +
                                            " Base4k.Edge(musec); Prefill.Edge(musec); Resize.Edge(musec);" +
                                            " Base4k.Fragments; Prefill.Fragments; Resize.Fragments\n");
            for (int i = 0; i < itersCount; i++)
            {
                var dir2 = Path.Combine(dir, i.ToString());
                if (!Directory.Exists(dir2))
                    Directory.CreateDirectory(dir2);

                // Base4k
                var path1 = Path.Combine(dir2, "online-allocated-1.dat");
                if (!File.Exists(path1))
                {
                    Console.WriteLine("Failed to read file: " + path1);
                    return;
                }
                var path2 = Path.Combine(dir2, "pre-allocated-1.dat");
                if (!File.Exists(path2))
                {
                    Console.WriteLine("Failed to read file: " + path2);
                    return;
                }
                var path3 = Path.Combine(dir2, "grow-allocated-1.dat");
                if (!File.Exists(path3))
                {
                    Console.WriteLine("Failed to read file: " + path3);
                    return;
                }
                var frags1 = CalcFragments(path1);
                var delays1 = EvaluateReadDelay(path1, readLimit);
                var frags2 = CalcFragments(path2);
                var delays2 = EvaluateReadDelay(path2, readLimit);
                var frags3 = CalcFragments(path3);
                var delays3 = EvaluateReadDelay(path3, readLimit);

                File.AppendAllText(csvPath, $"{i};" +
                                            $" {delays1.msCenter * 1000:F2}; {delays2.msCenter * 1000:F2}; {delays3.msCenter * 1000:F2};" +
                                            $" {delays1.msEdge * 1000:F2}; {delays2.msEdge * 1000:F2}; {delays3.msEdge * 1000:F2};" +
                                            $" {frags1}; {frags2}; {frags3};\n");
            }
        }

        private static long? CalcFragments(string path)
        {
            //// C:\bin\Contig64.exe -a F:\bb02\1\online-allocated-1.dat
            var binExe = @"C:\bin\Contig64.exe";
            var pi = new ProcessStartInfo(binExe, $"-a {Path.GetFullPath(path)}");
            pi.RedirectStandardOutput = true;
            var p = Process.Start(pi);
            p.WaitForExit();
            var output = p.StandardOutput.ReadToEnd();
            var lines = output.Split('\n');
            var line = lines.FirstOrDefault(p => p.Contains("fragments"));
            if (line == null)
            {
                if (output.Contains("is defragmented"))
                    return 1;
                Console.WriteLine("Error output: " + output);
                return null;
            }

            var countStr = line.Split(' ').TakeLast(2).First();
            return long.Parse(countStr);
        }

        private static (double speed, string[] paths) BaseBench(string dir, long maxSize)
        {
            var path1 = Path.Combine(dir, "online-allocated-1.dat");
            var path2 = Path.Combine(dir, "online-allocated-2.dat");
            var sw = Stopwatch.StartNew();
            using var f1 = new FileStream(path1, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            using var f2 = new FileStream(path2, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            var pack = new byte[4096];
            var len = 0L;
            try
            {
                while (len < maxSize)
                {
                    // В середину пачки запишем её номер.
                    RewritePack(pack, len);

                    // Запишем синхронно сначала в первый файл, затем во второй.
                    f1.Write(pack, 0, pack.Length);
                    f2.Write(pack, 0, pack.Length);
                    len += 4096;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed. " + ex);
            }
            sw.Stop();

            var speed = len / (sw.Elapsed.TotalSeconds * 1024 * 1024);
            Console.WriteLine($"Online: size = {len} bytes, dt = {sw.Elapsed}, speed={speed:F2} MiB/s");
            return (speed, new []{path1, path2});
        }

        private static (double speed, string[] paths) PrefillBench(string dir, long maxSize)
        {
            var path1 = Path.Combine(dir, "pre-allocated-1.dat");
            var path2 = Path.Combine(dir, "pre-allocated-2.dat");
            var sw = Stopwatch.StartNew();

            using var f1 = new FileStream(path1, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            using var f2 = new FileStream(path2, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            var len = 0L;
            var pack = new byte[4096];
            f1.SetLength(maxSize);
            f2.SetLength(maxSize);

            try
            {
                while (len < maxSize)
                {
                    // В середину пачки запишем её номер.
                    RewritePack(pack, len);

                    // Запишем синхронно сначала в первый файл, затем во второй.
                    f1.Write(pack, 0, pack.Length);
                    f2.Write(pack, 0, pack.Length);
                    len += 4096;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed. " + ex);
            }
            sw.Stop();

            var speed = len / (sw.Elapsed.TotalSeconds * 1024 * 1024);
            Console.WriteLine($"Preallocated: size = {len} bytes, dt = {sw.Elapsed}, speed={speed:F2} MiB/s");
            return (speed, new[] { path1, path2 });
        }

        private static (double speed, string[] paths) GrowResizeBench(string dir, long maxSize)
        {
            var path1 = Path.Combine(dir, "grow-allocated-1.dat");
            var path2 = Path.Combine(dir, "grow-allocated-2.dat");
            var sw = Stopwatch.StartNew();

            using var f1 = new FileStream(path1, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            using var f2 = new FileStream(path2, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 1, FileOptions.None);
            var len = 0L;
            var pack = new byte[4096];
            f1.SetLength(4096);
            f2.SetLength(4096);
            long max = 4096;

            try
            {
                while (len < maxSize)
                {
                    if (len == max)
                    {
                        max = Math.Min(max * 2, maxSize);
                        f1.SetLength(max);
                        f1.SetLength(max);
                    }

                    // В середину пачки запишем её номер.
                    RewritePack(pack, len);

                    // Запишем синхронно сначала в первый файл, затем во второй.
                    f1.Write(pack, 0, pack.Length);
                    f2.Write(pack, 0, pack.Length);
                    len += 4096;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed. " + ex);
            }
            sw.Stop();

            var speed = len / (sw.Elapsed.TotalSeconds * 1024 * 1024);
            Console.WriteLine($"GrowResize: size = {len} bytes, dt = {sw.Elapsed}, speed={speed:F2} MiB/s");
            return (speed, new[] { path1, path2 });
        }

        private static (double msEdge, double msCenter) EvaluateReadDelay(string path, int readLimit)
        {
            var swf = Stopwatch.StartNew();
            using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
            var len = f.Length;
            var packsCount = (int)(len / 8192 - 1);
            var packsOffset = new long[packsCount];
            for (int i = 0; i < packsOffset.Length; i++)
                packsOffset[i] = i * 8192L;
            var packsReady = packsCount;
            var rnd = new Random(Guid.NewGuid().GetHashCode());

            var maxReads = Math.Min(readLimit, packsCount) / 2;
            var centerDelays = new List<double>(maxReads);
            var edgeDelays = new List<double>(maxReads);
            var sw = new Stopwatch();
            var buf = new byte[2];
            for (int i = 0; i < maxReads; i++)
            {
                // evaliate id of pack
                var id = rnd.Next(0, packsReady);
                var packOffset = packsOffset[id];
                packsOffset[id] = packsOffset[packsReady - 1];
                packsReady--;

                // read center
                sw.Restart();
                f.Position = packOffset + 2047;
                f.Read(buf, 0, 2);
                sw.Stop();
                centerDelays.Add(sw.Elapsed.TotalMilliseconds);

                // evaliate id of pack
                id = rnd.Next(0, packsReady);
                packOffset = packsOffset[id];
                packsOffset[id] = packsOffset[packsReady - 1];
                packsReady--;

                // read center
                sw.Restart();
                f.Position = packOffset + 4095;
                f.Read(buf, 0, 2);
                sw.Stop();
                edgeDelays.Add(sw.Elapsed.TotalMilliseconds);
            }

            var centerDelay = centerDelays.OrderBy(p => p).Skip(maxReads / 10).Take(maxReads - maxReads / 5).Average();
            var edgeDelay = edgeDelays.OrderBy(p => p).Skip(maxReads / 10).Take(maxReads - maxReads / 5).Average();
            Console.WriteLine($"Read: {path} Edge={edgeDelay:F6} Center={centerDelay:F6} (Reads={maxReads}, Time={swf.Elapsed})");
            return (edgeDelay, centerDelay);
        }
    }
}
