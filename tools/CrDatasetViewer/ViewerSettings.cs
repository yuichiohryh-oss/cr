using System.Configuration;

namespace CrDatasetViewer;

internal sealed class ViewerSettings : ApplicationSettingsBase
{
    private static readonly ViewerSettings _default = (ViewerSettings)Synchronized(new ViewerSettings());

    public static ViewerSettings Default => _default;

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string LastDatasetRoot
    {
        get => (string)this[nameof(LastDatasetRoot)];
        set => this[nameof(LastDatasetRoot)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string LastMatchFolder
    {
        get => (string)this[nameof(LastMatchFolder)];
        set => this[nameof(LastMatchFolder)] = value;
    }

    [UserScopedSetting]
    [DefaultSettingValue("")]
    public string LastJsonlFile
    {
        get => (string)this[nameof(LastJsonlFile)];
        set => this[nameof(LastJsonlFile)] = value;
    }
}
