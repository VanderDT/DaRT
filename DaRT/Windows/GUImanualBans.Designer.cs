namespace DaRT
{
    partial class GUImanualBans
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GUImanualBans));
            this.ban = new System.Windows.Forms.Button();
            this.abort = new System.Windows.Forms.Button();
            this.input = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // ban
            // 
            this.ban.Location = new System.Drawing.Point(221, 249);
            this.ban.Name = "ban";
            this.ban.Size = new System.Drawing.Size(75, 23);
            this.ban.TabIndex = 9;
            this.ban.Text = global::DaRT.Resources.Strings.Ban;
            this.ban.UseVisualStyleBackColor = true;
            this.ban.Click += new System.EventHandler(this.ban_Click);
            // 
            // abort
            // 
            this.abort.Location = new System.Drawing.Point(302, 249);
            this.abort.Name = "abort";
            this.abort.Size = new System.Drawing.Size(75, 23);
            this.abort.TabIndex = 10;
            this.abort.Text = Resources.Strings.Cancel;
            this.abort.UseVisualStyleBackColor = true;
            this.abort.Click += new System.EventHandler(this.abort_Click);
            // 
            // input
            // 
            this.input.Location = new System.Drawing.Point(12, 12);
            this.input.Name = "input";
            this.input.Size = new System.Drawing.Size(566, 231);
            this.input.TabIndex = 11;
            this.input.Text = "";
            this.input.Enter += new System.EventHandler(this.input_Enter);
            // 
            // GUImanualBans
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(590, 284);
            this.Controls.Add(this.input);
            this.Controls.Add(this.abort);
            this.Controls.Add(this.ban);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "GUImanualBans";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = Resources.Strings.Bans+" GUIDS";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button ban;
        private System.Windows.Forms.Button abort;
        private System.Windows.Forms.RichTextBox input;
    }
}