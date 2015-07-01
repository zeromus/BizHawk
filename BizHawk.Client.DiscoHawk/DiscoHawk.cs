﻿using System;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Collections.Generic;
using System.IO;

using BizHawk.Emulation.DiscSystem;

//cue format preferences notes

//pcejin -
//does not like session commands
//it can handle binpercue
//it seems not to be able to handle binpertrack, or maybe i am doing something wrong (still havent ruled it out)

namespace BizHawk.Client.DiscoHawk
{
	static class Program
	{
		static Program()
		{
#if WINDOWS
			//http://www.codeproject.com/Articles/310675/AppDomain-AssemblyResolve-Event-Tips
			// this will look in subdirectory "dll" to load pinvoked stuff
			string dllDir = System.IO.Path.Combine(GetExeDirectoryAbsolute(), "dll");
			SetDllDirectory(dllDir);

			//but before we even try doing that, whack the MOTW from everything in that directory (thats a dll)
			//otherwise, some people will have crashes at boot-up due to .net security disliking MOTW.
			//some people are getting MOTW through a combination of browser used to download bizhawk, and program used to dearchive it
			WhackAllMOTW(dllDir);

			//in case assembly resolution fails, such as if we moved them into the dll subdiretory, this event handler can reroute to them
			AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
#endif
		}

		[STAThread]
		static void Main(string[] args)
		{
			SubMain(args);
		}

		//NoInlining should keep this code from getting jammed into Main() which would create dependencies on types which havent been setup by the resolver yet... or something like that
		[DllImport("user32.dll", SetLastError = true)]
		public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, ChangeWindowMessageFilterExAction action, ref CHANGEFILTERSTRUCT changeInfo);
		private static class Win32
		{
			[DllImport("kernel32.dll")]
			public static extern IntPtr LoadLibrary(string dllToLoad);
			[DllImport("kernel32.dll")]
			public static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
			[DllImport("kernel32.dll")]
			public static extern bool FreeLibrary(IntPtr hModule);
		}

		static void SubMain(string[] args)
		{
			//MICROSOFT BROKE DRAG AND DROP IN WINDOWS 7. IT DOESNT WORK ANYMORE
			//WELL, OBVIOUSLY IT DOES SOMETIMES. I DONT REMEMBER THE DETAILS OR WHY WE HAD TO DO THIS SHIT
#if WINDOWS
			//BUT THE FUNCTION WE NEED DOESNT EXIST UNTIL WINDOWS 7, CONVENIENTLY
			//SO CHECK FOR IT
			IntPtr lib = Win32.LoadLibrary("user32.dll");
			IntPtr proc = Win32.GetProcAddress(lib, "ChangeWindowMessageFilterEx");
			if (proc != IntPtr.Zero)
			{
				ChangeWindowMessageFilter(WM_DROPFILES, ChangeWindowMessageFilterFlags.Add);
				ChangeWindowMessageFilter(WM_COPYDATA, ChangeWindowMessageFilterFlags.Add);
				ChangeWindowMessageFilter(0x0049, ChangeWindowMessageFilterFlags.Add);
			}
			Win32.FreeLibrary(lib);
#endif

			var ffmpegPath = Path.Combine(GetExeDirectoryAbsolute(), "ffmpeg.exe");
			if (!File.Exists(ffmpegPath))
				ffmpegPath = Path.Combine(Path.Combine(GetExeDirectoryAbsolute(), "dll"), "ffmpeg.exe");
			FFMpeg.FFMpegPath = ffmpegPath;
			AudioExtractor.FFmpegPath = ffmpegPath;
			new DiscoHawk().Run(args);
		}


		public static string GetExeDirectoryAbsolute()
		{
			var uri = new Uri(Assembly.GetEntryAssembly().GetName().CodeBase);
			string module = uri.LocalPath + System.Web.HttpUtility.UrlDecode(uri.Fragment);
			return Path.GetDirectoryName(module);
		}

		static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
		{
			lock (AppDomain.CurrentDomain)
			{
				var asms = AppDomain.CurrentDomain.GetAssemblies();
				foreach (var asm in asms)
					if (asm.FullName == args.Name)
						return asm;

				//load missing assemblies by trying to find them in the dll directory
				string dllname = new AssemblyName(args.Name).Name + ".dll";
				string directory = Path.Combine(GetExeDirectoryAbsolute(), "dll");
				string fname = Path.Combine(directory, dllname);
				if (!File.Exists(fname)) return null;
				//it is important that we use LoadFile here and not load from a byte array; otherwise mixed (managed/unamanged) assemblies can't load
				return Assembly.LoadFile(fname);
			}
		}

