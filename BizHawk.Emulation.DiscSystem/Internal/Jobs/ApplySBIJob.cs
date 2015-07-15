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
		public class SBIPatch
		{
			Disc disc;
			SBI.SubQPatchData sbi;
			bool asMednafen;

			ISectorSynthProvider oldProvider;

			class SS_SBIPatch : ISectorSynthJob2448
			{
				public int SBIOffset;

				public void Synth(SectorSynthJob job)
				{
					//run the earlier synthesizer
					job.Params.SBIPatch.oldProvider.Get(job.LBA).Synth(job);
					var sbi = job.Params.SBIPatch.sbi;

					//if subQ was requested, it's now provided; we should patch it
					if ((job.Parts & ESectorSynthPart.SubchannelQ) != 0)
					{
						var buf = job.DestBuffer2448;
						var ofs = job.DestOffset + 2352 + 12;
						int b = SBIOffset;

						//apply SBI patch
						for (int j = 0; j < 12; j++)
						{
							short patch = sbi.subq[b++];
							if (patch == -1) continue;
							else buf[ofs + j] = (byte)patch;
						}

						//Apply mednafen hacks
						//The reasoning here is that we know we expect these sectors to have a wrong checksum. therefore, generate a checksum, and make it wrong
						//However, this seems senseless to me. The whole point of the SBI data is that it stores the patches needed to generate an acceptable subQ, right?
						//if (asMednafen) //ehh, lets always do it this way for now
						{
							SynthUtils.SubQ_SynthChecksum(buf, ofs);
							buf[ofs + 10] ^= 0xFF;
							buf[ofs + 11] ^= 0xFF;
						}
					}
				}
			}

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
				int ofs = 0;
				for (int i = 0; i < sbi.ABAs.Count; i++)
				{
					int lba = sbi.ABAs[i] + 150;
					var ss = new SS_SBIPatch()
					{
						SBIOffset = ofs
					};
					spssp.SetPatch(lba, i, ss);
					ofs += 12;
				}
			}
		}

		/// <summary>
		/// applies an SBI file to the disc
		/// </summary>
		public void Run(Disc disc, SBI.SubQPatchData sbi, bool asMednafen)
		{
			SBIPatch patch = new SBIPatch(disc, sbi, asMednafen);
		}
	}
}
