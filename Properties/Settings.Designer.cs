using System.Configuration;

namespace ExternalProgram.Properties
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

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowOpenFolder
        {
            get => (bool)this["Toolbar_ShowOpenFolder"];
            set => this["Toolbar_ShowOpenFolder"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowPartCoding
        {
            get => (bool)this["Toolbar_ShowPartCoding"];
            set => this["Toolbar_ShowPartCoding"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowDrawingSaveDwg
        {
            get => (bool)this["Toolbar_ShowDrawingSaveDwg"];
            set => this["Toolbar_ShowDrawingSaveDwg"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowDrawingSavePdf
        {
            get => (bool)this["Toolbar_ShowDrawingSavePdf"];
            set => this["Toolbar_ShowDrawingSavePdf"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowDrawingRotateView
        {
            get => (bool)this["Toolbar_ShowDrawingRotateView"];
            set => this["Toolbar_ShowDrawingRotateView"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowDrawingIso
        {
            get => (bool)this["Toolbar_ShowDrawingIso"];
            set => this["Toolbar_ShowDrawingIso"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowDrawingSettings
        {
            get => (bool)this["Toolbar_ShowDrawingSettings"];
            set => this["Toolbar_ShowDrawingSettings"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyCodingCleanup
        {
            get => (bool)this["Toolbar_ShowAssemblyCodingCleanup"];
            set => this["Toolbar_ShowAssemblyCodingCleanup"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblySort
        {
            get => (bool)this["Toolbar_ShowAssemblySort"];
            set => this["Toolbar_ShowAssemblySort"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyTreeSettings
        {
            get => (bool)this["Toolbar_ShowAssemblyTreeSettings"];
            set => this["Toolbar_ShowAssemblyTreeSettings"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyDeleteCustomProps
        {
            get => (bool)this["Toolbar_ShowAssemblyDeleteCustomProps"];
            set => this["Toolbar_ShowAssemblyDeleteCustomProps"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyDeleteConfigProps
        {
            get => (bool)this["Toolbar_ShowAssemblyDeleteConfigProps"];
            set => this["Toolbar_ShowAssemblyDeleteConfigProps"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyRename
        {
            get => (bool)this["Toolbar_ShowAssemblyRename"];
            set => this["Toolbar_ShowAssemblyRename"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyPropertyOverlay
        {
            get => (bool)this["Toolbar_ShowAssemblyPropertyOverlay"];
            set => this["Toolbar_ShowAssemblyPropertyOverlay"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Toolbar_ShowAssemblyPropertySettings
        {
            get => (bool)this["Toolbar_ShowAssemblyPropertySettings"];
            set => this["Toolbar_ShowAssemblyPropertySettings"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string Toolbar_ButtonLayout
        {
            get => (string)this["Toolbar_ButtonLayout"];
            set => this["Toolbar_ButtonLayout"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Rename_WriteFileName
        {
            get => (bool)this["Rename_WriteFileName"];
            set => this["Rename_WriteFileName"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Rename_WriteMaterialCode
        {
            get => (bool)this["Rename_WriteMaterialCode"];
            set => this["Rename_WriteMaterialCode"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Rename_WritePartNumber
        {
            get => (bool)this["Rename_WritePartNumber"];
            set => this["Rename_WritePartNumber"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Rename_CopyDrawing
        {
            get => (bool)this["Rename_CopyDrawing"];
            set => this["Rename_CopyDrawing"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool Rename_WriteBlankSize
        {
            get => (bool)this["Rename_WriteBlankSize"];
            set => this["Rename_WriteBlankSize"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool Rename_WriteDesign
        {
            get => (bool)this["Rename_WriteDesign"];
            set => this["Rename_WriteDesign"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string Rename_DesignText
        {
            get => (string)this["Rename_DesignText"];
            set => this["Rename_DesignText"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("False")]
        public bool Rename_WriteVersion
        {
            get => (bool)this["Rename_WriteVersion"];
            set => this["Rename_WriteVersion"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("A")]
        public string Rename_VersionText
        {
            get => (string)this["Rename_VersionText"];
            set => this["Rename_VersionText"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("configuration")]
        public string Rename_PropertyTarget
        {
            get => (string)this["Rename_PropertyTarget"];
            set => this["Rename_PropertyTarget"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("")]
        public string Rename_CustomProperties
        {
            get => (string)this["Rename_CustomProperties"];
            set => this["Rename_CustomProperties"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Sort_AssemblyFirst
        {
            get => (bool)this["Sort_AssemblyFirst"];
            set => this["Sort_AssemblyFirst"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Sort_SuppressedLast
        {
            get => (bool)this["Sort_SuppressedLast"];
            set => this["Sort_SuppressedLast"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Sort_SortFolders
        {
            get => (bool)this["Sort_SortFolders"];
            set => this["Sort_SortFolders"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("True")]
        public bool Sort_RecursiveSubAssemblies
        {
            get => (bool)this["Sort_RecursiveSubAssemblies"];
            set => this["Sort_RecursiveSubAssemblies"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("ComponentName")]
        public string Sort_NameSource
        {
            get => (string)this["Sort_NameSource"];
            set => this["Sort_NameSource"] = value;
        }

        [UserScopedSetting]
        [DefaultSettingValue("Ascending")]
        public string Sort_Direction
        {
            get => (string)this["Sort_Direction"];
            set => this["Sort_Direction"] = value;
        }
    }
}
