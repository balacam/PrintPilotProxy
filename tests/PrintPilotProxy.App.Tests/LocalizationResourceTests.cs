using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using FluentAssertions;
using PrintPilotProxy.App.Localization;
using Xunit;

namespace PrintPilotProxy.App.Tests;

/// <summary>
/// Every UI language must translate every string. A key missing from a satellite resource silently falls back to
/// English, which is how the About page once stayed English in eleven languages.
/// </summary>
public class LocalizationResourceTests
{
    private static readonly ResourceManager Resources = new(
        "PrintPilotProxy.App.Resources.Strings",
        typeof(LocalizationService).Assembly);

    public static IEnumerable<object[]> SatelliteCultures()
        => LocalizationService.SupportedCultureNames
            .Where(name => !name.StartsWith("en", System.StringComparison.OrdinalIgnoreCase))
            .Select(name => new object[] { SatelliteCultureName(name) });

    [Theory]
    [MemberData(nameof(SatelliteCultures))]
    public void Satellite_TranslatesEveryKey(string cultureName)
    {
        var expected = Keys(CultureInfo.InvariantCulture);
        var actual = Keys(new CultureInfo(cultureName));

        expected.Except(actual).Should().BeEmpty($"the {cultureName} resources must translate every key");
        actual.Except(expected).Should().BeEmpty($"the {cultureName} resources must not contain keys the English resources lack");
    }

    // Satellites are per language ("de", "tr", ...) except Brazilian Portuguese, which has its own "pt-BR" file.
    private static string SatelliteCultureName(string supportedName)
        => supportedName == "pt-BR" ? supportedName : new CultureInfo(supportedName).Parent.Name;

    private static HashSet<string> Keys(CultureInfo culture)
    {
        var set = Resources.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        set.Should().NotBeNull($"a resource set for '{culture.Name}' should exist");
        return set!.Cast<DictionaryEntry>().Select(entry => (string)entry.Key).ToHashSet();
    }
}
