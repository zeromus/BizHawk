﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

using BizHawk.Emulation.Common;
using BizHawk.Emulation.Common.IEmulatorExtensions;

namespace BizHawk.Client.Common
{
	/// <summary>
	/// Captures savestates and manages the logic of adding, retrieving, 
	/// invalidating/clearing of states.  Also does memory management and limiting of states
	/// </summary>
	public class TasStateManager
	{
		// TODO: pass this in, and find a solution to a stale reference (this is instantiated BEFORE a new core instance is made, making this one stale if it is simply set in the constructor
		private IStatable Core
		{
			get
			{
				return Global.Emulator.AsStatable();
			}
		}

		public Action<int> InvalidateCallback { get; set; }

		private void CallInvalidateCallback(int index)
		{
			if (InvalidateCallback != null)
			{
				InvalidateCallback(index);
			}
		}

		private readonly SortedList<int, byte[]> States = new SortedList<int, byte[]>();
		private string statePath
		{
			get
			{
				return PathManager.MakeAbsolutePath(Global.Config.PathEntries["Global", "TAStudio states"].Path, null);
			}
		}

		private readonly TasMovie _movie;
		private ulong _expectedStateSize = 0;

		private int _minFrequency = VersionInfo.DeveloperBuild ? 2 : 1;
		private const int _maxFrequency = 16;
		private int StateFrequency
		{
			get
			{
				int freq = (int)(_expectedStateSize / 65536);

				if (freq < _minFrequency)
				{
					return _minFrequency;
				}

				if (freq > _maxFrequency)
				{
					return _maxFrequency;
				}

				return freq;
			}
		}

		private int maxStates
		{ get { return (int)(Settings.Cap / _expectedStateSize); } }

		public TasStateManager(TasMovie movie)
		{
			_movie = movie;

			Settings = new TasStateManagerSettings(Global.Config.DefaultTasProjSettings);

			int limit = 0;

			_expectedStateSize = (ulong)Core.SaveStateBinary().Length;

			if (_expectedStateSize > 0)
			{
				limit = maxStates;
			}

			States = new SortedList<int, byte[]>(limit);
			if (Directory.Exists(statePath))
			{
				Directory.Delete(statePath, true); // To delete old files that may still exist.
			}
			Directory.CreateDirectory(statePath);
			accessed = new List<int>();
		}

		public TasStateManagerSettings Settings { get; set; }

		/// <summary>
		/// Retrieves the savestate for the given frame,
		/// If this frame does not have a state currently, will return an empty array
		/// </summary>
		/// <returns>A savestate for the given frame or an empty array if there isn't one</returns>
		public KeyValuePair<int, byte[]> this[int frame]
		{
			get
			{
				if (frame == 0 && _movie.StartsFromSavestate)
				{
					return new KeyValuePair<int, byte[]>(0, _movie.BinarySavestate);
				}

				if (States.ContainsKey(frame))
				{
					// if (States[frame] == null) // Get from file
					StateAccessed(frame);
					return new KeyValuePair<int, byte[]>(frame, States[frame]);
				}

				return new KeyValuePair<int, byte[]>(-1, new byte[0]);
			}
		}
		private List<int> accessed;

		public byte[] InitialState
		{
			get
			{
				if (_movie.StartsFromSavestate)
				{
					return _movie.BinarySavestate;
				}

				return States[0];
			}
		}

		/// <summary>
		/// Requests that the current emulator state be captured 
		/// Unless force is true, the state may or may not be captured depending on the logic employed by "greenzone" management
		/// </summary>
		public void Capture(bool force = false)
		{
			bool shouldCapture = false;

			int frame = Global.Emulator.Frame;
			if (_movie.StartsFromSavestate && frame == 0) // Never capture frame 0 on savestate anchored movies since we have it anyway
			{
				shouldCapture = false;
			}
			else if (force)
			{
				shouldCapture = force;
			}
			else if (frame == 0) // For now, long term, TasMovie should have a .StartState property, and a tasproj file for the start state in non-savestate anchored movies
			{
				shouldCapture = true;
			}
			else if (_movie.Markers.IsMarker(frame + 1))
			{
				shouldCapture = true; // Markers shoudl always get priority
			}
			else
			{
				shouldCapture = frame - States.Keys.LastOrDefault(k => k < frame) >= StateFrequency;
			}

			if (shouldCapture)
			{
				SetState(frame, (byte[])Core.SaveStateBinary().Clone());
			}
		}

