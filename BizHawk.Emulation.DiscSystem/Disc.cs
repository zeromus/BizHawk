﻿using System;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Generic;

//ARCHITECTURE NOTE:
//No provisions are made for caching synthesized data for later accelerated use.
//This is because, in the worst case that might result in synthesizing an entire disc in memory.
//Instead, users should be advised to `hawk` the disc first for most rapid access so that synthesis won't be necessary and speed will be maximized.
//This will result in a completely flattened CCD where everything comes right off the hard drive
//Our choice here might be an unwise decision for disc ID and miscellaneous purposes but it's best for gaming and stream-converting (hawking and hashing)

//TODO: in principle, we could mount audio to decode only on an as-needed basis
//this might result in hiccups during emulation, though, so it should be an option.
//This would imply either decode-length processing (scan file without decoding) or decoding and discarding the data.
//We should probably have some richer policy specifications for this kind of thing, but it's not a high priority. Main workflow is still discohawking.
//Alternate policies would probably be associated with copious warnings (examples: ? ? ?)


//https://books.google.com/books?id=caF_AAAAQBAJ&lpg=PA124&ots=OA9Ttj9CHZ&dq=disc%20TOC%20point%20A2&pg=PA124
//http://www.staff.uni-mainz.de/tacke/scsi/SCSI2-14.html
//http://www.pctechguide.com/iso-9660-data-format-for-cds-cd-roms-cd-rs-and-cd-rws
//http://linux.die.net/man/1/cue2toc
//http://cdemu.sourceforge.net/project.php#sf

//apparently cdrdao is the ultimate linux tool for doing this stuff but it doesnt support DAO96 (or other DAO modes) that would be necessary to extract P-Q subchannels
//(cdrdao only supports R-W)

//here is a featureset list of windows cd burning programs (useful for cuesheet compatibility info)
//http://www.dcsoft.com/cue_mastering_progs.htm

//good links
//http://linux-sxs.org/bedtime/cdapi.html
//http://en.wikipedia.org/wiki/Track_%28CD%29
//http://docs.google.com/viewer?a=v&q=cache:imNKye05zIEJ:www.13thmonkey.org/documentation/SCSI/mmc-r10a.pdf+q+subchannel+TOC+format&hl=en&gl=us&pid=bl&srcid=ADGEEShtYqlluBX2lgxTL3pVsXwk6lKMIqSmyuUCX4RJ3DntaNq5vI2pCvtkyze-fumj7vvrmap6g1kOg5uAVC0IxwU_MRhC5FB0c_PQ2BlZQXDD7P3GeNaAjDeomelKaIODrhwOoFNb&sig=AHIEtbRXljAcFjeBn3rMb6tauHWjSNMYrw
//http://digitalx.org/cue-sheet/examples/

//"qemu cdrom emulator"
//http://www.koders.com/c/fid7171440DEC7C18B932715D671DEE03743111A95A.aspx
 
//less good
//http://www.cyberciti.biz/faq/getting-volume-information-from-cds-iso-images/
//http://www.cims.nyu.edu/cgi-systems/man.cgi?section=7I&topic=cdio

//some other docs
//http://www.emutalk.net/threads/54428-Reference-for-8-byte-sub-header-used-in-CDROM-XA references http://ccsun.nchu.edu.tw/~imtech/cou...act%20Disc.pdf which is pretty cool

//ideas:
/*
 * do some stuff asynchronously. for example, decoding mp3 sectors.
 * keep a list of sectors and the blob/offset from which they pull -- also whether the sector is available
 * if it is not available and something requests it then it will have to block while that sector gets generated
 * perhaps the blobs know how to resolve themselves and the requested sector can be immediately resolved (priority boost)
 * mp3 blobs should be hashed and dropped in %TEMP% as a wav decode
*/

//here is an MIT licensed C mp3 decoder
//http://core.fluendo.com/gstreamer/src/gst-fluendo-mp3/

/*information on saturn TOC and session data structures is on pdf page 58 of System Library User's Manual;
 * as seen in yabause, there are 1000 u32s in this format:
 * Ctrl[4bit] Adr[4bit] StartFrameAddressFAD[24bit] (nonexisting tracks are 0xFFFFFFFF)
 * Followed by Fist Track Information, Last Track Information..
 * Ctrl[4bit] Adr[4bit] FirstTrackNumber/LastTrackNumber[8bit] and then some stuff I dont understand
 * ..and Read Out Information:
 * Ctrl[4bit] Adr[4bit] ReadOutStartFrameAddress[24bit]
 * 
 * Also there is some stuff about FAD of sessions.
 * This should be generated by the saturn core, but we need to make sure we pass down enough information to do it
*/

