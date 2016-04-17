using System;
using System.Collections.Generic;
using System.Linq;

using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Nintendo.Gameboy
{
	public partial class Gameboy
	{
		private List<MemoryDomain> _memoryDomains = new List<MemoryDomain>();
		internal IMemoryDomains MemoryDomains { get; set; }

		GameGenieCheatEngine GGEngine;

		class GameGenieCheatEngine : CheatEngine
		{
			GameGenieCheatEngine(Gameboy core) { this.Core = core; }

			public Gameboy Core;
			public override string Name { get { return "Game Genie"; } }

			protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
			{
				var ggcheats = new List<string>();
				foreach (Token cheat in this)
				{
					ggcheats.Add(cheat.Code);
				}
				var ggstring = string.Join(";", ggcheats);
				LibGambatte.gambatte_setgg(Core.GambatteState, ggstring);
			}

			public override ICheatToken Create(string str)
			{
				return new Token() { Code = str };
			}

			public class Token : ICheatToken
			{
				public string Code;
			}
		}

		void CreateMemoryDomain(LibGambatte.MemoryAreas which, string name)
		{
			IntPtr data = IntPtr.Zero;
			int length = 0;

			if (!LibGambatte.gambatte_getmemoryarea(GambatteState, which, ref data, ref length))
				throw new Exception("gambatte_getmemoryarea() failed!");

			// if length == 0, it's an empty block; (usually rambank on some carts); that's ok
			if (data != IntPtr.Zero && length > 0)
				_memoryDomains.Add(MemoryDomain.FromIntPtr(name, length, MemoryDomain.Endian.Little, data));
		}

		private void InitMemoryDomains()
		{
			CreateMemoryDomain(LibGambatte.MemoryAreas.wram, "WRAM");
			CreateMemoryDomain(LibGambatte.MemoryAreas.rom, "ROM");
			CreateMemoryDomain(LibGambatte.MemoryAreas.vram, "VRAM");
			CreateMemoryDomain(LibGambatte.MemoryAreas.cartram, "CartRAM");
			CreateMemoryDomain(LibGambatte.MemoryAreas.oam, "OAM");
			CreateMemoryDomain(LibGambatte.MemoryAreas.hram, "HRAM");

			// also add a special memory domain for the system bus, where calls get sent directly to the core each time
			var mdSysbus = new MemoryDomain("System Bus", 65536, MemoryDomain.Endian.Little,
				delegate(long addr)
				{
					if (addr < 0 || addr >= 65536)
						throw new ArgumentOutOfRangeException();
					return LibGambatte.gambatte_cpuread(GambatteState, (ushort)addr);
				},
				delegate(long addr, byte val)
				{
					if (addr < 0 || addr >= 65536)
						throw new ArgumentOutOfRangeException();
					LibGambatte.gambatte_cpuwrite(GambatteState, (ushort)addr, val);
				});
			_memoryDomains.Add(mdSysbus);

			mdSysbus.CheatInterfaces = new [] { GGEngine };

			MemoryDomains = new MemoryDomainList(_memoryDomains);
			(ServiceProvider as BasicServiceProvider).Register<IMemoryDomains>(MemoryDomains);
		}
	}
}
