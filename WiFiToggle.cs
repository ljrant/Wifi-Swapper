using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Program {
  static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFiToggle");
  static readonly string FileName = Path.Combine(Folder, "profiles.txt");

  [STAThread] static void Main(string[] args) {
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    string mode = args.Length == 0 ? "--toggle" : args[0];
    if (mode == "--tray") {
      bool fresh;
      using (Mutex mutex = new Mutex(true, @"Local\WiFiSwapper_Tray", out fresh)) {
        if (fresh) Application.Run(new TrayContext());
      }
      return;
    }
    try {
      using (WifiAdapter wifi = new WifiAdapter()) {
        string[] saved = ReadConfig();
        if (mode == "--configure" || saved == null || !wifi.HasProfiles(saved)) {
          Configure(wifi, saved); return;
        }
        string connected = wifi.CurrentProfile();
        string a = saved[0], b = saved[1];
        string last = saved.Length > 2 ? saved[2] : "";
        string destination = connected == a ? b : connected == b ? a : last == a ? b : a;
        wifi.Connect(destination);
        SaveConfig(a, b, destination);
      }
    } catch (Exception e) {
      MessageBox.Show("Verkon vaihto epäonnistui.\n\n" + e.Message,
        "Wi-Fi-vaihtaja", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
  }

  internal static string[] ReadConfig() {
    try {
      if (!System.IO.File.Exists(FileName)) return null;
      string[] result = System.IO.File.ReadAllLines(FileName, Encoding.UTF8);
      if (result.Length < 2 || String.IsNullOrWhiteSpace(result[0]) ||
          String.IsNullOrWhiteSpace(result[1]) || result[0] == result[1]) return null;
      return result;
    } catch { return null; }
  }
  static void SaveConfig(string a, string b, string last) {
    Directory.CreateDirectory(Folder);
    System.IO.File.WriteAllLines(FileName, new [] { a, b, last }, new UTF8Encoding(false));
  }
  static void Configure(WifiAdapter wifi, string[] existing) {
    List<string> profiles = wifi.Profiles();
    if (profiles.Count < 2) {
      MessageBox.Show("Tallenna ensin vähintään kaksi Wi-Fi-verkkoa Windowsissa.",
        "Wi-Fi-vaihtaja", MessageBoxButtons.OK, MessageBoxIcon.Information);
      return;
    }
    using (Form form = new Form()) {
      form.Text = "Valitse Wi-Fi-verkot";
      form.StartPosition = FormStartPosition.CenterScreen;
      form.ClientSize = new Size(380, 205);
      form.FormBorderStyle = FormBorderStyle.FixedDialog;
      form.MaximizeBox = false; form.MinimizeBox = false;
      form.Font = SystemFonts.MessageBoxFont;
      Label title = new Label { Text = "Valitse kaksi Windowsiin tallennettua verkkoa:", Location = new Point(16, 15), Size = new Size(348, 28) };
      Label labA = new Label { Text = "Verkko A", Location = new Point(18, 58), AutoSize = true };
      Label labB = new Label { Text = "Verkko B", Location = new Point(18, 100), AutoSize = true };
      ComboBox a = new ComboBox { Location = new Point(125, 54), Size = new Size(240, 25), DropDownStyle = ComboBoxStyle.DropDownList };
      ComboBox b = new ComboBox { Location = new Point(125, 96), Size = new Size(240, 25), DropDownStyle = ComboBoxStyle.DropDownList };
      foreach (string name in profiles) { a.Items.Add(name); b.Items.Add(name); }
      string initialA = existing != null && profiles.Contains(existing[0]) ? existing[0] : wifi.CurrentProfile();
      if (initialA == null || !profiles.Contains(initialA)) initialA = profiles[0];
      string initialB = existing != null && profiles.Contains(existing[1]) && existing[1] != initialA ? existing[1] : null;
      if (initialB == null) foreach (string name in profiles) if (name != initialA) { initialB = name; break; }
      a.SelectedItem = initialA; b.SelectedItem = initialB;
      Button cancel = new Button { Text = "Peruuta", Location = new Point(181, 159), Size = new Size(87, 29), DialogResult = DialogResult.Cancel };
      Button save = new Button { Text = "Tallenna", Location = new Point(276, 159), Size = new Size(87, 29) };
      save.Click += delegate {
        if (a.SelectedItem == null || b.SelectedItem == null || (string)a.SelectedItem == (string)b.SelectedItem) {
          MessageBox.Show(form, "Valitse kaksi eri verkkoa.", "Wi-Fi-vaihtaja"); return;
        }
        SaveConfig((string)a.SelectedItem, (string)b.SelectedItem, wifi.CurrentProfile());
        form.DialogResult = DialogResult.OK; form.Close();
      };
      form.AcceptButton = save; form.CancelButton = cancel;
      form.Controls.AddRange(new Control[] { title, labA, labB, a, b, cancel, save });
      form.ShowDialog();
    }
  }
}

internal sealed class TrayContext : ApplicationContext {
  NotifyIcon indicator;
  System.Windows.Forms.Timer timer;
  MenuItem status;
  Icon generated;
  string lastProfile, previousKnown;
  bool initialized;
  public TrayContext() {
    ContextMenu menu = new ContextMenu();
    status = new MenuItem("Tarkistetaan verkkoa...") { Enabled = false };
    menu.MenuItems.Add(status);
    menu.MenuItems.Add("Vaihda verkkoa", delegate { Launch("--toggle"); });
    menu.MenuItems.Add("Valitse verkot...", delegate { Launch("--configure"); });
    menu.MenuItems.Add("Sulje ilmaisin", delegate { ExitThread(); });
    indicator = new NotifyIcon { Icon = SystemIcons.Application, Text = "Wi-Fi-vaihtaja", ContextMenu = menu, Visible = true };
    indicator.DoubleClick += delegate { Launch("--toggle"); };
    timer = new System.Windows.Forms.Timer { Interval = 2000 };
    timer.Tick += delegate { Refresh(); };
    Refresh();
    timer.Start();
  }
  void Launch(string mode) {
    try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath, mode) { UseShellExecute = true }); }
    catch (Exception e) { indicator.BalloonTipText = e.Message; indicator.ShowBalloonTip(2000); }
  }
  static string Crop(string s, int max) { return s.Length <= max ? s : s.Substring(0, max - 1) + "…"; }
  void Refresh() {
    string current = null, letter = "?";
    try {
      using (WifiAdapter wifi = new WifiAdapter()) current = wifi.CurrentProfile();
      string[] config = Program.ReadConfig();
      if (current != null && config != null) {
        if (current == config[0]) letter = "A";
        else if (current == config[1]) letter = "B";
      }
    } catch { }
    status.Text = Crop("Yhdistetty: " + (current ?? "ei tietoa"), 72);
    indicator.Text = Crop("Wi-Fi: " + (current ?? "ei tietoa"), 63);
    if (initialized && letter != "?" && letter != lastProfile && (lastProfile != null || previousKnown != null)) {
      indicator.BalloonTipTitle = "Wi-Fi vaihdettu";
      indicator.BalloonTipText = "Yhdistetty verkkoon " + current + " (" + letter + ").";
      indicator.BalloonTipIcon = ToolTipIcon.Info;
      indicator.ShowBalloonTip(1800);
    }
    if (letter != "?") lastProfile = letter;
    if (current != null) previousKnown = current;
    initialized = true;
    string label = String.IsNullOrWhiteSpace(current) ? "?" : current.Trim();
    // Ilmoitusalueen kuvakkeessa näkyy verkkonimen alku; koko nimi näkyy vihjetekstissä.
    if (label.Length > 2) label = label.Substring(0, 2);
    DrawLetter(label.ToUpperInvariant(), letter);
  }
  string drawn;
  void DrawLetter(string label, string networkSide) {
    string key = label + "|" + networkSide;
    if (key == drawn) return; drawn = key;
    Color fill = networkSide == "A" ? Color.FromArgb(34, 112, 205) :
      networkSide == "B" ? Color.FromArgb(18, 144, 118) : Color.Gray;
    using (Bitmap bitmap = new Bitmap(32, 32))
    using (Graphics g = Graphics.FromImage(bitmap))
    using (SolidBrush brush = new SolidBrush(fill))
    using (Font font = new Font("Segoe UI", label.Length > 1 ? 15 : 20, FontStyle.Bold, GraphicsUnit.Pixel)) {
      g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
      g.Clear(Color.Transparent); g.FillEllipse(brush, 1, 1, 30, 30);
      TextRenderer.DrawText(g, label, font, new Rectangle(1, 0, 30, 31), Color.White,
        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
      IntPtr handle = bitmap.GetHicon();
      try {
        Icon next = (Icon)Icon.FromHandle(handle).Clone();
        indicator.Icon = next;
        if (generated != null) generated.Dispose();
        generated = next;
      } finally { DestroyIcon(handle); }
    }
  }
  [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);
  protected override void ExitThreadCore() {
    timer.Stop(); timer.Dispose(); indicator.Visible = false; indicator.Dispose();
    if (generated != null) generated.Dispose();
    base.ExitThreadCore();
  }
}

