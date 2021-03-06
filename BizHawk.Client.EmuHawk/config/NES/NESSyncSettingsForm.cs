﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using BizHawk.Client.Common;
using BizHawk.Emulation.Cores.Nintendo.NES;

namespace BizHawk.Client.EmuHawk
{
	public partial class NESSyncSettingsForm : Form
	{
		DataTableDictionaryBind<string, string> DTDB;
		NES.NESSyncSettings SyncSettings;

		public NESSyncSettingsForm()
		{
			InitializeComponent();

			SyncSettings = ((NES)Global.Emulator).GetSyncSettings();

			if ((Global.Emulator as NES).HasMapperProperties)
			{
				
				DTDB = new DataTableDictionaryBind<string, string>(SyncSettings.BoardProperties);
				dataGridView1.DataSource = DTDB.Table;
				InfoLabel.Visible = false;
			}
			else
			{
				BoardPropertiesGroupBox.Enabled = false;
				dataGridView1.DataSource = null;
				dataGridView1.Enabled = false;
				InfoLabel.Visible = true;
			}

			RegionComboBox.Items.AddRange(Enum.GetNames(typeof(NES.NESSyncSettings.Region)));
			RegionComboBox.SelectedItem = Enum.GetName(typeof(NES.NESSyncSettings.Region), SyncSettings.RegionOverride);
		}

		private void CancelBtn_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			Close();
		}

		private void OkBtn_Click(object sender, EventArgs e)
		{

			var old = SyncSettings.RegionOverride;
			SyncSettings.RegionOverride = (NES.NESSyncSettings.Region)
				Enum.Parse(
				typeof(NES.NESSyncSettings.Region),
				(string)RegionComboBox.SelectedItem);

			bool changed = (DTDB != null && DTDB.WasModified) ||
				old != SyncSettings.RegionOverride;

			DialogResult = DialogResult.OK;
			if (changed)
			{
				GlobalWin.MainForm.PutCoreSyncSettings(SyncSettings);
			}
		}

		private void HelpBtn_Click(object sender, EventArgs e)
		{
			MessageBox.Show(
				this,
				"Board Properties are special per-mapper system settings.  They are only useful to advanced users creating Tool Assisted Superplays.  No support will be provided if you break something with them.",
				"Help",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information);
		}

		private void NESSyncSettingsForm_Load(object sender, EventArgs e)
		{

		}
	}
}
