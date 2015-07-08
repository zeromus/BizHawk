﻿//TODO hook up newer file ID stuff, think about how to combine it with the disc ID
//TODO change display manager to not require 0xFF alpha channel set on videoproviders. check gdi+ and opengl! this will get us a speedup in some places
//TODO Disc.Structure.Sessions[0].length_aba was 0
//TODO mednafen 0.9.37 changed some disc region detection heuristics. analyze and apply in c# side. also the SCEX id handling changed, maybe simplified

//TODO - ok, think about this. we MUST load a state with the CDC completely intact. no quickly changing discs. thats madness.
//well, I could savestate the disc index and validate the disc collection when loading a state.
//the big problem is, it's completely at odds with the slider-based disc changing model. 
//but, maybe it can be reconciled with that model by using the disc ejection to our advantage. 
//perhaps moving the slider is meaningless if the disc is ejected--it only affects what disc is inserted when the disc gets inserted!! yeah! this might could save us!
//not exactly user friendly but maybe we can build it from there with a custom UI.. a disk-changer? dunno if that would help

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;


using BizHawk.Emulation.Common;
using BizHawk.Common;

#pragma warning disable 649 //adelikat: Disable dumb warnings until this file is complete

namespace BizHawk.Emulation.Cores.Sony.PSX
{
	[CoreAttributes(
		"Octoshock",
		"Ryphecha",
		isPorted: true,
		isReleased: false
		)]
	public unsafe class Octoshock : IEmulator, IVideoProvider, ISyncSoundProvider, ISaveRam, IStatable, IDriveLight, IInputPollable, ISettable<Octoshock.Settings, Octoshock.SyncSettings>, IDebuggable
	{
		public string SystemId { get { return "PSX"; } }

		private void SetControllerButtons()
		{
			ControllerDefinition = new ControllerDefinition();
			ControllerDefinition.Name = _SyncSettings.Controllers.All(c => c.Type == ControllerSetting.ControllerType.Gamepad) 
				? "PSX Gamepad Controller"
				: "PSX DualShock Controller"; // Meh, more nuanced logic doesn't really work with a simple property

			ControllerDefinition.BoolButtons.Clear();
			ControllerDefinition.FloatControls.Clear();
			
			for (int i = 0; i < _SyncSettings.Controllers.Length; i++)
			{
				if (_SyncSettings.Controllers[i].IsConnected)
				{
					ControllerDefinition.BoolButtons.AddRange(new[]
					{
						"P" + (i + 1) + " Up",
						"P" + (i + 1) + " Down",
						"P" + (i + 1) + " Left",
						"P" + (i + 1) + " Right",
						"P" + (i + 1) + " Select",
						"P" + (i + 1) + " Start",
						"P" + (i + 1) + " Square",
						"P" + (i + 1) + " Triangle",
						"P" + (i + 1) + " Circle",
						"P" + (i + 1) + " Cross",
						"P" + (i + 1) + " L1", 
						"P" + (i + 1) + " R1",
						"P" + (i + 1) + " L2",
						"P" + (i + 1) + " R2",
					});

					if (_SyncSettings.Controllers[i].Type != ControllerSetting.ControllerType.Gamepad)
					{
						ControllerDefinition.BoolButtons.Add("P" + (i + 1) + " L3");
						ControllerDefinition.BoolButtons.Add("P" + (i + 1) + " R3");
						ControllerDefinition.BoolButtons.Add("P" + (i + 1) + " MODE");

						ControllerDefinition.FloatControls.AddRange(new[]
						{
							"P" + (i + 1) + " LStick X",
							"P" + (i + 1) + " LStick Y",
							"P" + (i + 1) + " RStick X",
							"P" + (i + 1) + " RStick Y"
						});

						ControllerDefinition.FloatRanges.Add(new[] { 0.0f, 128.0f, 255.0f });
						ControllerDefinition.FloatRanges.Add(new[] { 255.0f, 128.0f, 0.0f });
						ControllerDefinition.FloatRanges.Add(new[] { 0.0f, 128.0f, 255.0f });
						ControllerDefinition.FloatRanges.Add(new[] { 255.0f, 128.0f, 0.0f });
					}
				}
			}

			ControllerDefinition.BoolButtons.AddRange(new[]
			{
				"Open",
				"Close",
				"Reset"
			});

			ControllerDefinition.FloatControls.Add("Disc Select");

			ControllerDefinition.FloatRanges.Add(
				new[] {-1f,-1f,-1f} //this is carefully chosen so that we end up with a -1 disc by default (indicating that it's never been set)
			);
		}

		public string BoardName { get { return null; } }

		private int[] frameBuffer = new int[0];
		private Random rand = new Random();
		public CoreComm CoreComm { get; private set; }

		//we can only have one active core at a time, due to the lib being so static.
		//so we'll track the current one here and detach the previous one whenever a new one is booted up.
		static Octoshock CurrOctoshockCore;

		IntPtr psx;
		TraceBuffer tracer = new TraceBuffer();

		bool disposed = false;
		public void Dispose()
		{
			if (disposed) return;

			OctoshockDll.shock_Destroy(psx);
			psx = IntPtr.Zero;

			disposed = true;

			//TODO - dispose disc wrappers
			//TODO - dispose discs
		}

		/// <summary>
		/// Wraps the ShockDiscRef returned from the DLL and acts as a bridge between it and a DiscSystem disc
		/// </summary>
		class DiscInterface : IDisposable
		{
			public DiscInterface(DiscSystem.Disc disc, Action<DiscInterface> cbActivity)
			{
				this.Disc = disc;
				cbReadTOC = ShockDisc_ReadTOC;
				cbReadLBA = ShockDisc_ReadLBA2448;
				this.cbActivity = cbActivity;
				OctoshockDll.shock_CreateDisc(out OctoshockHandle, IntPtr.Zero, disc.Session1.LeadoutLBA, cbReadTOC, cbReadLBA, true);
			}