		private void MaybeRemoveState()
		{
			int shouldRemove = -1;
			if (Used + DiskUsed > Settings.CapTotal)
				shouldRemove = StateToRemove();
			if (shouldRemove != -1)
			{
				RemoveState(States.ElementAt(shouldRemove).Key);
			}

			if (Used > Settings.Cap)
			{
				int lastMemState = -1;
				do { lastMemState++; } while (States[accessed[lastMemState]] == null);
				MoveStateToDisk(accessed[lastMemState]);
			}
		}
		private int StateToRemove()
		{
			int markerSkips = maxStates / 3;

			int shouldRemove = _movie.StartsFromSavestate ? -1 : 0;
			do
			{
				shouldRemove++;

				// No need to have two savestates with only lag frames between them.
				for (int i = shouldRemove; i < States.Count - 1; i++)
				{
					if (AllLag(States.ElementAt(i).Key, States.ElementAt(i + 1).Key))
					{
						shouldRemove = i;
						break;
					}
				}

				// Keep marker states
				markerSkips--;
				if (markerSkips < 0)
					shouldRemove = _movie.StartsFromSavestate ? 0 : 1;
			} while (_movie.Markers.IsMarker(States.ElementAt(shouldRemove).Key + 1) && markerSkips > -1);

			return shouldRemove;
		}
		private bool AllLag(int from, int upTo)
		{
			if (upTo >= Global.Emulator.Frame)
			{
				upTo = Global.Emulator.Frame - 1;
				if (!Global.Emulator.AsInputPollable().IsLagFrame)
					return false;
			}

			for (int i = from; i < upTo; i++)
			{
				if (!_movie[i].Lagged.Value)
					return false;
			}

			return true;
		}

		private void MoveStateToDisk(int index)
		{
			// Save
			string path = Path.Combine(statePath, index.ToString());
			File.WriteAllBytes(path, States[index]);
			DiskUsed += _expectedStateSize;

			// Remove from RAM
			Used -= (ulong)States[index].Length;
			States[index] = null;
		}
		private void MoveStateToMemory(int index)
		{
			// Load
			string path = Path.Combine(statePath, index.ToString());
			byte[] loadData = File.ReadAllBytes(path);
			DiskUsed -= _expectedStateSize;

			// States list
			Used += (ulong)loadData.Length;
			States[index] = loadData;

			File.Delete(path);
		}
		private void SetState(int frame, byte[] state)
		{
			if (States.ContainsKey(frame))
			{
				States[frame] = state;
				MaybeRemoveState(); // Also does moving to disk
			}
			else
			{
				Used += (ulong)state.Length;
				MaybeRemoveState(); // Remove before adding so this state won't be removed.

				States.Add(frame, state);
			}
			StateAccessed(frame);
		}
		private void RemoveState(int index)
		{
			if (States[index] == null)
			{
				DiskUsed -= _expectedStateSize; // Disk length?
				string path = Path.Combine(statePath, index.ToString());
				File.Delete(path);
			}
			else
				Used -= (ulong)States[index].Length;
			States.RemoveAt(States.IndexOfKey(index));

			accessed.Remove(index);
		}
		private void StateAccessed(int index)
		{
			bool removed = accessed.Remove(index);
			accessed.Add(index);

			if (States[index] == null)
			{
				if (States[accessed[0]] != null)
					MoveStateToDisk(accessed[0]);
				MoveStateToMemory(index);
			}

			if (!removed && accessed.Count > (int)(Used / _expectedStateSize))
				accessed.RemoveAt(0);
		}

		public bool HasState(int frame)
		{
			if (_movie.StartsFromSavestate && frame == 0)
			{
				return true;
			}

			return States.ContainsKey(frame);
		}

