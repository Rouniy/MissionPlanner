using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MissionPlanner.Views;

namespace MissionPlanner.Tests;

/// <summary>
/// Avalonia's Grid does not clip its children and Layoutable.ArrangeCore honours a child's MinWidth
/// over the cell it was given, so a control that does not fit paints across its neighbours instead
/// of being constrained or throwing. Nothing about that is visible at a window's default size - the
/// joystick window laid out correctly at its 900px default while overlapping badly at the 640px
/// minimum it can be dragged to - so every case here renders at the narrowest supported width.
/// </summary>
public class LayoutOverflowTests {
  /// Rounding slack. Arranged bounds land on subpixel values that differ harmlessly between runs.
  private const double Tolerance = 0.5;

  /// Roughly two digits plus the text box's own padding. Below this a NumericUpDown has no readable
  /// value even though the spinner buttons still draw, which is how several shipped.
  private const double MinimumEditableWidth = 24;

  [AvaloniaTheory]
  [InlineData(640)]
  [InlineData(900)]
  public void Joystick_setup_window_arranges_without_collisions(double width) {
    JoystickSetupWindow window = new() { Width = width, Height = 700 };

    try {
      window.Show();
      Dispatcher.UIThread.RunJobs(DispatcherPriority.Layout);

      List<string> problems = new();
      CollectGridCollisions(window, problems);
      CollectCollapsedInputs(window, problems);

      Assert.True(problems.Count == 0,
          $"Layout problems in {window.GetType().Name} at {width}px:"
          + Environment.NewLine
          + string.Join(Environment.NewLine, problems));
    } finally {
      window.Close();
    }
  }

  /// <summary>
  /// Two children of the same Grid that occupy disjoint column ranges must not overlap on screen.
  /// Children deliberately sharing a cell - a ProgressBar with its value drawn centred over it, say
  /// - share a column range and are skipped, so the check stays quiet about intentional stacking.
  /// </summary>
  private static void CollectGridCollisions(Visual root, List<string> problems) {
    foreach (Grid grid in root.GetVisualDescendants().OfType<Grid>()) {
      Control[] children = grid.Children
          .OfType<Control>()
          .Where(child => child.IsVisible && child.Bounds.Width > 0)
          .ToArray();

      for (int i = 0; i < children.Length; i++) {
        for (int j = i + 1; j < children.Length; j++) {
          Control first = children[i];
          Control second = children[j];

          if (!ColumnsAreDisjoint(first, second)
              || !OverlapsBeyondRounding(first.Bounds, second.Bounds)) {
            continue;
          }

          problems.Add(
              $"{Describe(first)} and {Describe(second)} sit in different columns of a Grid but "
              + $"overlap: {first.Bounds} against {second.Bounds}");
        }
      }
    }
  }

  /// <summary>
  /// A NumericUpDown whose spinner buttons have eaten the whole control still draws its chevrons, so
  /// the defect is only visible as a text field arranged down to nothing.
  /// </summary>
  private static void CollectCollapsedInputs(Visual root, List<string> problems) {
    foreach (NumericUpDown input in root.GetVisualDescendants().OfType<NumericUpDown>()) {
      if (!input.IsVisible) {
        continue;
      }

      TextBox? field = input.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
      if (field == null || field.Bounds.Width >= MinimumEditableWidth) {
        continue;
      }

      problems.Add(
          $"NumericUpDown at {input.Bounds} has its text field arranged to "
          + $"{field.Bounds.Width:0.#}px, leaving no room to read or type a value");
    }
  }

  private static bool OverlapsBeyondRounding(Rect first, Rect second) {
    double horizontal = Math.Min(first.Right, second.Right) - Math.Max(first.Left, second.Left);
    double vertical = Math.Min(first.Bottom, second.Bottom) - Math.Max(first.Top, second.Top);
    return horizontal > Tolerance && vertical > Tolerance;
  }

  private static bool ColumnsAreDisjoint(Control first, Control second) {
    int firstStart = Grid.GetColumn(first);
    int firstEnd = firstStart + Math.Max(1, Grid.GetColumnSpan(first));
    int secondStart = Grid.GetColumn(second);
    int secondEnd = secondStart + Math.Max(1, Grid.GetColumnSpan(second));

    return firstEnd <= secondStart || secondEnd <= firstStart;
  }

  private static string Describe(Control control) {
    string name = control.Name ?? (control as ContentControl)?.Content?.ToString() ?? string.Empty;
    return name.Length == 0
        ? control.GetType().Name
        : $"{control.GetType().Name} '{name.Trim()}'";
  }
}