internal sealed class WifiAdapter : IDisposable {
  IntPtr handle;
  Guid adapter;
  bool disposed;
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct InterfaceInfo {
    public Guid Id;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
    public int State;
  }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct ProfileInfo {
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
    public uint Flags;
  }
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  struct ConnectionParameters {
    public int Mode;
    [MarshalAs(UnmanagedType.LPWStr)] public string Profile;
    public IntPtr Ssid;
    public IntPtr Bssid;
    public int BssType;
    public uint Flags;
  }
  [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr client);
  [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr client, IntPtr reserved);
  [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserved, out IntPtr list);
  [DllImport("wlanapi.dll")] static extern uint WlanGetProfileList(IntPtr client, ref Guid id, IntPtr reserved, out IntPtr list);
  [DllImport("wlanapi.dll")] static extern uint WlanQueryInterface(IntPtr client, ref Guid id, int opcode, IntPtr reserved, out uint size, out IntPtr data, IntPtr type);
  [DllImport("wlanapi.dll")] static extern uint WlanConnect(IntPtr client, ref Guid id, ref ConnectionParameters p, IntPtr reserved);
  [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr data);
  static void Ensure(uint status, string activity) {
    if (status != 0) throw new Win32Exception((int)status, activity + " (" + status + ")");
  }
  public WifiAdapter() {
    uint version;
    Ensure(WlanOpenHandle(2, IntPtr.Zero, out version, out handle), "Wi-Fi-rajapinta");
    IntPtr list = IntPtr.Zero;
    try {
      Ensure(WlanEnumInterfaces(handle, IntPtr.Zero, out list), "Wi-Fi-sovittimet");
      int count = Marshal.ReadInt32(list, 0);
      if (count == 0) throw new InvalidOperationException("Wi-Fi-sovitinta ei löytynyt.");
      int stride = Marshal.SizeOf(typeof(InterfaceInfo));
      InterfaceInfo first = new InterfaceInfo(); bool connected = false;
      for (int i = 0; i < count; i++) {
        InterfaceInfo info = (InterfaceInfo)Marshal.PtrToStructure(IntPtr.Add(list, 8 + i * stride), typeof(InterfaceInfo));
        if (i == 0) first = info;
        if (info.State == 1) { adapter = info.Id; connected = true; break; }
      }
      if (!connected) adapter = first.Id;
    } finally { if (list != IntPtr.Zero) WlanFreeMemory(list); }
  }
  public List<string> Profiles() {
    IntPtr list = IntPtr.Zero;
    try {
      Ensure(WlanGetProfileList(handle, ref adapter, IntPtr.Zero, out list), "Tallennetut verkot");
      int count = Marshal.ReadInt32(list, 0);
      int stride = Marshal.SizeOf(typeof(ProfileInfo));
      List<string> profiles = new List<string>();
      for (int i = 0; i < count; i++) {
        ProfileInfo info = (ProfileInfo)Marshal.PtrToStructure(IntPtr.Add(list, 8 + i * stride), typeof(ProfileInfo));
        if (!String.IsNullOrWhiteSpace(info.Name) && !profiles.Contains(info.Name)) profiles.Add(info.Name);
      }
      return profiles;
    } finally { if (list != IntPtr.Zero) WlanFreeMemory(list); }
  }
  public bool HasProfiles(string[] chosen) {
    List<string> names = Profiles();
    return names.Contains(chosen[0]) && names.Contains(chosen[1]);
  }
  public string CurrentProfile() {
    IntPtr data = IntPtr.Zero;
    try {
      uint size;
      uint status = WlanQueryInterface(handle, ref adapter, 7, IntPtr.Zero, out size, out data, IntPtr.Zero);
      if (status != 0 || data == IntPtr.Zero || size < 520 || Marshal.ReadInt32(data, 0) != 1) return null;
      return Marshal.PtrToStringUni(IntPtr.Add(data, 8), 256).TrimEnd('\0');
    } finally { if (data != IntPtr.Zero) WlanFreeMemory(data); }
  }
  public void Connect(string profile) {
    ConnectionParameters parameters = new ConnectionParameters {
      Mode = 0, Profile = profile, Ssid = IntPtr.Zero, Bssid = IntPtr.Zero, BssType = 3, Flags = 0
    };
    Ensure(WlanConnect(handle, ref adapter, ref parameters, IntPtr.Zero), "Verkon vaihto");
  }
  public void Dispose() {
    if (!disposed) {
      if (handle != IntPtr.Zero) WlanCloseHandle(handle, IntPtr.Zero);
      disposed = true;
    }
  }
}