			OctoshockDll.ShockDisc_ReadTOC cbReadTOC;
			OctoshockDll.ShockDisc_ReadLBA cbReadLBA;
			Action<DiscInterface> cbActivity;

			public DiscSystem.Disc Disc;
			public IntPtr OctoshockHandle;

			public void Dispose()
			{
				OctoshockDll.shock_DestroyDisc(OctoshockHandle);
				OctoshockHandle = IntPtr.Zero;
			}

			int ShockDisc_ReadTOC(IntPtr opaque, OctoshockDll.ShockTOC* read_target, OctoshockDll.ShockTOCTrack* tracks101)
			{
				read_target->disc_type = (byte)Disc.TOC.Session1Format;
				read_target->first_track = (byte)Disc.TOC.FirstRecordedTrackNumber; //i _think_ thats what is meant here
				read_target->last_track = (byte)Disc.TOC.LastRecordedTrackNumber; //i _think_ thats what is meant here

				tracks101[0].lba = tracks101[0].adr = tracks101[0].control = 0;

				for (int i = 1; i < 100; i++)
				{
					var item = Disc.TOC.TOCItems[i];
					tracks101[i].adr = (byte)(item.Exists ? 1 : 0);
					tracks101[i].lba = (uint)item.LBATimestamp.Sector;
					tracks101[i].control = (byte)item.Control;
				}

				////the lead-out track is to be synthesized
				tracks101[read_target->last_track + 1].adr = 1;
				tracks101[read_target->last_track + 1].control = 0;
				tracks101[read_target->last_track + 1].lba = (uint)Disc.TOC.LeadoutLBA.Sector;

				//element 100 is to be copied as the lead-out track
				tracks101[100] = tracks101[read_target->last_track + 1];

				return OctoshockDll.SHOCK_OK;
			}

			byte[] SectorBuffer = new byte[2448];

			int ShockDisc_ReadLBA2448(IntPtr opaque, int lba, void* dst)
			{
				cbActivity(this);

				//lets you check subcode generation by logging it and checking against the CCD subcode
				bool subcodeLog = false;
				bool readLog = false;

				if (subcodeLog) Console.Write("{0}|", lba);
				else if (readLog) Console.WriteLine("Read Sector: " + lba);

				//todo - cache reader
				DiscSystem.DiscSectorReader dsr = new DiscSystem.DiscSectorReader(Disc);
				dsr.ReadLBA_2448(lba, SectorBuffer, 0);
				Marshal.Copy(SectorBuffer, 0, new IntPtr(dst), 2448);

				//if (subcodeLog)
				//{
				//  for (int i = 0; i < 24; i++)
				//    Console.Write("{0:X2}", *((byte*)dst + 2352 + i));
				//  Console.WriteLine();
				//}

				return OctoshockDll.SHOCK_OK;
			}
		}

		List<DiscInterface> discInterfaces = new List<DiscInterface>();
		DiscInterface currentDiscInterface;

		public OctoshockDll.eRegion SystemRegion { get; private set; }
		public OctoshockDll.eVidStandard SystemVidStandard { get; private set; }
		public System.Drawing.Size CurrentVideoSize { get; private set; }
		
		public bool CurrentTrayOpen { get; private set; }
		public int CurrentDiscIndexMounted { get; private set; }

		public List<string> HackyDiscButtons = new List<string>();

