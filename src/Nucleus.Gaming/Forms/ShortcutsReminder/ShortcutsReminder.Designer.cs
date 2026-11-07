namespace Nucleus.Coop.Forms
{
    partial class ShortcutsReminder
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
            this.gamepadImg = new System.Windows.Forms.PictureBox();
            this.kbImg = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.gamepadImg)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.kbImg)).BeginInit();
            this.SuspendLayout();
            // 
            // gamepadImg
            // 
            this.gamepadImg.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.gamepadImg.Location = new System.Drawing.Point(0, 0);
            this.gamepadImg.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.gamepadImg.Name = "gamepadImg";
            this.gamepadImg.Size = new System.Drawing.Size(277, 453);
            this.gamepadImg.TabIndex = 0;
            this.gamepadImg.TabStop = false;
            // 
            // kbImg
            // 
            this.kbImg.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.kbImg.Location = new System.Drawing.Point(285, 0);
            this.kbImg.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.kbImg.Name = "kbImg";
            this.kbImg.Size = new System.Drawing.Size(343, 283);
            this.kbImg.TabIndex = 1;
            this.kbImg.TabStop = false;
            // 
            // ShortcutsReminder
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.BackColor = System.Drawing.Color.Black;
            this.ClientSize = new System.Drawing.Size(629, 454);
            this.ControlBox = false;
            this.Controls.Add(this.kbImg);
            this.Controls.Add(this.gamepadImg);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "ShortcutsReminder";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Shortcuts Reminder";
            this.TopMost = true;
            ((System.ComponentModel.ISupportInitialize)(this.gamepadImg)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.kbImg)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox gamepadImg;
        private System.Windows.Forms.PictureBox kbImg;
    }
}