		/// <summary>
		/// Clears out all savestates after the given frame number
		/// </summary>
		public void Invalidate(int frame)
		{
			if (Any())
			{
				if (!_movie.StartsFromSavestate && frame == 0) // Never invalidate frame 0 on a non-savestate-anchored movie
				{
					frame = 1;
				}

				var statesToRemove = States
					.Where(x => x.Key >= frame)
					.ToList();
				foreach (var state in statesToRemove)
				{
					if (state.Value == null)
						DiskUsed -= _expectedStateSize; // Length??
					else
						Used -= (ulong)state.Value.Length;
					accessed.Remove(state.Key);
					States.Remove(state.Key);
				}

				CallInvalidateCallback(frame);
			}
		}

		/// <summary>
		/// Clears all state information
		/// </summary>
		/// 
		public void Clear()
		{
			States.Clear();
			accessed.Clear();
			Used = 0;
			DiskUsed = 0;
		}

		public void ClearStateHistory()
		{
			if (States.Any())
			{
				KeyValuePair<int, byte[]> power = States.FirstOrDefault(s => s.Key == 0);
				if (power.Value == null)
				{
					StateAccessed(power.Key);
					power = States.FirstOrDefault(s => s.Key == 0);
				}
				States.Clear();
				accessed.Clear();

				if (power.Value.Length > 0)
				{
					SetState(0, power.Value);
					Used = (ulong)power.Value.Length;
				}
				else
				{
					Used = 0;
					DiskUsed = 0;
				}
			}
		}

		public void Save(BinaryWriter bw)
		{
			List<int> noSave = ExcludeStates();

			bw.Write(States.Count - noSave.Count);
			for (int i = 0; i < States.Count; i++)
			{
				if (noSave.Contains(i))
					continue;

				StateAccessed(States.ElementAt(i).Key);
				KeyValuePair<int, byte[]> kvp = States.ElementAt(i);
				bw.Write(kvp.Key);
				bw.Write(kvp.Value.Length);
				bw.Write(kvp.Value);
			}
		}
		private List<int> ExcludeStates()
		{
			List<int> ret = new List<int>();

			ulong saveUsed = Used + DiskUsed;
			int index = -1;
			while (saveUsed > (ulong)Settings.DiskSaveCapacitymb * 1024 * 1024)
			{
				do
				{
					index++;
				} while (_movie.Markers.IsMarker(States.ElementAt(index).Key + 1));
				ret.Add(index);
				if (States.ElementAt(index).Value == null)
					saveUsed -= _expectedStateSize;
				else
					saveUsed -= (ulong)States.ElementAt(index).Value.Length;
			}

			// If there are enough markers to still be over the limit, remove marker frames
			index = -1;
			while (saveUsed > (ulong)Settings.DiskSaveCapacitymb * 1024 * 1024)
			{
				index++;
				ret.Add(index);
				if (States.ElementAt(index).Value == null)
					saveUsed -= _expectedStateSize;
				else
					saveUsed -= (ulong)States.ElementAt(index).Value.Length;
			}

			return ret;
		}

		public void Load(BinaryReader br)
		{
			States.Clear();
			//if (br.BaseStream.Length > 0)
			//{ BaseStream.Length does not return the expected value.
			int nstates = br.ReadInt32();
			for (int i = 0; i < nstates; i++)
			{
				int frame = br.ReadInt32();
				int len = br.ReadInt32();
				byte[] data = br.ReadBytes(len);
				SetState(frame, data);
				//States.Add(frame, data);
				//Used += len;
			}
			//}
		}

		public KeyValuePair<int, byte[]> GetStateClosestToFrame(int frame)
		{
			var s = States.LastOrDefault(state => state.Key < frame);

			return this[s.Key];
		}

		// Map:
		// 4 bytes - total savestate count
		//[Foreach state]
		// 4 bytes - frame
		// 4 bytes - length of savestate
		// 0 - n savestate

		private ulong Used
		{
			get;
			set;
		}
		private ulong DiskUsed
		{
			get;
			set;
		}

		public int StateCount
		{
			get
			{
				return States.Count;
			}
		}

		public bool Any()
		{
			if (_movie.StartsFromSavestate)
			{
				return States.Count > 0;
			}

			return States.Count > 1;
		}

		public int LastKey
		{
			get
			{
				if (States.Count == 0)
				{
					return 0;
				}

				return States.Last().Key;
			}
		}

		public int LastEmulatedFrame
		{
			get
			{
				if (StateCount > 0)
				{
					return LastKey;
				}

				return 0;
			}
		}
	}
}
