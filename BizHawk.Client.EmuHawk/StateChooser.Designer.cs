namespace BizHawk.Client.EmuHawk
{
	partial class StateChooser
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.components = new System.ComponentModel.Container();
			this.lvStates = new System.Windows.Forms.ListView();
			this.colFile = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
			this.imageList1 = new System.Windows.Forms.ImageList(this.components);
			this.button1 = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// lvStates
			// 
			this.lvStates.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.colFile});
			this.lvStates.Location = new System.Drawing.Point(12, 12);
			this.lvStates.Name = "lvStates";
			this.lvStates.Size = new System.Drawing.Size(444, 350);
			this.lvStates.TabIndex = 0;
			this.lvStates.UseCompatibleStateImageBehavior = false;
			this.lvStates.View = System.Windows.Forms.View.Tile;
			// 
			// colFile
			// 
			this.colFile.Text = "File";
			// 
			// imageList1
			// 
			this.imageList1.ColorDepth = System.Windows.Forms.ColorDepth.Depth8Bit;
			this.imageList1.ImageSize = new System.Drawing.Size(16, 16);
			this.imageList1.TransparentColor = System.Drawing.Color.Transparent;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(611, 130);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(75, 23);
			this.button1.TabIndex = 1;
			this.button1.Text = "button1";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// StateChooser
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(780, 431);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.lvStates);
			this.Name = "StateChooser";
			this.Text = "Load State";
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.ListView lvStates;
		private System.Windows.Forms.ColumnHeader colFile;
		private System.Windows.Forms.ImageList imageList1;
		private System.Windows.Forms.Button button1;

	}
}