//2048 bytes packed into 2352: 
//12 bytes sync(00 ff ff ff ff ff ff ff ff ff ff 00)
//3 bytes sector address (min+A0),sec,frac //does this correspond to ccd `point` field in the TOC entries?
//sector mode byte (0: silence; 1: 2048Byte mode (EDC,ECC,CIRC), 2: mode2 (could be 2336[vanilla mode2], 2048[xa mode2 form1], 2324[xa mode2 form2])
//cue sheets may use mode1_2048 (and the error coding needs to be regenerated to get accurate raw data) or mode1_2352 (the entire sector is present)
//audio is a different mode, seems to be just 2352 bytes with no sync, header or error correction. i guess the CIRC error correction is still there

namespace BizHawk.Emulation.DiscSystem
{

	public partial class Disc : IDisposable
	{
		/// <summary>
		/// The DiscMountPolicy used to mount the disc. Consider this read-only.
		/// NOT SURE WE NEED THIS
		/// </summary>
		//public DiscMountPolicy DiscMountPolicy;

		/// <summary>
		/// Free-form optional memos about the disc
		/// </summary>
		public Dictionary<string, object> Memos = new Dictionary<string, object>();

		/// <summary>
		/// The raw TOC entries found in the lead-in track.
		/// These aren't very useful, but theyre one of the most lowest-level data structures from which other TOC-related stuff is derived
		/// </summary>
		public List<RawTOCEntry> RawTOCEntries = new List<RawTOCEntry>();

		/// <summary>
		/// The DiscTOCRaw corresponding to the RawTOCEntries.
		/// TODO - rename to TOC
		/// </summary>
		public DiscTOCRaw TOCRaw;

		/// <summary>
		/// The DiscStructure corresponding to the TOCRaw
		/// </summary>
		public DiscStructure Structure;

		/// <summary>
		/// Disposable resources (blobs, mostly) referenced by this disc
		/// </summary>
		internal List<IDisposable> DisposableResources = new List<IDisposable>();

		/// <summary>
		/// The sectors on the disc
		/// </summary>
		public List<SectorEntry> Sectors = new List<SectorEntry>();

		internal SectorSynthParams SynthParams = new SectorSynthParams();

		public Disc()
		{
		}

		public void Dispose()
		{
			foreach (var res in DisposableResources)
			{
				res.Dispose();
			}
		}


		/// <summary>
		/// generates lead-out sectors according to very crude approximations
		/// </summary>
		public class SynthesizeLeadoutJob
		{
			public int Length;
			public Disc Disc;
			
			public void Run()
			{
				//TODO: encode_mode2_form2_sector
				var sz = new Sector_Zero();

				var leadoutTs = Disc.TOCRaw.LeadoutLBA;
				var lastTrackTOCItem = Disc.TOCRaw.TOCItems[Disc.TOCRaw.LastRecordedTrackNumber]; //NOTE: in case LastRecordedTrackNumber is al ie, this will malfunction

				//leadout flags.. let's set them the same as the last track.
				//THIS IS NOT EXACTLY THE SAME WAY MEDNAFEN DOES IT
				EControlQ leadoutFlags = lastTrackTOCItem.Control;

				//TODO - needs to be encoded as a certain mode (mode 2 form 2 for psx... i guess...)

				for (int i = 0; i < Length; i++)
				{
					var se = new SectorEntry(sz);
					Disc.Sectors.Add(se);
					SubchannelQ sq = new SubchannelQ();

					int track_relative_msf = i;
					sq.min = BCD2.FromDecimal(new Timestamp(track_relative_msf).MIN);
					sq.sec = BCD2.FromDecimal(new Timestamp(track_relative_msf).SEC);
					sq.frame = BCD2.FromDecimal(new Timestamp(track_relative_msf).FRAC);

					int absolute_msf = i + leadoutTs.Sector;
					sq.ap_min = BCD2.FromDecimal(new Timestamp(absolute_msf+150).MIN);
					sq.ap_sec = BCD2.FromDecimal(new Timestamp(absolute_msf + 150).SEC);
					sq.ap_frame = BCD2.FromDecimal(new Timestamp(absolute_msf + 150).FRAC);

					sq.q_tno.DecimalValue = 0xAA; //special value for leadout
					sq.q_index.DecimalValue = 1;

					byte ADR = 1;
					sq.SetStatus(ADR, leadoutFlags);

					var subcode = new BufferedSubcodeSector();
					subcode.Synthesize_SubchannelQ(ref sq, true);
					se.SubcodeSector = subcode;
				}
			}
		}

