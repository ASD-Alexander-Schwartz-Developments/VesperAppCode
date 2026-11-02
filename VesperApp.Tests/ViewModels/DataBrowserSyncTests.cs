using System.Diagnostics;
using VesperApp.Models;
using VesperApp.ViewModels;
using Xunit;

namespace VesperApp.Tests.ViewModels;

/// <summary>
/// Live refresh of the Recordings browser (RecordingParsingViewModel.SyncNodes). A Nanotag GNSS
/// download leaves ~17 files a snapshot in one folder (60 083 files after one night, 2026-11-02);
/// the old quadratic merge ran on the UI thread and froze VesperApp during a GNSS decode.
/// </summary>
public class DataBrowserSyncTests
{
    private static RecordingDataNode File(string name) =>
        new() { Name = name, FullPath = @"C:\data\GPS\" + name, IsFile = true };

    private static List<RecordingDataNode> Files(int n, int from = 0) =>
        Enumerable.Range(from, n).Select(i => File($"snap.{i:D6}.dat")).ToList();

    [Fact]
    public void Large_folder_merges_fast_and_keeps_instances()
    {
        var current = new BulkObservableCollection<RecordingDataNode>();
        current.ReplaceAll(Files(60_000));
        RecordingDataNode kept = current[12_345];

        List<RecordingDataNode> fresh = Files(60_500);           // same files (new instances) + 500 new
        var sw = Stopwatch.StartNew();
        RecordingParsingViewModel.SyncNodes(current, fresh);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, $"merge took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(60_500, current.Count);
        Assert.Same(kept, current[12_345]);                      // expansion / selection state survives
        Assert.Equal(fresh.Select(f => f.FullPath), current.Select(c => c.FullPath));
    }

    [Fact]
    public void Small_change_is_applied_in_place_and_in_order()
    {
        var current = new BulkObservableCollection<RecordingDataNode>();
        current.ReplaceAll(Files(10));
        RecordingDataNode first = current[0];
        int notifications = 0;
        current.CollectionChanged += (_, _) => notifications++;

        List<RecordingDataNode> fresh = Files(10);
        fresh.RemoveAt(3);                                       // one file gone
        fresh.Insert(5, File("snap.new.dat"));                   // one file added

        RecordingParsingViewModel.SyncNodes(current, fresh);

        Assert.Equal(fresh.Select(f => f.FullPath), current.Select(c => c.FullPath));
        Assert.Same(first, current[0]);
        Assert.Equal(2, notifications);                          // one remove + one insert, no reset
    }

    [Fact]
    public void Children_are_merged_too()
    {
        var folder = new RecordingDataNode { Name = "GPS", FullPath = @"C:\data\GPS", IsExpanded = true };
        folder.Children.ReplaceAll(Files(3));
        var current = new BulkObservableCollection<RecordingDataNode>();
        current.ReplaceAll(new[] { folder });

        var freshFolder = new RecordingDataNode { Name = "GPS", FullPath = @"C:\data\GPS" };
        freshFolder.Children.ReplaceAll(Files(4));
        RecordingParsingViewModel.SyncNodes(current, new List<RecordingDataNode> { freshFolder });

        Assert.Same(folder, current[0]);
        Assert.True(folder.IsExpanded);
        Assert.Equal(4, folder.Children.Count);
    }
}
