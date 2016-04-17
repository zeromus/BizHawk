﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BizHawk.Emulation.Common
{
	public class MemoryDomain
	{
		public enum Endian { Big, Little, Unknown }

		public MemoryDomain(string name, long size, Endian endian, Func<long, byte> peekByte, Action<long, byte> pokeByte, int byteSize = 1)
		{
			Name = name;
			Size = size;
			ByteSize = byteSize;
			EndianType = endian;
			PeekByte = peekByte;
			PokeByte = pokeByte;
		}

		/// <summary>
		/// Cheats engines go on MemoryDomains instead of on the core because a generic 'freeze' type cheat inherently has a MemoryDomain argument.
		/// We might wish to use it, if it's available, for general purpose 'freezing' an address, instead of pulsing.
		/// </summary>
		public CheatEngine[] CheatInterfaces { get; set; }

		public string Name { get; private set; }

		public long Size { get; private set; }

		public int ByteSize { get; private set; }

		public Endian EndianType { get; private set; }

		public Func<long, byte> PeekByte { get; private set; }

		public Action<long, byte> PokeByte { get; private set; }

		public void SetPeekPokeDelegates(Func<long, byte> peeker, Action<long, byte> poker)
		{
			PeekByte = peeker;
			PokeByte = poker;
		}

		/// <summary>
		/// creates a memorydomain that references a managed byte array
		/// </summary>
		/// <param name="name"></param>
		/// <param name="endian"></param>
		/// <param name="data"></param>
		/// <param name="writable">if false, writes will be ignored</param>
		/// <returns></returns>
		public static MemoryDomain FromByteArray(string name, Endian endian, byte[] data, bool writable = true, int byteSize = 1)
		{
			if (data == null)
				throw new ArgumentNullException("data");
			return new MemoryDomain
			(
				name,
				data.Length,
				endian,
				delegate(long addr)
				{
					return data[addr];
				},
				writable ?
				delegate(long addr, byte val)
				{
					data[addr] = val;
				}
				: (Action<long, byte>)null,
				byteSize
			);
		}

		public void SetDelegatesForIntPtr(long size, Endian endian, IntPtr data, bool writable = true, int byteSize = 1)
		{
			Func<long, byte> peeker;
			Action<long, byte> poker;
			CreateDelegatesForIntPtr(size, endian, data, out peeker, out poker, writable, byteSize);
			PeekByte = peeker;
			PokeByte = poker;
		}

		public unsafe static void CreateDelegatesForIntPtr(long size, Endian endian, IntPtr data, out Func<long, byte> peeker, out Action<long, byte> poker, bool writable = true, int byteSize = 1)
		{
			byte* p = (byte*)data;
			uint l = (uint)size;

			peeker = delegate(long addr)
				{
					if ((uint)addr >= l)
						throw new ArgumentOutOfRangeException();
					return p[addr];
				};

			poker =
				writable ?
				delegate(long addr, byte val)
				{
					if ((uint)addr >= l)
						throw new ArgumentOutOfRangeException();
					p[addr] = val;
				}
				: (Action<long, byte>)null;
		}

		/// <summary>
		/// create a memorydomain that references an unmanaged memory block
		/// </summary>
		/// <param name="data">must remain valid as long as the MemoryDomain exists!</param>
		/// <param name="writable">if false, writes will be ignored</param>
		/// <returns></returns>
		public unsafe static MemoryDomain FromIntPtr(string name, long size, Endian endian, IntPtr data, bool writable = true, int byteSize = 1)
		{
			if (data == IntPtr.Zero)
				throw new ArgumentNullException("data");
			if ((ulong)size >= 0x80000000)
				throw new ArgumentOutOfRangeException("size");

			var md = new MemoryDomain(name,size,endian, null, null, byteSize);
			md.SetDelegatesForIntPtr(size, endian, data, writable, byteSize);
			return md;
		}

		/// <summary>
		/// create a memorydomain that references an unmanaged memory block with 16 bit swaps
		/// </summary>
		/// <param name="data">must remain valid as long as the MemoryDomain exists!</param>
		/// <param name="writable">if false, writes will be ignored</param>
		/// <returns></returns>
		public unsafe static MemoryDomain FromIntPtrSwap16(string name, long size, Endian endian, IntPtr data, bool writable = true, int byteSize = 1)
		{
			if (data == IntPtr.Zero)
				throw new ArgumentNullException("data");
			if ((ulong)size >= 0x80000000)
				throw new ArgumentOutOfRangeException("size");
			byte* p = (byte*)data;
			uint l = (uint)size;
			return new MemoryDomain
			(
				name,
				size,
				endian,
				delegate(long addr)
				{
					if ((uint)addr >= l)
						throw new ArgumentOutOfRangeException();
					return p[addr ^ 1];
				},
				writable ?
				delegate(long addr, byte val)
				{
					if ((uint)addr >= l)
						throw new ArgumentOutOfRangeException();
					p[addr ^ 1] = val;
				}
				: (Action<long, byte>)null,
				byteSize
			);
		}

		public override string ToString()
		{
			return Name;
		}

		public ushort PeekWord(long addr, bool bigEndian)
		{
			Endian endian = bigEndian ? Endian.Big : Endian.Little;
			switch (endian)
			{
				default:
				case Endian.Big:
					return (ushort)((PeekByte(addr) << 8) | (PeekByte(addr + 1)));
				case Endian.Little:
					return (ushort)((PeekByte(addr)) | (PeekByte(addr + 1) << 8));
			}
		}

		public uint PeekDWord(long addr, bool bigEndian)
		{
			Endian endian = bigEndian ? Endian.Big : Endian.Little;
			switch (endian)
			{
				default:
				case Endian.Big:
					return (uint)((PeekByte(addr) << 24)
					| (PeekByte(addr + 1) << 16)
					| (PeekByte(addr + 2) << 8)
					| (PeekByte(addr + 3) << 0));
				case Endian.Little:
					return (uint)((PeekByte(addr) << 0)
					| (PeekByte(addr + 1) << 8)
					| (PeekByte(addr + 2) << 16)
					| (PeekByte(addr + 3) << 24));
			}
		}

		public void PokeWord(long addr, ushort val, bool bigEndian)
		{
			Endian endian = bigEndian ? Endian.Big : Endian.Little;
			switch (endian)
			{
				default:
				case Endian.Big:
					PokeByte(addr + 0, (byte)(val >> 8));
					PokeByte(addr + 1, (byte)(val));
					break;
				case Endian.Little:
					PokeByte(addr + 0, (byte)(val));
					PokeByte(addr + 1, (byte)(val >> 8));
					break;
			}
		}

		public void PokeDWord(long addr, uint val, bool bigEndian)
		{
			Endian endian = bigEndian ? Endian.Big : Endian.Little;
			switch (endian)
			{
				default:
				case Endian.Big:
					PokeByte(addr + 0, (byte)(val >> 24));
					PokeByte(addr + 1, (byte)(val >> 16));
					PokeByte(addr + 2, (byte)(val >> 8));
					PokeByte(addr + 3, (byte)(val));
					break;
				case Endian.Little:
					PokeByte(addr + 0, (byte)(val));
					PokeByte(addr + 1, (byte)(val >> 8));
					PokeByte(addr + 2, (byte)(val >> 16));
					PokeByte(addr + 3, (byte)(val >> 24));
					break;
			}
		}
	}

	public class MemoryDomainList : ReadOnlyCollection<MemoryDomain>, IMemoryDomains
	{
		private MemoryDomain _mainMemory;
		private MemoryDomain _systemBus;

		public bool Has(string name)
		{
			return this.FirstOrDefault((md) => md.Name == name) != null;
		}

		public MemoryDomainList(IList<MemoryDomain> domains) 
			: base(domains)
		{
		}

		public MemoryDomain this[string name]
		{
			get
			{
				return this.FirstOrDefault(x => x.Name == name);
			}
		}

		public MemoryDomain MainMemory
		{
			get
			{
				if (_mainMemory != null)
				{
					return _mainMemory;
				}

				return this.First();
			}

			set
			{
				_mainMemory = value;
			}
		}

		public bool HasSystemBus
		{
			get
			{
				if (_systemBus != null)
				{
					return true;
				}

				return this.Any(x => x.Name == "System Bus");
			}
		}

		public MemoryDomain SystemBus
		{
			get
			{
				if (_systemBus != null)
				{
					return _systemBus;
				}

				var bus = this.FirstOrDefault(x => x.Name == "System Bus");

				if (bus != null)
				{
					return bus;
				}

				return MainMemory;
			}

			set
			{
				_systemBus = value;
			}
		}
	}
}