		/// <summary>
		/// Automagically loads a disc, without any fine-tuned control at all
		/// </summary>
		public static Disc LoadAutomagic(string path)
		{
			var job = new DiscMountJob { IN_FromPath = path };
			job.IN_DiscInterface = DiscInterface.MednaDisc; //TEST
			job.Run();
			return job.OUT_Disc;
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

		/// <summary>
		/// applies an SBI file to the disc
		/// </summary>
		public void ApplySBI(SBI.SubQPatchData sbi, bool asMednafen)
		{
			//TODO - could implement as a blob, to avoid allocating so many byte buffers

			//save this, it's small, and we'll want it for disc processing a/b checks
			Memos["sbi"] = sbi;

			DiscSectorReader dsr = new DiscSectorReader(this);

			int n = sbi.ABAs.Count;
			int b=0;
			for (int i = 0; i < n; i++)
			{
				int lba = sbi.ABAs[i] - 150;

				//create a synthesizer which can return the patched data
				var ss_patchq = new SS_PatchQ() { Original = this.Sectors[lba+150].SectorSynth };
				byte[] subQbuf = ss_patchq.Buffer_SubQ;

				//read the old subcode
				dsr.ReadLBA_SubQ(lba, subQbuf, 0);

				//insert patch
				Sectors[lba + 150].SectorSynth = ss_patchq;

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
					SynthUtils.SubQ_Checksum(subQbuf, 0);
					subQbuf[10] ^= 0xFF;
					subQbuf[11] ^= 0xFF;
				}
			}
		}

		static byte IntToBCD(int n)
		{
			int ones;
			int tens = Math.DivRem(n,10,out ones);
			return (byte)((tens<<4)|ones);
		}
	}

	/// <summary>
	/// encapsulates a 2 digit BCD number as used various places in the CD specs
	/// </summary>
	public struct BCD2
	{
		/// <summary>
		/// The raw BCD value. you can't do math on this number! but you may be asked to supply it to a game program.
		/// The largest number it can logically contain is 99
		/// </summary>
		public byte BCDValue;

		/// <summary>
		/// The derived decimal value. you can do math on this! the largest number it can logically contain is 99.
		/// </summary>
		public int DecimalValue
		{
			get { return (BCDValue & 0xF) + ((BCDValue >> 4) & 0xF) * 10; }
			set { BCDValue = IntToBCD(value); }
		}

		/// <summary>
		/// makes a BCD2 from a decimal number. don't supply a number > 99 or you might not like the results
		/// </summary>
		public static BCD2 FromDecimal(int d)
		{
			return new BCD2 {DecimalValue = d};
		}

		public static BCD2 FromBCD(byte b)
		{
			return new BCD2 { BCDValue = b };
		}

		public static int BCDToInt(byte n)
		{
			var bcd = new BCD2();
			bcd.BCDValue = n;
			return bcd.DecimalValue;
		}

		public static byte IntToBCD(int n)
		{
			int ones;
			int tens = Math.DivRem(n, 10, out ones);
			return (byte)((tens << 4) | ones);
		}

		public override string ToString()
		{
			return BCDValue.ToString("X2");
		}
	}

	/// <summary>
	/// todo - rename to MSF? It can specify durations, so maybe it should be not suggestive of timestamp
	/// TODO - can we maybe use BCD2 in here
	/// </summary>
	public struct Timestamp
	{
		/// <summary>
		/// Checks if the string is a legit MSF. It's strict.
		/// </summary>
		public static bool IsMatch(string str)
		{
			return new Timestamp(str).Valid;
		}

		/// <summary>
		/// creates a timestamp from a string in the form mm:ss:ff
		/// </summary>
		public Timestamp(string str)
		{
			if (str.Length != 8) goto BOGUS;
			if (str[0] < '0' || str[0] > '9') goto BOGUS;
			if (str[1] < '0' || str[1] > '9') goto BOGUS;
			if (str[2] != ':') goto BOGUS;
			if (str[3] < '0' || str[3] > '9') goto BOGUS;
			if (str[4] < '0' || str[4] > '9') goto BOGUS;
			if (str[5] != ':') goto BOGUS;
			if (str[6] < '0' || str[6] > '9') goto BOGUS;
			if (str[7] < '0' || str[7] > '9') goto BOGUS;
			MIN = (byte)((str[0] - '0') * 10 + (str[1] - '0'));
			SEC = (byte)((str[3] - '0') * 10 + (str[4] - '0'));
			FRAC = (byte)((str[6] - '0') * 10 + (str[7] - '0'));
			Valid = true;
			Negative = false;
			return;
		BOGUS:
			MIN = SEC = FRAC = 0;
			Valid = false;
			Negative = false;
			return;
		}

