using System.Configuration;

namespace AvaloniaSpike;

public sealed class SpikeSettings : ApplicationSettingsBase
{
    public static SpikeSettings Instance { get; } = new();

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string LastValue
    {
        get => (string)this[nameof(LastValue)];
        set => this[nameof(LastValue)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("0")]
    public int RunCount
    {
        get => (int)this[nameof(RunCount)];
        set => this[nameof(RunCount)] = value;
    }
}
