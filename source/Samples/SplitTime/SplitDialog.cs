using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using StopWatch.Plugin;

namespace SplitTime
{
    /// <summary>Shows the dialog on the UI thread, as the handler already runs there.</summary>
    internal sealed class SplitDialogPrompt : ISplitPrompt
    {
        private readonly IPluginHost host;

        public SplitDialogPrompt(IPluginHost host)
        {
            this.host = host;
        }

        public SplitPlan Ask(string issueKey, IReadOnlyList<PluginSubtask> subtasks, int totalMinutes)
        {
            var dialog = new SplitDialog(issueKey, subtasks, totalMinutes) { Owner = host.MainWindow };
            dialog.ShowDialog();
            return dialog.Result;
        }
    }


    /// <summary>
    /// One row per subtask with its minutes, starting from an even split, and
    /// an optional new subtask. It can only be accepted when the minutes add up
    /// to the total, so the plan it returns always conserves the sum.
    /// </summary>
    internal sealed class SplitDialog : Window
    {
        private const string WholeMinutes = "Minutes must be whole numbers, zero or more.";

        private readonly int totalMinutes;
        private readonly List<KeyValuePair<PluginSubtask, TextBox>> rows = new List<KeyValuePair<PluginSubtask, TextBox>>();
        private readonly TextBox newSummary = new TextBox();
        private readonly TextBox newMinutes = new TextBox { Text = "0", Width = 60, HorizontalContentAlignment = HorizontalAlignment.Right };
        private readonly TextBlock status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        private readonly Button ok = new Button { Content = "Split", IsDefault = true, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0) };

        public SplitPlan Result { get; private set; }

        public SplitDialog(string issueKey, IReadOnlyList<PluginSubtask> subtasks, int totalMinutes)
        {
            this.totalMinutes = totalMinutes;

            Title = $"Split {totalMinutes} min of {issueKey}";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "Background");
            status.SetResourceReference(TextBlock.ForegroundProperty, "Text");

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(Label($"Minutes for each subtask (they must add up to {totalMinutes}):", 0));

            int[] shares = SplitMath.Distribute(totalMinutes, subtasks.Count);
            for (int i = 0; i < subtasks.Count; i++)
            {
                var box = new TextBox { Text = shares[i].ToString(), Width = 60, HorizontalContentAlignment = HorizontalAlignment.Right };
                box.TextChanged += (s, e) => Refresh();
                rows.Add(new KeyValuePair<PluginSubtask, TextBox>(subtasks[i], box));

                var line = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
                DockPanel.SetDock(box, Dock.Right);
                line.Children.Add(box);
                line.Children.Add(Label($"{subtasks[i].Key}  {subtasks[i].Summary}", 0));
                panel.Children.Add(line);
            }

            newMinutes.TextChanged += (s, e) => Refresh();
            newSummary.TextChanged += (s, e) => Refresh();

            panel.Children.Add(Label("Or also create a new subtask:", 12));
            var newLine = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            DockPanel.SetDock(newMinutes, Dock.Right);
            newLine.Children.Add(newMinutes);
            newLine.Children.Add(newSummary);
            panel.Children.Add(newLine);

            panel.Children.Add(status);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
            ok.Click += (s, e) => Accept();
            buttons.Children.Add(ok);
            buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(16, 4, 16, 4) });
            panel.Children.Add(buttons);

            Content = panel;
            Refresh();
        }


        private static TextBlock Label(string text, double top)
        {
            var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, top, 0, 0) };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Text");
            return label;
        }


        private static int Minutes(TextBox box)
        {
            return int.TryParse(box.Text.Trim(), out int value) && value >= 0 ? value : -1;
        }


        private bool TryBuild(out SplitPlan plan, out string problem)
        {
            plan = new SplitPlan();
            problem = null;

            foreach (KeyValuePair<PluginSubtask, TextBox> row in rows)
            {
                int minutes = Minutes(row.Value);
                if (minutes < 0)
                {
                    problem = WholeMinutes;
                    return false;
                }
                plan.Parts.Add(new SplitPart { ExistingKey = row.Key.Key, Minutes = minutes });
            }

            int extra = Minutes(newMinutes);
            if (extra < 0)
            {
                problem = WholeMinutes;
                return false;
            }

            string summary = newSummary.Text.Trim();
            if (extra > 0 && summary.Length == 0)
            {
                problem = "Give the new subtask a name, or set its minutes to 0.";
                return false;
            }
            if (extra > 0)
                plan.Parts.Add(new SplitPart { NewSummary = summary, Minutes = extra });

            int sum = plan.TotalMinutes;
            if (sum != totalMinutes)
            {
                problem = $"{sum} of {totalMinutes} min assigned ({totalMinutes - sum:+#;-#;0} to go).";
                return false;
            }

            return true;
        }


        private void Refresh()
        {
            bool valid = TryBuild(out _, out string problem);
            ok.IsEnabled = valid;
            status.Text = valid ? "All the time is assigned." : problem;
        }


        private void Accept()
        {
            if (TryBuild(out SplitPlan plan, out _))
            {
                Result = plan;
                DialogResult = true;
            }
        }
    }
}
