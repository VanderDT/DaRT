using System;
using System.Collections.Generic;
using System.Data;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using DaRT.Properties;
using System.Net.Sockets;

namespace DaRT
{
    public partial class GUImain : Form
    {
        #region Initialize
        public GUImain(String version)
        {
            
            // Initializing DaRT
            Thread.CurrentThread.CurrentCulture = new CultureInfo(Settings.Default.Language);
            this.version = version;

            // Upgrading configuration
            if (Settings.Default.UpgradeConfig)
            {
                Settings.Default.Upgrade();
                Settings.Default.UpgradeConfig = false;
            }

            // Initializing RCon class
            rcon = new RCon(this);

            _buffer = new List<string>();

            InitializeComponent();
        }

        // Public variables
        public String version;
        public RCon rcon;
        private ToolTip tooltip = new ToolTip();
        private List<Player> players = new List<Player>();
        private List<Location> locations = new List<Location>();
        private SqliteConnection connection;
        private MySqlConnection remoteconnection;
        private SqliteCommand command;
        private MySqlCommand remotecommand;
        private ContextMenuStrip playerContextMenu;
        private ContextMenuStrip bansContextMenu;
        private ContextMenuStrip playerDBContextMenu;
        private ContextMenuStrip executeContextMenu;
        private ListViewColumnSorter playerSorter;
        private ListViewColumnSorter banSorter;
        private ListViewColumnSorter playerDatabaseSorter;
        public uint refreshTimer = 0;
        bool menuOpened = false;
        StreamWriter consoleWriter;
        StreamWriter chatWriter;
        StreamWriter logWriter;
        public IWebProxy proxy;
        bool pendingConnect = false;
        public bool pendingPlayers = false;
        public bool pendingAdmins = false;
        bool pendingBans = false;
        bool pendingDatabase = false;

        private List<string> _buffer;

