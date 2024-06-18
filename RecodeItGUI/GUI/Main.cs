using ReCodeIt.AutoMapper;
using ReCodeIt.CrossPatcher;
using ReCodeIt.Enums;
using ReCodeIt.Models;
using ReCodeIt.ReMapper;
using ReCodeIt.Utils;

namespace ReCodeIt.GUI;

public partial class ReCodeItForm : Form
{
    public static ReCodeItRemapper Remapper { get; private set; } = new();
    public static ReCodeItAutoMapper AutoMapper { get; private set; } = new();

    public static ReCodeItCrossCompiler CrossPatcher { get; private set; } = new();

    private RemapModel CurrentRemap { get; set; }

    private int _selectedRemapTreeIndex = 0;

    public ReCodeItForm()
    {
        InitializeComponent();
        DataProvider.LoadMappingFile(DataProvider.Settings.Remapper.MappingPath);
        LoadedMappingFilePath.Text = DataProvider.Settings.Remapper.MappingPath;
        PopulateDomainUpDowns();
        RefreshSettingsPage();
        RefreshAutoMapperPage();
        RefreshCrossPatchPage();
        RemapTreeView.NodeMouseDoubleClick += EditSelectedRemap;

        Remapper.OnComplete += ReloadTreeView;
        ReloadTreeView(this, EventArgs.Empty);
    }

    #region MANUAL_REMAPPER

    #region BUTTONS

    #region MAIN_BUTTONS

    /// <summary>
    /// Construct a new remap when the button is pressed
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void AddRemapButton_Click(object sender, EventArgs e)
    {
        if (NewTypeName.Text == string.Empty)
        {
            MessageBox.Show("Please enter a new type name", "Invalid data");
            return;
        }

        var newRemap = new RemapModel
        {
            Succeeded = false,
            FailureReason = EFailureReason.None,
            NewTypeName = NewTypeName.Text,
            OriginalTypeName = OriginalTypeName.Text == string.Empty ? null : OriginalTypeName.Text,
            UseForceRename = RemapperUseForceRename.Checked,
            SearchParams = new SearchParams
            {
                IsPublic = IsPublicUpDown.GetEnabled(),
                IsAbstract = IsAbstractUpDown.GetEnabled(),
                IsInterface = IsInterfaceUpDown.GetEnabled(),
                IsEnum = IsEnumUpDown.GetEnabled(),
                IsNested = IsNestedUpDown.GetEnabled(),
                IsSealed = IsSealedUpDown.GetEnabled(),
                HasAttribute = HasAttributeUpDown.GetEnabled(),
                IsDerived = IsDerivedUpDown.GetEnabled(),
                HasGenericParameters = HasGenericParametersUpDown.GetEnabled(),

                ParentName = NestedTypeParentName.Text == string.Empty
                ? null
                : NestedTypeParentName.Text,

                MatchBaseClass = BaseClassIncludeTextFIeld.Text == string.Empty
                ? null
                : BaseClassIncludeTextFIeld.Text,

                IgnoreBaseClass = BaseClassExcludeTextField.Text == string.Empty
                ? null
                : BaseClassExcludeTextField.Text,

                // Constructor - TODO
                ConstructorParameterCount = ConstructorCountEnabled.GetCount(ConstuctorCountUpDown),
                MethodCount = MethodCountEnabled.GetCount(MethodCountUpDown),
                FieldCount = FieldCountEnabled.GetCount(FieldCountUpDown),
                PropertyCount = PropertyCountEnabled.GetCount(PropertyCountUpDown),
                NestedTypeCount = NestedTypeCountEnabled.GetCount(NestedTypeCountUpDown),
                IncludeMethods = GUIHelpers.GetAllEntriesFromListBox(MethodIncludeBox),
                ExcludeMethods = GUIHelpers.GetAllEntriesFromListBox(MethodExcludeBox),
                IncludeFields = GUIHelpers.GetAllEntriesFromListBox(FieldIncludeBox),
                ExcludeFields = GUIHelpers.GetAllEntriesFromListBox(FieldExcludeBox),
                IncludeProperties = GUIHelpers.GetAllEntriesFromListBox(PropertiesIncludeBox),
                ExcludeProperties = GUIHelpers.GetAllEntriesFromListBox(PropertiesExcludeBox),
                IncludeNestedTypes = GUIHelpers.GetAllEntriesFromListBox(NestedTypesIncludeBox),
                ExcludeNestedTypes = GUIHelpers.GetAllEntriesFromListBox(NestedTypesExcludeBox),
            }
        };

        var existingRemap = DataProvider.Remaps
            .Where(remap => remap.NewTypeName == newRemap.NewTypeName)
            .FirstOrDefault();

        // Handle overwriting an existing remap
        if (existingRemap != null)
        {
            var index = DataProvider.Remaps.IndexOf(existingRemap);

            DataProvider.Remaps.Remove(existingRemap);
            RemapTreeView.Nodes.RemoveAt(index);

            DataProvider.Remaps.Insert(index, newRemap);
            RemapTreeView.Nodes.Insert(index, GUIHelpers.GenerateTreeNode(newRemap, this));

            DataProvider.SaveMapping();

            CurrentRemap = existingRemap;

            ResetAll();
            return;
        }

        CurrentRemap = newRemap;
        RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(newRemap, this));
        DataProvider.Remaps.Add(newRemap);
        DataProvider.SaveMapping();