		//note: its annoying that we have to have a disc before constructing this.
		//might want to change that later. HOWEVER - we need to definitely have a region, at least
		public Octoshock(CoreComm comm, List<DiscSystem.Disc> discs, List<string> discNames, byte[] exe, object settings, object syncSettings)
		{
			ServiceProvider = new BasicServiceProvider(this);
			(ServiceProvider as BasicServiceProvider).Register<ITraceable>(tracer);
			CoreComm = comm;
			DriveLightEnabled = true;

			_Settings = (Settings)settings ?? new Settings();
			_SyncSettings = (SyncSettings)syncSettings ?? new SyncSettings();

			Attach();

			HackyDiscButtons.AddRange(discNames);

			//assume this region for EXE and PSF, maybe not correct though
			string firmwareRegion = "U";
			SystemRegion = OctoshockDll.eRegion.NA;

			if (discs != null)
			{
				foreach (var disc in discs)
				{
					var discInterface = new DiscInterface(disc,
						(di) =>
						{
							//if current disc this delegate disc, activity is happening
							if (di == currentDiscInterface)
								DriveLightOn = true;
						});

					discInterfaces.Add(discInterface);
				}
			}
			else
			{
				//assume its NA region for test programs, for now. could it be read out of the ps-exe header?
			}

			if (discInterfaces.Count != 0)
			{
				//determine region of one of the discs
				OctoshockDll.ShockDiscInfo discInfo;
				OctoshockDll.shock_AnalyzeDisc(discInterfaces[0].OctoshockHandle, out discInfo);

				//try to acquire the appropriate firmware
				if (discInfo.region == OctoshockDll.eRegion.EU) firmwareRegion = "E";
				if (discInfo.region == OctoshockDll.eRegion.JP) firmwareRegion = "J";
				SystemRegion = discInfo.region;
			}

			//see http://problemkaputt.de/psx-spx.htm
			int CpuClock_n = 44100 * 768;
			int CpuClock_d = 1;
			int VidClock_n = CpuClock_n * 11;
			int VidClock_d = CpuClock_d * 7;
			if (SystemRegion == OctoshockDll.eRegion.EU)
			{
				CoreComm.VsyncNum = VidClock_n;
				CoreComm.VsyncDen = VidClock_d * 314 * 3406;
				SystemVidStandard = OctoshockDll.eVidStandard.PAL;
			}
			else
			{
				CoreComm.VsyncNum = VidClock_n;
				CoreComm.VsyncDen = VidClock_d * 263 * 3413;
				SystemVidStandard = OctoshockDll.eVidStandard.NTSC;
			}

			//TODO - known bad firmwares are a no-go. we should refuse to boot them. (thats the mednafen policy)
			byte[] firmware = comm.CoreFileProvider.GetFirmware("PSX", firmwareRegion, true, "A PSX `" + firmwareRegion + "` region bios file is required");

			//create the instance
			fixed (byte* pFirmware = firmware)
				OctoshockDll.shock_Create(out psx, SystemRegion, pFirmware);

			SetMemoryDomains();

			//set a default framebuffer based on the first frame of emulation, to cut down on flickering or whatever
			//this is probably quixotic, but we have to pick something
			{
				BufferWidth = 280;
				BufferHeight = 240;
				if (SystemVidStandard == OctoshockDll.eVidStandard.PAL)
				{
					BufferWidth = 280;
					BufferHeight = 288;
				}
				CurrentVideoSize = new System.Drawing.Size(BufferWidth, BufferHeight);
				var size = Octoshock.CalculateResolution(SystemVidStandard, _Settings, BufferWidth, BufferHeight);
				BufferWidth = VirtualWidth = size.Width;
				BufferHeight = VirtualHeight = size.Height;
				frameBuffer = new int[BufferWidth * BufferHeight];
			}

			if (discInterfaces.Count != 0)
			{
				//start with first disc inserted and tray closed. it's a sensible default.
				//it will be possible for the user to specify a different initial configuration, but this will inform the UI
				CurrentTrayOpen = false;
				CurrentDiscIndexMounted = 1;
			}
			else
			{
				//must be an exe
				fixed (byte* pExeBuffer = exe)
					OctoshockDll.shock_MountEXE(psx, pExeBuffer, exe.Length);

				//start with no disc inserted and tray closed
				CurrentTrayOpen = false;
				CurrentDiscIndexMounted = 0;
				OctoshockDll.shock_CloseTray(psx);
			}

			//setup the controller based on sync settings
			SetControllerButtons();

			var lookup = new Dictionary<ControllerSetting.ControllerType,OctoshockDll.ePeripheralType> {
				{ ControllerSetting.ControllerType.Gamepad, OctoshockDll.ePeripheralType.Pad },
				{ ControllerSetting.ControllerType.DualAnalog, OctoshockDll.ePeripheralType.DualAnalog },
				{ ControllerSetting.ControllerType.DualShock, OctoshockDll.ePeripheralType.DualShock },
			};

			if (_SyncSettings.Controllers[0].IsConnected)
			{
				OctoshockDll.shock_Peripheral_Connect(psx, 0x01, lookup[_SyncSettings.Controllers[0].Type]);
			}

			if (_SyncSettings.Controllers[1].IsConnected)
			{
				OctoshockDll.shock_Peripheral_Connect(psx, 0x02, lookup[_SyncSettings.Controllers[1].Type]);
			}

			//do this after framebuffers and peripherals and whatever crap are setup. kind of lame, but thats how it is for now
			StudySaveBufferSize();

			OctoshockDll.shock_PowerOn(psx);
		}

		public IEmulatorServiceProvider ServiceProvider { get; private set; }

		public IInputCallbackSystem InputCallbacks { get { throw new NotImplementedException(); } }

		public bool DriveLightEnabled { get; private set; }
		public bool DriveLightOn { get; private set; }

		void Attach()
		{
			//attach this core as the current
			if (CurrOctoshockCore != null)
				CurrOctoshockCore.Dispose();
			CurrOctoshockCore = this;

			//the psx instance cant be created until the desired region is known, which needs a disc, so we need the dll static attached first
		}

		static Octoshock()
		{
		}


		public void ResetCounters()
		{
			Frame = 0;
			LagCount = 0;
			IsLagFrame = false;
		}

