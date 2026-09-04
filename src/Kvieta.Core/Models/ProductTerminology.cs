namespace Kvieta.Core.Models;

public static class ProductTerminology
{
    private static readonly IReadOnlyDictionary<string, LocalizedProductText> Texts =
        new Dictionary<string, LocalizedProductText>(StringComparer.Ordinal)
        {
            ["ChooseModeTitle"] = new("Kvieta'yı nasıl kullanacaksın?", "How will you use Kvieta?"),
            ["ChooseModeDescription"] = new(
                "Kullanımını anlayabilir, kendi düzenini kurabilir veya bir aile üyesi için korumalı kurallar oluşturabilirsin.",
                "Understand your usage, build your own routine, or create protected rules for a family member."),
            ["InsightsMode"] = new("Kullanımımı görmek istiyorum", "I want to understand my usage"),
            ["InsightsModeDescription"] = new(
                "Hiçbir şeyi kısıtlamadan hangi uygulamayı ne kadar kullandığını anla.",
                "See which applications you use and for how long without restricting anything."),
            ["InsightsModeHint"] = new(
                "Engel, günlük limit ve zorunlu mola yoktur. Veriler yalnızca bu cihazda tutulur.",
                "No blocking, daily limits, or forced breaks. Data stays on this device."),
            ["PersonalMode"] = new("Kendi düzenimi kurmak istiyorum", "I want to build my own routine"),
            ["PersonalModeDescription"] = new(
                "Odaklanmak ve kendi alışkanlıklarını düzenlemek için. Yönetici PIN'i zorunlu değildir.",
                "For focus and managing your own habits. An administrator PIN is optional."),
            ["PersonalModeHint"] = new(
                "Kural gevşetmeleri seçtiğin bekleme süresiyle uygulanır.",
                "Rule relaxations use the waiting period you choose."),
            ["FamilyMode"] = new("Bir aile üyesi için kuruyorum", "I'm setting up for a family member"),
            ["FamilyModeDescription"] = new(
                "Çocuk veya başka bir aile üyesinin Windows hesabı için. Ayarlar ve çıkış yönetici PIN'iyle korunur.",
                "For a child's or another family member's Windows account. Settings and exit are protected by an administrator PIN."),
            ["FamilyModeHint"] = new(
                "Bu kullanım biçiminde yönetici PIN'i zorunludur.",
                "An administrator PIN is required in this mode."),
            ["InsightsModeShort"] = new("Farkındalık", "Insights"),
            ["PersonalModeShort"] = new("Kişisel", "Personal"),
            ["FamilyModeShort"] = new("Aile", "Family"),
            ["PersonalProtected"] = new("Korumalı", "Protected"),
            ["PersonalProtectedDescription"] = new(
                "Kvieta kapatılırsa Guardian yeniden açar ve kuralları ayakta tutar.",
                "Guardian restores Kvieta if it is closed and keeps rules running."),
            ["PersonalProtectedHint"] = new(
                "PIN gerekmez. Daha düşük seviyeye geçiş seçtiğin bekleme süresinden sonra uygulanır.",
                "No PIN required. Moving to a lower level uses your selected waiting period.")
        };

    public static IEnumerable<KeyValuePair<string, string>> GetResources(LanguagePreference language) =>
        Texts.Select(item => KeyValuePair.Create(item.Key, item.Value.Get(language)));

    public static string Get(string key, LanguagePreference language) =>
        Texts.TryGetValue(key, out LocalizedProductText? text)
            ? text.Get(language)
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown product terminology key.");

    private sealed record LocalizedProductText(string Turkish, string English)
    {
        public string Get(LanguagePreference language) =>
            language == LanguagePreference.English ? English : Turkish;
    }
}