        private void InitializeSplitter()
        {
            // Setting splitter size
            //splitContainer2.SplitterDistance = Settings.Default.splitter;
            splitContainer2.SplitterDistance = 330;
        }
        private void InitializeText()
        {
            // Bringing text at the top to the front because it overlaps with the tab control
            news.BringToFront();
            playerCounter.BringToFront();
            adminCounter.BringToFront();
            banCounter.BringToFront();
            setAdminCount(0);
            setPlayerCount(0);
            setBanCount(0);

        }
        private void InitializeDatabase()
        {
            try
            {
                connection = new SqliteConnection(@"Data Source=data\db\dart.db");
                connection.Open();

                using (command = new SqliteCommand("CREATE TABLE IF NOT EXISTS players (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, lastip VARCHAR(100) NOT NULL, lastseen VARCHAR(100) NOT NULL, guid VARCHAR(32) NOT NULL, name VARCHAR(100) NOT NULL, lastseenon VARCHAR(20), location VARCHAR(2), synced INTEGER)", connection))
                {
                    command.ExecuteNonQuery();
                }
                using (command = new SqliteCommand("CREATE TABLE IF NOT EXISTS bans (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, guid VARCHAR(32) NOT NULL, date VARCHAR(20), reason VARCHAR(100), host VARCHAR(20) NOT NULL, synced INTEGER)", connection))
                {
                    command.ExecuteNonQuery();
                }
                using (command = new SqliteCommand("CREATE TABLE IF NOT EXISTS comments (guid VARCHAR(32) NOT NULL PRIMARY KEY, comment VARCHAR(256) NOT NULL, date VARCHAR(20))", connection))
                {
                    command.ExecuteNonQuery();
                }
                using (command = new SqliteCommand("CREATE TABLE IF NOT EXISTS hosts (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, host VARCHAR(100) NOT NULL, port VARCHAR(32) NOT NULL, password VARCHAR(100) NOT NULL)", connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void InitializeRemoteBase()
        {
            try
            {
                remoteconnection = new MySqlConnection(String.Format("Database={1};Data Source={0};User Id={2};Password={3}", Settings.Default.dbHost,Settings.Default.dbBase,Settings.Default.dbUser,Settings.Default.dbPassword));
                remoteconnection.Open();
                using (remotecommand = new MySqlCommand("CREATE TABLE IF NOT EXISTS players (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, lastip VARCHAR(100) NOT NULL, lastseen DATETIME NOT NULL, guid VARCHAR(32) NOT NULL, name VARCHAR(100) NOT NULL, lastseenon VARCHAR(20), location VARCHAR(2))", remoteconnection))
                {
                    remotecommand.ExecuteNonQuery();
                }
                using (remotecommand = new MySqlCommand("CREATE TABLE IF NOT EXISTS bans (id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, guid VARCHAR(32) NOT NULL, date DATETIME, reason VARCHAR(100), host VARCHAR(20) NOT NULL)", remoteconnection))
                {
                    remotecommand.ExecuteNonQuery();
                }
                using (remotecommand = new MySqlCommand("CREATE TABLE IF NOT EXISTS comments (guid VARCHAR(32) NOT NULL PRIMARY KEY, comment VARCHAR(256) NOT NULL, date DATETIME)", remoteconnection))
                {
                    remotecommand.ExecuteNonQuery();
                }

            }
            catch (Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void UpdateDatabase()
        {
            try
            {
                if (File.Exists("data/db/players.db"))
                {
                    File.Move("data/db/players.db", "data/db/players_old.db");
                    using (SqliteConnection old = new SqliteConnection(@"Data Source=data\db\players_old.db"))
                    {
                        old.Open();

                        DataTable table = new DataTable("players");
                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM players", old);
                            adapter.Fill(table);
                        }

                        foreach (DataRow row in table.Rows)
                        {
                            row.SetAdded();
                        }

                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM players", connection);
                            adapter.InsertCommand = new SqliteCommandBuilder(adapter).GetInsertCommand(true);
                            adapter.Update(table);
                        }
                    }
                }
                if (File.Exists("data/db/hosts.db"))
                {
                    File.Move("data/db/hosts.db", "data/db/hosts_old.db");
                    using (SqliteConnection old = new SqliteConnection(@"Data Source=data\db\hosts_old.db"))
                    {
                        old.Open();

                        DataTable table = new DataTable("hosts");
                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM hosts", old);
                            adapter.Fill(table);
                        }

                        foreach (DataRow row in table.Rows)
                        {
                            row.SetAdded();
                        }

                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM hosts", connection);
                            adapter.InsertCommand = new SqliteCommandBuilder(adapter).GetInsertCommand(true);
                            adapter.Update(table);
                        }
                    }
                }
                if (File.Exists("data/db/comments.db"))
                {
                    File.Move("data/db/comments.db", "data/db/comments_old.db");
                    using (SqliteConnection old = new SqliteConnection(@"Data Source=data\db\comments_old.db"))
                    {
                        old.Open();

                        DataTable table = new DataTable("comments");
                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM comments", old);
                            adapter.Fill(table);
                        }

                        foreach (DataRow row in table.Rows)
                        {
                            row.SetAdded();
                        }

                        using (SqliteDataAdapter adapter = new SqliteDataAdapter())
                        {
                            adapter.SelectCommand = new SqliteCommand("SELECT * FROM comments", connection);
                            adapter.InsertCommand = new SqliteCommandBuilder(adapter).GetInsertCommand(true);
                            adapter.Update(table);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                using (StreamWriter writer = new StreamWriter("migrate_error.txt"))
                {
                    writer.WriteLine(e.Message);
                    writer.WriteLine(e.StackTrace);
                }
            }
        }
        private void InitializeFields()
        {
            // Initializing fields with last used values
            host.Text = Settings.Default.host;
            port.Text = Settings.Default.port;
            password.Text = Settings.Default.password;
            autoRefresh.Checked = Settings.Default.refresh;
        }
        private void InitializeBox()
        {
            // Initializing filter box
            options.SelectedIndex = 0;

            filter.Items.Add(Resources.Strings.Name);
            filter.Items.Add("GUID");
            filter.Items.Add("IP");
            filter.Items.Add(Resources.Strings.Comment);
            filter.SelectedIndex = 0;
        }
        private void InitializePlayerList()
        {
            // Initializing context menu of player database
            playerContextMenu = new ContextMenuStrip();
            playerContextMenu.Items.Add(Resources.Strings.Copy);
            (playerContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add(Resources.Strings.All, null, playerCopyAll_click);
            (playerContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add("IP", null, playerCopyIP_click);
            (playerContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add("GUID", null, playerCopyGUID_click);
            (playerContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add(Resources.Strings.Name, null, playerCopyName_click);
            playerContextMenu.Items.Add(Resources.Strings.Comment, null, comment_click);
            playerContextMenu.Items.Add("-");
            playerContextMenu.Items.Add(Resources.Strings.Message, null, say_click);
            playerContextMenu.Items.Add(Resources.Strings.Kick, null, kick_click);
            playerContextMenu.Items.Add(Resources.Strings.Ban, null, ban_click);
            playerContextMenu.Items.Add(Resources.Strings.Quick_Ban, null, quickBan_click);

            // Attaching event handlers to detect state of context menu
            playerContextMenu.Opened += new System.EventHandler(this.menu_Opened);
            playerContextMenu.Closed += new System.Windows.Forms.ToolStripDropDownClosedEventHandler(this.menu_Closed);

            // Adding the columns to the player list
            playerList.Columns.Add("", 45);
            playerList.Columns.Add("#", 30);
            playerList.Columns.Add("IP", 150);
            playerList.Columns.Add(Resources.Strings.Ping, 50);
            playerList.Columns.Add("GUID", 250);
            playerList.Columns.Add(Resources.Strings.Name, 180);
            playerList.Columns.Add(Resources.Strings.Status, 100);
            playerList.Columns.Add(Resources.Strings.Comment, 210);

            // Initializing the player sorter used to sort the player list
            playerSorter = new ListViewColumnSorter();
            this.playerList.ListViewItemSorter = playerSorter;

            // Load order and sizes from config
            String[] order = Settings.Default.playerOrder.Split(';');
            String[] sizes = Settings.Default.playerSizes.Split(';');

            for (int i = 0; i < playerList.Columns.Count; i++)
            {
                playerList.Columns[i].DisplayIndex = Int32.Parse(order[i]);
                playerList.Columns[i].Width = Int32.Parse(sizes[i]);
            }
        }
        private void InitializeBansList()
        {
            // Initializing context menu of ban list
            bansContextMenu = new ContextMenuStrip();
            bansContextMenu.Items.Add(Resources.Strings.Copy, null, bansCopyGUIDIP_click);
            bansContextMenu.Items.Add(Resources.Strings.Comment, null, comment_click);
            bansContextMenu.Items.Add(Resources.Strings.Unban, null, unban_click);
            bansContextMenu.Items.Add("-");
            bansContextMenu.Items.Add(String.Format("{0} {1} {2}",Resources.Strings.Delete,Resources.Strings.Expired, Resources.Strings.Bans), null, expired_click);
            bansContextMenu.Items.Add(String.Format("{0} {1} {2}", Resources.Strings.Copy, Resources.Strings.All, Resources.Strings.Bans), null, copyall_click);

            // Attaching event handlers to detect state of context menu
            bansContextMenu.Opened += new System.EventHandler(this.menu_Opened);
            bansContextMenu.Closed += new System.Windows.Forms.ToolStripDropDownClosedEventHandler(this.menu_Closed);

            // Adding the columns to the ban list
            bansList.Columns.Add("#", 50);
            bansList.Columns.Add("GUID/IP", 250);
            bansList.Columns.Add(Resources.Strings.Ban_duration, 100);
            bansList.Columns.Add(Resources.Strings.Reason, 300);
            bansList.Columns.Add(Resources.Strings.Comment, 315);

            // Initializing the ban sorter used to sort the ban list
            banSorter = new ListViewColumnSorter();
            this.bansList.ListViewItemSorter = banSorter;
        }
        private void InitializePlayerDBList()
        {
            // Initializing the context menu for the player database
            playerDBContextMenu = new ContextMenuStrip();
            playerDBContextMenu.Items.Add(Resources.Strings.Copy);
            (playerDBContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add(Resources.Strings.All, null, playerDBCopyAll_click);
            (playerDBContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add("IP", null, playerDBCopyIP_click);
            (playerDBContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add("GUID", null, playerDBCopyGUID_click);
            (playerDBContextMenu.Items[0] as ToolStripMenuItem).DropDownItems.Add(Resources.Strings.Name, null, playerDBCopyName_click);
            playerDBContextMenu.Items.Add("-");
            playerDBContextMenu.Items.Add(Resources.Strings.Ban, null, banOffline_click);
            playerDBContextMenu.Items.Add(Resources.Strings.Comment, null, comment_click);
            playerDBContextMenu.Items.Add("-");
            playerDBContextMenu.Items.Add(Resources.Strings.Delete, null, delete_click);
            playerDBContextMenu.Opened += new System.EventHandler(this.menu_Opened);
            playerDBContextMenu.Closed += new System.Windows.Forms.ToolStripDropDownClosedEventHandler(this.menu_Closed);

            // Adding the columns to the player database
            playerDBList.Columns.Add("#", 50);
            playerDBList.Columns.Add("IP", 150);
            playerDBList.Columns.Add(Resources.Strings.Date, 150);
            playerDBList.Columns.Add("GUID", 250);
            playerDBList.Columns.Add(Resources.Strings.Name, 200);
            playerDBList.Columns.Add(Resources.Strings.Host, 95);
            playerDBList.Columns.Add(Resources.Strings.Comment, 120);

            // Initializing the sorter for the player database
            playerDatabaseSorter = new ListViewColumnSorter();
            this.playerDBList.ListViewItemSorter = playerDatabaseSorter;
        }
        private void InitializeFunctions()
        {
            // Initializing context menu of Execute button
            executeContextMenu = new ContextMenuStrip();
            executeContextMenu.Items.Add(Resources.Strings.Rel_script, null, reloadScripts_Click);
            executeContextMenu.Items.Add(Resources.Strings.Rel_bans, null, reloadBans_Click);
            executeContextMenu.Items.Add(Resources.Strings.Rel_events, null, reloadEvents_Click);
            executeContextMenu.Items.Add("-");
            executeContextMenu.Items.Add(Resources.Strings.Lock_server, null, lock_Click);
            executeContextMenu.Items.Add(Resources.Strings.Unlock_server, null, unlock_Click);
            executeContextMenu.Items.Add(Resources.Strings.Shutdown, null, shutdown_Click);
            executeContextMenu.Items.Add("-");
            //executeContextMenu.Items.Add("Manually add a ban", null, addBan_Click);
            executeContextMenu.Items.Add(Resources.Strings.Multiban, null, addBans_Click);
        }
        private void InitializeConsole()
        {
            // Initializing context menus of consoles
            all.ContextMenuStrip = new ContextMenuStrip();
            all.ContextMenuStrip.Items.Add(Resources.Strings.Copy, null, copy_click);
            all.ContextMenuStrip.Items.Add("-");
            all.ContextMenuStrip.Items.Add(Resources.Strings.Clear, null, clear_click);
            all.ContextMenuStrip.Items.Add(Resources.Strings.Clear_all, null, clearAll_click);
            console.ContextMenuStrip = new ContextMenuStrip();
            console.ContextMenuStrip.Items.Add(Resources.Strings.Copy, null, copy_click);
            console.ContextMenuStrip.Items.Add("-");
            console.ContextMenuStrip.Items.Add(Resources.Strings.Clear, null, clear_click);
            console.ContextMenuStrip.Items.Add(Resources.Strings.Clear_all, null, clearAll_click);
            chat.ContextMenuStrip = new ContextMenuStrip();
            chat.ContextMenuStrip.Items.Add(Resources.Strings.Copy, null, copy_click);
            chat.ContextMenuStrip.Items.Add("-");
            chat.ContextMenuStrip.Items.Add(Resources.Strings.Clear, null, clear_click);
            chat.ContextMenuStrip.Items.Add(Resources.Strings.Clear_all, null, clearAll_click);
            logs.ContextMenuStrip = new ContextMenuStrip();
            logs.ContextMenuStrip.Items.Add(Resources.Strings.Copy, null, copy_click);
            logs.ContextMenuStrip.Items.Add("-");
            logs.ContextMenuStrip.Items.Add(Resources.Strings.Clear, null, clear_click);
            logs.ContextMenuStrip.Items.Add(Resources.Strings.Clear_all, null, clearAll_click);
        }
        private void InitializeBanner()
        {
            // Setting the image in the lower right corner
            banner.Image = GetImage(Resources.Strings.Error_nocon);
        }
        public void InitializeNews()
        {
            // Requesting the news
            Thread thread = new Thread(new ThreadStart(thread_News));
            thread.IsBackground = true;
            thread.Start();
        }
        private void InitializeProxy()
        {
            // Getting default proxy
            proxy = HttpWebRequest.DefaultWebProxy;
        }
        private void InitializeProgressBar()
        {
            // Setting progressbar to interval
            nextRefresh.Maximum = (int)Settings.Default.interval;
        }
        private void InitializeFonts()
        {
            all.Font = Settings.Default.font;
            console.Font = Settings.Default.font;
            chat.Font = Settings.Default.font;
            logs.Font = Settings.Default.font;
        }
        private void InitializeTooltips()
        {
            tooltip.AutoPopDelay = 30000;

            tooltip.SetToolTip(connect, Resources.Strings.Tip_connect);
            tooltip.SetToolTip(disconnect, Resources.Strings.Tip_disconnect);
            tooltip.SetToolTip(refresh, Resources.Strings.Tip_refr);
            tooltip.SetToolTip(hosts, Resources.Strings.Tip_host);
            tooltip.SetToolTip(settings, Resources.Strings.Tip_settings);
            tooltip.SetToolTip(banner, Resources.Strings.Tip_banner);
            tooltip.SetToolTip(options, Resources.Strings.Tip_say);
            tooltip.SetToolTip(execute, Resources.Strings.Tip_tools);
            tooltip.SetToolTip(autoRefresh, Resources.Strings.Tip_autorefr);
            tooltip.SetToolTip(allowMessages, Resources.Strings.Tip_message);
        }
        #endregion

        #region Button Listeners
        private void connect_Click(object sender, EventArgs args)
        {
            if (!pendingConnect)
            {
                Thread thread = new Thread(new ThreadStart(thread_Connect));
                thread.IsBackground = true;
                thread.Start();
            }
            else
                this.Log("DaRT is already connecting. Please wait for it to finish before connecting again.", LogType.Console, false);
        }
        private void refresh_Click(object sender, EventArgs args)
        {
            if (!(tabControl.SelectedTab.Name == "playerdatabaseTab"))
            {
                if (tabControl.SelectedTab.Name == "playersTab")
                {
                    // Refresh if BattleNET is connected and no request is pending
                    if (rcon.Connected && !pendingPlayers)
                    {
                        Thread thread = new Thread(new ThreadStart(thread_Player));
                        thread.IsBackground = true;
                        thread.Start();
                        Thread banner = new Thread(new ThreadStart(thread_Banner));
                        banner.IsBackground = true;
                        banner.Start();
                    }
                    else
                    {
                        this.Log("There already is a pending player list request, please wait for it to finish!", LogType.Console, false);
                    }
                }
                else if (tabControl.SelectedTab.Name == "bansTab")
                {
                    // Refresh bans if no other request is pending
                    if (!pendingBans)
                    {
                        Thread thread = new Thread(new ThreadStart(thread_Bans));
                        thread.IsBackground = true;
                        thread.Start();
                        Thread banner = new Thread(new ThreadStart(thread_Banner));
                        banner.IsBackground = true;
                        banner.Start();
                    }
                    else
                    {
                        this.Log("There already is a pending ban list request, please wait for it to finish!", LogType.Console, false);
                    }
                }
            }
            else
            {
                // Refresh player database
                if (!pendingDatabase)
                {
                    Thread thread = new Thread(new ThreadStart(thread_Database));
                    thread.IsBackground = true;
                    thread.Start();
                }
            }
        }
        private void disconnect_Click(object sender, EventArgs args)
        {
            // Setting state to disconnected
            disconnect.Enabled = false;
            connect.Enabled = true;
            refresh.Enabled = false;
            input.Enabled = false;
            refresh.Enabled = false;
            host.Enabled = true;
            port.Enabled = true;
            password.Enabled = true;
            execute.Enabled = false;
            hosts.Enabled = true;

            // Disconnect, clear lists and reset everything
            rcon.Disconnect();
            rcon.Reconnecting = false;
            playerList.Items.Clear();
            bansList.Items.Clear();
            nextRefresh.Value = 0;
            setPlayerCount(0);
            setAdminCount(0);
            setBanCount(0);
            banner.Image = GetImage(Resources.Strings.Error_nocon);

            this.Log("Disconnected.", LogType.Console, false);

            timer.Stop();
            seconds = 0;
            minutes = 0;
            hours = 0;
            refreshTimer = 0;
            lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_s,0);
        }
        private void reloadScripts_Click(object sender, EventArgs args)
        {
            // Reloads the scripts.txt
            rcon.scripts();
        }
        #endregion

        #region Context Menu Listeners
        private void comment_click(object sender, EventArgs args)
        {
            GUIcomment gui = new GUIcomment();

            try
            {
                if (tabControl.SelectedTab.Name == "playersTab")
                {
                    ListViewItem item = playerList.SelectedItems[0];
                    String name = item.SubItems[5].Text;
                    String guid = item.SubItems[4].Text;
                    gui.Comment(this, connection, name, guid, "players");
                }
                else if (tabControl.SelectedTab.Name == "bansTab")
                {
                    ListViewItem item = bansList.SelectedItems[0];
                    String name = "";
                    String guid = item.SubItems[1].Text;
                    if (guid.Length == 32)
                    {
                        gui.Comment(this, connection, name, guid, "bans");
                    }
                    else
                    {
                        this.Log(Resources.Strings.Error_ip_com, LogType.Console, false);
                        return;
                    }
                }
                else if (tabControl.SelectedTab.Name == "playerdatabaseTab")
                {
                    ListViewItem item = playerDBList.SelectedItems[0];
                    String name = item.SubItems[4].Text;
                    String guid = item.SubItems[3].Text;
                    gui.Comment(this, connection, name, guid, "player database");
                }
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
            gui.ShowDialog();
        }
        private void delete_click(object sender, EventArgs args)
        {
            // Get selected item from player database
            ListViewItem item = playerDBList.SelectedItems[0];

            // Read GUID and Name from the list
            String guid = item.SubItems[3].Text;
            String name = item.SubItems[4].Text;

            // Delete from player database...
            command = new SqliteCommand(connection);

            command.CommandText = "DELETE FROM players WHERE guid = @guid AND name = @name";
            command.Parameters.Add(new SqliteParameter("@guid", guid));
            command.Parameters.Add(new SqliteParameter("@name", name));
            command.ExecuteNonQuery();

            command.Dispose();

            playerDBList.Items.Remove(item);

            this.Log(String.Format(Resources.Strings.Deleted,name), LogType.Console, false);
        }
        private void say_click(object sender, EventArgs args)
        {
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                int id = Int32.Parse(item.SubItems[1].Text);
                String name = item.SubItems[5].Text;

                GUImessage gui = new GUImessage(this, id, name);
                gui.ShowDialog();
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void kick_click(object sender, EventArgs args)
        {

            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                int id = Int32.Parse(item.SubItems[1].Text);
                String name = item.SubItems[5].Text;
                GUIkick gui = new GUIkick(this, id, name);
                gui.ShowDialog();
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void ban_click(object sender, EventArgs args)
        {
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                int id = Int32.Parse(item.SubItems[1].Text);
                String ip = item.SubItems[2].Text;
                String guid = item.SubItems[4].Text;
                String name = item.SubItems[5].Text;
                GUIban gui = new GUIban(rcon, id, name, guid, ip, true);
                rcon.Pending = name;
                gui.ShowDialog();
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void quickBan_click(object sender, EventArgs args)
        {
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                int id = Int32.Parse(item.SubItems[1].Text);
                String ip = item.SubItems[2].Text;
                String guid = item.SubItems[4].Text;
                String name = item.SubItems[5].Text;
                
                rcon.Pending = name;
                Ban ban = new Ban(id, name, guid, "", Settings.Default.quickBan, String.Format(Resources.Strings.Quick_reason,Settings.Default.quickBan), true);
                rcon.Ban(ban);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void banOffline_click(object sender, EventArgs args)
        {
            try
            {
                ListViewItem item = playerDBList.SelectedItems[0];
                String guid = item.SubItems[3].Text;
                String name = item.SubItems[4].Text;

                GUIban gui = new GUIban(rcon, 0, name, guid, "", false);
                gui.ShowDialog();
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void unban_click(object sender, EventArgs args)
        {
            try
            {
                // Getting selected item from cache
                ListViewItem item = bansList.SelectedItems[0];
                int index = bansList.SelectedIndices[0];

                // Unbanning player with ID
                rcon.unban(item.SubItems[0].Text);

                // Removing entry from banlist
                bansList.Items.Remove(item);
                for (int i = index; i < bansList.Items.Count; i++)
                {
                    // Increasing ban ID for each ban after the removed one
                    int number = Int32.Parse(bansList.Items[i].SubItems[0].Text);
                    bansList.Items[i].SubItems[0].Text = (number - 1).ToString();
                }
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void expired_click(object sender, EventArgs args)
        {
            foreach(ListViewItem item in bansList.Items)
            {
                if (item.SubItems[2].Text.Equals("expired"))
                {
                    rcon.unban(item.SubItems[0].Text);
                    bansList.Items.Remove(item);
                }
            }
            this.Log(Resources.Strings.Expired_removed, LogType.Console, false);
        }
        private void copyall_click(object sender, EventArgs args)
        {
            String bansAll = "";
            foreach(ListViewItem item in bansList.Items)
            {
                if (!item.SubItems[2].Text.Equals("expired"))
                    bansAll += String.Format("{0} {1} {2}\r\n", item.SubItems[1].Text, (item.SubItems[2].Text.Equals("perm"))?"-1": item.SubItems[2].Text, item.SubItems[3].Text);
            }
            try
            {
                Clipboard.SetText(bansAll);
            }
            catch (Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerCopyAll_click(object sender, EventArgs args)
        {
            // Copying everything to clipboard
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                String ip = item.SubItems[2].Text;
                String guid = item.SubItems[4].Text;
                String name = item.SubItems[5].Text;
                Clipboard.SetText(ip + "    " + guid + "    " + name);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerCopyIP_click(object sender, EventArgs args)
        {
            // Copying IP to clipboard
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                String ip = item.SubItems[2].Text;
                Clipboard.SetText(ip);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerCopyGUID_click(object sender, EventArgs args)
        {
            // Copying GUID to clipboard
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                String guid = item.SubItems[4].Text;
                Clipboard.SetText(guid);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerCopyName_click(object sender, EventArgs args)
        {
            // Copying name to clipboard
            try
            {
                ListViewItem item = playerList.SelectedItems[0];
                String name = item.SubItems[5].Text;
                Clipboard.SetText(name);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void bansCopyGUIDIP_click(object sender, EventArgs args)
        {
            // Copying GUID to clipboard
            try
            {
                ListViewItem item = bansList.SelectedItems[0];
                String guid = item.SubItems[1].Text;
                Clipboard.SetText(guid);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerDBCopyAll_click(object sender, EventArgs args)
        {
            // Copying everything to clipboard
            try
            {
                ListViewItem item = playerDBList.SelectedItems[0];
                String ip = item.SubItems[1].Text;
                String guid = item.SubItems[3].Text;
                String name = item.SubItems[4].Text;
                Clipboard.SetText(ip + "    " + guid + "    " + name);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerDBCopyIP_click(object sender, EventArgs args)
        {
            // Copying IP to clipboard
            try
            {
                ListViewItem item = playerDBList.SelectedItems[0];
                String ip = item.SubItems[1].Text;
                Clipboard.SetText(ip);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerDBCopyGUID_click(object sender, EventArgs args)
        {
            // Copying GUID to clipboard
            try
            {
                ListViewItem item = playerDBList.SelectedItems[0];
                String guid = item.SubItems[3].Text;
                Clipboard.SetText(guid);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        private void playerDBCopyName_click(object sender, EventArgs args)
        {
            //Copying name to clipboard
            try
            {
                ListViewItem item = playerDBList.SelectedItems[0];
                String name = item.SubItems[4].Text;
                Clipboard.SetText(name);
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Error_occ, LogType.Debug, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        #endregion

        #region List Listeners
        private void playerList_MouseDown(object sender, MouseEventArgs args)
        {
            // Detect if right mouse button is down
            if (args.Button == MouseButtons.Right)
            {
                // If it is, show context menu on clicked position
                ListViewItem Item = default(ListViewItem);
                {
                    Item = playerList.GetItemAt(args.Location.X, args.Location.Y);
                    if ((Item != null))
                    {
                        playerContextMenu.Show(Cursor.Position);
                    }
                }
            }
        }
        private void bansList_MouseDown(object sender, MouseEventArgs args)
        {
            // Detect if right mouse button is down
            if (args.Button == MouseButtons.Right)
            {
                // If it is, open context menu at clicked position
                ListViewItem Item = default(ListViewItem);
                {
                    Item = bansList.GetItemAt(args.Location.X, args.Location.Y);
                    if ((Item != null))
                    {
                        bansContextMenu.Show(Cursor.Position);
                    }
                }
            }
        }
        private void playerDBList_MouseDown(object sender, MouseEventArgs args)
        {
            // Detect if right mouse button is down
            if (args.Button == MouseButtons.Right)
            {
                // If it is, open context menu at the clicked position and enable/disable options depending on connection state
                ListViewItem Item = default(ListViewItem);
                {
                    Item = playerDBList.GetItemAt(args.Location.X, args.Location.Y);
                    if ((Item != null))
                    {
                        if (connect.Enabled)
                        {
                            playerDBContextMenu.Items[2].Enabled = false;
                            playerDBContextMenu.Items[4].Enabled = true;
                            //playerDBContextMenu.Items[4].Enabled = false;
                        }
                        else
                        {
                            playerDBContextMenu.Items[2].Enabled = true;
                            playerDBContextMenu.Items[4].Enabled = false;
                        }
                        playerDBContextMenu.Show(Cursor.Position);
                    }
                }
            }
        }
        private void menu_Opened(object sender, EventArgs args)
        {
            // A menu is open, setting menuOpened to true
            menuOpened = true;
        }
        private void menu_Closed(object sender, EventArgs args)
        {
            // A menu was closed, setting menuOpened to false
            menuOpened = false;
        }
        #endregion

        #region System Listeners
        private void input_KeyDown(object sender, KeyEventArgs args)
        {
            // Detect if enter key is down in input field
            if (args.KeyCode == Keys.Return)
            {
                // Check if text is empty
                if (input.Text != "")
                {
                        // Say it in global chat if option is specified...
                        if (options.SelectedIndex == 0)
                        {
                            // Check if more then 3 characters were entered
                            if (input.Text.Length > 3)
                            {
                                rcon.say(input.Text);
                            }
                            else
                                this.Log("You need to enter atleast 4 characters.", LogType.Console, false);
                        }
                    else if(options.SelectedIndex == 1 && playerList.SelectedItems.Count > 0)
                    {
                        if (input.Text.Length > 3)
                        {
                            ListViewItem item = playerList.SelectedItems[0];
                            int id = Int32.Parse(item.SubItems[1].Text);
                            String name = item.SubItems[5].Text;
                            Message message = new Message(id, name, input.Text);
                            rcon.sayPrivate(message);
                        }
                        else
                            this.Log("You need to enter atleast 4 characters.", LogType.Console, false);
                    }
                        // ... or send it as command if in console mode
                        else if (options.SelectedIndex == 2)
                        {
                            if (!input.Text.StartsWith("players") && !input.Text.StartsWith("bans") && !input.Text.StartsWith("admins"))
                                rcon.execute(input.Text);
                        }
                        input.Clear();
                }
                // Setting event to handled
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
            else if (args.KeyCode == Keys.Escape)
            {
                input.Clear();
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        }
        private void banner_Click(object sender, EventArgs args)
        {
            // If connected open GameTracker homepage
            if (disconnect.Enabled)
            {
                try
                {
                    String ip = String.Format("{0}:{1}",host.Text, Int32.Parse(port.Text)+1);
                    String url = String.Format(Settings.Default.bannerUrl, ip);
                    Process.Start(url);
                }
                catch(Exception e)
                {
                    this.Log(Resources.Strings.Error_occ, LogType.Console, false);
                    this.Log(e.Message, LogType.Debug, false);
                    this.Log(e.StackTrace, LogType.Debug, false);
                }
            }
        }
        private void GUI_FormClosing(object sender, FormClosingEventArgs args)
        {
            // Disconnecting BattleNET
            if (disconnect.Enabled)
            {
                rcon.Disconnect();
            }

            try
            {
                // Calculating splitter
                int splitter;
                if (this.Height != 606)
                    splitter = ((splitContainer2.SplitterDistance * 606) / this.Height);
                else
                    splitter = splitContainer2.SplitterDistance;

                // Saving orders and sizes to config
                String order = "";
                String sizes = "";

                for (int i = 0; i < playerList.Columns.Count; i++)
                {
                    if (i == 0)
                    {
                        order = playerList.Columns[i].DisplayIndex.ToString();
                        sizes = playerList.Columns[i].Width.ToString();
                    }
                    else
                    {
                        order += ";" + playerList.Columns[i].DisplayIndex;
                        sizes += ";" + playerList.Columns[i].Width;
                    }
                }

                // Saving settings
                Settings.Default["splitter"] = splitter;
                Settings.Default["refresh"] = autoRefresh.Checked;
                Settings.Default["playerOrder"] = order;
                Settings.Default["playerSizes"] = sizes;
                Settings.Default.Save();
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }

            try
            {
                connection.Close();
                connection.Dispose();
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }

            if(Settings.Default.dbRemote) 
                try
                {
                    remoteconnection.Close();
                    remoteconnection.Dispose();
                }
                catch (Exception e)
                {
                    this.Log(e.Message, LogType.Debug, false);
                    this.Log(e.StackTrace, LogType.Debug, false);
                }

            // Closing log file writer
            if (consoleWriter != null)
            {
                consoleWriter.Close();
                consoleWriter.Dispose();
            }
            if (chatWriter != null)
            {
                chatWriter.Close();
                chatWriter.Dispose();
            }
            if (logWriter != null)
            {
                logWriter.Close();
                logWriter.Dispose();
            }
        }
        private void tabControl_SelectedIndexChanged(object sender, EventArgs args)
        {
          try
            {
                // Reset sorter
                playerSorter.Order = SortOrder.None;
                banSorter.Order = SortOrder.None;
                playerDatabaseSorter.Order = SortOrder.None;

                if (tabControl.SelectedTab.Name == "playersTab")
                {
                    // Switching everything to player tab
                    search.Text = "";
                    if (connect.Enabled)
                        refresh.Enabled = false;
                    filter.Items.Clear();
                    filter.Items.Add(Resources.Strings.Name);
                    filter.Items.Add("GUID");
                    filter.Items.Add("IP");
                    filter.Items.Add(Resources.Strings.Comment);
                    filter.SelectedIndex = 0;

                    // Adding all the player flags
                    for (int i = 0; i < players.Count; i++)
                        playerList.Items[i].Text = " " + locations[i].location.ToUpper();
                }
                else if (tabControl.SelectedTab.Name == "bansTab")
                {
                    // Switching to bans tab
                    search.Text = "";
                    if (connect.Enabled)
                        refresh.Enabled = false;
                    filter.Items.Clear();
                    filter.Items.Add("GUID/IP");
                    filter.Items.Add(Resources.Strings.Reason);
                    filter.Items.Add(Resources.Strings.Comment);
                    filter.SelectedIndex = 0;
                }
                else if (tabControl.SelectedTab.Name == "playerdatabaseTab")
                {
                    // Switching to player database tab
                    search.Text = "";
                    if (!refresh.Enabled)
                        refresh.Enabled = true;
                    filter.Items.Clear();
                    filter.Items.Add(Resources.Strings.Name);
                    filter.Items.Add("GUID");
                    filter.Items.Add("IP");
                    filter.Items.Add(Resources.Strings.Comment);
                    filter.SelectedIndex = 0;

                    List<ListViewItem> items = GetPlayersList(filter.SelectedIndex, search.Text);
                    playerDBList.Items.Clear();
                    playerDBList.Items.AddRange(items.ToArray());

                }
            }
            catch(Exception e)
            {
                this.Log("An error occurred, please try again.", LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
        }
        #endregion

        #region Threads
        private void thread_Connect()
        {
            // Connect process is pending
            pendingConnect = true;

            // Replace spaces in IP/host
            this.Invoke((MethodInvoker)delegate
            {
                host.Text = host.Text.Replace(" ", "");
            });

            IPAddress ip;
            // Checking if host is IP
            if (isIP(host.Text))
            {
                ip = IPAddress.Parse(host.Text);
            }
            else
            {
                ip = Dns.GetHostEntry(host.Text).AddressList[0];
            }
            // Checking if port is valid
            if (isPort(port.Text))
            {
                // Checking if password is not empty
                if (password.Text != "")
                {
                    // Connecting using BattleNET
                    if (Settings.Default.showConnectMessages)
                        this.Log(String.Format(Resources.Strings.Conn, host.Text, port.Text), LogType.Console, false);
                    rcon.Connect(ip, Int32.Parse(port.Text), password.Text);

                    while (!rcon.Connected && !rcon.Error)
                    {
                        // Wait for BattleNET to connect or throw an error
                    }

                    // Check if anything went wrong
                    if (rcon.Connected && !rcon.Error)
                    {
                        // Saving new settings
                        Settings.Default["host"] = host.Text;
                        Settings.Default["port"] = port.Text;
                        Settings.Default["password"] = password.Text;
                        Settings.Default.Save();

                        // Adding to host database if enabled
                        if (Settings.Default.saveHosts)
                        {
                            try
                            {
                                // Check if host is already in database
                                using (command = new SqliteCommand("SELECT * FROM hosts WHERE host = @host AND port = @port", connection))
                                {
                                    command.Parameters.Clear();
                                    command.Parameters.Add(new SqliteParameter("@host", host.Text));
                                    command.Parameters.Add(new SqliteParameter("@port", port.Text));

                                    using (SqliteDataReader reader = command.ExecuteReader())
                                    {
                                        if (!reader.Read())
                                        {
                                            reader.Close();
                                            reader.Dispose();

                                            // If not, add it
                                            using (SqliteCommand addCommand = new SqliteCommand("INSERT INTO hosts (id, host, port, password) VALUES(NULL, @host, @port, @password)", connection))
                                            {
                                                addCommand.Parameters.Clear();
                                                addCommand.Parameters.Add(new SqliteParameter("@host", host.Text));
                                                addCommand.Parameters.Add(new SqliteParameter("@port", port.Text));
                                                addCommand.Parameters.Add(new SqliteParameter("@password", password.Text));
                                                addCommand.ExecuteNonQuery();
                                            }
                                        }
                                        else
                                        {
                                            reader.Close();
                                            reader.Dispose();

                                            // If yes, update password
                                            using (SqliteCommand updateCommand = new SqliteCommand("UPDATE hosts SET password = @password WHERE host = @host AND port = @port", connection))
                                            {
                                                updateCommand.Parameters.Clear();
                                                updateCommand.Parameters.Add(new SqliteParameter("@host", host.Text));
                                                updateCommand.Parameters.Add(new SqliteParameter("@port", port.Text));
                                                updateCommand.Parameters.Add(new SqliteParameter("@password", password.Text));
                                                updateCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                this.Log(e.Message, LogType.Debug, false);
                                this.Log(e.StackTrace, LogType.Debug, false);
                            }
                        }

                        // Switching everything to connected state
                        this.Invoke((MethodInvoker)delegate
                        {
                            tabControl.SelectedIndex = 0;
                            connect.Enabled = false;
                            disconnect.Enabled = true;
                            refresh.Enabled = true;
                            input.Enabled = true;
                            execute.Enabled = true;
                            host.Enabled = false;
                            port.Enabled = false;
                            password.Enabled = false;
                            hosts.Enabled = false;
                        });

                        // Request players, bans and admins when enabled
                        if (Settings.Default.requestOnConnect && rcon.Connected)
                        {
                            Thread.Sleep(3000);
                            Thread threadPlayer = new Thread(new ThreadStart(thread_Player));
                            threadPlayer.IsBackground = true;
                            threadPlayer.Start();
                            Thread.Sleep(5000);
                            Thread threadBans = new Thread(new ThreadStart(thread_Bans));
                            threadBans.IsBackground = true;
                            threadBans.Start();
                            Thread.Sleep(5000);
                            Thread threadAdmins = new Thread(new ThreadStart(thread_Admins));
                            threadAdmins.IsBackground = true;
                            threadAdmins.Start();
                        }
                        else
                        {
                            // Starting timer
                            this.Invoke((MethodInvoker)delegate
                            {
                                lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_s, 0);
                                seconds = 0;
                                minutes = 0;
                                hours = 0;
                                timer.Start();
                            });
                        }

                        // Requesting banner from GameTracker
                        Thread threadBanner = new Thread(new ThreadStart(thread_Banner));
                        threadBanner.IsBackground = true;
                        threadBanner.Start();

                        // Setting maximum of refresh timer
                        refreshTimer = Settings.Default.interval;
                    }
                }
                else
                {
                    this.Log(Resources.Strings.Error_pass, LogType.Console, false);
                }
            }
            else
            {
                this.Log(Resources.Strings.Error_port, LogType.Console, false);
            }

            // Connect is done
            pendingConnect = false;
        }
        public void thread_Player()
        {
            try
            {
                pendingPlayers = true;

                this.Invoke((MethodInvoker)delegate
                {
                    playerContextMenu.Hide();
                    timer.Stop();
                    refreshTimer = Settings.Default.interval;
                    nextRefresh.Value = 0;
                    lastRefresh.Text = Resources.Strings.Refr_now;
                    seconds = 0;
                    minutes = 0;
                    hours = 0;
                });
                if (Settings.Default.showRefreshMessages)
                    this.Log(Resources.Strings.Refr_players, LogType.Console, false);

                players = rcon.getPlayers();

                if (players != null && players.Count > 0)
                {
                    ImageList imageList = new ImageList();
                    imageList.ColorDepth = ColorDepth.Depth32Bit;

                    foreach (Location location in locations)
                    {
                        location.flag.Dispose();
                    }
                    locations.Clear();

                    playerList.Invoke((MethodInvoker)delegate
                    {
                        playerList.Items.Clear();
                        playerList.SmallImageList = imageList;
                        playerList.ListViewItemSorter = null;
                    });
                    List<ListViewItem> items = new List<ListViewItem>();
                    for (int i = 0; i < players.Count; i++)
                    {
                        String[] entries = { "", players[i].number.ToString(), players[i].ip, players[i].ping, players[i].guid, players[i].name, players[i].status, GetComment(players[i].guid) };
                        items.Add(new ListViewItem(entries));
                        items[i].ImageIndex = i;
                    }
                    playerList.Invoke((MethodInvoker)delegate
                    {
                        playerList.Items.AddRange(items.ToArray());
                        playerList.ListViewItemSorter = playerSorter;
                    });

                    this.Invoke((MethodInvoker)delegate
                    {
                        this.Log(String.Format(Resources.Strings.Get_n_players, players.Count), LogType.Debug, false);
                        setPlayerCount(players.Count);
                    });

                    this.Invoke((MethodInvoker)delegate
                    {
                        lastRefresh.Text = Resources.Strings.Geoloc;
                    });

                    for (int i = 0; i < players.Count; i++)
                    {
                        locations.Add(GetLocation(IPAddress.Parse(players[i].ip)));

                        imageList.Images.Add(locations[i].flag);
                        playerList.Invoke((MethodInvoker)delegate
                        {
                            playerList.Items[i].Text = " " + locations[i].location.ToUpper();
                        });
                    }
                    playerList.Invoke((MethodInvoker)delegate
                    {
                        playerList.Refresh();
                    });

                    for (int i = 0; i < players.Count; i++)
                    {
                        String lastip = players[i].ip;
                        String lastseen = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        String guid = players[i].guid;
                        String name = players[i].name;
                        String status = players[i].status;
                        String lastseenon = host.Text;

                        if (Settings.Default.savePlayers)
                        {
                            try
                            {
                                using (SqliteCommand selectCommand = new SqliteCommand("SELECT id, guid, name FROM players WHERE guid = @guid AND name = @name LIMIT 0, 1", connection))
                                {
                                    selectCommand.Parameters.Clear();
                                    selectCommand.Parameters.Add(new SqliteParameter("@guid", guid));
                                    selectCommand.Parameters.Add(new SqliteParameter("@name", name));

                                    using (SqliteDataReader reader = selectCommand.ExecuteReader())
                                    {
                                        if (!reader.Read())
                                        {
                                            if (status != "Initializing")
                                            {
                                                using (SqliteCommand addCommand = new SqliteCommand("INSERT INTO players (id, lastip, lastseen, guid, name, lastseenon, synced) VALUES(NULL, @lastip, @lastseen, @guid, @name, @lastseenon, 0)", connection))
                                                {
                                                    addCommand.Parameters.Clear();
                                                    addCommand.Parameters.Add(new SqliteParameter("@lastip", lastip));
                                                    addCommand.Parameters.Add(new SqliteParameter("@lastseen", lastseen));
                                                    addCommand.Parameters.Add(new SqliteParameter("@guid", guid));
                                                    addCommand.Parameters.Add(new SqliteParameter("@name", name));
                                                    addCommand.Parameters.Add(new SqliteParameter("@lastseenon", lastseenon));
                                                    addCommand.ExecuteNonQuery();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            using (SqliteCommand updateCommand = new SqliteCommand("UPDATE players SET lastip = @lastip, lastseen = @lastseen, lastseenon = @lastseenon, synced = 0 WHERE guid = @guid AND name = @name", connection))
                                            {
                                                updateCommand.Parameters.Clear();
                                                updateCommand.Parameters.Add(new SqliteParameter("@lastip", lastip));
                                                updateCommand.Parameters.Add(new SqliteParameter("@lastseen", lastseen));
                                                updateCommand.Parameters.Add(new SqliteParameter("@guid", guid));
                                                updateCommand.Parameters.Add(new SqliteParameter("@name", name));
                                                updateCommand.Parameters.Add(new SqliteParameter("@lastseenon", lastseenon));
                                                updateCommand.ExecuteNonQuery();
                                            }
                                        }
                                    }
                                }
                            }
                            catch(Exception e)
                            {
                                this.Log(e.Message, LogType.Debug, false);
                                this.Log(e.StackTrace, LogType.Debug, false);
                            }
                        }
                    }
                }

                this.Invoke((MethodInvoker)delegate
                {
                    lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_s, 0);
                    timer.Start();
                });

                pendingPlayers = false;
            }
            catch(Exception e)
            {
                this.Log(Resources.Strings.Players_timeout, LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                pendingPlayers = false;
            }
        }
        public void thread_Bans()
        {
            try
            {
                pendingBans = true;

                if (rcon.Connected)
                {
                    if (Settings.Default.showRefreshMessages)
                        this.Log(Resources.Strings.Refr_bans, LogType.Console, false);

                    List<ListViewItem> items = GetBanList(-1, "");

                    if (items != null && items.Count > 0)
                    {
                        bansList.Invoke((MethodInvoker)delegate
                        {
                            bansList.ListViewItemSorter = null;
                            bansList.Items.Clear();
                        });
                        
                        bansList.Invoke((MethodInvoker)delegate
                        {
                            bansList.Items.AddRange(items.ToArray());
                            bansList.ListViewItemSorter = banSorter;
                        });

                        this.Invoke((MethodInvoker)delegate
                        {
                            this.Log(String.Format(Resources.Strings.Get_n_bans,bansList.Items.Count), LogType.Debug, false);
                            setBanCount(bansList.Items.Count);
                        });

                        if (bansList.Items.Count > 3000 && !Settings.Default.showedBanWarning && !Settings.Default.dartbrs)
                        {
                            MessageBox.Show(Resources.Strings.Big_banlist, Resources.Strings.Notice, MessageBoxButtons.OK, MessageBoxIcon.Information);

                            Settings.Default["showedBanWarning"] = true;
                            Settings.Default.Save();
                        }
                    }
                }
                pendingBans = false;
            }
            catch(Exception e)
            {
                if (Settings.Default.dartbrs)
                {
                    this.Log(Resources.Strings.Error_brs, LogType.Console, false);
                }
                else
                {
                    this.Log(Resources.Strings.Bans_timeout, LogType.Console, false);
                    this.Log(e.Message, LogType.Debug, false);
                    this.Log(e.StackTrace, LogType.Debug, false);
                }
                pendingBans = false;
            }
        }
        public void thread_Database()
        {
            try
            {
                pendingDatabase = true;
                
                // Clear cache, set virtual list size

                playerDBList.Invoke((MethodInvoker)delegate
                {
                    playerDBList.ListViewItemSorter = null;
                    playerDBList.Items.Clear();
                });

                List<ListViewItem> items = GetPlayersList(-1,"");

                playerDBList.Invoke((MethodInvoker)delegate
                {
                    playerDBList.Items.AddRange(items.ToArray());
                    playerDBList.ListViewItemSorter = playerDatabaseSorter;
                });

                pendingDatabase = false;
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                pendingDatabase = false;
            }
        }
        private void thread_Admins()
        {
            pendingAdmins = true;
            List<string> admins = rcon.getAdmins();
            this.Invoke((MethodInvoker)delegate
            {
                tooltip.SetToolTip(this.adminCounter, String.Join<string>("\n", admins));
                setAdminCount(admins.Count);
            });
            pendingAdmins = false;
        }
        private void thread_Banner()
        {
            try
            {
                banner.Invoke((MethodInvoker)delegate
                {
                    banner.Image = GetImage(Resources.Strings.Get_banner);
                });
                String ip =  String.Format("{0}:{1}",host.Text, Int32.Parse(port.Text)+1);
                String url = String.Format(Settings.Default.bannerImage, ip);

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(url);
                request.Proxy = proxy;
                request.UserAgent = "DaRT " + version;

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream stream = response.GetResponseStream())
                    {
                        Image image = Image.FromStream(stream);
                        banner.Invoke((MethodInvoker)delegate
                        {
                            banner.Image = image;
                        });
                    }
                }
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                banner.Image = GetImage(Resources.Strings.Error_banner);
            }
        }

        private void thread_News()
        {
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    this.news.Text = Resources.Strings.Loading_news;

                });
                WebClient client = new WebClient();
                client.Encoding = System.Text.Encoding.UTF8;
                client.Headers.Add("user-agent", "DaRT " + version);
                String request = client.DownloadString(Settings.Default.ReleaseUrl);
                client.Dispose();
                System.Text.RegularExpressions.Match info = new System.Text.RegularExpressions.Regex("<a href=\"(.+DaRT/releases/tag/(.[^\"]+))\"?.>(.[^<]+)</a>").Match(request);
                String size = new System.Text.RegularExpressions.Regex("<small class=\"text-gray float-right\">(.[^<]+)</small>").Match(request).Groups[1].Value;
                String baseUrl = new System.Text.RegularExpressions.Regex(@"https?:\/\/(.[^\/]+)").Match(Settings.Default.ReleaseUrl).Value;
                String url =  baseUrl + new System.Text.RegularExpressions.Regex("<a href=\"(.+DaRT/releases/download/.[^\"]+)\" rel=\"nofollow\">").Match(request).Groups[1].Value;
                String desc = new System.Text.RegularExpressions.Regex(@"<p>(.[^<]+\.)</p>").Match(request).Groups[1].Value;
                //new System.Text.RegularExpressions.Regex(@"<title>(.[^<]+)</title>").Match(request).Groups[1].Value;
                if(!version.Equals(info.Groups[2].Value))
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.Log(String.Format(Resources.Strings.News_download, info.Groups[3].Value,url,size,desc), LogType.Console, false);
                        this.news.Text = String.Format(Resources.Strings.News_update,info.Groups[3].Value,info.Groups[2].Value,size);
                        this.news.AccessibleDescription = url;
                        ToolTip tooltip = new ToolTip();
                        tooltip.AutoPopDelay = 30000;
                        tooltip.SetToolTip(this.news, desc + Resources.Strings.Down_upd);
                    
                    });
                }
                else
                {
                     this.Invoke((MethodInvoker)delegate
                    {
                        this.Log(Resources.Strings.News_noupdate, LogType.Console, false);
                        this.news.Text = "";
                        this.news.AccessibleDescription = "";
                    });
                }
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        this.news.Text = Resources.Strings.Error_news;
                    });
                }
            }
        }
        private void thread_Sync()
        {
            this.Invoke((MethodInvoker)delegate
            {
                this.Enabled = false;
            });

            this.Log("Starting player database sync...", LogType.Console, false);
            this.Log("This could take a while depending on how many players are in the database.", LogType.Console, false);
            this.Log("Please do not close DaRT during the process.", LogType.Console, false);
            playerDBList.Items.Clear();

            try
            {
                this.Log("Creating a backup of the existing database...", LogType.Console, false);
                if (File.Exists("data/db/players.db"))
                    File.Copy("data/db/players.db", "data/db/players.db_bak");

                this.Log("Reading database...", LogType.Console, false);
                command = new SqliteCommand(connection);
                command.CommandText = "SELECT guid, name, lastip, lastseenon, location, lastseen FROM players WHERE synced = 0";

                SqliteDataReader reader = command.ExecuteReader();

                List<Player> sync = new List<Player>();

                while (reader.Read())
                {
                    String guid = this.GetSafeString(reader, 0);
                    String name = this.GetSafeString(reader, 1);
                    String lastip = this.GetSafeString(reader, 2);
                    String lastseenon = this.GetSafeString(reader, 3);
                    String location = this.GetSafeString(reader, 4);
                    String lastseen = this.GetSafeString(reader, 5);

                    sync.Add(new Player(0, lastip, "", guid, name, "", lastseen, lastseenon, location));
                }

                reader.Close();
                reader.Dispose();
                command.Dispose();

                this.Log("Wiping database...", LogType.Console, false);
                command = new SqliteCommand(connection);
                command.CommandText = "DELETE FROM players";
                command.ExecuteNonQuery();
                command.Dispose();

                command = new SqliteCommand(connection);
                command.CommandText = "DELETE FROM Sqlite_sequence WHERE 'name' = 'players'";
                command.ExecuteNonQuery();
                command.Dispose();

                this.Log("Contacting master server...", LogType.Console, false);
                this.Log(String.Format("Syncing {0} players from database...", sync.Count), LogType.Console, false);

                foreach (Player player in sync)
                {
                    String data = String.Format("key={0}&guid={1}&ip={2}&lastseenon={3}&location={4}&lastseen={5}&name={6}", "l2k3g4jlksjaalkjhgt4whjkgsh4sap4", player.guid, player.ip, player.lastseenon, player.location, player.lastseen, player.name);

                    ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(certCheck);
                    HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("https://forum.swisscraft.eu/DaRT Sync/submit.php");
                    request.Proxy = proxy;
                    request.UserAgent = "DaRT " + version;
                    request.KeepAlive = false;
                    request.ProtocolVersion = HttpVersion.Version10;
                    request.Method = "POST";

                    byte[] postBytes = Encoding.ASCII.GetBytes(data);

                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = postBytes.Length;
                    
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(postBytes, 0, postBytes.Length);
                    requestStream.Close();

                    HttpWebResponse responseStream = (HttpWebResponse)request.GetResponse();
                    int status = (int)responseStream.StatusCode;

                    responseStream.Close();

                    if (status != 200)
                        throw new Exception();
                }

                this.Log("Requesting new database...", LogType.Console, false);
                if (connection.State == ConnectionState.Open)
                {
                    String data = String.Format("key={0}", "l2k3g4jlksjaalkjhgt4whjkgsh4sap4");

                    ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(certCheck);
                    HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("https://forum.swisscraft.eu/DaRT Sync/get.php");
                    request.Proxy = proxy;
                    request.UserAgent = "DaRT " + version;
                    request.KeepAlive = false;
                    request.ProtocolVersion = HttpVersion.Version10;
                    request.Method = "POST";

                    byte[] postBytes = Encoding.ASCII.GetBytes(data);

                    request.ContentType = "application/x-www-form-urlencoded";
                    request.ContentLength = postBytes.Length;
                    
                    Stream requestStream = request.GetRequestStream();
                    requestStream.Write(postBytes, 0, postBytes.Length);
                    requestStream.Close();

                    HttpWebResponse responseStream = (HttpWebResponse)request.GetResponse();
                    int status = (int)responseStream.StatusCode;

                    if (status == 200 && responseStream.ContentLength != 0)
                    {
                        command = new SqliteCommand(connection);
                        command.CommandText = "INSERT INTO players (id, lastip, lastseen, guid, name, lastseenon, location, synced) VALUES(NULL, @lastip, @lastseen, @guid, @name, @lastseenon, @location, 1)";

                        using (StreamReader stream = new StreamReader(responseStream.GetResponseStream()))
                        {
                            String line;
                            int received = 0;
                            while ((line = stream.ReadLine()) != null)
                            {
                                String[] items = line.Split(new char[] { ';' }, 6, StringSplitOptions.RemoveEmptyEntries);

                                command.Parameters.Add(new SqliteParameter("@lastip", items[1]));
                                command.Parameters.Add(new SqliteParameter("@lastseen", items[4]));
                                command.Parameters.Add(new SqliteParameter("@guid", items[0]));
                                command.Parameters.Add(new SqliteParameter("@name", items[5]));
                                command.Parameters.Add(new SqliteParameter("@lastseenon", items[2]));
                                command.Parameters.Add(new SqliteParameter("@location", items[3]));
                                command.ExecuteNonQuery();
                                received++;
                            }
                            this.Log("Received " + received + "players from master server.", LogType.Console, false);
                        }
                    }
                    else
                    {
                        responseStream.Close();
                        throw new Exception();
                    }
                    responseStream.Close();
                }

                this.Log("Player sync complete.", LogType.Console, false);
            }
            catch(Exception e)
            {
                this.Log("Something went wrong.", LogType.Console, false);
                this.Log("Restoring backup...", LogType.Console, false);

                connection.Close();

                if (File.Exists("data/db/players.db_bak"))
                {
                    File.Delete("data/db/players.db");
                    File.Copy("data/db/players.db_bak", "data/db/players.db");
                }

                connection.Open();

                this.Log("Please try again later.", LogType.Console, false);
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }

            this.Invoke((MethodInvoker)delegate
            {
                this.Enabled = true;
            });

            /*
            this.Log("Reading database...");
            command = new SqliteCommand(connection);

            command.CommandText = "SELECT id, lastip, lastseen, guid, name, lastseenon, location FROM players WHERE synced != 1 ORDER BY id ASC";

            SqliteDataReader reader = command.ExecuteReader();

            List<Player> sync = new List<Player>();

            while (reader.Read())
            {
                String id = reader[0].ToString();
                String lastip = reader[1].ToString();
                String lastseen = reader[2].ToString();
                String guid = reader[3].ToString();
                String name = reader[4].ToString();
                String lastseenon = reader[5].ToString();
                String location = reader[6].ToString();

                sync.Add(new Player(id, lastip, "", guid, name, "", lastseen, lastseenon, location));
            }

            this.Log("Submitting database...");
            foreach (Player player in sync)
            {
                String data = String.Format("key={0}&lastip={1}&lastseen={2}&guid={3}&name={4}&server={5}&location={6}", "l2k3g4jlksjaalkjhgt4whjkgsh4sap4", player.ip, player.lastseen, player.guid, player.name, player.lastseenon, player.location);

                ServicePointManager.ServerCertificateValidationCallback += new System.Net.Security.RemoteCertificateValidationCallback(certCheck);
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create("https://forum.swisscraft.eu/DaRT sync.php");
                request.Proxy = proxy;
                request.UserAgent = "DaRT " + version;
                request.KeepAlive = false;
                request.ProtocolVersion = HttpVersion.Version10;
                request.Method = "POST";

                byte[] postBytes = Encoding.ASCII.GetBytes(data);

                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = postBytes.Length;
                Stream requestStream = request.GetRequestStream();

                requestStream.Write(postBytes, 0, postBytes.Length);
                requestStream.Close();

                HttpWebResponse responseStream = (HttpWebResponse)request.GetResponse();
                String response = new StreamReader(responseStream.GetResponseStream()).ReadToEnd();
            }

            this.Log("Finished sync, thank you for your support!");
             */
        }
        #endregion

        #region Functions
        bool certCheck(object sender, X509Certificate cert, X509Chain chain, System.Net.Security.SslPolicyErrors error) { return true; }
        public static int GetDuration(double timestamp)
        {
            DateTime datetime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
            datetime = datetime.AddSeconds(timestamp).ToLocalTime();
            TimeSpan duration = datetime.Subtract(DateTime.Now);
            return (int)duration.TotalMinutes;
        }
        private uint IPToInt(IPAddress ip)
        {
            byte[] octets = ip.GetAddressBytes();
            return Convert.ToUInt32((Convert.ToUInt32(octets[0]) * Math.Pow(256, 3)) + (Convert.ToUInt32(octets[1]) * Math.Pow(256, 2)) + (Convert.ToUInt32(octets[2]) * 256) + Convert.ToUInt32(octets[3]));
        }
        public string GetSafeString(SqliteDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return string.Empty;
            else
                return reader.GetString(index);
        }
        private bool isPort(String number)
        {
            try
            {
                int port = Convert.ToInt32(number);
                if (port <= 65535 && port >= 0)
                    return true;
                else
                    return false;
            }
            catch
            {
                return false;
            }
        }
        public bool isIP(String ip)
        {
            try
            {
                IPAddress.Parse(ip);
                return true;
            }
            catch
            {
                return false;
            }
        }
        private Image GetImage(String text)
        {
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);
            Font font = new Font(new FontFamily("Verdana"), 12);

            img.Dispose();
            drawing.Dispose();

            img = new Bitmap(350, 20);

            drawing = Graphics.FromImage(img);

            drawing.Clear(this.BackColor);

            Brush textBrush = new SolidBrush(this.ForeColor);

            drawing.DrawString(text, font, textBrush, 0, 0);

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }
        public void say(Message message)
        {
            rcon.sayPrivate(message);
        }
        public void kick(Kick kick)
        {
            rcon.kick(kick);
        }
        public void banIP(BanIP ban)
        {
            rcon.banIP(ban);
        }
        public void banOffline(BanOffline ban)
        {
            rcon.banOffline(ban);
        }

        public void Log(object message, LogType type, bool important)
        {
            // Return if logging is not possible
            if (this.IsDisposed || !this.IsHandleCreated)
                return;

            // Message is important, flash window!
            if (Settings.Default.flash && important)
            {
                this.Invoke((MethodInvoker)delegate
                {
                    FlashWindow.Flash(this);
                });
            }

            if (type == LogType.Console)
            {
                this.AddMessage(console, new LogItem(type, message, important), false, false);
                if (Settings.Default.saveLog && consoleWriter != null) consoleWriter.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd | HH:mm:ss]") + " " + message);
            }

            else if
                (
                type == LogType.GlobalChat ||
                type == LogType.SideChat ||
                type == LogType.DirectChat ||
                type == LogType.VehicleChat ||
                type == LogType.CommandChat ||
                type == LogType.GroupChat ||
                type == LogType.UnknownChat ||
                type == LogType.AdminChat
                )
            {
                this.AddMessage(chat, new LogItem(type, message, important), true, false);
                if (Settings.Default.saveLog && chatWriter != null) chatWriter.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd | HH:mm:ss]") + " " + message);
            }

            else if
                (
                type == LogType.ScriptsLog ||
                type == LogType.CreateVehicleLog ||
                type == LogType.DeleteVehicleLog ||
                type == LogType.PublicVariableLog ||
                type == LogType.PublicVariableValLog ||
                type == LogType.RemoteExecLog ||
                type == LogType.RemoteControlLog ||
                type == LogType.SetDamageLog ||
                type == LogType.SetPosLog ||
                type == LogType.SetVariableLog ||
                type == LogType.SetVariableValLog ||
                type == LogType.AddBackpackCargoLog ||
                type == LogType.AddMagazineCargoLog ||
                type == LogType.AddWeaponCargoLog ||
                type == LogType.AttachToLog ||
                type == LogType.MPEventHandlerLog ||
                type == LogType.SelectPlayerLog ||
                type == LogType.TeamSwitchLog ||
                type == LogType.WaypointConditionLog ||
                type == LogType.WaypointStatementLog
                )
            {
                this.AddMessage(logs, new LogItem(type, message, important), false, true);
                if (Settings.Default.saveLog && logWriter != null) logWriter.WriteLine(DateTime.Now.ToString("[yyyy-MM-dd | HH:mm:ss]") + " " + message);
            }

            else if (Settings.Default.showDebug && type == LogType.Debug)
            {
                this.AddMessage(console, new LogItem(type, message, important), false, false);
            }
        }

        private List<LogItem> _queue;
        private bool _queueing = false;
        private void AddMessage(ExtendedRichTextBox box, LogItem item, bool isChat, bool isFilter)
        {
            if (_queue == null)
                _queue = new List<LogItem>();

            if (allowMessages.Checked)
            {
                string message = item.Message;

                // Adding timestamps if necessary
                if (Settings.Default.showTimestamps)
                    message = DateTime.Now.ToString("[yyyy-MM-dd | HH:mm:ss]") + " " + message;

                if (_buffer.Count <= Settings.Default.buffer)
                {
                    _buffer.Add(message);
                }
                else
                {
                    _buffer.RemoveAt(0);
                    _buffer.Add(message);
                }

                message = "\r\n" + message;

                Color color = Color.Empty;
                if (item.Type == LogType.Console)
                    color = this.GetColor("#000000");

                else if(item.Type == LogType.GlobalChat)
                    color = this.GetColor("#474747");
                else if (item.Type == LogType.SideChat)
                    color = this.GetColor("#19B5D1");
                else if (item.Type == LogType.DirectChat)
                    color = this.GetColor("#7F6D8B");
                else if (item.Type == LogType.VehicleChat)
                    color = this.GetColor("#C3990C");
                else if (item.Type == LogType.CommandChat)
                    color = this.GetColor("#A5861E");
                else if (item.Type == LogType.GroupChat)
                    color = this.GetColor("#87B066");
                else if (item.Type == LogType.UnknownChat)
                    color = this.GetColor("#4F0061");
                else if (item.Type == LogType.AdminChat)
                    color = this.GetColor("#B20600");

                else if (item.Type == LogType.ScriptsLog)
                    color = this.GetColor("#7E0500");
                else if (item.Type == LogType.CreateVehicleLog)
                    color = this.GetColor("#078B5D");
                else if (item.Type == LogType.DeleteVehicleLog)
                    color = this.GetColor("#07953B");
                else if (item.Type == LogType.PublicVariableLog)
                    color = this.GetColor("#8B0719");
                else if (item.Type == LogType.PublicVariableValLog)
                    color = this.GetColor("#950762");
                else if (item.Type == LogType.RemoteExecLog)
                    color = this.GetColor("#75007E");
                else if (item.Type == LogType.RemoteControlLog)
                    color = this.GetColor("#640795");
                else if (item.Type == LogType.SetDamageLog)
                    color = this.GetColor("#000853");
                else if (item.Type == LogType.SetPosLog)
                    color = this.GetColor("#17056A");
                else if (item.Type == LogType.SetVariableLog)
                    color = this.GetColor("#05296A");
                else if (item.Type == LogType.SetVariableValLog)
                    color = this.GetColor("#2E0560");
                else if (item.Type == LogType.AddBackpackCargoLog)
                    color = this.GetColor("#604805");
                else if (item.Type == LogType.AddMagazineCargoLog)
                    color = this.GetColor("#6A4505");
                else if (item.Type == LogType.AddWeaponCargoLog)
                    color = this.GetColor("#532A00");
                else if (item.Type == LogType.AttachToLog)
                    color = this.GetColor("#6A2A05");
                else if (item.Type == LogType.MPEventHandlerLog)
                    color = this.GetColor("#601905");
                else if (item.Type == LogType.SelectPlayerLog)
                    color = this.GetColor("#056043");
                else if (item.Type == LogType.TeamSwitchLog)
                    color = this.GetColor("#056A67");
                else if (item.Type == LogType.WaypointConditionLog)
                    color = this.GetColor("#003F53");
                else if (item.Type == LogType.WaypointStatementLog)
                    color = this.GetColor("#05386A");

                else if (item.Type == LogType.Debug)
                    color = this.GetColor("#808080");

                all.Invoke((MethodInvoker)delegate
                {
                    all.SelectionStart = all.TextLength;
                    all.SelectionLength = 0;

                    if (isChat && !Settings.Default.colorChat)
                        all.SelectionColor = this.GetColor("#000000");
                    else if(isFilter && !Settings.Default.colorFilters)
                        all.SelectionColor = this.GetColor("#000000");
                    else
                        all.SelectionColor = color;

                    if (item.Important)
                        all.SelectionFont = new Font(all.Font, FontStyle.Bold);

                    all.AppendText(message);

                    all.SelectionStart = all.Text.Length;
                    all.ScrollToCaret();
                    all.Enabled = false;
                    all.Enabled = true;

                    all.SelectionColor = all.ForeColor;
                    all.SelectionFont = new Font(all.Font, FontStyle.Regular);
                });

                box.Invoke((MethodInvoker)delegate
                {
                    box.SelectionStart = box.TextLength;
                    box.SelectionLength = 0;
                    box.SelectionColor = color;
                    if (item.Important)
                        box.SelectionFont = new Font(box.Font, FontStyle.Bold);

                    box.AppendText(message);

                    box.SelectionStart = box.Text.Length;
                    box.ScrollToCaret();
                    box.Enabled = false;
                    box.Enabled = true;

                    box.SelectionColor = box.ForeColor;
                    box.SelectionFont = new Font(box.Font, FontStyle.Regular);
                });
            }
            else
            {
                _queueing = true;
                _queue.Add(item);
            }
        }
        private Color GetColor(string hex)
        {
            return ColorTranslator.FromHtml(hex);
        }

        public void setPlayerCount(int amount)
        {
            playerCounter.Text = String.Format(Resources.Strings.Players_c,amount);
        }
        public void setBanCount(int amount)
        {
            banCounter.Text = String.Format(Resources.Strings.Bans_c,amount);
        }
        public void setAdminCount(int amount)
        {
            adminCounter.Text = String.Format(Resources.Strings.Admins_c,amount);
        }
        
        public Location GetLocation(IPAddress ip)
        {
            try
            {
                using (command = new SqliteCommand("SELECT location FROM players WHERE lastip = @lastip AND location != ''", connection))
                {
                    command.Parameters.Clear();
                    command.Parameters.Add(new SqliteParameter("@lastip", ip));

                    using(SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            String location = this.GetSafeString(reader, 0);

                            if (File.Exists("data/img/flags/" + location + ".gif"))
                            {
                                return new Location(location, Image.FromFile("data/img/flags/" + location + ".gif"));
                            }
                            else
                            {
                                command.Dispose();

                                String url = "http://www.ipinfodb.com/img/flags/" + location.ToLower() + ".gif";

                                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
                                webRequest.Proxy = proxy;
                                webRequest.AllowWriteStreamBuffering = true;
                                webRequest.Timeout = 10000;
                                webRequest.UserAgent = "DaRT " + version;

                                WebResponse webResponse = webRequest.GetResponse();

                                Stream stream = webResponse.GetResponseStream();
                                Image image = Image.FromStream(stream);
                                image.Save("data/img/flags/" + location + ".gif");
                                return new Location(location, image);
                            }
                        }
                        else
                        {
                            string location = "";
                            using (SqliteCommand locationCommand = new SqliteCommand("SELECT country FROM location WHERE start <= @ip AND end >= @ip", connection))
                            {
                                locationCommand.Parameters.Clear();
                                SqliteParameter parameter = new SqliteParameter(DbType.UInt64, this.IPToInt(ip));
                                parameter.ParameterName = "@ip";
                                locationCommand.Parameters.Add(parameter);

                                using (SqliteDataReader locationReader = locationCommand.ExecuteReader())
                                {
                                    if (locationReader.Read())
                                        location = this.GetSafeString(locationReader, 0);
                                }
                            }

                            if (location != "")
                            {
                                using (command = new SqliteCommand("UPDATE players SET location = @location WHERE lastip = @lastip", connection))
                                {
                                    command.Parameters.Clear();
                                    command.Parameters.Add(new SqliteParameter("@location", location));
                                    command.Parameters.Add(new SqliteParameter("@lastip", ip));
                                    command.ExecuteNonQuery();
                                }

                                if (File.Exists("data/img/flags/" + location + ".gif"))
                                {
                                    return new Location(location, Image.FromFile("data/img/flags/" + location + ".gif"));
                                }
                                else
                                {
                                    String url = "http://www.ipinfodb.com/img/flags/" + location.ToLower() + ".gif";

                                    HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
                                    webRequest.Proxy = proxy;
                                    webRequest.AllowWriteStreamBuffering = true;
                                    webRequest.Timeout = 10000;
                                    webRequest.UserAgent = "DaRT " + version;

                                    WebResponse webResponse = webRequest.GetResponse();

                                    Stream stream = webResponse.GetResponseStream();
                                    Image image = Image.FromStream(stream);
                                    image.Save("data/img/flags/" + location + ".gif");
                                    return new Location(location, image);
                                }
                            }
                            else
                                return new Location("?", Image.FromFile("data/img/flags/unknown.gif"));
                        }
                    }
                }
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                return new Location("?", Image.FromFile("data/img/flags/unknown.gif"));
            }
        }
        public String GetLocationString(String ip)
        {
            try
            {
                string location = "?";
                using (command = new SqliteCommand("SELECT country FROM location WHERE start <= @ip AND end >= @ip", connection))
                {
                    command.Parameters.Add(new SqliteParameter("@ip", this.IPToInt(IPAddress.Parse(ip))));

                    using (SqliteDataReader locationReader = command.ExecuteReader())
                    {
                        if (locationReader.Read())
                            location = this.GetSafeString(locationReader, 0);
                    }
                }

                return location;
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                return "?";
            }
        }

        public List<ListViewItem> GetPlayersList(int filter , String searchtext)
        {
            List<ListViewItem> items = new List<ListViewItem>();
            try
            {
                command = new SqliteCommand(connection);

                command.CommandText = "SELECT players.id, players.lastip, players.lastseen, players.guid, players.name, players.lastseenon, comments.comment FROM players LEFT JOIN comments ON players.guid = comments.guid ";

                if (filter != -1 && searchtext != "")
                {
                    searchtext = "%" + searchtext + "%";
                    switch (filter)
                    {
                        case 0:
                            command.CommandText += "WHERE players.name LIKE(@name) ";
                            command.Parameters.Add(new SqliteParameter("@name", searchtext));
                            break;

                        case 1:
                            command.CommandText += "WHERE players.guid LIKE(@guid) ";
                            command.Parameters.Add(new SqliteParameter("@guid", searchtext));
                            break;

                        case 2:
                            command.CommandText += "WHERE players.lastip LIKE(@lastip) ";
                            command.Parameters.Add(new SqliteParameter("@lastip", searchtext));
                            break;

                        case 3:
                            command.CommandText += "WHERE comments.comment LIKE(@comment) ";
                            command.Parameters.Add(new SqliteParameter("@comment", searchtext));
                            break;
                    }
                }

                command.CommandText += "ORDER BY id ASC";

                SqliteDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    int id = reader.GetInt32(0);
                    String lastip = this.GetSafeString(reader, 1);
                    String lastseen = this.GetSafeString(reader, 2);
                    String guid = this.GetSafeString(reader, 3);
                    String name = this.GetSafeString(reader, 4);
                    String lastseenon = this.GetSafeString(reader, 5);
                    String comment = this.GetSafeString(reader, 6);

                    String[] row = { id.ToString(), lastip, lastseen, guid, name, lastseenon, comment };
                    items.Add(new ListViewItem(row));
                }
                reader.Close();
                reader.Dispose();
                command.Dispose();
            }
            catch (Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
            return items;
        }
        public List<ListViewItem> GetBanList(int filter, String searchtext)
        {
            List<ListViewItem> items = new List<ListViewItem>();
            if (!Settings.Default.dartbrs)
            {
                searchtext = searchtext.ToLower();
                foreach(Ban ban in rcon.getBans())
                {
                    String comment = "";
                    if (ban.ipguid.Length == 32)
                    {
                        comment = GetComment(ban.ipguid);
                    }
                    if (filter == -1 || searchtext.Equals("") ||
                        (filter == 0 && ban.ipguid.ToLower().Contains(searchtext)) ||
                        (filter == 1 && ban.reason.ToLower().Contains(searchtext)) ||
                        (filter == 2 && comment.ToLower().Contains(searchtext)))
                    {
                        String[] entries = { ban.number, ban.ipguid, ban.time, ban.reason, comment };
                        items.Add(new ListViewItem(entries));
                    }
                }
            }
            else
            {
                #region BRS
                /*using (TcpClient client = new TcpClient())
                        {
                            client.Connect(IPAddress.Parse(host.Text), Int32.Parse(port.Text));
                            client.SendTimeout = 15000;
                            client.ReceiveTimeout = 15000;

                            Socket socket = client.Client;

                            byte[] buffer = Encoding.UTF8.GetBytes(password.Text);
                            socket.Send(buffer);

                            buffer = new byte[4];
                            socket.Receive(buffer);

                            int length = BitConverter.ToInt32(buffer, 0);
                            socket.ReceiveBufferSize = length;

                            buffer = Encoding.UTF8.GetBytes("OK");
                            socket.Send(buffer);

                            buffer = new byte[length];
                            int tempLength = 0;
                            int tempPos = 0;
                            byte[] tempBuffer = new byte[1024];
                            while ((tempLength = socket.Receive(tempBuffer, 1024, SocketFlags.None)) != 0)
                            {
                                Array.Copy(tempBuffer, 0, buffer, tempPos, tempLength);
                                tempPos += tempLength;
                            }

                            String brsBans = Encoding.UTF8.GetString(buffer);
                            bans.Clear();

                            using (StringReader reader = new StringReader(brsBans))
                            {
                                String line;
                                int number = 0;
                                while ((line = reader.ReadLine()) != null)
                                {
                                    String[] items = line.Split(new char[] { ' ' }, 3, StringSplitOptions.RemoveEmptyEntries);
                                    if (items.Length > 0)
                                    {
                                        String ipguid = items[0];
                                        String time = items[1];
                                        String reason = "(No reason)";
                                        if (time == "-1")
                                            time = "perm";
                                        else
                                        {
                                            time = GetDuration(double.Parse(time)).ToString();

                                            if (Int32.Parse(time) < 0)
                                                time = "expired";
                                        }

                                        if (items.Length >= 3) reason = items[2];

                                        if (ipguid.IndexOf('.') == -1)
                                        {
                                            bans.Add(new Ban(number.ToString(), ipguid, time, reason));
                                            number++;
                                        }
                                    }

                                }
                            }
                        }*/
                #endregion
                try
                {
                    command = new SqliteCommand(connection);

                    command.CommandText = "SELECT bans.id, bans.guid, bans.duration, bans.reason, comments.comment FROM players LEFT JOIN comments ON bans.guid = comments.guid ";

                    searchtext = "%" + searchtext + "%";
                    if (filter != -1 && searchtext != "%%") switch (filter)
                        {
                            case 0:
                                command.CommandText += "WHERE bans.guid LIKE(@guid) ";
                                command.Parameters.Add(new SqliteParameter("@guid", searchtext));
                                break;

                            case 1:
                                command.CommandText += "WHERE bans.reason LIKE(@lastip) ";
                                command.Parameters.Add(new SqliteParameter("@lastip", searchtext));
                                break;

                            case 2:
                                command.CommandText += "WHERE comments.comment LIKE(@comment) ";
                                command.Parameters.Add(new SqliteParameter("@comment", searchtext));
                                break;
                        }

                    command.CommandText += "ORDER BY id ASC";

                    SqliteDataReader reader = command.ExecuteReader();

                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        String guid = this.GetSafeString(reader, 1);
                        String duration = this.GetSafeString(reader, 2);
                        String reason = this.GetSafeString(reader, 3);
                        String comment = this.GetSafeString(reader, 4);

                        String[] entries = { id.ToString(), guid, duration, reason, comment };
                        items.Add(new ListViewItem(entries));
                    }
                    reader.Close();
                    reader.Dispose();
                    command.Dispose();
                }
                catch (Exception e)
                {
                    this.Log(e.Message, LogType.Debug, false);
                    this.Log(e.StackTrace, LogType.Debug, false);
                }
            }
            return items;
        }
        public String GetComment(String guid)
        {
            String comment = "";
            // Get comment for GUID
            try
            {
                using (command = new SqliteCommand("SELECT comment FROM comments WHERE guid = @guid", connection))
                {
                    command.Parameters.Clear();
                    command.Parameters.Add(new SqliteParameter("@guid", guid));

                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (!reader.IsClosed && reader.HasRows && reader.Read())
                            comment = this.GetSafeString(reader, 0);
                    }
                }
            }
            catch (Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
            }
            return comment;
        }
        #endregion 

        int seconds = 0;
        int minutes = 0;
        int hours = 0;

        private void timer_Tick(object sender, EventArgs args)
        {
            if (!rcon.Reconnecting)
            {
                refresh.Enabled = true;
                execute.Enabled = true;
                input.Enabled = true;

                if (refreshTimer > 0)
                {
                    refreshTimer--;
                    if (autoRefresh.Checked)
                    {
                        nextRefresh.Maximum = (int)Settings.Default.interval;
                        if (nextRefresh.Value + 1 <= nextRefresh.Maximum)
                            nextRefresh.Value += 1;
                    }
                    else
                        nextRefresh.Value = 0;
                }
                else if (autoRefresh.Checked)
                {
                    if (!menuOpened && this.Enabled && search.Text == "" && rcon.Connected)
                    {
                        if (!pendingPlayers)
                        {
                            Thread threadPlayer = new Thread(new ThreadStart(thread_Player));
                            threadPlayer.IsBackground = true;
                            threadPlayer.Start();
                            Thread.Sleep(2000);
                        }
                        if (!pendingAdmins)
                        {
                            Thread threadAdmins = new Thread(new ThreadStart(thread_Admins));
                            threadAdmins.IsBackground = true;
                            threadAdmins.Start();
                        }
                    }
                }
                seconds++;

                if (seconds >= 60)
                {
                    minutes++;
                    seconds = 0;
                }
                if (minutes >= 60)
                {
                    hours++;
                    minutes = 0;
                }
                if (hours >= 60)
                {
                    timer.Stop();
                    lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_h, "XX");
                }
                else
                {
                    if (hours > 0)
                    {
                        lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_h, hours);
                    }
                    else if (minutes > 0)
                    {
                        lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_m, minutes);
                    }
                    else
                    {
                        lastRefresh.Text = String.Format(Resources.Strings.Last_refresh_s, seconds);
                    }
                }
            }
            else
            {
                refresh.Enabled = false;
                execute.Enabled = false;
                input.Enabled = false;
                lastRefresh.Text = Resources.Strings.Recon;
                nextRefresh.Maximum = 5;
                if (nextRefresh.Value >= 5)
                    nextRefresh.Value = 0;
                else
                    nextRefresh.Value++;
            }
        }

        #region Konami
        private UInt16 konami = 0;
        #endregion
        private void GUI_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode == Keys.F5)
            {
                search.Text = "";

                if (!(tabControl.SelectedTab.Name == "playerdatabaseTab"))
                {
                    if (disconnect.Enabled)
                    {
                        if (tabControl.SelectedTab.Name == "playersTab" && rcon.Connected && !pendingPlayers)
                        {
                            Thread thread = new Thread(new ThreadStart(thread_Player));
                            thread.IsBackground = true;
                            thread.Start();
                            Thread banner = new Thread(new ThreadStart(thread_Banner));
                            banner.IsBackground = true;
                            banner.Start();
                        }
                        else if (tabControl.SelectedTab.Name == "bansTab" && !pendingBans)
                        {
                            Thread thread = new Thread(new ThreadStart(thread_Bans));
                            thread.IsBackground = true;
                            thread.Start();
                            Thread banner = new Thread(new ThreadStart(thread_Banner));
                            banner.IsBackground = true;
                            banner.Start();
                        }
                    }
                    else
                    {
                        this.Log(Resources.Strings.Error_nocon, LogType.Console, false);
                    }
                }
                else
                {
                    if (Settings.Default.savePlayers && !pendingDatabase)
                    {
                        Thread thread = new Thread(new ThreadStart(thread_Database));
                        thread.IsBackground = true;
                        thread.Start();
                    }
                }
            }
            else if (konami == 0 && args.KeyCode == Keys.Up)
                konami++;
            else if (konami == 1 && args.KeyCode == Keys.Up)
                konami++;
            else if (konami == 2 && args.KeyCode == Keys.Down)
                konami++;
            else if (konami == 3 && args.KeyCode == Keys.Down)
                konami++;
            else if (konami == 4 && args.KeyCode == Keys.Left)
                konami++;
            else if (konami == 5 && args.KeyCode == Keys.Right)
                konami++;
            else if (konami == 6 && args.KeyCode == Keys.Left)
                konami++;
            else if (konami == 7 && args.KeyCode == Keys.Right)
                konami++;
            else if (konami == 8 && args.KeyCode == Keys.B)
                konami++;
            else if (konami == 9 && args.KeyCode == Keys.A)
            {
                konami = 0;

                this.BackColor = Color.Black;
                this.MinimumSize = new Size(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height);
                this.MaximizeBox = false;
                this.FormBorderStyle = FormBorderStyle.None;
                this.Width = Screen.PrimaryScreen.Bounds.Width;
                this.Height = Screen.PrimaryScreen.Bounds.Height;
                this.Location = new Point(0, 0);

                dart.Visible = true;
                dart.BringToFront();
                dart.Width = 640;
                dart.Height = 360;
                dart.Parent = this;
                dart.Left = (this.Width - dart.Width) / 2;
                dart.Top = (this.Height - dart.Height) / 2;

                foreach (Control control in this.Controls)
                {
                    if (control.Name != "dart")
                    {
                        control.Visible = false;
                    }
                }
            }
            else
                konami = 0;
        }

        private void LinkClicked(object sender, LinkClickedEventArgs args)
        {
            Process.Start(args.LinkText);
        }

        private void settings_Click(object sender, EventArgs args)
        {
            GUIsettings gui = new GUIsettings(this);
            gui.ShowDialog();
        }

        private void reloadBans_Click(object sender, EventArgs args)
        {
            rcon.bans();
        }

        private void reloadEvents_Click(object sender, EventArgs args)
        {
            rcon.events();
        }

        private void lock_Click(object sender, EventArgs args)
        {
            rcon.lockServer();
        }
        private void unlock_Click(object sender, EventArgs args)
        {
            rcon.unlockServer();
        }
        private void shutdown_Click(object sender, EventArgs args)
        {
            GUIconfirm gui = new GUIconfirm(this, rcon);
            gui.ShowDialog();
        }

        private void addBan_Click(object sender, EventArgs args)
        {
            // TODO: REIMPLEMENT
            //GUIbanGUID gui = new GUIbanGUID(this);
            //gui.ShowDialog();
        }
        private void addBans_Click(object sender, EventArgs args)
        {
            GUImanualBans gui = new GUImanualBans(rcon);
            gui.ShowDialog();
        }

        private void hosts_Click(object sender, EventArgs args)
        {
            GUIhosts gui = new GUIhosts(this, connection, command);
            gui.ShowDialog();
        }
        private void copy_click(object sender, EventArgs args)
        {
            ((DaRT.ExtendedRichTextBox)((ContextMenuStrip)(((ToolStripMenuItem)sender).Owner)).SourceControl).Copy();
        }
        private void clear_click(object sender, EventArgs args)
        {
            DaRT.ExtendedRichTextBox textbox = (DaRT.ExtendedRichTextBox)((ContextMenuStrip)(((ToolStripMenuItem)sender).Owner)).SourceControl;
            textbox.Clear();
            textbox.AppendText(String.Format(Resources.Strings.InitApp, version));
        }
        private void clearAll_click(object sender, EventArgs args)
        {
            all.Clear();
            all.AppendText(String.Format(Resources.Strings.InitApp, version));

            console.Clear();
            console.AppendText(String.Format(Resources.Strings.InitApp, version));

            chat.Clear();
            chat.AppendText(String.Format(Resources.Strings.InitApp, version));

            logs.Clear();
            logs.AppendText(String.Format(Resources.Strings.InitApp, version));
        }

        private void console_MouseDown(object sender, MouseEventArgs args)
        {
            
        }
        private void DblClick_log(object sender, MouseEventArgs args)
        {
            String text = ((DaRT.ExtendedRichTextBox)sender).SelectedText.Trim();
            if (text != "")
            {
                foreach (ListViewItem item in playerList.Items)
                {
                    if (item.SubItems[5].Text.Contains(text)|| item.SubItems[4].Text.Equals(text))
                    {
                        item.Selected = true;
                        playerList.Select();
                        item.EnsureVisible();
                        options.SelectedIndex = 1;
                        input.Focus();
                    }
                }
            }
        }
        private void GUI_Load(object sender, EventArgs args)
        {
            InitializeSplitter();
            InitializeText();
            InitializeDatabase();
            if (Settings.Default.dbRemote) InitializeRemoteBase();
            InitializeFields();
            InitializeBox();
            InitializePlayerList();
            InitializeBansList();
            InitializePlayerDBList();
            InitializeFunctions();
            InitializeConsole();
            InitializeBanner();
            if (Settings.Default.Check_update) InitializeNews();
            InitializeProxy();
            InitializeProgressBar();
            InitializeFonts();
            InitializeTooltips();
            Console.WriteLine(String.Format(Resources.Strings.InitApp, version));
            all.AppendText(String.Format(Resources.Strings.InitApp, version));
            console.AppendText(String.Format(Resources.Strings.InitApp, version));
            chat.AppendText(String.Format(Resources.Strings.InitApp, version));
            logs.AppendText(String.Format(Resources.Strings.InitApp, version));

            if (Settings.Default.firstStart)
            {
                if (File.Exists("data/db/players.db"))
                    MessageBox.Show(Resources.Strings.First_run, Resources.Strings.Notice, MessageBoxButtons.OK, MessageBoxIcon.Information);

            UpdateDatabase();

            Settings.Default.firstStart = false;
            Settings.Default.Save();
            }

            if (Settings.Default.saveLog)
            {
                try
                {
                    consoleWriter = new StreamWriter("console.log", true, Encoding.Unicode);
                    consoleWriter.AutoFlush = true;
                    chatWriter = new StreamWriter("chat.log", true, Encoding.Unicode);
                    chatWriter.AutoFlush = true;
                    logWriter = new StreamWriter("report.log", true, Encoding.Unicode);
                    logWriter.AutoFlush = true;
                }
                catch (Exception e)
                {
                    this.Log(Resources.Strings.Error_occ_log, LogType.Console, false);
                    this.Log(e.Message, LogType.Debug, false);
                    this.Log(e.StackTrace, LogType.Debug, false);
                }
            }

            if (Settings.Default.connectOnStartup)
            {
                Thread thread = new Thread(new ThreadStart(thread_Connect));
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private void news_Click(object sender, EventArgs args)
        {
            try
            {
                if (((Label)sender).AccessibleDescription != "")
                    Process.Start(((Label)sender).AccessibleDescription);
            }
            catch(Exception e)
            {
                this.Log(e.Message, LogType.Debug, false);
                this.Log(e.StackTrace, LogType.Debug, false);
                this.Log(Resources.Strings.Error_occ, LogType.Console, false);
            }
        }

        private void playerList_ColumnClick(object sender, ColumnClickEventArgs args)
        {
            if (args.Column == playerSorter.SortColumn)
            {
                if (playerSorter.Order == SortOrder.Ascending)
                {
                    playerSorter.Order = SortOrder.Descending;
                }
                else
                {
                    playerSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                playerSorter.SortColumn = args.Column;
                playerSorter.Order = SortOrder.Ascending;
            }

            this.playerList.Sort();
        }

        private void bansList_ColumnClick(object sender, ColumnClickEventArgs args)
        {

            if (args.Column == banSorter.SortColumn)
            {
                if (banSorter.Order == SortOrder.Ascending)
                {
                    banSorter.Order = SortOrder.Descending;
                }
                else
                {
                    banSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                banSorter.SortColumn = args.Column;
                banSorter.Order = SortOrder.Ascending;
            }

            this.bansList.Sort();
            
        }

        private void playerDBList_ColumnClick(object sender, ColumnClickEventArgs args)
        {
            
            if (args.Column == playerDatabaseSorter.SortColumn)
            {
                if (playerDatabaseSorter.Order == SortOrder.Ascending)
                {
                    playerDatabaseSorter.Order = SortOrder.Descending;
                }
                else
                {
                    playerDatabaseSorter.Order = SortOrder.Ascending;
                }
            }
            else
            {
                playerDatabaseSorter.SortColumn = args.Column;
                playerDatabaseSorter.Order = SortOrder.Ascending;
            }

            this.playerDBList.Sort();
            
        }

        private void execute_Click(object sender, EventArgs args)
        {
            executeContextMenu.Show(Cursor.Position);
        }

        private void search_KeyDown(object sender, KeyEventArgs args)
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.Handled = true;
                args.SuppressKeyPress = true;
            }
            else if (args.KeyCode == Keys.Escape)
            {
                search.Clear();

                args.Handled = true;
                args.SuppressKeyPress = true;
            }
        }

        private void searchButton_Click(object sender, EventArgs args)
        {
            #region Players
            if (tabControl.SelectedTab.Name == "playersTab")
            {
                if (search.Text != "")
                {
                    playerList.Items.Clear();

                    for (int i = 0; i < players.Count; i++)
                    {
                        String[] items = { "", players[i].number.ToString(), players[i].ip, players[i].ping, players[i].guid, players[i].name, players[i].status, GetComment(players[i].guid) };
                        ListViewItem item = new ListViewItem(items);
                        item.ImageIndex = i;

                        playerList.Items.Add(item);
                    }

                    List<ListViewItem> foundItems = new List<ListViewItem>();

                    foreach (ListViewItem item in playerList.Items)
                    {
                        if (filter.SelectedIndex == 0)
                        {
                            if (item.SubItems[5].Text.ToLower().Contains(search.Text.ToLower()))
                            {
                                foundItems.Add(item);
                            }
                        }
                        else if (filter.SelectedIndex == 1)
                        {
                            if (item.SubItems[4].Text.ToLower().Contains(search.Text.ToLower()))
                            {
                                foundItems.Add(item);
                            }
                        }
                        else if (filter.SelectedIndex == 2)
                        {
                            if (item.SubItems[2].Text.ToLower().Contains(search.Text.ToLower()))
                            {
                                foundItems.Add(item);
                            }
                        }
                        else if (filter.SelectedIndex == 3)
                        {
                            if (item.SubItems[7].Text.ToLower().Contains(search.Text.ToLower()))
                            {
                                foundItems.Add(item);
                            }
                        }
                    }

                    playerList.Items.Clear();

                    for (int i = 0; i < foundItems.Count; i++)
                    {
                        playerList.Items.Add(foundItems[i]);
                    }

                }
                else
                {
                    playerList.Items.Clear();

                    for (int i = 0; i < players.Count; i++)
                    {
                        String[] items = { "", players[i].number.ToString(), players[i].ip, players[i].ping, players[i].guid, players[i].name, players[i].status, GetComment(players[i].guid) };
                        ListViewItem item = new ListViewItem(items);
                        item.ImageIndex = i;

                        playerList.Items.Add(item);
                    }
                }
            }
            #endregion
            #region Bans
            if (tabControl.SelectedTab.Name == "bansTab")
            {
                List<ListViewItem> items = GetBanList(filter.SelectedIndex, search.Text);
                bansList.Items.Clear();
                bansList.Items.AddRange(items.ToArray());
                bansList.ListViewItemSorter = banSorter;
            }
            #endregion
            #region Player Database
            else if (tabControl.SelectedTab.Name == "playerdatabaseTab")
            {
                List<ListViewItem> items = GetPlayersList(filter.SelectedIndex, search.Text);
                playerDBList.ListViewItemSorter = null;
                playerDBList.Items.Clear();
                playerDBList.Items.AddRange(items.ToArray());
                playerDBList.ListViewItemSorter = playerDatabaseSorter;

            }
            #endregion
        }

        private void options_SelectedIndexChanged(object sender, EventArgs args)
        {
            if (options.SelectedIndex == 0)
            {
                input.AutoCompleteMode = AutoCompleteMode.None;
            }
            else if (options.SelectedIndex == 1)
            {
                input.AutoCompleteMode = AutoCompleteMode.Append;
            }
        }


        private void input_TextChanged(object sender, EventArgs e)
        {
            counter.Text = input.Text.Length + "/400";
        }

        private void dart_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void autoScroll_CheckedChanged(object sender, EventArgs e)
        {
            if (allowMessages.Checked && _queueing && _queue.Count > 0)
            {
                _queueing = false;

                foreach (LogItem item in _queue)
                {
                    this.Log(item.Message, item.Type, item.Important);
                }
                _queue.Clear();
            }
        }
    }
}