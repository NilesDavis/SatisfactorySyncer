using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace SatisfactorySyncer
{
  public class MainViewModel : ViewModelBase
  {
    private Brush _syncColor = Brushes.YellowGreen;
    public Brush SyncColor
    {
      get { return _syncColor; }
      set { _syncColor = value; }
    }

    private string _saveGamesLocalPath;
    /// <summary>
    /// String to the local savegames
    /// </summary>
    public string SaveGamesLocalPath
    {
      get { return _saveGamesLocalPath; }
      set
      {
        _saveGamesLocalPath = value;
        try
        {
          DirectoryInfo di = new DirectoryInfo(_saveGamesLocalPath);
          if (di.Exists)
          {
            //FileSystemWatcher starten
            AppendLog($"SaveGamesLocalPath={_saveGamesLocalPath}");
            List<string> distinctsessionslocal;
            this.LocalSaveFiles = ScanForSessions(_saveGamesLocalPath, out distinctsessionslocal);
            if (distinctsessionslocal != null)
              this.LocalSessionList = distinctsessionslocal;
            this.LocalFSW = new FileSystemWatcher(_saveGamesLocalPath, "*.sav");
          }
          RaisePropertyChanged();
        }
        catch (Exception ex)
        {

        }
      }
    }

    private List<string> _localSessionList;
    /// <summary>
    /// List of local session names
    /// </summary>
    public List<string> LocalSessionList
    {
      get { return _localSessionList; }
      set { _localSessionList = value; RaisePropertyChanged(); }
    }

    private string _selectedLocalSession;
    /// <summary>
    /// name of selected session
    /// </summary>
    public string SelectedLocalSession
    {
      get { return _selectedLocalSession; }
      set
      {
        _selectedLocalSession = value;
        RaisePropertyChanged();
        try
        {
          PushCommand.RaiseCanExecuteChanged();
        }
        catch (Exception)
        {
          //System.Diagnostics.Debugger.Break();
        }

        //nun das neueste Datum raussuchen
        if (this.LocalSaveFiles != null && this.CloudSaveFiles != null)
        {
          var alllocalsessions = this.LocalSaveFiles.Where(sf => sf.Item2.Session == _selectedLocalSession).Select(sf => sf);
          if (alllocalsessions.Count() > 0)
            this.SelectedLocalSessionDate = alllocalsessions.First().Item1;
          else
            this.SelectedLocalSessionDate = new DateTime();


          var allcloudsessions = this.CloudSaveFiles.Where(sf => sf.Item2.Session == _selectedLocalSession).Select(sf => sf);
          if (allcloudsessions.Count() > 0)
            this.SelectedCloudSessionDate = allcloudsessions.First().Item1;
          else
            this.SelectedCloudSessionDate = new DateTime();
        }

        //jetzt könnte ich schauen, ob es die Session auch in der cloud gibt:
        if (this.CloudSessionList.Contains(_selectedLocalSession))
        {
          this._selectedCloudSession = _selectedLocalSession;
          RaisePropertyChanged("SelectedCloudSession");
          try
          {
            this.PullCommand.RaiseCanExecuteChanged();
            this.PushCommand.RaiseCanExecuteChanged();
          }
          catch (Exception) { }
        }
      }
    }

    private List<Tuple<DateTime, SaveFileInfo>> _localSaveFiles;
    /// <summary>
    /// List of local SaveGames
    /// </summary>
    public List<Tuple<DateTime, SaveFileInfo>> LocalSaveFiles
    {
      get { return _localSaveFiles; }
      set { _localSaveFiles = value; }
    }

    public DateTime _selectedLocalSessionDate;
    /// <summary>
    /// Date of the selected local session
    /// </summary>
    public DateTime SelectedLocalSessionDate
    {
      get { return _selectedLocalSessionDate; }
      set { _selectedLocalSessionDate = value; RaisePropertyChanged(); }
    }

    //Helpers for SaveGame decode
    const string sessionkey = "sessionName=";
    const string sessionendkey = "?";
    const string visikey = "Visibility=";
    string visiendkey = Encoding.Default.GetString(new byte[] { (byte)0, (byte)14, (byte)0 });

    /// <summary>
    /// Scans a path for Sessions
    /// </summary>
    /// <param name="savegamespath">path where all savegames are analysed</param>
    /// <param name="distincsessions">also returns a List of strings representing the distinct sessions</param>
    /// <param name="sessionname">find sessions matching a given name</param>
    /// <returns></returns>
    private List<Tuple<DateTime, SaveFileInfo>> ScanForSessions(string savegamespath, out List<string> distincsessions, string sessionname = "")
    {
      //if (syncing)
      //{
      //  distincsessions = null;
      //  return null;
      //}
      FileInfo[] fis = new DirectoryInfo(savegamespath).GetFiles("*.sav");
      List<SaveFileInfo> savefiles = new List<SaveFileInfo>();
      for (int f = 0; f < fis.Length; f++)
      {
        FileInfo fi = fis[f];
        string line = "";
        bool foundsession = false;
        SaveFileInfo sfi = new SaveFileInfo() { FileInfo = fi, lasteditdate = fi.LastWriteTime };
        using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
          using (StreamReader sr = new StreamReader(fs))
          {
            int linecounter = 0;
            while (!foundsession && !sr.EndOfStream)
            {
              line = sr.ReadLine();
              linecounter++;

              int startindex = 0;
              int endindex = 0;

              if (line.Contains(sessionkey))
              {
                startindex = line.IndexOf(sessionkey) + sessionkey.Length;
                endindex = line.IndexOf(sessionendkey, startindex + 1);
                if (startindex > -1 && endindex > -1)
                {
                  sfi.Session = line.Substring(startindex, endindex - startindex);
                  foundsession = true;
                }
              }
              if (line.Contains(visikey))
              {
                startindex = line.IndexOf(visikey) + visikey.Length;
                endindex = line.IndexOf(visiendkey, startindex + 1);
                if (startindex > -1 && endindex > -1)
                {
                  sfi.Visibility = line.Substring(startindex, endindex - startindex);
                }
              }
            }
            if (foundsession)
              savefiles.Add(sfi);
            else
            {
#if DEBUG
              Debugger.Break();
#endif
            }
            sr.Close();
            sr.Dispose();
          }
          fs.Close();
          fs.Dispose();
        }
      }

      distincsessions = savefiles.Select(s => s.Session).Distinct().ToList();
      if (String.IsNullOrEmpty(sessionname))
      {
        List<Tuple<DateTime, SaveFileInfo>> savetimes = savefiles.Select(s => new Tuple<DateTime, SaveFileInfo>(s.lasteditdate, s)).OrderByDescending(t => t.Item1).ToList();
        return savetimes;
      }
      else
      {
        int matchingsession = savefiles.Where(s => s.Session == sessionname).Count();
        if (matchingsession == 0)
        {
#if DEBUG
          Debugger.Break();
#endif
        }
        //sessionname = "Quarantine";
        List<Tuple<DateTime, SaveFileInfo>> savetimes = savefiles.Where(s => s.Session == sessionname).Select(s => new Tuple<DateTime, SaveFileInfo>(s.lasteditdate, s)).OrderByDescending(t => t.Item1).ToList();
        return savetimes;
      }
    }

    private FileSystemWatcher _localFSW;
    public FileSystemWatcher LocalFSW
    {
      get { return _localFSW; }
      set
      {
        if (_localFSW != value)
        {
          if (_localFSW != null)
          {
            _localFSW.Created -= localCreated;
            _localFSW.Changed -= localChanged;
            _localFSW.Deleted -= localDeleted;
          }
          _localFSW = value;
          if (_localFSW != null)
          {
            _localFSW.EnableRaisingEvents = true;
            _localFSW.Created += localCreated;
            _localFSW.Changed += localChanged;
            _localFSW.Deleted += localDeleted;
          }
        }
      }
    }

    private void localDeleted(object sender, FileSystemEventArgs e)
    {
      //eher nicht löschen
      //AppendLog($"LocalDeleted: {e.ChangeType} | {e.Name}");
      Refresh();
    }

    private void localChanged(object sender, FileSystemEventArgs e)
    {
      //ab in die Cloud
      Refresh(); //AppendLog($"LocalChanged: {e.ChangeType} | {e.Name}");
    }

    private void localCreated(object sender, FileSystemEventArgs e)
    {
      //ab in die Cloud
      Refresh(); //      AppendLog($"LocalCreated: {e.ChangeType} | {e.Name}");
    }

    private string _saveGamesCloudPath;
    public string SaveGamesCloudPath
    {
      get { return _saveGamesCloudPath; }
      set
      {
        _saveGamesCloudPath = value;
        try
        {
          DirectoryInfo di = new DirectoryInfo(_saveGamesCloudPath);
          if (di.Exists)
          {
            //FileSystemWatcher starten
            AppendLog($"SaveGamesCloudPath={_saveGamesCloudPath}");
            List<string> distinctsessionscloud;
            this.CloudSaveFiles = ScanForSessions(_saveGamesCloudPath, out distinctsessionscloud);
            if (distinctsessionscloud != null)
              this.CloudSessionList = distinctsessionscloud;
            this.CloudFSW = new FileSystemWatcher(_saveGamesCloudPath, "*.sav");

          }
          RaisePropertyChanged();
        }
        catch (Exception ex)
        {

        }
      }
    }

    private List<string> _cloudSessionList;
    public List<string> CloudSessionList
    {
      get { return _cloudSessionList; }
      set { _cloudSessionList = value; RaisePropertyChanged(); }
    }

    private string _selectedCloudSession;
    public string SelectedCloudSession
    {
      get { return _selectedCloudSession; }
      set
      {
        _selectedCloudSession = value;
        RaisePropertyChanged();
        try
        {
          PullCommand.RaiseCanExecuteChanged();
        }
        catch (Exception)
        {
          System.Diagnostics.Debugger.Break();
        }

        //nun das neueste Datum raussuchen
        if (this.LocalSaveFiles != null && this.CloudSaveFiles != null)
        {
          var alllocalsessions = this.LocalSaveFiles.Where(sf => sf.Item2.Session == _selectedCloudSession).Select(sf => sf);
          if (alllocalsessions.Count() > 0)
            this.SelectedLocalSessionDate = alllocalsessions.First().Item1;
          else
            this.SelectedLocalSessionDate = new DateTime();


          var allcloudsessions = this.CloudSaveFiles.Where(sf => sf.Item2.Session == _selectedCloudSession).Select(sf => sf);
          if (allcloudsessions.Count() > 0)
            this.SelectedCloudSessionDate = allcloudsessions.First().Item1;
          else
            this.SelectedCloudSessionDate = new DateTime();
        }

        //jetzt könnte ich schauen, ob es die Session auch lokal gibt:
        if (this.LocalSessionList.Contains(_selectedCloudSession))
        {
          this._selectedLocalSession = _selectedCloudSession;
          RaisePropertyChanged("SelectedLocalSession");
          try
          {
            this.PullCommand.RaiseCanExecuteChanged();
            this.PushCommand.RaiseCanExecuteChanged();
          }
          catch (Exception) { }
        }
      }
    }

    public DateTime _selectedCloudSessionDate;
    public DateTime SelectedCloudSessionDate
    {
      get { return _selectedCloudSessionDate; }
      set { _selectedCloudSessionDate = value; RaisePropertyChanged(); }
    }

    private List<Tuple<DateTime, SaveFileInfo>> _cloudSaveFiles;
    public List<Tuple<DateTime, SaveFileInfo>> CloudSaveFiles
    {
      get { return _cloudSaveFiles; }
      set { _cloudSaveFiles = value; }
    }

    private FileSystemWatcher _cloudFSW;
    public FileSystemWatcher CloudFSW
    {
      get { return _cloudFSW; }
      set
      {
        if (_cloudFSW != value)
        {
          if (_cloudFSW != null)
          {
            _cloudFSW.Created -= CloudCreated;
            _cloudFSW.Changed -= CloudChanged;
            _cloudFSW.Deleted -= CloudDeleted;
          }
          _cloudFSW = value;
          if (_cloudFSW != null)
          {
            _cloudFSW.EnableRaisingEvents = true;
            _cloudFSW.Created += CloudCreated;
            _cloudFSW.Changed += CloudChanged;
            _cloudFSW.Deleted += CloudDeleted;
          }
        }
      }
    }

    private void CloudDeleted(object sender, FileSystemEventArgs e)
    {
      Refresh(); //AppendLog($"CloudDeleted: {e.ChangeType} | {e.Name}");
    }

    private void CloudChanged(object sender, FileSystemEventArgs e)
    {
      Refresh(); //AppendLog($"CloudChanged: {e.ChangeType} | {e.Name}");
    }

    private void CloudCreated(object sender, FileSystemEventArgs e)
    {
      Refresh(); //AppendLog($"CloudCreated: {e.ChangeType} | {e.Name}");
    }

    private RelayCommand _locateLocalFolderCommand;
    public RelayCommand LocateLocalFolderCommand
    {
      get
      {
        if (_locateLocalFolderCommand == null)
          _locateLocalFolderCommand = new RelayCommand(LocateLocalFolder);
        return _locateLocalFolderCommand;
      }
    }

    private RelayCommand _locateCloudFolderCommand;
    public RelayCommand LocateCloudFolderCommand
    {
      get
      {
        if (_locateCloudFolderCommand == null)
          _locateCloudFolderCommand = new RelayCommand(LocateCloudFolder);
        return _locateCloudFolderCommand;
      }
    }

    private void LocateLocalFolder()
    {
      Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
      ofd.Title = "Locate a arbitrary SaveGame file in local SaveGames Folder";
      ofd.Filter = "*.sav|*.sav";
      if (String.IsNullOrEmpty(this.SaveGamesLocalPath))
        ofd.InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FactoryGame\\Saved\\SaveGames\\");
      else
      {
        string directorystring = Path.GetDirectoryName(this.SaveGamesLocalPath);
        if (!directorystring.EndsWith("\\"))
          directorystring += "\\";
        ofd.InitialDirectory = directorystring;
      }
      bool? dialogresult = ofd.ShowDialog();
      if (dialogresult.HasValue && dialogresult.Value)
      {
        this.SaveGamesLocalPath = Path.GetDirectoryName(ofd.FileName);
      }
    }

    private void LocateCloudFolder()
    {
      Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
      ofd.Title = "Locate SaveGame in Cloud folder";
      ofd.Filter = "*.sav|*.sav";
      if (!String.IsNullOrEmpty(this.SaveGamesCloudPath))
      {
        string directorystring = Path.GetDirectoryName(this.SaveGamesCloudPath);
        if (!directorystring.EndsWith("\\"))
          directorystring += "\\";
        ofd.InitialDirectory = directorystring;
      }
      bool? dialogresult = ofd.ShowDialog();
      if (dialogresult.HasValue && dialogresult.Value)
      {
        this.SaveGamesCloudPath = Path.GetDirectoryName(ofd.FileName);
      }
    }

    private RelayCommand<object> _openPathCommand;
    public RelayCommand<object> OpenPathCommand
    {
      get
      {
        if (_openPathCommand == null)
          _openPathCommand = new RelayCommand<object>(OpenPath);
        return _openPathCommand;
      }
    }

    private void OpenPath(object obj)
    {
      if (obj is string)
      {
        string path = (string)obj;
        Process.Start(path);
      }
      else
      {
#if DEBUG
        System.Diagnostics.Debugger.Break();
#endif
      }
    }

    #region Log

    private StringBuilder _logBuilder = new StringBuilder();
    public StringBuilder LogBuilder
    {
      get { return _logBuilder; }
      set { _logBuilder = value; }
    }

    public string LogString
    {
      get
      {
        string log = "";
        try
        {
          log = LogBuilder.ToString();

        }
        catch (Exception) { }
        return log;
      }
    }

    public void AppendLog(string logstring)
    {
      this.LogBuilder.AppendLine(
        DateTime.Now.ToShortDateString() + " " + DateTime.Now.ToShortTimeString()
        + "   " +
        logstring);
      RaisePropertyChanged("LogString");
    }

    #endregion Log

    private volatile bool syncing = false;

    private RelayCommand _syncCommand;
    public RelayCommand SyncCommand
    {
      get
      {
        if (_syncCommand == null)
          _syncCommand = new RelayCommand(Refresh);
        return _syncCommand;
      }
    }

    private RelayCommand _pushCommand;
    public RelayCommand PushCommand
    {
      get
      {
        if (_pushCommand == null)
          _pushCommand = new RelayCommand(Push, () => !String.IsNullOrEmpty(this.SelectedLocalSession));
        return _pushCommand;
      }
    }

    private void Push()
    {
      AppendLog("Pushing");
      this.LocalFSW.EnableRaisingEvents = false;
      this.CloudFSW.EnableRaisingEvents = false;

      int waitcounter = 0;
      while (syncing)
      {
        Task.Delay(250);
        waitcounter++;
        if (waitcounter > 10)
          return;
      }
      FileInfo[] filestocopy = this.LocalSaveFiles.Where(sf => sf.Item2.Session == this.SelectedLocalSession).Select(sf => sf.Item2.FileInfo).ToArray();
      int copied = 0;
      int same = 0;
      int notexisting = 0;
      for (int i = 0; i < filestocopy.Length; i++)
      {
        FileInfo fi = filestocopy[i];
        FileInfo targetfi = new FileInfo(Path.Combine(this.SaveGamesCloudPath, fi.Name));
        if (!targetfi.Exists)
        {
          notexisting++;
          File.Copy(fi.FullName, targetfi.FullName, false);
        }
        else
        {
          //aktueller?
          if (targetfi.LastWriteTimeUtc > fi.LastWriteTimeUtc)
          {
#if DEBUG
            Debugger.Break();
#endif
          }
          else
          {
            if (targetfi.LastWriteTimeUtc != fi.LastWriteTimeUtc
              //|| targetfi.LastAccessTimeUtc != fi.LastAccessTimeUtc
              || targetfi.Length != fi.Length)
            {  //dann sind sie wohl nicht gleich
              try
              {
                File.Copy(fi.FullName, targetfi.FullName, true);
              }
              catch (Exception es)
              {
#if DEBUG
                Debugger.Break();
#endif
              }

              copied++;
            }
            else
            {
              //gleich
              same++;
            }
          }
        }
      }

      this.LocalFSW.EnableRaisingEvents = true;
      this.CloudFSW.EnableRaisingEvents = true;
      Refresh();

      ////this.LocalFSW.EnableRaisingEvents = false;
      ////this.CloudFSW.EnableRaisingEvents = false;
      //AppendLog("Pushing");
      //syncing = true;
      //FolderDiff fd = new FolderDiff(this.SaveGamesLocalPath, this.SaveGamesCloudPath);
      //fd.CompareEvent += Diff_CompareEvent;
      //cmplg.Clear();
      //fd.Compare();

      //FolderSync fs = new FolderSync(this.SaveGamesLocalPath, this.SaveGamesCloudPath, FileActions.Ignore, FileActions.Copy, FileActions.OverwriteOlder);
      ////fs.diff.CompareEvent += Diff_CompareEvent;
      //fs.ErrorEvent += Fs_ErrorEvent;
      //fs.Sync();
      //syncing = false;

      ////this.LocalFSW.EnableRaisingEvents = true;
      ////this.CloudFSW.EnableRaisingEvents = true;

      AppendLog($"Pushed({filestocopy.Length}): copied {copied}, same {same}");
    }

    private void Fs_ErrorEvent(Exception e, string[] files)
    {
      AppendLog($"{e.Message}");
    }

    private RelayCommand _pullCommand;
    public RelayCommand PullCommand
    {
      get
      {
        if (_pullCommand == null)
          _pullCommand = new RelayCommand(Pull, () => !String.IsNullOrEmpty(this.SelectedCloudSession));
        return _pullCommand;
      }
    }

    private void Pull()
    {
      this.LocalFSW.EnableRaisingEvents = false;
      this.CloudFSW.EnableRaisingEvents = false;

      //warnen, falls Cloud älter ist!
      //ansonsten alle Dateinamen holen und die Dateien ins Ziel kopieren
      int waitcounter = 0;
      while (syncing)
      {
        Task.Delay(250);
        waitcounter++;
        if (waitcounter > 10)
          return;
      }
      AppendLog("Pulling");
      FileInfo[] filestocopy = this.CloudSaveFiles.Where(sf => sf.Item2.Session == this.SelectedCloudSession).Select(sf => sf.Item2.FileInfo).ToArray();
      int same = 0;
      int copied = 0;
      int notexisting = 0;
      for (int i = 0; i < filestocopy.Length; i++)
      {
        FileInfo fi = filestocopy[i];
        FileInfo targetfi = new FileInfo(Path.Combine(this.SaveGamesLocalPath, fi.Name));
        if (!targetfi.Exists)
        {
          try
          {
            File.Copy(fi.FullName, targetfi.FullName, false);
            notexisting++;
          }
          catch (Exception es)
          {
#if DEBUG
            Debugger.Break();
#endif
          }
        }
        else
        {
          //aktueller?
          if (targetfi.LastWriteTimeUtc != fi.LastWriteTimeUtc
            //|| targetfi.LastAccessTimeUtc != fi.LastAccessTimeUtc
            || targetfi.Length != fi.Length)
          {  //dann sind sie wohl nicht gleich
            try
            {
              File.Copy(fi.FullName, targetfi.FullName, true);
              copied++;
            }
            catch (Exception es)
            {
#if DEBUG
              Debugger.Break();
#endif
            }
          }
          else
            same++;
        }
      }

      this.LocalFSW.EnableRaisingEvents = true;
      this.CloudFSW.EnableRaisingEvents = true;

      Refresh();

      ////this.LocalFSW.EnableRaisingEvents = false;
      ////this.CloudFSW.EnableRaisingEvents = false;
      //AppendLog("Pulling");
      //syncing = true;
      //FolderSync fs = new FolderSync(this.SaveGamesCloudPath, this.SaveGamesLocalPath, FileActions.Ignore, FileActions.Copy, FileActions.OverwriteOlder);
      //fs.diff.CompareEvent += Diff_CompareEvent;
      //fs.ErrorEvent += Fs_ErrorEvent;
      //fs.Sync();
      //syncing = false;
      ////this.LocalFSW.EnableRaisingEvents = true;
      ////this.CloudFSW.EnableRaisingEvents = true;

      AppendLog($"Pulled({filestocopy.Length}): copied {copied}, same {same}");
    }

    private void Refresh()
    {
      syncing = true;
      //this.LocalFSW.EnableRaisingEvents = false;
      //this.CloudFSW.EnableRaisingEvents = false;

      //FolderDiff fDiff = new FolderDiff(this.SaveGamesLocalPath, this.SaveGamesCloudPath);
      //fDiff.CompareEvent += FDiff_CompareEvent;
      //// The event raised for each file
      //fDiff.Compare(); // Starts comparing

      //this.LocalFSW.EnableRaisingEvents = true;
      //this.CloudFSW.EnableRaisingEvents = true;

      AppendLog("Refresh");
      string localpath = this.SaveGamesLocalPath;
      string cloudpath = this.SaveGamesCloudPath;
      this.SaveGamesLocalPath = "";
      this.SaveGamesCloudPath = "";
      this.SaveGamesLocalPath = localpath;
      this.SaveGamesCloudPath = cloudpath;

      if (this.LocalSessionList.Contains(this.SelectedLocalSession))
        this.SelectedLocalSession = this.SelectedLocalSession;

      if (this.CloudSessionList.Contains(this.SelectedCloudSession))
        this.CloudSessionList = this.CloudSessionList;

      syncing = false;
      AppendLog("Refresh");
    }


    private RelayCommand _openCalculatorCommand;
    public RelayCommand OpenCalculatorCommand
    {
      get
      {
        if (_openCalculatorCommand == null)
          _openCalculatorCommand = new RelayCommand(OpenCalculator);
        return _openCalculatorCommand;
      }
    }

    private void OpenCalculator()
    {
      System.Windows.MessageBox.Show("Your really thought that woudl work???", "REALLY?", System.Windows.MessageBoxButton.YesNo);
      string websitestring = "https://satisfactory-calculator.com/en/interactive-map";
    }

    #region Ctor

    public MainViewModel()
    {
      Properties.Settings.Default.Reload();
      this.SaveGamesLocalPath = Properties.Settings.Default.LocalPath;
      this.SaveGamesCloudPath = Properties.Settings.Default.CloudPath;

      if (string.IsNullOrEmpty(this.SaveGamesLocalPath))
        this.SaveGamesLocalPath = LocateDefaultLocalSaveGames();

      if (string.IsNullOrEmpty(this.SaveGamesCloudPath))
        this.SaveGamesCloudPath = LocateDefaultDropboxFolder();
    }

    ~MainViewModel()
    {
      if (this.LocalFSW != null)
      {
        this.LocalFSW.EnableRaisingEvents = false;
        this.LocalFSW.Dispose();
      }
      if (this.CloudFSW != null)
      {
        this.CloudFSW.EnableRaisingEvents = false;
        this.CloudFSW.Dispose();
      }
      Properties.Settings.Default.LocalPath = this.SaveGamesLocalPath;
      Properties.Settings.Default.CloudPath = this.SaveGamesCloudPath;
      Properties.Settings.Default.Save();
    }

    #endregion Ctor

    private string LocateDefaultDropboxFolder()
    {
      string result = "";
      string dropboxinfofile = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), @"Dropbox\info.json");
      if (new FileInfo(dropboxinfofile).Exists)
      {
        string jsonstring = "";
        using (StreamReader sr = new StreamReader(dropboxinfofile))
        {
          jsonstring = sr.ReadToEnd();
          sr.Close();
          sr.Dispose();
        }
        //read private folder from info file
        DropboxInfo dropboxsettings = Newtonsoft.Json.JsonConvert.DeserializeObject<DropboxInfo>(jsonstring);
        if (dropboxsettings.personal != null)
        {
          result = dropboxsettings.personal.path;
        }
        else if (dropboxsettings.business != null)
        {
          result = dropboxsettings.business.path;
        }

      }
      return result;
    }

    private string LocateDefaultLocalSaveGames()
    {
      string result = Path.Combine(Environment.GetEnvironmentVariable("LocalAppData"), @"FactoryGame\Saved\SaveGames");
      return result;
    }
  }

  public struct SaveFileInfo
  {
    public FileInfo FileInfo;
    public string Session;
    public string Visibility;
    public DateTime lasteditdate;
  }

  public class DropboxInfo
  {
    public DropboxSubInfo personal { get; set; }
    public DropboxSubInfo business { get; set; }
  }
  public class DropboxSubInfo
  {
    public string path { get; set; }
    public long host { get; set; }
    public bool is_team { get; set; }
    public string subscription_type { get; set; }
  }

}
