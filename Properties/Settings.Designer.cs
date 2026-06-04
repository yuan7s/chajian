using System.Configuration;

namespace 外部程序.Properties
{
    internal sealed partial class Settings : ApplicationSettingsBase
    {
        private static readonly Settings _default = (Settings)Synchronized(new Settings());

        public static Settings Default => _default;

        [UserScopedSetting]
        [DefaultSettingValue("0")]
        public int DefaultSwProcessId
        {
            get => (int)this["DefaultSwProcessId"];
            set => this["DefaultSwProcessId"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string CodingCleanup_NameFilter
        {
            get => (string)this["CodingCleanup_NameFilter"];
            set => this["CodingCleanup_NameFilter"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool CodingCleanup_ProcessAsm
        {
            get => (bool)this["CodingCleanup_ProcessAsm"];
            set => this["CodingCleanup_ProcessAsm"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool CodingCleanup_ProcessPart
        {
            get => (bool)this["CodingCleanup_ProcessPart"];
            set => this["CodingCleanup_ProcessPart"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool CodingCleanup_ExcludeVirtual
        {
            get => (bool)this["CodingCleanup_ExcludeVirtual"];
            set => this["CodingCleanup_ExcludeVirtual"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool CodingCleanup_ExcludeStandard
        {
            get => (bool)this["CodingCleanup_ExcludeStandard"];
            set => this["CodingCleanup_ExcludeStandard"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool CodingCleanup_ExcludePurchased
        {
            get => (bool)this["CodingCleanup_ExcludePurchased"];
            set => this["CodingCleanup_ExcludePurchased"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("0.25")]
        public double Form5_Opacity
        {
            get => (double)this["Form5_Opacity"];
            set => this["Form5_Opacity"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("自适应")]
        public string Form5_ColorScheme
        {
            get => (string)this["Form5_ColorScheme"];
            set => this["Form5_ColorScheme"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Form5_TopMost
        {
            get => (bool)this["Form5_TopMost"];
            set => this["Form5_TopMost"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string Form5_KeyProperties
        {
            get => (string)this["Form5_KeyProperties"];
            set => this["Form5_KeyProperties"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool Form5_MouseThrough
        {
            get => (bool)this["Form5_MouseThrough"];
            set => this["Form5_MouseThrough"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Form5_ShowKeyOnly
        {
            get => (bool)this["Form5_ShowKeyOnly"];
            set => this["Form5_ShowKeyOnly"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool Form5_ShowCustomProps
        {
            get => (bool)this["Form5_ShowCustomProps"];
            set => this["Form5_ShowCustomProps"] = value;
        }
    }
}
