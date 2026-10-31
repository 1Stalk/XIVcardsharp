using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using DalamudDesignKit.Components;
using DalamudDesignKit.Drawing;
using DalamudDesignKit.Shell;
using DalamudDesignKit.Theme;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using XIVcardsharp.Core.Draft;
using XIVcardsharp.Core.State;
using XIVcardsharp.Execution;
using XIVcardsharp.Interop;

namespace XIVcardsharp.UI;

/// <summary>
/// Modern, ultra-compact HUD window for XIVcardsharp powered by DalamudDesignKit with HyperViolet theme.
/// Single-page layout without tabs or appearance settings.
/// </summary>
public class MainWindow : AppWindowShell
{
    private bool _settingsExpanded = false;

    public MainWindow() : base("XIVcardsharp###XIVcardsharpMainWindow", new Vector2(360, 220), 300f, 130f)
    {
        // Lock to HyperViolet (purple accent theme)
        Palette = PalettePresets.HyperViolet;
        ThemeTokens.Current = PalettePresets.HyperViolet;

        // Explicitly disable tab navigation & header tab toggle button
        ShowTabToggleInHeader = false;
        Tabs = null;
    }

    protected override FontAwesomeIcon GetAppIcon() => FontAwesomeIcon.Clone;

    protected override string? GetStatusText()
    {
        return UniversalTriadManager.Status switch
        {
            UniversalEngineStatus.Evaluating => "SOLVING",
            UniversalEngineStatus.MoveReady => "READY",
            UniversalEngineStatus.Executing => "PLAYING",
            UniversalEngineStatus.WaitingTurn => "WAITING",
            _ => (Plugin.Configuration.TournamentAutoplay || Plugin.Configuration.UniversalAutoplay) ? "IDLE" : "OFF"
        };
    }

    protected override void DrawContent()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = ContentMarginX * scale;
        var padY = ContentMarginY * scale;
        var avail = ImGui.GetContentRegionAvail();
        var contentWidth = MathF.Max(1f, avail.X - padX);
        var contentHeight = MathF.Max(1f, avail.Y - padY);

        using var paddingScope = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        using var child = ImRaii.Child("##xivcardsharp_content", new Vector2(contentWidth, contentHeight), false);
        if (!child.Success) return;

