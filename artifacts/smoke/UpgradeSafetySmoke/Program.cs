using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Data.Sqlite;
using PromptManager;
using PromptManager.Data;
using PromptManager.Models;
using PromptManager.Services;
using PromptManager.ViewModels;

internal static class Program
{
    private static int _checks;
    private static string _root = "";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--recover-library")
        {
            var repository = new PromptRepository(args[1]);
            var before = TableSnapshot(args[1], "Prompts") + TableSnapshot(args[1], "Folders");
            var count = repository.DetachedHistoryCount();
            var safety = repository.RecoverDetachedHistory();
            if (before != TableSnapshot(args[1], "Prompts") + TableSnapshot(args[1], "Folders")) throw new InvalidOperationException("Current records changed unexpectedly.");
            PromptRepository.ValidateBackupDatabase(args[1]);
            Console.WriteLine($"Recovered {count} detached history entries. Current prompts and folders are unchanged. Safety backup: {safety}");
            return 0;
        }
        if (args.Length == 2 && args[0] == "--verify-library")
        {
            var directory=Path.Combine(Path.GetTempPath(),"PromptManagerCompatibility-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            try
            {
                var source=new PromptRepository(args[1]); var copyPath=Path.Combine(directory,"copy.db"); source.BackupTo(copyPath);
                var copy=new PromptRepository(copyPath); copy.Initialize();
                var rows=copy.GetPrompts(PromptFilterKind.All,null,null).Concat(copy.GetPrompts(PromptFilterKind.Retired,null,null)).DistinctBy(p=>p.Id).ToArray();
                foreach (var row in rows) copy.GetPromptVersions(row.Id);
                Console.WriteLine($"Library compatibility passed on a snapshot copy: {rows.Length} current prompts; {copy.GetRecoveryHistory().Count} recovered entries. Original library was not migrated.");
            }
            finally {Directory.Delete(directory,true);}
            return 0;
        }
        _root = Path.Combine(Path.GetTempPath(), "PromptManagerUpgradeTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        try
        {
            if (args.Length == 2 && args[0] == "--package") { PackageChecks(args[1]); Console.WriteLine($"PASS: {_checks} package/install checks."); return 0; }
            LibraryChecks(); ImportChecks(); RecoveryChecks(); InstallerChecks(); UiChecks(args.FirstOrDefault());
            Console.WriteLine($"PASS: {_checks} upgrade checks. Tests used isolated temporary libraries.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(_root, true); }
    }

    private static PromptRepository NewLibrary(string name)
    {
        var repository = new PromptRepository(Path.Combine(_root, name + ".db")); repository.Initialize(); return repository;
    }

    private static void LibraryChecks()
    {
        var r = NewLibrary("library");
        Check(r.GetPrompts(PromptFilterKind.All, null, null).Count == 0, "New library starts empty");
        var a = r.CreateFolder(null, "A"); var b = r.CreateFolder(a, "B"); var c = r.CreateFolder(null, "C");
        Reject(() => r.CreateFolder(null, "a"), "Duplicate sibling name rejected");
        Reject(() => r.CreateFolder(null, "  "), "Blank folder rejected");
        Reject(() => r.UpdateFolder(a, "A", b), "Folder cycle rejected");
        r.UpdateFolder(b, "Renamed", c);
        var id = r.CreatePrompt(b); var p = r.GetPromptById(id)!;
        Check(p.FolderPath == "C / Renamed", "Rename/move folder path updates");
        p.Title = "Protocol {{Unit}}"; p.Body = "line\nline\n\nlast"; p.Type = "Custom"; p.Tags = "keyword"; p.Notes = "note";
        p = r.SavePrompt(p, "Edit fields");
        Check(r.GetPromptTypes().Contains("Custom"), "Custom type reused");
        Check(r.GetPrompts(PromptFilterKind.All, null, "keyword").Single().Id == id, "Search tags");
        Check(r.GetPrompts(PromptFilterKind.All, c, "note").Single().Id == id, "Search notes and descendant scope");
        var stale = r.GetPromptById(id)!;
        p.Title = "Latest"; p = r.SavePrompt(p, "New title"); stale.Body = "stale body";
        Reject(() => r.SavePrompt(stale, "Stale edit"), "Stale save rejected");
        Check(r.GetPromptById(id)!.Title == "Latest" && stale.Body == "stale body", "Conflict keeps database and caller edits");
        p.Status = PromptStatus.InReview; p = r.SavePrompt(p, "Review");
        Check(r.GetPrompts(PromptFilterKind.InReview, null, null).Single().Id == id, "In Review filter");
        var version = p.Version; r.ArchivePrompt(id);
        Check(r.GetPromptById(id)!.Version == version + 1 && r.GetPromptVersions(id).Last().IsArchived == true, "Archive snapshot");
        r.RestorePrompt(id);
        Check(r.GetPromptById(id)!.Version == version + 2 && r.GetPromptById(id)!.Status == PromptStatus.Draft, "Archive restore snapshot");
        var id2 = r.CreatePrompt(a);
        r.MovePrompts(new[] { id, id2 }, c, "Batch move");
        Check(r.GetPromptById(id)!.FolderId == c && r.GetPromptById(id2)!.FolderId == c, "Batch move succeeds");
        var oldVersion = r.GetPromptById(id)!.Version;
        Reject(() => r.MovePrompts(new[] { id, int.MaxValue }, a, "Invalid batch"), "Invalid batch rejected");
        Check(r.GetPromptById(id)!.Version == oldVersion, "Batch failure leaves records unchanged");
        r.DeleteFolder(c);
        Check(r.GetPromptById(id)!.FolderId == null && r.GetPromptById(id)!.Version == oldVersion + 1, "Folder deletion relocates with history");
        Check(r.GetPromptVersions(id).Any(v => v.FolderPath == "C / Renamed"), "Historic path survives folder deletion");
        var restored = r.RestoreVersion(id, 2, "Use earlier text");
        Check(restored.Title == "Protocol {{Unit}}" && restored.Status == PromptStatus.Draft && restored.FolderId == null, "Revision restores fields as Draft with missing folder Unfiled");
        Check(r.GetPromptVersions(id).Count == restored.Version, "Restore appends rather than deletes history");
        Reject(() => r.RestoreVersion(id, 2, "Stale restore", 0), "Stale restore rejected");
        r.DeletePrompt(id); r.DeletePrompt(id2); r.Initialize();
        Check(r.GetPrompts(PromptFilterKind.All, null, null).Count == 0, "Deleted library stays empty on reopen");
        var text = "{{A}} {{a}} {{ A }} {{B}}";
        Check(VariableRenderer.Names(text).Count == 2, "Repeated variable names collapse");
        Check(VariableRenderer.Render(text, new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) { ["a"] = "$1\\x" }) == "$1\\x $1\\x $1\\x {{B}}", "Literal variable values and missing values handled");
        var diff = VersionComparison.LineDiff("a\na\n\nb", "a\n\nb\nc");
        Check(diff.Contains("- a") && diff.Contains("+ c") && diff.Contains("  \n".Replace("\n", Environment.NewLine)), "Diff retains repeated/blank lines");
    }

    private static void ImportChecks()
    {
        var source = NewLibrary("source"); var folder = source.CreateFolder(null, "Historical"); var id = source.CreatePrompt(folder);
        var p = source.GetPromptById(id)!; p.Title = "Imported"; p.Notes = "current note"; p.Tags = "current tag"; p = source.SavePrompt(p, "Current metadata");
        var package = source.CreateScopedExport(new[] { id });
        var destination = NewLibrary("destination"); destination.CreateFolder(null, "Pre-existing");
        destination.ImportPackage(package);
        var imported = destination.GetPrompts(PromptFilterKind.All, null, null).Single();
        var first = destination.GetPromptVersions(imported.Id).First();
        Check(imported.FolderId != folder && first.FolderId == imported.FolderId, "Current and historical folder IDs remapped");
        Check(first.Notes == "" && first.Tags == "", "Historic empty fields preserved");
        var broken = new PromptExportPackage { Folders = new() { new() { Id=1,ParentId=2,Name="Cycle" }, new() { Id=2,ParentId=1,Name="Cycle2" } } };
        Reject(() => destination.ImportPackage(broken), "Circular import rejected");
        Check(destination.GetPrompts(PromptFilterKind.All, null, null).Count == 1, "Failed import rolls back");
        var noHistory = new PromptExportPackage { Folders = new() { new() { Id=42,Name="No History" } }, Prompts = new() { new() { Title="Legacy",FolderId=42,Body="Legacy body" } } };
        destination.ImportPackage(noHistory);
        var legacy = destination.GetPrompts(PromptFilterKind.All, null, "Legacy").Single();
        Check(destination.GetPromptVersions(legacy.Id).Single().FolderId == legacy.FolderId, "Synthesized version folder remapped once");
        package.FormatVersion = "9.0";
        Reject(() => destination.ImportPackage(package), "Future export rejected");
        Check(source.CreateScopedExport(Array.Empty<int>()).Prompts.Count == 0, "Empty export scope is not entire library");
    }

    private static void RecoveryChecks()
    {
        var r = NewLibrary("recovery"); var id = r.CreatePrompt(null); var p = r.GetPromptById(id)!; p.Title = "Keep me"; r.SavePrompt(p);
        var backup = Path.Combine(_root, "backup.db"); r.BackupTo(backup);
        var malformed = Path.Combine(_root, "bad.db");
        Sql(malformed, "CREATE TABLE Prompts(Id INTEGER); CREATE TABLE Folders(Id INTEGER); CREATE TABLE PromptVersions(Id INTEGER);");
        Reject(() => r.RestoreFrom(malformed), "Table-only incompatible restore rejected");
        Check(r.GetPromptById(id)!.Title == "Keep me", "Failed restore preserves active library");
        r.CreatePrompt(null); var safety = r.RestoreFrom(backup);
        Check(r.GetPrompts(PromptFilterKind.All, null, null).Count == 1 && File.Exists(safety), "Restore candidate replaces with safety backup");
        Check(new PromptRepository(safety).GetPrompts(PromptFilterKind.All, null, null).Count == 2, "Safety backup holds previous records");
        Reject(() => r.BackupTo(r.DatabasePath), "Backup cannot overwrite active library");
        Reject(() => r.RestoreFrom(r.DatabasePath), "Restore cannot replace itself");
        var newer = Path.Combine(_root, "newer.db"); File.Copy(backup,newer); Sql(newer,"PRAGMA user_version=99;");
        Reject(() => r.RestoreFrom(newer), "Future schema rejected");
        Check(r.GetPrompts(PromptFilterKind.All, null, null).Count == 1, "Future rejection retains library");
        using (var wal = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=r.DatabasePath, Pooling=false }.ToString()))
        {
            wal.Open(); using var cmd = wal.CreateCommand(); cmd.CommandText="PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0;"; cmd.ExecuteNonQuery();
            var walId = r.CreatePrompt(null); var walBackup = Path.Combine(_root,"wal.db"); r.BackupTo(walBackup);
            var check = new PromptRepository(walBackup); check.Initialize();
            Check(check.GetPromptById(walId) != null,"Backup includes committed WAL writes");
        }
        var old = Path.Combine(_root,"old.db"); File.Copy(backup,old); Sql(old,"PRAGMA user_version=0;");
        new PromptRepository(old).Initialize();
        Check(Directory.EnumerateFiles(_root,"pre-upgrade-*.db").Any(),"Schema migration creates pre-upgrade backup");
        var detached=NewLibrary("detached"); var currentId=detached.CreatePrompt(null);
        Sql(detached.DatabasePath,"PRAGMA foreign_keys=OFF; INSERT INTO PromptVersions (PromptId,VersionNumber,Body,ChangeNote,CreatedAt) VALUES (999,1,'detached body','original note','2020-01-01T00:00:00');");
        Check(detached.DetachedHistoryCount()==1,"Detached history detected");
        var before=TableSnapshot(detached.DatabasePath,"Prompts")+TableSnapshot(detached.DatabasePath,"Folders");
        var recoveryBackup=detached.RecoverDetachedHistory();
        Check(File.Exists(recoveryBackup) && detached.DetachedHistoryCount()==0,"Recovery backup and relationships repaired");
        Check(before==TableSnapshot(detached.DatabasePath,"Prompts")+TableSnapshot(detached.DatabasePath,"Folders"),"Recovery leaves current prompts and folders unchanged");
        Check(detached.GetRecoveryHistory().Single().SnapshotJson.Contains("detached body"),"Recovery retains original version fields");
        PromptRepository.ValidateBackupDatabase(detached.DatabasePath);
        Check(detached.RecoverDetachedHistory()==null,"Recovery is idempotent");
        Check(detached.CreateExportPackage(null).RecoveryHistory.Count==1 && detached.CreateScopedExport(new[]{currentId}).RecoveryHistory.Count==0,"Entire-library export retains recovery records only in library scope");
        var copy=NewLibrary("recovered-import"); copy.ImportPackage(detached.CreateExportPackage(null));
        Check(copy.GetRecoveryHistory().Single().SnapshotJson==detached.GetRecoveryHistory().Single().SnapshotJson,"Recovery records survive JSON transfer");
    }

