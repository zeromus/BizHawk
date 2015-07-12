using System;
using System.Collections.Generic;
using System.IO;

namespace BizHawk.Emulation.DiscSystem
{
	/// <summary>
	/// Indicates which part of a sector are needing to be synthesized.
	/// Sector synthesis may create too much data, but this is a hint as to what's needed
	/// TODO - add a flag indicating whether clearing has happened
	/// TODO - add output to the job indicating whether interleaving has happened. let the sector reader be responsible
	/// </summary>
	[Flags] enum ESectorSynthPart
	{
		/// <summary>
		/// The data sector header is required. There's no header for audio tracks/sectors.
		/// </summary>
		Header16 = 1,
		
		/// <summary>
		/// The main 2048 user data bytes are required
		/// </summary>
		User2048 = 2,

		/// <summary>
		/// The 276 bytes of error correction are required
		/// </summary>
		ECC276 = 4,

		/// <summary>
		/// The 12 bytes preceding the ECC section are required (usually EDC and zero but also userdata sometimes)
		/// </summary>
		EDC12 = 8,

		/// <summary>
		/// The entire possible 276+12=288 bytes of ECM data is required (ECC276|EDC12)
		/// </summary>
		ECM288Complete = (ECC276 | EDC12),

		/// <summary>
		/// An alias for ECM288Complete
		/// </summary>
		ECMAny = ECM288Complete,

		/// <summary>
		/// A mode2 userdata section is required: the main 2048 user bytes AND the ECC and EDC areas
		/// </summary>
		User2336 = (User2048 | ECM288Complete),

		/// <summary>
		/// The complete sector userdata (2352 bytes) is required
		/// </summary>
		UserComplete = 15,

		/// <summary>
		/// An alias for UserComplete
		/// </summary>
		UserAny = UserComplete,

		/// <summary>
		/// An alias for UserComplete
		/// </summary>
		User2352 = UserComplete,

		/// <summary>
		/// SubP is required
		/// </summary>
		SubchannelP = 16,

		/// <summary>
		/// SubQ is required
		/// </summary>
		SubchannelQ = 32,

		/// <summary>
		/// Subchannels R-W (all except for P and Q)
		/// </summary>
		Subchannel_RSTUVW = (64|128|256|512|1024|2048),

		/// <summary>
		/// Complete subcode is required
		/// </summary>
		SubcodeComplete = (SubchannelP | SubchannelQ | Subchannel_RSTUVW),

		/// <summary>
		/// Any of the subcode might be required (just another way of writing SubcodeComplete)
		/// </summary>
		SubcodeAny = SubcodeComplete,

		/// <summary>
		/// The subcode should be deinterleaved
		/// </summary>
		SubcodeDeinterleave = 4096,

		/// <summary>
		/// The 100% complete sector is required including 2352 bytes of userdata and 96 bytes of subcode
		/// </summary>
		Complete2448 = SubcodeComplete | User2352,
	}

	/// <summary>
	/// Basic unit of sector synthesis
	/// </summary>
	interface ISectorSynthJob2448
	{
		/// <summary>
		/// Synthesizes a sctor with the given job parameters
		/// </summary>
		void Synth(SectorSynthJob job);
	}

	/// <summary>
	/// When creating a disc, this is set with a callback that can deliver an ISectorSynthJob2448 for the given LBA
	/// </summary>
	interface ISectorSynthProvider
	{
		/// <summary>
		/// Retrieves an ISectorSynthJob2448 for the given LBA
		/// </summary>
		ISectorSynthJob2448 Get(int lba);
	}

	/// <summary>
	/// A simple ISectorSynthProvider which always returns the same ISectorSynthJob2448
	/// </summary>
	class SimpleSectorSynthProvider : ISectorSynthProvider
	{
		public ISectorSynthJob2448 Job;
		public ISectorSynthJob2448 Get(int lba)
		{
			return Job;
		}
	}

	/// <summary>
	/// An ISectorSynthProvider which patches another ISectorSynthProvider with an array of optional patches
	/// </summary>
	class SparsePatchSectorSynthProvider : ISectorSynthProvider
	{
		public int MapStart, MapEnd;
		public ISectorSynthProvider OldProvider;

		struct PatchRecord
		{
			public ISectorSynthJob2448 SS_Patch;
			public int Index;
		}

		PatchRecord[] Patches;


		public void SetPatch(int lba, int index, ISectorSynthJob2448 ss)
		{
			int idx = lba - MapStart;
			Patches[idx].SS_Patch = ss;
			Patches[idx].Index = index;
		}

		public void Initialize(ISectorSynthProvider oldProvider, int start, int end)
		{
			OldProvider = oldProvider;
			MapStart = start;
			MapEnd = end;
			int count = MapStart - MapEnd + 1;
			Patches = new PatchRecord[count];
		}

		public ISectorSynthJob2448 Get(int lba)
		{
			if (lba < MapStart) return OldProvider.Get(lba);
			if (lba > MapEnd) return OldProvider.Get(lba);
			var patch = Patches[lba - MapStart].SS_Patch;
			if (patch == null) return OldProvider.Get(lba);
			return patch;
		}
	}

	/// <summary>
	/// Not a proper job? maybe with additional flags, it could be
	/// </summary>
	class SectorSynthJob
	{
		/// <summary>
		/// The LBA to synth a sector for
		/// </summary>
		public int LBA;

		/// <summary>
		/// Flags for each part of a sector (and some other flags) which could need generating
		/// </summary>
		public ESectorSynthPart Parts;

		/// <summary>
		/// A target buffer for the synthesis job. Components are always parked at their normal locations within this sector.
		/// Synthesis is not required to clear the destination buffer
		/// </summary>
		public byte[] DestBuffer2448;

		/// <summary>
		/// Offset within DestBuffer2448 to park data
		/// </summary>
		public int DestOffset;

		/// <summary>
		/// A copy of the SectorSynthParams set on the disc (sector synths will use this to drive the synthesis of multiple sectors from one instance of the sector s ynth)
		/// </summary>
		public SectorSynthParams Params;

		/// <summary>
		/// The disc that's being worked on.. just in case you need it, I guess
		/// </summary>
		public Disc Disc;
	}

	/// <summary>
	/// Generic parameters for sector synthesis.
	/// To cut down on resource utilization, these can be stored in a disc and are tightly coupled to
	/// the SectorSynths that have been setup for it
	/// </summary>
	struct SectorSynthParams
	{
		public long[] BlobOffsets;
		public MednaDisc MednaDisc;

		/// <summary>
		/// used by the SBI patcher to store offsets within the 
		/// </summary>
		public int[] SBIOffsets;
	}


	class SS_PatchQ : ISectorSynthJob2448
	{
		public ISectorSynthJob2448 Original;
		public byte[] Buffer_SubQ = new byte[12];
		public void Synth(SectorSynthJob job)
		{
			Original.Synth(job);

			if ((job.Parts & ESectorSynthPart.SubchannelQ) == 0)
				return;

			//apply patched subQ
			for (int i = 0; i < 12; i++)
				job.DestBuffer2448[2352 + 12 + i] = Buffer_SubQ[i];
		}
	}


}