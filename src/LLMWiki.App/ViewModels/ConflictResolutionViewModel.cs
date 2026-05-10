using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMWiki.Core.Domain;
using LLMWiki.Core.Infrastructure;

namespace LLMWiki.App.ViewModels;

public enum ConflictChoice
{
    Unresolved,
    KeepLocal,
    TakeRemote,
}

public partial class ConflictResolutionItem : ViewModelBase
{
    public ConflictResolutionItem(ConflictEntry entry, ConflictChoice initial = ConflictChoice.Unresolved)
    {
        Entry = entry;
        _choice = initial;
    }

    public ConflictEntry Entry { get; }

    [ObservableProperty]
    private ConflictChoice _choice;

    public string RelativePath => Entry.RelativePath;
}

public partial class ConflictResolutionViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public ObservableCollection<ConflictResolutionItem> Items { get; } = new();

    [ObservableProperty]
    private bool _allResolved;

    public void Load(IReadOnlyList<ConflictEntry> entries)
    {
        Items.Clear();
        var savedChoices = TryLoadSavedChoices();
        foreach (var entry in entries)
        {
            var initial = savedChoices.GetValueOrDefault(entry.RelativePath, ConflictChoice.Unresolved);
            var item = new ConflictResolutionItem(entry, initial);
            item.PropertyChanged += (_, _) =>
            {
                Persist();
                AllResolved = Items.All(i => i.Choice != ConflictChoice.Unresolved);
            };
            Items.Add(item);
        }
        AllResolved = Items.All(i => i.Choice != ConflictChoice.Unresolved);
    }

    [RelayCommand]
    public void Persist()
    {
        var snapshot = Items.ToDictionary(i => i.RelativePath, i => i.Choice);
        var json = JsonSerializer.Serialize(snapshot, Json);
        AtomicFile.WriteAllText(LLMWikiPaths.ConflictResolutionStateFile, json);
    }

    public void ClearPersistedState()
    {
        try
        {
            if (File.Exists(LLMWikiPaths.ConflictResolutionStateFile))
                File.Delete(LLMWikiPaths.ConflictResolutionStateFile);
        }
        catch { }
    }

    private static Dictionary<string, ConflictChoice> TryLoadSavedChoices()
    {
        var path = LLMWikiPaths.ConflictResolutionStateFile;
        if (!File.Exists(path)) return new();
        try
        {
            var raw = File.ReadAllText(path);
            return JsonSerializer
                .Deserialize<Dictionary<string, ConflictChoice>>(raw, Json)
                ?? new();
        }
        catch
        {
            return new();
        }
    }
}