		/// <summary>
		/// The string representation of the MSF
		/// </summary>
		public string Value
		{
			get
			{
				if (!Valid) return "--:--:--";
				return string.Format("{0}{1:D2}:{2:D2}:{3:D2}", Negative?'-':'+',MIN, SEC, FRAC);
			}
		}

		public readonly byte MIN, SEC, FRAC;
		public readonly bool Valid, Negative;

		/// <summary>
		/// The fully multiplied out flat-address Sector number
		/// </summary>
		public int Sector { get { return MIN * 60 * 75 + SEC * 75 + FRAC; } }

		/// <summary>
		/// creates timestamp from the supplied MSF
		/// </summary>
		public Timestamp(int m, int s, int f)
		{
			MIN = (byte)m;
			SEC = (byte)s;
			FRAC = (byte)f;
			Valid = true;
			Negative = false;
		}

		/// <summary>
		/// creates timestamp from supplied SectorNumber
		/// </summary>
		public Timestamp(int SectorNumber)
		{
			if (SectorNumber < 0)
			{
				SectorNumber = -SectorNumber;
				Negative = true;
			}
			else Negative = false;
			MIN = (byte)(SectorNumber / (60 * 75));
			SEC = (byte)((SectorNumber / 75) % 60);
			FRAC = (byte)(SectorNumber % 75);
			Valid = true;
		}

		public override string ToString()
		{
			return Value;
		}
	}


	static class SynthUtils
	{

		/// <summary>
		/// Calculates the checksum of the provided Q subchannel
		/// </summary>
		/// <param name="buffer">12 byte Q subchannel: input and output buffer for operation</param>
		/// <param name="offset">location within buffer of Q subchannel</param>
		public static void SubQ_Checksum(byte[] buffer, int offset)
		{
			ushort crc16 = CRC16_CCITT.Calculate(buffer, offset, 10);

			//CRC is stored inverted and big endian
			buffer[offset + 10] = (byte)(~(crc16 >> 8));
			buffer[offset + 11] = (byte)(~(crc16));
		}

		public static void SubP(byte[] buffer, int offset, bool pause)
		{
			byte val = (byte)(pause ? 0xFF : 0x00);
			for (int i = 0; i < 12; i++)
				buffer[offset + i] = val;
		}

		public static void SectorHeader(byte[] buffer, int offset, int LBA, byte mode)
		{
			buffer[offset + 0] = 0x00;
			for (int i = 1; i < 11; i++) buffer[offset + i] = 0xFF;
			buffer[offset + 11] = 0x00;
			Timestamp ts = new Timestamp(LBA + 150);
			buffer[offset + 12] = BCD2.IntToBCD(ts.MIN);
			buffer[offset + 13] = BCD2.IntToBCD(ts.SEC);
			buffer[offset + 14] = BCD2.IntToBCD(ts.FRAC);
			buffer[offset + 15] = mode;
		}

		public static void EDC_Mode2_Form1(byte[] buffer, int offset)
		{
			uint edc = ECM.EDC_Calc(buffer, offset + 16, 2048 + 8);
			ECM.PokeUint(buffer, offset + 2072, edc);
		}

		public static void EDC_Mode2_Form2(byte[] buffer, int offset)
		{
			uint edc = ECM.EDC_Calc(buffer, offset + 16, 2324 + 8);
			ECM.PokeUint(buffer, offset + 2348, edc);
		}


		/// <summary>
		/// Make sure everything else in the sector userdata is done before calling this
		/// </summary>
		public static void ECM_Mode1(byte[] buffer, int offset, int LBA)
		{
			//EDC
			uint edc = ECM.EDC_Calc(buffer, offset, 2064);
			ECM.PokeUint(buffer, offset + 2064, edc);

			//reserved, zero
			for (int i = 0; i < 8; i++) buffer[offset + 2068 + i] = 0;

			//ECC
			ECM.ECC_Populate(buffer, offset, buffer, offset, false);
		}
	}

	//not being used yet
	class DiscPreferences
	{

	}
}