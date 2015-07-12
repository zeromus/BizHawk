using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace BizHawk.Emulation.DiscSystem
{
	class ApplySBIJob
	{
		class SBIPatch : ISectorSynthJob2448
		{
			Disc disc;
			SBI.SubQPatchData sbi;
			bool asMednafen;

			ISectorSynthProvider oldProvider;

			public SBIPatch(Disc disc, SBI.SubQPatchData sbi, bool asMednafen)
			{
				this.disc = disc;
				this.sbi = sbi;
				this.asMednafen = asMednafen;
				
				//find minimum and maximum ABA of patches
				int minIndex = -1, maxIndex = -1, min = int.MaxValue, max = int.MinValue;
				for (int i = 0; i < sbi.ABAs.Count; i++)
				{
					if (sbi.ABAs[i] < min) { minIndex = i; min = sbi.ABAs[i]; }
					if (sbi.ABAs[i] > max) { maxIndex = i; max = sbi.ABAs[i]; }
				}

				//insert our own provider
				oldProvider = disc.SynthProvider;
				var spssp = new SparsePatchSectorSynthProvider();
				spssp.Initialize(oldProvider, minIndex + 150, maxIndex + 150);
				disc.SynthProvider = spssp;

				//set patches
				for (int i = 0; i < sbi.ABAs.Count; i++)
				{
					int lba = sbi.ABAs[i] + 150;
					spssp.SetPatch(lba, i, this);
				}
			}

			public void Synth(SectorSynthJob job)
			{
				//run the earlier synthesizer
				oldProvider.Get(job.LBA).Synth(job);

				//if subQ was requested, it's now provided; we should patch it
				if ((job.Parts & ESectorSynthPart.SubchannelQ) != 0)
				{
					var buf = job.DestBuffer2448;
					var ofs = job.DestOffset + 2352 + 12;

					//apply SBI patch
					for (int j = 0; j < 12; j++)
					{
						short patch = sbi.subq[b++];
						if (patch == -1) continue;
						else buf[ofs+j] = (byte)patch;
					}

					//Apply mednafen hacks
					//The reasoning here is that we know we expect these sectors to have a wrong checksum. therefore, generate a checksum, and make it wrong
					//However, this seems senseless to me. The whole point of the SBI data is that it stores the patches needed to generate an acceptable subQ, right?
					if (asMednafen)
					{
						SynthUtils.SubQ_SynthChecksum(subQbuf, 0);
						subQbuf[10] ^= 0xFF;
						subQbuf[11] ^= 0xFF;
					}
				}
			}
		}

		/// <summary>
		/// applies an SBI file to the disc
		/// </summary>
		public void Run(Disc disc, SBI.SubQPatchData sbi, bool asMednafen)
		{
			//TODO - could implement as a blob, to avoid allocating so many byte buffers

			//save this, it's small, and we'll want it for disc processing a/b checks
			disc.Memos["sbi"] = sbi;



			DiscSectorReader dsr = new DiscSectorReader(disc);

			int n = sbi.ABAs.Count;
			int b = 0;
			for (int i = 0; i < n; i++)
			{
				int lba = sbi.ABAs[i] - 150;

				//create a synthesizer which can return the patched data
				var ss_patchq = new SS_PatchQ() { Original = disc.Sectors[lba + 150] };
				byte[] subQbuf = ss_patchq.Buffer_SubQ;

				//read the old subcode
				dsr.ReadLBA_SubQ(lba, subQbuf, 0);

				//insert patch
				disc.Sectors[lba + 150] = ss_patchq;

				//apply SBI patch
				for (int j = 0; j < 12; j++)
				{
					short patch = sbi.subq[b++];
					if (patch == -1) continue;
					else subQbuf[j] = (byte)patch;
				}

				//Apply mednafen hacks
				//The reasoning here is that we know we expect these sectors to have a wrong checksum. therefore, generate a checksum, and make it wrong
				//However, this seems senseless to me. The whole point of the SBI data is that it stores the patches needed to generate an acceptable subQ, right?
				if (asMednafen)
				{
					SynthUtils.SubQ_SynthChecksum(subQbuf, 0);
					subQbuf[10] ^= 0xFF;
					subQbuf[11] ^= 0xFF;
				}
			}
		}
	}
}
