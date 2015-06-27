﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace BizHawk.Emulation.Cores.Consoles.Sega.gpgx
{
	public class LibGPGXDynamic
	{
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_get_video_t(ref int w, ref int h, ref int pitch, ref IntPtr buffer);
		public gpgx_get_video_t gpgx_get_video;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_get_audio_t(ref int n, ref IntPtr buffer);
		public gpgx_get_audio_t gpgx_get_audio;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int load_archive_cb(string filename, IntPtr buffer, int maxsize);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_advance_t();
		public gpgx_advance_t gpgx_advance;

		public enum Region : int
		{
			Autodetect = 0,
			USA = 1,
			Europe = 2,
			Japan_NTSC = 3,
			Japan_PAL = 4
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate bool gpgx_init_t(string feromextension, load_archive_cb feload_archive_cb, bool sixbutton, INPUT_SYSTEM system_a, INPUT_SYSTEM system_b, Region region);
		public gpgx_init_t gpgx_init;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_get_fps_t(ref int num, ref int den);
		public gpgx_get_fps_t gpgx_get_fps;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int gpgx_state_max_size_t();
		public gpgx_state_max_size_t gpgx_state_max_size;
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int gpgx_state_size_t(byte[] dest, int size);
		public gpgx_state_size_t gpgx_state_size;
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate bool gpgx_state_save_t(byte[] dest, int size);
		public gpgx_state_save_t gpgx_state_save;
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate bool gpgx_state_load_t(byte[] src, int size);
		public gpgx_state_load_t gpgx_state_load;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate bool gpgx_get_control_t([Out]InputData dest, int bytes);
		public gpgx_get_control_t gpgx_get_control;
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate bool gpgx_put_control_t([In]InputData src, int bytes);
		public gpgx_put_control_t gpgx_put_control;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_get_sram_t(ref IntPtr area, ref int size);
		public gpgx_get_sram_t gpgx_get_sram;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_clear_sram_t();
		public gpgx_clear_sram_t gpgx_clear_sram;

		public const int MIN_MEM_DOMAIN = 0;
		public const int MAX_MEM_DOMAIN = 13;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		// apparently, if you use built in string marshalling, the interop will assume that
		// the unmanaged char pointer was allocated in hglobal and try to free it that way
		public delegate IntPtr gpgx_get_memdom_t(int which, ref IntPtr area, ref int size);
		public gpgx_get_memdom_t gpgx_get_memdom;

		// call this before reading sram returned by gpgx_get_sram()
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_sram_prepread_t();
		public gpgx_sram_prepread_t gpgx_sram_prepread;

		// call this after writing sram returned by gpgx_get_sram()
		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_sram_commitwrite_t();
		public gpgx_sram_commitwrite_t gpgx_sram_commitwrite;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_reset_t(bool hard);
		public gpgx_reset_t gpgx_reset;

		public const int MAX_DEVICES = 8;

		public enum INPUT_SYSTEM : byte
		{
			SYSTEM_NONE = 0,		// unconnected port	
			SYSTEM_MD_GAMEPAD = 1,	// single 3-buttons or 6-buttons Control Pad 	
			SYSTEM_MOUSE = 2,		// Sega Mouse 	
			SYSTEM_MENACER = 3,		// Sega Menacer -- port B only
			SYSTEM_JUSTIFIER = 4,	// Konami Justifiers -- port B only
			SYSTEM_XE_A1P = 5,		// XE-A1P analog controller -- port A only
			SYSTEM_ACTIVATOR = 6,	// Sega Activator 	
			SYSTEM_MS_GAMEPAD = 7,	// single 2-buttons Control Pad -- Master System
			SYSTEM_LIGHTPHASER = 8,	// Sega Light Phaser -- Master System
			SYSTEM_PADDLE = 9,		// Sega Paddle Control -- Master System
			SYSTEM_SPORTSPAD = 10,	// Sega Sports Pad -- Master System
			SYSTEM_TEAMPLAYER = 11,	// Multi Tap -- Sega TeamPlayer 	
			SYSTEM_WAYPLAY = 12,	// Multi Tap -- EA 4-Way Play -- use both ports
		};

		public enum INPUT_DEVICE : byte
		{
			DEVICE_NONE = 0xff,		// unconnected device = fixed ID for Team Player)
			DEVICE_PAD3B = 0x00,	// 3-buttons Control Pad = fixed ID for Team Player)
			DEVICE_PAD6B = 0x01,	// 6-buttons Control Pad = fixed ID for Team Player)
			DEVICE_PAD2B = 0x02,	// 2-buttons Control Pad
			DEVICE_MOUSE = 0x03,	// Sega Mouse
			DEVICE_LIGHTGUN = 0x04, // Sega Light Phaser, Menacer or Konami Justifiers
			DEVICE_PADDLE = 0x05,	// Sega Paddle Control
			DEVICE_SPORTSPAD = 0x06,// Sega Sports Pad
			DEVICE_PICO = 0x07,		// PICO tablet
			DEVICE_TEREBI = 0x08,	// Terebi Oekaki tablet
			DEVICE_XE_A1P = 0x09,	// XE-A1P analog controller
			DEVICE_ACTIVATOR = 0x0a,// Activator
		};

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void input_cb();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_set_input_callback_t(input_cb cb);
		public gpgx_set_input_callback_t gpgx_set_input_callback;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void mem_cb(uint addr);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_set_mem_callback_t(mem_cb read, mem_cb write, mem_cb exec);
		public gpgx_set_mem_callback_t gpgx_set_mem_callback;

		/// <summary>
		/// not every flag is valid for every device!
		/// </summary>
		[Flags]
		public enum INPUT_KEYS : ushort
		{
			/* Default Input bitmasks */
			INPUT_MODE = 0x0800,
			INPUT_X = 0x0400,
			INPUT_Y = 0x0200,
			INPUT_Z = 0x0100,
			INPUT_START = 0x0080,
			INPUT_A = 0x0040,
			INPUT_C = 0x0020,
			INPUT_B = 0x0010,
			INPUT_RIGHT = 0x0008,
			INPUT_LEFT = 0x0004,
			INPUT_DOWN = 0x0002,
			INPUT_UP = 0x0001,

			/* Master System specific bitmasks */
			INPUT_BUTTON2 = 0x0020,
			INPUT_BUTTON1 = 0x0010,

			/* Mega Mouse specific bitmask */
			INPUT_MOUSE_START = 0x0080,
			INPUT_MOUSE_CENTER = 0x0040,
			INPUT_MOUSE_RIGHT = 0x0020,
			INPUT_MOUSE_LEFT = 0x0010,

			/* Pico hardware specific bitmask */
			INPUT_PICO_PEN = 0x0080,
			INPUT_PICO_RED = 0x0010,

			/* XE-1AP specific bitmask */
			INPUT_XE_E1 = 0x0800,
			INPUT_XE_E2 = 0x0400,
			INPUT_XE_START = 0x0200,
			INPUT_XE_SELECT = 0x0100,
			INPUT_XE_A = 0x0080,
			INPUT_XE_B = 0x0040,
			INPUT_XE_C = 0x0020,
			INPUT_XE_D = 0x0010,

			/* Activator specific bitmasks */
			INPUT_ACTIVATOR_8U = 0x8000,
			INPUT_ACTIVATOR_8L = 0x4000,
			INPUT_ACTIVATOR_7U = 0x2000,
			INPUT_ACTIVATOR_7L = 0x1000,
			INPUT_ACTIVATOR_6U = 0x0800,
			INPUT_ACTIVATOR_6L = 0x0400,
			INPUT_ACTIVATOR_5U = 0x0200,
			INPUT_ACTIVATOR_5L = 0x0100,
			INPUT_ACTIVATOR_4U = 0x0080,
			INPUT_ACTIVATOR_4L = 0x0040,
			INPUT_ACTIVATOR_3U = 0x0020,
			INPUT_ACTIVATOR_3L = 0x0010,
			INPUT_ACTIVATOR_2U = 0x0008,
			INPUT_ACTIVATOR_2L = 0x0004,
			INPUT_ACTIVATOR_1U = 0x0002,
			INPUT_ACTIVATOR_1L = 0x0001,

			/* Menacer */
			INPUT_MENACER_TRIGGER = 0x0040,
			INPUT_MENACER_START = 0x0080,
		};

		[StructLayout(LayoutKind.Sequential)]
		public class InputData
		{
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
			public readonly INPUT_SYSTEM[] system = new INPUT_SYSTEM[2];
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEVICES)]
			public readonly INPUT_DEVICE[] dev = new INPUT_DEVICE[MAX_DEVICES];
			/// <summary>
			/// digital inputs
			/// </summary>
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEVICES)]
			public readonly INPUT_KEYS[] pad = new INPUT_KEYS[MAX_DEVICES];
			/// <summary>
			/// analog (x/y)
			/// </summary>
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_DEVICES * 2)]
			public readonly short[] analog = new short[MAX_DEVICES * 2];
			/// <summary>
			/// gun horizontal offset
			/// </summary>
			public int x_offset;
			/// <summary>
			/// gun vertical offset
			/// </summary>
			public int y_offset;

			public void ClearAllBools()
			{
				for (int i = 0; i < pad.Length; i++)
					pad[i] = 0;
			}
		}

		public const int CD_MAX_TRACKS = 100;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void cd_read_cb(int lba, IntPtr dest, bool audio);

		[StructLayout(LayoutKind.Sequential)]
		public struct CDTrack
		{
			public int start;
			public int end;
		}

		[StructLayout(LayoutKind.Sequential)]
		public class CDData
		{
			public int end;
			public int last;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = CD_MAX_TRACKS)]
			public readonly CDTrack[] tracks = new CDTrack[CD_MAX_TRACKS];
			public cd_read_cb readcallback;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct VDPNameTable
		{
			public int Width; // in cells
			public int Height; // in cells
			public int Baseaddr;
		}

		[StructLayout(LayoutKind.Sequential)]
		public class VDPView
		{
			public IntPtr VRAM;
			public IntPtr PatternCache;
			public IntPtr ColorCache;
			public VDPNameTable NTA;
			public VDPNameTable NTB;
			public VDPNameTable NTW;
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_get_vdp_view_t([Out] VDPView view);
		public gpgx_get_vdp_view_t gpgx_get_vdp_view;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_poke_vram_t(int addr, byte value);
		public gpgx_poke_vram_t gpgx_poke_vram;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_flush_vram_t();
		public gpgx_flush_vram_t gpgx_flush_vram;

		[StructLayout(LayoutKind.Sequential)]
		public struct RegisterInfo
		{
			public int Value;
			public IntPtr Name;
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int gpgx_getmaxnumregs_t();
		public gpgx_getmaxnumregs_t gpgx_getmaxnumregs;

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate int gpgx_getregs_t([Out] RegisterInfo[] regs);
		public gpgx_getregs_t gpgx_getregs;

		[Flags]
		public enum DrawMask : int
		{
			BGA = 1,
			BGB = 2,
			BGW = 4
		}

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void gpgx_set_draw_mask_t(DrawMask mask);
		public gpgx_set_draw_mask_t gpgx_set_draw_mask;
	}
}
