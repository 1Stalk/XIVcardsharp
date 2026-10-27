using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Numerics;
using XIVcardsharp.Execution;
using XIVcardsharp.Interop;

namespace XIVcardsharp.UI;

public class MainWindow : Window, IDisposable
{
    private string _draftStatusMessage = string.Empty;

    public MainWindow() : base("XIVcardsharp###XIVcardsharpMainWindow", ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(360, 200),
            MaximumSize = new Vector2(600, 800)
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "XIVcardsharp - Triple Triad Solver");
        ImGui.Separator();

        // 1. Core Autoplay Checkboxes
        var tourney = Plugin.Configuration.TournamentAutoplay;
        if (ImGui.Checkbox("Open Tournament Autoplay", ref tourney))
        {
            Plugin.Configuration.TournamentAutoplay = tourney;
            if (tourney)
            {
                Plugin.Configuration.UniversalAutoplay = false;
            }
            Plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Automatically plays Battlehall Open Tournament matches.\nApplies draft deck deduction and adversarial minimax.");
        }

        var universal = Plugin.Configuration.UniversalAutoplay;
        if (ImGui.Checkbox("PvP / Universal Autoplay", ref universal))
        {
            Plugin.Configuration.UniversalAutoplay = universal;
            if (universal)
            {
                Plugin.Configuration.TournamentAutoplay = false;
            }
            Plugin.Configuration.Save();
        }
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Automatically plays general Triple Triad matches (PvP, Open Rules, Friendly matches).");
        }

        ImGui.Spacing();
        ImGui.Separator();

        // 2. Tournament Draft Deck Selection
        var isDraftOpen = TournamentDraftManager.IsDraftWindowOpen(out var draftRules);
        if (isDraftOpen)
        {
            ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.4f, 1.0f), "● Open Tournament Draft Window Detected!");
            if (draftRules.Count > 0)
            {
                ImGui.TextDisabled($"Active Rules: {string.Join(", ", draftRules)}");
            }

            var eval = TournamentDraftManager.GetCurrentEvaluation();
            if (eval != null)
            {
                ImGui.TextDisabled($"Best Synergies: {eval.Summary}");
            }

            if (ImGui.Button("✨ Select Optimal Tournament Deck", new Vector2(-1, 32)))
            {
                if (TournamentDraftManager.ApplyOptimalDraft(out var msg))
                {
                    _draftStatusMessage = msg;
                }
                else
                {
                    _draftStatusMessage = $"Draft failed: {msg}";
                }
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button("Open Tournament Draft (No Draft Active)", new Vector2(-1, 28));
            ImGui.EndDisabled();
        }

        if (!string.IsNullOrEmpty(_draftStatusMessage))
        {
            ImGui.TextWrapped(_draftStatusMessage);
        }

        ImGui.Spacing();
        ImGui.Separator();

        // 3. Engine Live Status
        ImGui.Text("Status:");
        ImGui.SameLine();
        var statusColor = UniversalTriadManager.Status switch
        {
            Core.State.UniversalEngineStatus.Evaluating => new Vector4(1.0f, 0.8f, 0.2f, 1.0f),
            Core.State.UniversalEngineStatus.MoveReady => new Vector4(0.2f, 1.0f, 0.4f, 1.0f),
            Core.State.UniversalEngineStatus.Executing => new Vector4(0.4f, 0.8f, 1.0f, 1.0f),
            Core.State.UniversalEngineStatus.WaitingTurn => new Vector4(0.7f, 0.7f, 0.7f, 1.0f),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1.0f)
        };
        ImGui.TextColored(statusColor, UniversalTriadManager.StatusSummary);

        ImGui.Spacing();

        // 4. Advanced Settings Collapsible
        if (ImGui.CollapsingHeader("Settings"))
        {
            var moveDelay = Plugin.Configuration.MoveDelayMs;
            if (ImGui.SliderInt("Move Delay (ms)", ref moveDelay, 100, 3000))
            {
                Plugin.Configuration.MoveDelayMs = moveDelay;
                Plugin.Configuration.Save();
            }

            var sampleCount = Plugin.Configuration.SampleCount;
            if (ImGui.SliderInt("Monte Carlo Samples", ref sampleCount, 50, 500))
            {
                Plugin.Configuration.SampleCount = sampleCount;
                Plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Number of plausible hidden opponent hand worlds sampled per move (50–500).\n300 is recommended for strong, master-level play.");
            }

            var maxCores = Environment.ProcessorCount;
            var threads = Math.Clamp(Plugin.Configuration.MaxThreads, 0, maxCores);
            if (ImGui.SliderInt("Solver Threads (0 = all)", ref threads, 0, maxCores, threads == 0 ? "All" : "%d"))
            {
                Plugin.Configuration.MaxThreads = threads;
                Plugin.Configuration.Save();
            }
            if (ImGui.IsItemHovered())
            {
                var effective = threads <= 0 ? maxCores : threads;
                ImGui.SetTooltip($"Uses {effective} of {maxCores} logical cores for Monte Carlo solving.");
            }

            var recordMatches = Plugin.Configuration.RecordMatches;
            if (ImGui.Checkbox("Record Match Logs to Disk", ref recordMatches))
            {
                Plugin.Configuration.RecordMatches = recordMatches;
                Plugin.Configuration.Save();
            }

            if (ImGui.Button("Open Match Logs Folder"))
            {
                UniversalMatchRecorder.OpenLogsFolder();
            }
        }
    }
}