		void SetInput()
		{
			uint buttons = 0;

			if (_SyncSettings.Controllers[0].IsConnected)
			{
				//dualshock style
				if (Controller["P1 Select"]) buttons |= 1;
				if (Controller["P1 L3"]) buttons |= 2;
				if (Controller["P1 R3"]) buttons |= 4;
				if (Controller["P1 Start"]) buttons |= 8;
				if (Controller["P1 Up"]) buttons |= 16;
				if (Controller["P1 Right"]) buttons |= 32;
				if (Controller["P1 Down"]) buttons |= 64;
				if (Controller["P1 Left"]) buttons |= 128;
				if (Controller["P1 L2"]) buttons |= 256;
				if (Controller["P1 R2"]) buttons |= 512;
				if (Controller["P1 L1"]) buttons |= 1024;
				if (Controller["P1 R1"]) buttons |= 2048;
				if (Controller["P1 Triangle"]) buttons |= 4096;
				if (Controller["P1 Circle"]) buttons |= 8192;
				if (Controller["P1 Cross"]) buttons |= 16384;
				if (Controller["P1 Square"]) buttons |= 32768;
				if (Controller["P1 MODE"]) buttons |= 65536;

				byte left_x = (byte)Controller.GetFloat("P1 LStick X");
				byte left_y = (byte)Controller.GetFloat("P1 LStick Y");
				byte right_x = (byte)Controller.GetFloat("P1 RStick X");
				byte right_y = (byte)Controller.GetFloat("P1 RStick Y");

				OctoshockDll.shock_Peripheral_SetPadInput(psx, 0x01, buttons, left_x, left_y, right_x, right_y);
			}

			if (_SyncSettings.Controllers[1].IsConnected)
			{
				//dualshock style
				buttons = 0;
				if (Controller["P2 Select"]) buttons |= 1;
				if (Controller["P2 L3"]) buttons |= 2;
				if (Controller["P2 R3"]) buttons |= 4;
				if (Controller["P2 Start"]) buttons |= 8;
				if (Controller["P2 Up"]) buttons |= 16;
				if (Controller["P2 Right"]) buttons |= 32;
				if (Controller["P2 Down"]) buttons |= 64;
				if (Controller["P2 Left"]) buttons |= 128;
				if (Controller["P2 L2"]) buttons |= 256;
				if (Controller["P2 R2"]) buttons |= 512;
				if (Controller["P2 L1"]) buttons |= 1024;
				if (Controller["P2 R1"]) buttons |= 2048;
				if (Controller["P2 Triangle"]) buttons |= 4096;
				if (Controller["P2 Circle"]) buttons |= 8192;
				if (Controller["P2 Cross"]) buttons |= 16384;
				if (Controller["P2 Square"]) buttons |= 32768;
				if (Controller["P2 MODE"]) buttons |= 65536;

				byte left_x = (byte)Controller.GetFloat("P2 LStick X");
				byte left_y = (byte)Controller.GetFloat("P2 LStick Y");
				byte right_x = (byte)Controller.GetFloat("P2 RStick X");
				byte right_y = (byte)Controller.GetFloat("P2 RStick Y");

				OctoshockDll.shock_Peripheral_SetPadInput(psx, 0x02, buttons, left_x, left_y, right_x, right_y);
			}
		}

		/// <summary>
		/// Calculates what the output resolution would be for the given input resolution and settings
		/// </summary>
		public static System.Drawing.Size CalculateResolution(OctoshockDll.eVidStandard standard, Settings settings, int w, int h)
		{
			int virtual_width = settings.ClipHorizontalOverscan ? 768 : 800;

			int scanline_start = standard == OctoshockDll.eVidStandard.NTSC ? settings.ScanlineStart_NTSC : settings.ScanlineStart_PAL;
			int scanline_end = standard == OctoshockDll.eVidStandard.NTSC ? settings.ScanlineEnd_NTSC : settings.ScanlineEnd_PAL;
			int scanline_num = scanline_end - scanline_start + 1;
			int real_scanline_num = standard == OctoshockDll.eVidStandard.NTSC ? 240 : 288;

			int VirtualWidth=-1, VirtualHeight=-1;
			switch (settings.ResolutionMode)
			{
				case eResolutionMode.Debug:
					VirtualWidth = w;
					VirtualHeight = h;
					break;
				case eResolutionMode.Mednafen:
					VirtualWidth = settings.ClipHorizontalOverscan ? 302 : 320;
					VirtualHeight = scanline_num;
					break;
				case eResolutionMode.PixelPro:
					VirtualWidth = virtual_width;
					VirtualHeight = scanline_num * 2;
					break;
				case eResolutionMode.TweakedMednafen:
					VirtualWidth = settings.ClipHorizontalOverscan ? 378 : 400;
					VirtualHeight = (int)(scanline_num * 300.0f / real_scanline_num);
					break;
			}

			return new System.Drawing.Size(VirtualWidth, VirtualHeight);
		}

		void PokeDisc()
		{
			if (CurrentDiscIndexMounted == 0)
			{
				currentDiscInterface = null;
				OctoshockDll.shock_PokeDisc(psx, IntPtr.Zero);
			}
			else
			{
				currentDiscInterface = discInterfaces[CurrentDiscIndexMounted - 1];
				OctoshockDll.shock_PokeDisc(psx, currentDiscInterface.OctoshockHandle);
			}
		}

		void FrameAdvance_PrepDiscState()
		{
			//reminder: if this is the beginning of time, we can begin with the disc ejected or inserted.

			//if tray open is requested, and valid, apply it
			//in the first frame, go ahead and open it up so we have a chance to put a disc in it
			if (Controller["Open"] && !CurrentTrayOpen || Frame==0)
			{
				OctoshockDll.shock_OpenTray(psx);
				CurrentTrayOpen = true;
			}

			//change the disc if needed, and valid
			//also if frame is 0, we need to set a disc no matter what
			int requestedDisc = (int)Controller.GetFloat("Disc Select");
			if (requestedDisc != CurrentDiscIndexMounted && CurrentTrayOpen
				|| Frame == 0
				)
			{
				//dont replace default disc with the leave-default placeholder!
				if (requestedDisc == -1)
				{

				}
				else
				{
					CurrentDiscIndexMounted = requestedDisc;
				}

				if (CurrentDiscIndexMounted == 0)
				{
					currentDiscInterface = null;
					OctoshockDll.shock_SetDisc(psx, IntPtr.Zero);
				}
				else
				{
					currentDiscInterface = discInterfaces[CurrentDiscIndexMounted - 1];
					OctoshockDll.shock_SetDisc(psx, currentDiscInterface.OctoshockHandle);
				}
			}

			//if tray close is requested, and valid, apply it.
			if (Controller["Close"] && CurrentTrayOpen)
			{
				OctoshockDll.shock_CloseTray(psx);
				CurrentTrayOpen = false;
			}

			//if frame is 0 and user has made no preference, close the tray
			if (!Controller["Close"] && !Controller["Open"] && Frame == 0 && CurrentTrayOpen)
			{
				OctoshockDll.shock_CloseTray(psx);
				CurrentTrayOpen = false;
			}
		}

