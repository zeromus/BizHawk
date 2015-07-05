﻿using System;
using System.Collections.Generic;

using BizHawk.Common.BufferExtensions;

//some old junk

namespace BizHawk.Emulation.DiscSystem
{
	[Serializable]
	public class DiscReferenceException : Exception
	{
		public DiscReferenceException(string fname, Exception inner)
			: base(string.Format("A disc attempted to reference a file which could not be accessed or loaded: {0}", fname), inner)
		{
		}
		public DiscReferenceException(string fname, string extrainfo)
			: base(string.Format("A disc attempted to reference a file which could not be accessed or loaded:\n\n{0}\n\n{1}", fname, extrainfo))
		{
		}
	}

	

	sealed public partial class Disc
	{

		/// <summary>
		/// Returns a SectorEntry from which you can retrieve various interesting pieces of information about the sector.
		/// The SectorEntry's interface is not likely to be stable, though, but it may be more convenient.
		/// </summary>
		public SectorEntry ReadLBA_SectorEntry(int lba)
		{
			return Sectors[lba + 150];
		}


		/// <summary>
		/// Main API to determine how many LBAs are available on the disc.
		/// This counts from LBA 0 to the final sector available.
		/// THIS IS DUMB. Like everything else here.
		/// Fetch it from a toc or disc structure
		/// </summary>
		public int LBACount { get { return ABACount - 150; } }

		/// <summary>
		/// Main API to determine how many ABAs (sectors) are available on the disc.
		/// This counts from ABA 0 to the final sector available.
		/// </summary>
		public int ABACount { get { return Sectors.Count; } }

		// converts LBA to minute:second:frame format.
		//TODO - somewhat redundant with Timestamp, which is due for refactoring into something not cue-related
		public static void ConvertLBAtoMSF(int lba, out byte m, out byte s, out byte f)
		{
			lba += 150;
			m = (byte)(lba / 75 / 60);
			s = (byte)((lba - (m * 75 * 60)) / 75);
			f = (byte)(lba - (m * 75 * 60) - (s * 75));
		}

		// converts MSF to LBA offset
		public static int ConvertMSFtoLBA(byte m, byte s, byte f)
		{
			return f + (s * 75) + (m * 75 * 60) - 150;
		}

		
	}
}