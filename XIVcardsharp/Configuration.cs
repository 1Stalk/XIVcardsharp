using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace XIVcardsharp;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool TournamentAutoplay { get; set; } = true;
    public bool UniversalAutoplay { get; set; } = false;
    public int MoveDelayMs { get; set; } = 100;
    public int SampleCount { get; set; } = 300;
    public int MaxThreads { get; set; } = 0;
    public bool RecordMatches { get; set; } = false;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        pluginInterface = pi;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
    }
}