		public void FrameAdvance(bool render, bool rendersound)
		{
			FrameAdvance_PrepDiscState();

			//clear drive light. itll get set to light up by sector-reading callbacks
			//TODO - debounce this by a frame or so perhaps?
			DriveLightOn = false;

			Frame++;

			SetInput();

			var ropts = new OctoshockDll.ShockRenderOptions()
			{
				scanline_start = SystemVidStandard == OctoshockDll.eVidStandard.NTSC ? _Settings.ScanlineStart_NTSC : _Settings.ScanlineStart_PAL,
				scanline_end = SystemVidStandard == OctoshockDll.eVidStandard.NTSC ? _Settings.ScanlineEnd_NTSC : _Settings.ScanlineEnd_PAL,
				clipOverscan = _Settings.ClipHorizontalOverscan
			};
			OctoshockDll.shock_SetRenderOptions(psx, ref ropts);

			//prep tracer
			if (tracer.Enabled)
				OctoshockDll.shock_SetTraceCallback(psx, IntPtr.Zero, ShockTraceCallback);
			else
				OctoshockDll.shock_SetTraceCallback(psx, IntPtr.Zero, null);

			//------------------------
			OctoshockDll.shock_Step(psx, OctoshockDll.eShockStep.Frame);
			//------------------------

			//lag maintenance:
			int pad1 = OctoshockDll.shock_Peripheral_PollActive(psx, 0x01, true);
			int pad2 = OctoshockDll.shock_Peripheral_PollActive(psx, 0x02, true);
			IsLagFrame = true;
			if (pad1 == OctoshockDll.SHOCK_TRUE) IsLagFrame = false;
			if (pad2 == OctoshockDll.SHOCK_TRUE) IsLagFrame = false;
			if (IsLagFrame)
				LagCount++;

			//what happens to sound in this case?
			if (render == false) return;

			OctoshockDll.ShockFramebufferInfo fb = new OctoshockDll.ShockFramebufferInfo();

			//run this once to get current logical size
			OctoshockDll.shock_GetFramebuffer(psx, ref fb);
			CurrentVideoSize = new System.Drawing.Size(fb.width, fb.height);

			if (_Settings.ResolutionMode == eResolutionMode.PixelPro)
				fb.flags = OctoshockDll.eShockFramebufferFlags.Normalize;

			OctoshockDll.shock_GetFramebuffer(psx, ref fb);

			int w = fb.width;
			int h = fb.height;
			BufferWidth = w;
			BufferHeight = h;

			var size = CalculateResolution(this.SystemVidStandard, _Settings, w, h);
			VirtualWidth = size.Width;
			VirtualHeight = size.Height;

			int len = w * h;
			if (frameBuffer.Length != len)
			{
				Console.WriteLine("PSX FB size: {0},{1}", fb.width, fb.height);
				frameBuffer = new int[len];
			}

			fixed (int* ptr = frameBuffer)
			{
				fb.ptr = ptr;
				OctoshockDll.shock_GetFramebuffer(psx, ref fb);
			}

			fixed (short* samples = sbuff)
			{
				sbuffcontains = OctoshockDll.shock_GetSamples(psx, null);
				if (sbuffcontains * 2 > sbuff.Length) throw new InvalidOperationException("shock_GetSamples returned too many samples: " + sbuffcontains);
				OctoshockDll.shock_GetSamples(psx, samples);
			}
		}

		public ControllerDefinition ControllerDefinition { get; private set; }
		public IController Controller { get; set; }

		public int Frame { get; private set; }
		public int LagCount { get; private set; }
		public bool IsLagFrame { get; private set; }

		[FeatureNotImplemented]
		public bool DeterministicEmulation { get { return true; } }

		public int[] GetVideoBuffer() { return frameBuffer; }
		public int VirtualWidth { get; private set; }
		public int VirtualHeight { get; private set; }
		public int BufferWidth { get; private set; }
		public int BufferHeight { get; private set; }
		public int BackgroundColor { get { return 0; } }

		#region Debugging

