using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Mono.Data.Sqlite;

namespace DaRT
{
    public partial class GUIcomment : Form
    {
        private GUImain gui = null;
        private String guid = "";
        private String mode = "";

        public GUIcomment()
        {
            InitializeComponent();
        }
        public void Comment(GUImain gui, String name, String guid, String mode)
        {
            this.gui = gui;
            this.Text = Resources.Strings.Comment + " " + name;
            this.guid = guid;
            this.mode = mode;
            input.Text = gui.GetComment(guid);

        }

        private void apply_Click(object sender, EventArgs e)
        {
            gui.SetComment(guid, input.Text);

            if (mode == "players")
            {
                Thread thread = new Thread(new ThreadStart(gui.thread_Player));
                thread.IsBackground = true;
                thread.Start();
            }
            else if (mode == "bans")
            {
                Thread thread = new Thread(new ThreadStart(gui.thread_Bans));
                thread.IsBackground = true;
                thread.Start();
            }
            else if (mode == "player database")
            {
                Thread thread = new Thread(new ThreadStart(gui.thread_Database));
                thread.IsBackground = true;
                thread.Start();
            }

            this.Close();
        }

        private void GUIcomment_FormClosing(object sender, FormClosingEventArgs e)
        {
            gui.Invoke((MethodInvoker)delegate
            {
                gui.Enabled = true;
            });
        }
    }
}