        DrawMainCard(scale);
        DrawAdvancedSettingsCard(scale);
    }

    // ==========================================
    // Section 1: Main Status, Autoplay & Draft
    // ==========================================
    private void DrawMainCard(float scale)
    {
        var status = UniversalTriadManager.Status;
        var isAutoplayActive = Plugin.Configuration.TournamentAutoplay || Plugin.Configuration.UniversalAutoplay;

        var statusColor = status switch
        {
            UniversalEngineStatus.Evaluating => new Vector4(0.96f, 0.65f, 0.18f, 1.0f),
            UniversalEngineStatus.MoveReady => Palette.Primary,
            UniversalEngineStatus.Executing => Palette.AccentSoft,
            UniversalEngineStatus.WaitingTurn => ThemeTokens.TextSecondary,
            _ => isAutoplayActive ? Palette.Primary : ThemeTokens.TextMuted
        };

        using (SettingsGroup.Begin())
        {
            // 1. Status dot
            var summary = UniversalTriadManager.StatusSummary;
            var maxTextWidth = MathF.Max(80f * scale, SettingsGroup.ContentWidth - 24f * scale);
            var isTruncated = TextDraw.Measure(summary).X > maxTextWidth;
            var displayText = isTruncated ? TextDraw.Truncate(summary, maxTextWidth) : summary;

            SimpleWidgets.StatusDot(displayText, statusColor, pulsing: status == UniversalEngineStatus.Evaluating);
            if (isTruncated && ImGui.IsItemHovered())
            {
                Tooltip.Show(summary);
            }

            if (status == UniversalEngineStatus.Evaluating)
            {
                ImGui.Dummy(new Vector2(0f, 2f * scale));
                var dl = ImGui.GetWindowDrawList();
                var barPos = ImGui.GetCursorScreenPos();
                var barWidth = SettingsGroup.ContentWidth;
                Paint.IndeterminateBar(dl, barPos, barWidth, 3f * scale, Palette.Primary);
                ImGui.Dummy(new Vector2(barWidth, 6f * scale));
            }

            // 2. Open Tournament checkbox
            var tourney = Plugin.Configuration.TournamentAutoplay;
            if (SettingsRow.DrawToggle("Open Tournament", "Automatically plays Battlehall Open Tournament matches with draft deck deduction and adversarial minimax.", ref tourney, Palette.Primary))
            {
                Plugin.Configuration.TournamentAutoplay = tourney;
                if (tourney)
                {
                    Plugin.Configuration.UniversalAutoplay = false;
                }
                Plugin.Configuration.Save();
            }

            // 3. PvP / Universal checkbox
            var universal = Plugin.Configuration.UniversalAutoplay;
            if (SettingsRow.DrawToggle("PvP / Universal", "Automatically plays general Triple Triad matches (PvP, Open Rules, Friendly matches).", ref universal, Palette.Primary))
            {
                Plugin.Configuration.UniversalAutoplay = universal;
                if (universal)
                {
                    Plugin.Configuration.TournamentAutoplay = false;
                }
                Plugin.Configuration.Save();
            }

            // 4. Draft section (always visible: Draft Standby or active draft choices)
            DrawDraftRowOrActive(scale);
        }
    }

    private void DrawDraftRowOrActive(float scale)
    {
        var isDraftOpen = TournamentDraftManager.IsDraftWindowOpen(out var draftRules);

        // Divider separating toggles from draft
        ImGui.Dummy(new Vector2(0f, 2f * scale));
        var dl = ImGui.GetWindowDrawList();
        var curPos = ImGui.GetCursorScreenPos();
        var rightEdge = SettingsGroup.ContentRightEdge > 0f ? SettingsGroup.ContentRightEdge : curPos.X + SettingsGroup.ContentWidth;
        dl.AddLine(curPos, new Vector2(rightEdge, curPos.Y), Paint.Col(ThemeTokens.Hairline), 1f);
        ImGui.Dummy(new Vector2(0f, 4f * scale));

        var startY = ImGui.GetCursorPosY();
        const float targetDraftHeight = 88f;
        var minHeight = targetDraftHeight * scale;

        if (isDraftOpen)
        {
            DrawActiveDraftContent(draftRules, scale);
        }
        else
        {
            DrawStandbyDraftContent(scale);
        }

        var elapsed = ImGui.GetCursorPosY() - startY;
        if (elapsed < minHeight)
        {
            ImGui.Dummy(new Vector2(0f, minHeight - elapsed));
        }
    }

    private void DrawStandbyDraftContent(float scale)
    {
        // 1. Current RULES line in Standby
        using (ImRaii.PushColor(ImGuiCol.Text, ThemeTokens.TextMuted))
        {
            ImGui.TextUnformatted("RULES:");
        }
        ImGui.SameLine();
        ImGui.TextDisabled("Standby");
        ImGui.Dummy(new Vector2(0f, 4f * scale));

        // 2. SET 1, SET 2, SET 3 placeholders
        DrawDraftRow(1, -1, null, Palette.Primary);
        DrawDraftRow(2, -1, null, Palette.Primary);
        DrawDraftRow(3, -1, null, Palette.Primary);

        // 3. Caption
        ImGui.Dummy(new Vector2(0f, 2f * scale));
        using (ImRaii.PushColor(ImGuiCol.Text, ThemeTokens.TextMuted))
        {
            ImGui.TextUnformatted("[Standard Mode] Waiting for Open Tournament draft...");
        }
    }

    private void DrawActiveDraftContent(List<string> draftRules, float scale)
    {
        var eval = TournamentDraftManager.GetCurrentEvaluation(out var draftState);

        // 1. Current RULES
        using (ImRaii.PushColor(ImGuiCol.Text, ThemeTokens.TextMuted))
        {
            ImGui.TextUnformatted("RULES:");
        }
        if (draftRules != null && draftRules.Count > 0)
        {
            for (var r = 0; r < draftRules.Count; r++)
            {
                ImGui.SameLine();
                SimpleWidgets.StatusBadge(draftRules[r], true, Palette.Primary);
            }
        }
        else
        {
            ImGui.SameLine();
            SimpleWidgets.StatusBadge("Standard", false);
        }

        ImGui.Dummy(new Vector2(0f, 4f * scale));

        // 2. SET 1, SET 2, SET 3 & [Standard Mode]... caption
        if (eval != null && draftState != null)
        {
            DrawDraftRow(1, eval.Set1Option, draftState.Set1Options, Palette.Primary);
            DrawDraftRow(2, eval.Set2Option, draftState.Set2Options, Palette.Primary);
            DrawDraftRow(3, eval.Set3Option, draftState.Set3Options, Palette.Primary);

            if (!string.IsNullOrEmpty(eval.Summary))
            {
                ImGui.Dummy(new Vector2(0f, 2f * scale));
                using (ImRaii.PushColor(ImGuiCol.Text, ThemeTokens.TextSecondary))
                {
                    ImGui.TextWrapped(eval.Summary);
                }
            }
        }
        else
        {
            SimpleWidgets.StatusDot("Calculating optimal draft choices...", Palette.Primary, pulsing: true);
            var dl = ImGui.GetWindowDrawList();
            var barPos = ImGui.GetCursorScreenPos();
            var barWidth = SettingsGroup.ContentWidth;
            Paint.IndeterminateBar(dl, barPos, barWidth, 3f * scale, Palette.Primary);
            ImGui.Dummy(new Vector2(barWidth, 6f * scale));
        }
    }

    private static void DrawDraftRow(int rowNumber, int optionIndex, List<TournamentDraftOption>? options, Vector4 accent)
    {
        var scale = ImGuiHelpers.GlobalScale;

        SimpleWidgets.StatusBadge($"SET {rowNumber}", false);
        ImGui.SameLine();

        if (options == null || optionIndex < 0 || optionIndex >= options.Count)
        {
            ImGui.TextDisabled("-");
            ImGui.Dummy(new Vector2(0f, 2f * scale));
            return;
        }

        var opt = options[optionIndex];

        SimpleWidgets.StatusBadge($"CHOICE #{optionIndex + 1}", true, accent);
        ImGui.SameLine();

        var cards = FormatOptionCards(options, optionIndex);
        using (ImRaii.PushColor(ImGuiCol.Text, ThemeTokens.TextSecondary))
        {
            ImGui.TextUnformatted($"— {cards}");
        }

        if (ImGui.IsItemHovered())
        {
            var detailedTooltip = FormatCardsDetailedTooltip(options, optionIndex);
            if (!string.IsNullOrEmpty(detailedTooltip))
            {
                Tooltip.Show(detailedTooltip);
            }
        }

        ImGui.Dummy(new Vector2(0f, 2f * scale));
    }

    private static string FormatOptionCards(List<TournamentDraftOption> options, int optionIndex)
    {
        if (options == null || optionIndex < 0 || optionIndex >= options.Count)
        {
            return "-";
        }

        var opt = options[optionIndex];
        if (opt.Cards == null || opt.Cards.Count == 0)
        {
            return "-";
        }

        return string.Join(", ", opt.Cards.Select(c =>
        {
            var stars = c.Rarity <= 0 ? 1 : (c.Rarity <= 4 ? c.Rarity + 1 : c.Rarity);
            return $"{c.Name} (★{stars})";
        }));
    }

    private static string FormatCardsDetailedTooltip(List<TournamentDraftOption> options, int optionIndex)
    {
        if (options == null || optionIndex < 0 || optionIndex >= options.Count)
        {
            return string.Empty;
        }

        var opt = options[optionIndex];
        if (opt.Cards == null || opt.Cards.Count == 0)
        {
            return string.Empty;
        }

        var lines = new List<string>();
        foreach (var c in opt.Cards)
        {
            var stars = c.Rarity <= 0 ? 1 : (c.Rarity <= 4 ? c.Rarity + 1 : c.Rarity);
            var starStr = new string('★', stars);
            var sideStr = $"↑{FormatSide(c.Up)}  ←{FormatSide(c.Left)}  ↓{FormatSide(c.Down)}  →{FormatSide(c.Right)}";
            var line = $"{c.Name} ({starStr})\n  Sides: {sideStr}";
            if (!string.IsNullOrEmpty(c.Tribe))
            {
                line += $"\n  Tribe: {c.Tribe}";
            }
            lines.Add(line);
        }
        return string.Join("\n\n", lines);
    }

    private static string FormatSide(int val) => val >= 10 ? "A" : val.ToString();

    // ==========================================
    // Section 3: Advanced Settings (Collapsible)
    // ==========================================
    private void DrawAdvancedSettingsCard(float scale)
    {
        using var group = SettingsGroup.BeginCollapsible("##group_settings", "Advanced Settings", ref _settingsExpanded);
        if (group == null) return;

        var controlWidth = Math.Clamp(SettingsGroup.ContentWidth * 0.42f, 110f * scale, 160f * scale);

        var moveDelay = Plugin.Configuration.MoveDelayMs;
        SettingsRow.Draw("Move Delay", "Delay in milliseconds before executing a chosen move in-game (100–3000ms).", controlWidth, () =>
        {
            if (ImGui.SliderInt("##move_delay", ref moveDelay, 100, 3000, "%d ms"))
            {
                Plugin.Configuration.MoveDelayMs = moveDelay;
                Plugin.Configuration.Save();
            }
        });

        var sampleCount = Plugin.Configuration.SampleCount;
        SettingsRow.Draw("Monte Carlo Samples", "Number of plausible hidden opponent hand worlds sampled per move (50–500).\n300 is recommended for master-level play.", controlWidth, () =>
        {
            if (ImGui.SliderInt("##mc_samples", ref sampleCount, 50, 500, "%d"))
            {
                Plugin.Configuration.SampleCount = sampleCount;
                Plugin.Configuration.Save();
            }
        });

        var maxCores = Environment.ProcessorCount;
        var threads = Math.Clamp(Plugin.Configuration.MaxThreads, 0, maxCores);
        var effective = threads <= 0 ? maxCores : threads;
        SettingsRow.Draw("Solver Threads", $"Uses {effective} of {maxCores} logical CPU cores for parallel Monte Carlo solving (0 = all).", controlWidth, () =>
        {
            if (ImGui.SliderInt("##threads", ref threads, 0, maxCores, threads == 0 ? "All" : "%d"))
            {
                Plugin.Configuration.MaxThreads = threads;
                Plugin.Configuration.Save();
            }
        });

        var recordMatches = Plugin.Configuration.RecordMatches;
        if (SettingsRow.DrawToggle("Record Match Logs", "Save full match traces and solver rollouts to disk in JSON format for analysis.", ref recordMatches, Palette.Primary))
        {
            Plugin.Configuration.RecordMatches = recordMatches;
            Plugin.Configuration.Save();
        }

        var btnWidth = PillButton.Width("Open Logs Folder", FontAwesomeIcon.FolderOpen);
        SettingsRow.Draw("Logs Directory", "Open the folder where match logs are saved.", btnWidth, () =>
        {
            if (PillButton.Draw("##btn_open_logs", "Open Logs Folder", Palette.Primary, PillButton.Emphasis.Tinted, FontAwesomeIcon.FolderOpen))
            {
                UniversalMatchRecorder.OpenLogsFolder();
            }
        });
    }
}