		unsafe void SetMemoryDomains()
		{
			var mmd = new List<MemoryDomain>();
			IntPtr ptr;
			int size;

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.MainRAM);
			mmd.Add(MemoryDomain.FromIntPtr("MainRAM", size, MemoryDomain.Endian.Little, ptr, true, 4));

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.GPURAM);
			mmd.Add(MemoryDomain.FromIntPtr("GPURAM", size, MemoryDomain.Endian.Little, ptr, true, 4));

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.SPURAM);
			mmd.Add(MemoryDomain.FromIntPtr("SPURAM", size, MemoryDomain.Endian.Little, ptr, true, 4));

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.BiosROM);
			mmd.Add(MemoryDomain.FromIntPtr("BiosROM", size, MemoryDomain.Endian.Little, ptr, true, 4));

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.PIOMem);
			mmd.Add(MemoryDomain.FromIntPtr("PIOMem", size, MemoryDomain.Endian.Little, ptr, true, 4));

			OctoshockDll.shock_GetMemData(psx, out ptr, out size, OctoshockDll.eMemType.DCache);
			mmd.Add(MemoryDomain.FromIntPtr("DCache", size, MemoryDomain.Endian.Little, ptr, true, 4));

			MemoryDomains = new MemoryDomainList(mmd);
			(ServiceProvider as BasicServiceProvider).Register<IMemoryDomains>(MemoryDomains);
		}

		private IMemoryDomains MemoryDomains;

		#endregion

		#region ISoundProvider

		//private short[] sbuff = new short[1454 * 2]; //this is the most ive ever seen.. dont know why. two frames worth i guess
		private short[] sbuff = new short[1611 * 2]; //need this for pal
		private int sbuffcontains = 0;

		public ISoundProvider SoundProvider { get { throw new InvalidOperationException(); } }
		public ISyncSoundProvider SyncSoundProvider { get { return this; } }
		public bool StartAsyncSound() { return false; }
		public void EndAsyncSound() { }

		public void GetSamples(out short[] samples, out int nsamp)
		{
			samples = sbuff;
			nsamp = sbuffcontains;
		}

		public void DiscardSamples()
		{
			sbuffcontains = 0;
		}

		#endregion

		#region ISaveRam

		public byte[] CloneSaveRam()
		{
			var buf = new byte[128 * 1024];
			fixed (byte* pbuf = buf)
			{
				var transaction = new OctoshockDll.ShockMemcardTransaction();
				transaction.buffer128k = pbuf;
				transaction.transaction = OctoshockDll.eShockMemcardTransaction.Read;
				OctoshockDll.shock_Peripheral_MemcardTransact(psx, 0x01, ref transaction);
			}
			return buf;
		}

		public void StoreSaveRam(byte[] data)
		{
			fixed (byte* pbuf = data)
			{
				var transaction = new OctoshockDll.ShockMemcardTransaction();
				transaction.buffer128k = pbuf;
				transaction.transaction = OctoshockDll.eShockMemcardTransaction.Write;
				OctoshockDll.shock_Peripheral_MemcardTransact(psx, 0x01, ref transaction);
			}
		}

		public bool SaveRamModified
		{
			get
			{
				var transaction = new OctoshockDll.ShockMemcardTransaction();
				transaction.transaction = OctoshockDll.eShockMemcardTransaction.CheckDirty;
				return OctoshockDll.shock_Peripheral_MemcardTransact(psx, 0x01, ref transaction) == OctoshockDll.SHOCK_TRUE;
			}
		}

		#endregion //ISaveRam


		#region Savestates
		//THIS IS STILL AWFUL

		JsonSerializer ser = new JsonSerializer() { Formatting = Formatting.Indented };

		class TextStateData
		{
			public int Frame;
			public int LagCount;
			public bool IsLagFrame;
			public bool CurrentDiscEjected;
			public int CurrentDiscIndexMounted;
		}

		public void SaveStateText(TextWriter writer)
		{
			var s = new TextState<TextStateData>();
			s.Prepare();

			var transaction = new OctoshockDll.ShockStateTransaction()
			{
				transaction = OctoshockDll.eShockStateTransaction.TextSave,
				ff = s.GetFunctionPointersSave()
			};
			int result = OctoshockDll.shock_StateTransaction(psx, ref transaction);
			if (result != OctoshockDll.SHOCK_OK)
				throw new InvalidOperationException("eShockStateTransaction.TextSave returned error!");

			s.ExtraData.IsLagFrame = IsLagFrame;
			s.ExtraData.LagCount = LagCount;
			s.ExtraData.Frame = Frame;
			s.ExtraData.CurrentDiscEjected = CurrentTrayOpen;
			s.ExtraData.CurrentDiscIndexMounted = CurrentDiscIndexMounted;

			ser.Serialize(writer, s);
			// TODO write extra copy of stuff we don't use (WHY?)
		}

		public void LoadStateText(TextReader reader)
		{
			var s = (TextState<TextStateData>)ser.Deserialize(reader, typeof(TextState<TextStateData>));
			s.Prepare();
			var transaction = new OctoshockDll.ShockStateTransaction()
			{
				transaction = OctoshockDll.eShockStateTransaction.TextLoad,
				ff = s.GetFunctionPointersLoad()
			};

			int result = OctoshockDll.shock_StateTransaction(psx, ref transaction);
			if (result != OctoshockDll.SHOCK_OK)
				throw new InvalidOperationException("eShockStateTransaction.TextLoad returned error!");

			IsLagFrame = s.ExtraData.IsLagFrame;
			LagCount = s.ExtraData.LagCount;
			Frame = s.ExtraData.Frame;
			CurrentTrayOpen = s.ExtraData.CurrentDiscEjected;
			CurrentDiscIndexMounted = s.ExtraData.CurrentDiscIndexMounted;
			PokeDisc();
		}

		byte[] savebuff;
		byte[] savebuff2;

		void StudySaveBufferSize()
		{
			var transaction = new OctoshockDll.ShockStateTransaction();
			transaction.transaction = OctoshockDll.eShockStateTransaction.BinarySize;
			int size = OctoshockDll.shock_StateTransaction(psx, ref transaction);
			savebuff = new byte[size];
			savebuff2 = new byte[savebuff.Length + 4+  4+4+1+1+4];
		}

		public void SaveStateBinary(BinaryWriter writer)
		{
			fixed (byte* psavebuff = savebuff)
			{
				var transaction = new OctoshockDll.ShockStateTransaction()
				{
					transaction = OctoshockDll.eShockStateTransaction.BinarySave,
					buffer = psavebuff,
					bufferLength = savebuff.Length
				};

				int result = OctoshockDll.shock_StateTransaction(psx, ref transaction);
				if (result != OctoshockDll.SHOCK_OK)
					throw new InvalidOperationException("eShockStateTransaction.BinarySave returned error!");
				writer.Write(savebuff.Length);
				writer.Write(savebuff);

				// other variables
				writer.Write(IsLagFrame);
				writer.Write(LagCount);
				writer.Write(Frame);
				writer.Write(CurrentTrayOpen);
				writer.Write(CurrentDiscIndexMounted);
			}
		}

		public void LoadStateBinary(BinaryReader reader)
		{
			fixed (byte* psavebuff = savebuff)
			{
				var transaction = new OctoshockDll.ShockStateTransaction()
				{
					transaction = OctoshockDll.eShockStateTransaction.BinaryLoad,
					buffer = psavebuff,
					bufferLength = savebuff.Length
				};

				int length = reader.ReadInt32();
				if (length != savebuff.Length)
					throw new InvalidOperationException("Save buffer size mismatch!");
				reader.Read(savebuff, 0, length);
				int ret = OctoshockDll.shock_StateTransaction(psx, ref transaction);
				if (ret != OctoshockDll.SHOCK_OK)
					throw new InvalidOperationException("eShockStateTransaction.BinaryLoad returned error!");

				// other variables
				IsLagFrame = reader.ReadBoolean();
				LagCount = reader.ReadInt32();
				Frame = reader.ReadInt32();
				CurrentTrayOpen = reader.ReadBoolean();
				CurrentDiscIndexMounted = reader.ReadInt32();
				PokeDisc();
			}
		}

		public byte[] SaveStateBinary()
		{
			//this are objectionable shenanigans, but theyre required to get the extra info in the stream. we need a better approach.
			var ms = new MemoryStream(savebuff2, true);
			var bw = new BinaryWriter(ms);
			SaveStateBinary(bw);
			bw.Flush();
			if (ms.Position != savebuff2.Length)
				throw new InvalidOperationException();
			ms.Close();
			return savebuff2;
		}

		public bool BinarySaveStatesPreferred
		{
			get { return true; }
		}

		#endregion

		#region Settings

		Settings _Settings = new Settings();
		SyncSettings _SyncSettings;

		public enum eResolutionMode
		{
			PixelPro, Debug,
			Mednafen, TweakedMednafen
		}

		public class SyncSettings
		{
			public SyncSettings Clone()
			{
				var ret = (SyncSettings)MemberwiseClone();
				ret.Controllers = Controllers.Select(x => x.Clone()).ToArray();

				return ret;
			}

			public ControllerSetting[] Controllers = 
			{
				new ControllerSetting
				{
					IsConnected = true,
					Type = ControllerSetting.ControllerType.DualShock
				},
				new ControllerSetting
				{
					IsConnected = false,
					Type = ControllerSetting.ControllerType.DualShock
				}
			};
		}

		public class ControllerSetting
		{
			public ControllerSetting Clone()
			{
				return (ControllerSetting)this.MemberwiseClone();
			}

			public bool IsConnected { get; set; }
			public ControllerType Type { get; set; }

			public enum ControllerType
			{
				Gamepad,

				[Description("Dual Analog")]
				DualAnalog,

				[Description("Dual Shock")]
				DualShock
			}
		}

		public class Settings
		{
			[DisplayName("Resolution Mode")]
			[Description("Stuff")]
			[DefaultValue(eResolutionMode.PixelPro)]
			public eResolutionMode ResolutionMode { get; set; }

			[DisplayName("ScanlineStart_NTSC")]
			[DefaultValue(0)]
			public int ScanlineStart_NTSC { get; set; }

			[DisplayName("ScanlineEnd_NTSC")]
			[DefaultValue(239)]
			public int ScanlineEnd_NTSC { get; set; }

			[DisplayName("ScanlineStart_PAL")]
			[DefaultValue(0)]
			public int ScanlineStart_PAL { get; set; }

			[DisplayName("ScanlineEnd_PAL")]
			[DefaultValue(287)]
			public int ScanlineEnd_PAL { get; set; }

			[DisplayName("Clip Horizontal Overscan")]
			[DefaultValue(false)]
			public bool ClipHorizontalOverscan { get; set; }

			public void Validate()
			{
				if (ScanlineStart_NTSC < 0) ScanlineStart_NTSC = 0;
				if (ScanlineStart_PAL < 0) ScanlineStart_PAL = 0;
				if (ScanlineEnd_NTSC > 239) ScanlineEnd_NTSC = 239;
				if (ScanlineEnd_PAL > 287) ScanlineEnd_PAL = 287;
				
				//make sure theyre not in the wrong order
				if (ScanlineEnd_NTSC < ScanlineStart_NTSC)
				{
					int temp = ScanlineEnd_NTSC;
					ScanlineEnd_NTSC = ScanlineStart_NTSC;
					ScanlineStart_NTSC = temp;
				}
				if (ScanlineEnd_PAL < ScanlineStart_PAL)
				{
					int temp = ScanlineEnd_PAL;
					ScanlineEnd_PAL = ScanlineStart_PAL;
					ScanlineStart_PAL = temp;
				}
			}

			public Settings()
			{
				SettingsUtil.SetDefaultValues(this);
			}

			public Settings Clone()
			{
				return (Settings)MemberwiseClone();
			}
		}

		public Settings GetSettings()
		{
			return _Settings.Clone();
		}

		public SyncSettings GetSyncSettings()
		{
			return _SyncSettings.Clone();
		}

		public bool PutSettings(Settings o)
		{
			_Settings.Validate();
			_Settings = o;
			//TODO
			//var native = _Settings.GetNativeSettings();
			//BizSwan.bizswan_putsettings(Core, ref native);
			return false;
		}

		public bool PutSyncSettings(SyncSettings o)
		{
			var ret = !DeepEquality.DeepEquals(_SyncSettings, o);
			_SyncSettings = o;
			return ret;
		}

		#endregion

		#region IDebuggable

		// TODO: don't cast to int, and are any of these not 32 bit?
		public IDictionary<string, RegisterValue> GetCpuFlagsAndRegisters()
		{
			Dictionary<string, RegisterValue> ret = new Dictionary<string, RegisterValue>();
			var regs = new OctoshockDll.ShockRegisters_CPU();

			OctoshockDll.shock_GetRegisters_CPU(psx, ref regs);

			ret["r1"] = (int)regs.GPR[1]; ret["r2"] = (int)regs.GPR[2]; ret["r3"] = (int)regs.GPR[3];
			ret["r4"] = (int)regs.GPR[4]; ret["r5"] = (int)regs.GPR[5]; ret["r6"] = (int)regs.GPR[6]; ret["r7"] = (int)regs.GPR[7];
			ret["r8"] = (int)regs.GPR[8]; ret["r9"] = (int)regs.GPR[9]; ret["r10"] = (int)regs.GPR[10]; ret["r11"] = (int)regs.GPR[11];
			ret["r12"] = (int)regs.GPR[12]; ret["r13"] = (int)regs.GPR[13]; ret["r14"] = (int)regs.GPR[14]; ret["r15"] = (int)regs.GPR[15];
			ret["r16"] = (int)regs.GPR[16]; ret["r17"] = (int)regs.GPR[17]; ret["r18"] = (int)regs.GPR[18]; ret["r19"] = (int)regs.GPR[19];
			ret["r20"] = (int)regs.GPR[20]; ret["r21"] = (int)regs.GPR[21]; ret["r22"] = (int)regs.GPR[22]; ret["r23"] = (int)regs.GPR[23];
			ret["r24"] = (int)regs.GPR[24]; ret["r25"] = (int)regs.GPR[25]; ret["r26"] = (int)regs.GPR[26]; ret["r27"] = (int)regs.GPR[27];
			ret["r28"] = (int)regs.GPR[28]; ret["r29"] = (int)regs.GPR[29]; ret["r30"] = (int)regs.GPR[30]; ret["r31"] = (int)regs.GPR[31];

			ret["at"] = (int)regs.GPR[1];
			ret["v0"] = (int)regs.GPR[2]; ret["v1"] = (int)regs.GPR[3];
			ret["a0"] = (int)regs.GPR[4]; ret["a1"] = (int)regs.GPR[5]; ret["a2"] = (int)regs.GPR[6]; ret["a3"] = (int)regs.GPR[7];
			ret["t0"] = (int)regs.GPR[8]; ret["t1"] = (int)regs.GPR[9]; ret["t2"] = (int)regs.GPR[10]; ret["t3"] = (int)regs.GPR[11];
			ret["t4"] = (int)regs.GPR[12]; ret["t5"] = (int)regs.GPR[13]; ret["t6"] = (int)regs.GPR[14]; ret["t7"] = (int)regs.GPR[15];
			ret["s0"] = (int)regs.GPR[16]; ret["s1"] = (int)regs.GPR[17]; ret["s2"] = (int)regs.GPR[18]; ret["s3"] = (int)regs.GPR[19];
			ret["s4"] = (int)regs.GPR[20]; ret["s5"] = (int)regs.GPR[21]; ret["s6"] = (int)regs.GPR[22]; ret["s7"] = (int)regs.GPR[23];
			ret["t8"] = (int)regs.GPR[24]; ret["t9"] = (int)regs.GPR[25];
			ret["k0"] = (int)regs.GPR[26]; ret["k1"] = (int)regs.GPR[27];
			ret["gp"] = (int)regs.GPR[28];
			ret["sp"] = (int)regs.GPR[29];
			ret["fp"] = (int)regs.GPR[30];
			ret["ra"] = (int)regs.GPR[31];

			ret["pc"] = (int)regs.PC;
			ret["lo"] = (int)regs.LO;
			ret["hi"] = (int)regs.HI;
			ret["sr"] = (int)regs.SR;
			ret["cause"] = (int)regs.CAUSE;
			ret["epc"] = (int)regs.EPC;

			return ret;
		}

		static Dictionary<string, int> CpuRegisterIndices = new Dictionary<string, int>() {
			{"r1",1},{"r2",2},{"r3",3},{"r4",4},{"r5",5},{"r6",6},{"r7",7},
			{"r8",8},{"r9",9},{"r10",10},{"r11",11},{"r12",12},{"r13",13},{"r14",14},{"r15",15},
			{"r16",16},{"r17",17},{"r18",18},{"r19",19},{"r20",20},{"r21",21},{"r22",22},{"r23",23},
			{"r24",24},{"r25",25},{"r26",26},{"r27",27},{"r28",28},{"r29",29},{"r30",30},{"r31",31},
			{"at",1},{"v0",2},{"v1",3},
			{"a0",4},{"a1",5},{"a2",6},{"a3",7},
			{"t0",8},{"t1",9},{"t2",10},{"t3",11},
			{"t4",12},{"t5",13},{"t6",14},{"t7",15},
			{"s0",16},{"s1",17},{"s2",18},{"s3",19},
			{"s4",20},{"s5",21},{"s6",22},{"s7",23},
			{"t8",24},{"t9",25},
			{"k0",26},{"k1",27},
			{"gp",28},{"sp",29},{"fp",30},{"ra",31},
			{"pc",32},
			//33 - PC_NEXT
			//34 - IN_BD_SLOT
			{"lo",35},
			{"hi",36},
			{"sr",37},
			{"cause",38},
			{"epc",39},
		};

		public void SetCpuRegister(string register, int value)
		{
			int index = CpuRegisterIndices[register];
			OctoshockDll.shock_SetRegister_CPU(psx, index, (uint)value);
		}

		public ITraceable Tracer { get { return tracer; } }

		public int ShockTraceCallback(IntPtr opaque, uint PC, uint inst, string dis)
		{
			Tracer.Put(dis);
			return OctoshockDll.SHOCK_OK;
		}

		[FeatureNotImplemented]
		public IMemoryCallbackSystem MemoryCallbacks { get { throw new NotImplementedException(); } }

		public bool CanStep(StepType type) { return false; }

		[FeatureNotImplemented]
		public void Step(StepType type) { throw new NotImplementedException(); }

		#endregion //IDebuggable
	}
}
