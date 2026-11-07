namespace Nucleus.Gaming.Controls
{
    partial class CustomNumericUpDown
    {
        /// <summary> 
        /// Variable nécessaire au concepteur.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Nettoyage des ressources utilisées.
        /// </summary>
        /// <param name="disposing">true si les ressources managées doivent être supprimées ; sinon, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Code généré par le Concepteur de composants

        /// <summary> 
        /// Méthode requise pour la prise en charge du concepteur - ne modifiez pas 
        /// le contenu de cette méthode avec l'éditeur de code.
        /// </summary>
        private void InitializeComponent()
        {
            this.val = new System.Windows.Forms.Label();
            this.down = new System.Windows.Forms.Button();
            this.up = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // val
            // 
            this.val.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.val.BackColor = System.Drawing.Color.MidnightBlue;
            this.val.ForeColor = System.Drawing.Color.White;
            this.val.Location = new System.Drawing.Point(0, 0);
            this.val.Margin = new System.Windows.Forms.Padding(0);
            this.val.Name = "val";
            this.val.Size = new System.Drawing.Size(38, 20);
            this.val.TabIndex = 2;
            this.val.Text = "0";
            this.val.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.val.TextChanged += new System.EventHandler(this.val_TextChanged);
            // 
            // down
            // 
            this.down.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.down.BackColor = System.Drawing.Color.Chartreuse;
            this.down.BackgroundImage = global::Nucleus.Gaming.Properties.Resources.title_dropdown_closed;
            this.down.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.down.FlatAppearance.BorderSize = 0;
            this.down.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.down.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.down.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.down.Location = new System.Drawing.Point(38, 10);
            this.down.Margin = new System.Windows.Forms.Padding(0);
            this.down.Name = "down";
            this.down.Size = new System.Drawing.Size(10, 10);
            this.down.TabIndex = 1;
            this.down.UseVisualStyleBackColor = false;
            this.down.Click += new System.EventHandler(this.down_Click);
            // 
            // up
            // 
            this.up.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.up.BackColor = System.Drawing.Color.Chartreuse;
            this.up.BackgroundImage = global::Nucleus.Gaming.Properties.Resources.title_dropdown_opened;
            this.up.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.up.FlatAppearance.BorderSize = 0;
            this.up.FlatAppearance.MouseDownBackColor = System.Drawing.Color.Transparent;
            this.up.FlatAppearance.MouseOverBackColor = System.Drawing.Color.Transparent;
            this.up.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.up.Location = new System.Drawing.Point(38, 0);
            this.up.Margin = new System.Windows.Forms.Padding(0);
            this.up.Name = "up";
            this.up.Size = new System.Drawing.Size(10, 10);
            this.up.TabIndex = 0;
            this.up.Text = "up";
            this.up.UseVisualStyleBackColor = false;
            this.up.Click += new System.EventHandler(this.up_Click);
            // 
            // CustomNumericUpDown
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(128)))));
            this.Controls.Add(this.val);
            this.Controls.Add(this.down);
            this.Controls.Add(this.up);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(2, 3, 2, 3);
            this.Name = "CustomNumericUpDown";
            this.Size = new System.Drawing.Size(48, 20);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button up;
        private System.Windows.Forms.Button down;
        private System.Windows.Forms.Label val;
    }
}
