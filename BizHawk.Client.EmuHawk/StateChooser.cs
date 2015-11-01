using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows.Forms;

using BizHawk.Bizware.BizwareGL;
using BizHawk.Common;
using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	public partial class StateChooser : Form
	{
		private bool _sortReverse;
		private string _sortedCol;

		public StateChooser()
		{
			InitializeComponent();

			_sortReverse = false;
			_sortedCol = string.Empty;

			ScanFiles();
		}

		void lvStates_QueryItemImage(int index, int column, out int imageIndex)
		{
			imageIndex = -1;
			if (column == 0) // File
			{
				imageIndex = index;
			}
		}

		private void lvStates_QueryItemText(int index, int column, out string text)
		{
			text = string.Empty;
			if (column == 0) // File
			{
				text = Path.GetFileName(_stateList[index].Filename);
			}
		}

		class StateInfo
		{
			public string Filename;
			public Bitmap Thumbnail;
		}

		private List<StateInfo> _stateList = new List<StateInfo>();

		private int? AddStateToList(string filename)
		{
			using (var file = new HawkFile(filename))
			{
				if (!file.Exists)
				{
					return null;
				}


				var bl = BinaryStateLoader.LoadAndDetect(filename);
				if (bl == null)
					return null;

				BitmapBuffer bb = null;
				if(!bl.GetLump(BinaryStateLump.Framebuffer, false, (br) => 
					QuickBmpFile.Load(out bb, br.BaseStream)))
					return null;
				
				if (bb == null)
					return null;

				Bitmap thumbnail = bb.ToSysdrawingBitmap();

				StateInfo state = new StateInfo()
				{
					Filename = Path.GetFileNameWithoutExtension(filename),
					Thumbnail = thumbnail
				};

				int? index;
				lock (_stateList)
				{
					_stateList.Add(state);
					index = _stateList.Count - 1;
				}

				_sortReverse = false;
				_sortedCol = string.Empty;

				return index;
			}

		}

		private void ScanFiles()
		{
			_stateList.Clear();
			//lvStates.ItemCount = 0;
			//lvStates.Update();

			var directory = Path.GetDirectoryName(PathManager.SaveStatePrefix(Global.Game));
			if (!Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			var dpTodo = new Queue<string>();
			var fpTodo = new List<string>();
			dpTodo.Enqueue(directory);
			Dictionary<string, int> ordinals = new Dictionary<string, int>();

			while (dpTodo.Count > 0)
			{
				string dp = dpTodo.Dequeue();

				//enqueue subdirectories if appropriate
				//if (Global.Config.PlayMovie_IncludeSubdir)
					foreach (var subdir in Directory.GetDirectories(dp))
						dpTodo.Enqueue(subdir);

				//add movies
				fpTodo.AddRange(Directory.GetFiles(dp, "*.state"));
			}

			//in parallel, scan each movie
			Parallel.For(0, fpTodo.Count, (i) =>
			//for(int i=0;i<fpTodo.Count;i++)
			{
				var file = fpTodo[i];
				lock (ordinals) ordinals[file] = i;
				AddStateToList(file);
			}
			);

			//doesn't work right now. cant rememebr what it was all about anyway
			//sort by the ordinal key to maintain relatively stable results when rescanning
			//_stateList.Sort((a, b) => ordinals[a.Filename].CompareTo(ordinals[b.Filename]));

			SortBy("File");

			RefreshStateList();
		}

		void RefreshStateList()
		{
			//lvStates.ItemCount = _stateList.Count;
			lvStates.BeginUpdate();
			lvStates.Items.Clear();
			lvStates.LargeImageList = imageList1;
			foreach (var img in imageList1.Images)
				((Image)img).Dispose();
			imageList1.Images.Clear();
			int maxw = 32;
			int maxh = 32;
			foreach (var item in _stateList)
			{
				var lvi = new ListViewItem();
				lvi.Text = item.Filename;
				lvi.ImageIndex = imageList1.Images.Count;
				imageList1.Images.Add(item.Thumbnail);
				lvStates.Items.Add(lvi);
				maxw = Math.Max(maxw, item.Thumbnail.Width);
				maxh = Math.Max(maxh, item.Thumbnail.Height);
			}

			//actually, lets hardcode it to something reasonable for now
			maxw = 64;
			maxh = 64;
			//TODO - letterbox the images

			imageList1.ImageSize = new System.Drawing.Size(maxw, maxh);
			imageList1.ColorDepth = ColorDepth.Depth24Bit;
			lvStates.EndUpdate();
			UpdateList();
		}

		private void UpdateList()
		{
			lvStates.Refresh();
			//MovieCount.Text = _movieList.Count + " movie" + (_movieList.Count != 1 ? "s" : string.Empty);
		}

		private void lvStates_ColumnClick(object sender, ColumnClickEventArgs e)
		{
			SortBy(lvStates.Columns[e.Column].Text);
		}

		void SortBy(string columnName)
		{
			if (_sortedCol != columnName)
			{
				_sortReverse = false;
			}

			switch (columnName)
			{
				case "File":
					if (_sortReverse)
					{
						_stateList = _stateList
							.OrderByDescending(x => Path.GetFileName(x.Filename))
							.ToList();
					}
					else
					{
						_stateList = _stateList
							.OrderBy(x => Path.GetFileName(x.Filename))
							.ToList();
					}
					break;
			}

			_sortedCol = columnName;
			_sortReverse = !_sortReverse;
			lvStates.Refresh();
		}

		private void button1_Click(object sender, EventArgs e)
		{
			RefreshStateList();
		}
	}
}