		//declared here instead of a more usual place to avoid dependencies on the more usual place
#if WINDOWS
		[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool SetDllDirectory(string lpPathName);

		[DllImport("kernel32.dll", EntryPoint = "DeleteFileW", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
		static extern bool DeleteFileW([MarshalAs(UnmanagedType.LPWStr)]string lpFileName);
		static void RemoveMOTW(string path)
		{
			DeleteFileW(path + ":Zone.Identifier");
		}

		//for debugging purposes, this is provided. when we're satisfied everyone understands whats going on, we'll get rid of this
		[DllImportAttribute("kernel32.dll", EntryPoint = "CreateFileW")]
		public static extern System.IntPtr CreateFileW([InAttribute()] [MarshalAsAttribute(UnmanagedType.LPWStr)] string lpFileName, int dwDesiredAccess, int dwShareMode, [InAttribute()] int lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, [InAttribute()] int hTemplateFile);
		static void ApplyMOTW(string path)
		{
			int generic_write = 0x40000000;
			int file_share_write = 2;
			int create_always = 2;
			var adsHandle = CreateFileW(path + ":Zone.Identifier", generic_write, file_share_write, 0, create_always, 0, 0);
			using (var sfh = new Microsoft.Win32.SafeHandles.SafeFileHandle(adsHandle, true))
			{
				var adsStream = new System.IO.FileStream(sfh, FileAccess.Write);
				StreamWriter sw = new StreamWriter(adsStream);
				sw.Write("[ZoneTransfer]\r\nZoneId=3");
				sw.Flush();
				adsStream.Close();
			}
		}

		static void WhackAllMOTW(string dllDir)
		{
			var todo = new Queue<DirectoryInfo>(new[] { new DirectoryInfo(dllDir) });
			while (todo.Count > 0)
			{
				var di = todo.Dequeue();
				foreach (var disub in di.GetDirectories()) todo.Enqueue(disub);
				foreach (var fi in di.GetFiles("*.dll"))
					RemoveMOTW(fi.FullName);
				foreach (var fi in di.GetFiles("*.exe"))
					RemoveMOTW(fi.FullName);
			}

		}
#endif

		private const UInt32 WM_DROPFILES = 0x0233;
		private const UInt32 WM_COPYDATA = 0x004A;
		[DllImport("user32")]
		public static extern bool ChangeWindowMessageFilter(uint msg, ChangeWindowMessageFilterFlags flags);
		public enum ChangeWindowMessageFilterFlags : uint
		{
			Add = 1, Remove = 2
		};
		public enum MessageFilterInfo : uint
		{
			None = 0, AlreadyAllowed = 1, AlreadyDisAllowed = 2, AllowedHigher = 3
		};

		public enum ChangeWindowMessageFilterExAction : uint
		{
			Reset = 0, Allow = 1, DisAllow = 2
		};

		[StructLayout(LayoutKind.Sequential)]
		public struct CHANGEFILTERSTRUCT
		{
			public uint size;
			public MessageFilterInfo info;
		}
	}

	class DiscoHawk
	{
		public void Run(string[] args)
		{
			if (args.Length == 0)
			{
				var dialog = new MainDiscoForm();
				dialog.ShowDialog();
				return;
			}

			string infile = null;
			var loadDiscInterface = DiscInterface.BizHawk;
			var compareDiscInterfaces = new List<DiscInterface> ();
			bool hawk = false;
			
			int idx = 0;
			while (idx < args.Length)
			{
				string a = args[idx++];
				string au = a.ToUpperInvariant();
				if (au == "LOAD")
					loadDiscInterface = (DiscInterface)Enum.Parse(typeof(DiscInterface), args[idx++], true);
				else if (au == "COMPARE")
					compareDiscInterfaces.Add((DiscInterface)Enum.Parse(typeof(DiscInterface), args[idx++], true));
				else if (au == "HAWK")
					hawk = true;
				else infile = a;
			}

			if (infile == null)
				return;
			
			var dmj = new DiscMountJob { IN_DiscInterface = loadDiscInterface, IN_FromPath = infile };
			dmj.Run();
			var disc = dmj.OUT_Disc;
			
			if(hawk) {} //todo - write it out

			StringWriter sw = new StringWriter();
			foreach (var cmpif in compareDiscInterfaces)
			{
				sw.WriteLine("BEGIN COMPARE: SRC {0} vs DST {1}", loadDiscInterface, cmpif);

				var src_disc = disc;

				var dst_dmj = new DiscMountJob { IN_DiscInterface = cmpif, IN_FromPath = infile };
				dst_dmj.Run();
				var dst_disc = dst_dmj.OUT_Disc;

				var src_dsr = new DiscSectorReader(src_disc);
				var dst_dsr = new DiscSectorReader(dst_disc);

				var src_toc = src_disc.TOCRaw;
				var dst_toc = dst_disc.TOCRaw;

				var src_databuf = new byte[2448];
				var dst_databuf = new byte[2448];

				Action<DiscTOCRaw.TOCItem> sw_dump_toc_one = (item) =>
					{
						if(!item.Exists)
							sw.Write("(---missing---)");
						else
							sw.Write("({0:X2} - {1})",(byte)item.Control,item.LBATimestamp);
					};

				Action<int> sw_dump_toc = (index) =>
				  {
						sw.Write("SRC TOC#{0,3} ",index); sw_dump_toc_one(src_toc.TOCItems[index]); sw.WriteLine();
						sw.Write("DST TOC#{0,3} ",index); sw_dump_toc_one(dst_toc.TOCItems[index]); sw.WriteLine();
				  };

				//verify sector count
				if (src_disc.LBACount != dst_disc.LBACount)
				{
					sw.Write("LBACount count {0} vs {1}\n", src_disc.LBACount, dst_disc.LBACount);
					goto SKIPPO;
				}

				//verify TOC match
				if (src_disc.TOCRaw.FirstRecordedTrackNumber != dst_disc.TOCRaw.FirstRecordedTrackNumber
					|| src_disc.TOCRaw.LastRecordedTrackNumber != dst_disc.TOCRaw.LastRecordedTrackNumber)
				{
					sw.WriteLine("Mismatch of RecordedTrackNumbers: {0}-{1} vs {2}-{3}",
						src_disc.TOCRaw.FirstRecordedTrackNumber, src_disc.TOCRaw.LastRecordedTrackNumber,
						dst_disc.TOCRaw.FirstRecordedTrackNumber, dst_disc.TOCRaw.LastRecordedTrackNumber
						);
					goto SKIPPO;
				}

				bool badToc = false;
				for (int t = 0; t < 101; t++)
				{
					if (src_toc.TOCItems[t].Exists != dst_toc.TOCItems[t].Exists
						|| src_toc.TOCItems[t].Control != dst_toc.TOCItems[t].Control
						|| src_toc.TOCItems[t].LBATimestamp.Sector != dst_toc.TOCItems[t].LBATimestamp.Sector
						)
					{
						sw.WriteLine("Mismatch in TOCItem");
						sw_dump_toc(t);
						badToc = true;
					}
				}
				if(badToc)
					goto SKIPPO;

				Action<string, int, byte[], int, int> sw_dump_chunk_one = (comment, lba, buf, addr, count) =>
				{
					sw.Write("{0} -  ", comment);
					for (int i = 0; i < count; i++)
					{
						if (i + addr >= buf.Length) continue;
						sw.Write("{0:X2}{1}", buf[addr + i], (i == count - 1) ? " " : "  ");
					}
					sw.WriteLine();
				};

				int[] offenders = new int[12];
				Action<int, int, int, int, int> sw_dump_chunk = (lba, dispaddr, addr, count, numoffenders) =>
				{
					var hashedOffenders = new HashSet<int>();
					for (int i = 0; i < numoffenders; i++) hashedOffenders.Add(offenders[i]);
					sw.Write("                         ");
					for (int i = 0; i < count; i++) sw.Write((hashedOffenders.Contains(dispaddr + i)) ? "vvv " : "    ");
					sw.WriteLine();
					sw.Write("                         ");
					for (int i = 0; i < count; i++) sw.Write("{0:X3} ", dispaddr + i, (i == count - 1) ? " " : "  ");
					sw.WriteLine();
					sw.Write("                         ");
					sw.Write(new string('-', count * 4));
					sw.WriteLine();
					sw_dump_chunk_one(string.Format("SRC #{0,6} ({1})", lba, new Timestamp(lba)), lba, src_databuf, addr, count);
					sw_dump_chunk_one(string.Format("DST #{0,6} ({1})", lba, new Timestamp(lba)), lba, dst_databuf, addr, count);
				};

				//verify each sector contents (skip the pregap junk for now)
				int nSectors = src_disc.LBACount;
				for (int lba = 0; lba < nSectors; lba++)
				{
					if (lba % 1000 == 0)
						Console.WriteLine("LBA {0} of {1}", lba, src_disc.LBACount);

					src_dsr.ReadLBA_2442(lba, src_databuf, 0);
					dst_dsr.ReadLBA_2442(lba, dst_databuf, 0);

					//check the header
					for (int b = 0; b < 16; b++)
					{
						if (src_databuf[b] != dst_databuf[b])
						{
							sw.WriteLine("Mismatch in sector header at byte {0}",b);
							offenders[0] = b;
							sw_dump_chunk(lba, 0, 0, 16, 1);
							goto SKIPPO;
						}
					}

					//check userdata
					for (int b = 16; b < 2352; b++)
					{
						if (src_databuf[b] != dst_databuf[b])
						{
							sw.Write("LBA {0} mismatch at userdata byte {1}; terminating sector cmp\n", lba, b);
							goto SKIPPO;
						}
					}

					//check subchannels
					for (int c = 0, b=2352; c < 8; c++)
					{
						int numOffenders = 0;
						for (int e = 0; e < 12; e++, b++)
						{
							if (src_databuf[b] != dst_databuf[b])
							{
								offenders[numOffenders++] = e;
							}
						}
						if (numOffenders != 0)
						{
							sw.Write("LBA {0} mismatch(es) at subchannel {1}; terminating sector cmp\n", lba, (char)('P' + c));
							sw_dump_chunk(lba, 0, 2352 + c * 12, 12, numOffenders);
							goto SKIPPO;
						}
					}

				}

			SKIPPO:
				sw.WriteLine("END COMPARE");
				sw.WriteLine("-----------------------------");
			}

			sw.Flush();
			var cr = new ComparisonResults();
			string results = sw.ToString();
			cr.textBox1.Text = results;
			cr.ShowDialog();
		}
	}

}

//code to test ECM:
//static class test
//{
//  public static void Shuffle<T>(this IList<T> list, Random rng)
//  {
//    int n = list.Count;
//    while (n > 1)
//    {
//      n--;
//      int k = rng.Next(n + 1);
//      T value = list[k];
//      list[k] = list[n];
//      list[n] = value;
//    }
//  }

//  public static void Test()
//  {
//    var plaindisc = BizHawk.DiscSystem.Disc.FromCuePath("d:\\ecmtest\\test.cue", BizHawk.MainDiscoForm.GetCuePrefs());
//    var ecmdisc = BizHawk.DiscSystem.Disc.FromCuePath("d:\\ecmtest\\ecmtest.cue", BizHawk.MainDiscoForm.GetCuePrefs());

//    //var prefs = new BizHawk.DiscSystem.CueBinPrefs();
//    //prefs.AnnotateCue = false;
//    //prefs.OneBlobPerTrack = false;
//    //prefs.ReallyDumpBin = true;
//    //prefs.SingleSession = true;
//    //prefs.DumpToBitbucket = true;
//    //var dump = ecmdisc.DumpCueBin("test", prefs);
//    //dump.Dump("test", prefs);

//    //var prefs = new BizHawk.DiscSystem.CueBinPrefs();
//    //prefs.AnnotateCue = false;
//    //prefs.OneBlobPerTrack = false;
//    //prefs.ReallyDumpBin = true;
//    //prefs.SingleSession = true;
//    //var dump = ecmdisc.DumpCueBin("test", prefs);
//    //dump.Dump(@"D:\ecmtest\myout", prefs);

//    int seed = 102;

//    for (; ; )
//    {
//      Console.WriteLine("running seed {0}", seed);
//      Random r = new Random(seed);
//      seed++;

//      byte[] chunkbuf_corlet = new byte[2352 * 20];
//      byte[] chunkbuf_mine = new byte[2352 * 20];
//      int length = ecmdisc.LBACount * 2352;
//      int counter = 0;
//      List<Tuple<int, int>> testChunks = new List<Tuple<int, int>>();
//      while (counter < length)
//      {
//        int chunk = r.Next(1, 2352 * 20);
//        if (r.Next(20) == 0)
//          chunk /= 100;
//        if (r.Next(40) == 0)
//          chunk = 0;
//        if (counter + chunk > length)
//          chunk = length - counter;
//        testChunks.Add(new Tuple<int, int>(counter, chunk));
//        counter += chunk;
//      }
//      testChunks.Shuffle(r);

//      for (int t = 0; t < testChunks.Count; t++)
//      {
//        //Console.WriteLine("skank");
//        var item = testChunks[t];
//        //Console.WriteLine("chunk {0} of {3} is {1} bytes @ {2:X8}", t, item.Item2, item.Item1, testChunks.Count);
//        plaindisc.ReadLBA_2352_Flat(item.Item1, chunkbuf_corlet, 0, item.Item2);
//        ecmdisc.ReadLBA_2352_Flat(item.Item1, chunkbuf_mine, 0, item.Item2);
//        for (int i = 0; i < item.Item2; i++)
//          if (chunkbuf_corlet[i] != chunkbuf_mine[i])
//          {
//            Debug.Assert(false);
//          }
//      }
//    }
//  }
//}