        ResetAll();
    }

    private void RemoveRemapButton_Click(object sender, EventArgs e)
    {
        DataProvider.Remaps?.RemoveAt(RemapTreeView.SelectedNode.Index);
        RemapTreeView.SelectedNode?.Remove();
        DataProvider.SaveMapping();
    }

    private void EditRemapButton_Click(object sender, EventArgs e)
    {
        EditSelectedRemap(this, null);
        DataProvider.SaveMapping();
    }

    private void RunRemapButton_Click(object sender, EventArgs e)
    {
        if (ReCodeItRemapper.IsRunning) { return; }

        if (string.IsNullOrEmpty(DataProvider.Settings.Remapper.AssemblyPath))
        {
            MessageBox.Show("Please select an assembly path", "Assembly not loaded");
            return;
        }

        Console.Clear();

        var settings = DataProvider.Settings;

        Remapper.InitializeRemap(settings.Remapper.AssemblyPath, settings.Remapper.OutputPath);
    }

    private void SaveMappingFileButton_Click(object sender, EventArgs e)
    {
        if (MessageBox.Show(
            "Are you sure?",
            "Confirmation",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Exclamation,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes)
        {
            DataProvider.SaveMapping();
        }
    }

    private void LoadMappingFileButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select a mapping file",
                "JSON Files (*.json)|*.json|JSONC Files (*.jsonc)|*.jsonc|All Files (*.*)|*.*");

        if (result == string.Empty) { return; }

        DataProvider.LoadMappingFile(result);
        DataProvider.Settings.Remapper.MappingPath = result;
        DataProvider.SaveAppSettings();

        LoadedMappingFilePath.Text = result;

        RemapTreeView.Nodes.Clear();

        foreach (var remap in DataProvider.Remaps)
        {
            RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(remap, this));
        }
    }

    private void PickAssemblyPathButton_Click_1(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select a DLL file",
                "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*");

        if (result != string.Empty)
        {
            DataProvider.Settings.Remapper.AssemblyPath = result;
            TargetAssemblyPath.Text = result;
        }
    }

    private void OutputDirectoryButton_Click_1(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFolderDialog("Select an output directory");

        if (result != string.Empty)
        {
            DataProvider.Settings.Remapper.OutputPath = result;
            RemapperOutputDirectoryPath.Text = result;
        }
    }

    private void RunAutoMapButton_Click(object sender, EventArgs e)
    {
        AutoMapper.InitializeAutoMapping();
    }

    #endregion MAIN_BUTTONS

    #region LISTBOX_BUTTONS

    private void MethodIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (!MethodIncludeBox.Items.Contains(IncludeMethodTextBox.Text))
        {
            MethodIncludeBox.Items.Add(IncludeMethodTextBox.Text);
        }
    }

    private void MethodIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (MethodIncludeBox.SelectedItem != null)
        {
            MethodIncludeBox.Items.Remove(MethodIncludeBox.SelectedItem);
        }
    }

    private void MethodExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (!MethodExcludeBox.Items.Contains(ExcludeMethodTextBox.Text))
        {
            MethodExcludeBox.Items.Add(ExcludeMethodTextBox.Text);
        }
    }

    private void MethodExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (MethodExcludeBox.SelectedItem != null)
        {
            MethodExcludeBox.Items.Remove(MethodExcludeBox.SelectedItem);
        }
    }

    private void FIeldIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (!FieldIncludeBox.Items.Contains(FieldsIncludeTextInput.Text))
        {
            FieldIncludeBox.Items.Add(FieldsIncludeTextInput.Text);
        }
    }

    private void FieldIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (FieldIncludeBox.SelectedItem != null)
        {
            FieldIncludeBox.Items.Remove(FieldIncludeBox.SelectedItem);
        }
    }

    private void FieldExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (!FieldExcludeBox.Items.Contains(FieldsExcludeTextInput.Text))
        {
            FieldExcludeBox.Items.Add(FieldsExcludeTextInput.Text);
        }
    }

    private void FieldExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (FieldExcludeBox.SelectedItem != null)
        {
            FieldExcludeBox.Items.Remove(FieldExcludeBox.SelectedItem);
        }
    }

    private void PropertiesIncludeAddButton_Click(object sender, EventArgs e)
    {
        if (!PropertiesIncludeBox.Items.Contains(PropertiesIncludeTextField.Text))
        {
            PropertiesIncludeBox.Items.Add(PropertiesIncludeTextField.Text);
        }
    }

    private void PropertiesIncludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (PropertiesIncludeBox.SelectedItem != null)
        {
            PropertiesIncludeBox.Items.Remove(PropertiesIncludeBox.SelectedItem);
        }
    }

    private void PropertiesExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (!PropertiesExcludeBox.Items.Contains(PropertiesExcludeTextField.Text))
        {
            PropertiesExcludeBox.Items.Add(PropertiesExcludeTextField.Text);
        }
    }

    private void PropertiesExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (PropertiesExcludeBox.SelectedItem != null)
        {
            PropertiesExcludeBox.Items.Remove(PropertiesExcludeBox.SelectedItem);
        }
    }

    private void NestedTypesAddButton_Click(object sender, EventArgs e)
    {
        if (!NestedTypesIncludeBox.Items.Contains(NestedTypesIncludeTextField.Text))
        {
            NestedTypesIncludeBox.Items.Add(NestedTypesIncludeTextField.Text);
        }
    }

    private void NestedTypesRemoveButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesIncludeBox.SelectedItem != null)
        {
            NestedTypesIncludeBox.Items.Remove(NestedTypesIncludeBox.SelectedItem);
        }
    }

    private void NestedTypesExlcudeAddButton_Click(object sender, EventArgs e)
    {
        if (!NestedTypesExcludeBox.Items.Contains(NestedTypesExcludeTextField.Text))
        {
            NestedTypesExcludeBox.Items.Add(NestedTypesExcludeTextField.Text);
        }
    }

    private void NestedTypesExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (NestedTypesExcludeBox.SelectedItem != null)
        {
            NestedTypesExcludeBox.Items.Remove(NestedTypesExcludeBox.SelectedItem);
        }
    }

    private void AutoMapperExcludeAddButton_Click(object sender, EventArgs e)
    {
        if (!AutoMapperTypesExcludeBox.Items.Contains(AutoMapperTypesToIgnoreTextField.Text))
        {
            DataProvider.Settings.AutoMapper.TypesToIgnore.Add(AutoMapperTypesToIgnoreTextField.Text);
            AutoMapperTypesExcludeBox.Items.Add(AutoMapperTypesToIgnoreTextField.Text);
            AutoMapperTypesToIgnoreTextField.Clear();
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperExcludeRemoveButton_Click(object sender, EventArgs e)
    {
        if (AutoMapperTypesExcludeBox.SelectedItem != null)
        {
            DataProvider.Settings.AutoMapper.TypesToIgnore.RemoveAt(AutoMapperTypesExcludeBox.SelectedIndex);
            AutoMapperTypesExcludeBox.Items.Remove(AutoMapperTypesExcludeBox.SelectedItem);
            DataProvider.SaveAppSettings();
        }
    }

    private void RunAutoRemapButton_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(DataProvider.Settings.AutoMapper.AssemblyPath))
        {
            MessageBox.Show("Please go to the settings tab and load an assembly and select and output location", "Assembly not loaded");
            return;
        }

        AutoMapper.InitializeAutoMapping();
    }

    #endregion LISTBOX_BUTTONS

    #endregion BUTTONS

    #endregion MANUAL_REMAPPER

    #region SETTINGS_TAB

    public void RefreshSettingsPage()
    {
        // Settings page
        DebugLoggingCheckbox.Checked = DataProvider.Settings.AppSettings.Debug;
        SilentModeCheckbox.Checked = DataProvider.Settings.AppSettings.SilentMode;

        // Remapper page
        TargetAssemblyPath.Text = DataProvider.Settings.Remapper.AssemblyPath;
        RemapperOutputDirectoryPath.Text = DataProvider.Settings.Remapper.OutputPath;
        RenameFieldsCheckbox.Checked = DataProvider.Settings.Remapper.MappingSettings.RenameFields;
        RenamePropertiesCheckbox.Checked = DataProvider.Settings.Remapper.MappingSettings.RenameProperties;
        RemapperPublicicize.Checked = DataProvider.Settings.Remapper.MappingSettings.Publicize;
        RemapperUnseal.Checked = DataProvider.Settings.Remapper.MappingSettings.Unseal;
    }

    #region CHECKBOXES

    private void DebugLoggingCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.Debug = DebugLoggingCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void SilentModeCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.SilentMode = SilentModeCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void RenameFieldsCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.RenameFields = RenameFieldsCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void RenamePropertiesCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AppSettings.RenameProperties = RenamePropertiesCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void PublicizeCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.Remapper.MappingSettings.Publicize = PublicizeCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void UnsealCheckbox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.Remapper.MappingSettings.Unseal = UnsealCheckbox.Checked;
        DataProvider.SaveAppSettings();
    }

    #endregion CHECKBOXES

    #region UPDOWNS

    private void AutoMapperRequiredMatchesUpDown_ValueChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.RequiredMatches = (int)AutoMapperRequiredMatchesUpDown.Value;
        DataProvider.SaveAppSettings();
    }

    #endregion UPDOWNS

    #endregion SETTINGS_TAB

    #region AUTOMAPPER

    public void RefreshAutoMapperPage()
    {
        AutoMapperTypesExcludeBox.Items.Clear();
        AutoMapperTokensBox.Items.Clear();
        AutoMapperFPBox.Items.Clear();

        AutoMapperRequiredMatchesUpDown.Value = DataProvider.Settings.AutoMapper.RequiredMatches;
        AutoMapperMinLengthUpDown.Value = DataProvider.Settings.AutoMapper.MinLengthToMatch;
        AutoMapperSearchMethodsCheckBox.Checked = DataProvider.Settings.AutoMapper.SearchMethods;
        AutoMapperRenameFields.Checked = DataProvider.Settings.AutoMapper.MappingSettings.RenameFields;
        AutoMapperRenameProps.Checked = DataProvider.Settings.AutoMapper.MappingSettings.RenameProperties;
        AutoMapperPublicize.Checked = DataProvider.Settings.AutoMapper.MappingSettings.Publicize;
        AutoMapperUnseal.Checked = DataProvider.Settings.AutoMapper.MappingSettings.Unseal;

        foreach (var type in DataProvider.Settings.AutoMapper.TypesToIgnore)
        {
            AutoMapperTypesExcludeBox.Items.Add(type);
        }

        foreach (var token in DataProvider.Settings.AutoMapper.TokensToMatch)
        {
            AutoMapperTokensBox.Items.Add(token);
        }

        foreach (var fp in DataProvider.Settings.AutoMapper.PropertyFieldBlackList)
        {
            AutoMapperFPBox.Items.Add(fp);
        }

        foreach (var mp in DataProvider.Settings.AutoMapper.MethodParamaterBlackList)
        {
            AutoMapperMethodBox.Items.Add(mp);
        }
    }

    private void AutoMapperChooseTargetPathButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select a DLL file",
            "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*");

        if (result != string.Empty)
        {
            DataProvider.Settings.AutoMapper.AssemblyPath = result;
            TargetAssemblyPath.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperChooseOutpathButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFolderDialog("Select an output directory");

        if (result != string.Empty)
        {
            DataProvider.Settings.AutoMapper.OutputPath = result;
            RemapperOutputDirectoryPath.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperRequiredMatchesUpDown_ValueChanged_1(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.RequiredMatches = (int)AutoMapperRequiredMatchesUpDown.Value;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperMinLengthUpDown_ValueChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.MinLengthToMatch = (int)AutoMapperMinLengthUpDown.Value;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperTokensAddButton_Click(object sender, EventArgs e)
    {
        if (!AutoMapperTokensBox.Items.Contains(AutoMapperTokensTextField.Text))
        {
            AutoMapperTokensBox.Items.Add(AutoMapperTokensTextField.Text);
            DataProvider.Settings.AutoMapper.TokensToMatch.Add(AutoMapperTokensTextField.Text);

            AutoMapperTokensTextField.Clear();
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperTokensRemoveButton_Click(object sender, EventArgs e)
    {
        if (AutoMapperTokensBox.SelectedItem != null)
        {
            AutoMapperTokensBox.Items.Remove(AutoMapperTokensBox.SelectedItem);
            DataProvider.Settings.AutoMapper.TokensToMatch.RemoveAt(AutoMapperTokensBox.SelectedIndex + 1);
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperFPAddButton_Click(object sender, EventArgs e)
    {
        if (!AutoMapperFPBox.Items.Contains(AutoMapperFPTextField.Text))
        {
            AutoMapperFPBox.Items.Add(AutoMapperFPTextField.Text);
            DataProvider.Settings.AutoMapper.PropertyFieldBlackList.Add(AutoMapperFPTextField.Text);

            AutoMapperFPTextField.Clear();
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperFPRemoveButton_Click(object sender, EventArgs e)
    {
        if (AutoMapperFPBox.SelectedItem != null)
        {
            AutoMapperFPBox.Items.Remove(AutoMapperFPBox.SelectedItem);
            DataProvider.Settings.AutoMapper.PropertyFieldBlackList.RemoveAt(AutoMapperFPBox.SelectedIndex);
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperMethodAddButton_Click(object sender, EventArgs e)
    {
        if (!AutoMapperMethodBox.Items.Contains(AutoMapperMethodTextBox.Text))
        {
            AutoMapperMethodBox.Items.Add(AutoMapperMethodTextBox.Text);
            DataProvider.Settings.AutoMapper.MethodParamaterBlackList.Add(AutoMapperMethodTextBox.Text);

            AutoMapperMethodTextBox.Clear();
            DataProvider.SaveAppSettings();
        }
    }

    private void AutoMapperMethodRemoveButton_Click(object sender, EventArgs e)
    {
        if (AutoMapperMethodBox.SelectedItem != null)
        {
            AutoMapperMethodBox.Items.Remove(AutoMapperMethodBox.SelectedItem);
            DataProvider.Settings.AutoMapper.MethodParamaterBlackList.RemoveAt(AutoMapperMethodBox.SelectedIndex > 0 ? AutoMapperMethodBox.SelectedIndex : 0);
            DataProvider.SaveAppSettings();
        }
    }

    private void SearchMethodsCheckBox_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.SearchMethods = AutoMapperSearchMethodsCheckBox.Checked;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperRenameFields_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.MappingSettings.RenameFields = AutoMapperRenameFields.Checked;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperRenameProps_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.MappingSettings.RenameProperties = AutoMapperRenameProps.Checked;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperPublicize_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.MappingSettings.Publicize = AutoMapperPublicize.Checked;
        DataProvider.SaveAppSettings();
    }

    private void AutoMapperUnseal_CheckedChanged(object sender, EventArgs e)
    {
        DataProvider.Settings.AutoMapper.MappingSettings.Unseal = AutoMapperUnseal.Checked;
        DataProvider.SaveAppSettings();
    }

    #endregion AUTOMAPPER

    #region CROSS_COMPILER

    /// <summary>
    /// Remapper Input
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossPatchingOrigAssemblyButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Select the original assembly",
            "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*");

        if (result != string.Empty)
        {
            DataProvider.Settings.CrossCompiler.OriginalAssemblyPath = result;
            CrossMapperOriginalAssembly.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    /// <summary>
    /// Remapper Output
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossPatchingProjectReferncePath_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFileDialog("Output path of the Remapped assembly",
            "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*");

        if (result != string.Empty)
        {
            DataProvider.Settings.CrossCompiler.RemappedOutput = result;
            CrossMapperReferencePath.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    /// <summary>
    /// Reverse Patch Input - The input path of the assembly referenced by the remapped dll
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossPatchingProjectBuildDirButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFolderDialog("Select your visual studio solution directory");

        if (result != string.Empty)
        {
            DataProvider.Settings.CrossCompiler.VisualStudioSolutionDirectory = result;
            CrossMapperProjectBuildPath.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    /// <summary>
    /// Reverse Patch output
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void CrossMappingOutputChooseButton_Click(object sender, EventArgs e)
    {
        var result = GUIHelpers.OpenFolderDialog("Select your final build directory");

        if (result != string.Empty)
        {
            DataProvider.Settings.CrossCompiler.BuildDirectory = result;
            CrossMapperProjTargetAssembly.Text = result;
            DataProvider.SaveAppSettings();
        }
    }

    private void CrossPatchRemapButton_Click(object sender, EventArgs e)
    {
        CrossPatcher.StartRemap();
    }

    private void CrossPatchRunButton_Click(object sender, EventArgs e)
    {
        CrossPatcher.StartCrossCompile();
    }

    private void RefreshCrossPatchPage()
    {
        CrossMapperOriginalAssembly.Text = DataProvider.Settings.CrossCompiler.OriginalAssemblyPath;
        CrossMapperReferencePath.Text = DataProvider.Settings.CrossCompiler.RemappedOutput;
        CrossMapperProjectBuildPath.Text = DataProvider.Settings.CrossCompiler.VisualStudioSolutionDirectory;
        CrossMapperProjTargetAssembly.Text = DataProvider.Settings.CrossCompiler.BuildDirectory;
    }

    private void CrossCompilerNewProjectButton_Click(object sender, EventArgs e)
    {
        CrossPatcher.CreateProject();
    }

    private void CrossCompilerProjectComboBox_SelectedIndexChanged(object sender, EventArgs e)
    {
    }

    #endregion CROSS_COMPILER

    // Reset All UI elements to default
    private void ResetAll()
    {
        PopulateDomainUpDowns();

        // Text fields

        NewTypeName.Clear();
        BaseClassIncludeTextFIeld.Clear();
        BaseClassExcludeTextField.Clear();
        NestedTypeParentName.Clear();
        BaseClassExcludeTextField.Clear();
        IncludeMethodTextBox.Clear();
        ExcludeMethodTextBox.Clear();
        FieldsIncludeTextInput.Clear();
        FieldsExcludeTextInput.Clear();
        PropertiesIncludeTextField.Clear();
        PropertiesExcludeTextField.Clear();
        NestedTypesIncludeTextField.Clear();
        NestedTypesExcludeTextField.Clear();

        // Numeric UpDowns

        ConstuctorCountUpDown.Value = 0;
        MethodCountUpDown.Value = 0;
        FieldCountUpDown.Value = 0;
        PropertyCountUpDown.Value = 0;
        NestedTypeCountUpDown.Value = 0;

        // Check boxes

        RemapperUseForceRename.Checked = false;
        ConstructorCountEnabled.Checked = false;
        MethodCountEnabled.Checked = false;
        FieldCountEnabled.Checked = false;
        PropertyCountEnabled.Checked = false;
        NestedTypeCountEnabled.Checked = false;

        // List boxes

        MethodIncludeBox.Items.Clear();
        MethodExcludeBox.Items.Clear();
        FieldIncludeBox.Items.Clear();
        FieldExcludeBox.Items.Clear();
        PropertiesIncludeBox.Items.Clear();
        PropertiesExcludeBox.Items.Clear();
        NestedTypesIncludeBox.Items.Clear();
        NestedTypesExcludeBox.Items.Clear();
    }

    private void EditSelectedRemap(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e?.Node.Level != 0 || RemapTreeView?.SelectedNode?.Index < 0 || RemapTreeView?.SelectedNode?.Index == null)
        {
            return;
        }

        _selectedRemapTreeIndex = RemapTreeView.SelectedNode.Index;

        ResetAll();

        var remap = DataProvider.Remaps.ElementAt(_selectedRemapTreeIndex);

        NewTypeName.Text = remap.NewTypeName;
        OriginalTypeName.Text = remap.OriginalTypeName;
        RemapperUseForceRename.Checked = remap.UseForceRename;

        BaseClassIncludeTextFIeld.Text = remap.SearchParams.MatchBaseClass;
        BaseClassExcludeTextField.Text = remap.SearchParams.IgnoreBaseClass;
        NestedTypeParentName.Text = remap.SearchParams.ParentName;

        ConstructorCountEnabled.Checked = remap.SearchParams.ConstructorParameterCount != null ? remap.SearchParams.ConstructorParameterCount > 0 : false;
        MethodCountEnabled.Checked = remap.SearchParams.MethodCount != null ? remap.SearchParams.MethodCount > 0 : false;
        FieldCountEnabled.Checked = remap.SearchParams.FieldCount != null ? remap.SearchParams.FieldCount > 0 : false;
        PropertyCountEnabled.Checked = remap.SearchParams.PropertyCount != null ? remap.SearchParams.PropertyCount > 0 : false;
        NestedTypeCountEnabled.Checked = remap.SearchParams.NestedTypeCount != null ? remap.SearchParams.NestedTypeCount > 0 : false;

        ConstuctorCountUpDown.Value = (decimal)((remap.SearchParams.ConstructorParameterCount != null ? remap.SearchParams.ConstructorParameterCount : 0));
        MethodCountUpDown.Value = (decimal)(remap.SearchParams.MethodCount != null ? remap.SearchParams.MethodCount : 0);
        FieldCountUpDown.Value = (decimal)(remap.SearchParams.FieldCount != null ? remap.SearchParams.FieldCount : 0);
        PropertyCountUpDown.Value = (decimal)(remap.SearchParams.PropertyCount != null ? remap.SearchParams.PropertyCount : 0);
        NestedTypeCountUpDown.Value = (decimal)(remap.SearchParams.NestedTypeCount != null ? remap.SearchParams.NestedTypeCount : 0);

        IsPublicUpDown.BuildStringList("IsPublic", remap.SearchParams.IsPublic);
        IsAbstractUpDown.BuildStringList("IsAbstract", remap.SearchParams.IsAbstract);
        IsInterfaceUpDown.BuildStringList("IsInterface", remap.SearchParams.IsInterface);
        IsEnumUpDown.BuildStringList("IsEnum", remap.SearchParams.IsEnum);
        IsNestedUpDown.BuildStringList("IsNested", remap.SearchParams.IsNested);
        IsSealedUpDown.BuildStringList("IsSealed", remap.SearchParams.IsSealed);
        HasAttributeUpDown.BuildStringList("HasAttribute", remap.SearchParams.HasAttribute);
        IsDerivedUpDown.BuildStringList("IsDerived", remap.SearchParams.IsDerived);
        HasGenericParametersUpDown.BuildStringList("HasGenericParams", remap.SearchParams.HasGenericParameters);

        foreach (var method in remap.SearchParams.IncludeMethods)
        {
            MethodIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeMethods)
        {
            MethodExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeFields)
        {
            FieldIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeFields)
        {
            FieldExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeProperties)
        {
            PropertiesIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeProperties)
        {
            PropertiesExcludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.IncludeNestedTypes)
        {
            NestedTypesIncludeBox.Items.Add(method);
        }

        foreach (var method in remap.SearchParams.ExcludeNestedTypes)
        {
            NestedTypesExcludeBox.Items.Add(method);
        }
    }

    private void PopulateDomainUpDowns()
    {
        // Clear them all first just incase
        IsPublicUpDown.BuildStringList("IsPublic");
        IsAbstractUpDown.BuildStringList("IsAbstract");
        IsInterfaceUpDown.BuildStringList("IsInterface");
        IsEnumUpDown.BuildStringList("IsEnum");
        IsNestedUpDown.BuildStringList("IsNested");
        IsSealedUpDown.BuildStringList("IsSealed");
        HasAttributeUpDown.BuildStringList("HasAttribute");
        IsDerivedUpDown.BuildStringList("IsDerived");
        HasGenericParametersUpDown.BuildStringList("HasGenericParams");
    }

    #region TAB_REFRESH

    private void AutoMapperTab_Click(object sender, EventArgs e)
    {
        RefreshAutoMapperPage();
    }

    private void tabPage5_Click(object sender, EventArgs e)
    {
        RefreshCrossPatchPage();
    }

    private void SettingsTab_Click(object sender, EventArgs e)
    {
        RefreshSettingsPage();
    }

    #endregion TAB_REFRESH

    /// <summary>
    /// Subscribes the the remappers OnComplete event
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void ReloadTreeView(object sender, EventArgs e)
    {
        RemapTreeView.Nodes.Clear();

        foreach (var remap in DataProvider.Remaps)
        {
            RemapTreeView.Nodes.Add(GUIHelpers.GenerateTreeNode(remap, this));
        }
    }
}