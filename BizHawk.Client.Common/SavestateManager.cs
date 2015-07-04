﻿using System;
using System.IO;
using System.Linq;

using BizHawk.Common;
using BizHawk.Common.BufferExtensions;
using BizHawk.Common.IOExtensions;
using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;
using System.Collections.Generic;

namespace BizHawk.Client.Common
{
	public static class SavestateManager
	{
		public static void SaveStateFile(string filename, string name)
		{
			var core = Global.Emulator.AsStatable();
			// the old method of text savestate save is now gone.
			// a text savestate is just like a binary savestate, but with a different core lump
			using (var bs = new BinaryStateSaver(filename))
			{
				if (Global.Config.SaveStateType == Config.SaveStateTypeE.Text ||
					(Global.Config.SaveStateType == Config.SaveStateTypeE.Default && !core.BinarySaveStatesPreferred))
				{
					// text savestate format
					using (new SimpleTime("Save Core"))
						bs.PutLump(BinaryStateLump.CorestateText, (tw) => core.SaveStateText(tw));
				}
				else
				{
					// binary core lump format
					using (new SimpleTime("Save Core"))
						bs.PutLump(BinaryStateLump.Corestate, bw => core.SaveStateBinary(bw));
				}

				if (Global.Config.SaveScreenshotWithStates)
				{
					var vp = Global.Emulator.VideoProvider();
					var buff = vp.GetVideoBuffer();

					int out_w = vp.BufferWidth;
					int out_h = vp.BufferHeight;

					// if buffer is too big, scale down screenshot
					if (!Global.Config.NoLowResLargeScreenshotWithStates && buff.Length >= Global.Config.BigScreenshotSize)
					{
						out_w /= 2;
						out_h /= 2;
					}
					using (new SimpleTime("Save Framebuffer"))
						bs.PutLump(BinaryStateLump.Framebuffer, (s) => QuickBmpFile.Save(Global.Emulator.VideoProvider(), s, out_w, out_h));
				}

				if (Global.MovieSession.Movie.IsActive)
				{
					bs.PutLump(BinaryStateLump.Input,
						delegate(TextWriter tw)
						{
							// this never should have been a core's responsibility
							tw.WriteLine("Frame {0}", Global.Emulator.Frame);
							Global.MovieSession.HandleMovieSaveState(tw);
						});
				}

				if (Global.UserBag.Any())
				{
					bs.PutLump(BinaryStateLump.UserData,
						delegate(TextWriter tw)
						{
							var data = ConfigService.SaveWithType(Global.UserBag);
							tw.WriteLine(data);
						});
				}
			}
		}

		public static void PopulateFramebuffer(BinaryReader br)
		{
			try
			{
				using (new SimpleTime("Load Framebuffer"))
					QuickBmpFile.Load(Global.Emulator.VideoProvider(), br.BaseStream);
			}
			catch
			{
				var buff = Global.Emulator.VideoProvider().GetVideoBuffer();
				try
				{
					for (int i = 0; i < buff.Length; i++)
					{
						int j = br.ReadInt32();
						buff[i] = j;
					}
				}
				catch (EndOfStreamException) { }
			}
		}

		public static bool LoadStateFile(string path, string name)
		{
			var core = Global.Emulator.AsStatable();

			// try to detect binary first
			var bl = BinaryStateLoader.LoadAndDetect(path);
			if (bl != null)
			{
				try
				{
					var succeed = false;

					if (Global.MovieSession.Movie.IsActive)
					{
						bl.GetLump(BinaryStateLump.Input, true, tr => succeed = Global.MovieSession.HandleMovieLoadState_HackyStep1(tr));
						if (!succeed)
						{
							return false;
						}

						bl.GetLump(BinaryStateLump.Input, true, tr => succeed = Global.MovieSession.HandleMovieLoadState_HackyStep2(tr));
						if (!succeed)
						{
							return false;
						}
					}

					using (new SimpleTime("Load Core"))
						bl.GetCoreState(br => core.LoadStateBinary(br), tr => core.LoadStateText(tr));

					bl.GetLump(BinaryStateLump.Framebuffer, false, PopulateFramebuffer);

					if (bl.HasLump(BinaryStateLump.UserData))
					{
						string userData = string.Empty;
						bl.GetLump(BinaryStateLump.UserData, false, delegate(TextReader tr)
						{
							string line;
							while ((line = tr.ReadLine()) != null)
							{
								if (!string.IsNullOrWhiteSpace(line))
								{
									userData = line;
								}
							}
						});

						Global.UserBag = (Dictionary<string, object>)ConfigService.LoadWithType(userData);
					}
				}
				catch
				{
					return false;
				}
				finally
				{
					bl.Dispose();
				}

				return true;
			}
			else // text mode
			{
				if (Global.MovieSession.HandleMovieLoadState(path))
				{
					using (var reader = new StreamReader(path))
					{
						core.LoadStateText(reader);

						while (true)
						{
							var str = reader.ReadLine();
							if (str == null)
							{
								break;
							}
							
							if (str.Trim() == string.Empty)
							{
								continue;
							}

							var args = str.Split(' ');
							if (args[0] == "Framebuffer")
							{
								Global.Emulator.VideoProvider().GetVideoBuffer().ReadFromHex(args[1]);
							}
						}
					}

					return true;
				}
				else
				{
					return false;
				}
			}
		}
	}
}