    private static void InstallerChecks()
    {
        var stage=Path.Combine(_root,"stage"); var install=Path.Combine(_root,"install"); Directory.CreateDirectory(stage); Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install,"PromptManager.exe"),"old"); File.WriteAllText(Path.Combine(install,"keep.txt"),"unrelated");
        File.WriteAllText(Path.Combine(stage,"PromptManager.exe"),"new");
        var assembly=Assembly.Load("PromptManagerSetup"); var installer=assembly.GetType("PromptManagerSetup.Program+Installer")!;
        var method=installer.GetMethod("InstallPayloadFiles",BindingFlags.Static|BindingFlags.NonPublic)!;
        Reject(() => method.Invoke(null,new object[] {stage,install,new[] {"PromptManager.exe","missing.dll"}}),"Installer copy failure detected");
        Check(File.ReadAllText(Path.Combine(install,"PromptManager.exe"))=="old","Installer rollback restores prior executable");
        method.Invoke(null,new object[] {stage,install,new[] {"PromptManager.exe"}});
        Check(File.ReadAllText(Path.Combine(install,"PromptManager.exe"))=="new" && File.ReadAllText(Path.Combine(install,"keep.txt"))=="unrelated","Installer updates owned files, preserves unrelated files");
        Reject(() => method.Invoke(null,new object[] {stage,install,new[] {"../outside.txt"}}),"Installer rejects path traversal");
    }

    private static void UiChecks(string? screenshotDirectory)
    {
        var app = new App(false); app.InitializeComponent(); app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        var r=NewLibrary("ui"); r.CreatePrompt(null); var vm=new MainViewModel(r,new PromptFileService(r));
        vm.SearchText="NoMatchAnything"; vm.NewPromptCommand.Execute(null);
        Check(vm.SearchText=="" && vm.SelectedPrompt?.Title=="Untitled Prompt","New record clears search and stays selected");
        var settingsPath=Path.Combine(_root,"ui-settings.json"); var w=new MainWindow(settingsPath) { DataContext=vm };
        app.MainWindow=w;
        Console.WriteLine("UI: showing isolated window"); w.Show(); Console.WriteLine("UI: window shown");
        Check(!vm.HasUnsavedChanges,"Opening Notes does not create false unsaved edits");
        var notes=(RichTextBox)w.FindName("PromptNotesEditor"); notes.AppendText("Readable test note");
        notes.Selection.Select(notes.Document.ContentStart,notes.Document.ContentEnd);
        ((Button)w.FindName("NotesBoldButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((Button)w.FindName("NotesItalicButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ((ComboBox)w.FindName("NotesColorComboBox")).SelectedIndex=2;
        Check(vm.HasUnsavedChanges && vm.SelectedPrompt!.FormattedNotes.Contains("\\b") && vm.SelectedPrompt.FormattedNotes.Contains("\\i"),"Notes formatting creates pending rich text");
        vm.ChangeNote="Formatted notes test"; var noteId=vm.SelectedPrompt!.Id; vm.SaveCommand.Execute(null);
        Check(!vm.HasUnsavedChanges && r.GetPromptById(noteId)!.Notes=="Readable test note" && r.GetPromptById(noteId)!.FormattedNotes.Contains("\\colortbl"),"Notes text and formatting save and reload");
        foreach (var size in new[] {new Size(1480,880),new Size(1000,620)})
        {
            w.Width=size.Width; w.Height=size.Height;
            w.Measure(size); w.Arrange(new Rect(size)); w.UpdateLayout();
            w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            var tabs=(TabControl)w.FindName("EditorTabs");
            foreach (var index in new[] {1,2})
            {
                tabs.SelectedIndex=index; w.UpdateLayout();
                w.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                var editor=(FrameworkElement)w.FindName(index==1?"PromptNotesEditor":"PromptBodyTextBox");
                Check(editor.ActualHeight>180,$"Editor usable height at {size.Width}x{size.Height} tab {index}");
                var point=editor.TranslatePoint(new Point(),w);
                Check(point.X>=0 && point.Y>=0 && point.Y+editor.ActualHeight<=size.Height && point.X+editor.ActualWidth<=size.Width,$"Editor contained at {size.Width}x{size.Height} tab {index}");
                if (screenshotDirectory != null)
                {
                    Directory.CreateDirectory(screenshotDirectory);
                    var bmp=new RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,PixelFormats.Pbgra32); bmp.Render((Visual)w.Content);
                    var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
                    using var output=File.Create(Path.Combine(screenshotDirectory,$"ui-{size.Width}-{index}.png")); encoder.Save(output);
                }
            }
        }
        Check(w.Icon != null,"Application window has arrow icon");
        Check(File.Exists(Path.Combine(AppContext.BaseDirectory,"USER_MANUAL.html")),"Rendered offline manual included");
        var preferences=new UiSettings {ReadingSize=20,EditorWidth=450}; preferences.Save(settingsPath);
        Check(UiSettings.Load(settingsPath).ReadingSize==20,"View preferences round trip");
        File.WriteAllText(settingsPath,"invalid json"); Check(UiSettings.Load(settingsPath).ReadingSize==16,"Invalid preferences recover with defaults");
        w.Close(); app.Shutdown();
    }

    private static void PackageChecks(string projectRoot)
    {
        var payload=Path.Combine(projectRoot,"artifacts","installer","payload.zip");
        using var zip=System.IO.Compression.ZipFile.OpenRead(payload);
        foreach(var name in new[]{"PromptManager.exe","PresentationFramework.dll","e_sqlite3.dll","USER_MANUAL.html","USER_MANUAL.md","Assets/ProtoCall_logo.png"})
            Check(zip.GetEntry(name)!=null,"Payload includes "+name);
        var setup=Path.Combine(projectRoot,"artifacts","installer","PromptManagerSetup.exe");
        Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(setup).FileVersion=="1.4.0.0","Setup executable version");
        var ico=Path.Combine(projectRoot,"src","PromptManager","Assets","PromptManager.ico");
        var bytes=File.ReadAllBytes(ico); Check(BitConverter.ToUInt16(bytes,0)==0 && BitConverter.ToUInt16(bytes,2)==1 && BitConverter.ToUInt16(bytes,4)==10,"Ten-resolution Windows icon");
        var install=Path.Combine(_root,"packaged-install"); Directory.CreateDirectory(install); File.WriteAllText(Path.Combine(install,"unrelated.txt"),"keep");
        var assembly=Assembly.Load("PromptManagerSetup"); var optionsType=assembly.GetType("PromptManagerSetup.Program+InstallOptions")!;
        var options=optionsType.GetMethod("FromArgs")!.Invoke(null,new object[]{new[]{"/DIR="+install,"/NODESKTOP","/NOSTARTMENU","/NOLAUNCH","/NOREGISTRY"}})!;
        var installer=assembly.GetType("PromptManagerSetup.Program+Installer")!;
        installer.GetMethod("Install")!.Invoke(null,new object?[]{options,null});
        var installedExe=Path.Combine(install,"PromptManager.exe");
        Check(System.Diagnostics.FileVersionInfo.GetVersionInfo(installedExe).FileVersion=="1.4.0.0","Embedded payload installs version 1.4.0 to custom location");
        Check(File.Exists(Path.Combine(install,"USER_MANUAL.html")) && File.Exists(Path.Combine(install,"install-manifest.txt")),"Installer writes manual and ownership manifest");
        installer.GetMethod("Install")!.Invoke(null,new object?[]{options,null});
        Check(File.ReadAllText(Path.Combine(install,"unrelated.txt"))=="keep","Repeated installation preserves unrelated files");
        foreach(var executable in new[]{installedExe,setup})
        {
            using var extracted=System.Drawing.Icon.ExtractAssociatedIcon(executable);
            using var expected=new System.Drawing.Icon(ico,new System.Drawing.Size(32,32));
            using var a=extracted!.ToBitmap(); using var b=expected.ToBitmap();
            var matches=0; var total=0;
            for(var y=0;y<32;y++) for(var x=0;x<32;x++) {if(a.GetPixel(x,y).ToArgb()==b.GetPixel(x,y).ToArgb()) matches++; total++;}
            Check(matches/(double)total>0.98,"Embedded Explorer icon matches arrow: "+Path.GetFileName(executable));
        }
    }

    private static void Sql(string path,string sql) { using var c=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=path,Pooling=false}.ToString()); c.Open(); using var cmd=c.CreateCommand(); cmd.CommandText=sql; cmd.ExecuteNonQuery(); }
    private static string TableSnapshot(string path,string table)
    {
        using var c=new SqliteConnection(new SqliteConnectionStringBuilder {DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString()); c.Open(); using var cmd=c.CreateCommand();
        cmd.CommandText="SELECT * FROM "+table+" ORDER BY Id;"; using var reader=cmd.ExecuteReader(); var rows=new List<Dictionary<string,object?>>();
        while(reader.Read()) rows.Add(Enumerable.Range(0,reader.FieldCount).ToDictionary(reader.GetName,i=>reader.IsDBNull(i)?null:reader.GetValue(i)));
        return System.Text.Json.JsonSerializer.Serialize(rows);
    }
    private static void Check(bool condition,string name) { if (!condition) throw new InvalidOperationException("FAIL: "+name); _checks++; Console.WriteLine("PASS: "+name); }
    private static void Reject(Action action,string name) { try {action();} catch (Exception ex) when(ex is InvalidOperationException or SqliteException or TargetInvocationException) {Check(true,name); return;} throw new InvalidOperationException("FAIL: "+name+" unexpectedly succeeded"); }
}
