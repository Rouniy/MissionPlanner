using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace MissionPlanner.Tests;

/// <summary>
/// A NumericUpDown spends a fixed part of its declared width on chrome before the first glyph, so a
/// control that looks generously sized in markup can render as a bare pair of chevrons with no
/// readable value. The joystick Expo field, the connection bar baud field and the SITL swarm
/// instance count all shipped that way. This check reads the markup rather than rendering it, so it
/// costs nothing and runs without a display.
/// </summary>
public class InputSizingTests {
  /// 34px for each of the two ButtonSpinner RepeatButtons in Avalonia's Fluent theme, 1px of border
  /// a side, and the 12px of InputPad from Theme/MpTheme.axaml.
  private const double SpinnerChrome = 82;

  /// One digit at the default 14px face. Measured against a 105px baud field that rendered exactly
  /// three of the six digits in 115200.
  private const double DigitWidth = 7.8;

  private const double SignWidth = 4.5;

  /// Chrome plus two digits. Applied when Maximum is absent or bound, where the largest value cannot
  /// be known from the markup but the field still must not collapse.
  private const double CollapseFloor = 98;

  /// Maximum is often a sentinel meaning "no practical limit" - the baud field allows 10000000 - and
  /// demanding room for every digit of that would be stricter than any real value needs.
  private const int MaxDigitsEnforced = 6;

  /// <summary>
  /// Sites that predate this check, keyed by path, width and Maximum so the entry survives the lines
  /// around it moving. They are recorded rather than fixed because several sit inside fixed-width
  /// grid columns, where widening the control would push it across its neighbour instead - the exact
  /// defect <see cref="LayoutOverflowTests"/> guards. This set must only ever shrink; a stale entry
  /// fails the second test below.
  /// </summary>
  private static readonly HashSet<string> Baseline = new(StringComparer.Ordinal) {
    "GCSViews/ConfigurationView/ConfigFFTView.axaml|100|1000",
    "GCSViews/ConfigurationView/ConfigFFTView.axaml|90|14",
    "GCSViews/ConfigurationView/ConfigOSDView.axaml|90|",
    "GCSViews/ConfigurationView/ConfigRadioOutputView.axaml|66|2200",
    "Views/FormationControlWindow.axaml|85|10000",
    "Views/GeoRefView.axaml|100|60000",
    "Views/GeoRefView.axaml|110|10000",
    "Views/GridUIView.axaml|100|2200",
    "Views/GridUIView.axaml|100|359",
    "Views/GridUIView.axaml|110|1000",
    "Views/SwarmFollowPathWindow.axaml|90|10000",
    "Views/Terrain3DView.axaml|68|20",
    "Views/Terrain3DView.axaml|72|65",
    "Views/Terrain3DView.axaml|72|8",
    "Views/Terrain3DView.axaml|90|5000",
  };

  [Fact]
  public void NumericUpDown_widths_leave_room_for_their_largest_value() {
    List<string> offenders = new();

    foreach (Undersized found in FindUndersized()) {
      if (Baseline.Contains(found.Key)) {
        continue;
      }

      offenders.Add($"{found.Path}:{found.Line} Width={found.Width:0.#} needs {found.Required:0.#} "
          + $"(Maximum={(found.Maximum.Length == 0 ? "unset" : found.Maximum)})");
    }

    Assert.True(offenders.Count == 0,
        $"NumericUpDown controls too narrow to show their own value. {SpinnerChrome}px of any width "
        + "you set goes to spinner chrome and padding before the first glyph, so these truncate or "
        + "render as bare chevrons. Prefer omitting Width so the control sizes to its content."
        + Environment.NewLine
        + string.Join(Environment.NewLine, offenders));
  }

  [Fact]
  public void Undersized_input_baseline_has_no_stale_entries() {
    HashSet<string> live = FindUndersized().Select(found => found.Key).ToHashSet(StringComparer.Ordinal);
    string[] stale = Baseline.Where(key => !live.Contains(key)).OrderBy(key => key).ToArray();

    Assert.True(stale.Length == 0,
        "These inputs were widened but are still listed as known-undersized. Delete them from "
        + "Baseline so the list keeps reflecting the work that is left."
        + Environment.NewLine
        + string.Join(Environment.NewLine, stale));
  }

  private static IEnumerable<Undersized> FindUndersized() {
    string root = FindRepoRoot();

    foreach (string file in Directory.EnumerateFiles(root, "*.axaml", SearchOption.AllDirectories)) {
      string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
      if (relative.Contains("/bin/", StringComparison.Ordinal)
          || relative.Contains("/obj/", StringComparison.Ordinal)) {
        continue;
      }

      XDocument document;
      try {
        document = XDocument.Load(file, LoadOptions.SetLineInfo);
      } catch (XmlException) {
        continue;
      }

      foreach (XElement element in document.Descendants()) {
        if (element.Name.LocalName != "NumericUpDown") {
          continue;
        }

        string? widthText = (string?)element.Attribute("Width");
        if (widthText == null || !TryParse(widthText, out double width)) {
          continue;
        }

        string maximum = (string?)element.Attribute("Maximum") ?? string.Empty;
        double required = RequiredWidth(maximum, (string?)element.Attribute("Minimum"));
        if (width >= required) {
          continue;
        }

        yield return new Undersized(
            relative,
            element is IXmlLineInfo { HasLineInfo: true } info ? info.LineNumber : 0,
            width,
            maximum,
            required,
            $"{relative}|{widthText}|{maximum}");
      }
    }
  }

  private static double RequiredWidth(string maximum, string? minimum) {
    double signAllowance = TryParse(minimum, out double minimumValue) && minimumValue < 0
        ? SignWidth
        : 0;

    if (!TryParse(maximum, out double maximumValue)) {
      return CollapseFloor;
    }

    int digits = Math.Min(
        Math.Abs(maximumValue).ToString("0", CultureInfo.InvariantCulture).Length,
        MaxDigitsEnforced);

    return Math.Max(CollapseFloor, SpinnerChrome + (digits * DigitWidth) + signAllowance);
  }

  private static bool TryParse(string? text, out double value) =>
      double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

  private static string FindRepoRoot() {
    string? path = AppContext.BaseDirectory;
    while (path != null && !File.Exists(Path.Combine(path, "MissionPlanner.slnx"))) {
      path = Directory.GetParent(path)?.FullName;
    }
    return path ?? throw new DirectoryNotFoundException("Repository root not found.");
  }

  private readonly record struct Undersized(
      string Path, int Line, double Width, string Maximum, double Required, string Key);
}
