using Microsoft.AspNetCore.Components;
using RegistrationSystem.Core.Application.Settings;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Web.Components.Pages.Admin;

public partial class Settings : ComponentBase, IDisposable
{
    [Inject] private SettingsService SettingsService { get; set; } = default!;

    // ═══════════════════════════════════════════════════════════════════════════
    // STATE
    // ═══════════════════════════════════════════════════════════════════════════

    private CompetitionSettings? settings;
    private CompetitionSettings? originalSnapshot;
    private GlobalRegistrationStatus? globalStatus;

    private bool isLoading = true;
    private bool isSaving;
    private string? errorMessage;
    private string? successMessage;

    private int activeTab;
    private int? pendingTabSwitch;

    // Division accordion
    private string? expandedDivisionId;

    // Edit panel state - we track WHICH item is selected, and maintain separate editing copies
    private bool editPanelOpen;
    private string? selectedDivisionId;
    private string? selectedCategoryId;
    private Division? selectedDivision;
    private Category? selectedCategory;

    // Editing copies - changes are made here, not directly on settings
    private Division? editingDivision;
    private Category? editingCategory;

    // Add forms
    private bool isAddingDivision;
    private string newDivisionName = string.Empty;
    private string? addingCategoryToDivisionId;
    private string newCategoryName = string.Empty;

    // Modals
    private bool showDeleteModal;
    private string deleteModalMessage = string.Empty;
    private Action? deleteConfirmAction;

    private bool showHierarchyModal;
    private string hierarchyModalMessage = string.Empty;

    private bool showInfoModal;
    private string infoModalTitle = string.Empty;
    private string infoModalContent = string.Empty;

    private bool showUnsavedWarningModal;

    // Unsaved edit panel modal
    private bool showUnsavedEditModal;
    private Action? pendingEditAction;

    // Timer for auto-dismiss
    private System.Threading.Timer? alertTimer;

    // ═══════════════════════════════════════════════════════════════════════════
    // DATE BINDING HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private void SetGlobalStart(string? value)
    {
        if (settings is null) return;
        settings.RegistrationStart = DateTime.TryParse(value, out var dt) ? new DateTimeOffset(dt) : null;
    }

    private void SetGlobalEnd(string? value)
    {
        if (settings is null) return;
        settings.RegistrationEnd = DateTime.TryParse(value, out var dt) ? new DateTimeOffset(dt) : null;
    }

    private string ageCutoffDateString => settings?.AgeCutoffDate.ToString("yyyy-MM-dd") ?? string.Empty;

    private void OnAgeCutoffChanged(ChangeEventArgs e)
    {
        if (settings is not null && DateOnly.TryParse(e.Value?.ToString(), out var date))
        {
            settings.AgeCutoffDate = date;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHANGE DETECTION
    // ═══════════════════════════════════════════════════════════════════════════

    private bool HasUnsavedChanges => !SettingsComparer.AreEqual(settings, originalSnapshot);

    private bool HasEditPanelChanges
    {
        get
        {
            if (editingDivision is not null && selectedDivision is not null)
            {
                return !SettingsComparer.AreDivisionsEqual(editingDivision, selectedDivision);
            }
            if (editingCategory is not null && selectedCategory is not null)
            {
                return !SettingsComparer.AreCategoriesEqual(editingCategory, selectedCategory);
            }
            return false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════════

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
    }

    private async Task ReloadAsync(string? preservedDivisionId = null, string? preservedCategoryId = null)
    {
        isLoading = true;
        ClearMessages();
        StateHasChanged();

        try
        {
            settings = await SettingsService.GetSettingsAsync();

            originalSnapshot = SettingsCloner.Clone(settings);
            globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);

            // Close edit panel on reload
            CloseEditPanel();

            // Preserve expansion if requested
            if (preservedDivisionId is not null)
                expandedDivisionId = preservedDivisionId;
        }
        catch (Exception ex)
        {
            errorMessage = $"Failed to load settings: {ex.Message}";
        }
        finally
        {
            isLoading = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SAVE
    // ═══════════════════════════════════════════════════════════════════════════

    private async Task SaveAsync()
    {
        if (settings is null || isSaving) return;

        isSaving = true;
        ClearMessages();

        try
        {
            await SettingsService.SaveSettingsAsync(settings);
            originalSnapshot = SettingsCloner.Clone(settings);
            globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);
            ShowSuccessMessage("Settings saved successfully.");

            // Close edit panel after save
            CloseEditPanel();
        }
        catch (ValidationException ex)
        {
            ShowErrorMessage(ex.Message);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to save settings: {ex.Message}");
        }
        finally
        {
            isSaving = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TABS
    // ═══════════════════════════════════════════════════════════════════════════

    private void TrySwitchTab(int tabIndex)
    {
        if (tabIndex == activeTab) return;

        // Check for unsaved edit panel changes first
        if (HasEditPanelChanges)
        {
            pendingEditAction = () =>
            {
                CloseEditPanel();
                activeTab = tabIndex;
            };
            showUnsavedEditModal = true;
            return;
        }

        // Check for unsaved global settings changes
        if (HasUnsavedChanges)
        {
            pendingTabSwitch = tabIndex;
            showUnsavedWarningModal = true;
        }
        else
        {
            activeTab = tabIndex;
            CloseEditPanel();
        }
    }

    private async Task SaveAndSwitchTab()
    {
        showUnsavedWarningModal = false;
        await SaveAsync();
        if (pendingTabSwitch.HasValue && string.IsNullOrEmpty(errorMessage))
        {
            activeTab = pendingTabSwitch.Value;
            pendingTabSwitch = null;
        }
    }

    private async Task ConfirmDiscardAndSwitchTab()
    {
        showUnsavedWarningModal = false;
        if (pendingTabSwitch.HasValue)
        {
            await ReloadAsync();
            activeTab = pendingTabSwitch.Value;
            pendingTabSwitch = null;
        }
    }

    private void CancelTabSwitch()
    {
        showUnsavedWarningModal = false;
        pendingTabSwitch = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ACCORDION
    // ═══════════════════════════════════════════════════════════════════════════

    private bool IsDivisionExpanded(string divisionId) => expandedDivisionId == divisionId;

    private void ToggleDivisionExpand(string divisionId)
    {
        expandedDivisionId = expandedDivisionId == divisionId ? null : divisionId;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EDIT PANEL - Division
    // ═══════════════════════════════════════════════════════════════════════════

    private void BeginEditDivision(Division division)
    {
        // Check if there are unsaved edits in the current panel
        if (HasEditPanelChanges)
        {
            pendingEditAction = () => DoBeginEditDivision(division);
            showUnsavedEditModal = true;
            return;
        }

        DoBeginEditDivision(division);
    }

    private void DoBeginEditDivision(Division division)
    {
        selectedDivision = division;
        selectedDivisionId = division.Id;
        selectedCategory = null;
        selectedCategoryId = null;

        // Create editing copy
        editingDivision = SettingsCloner.CloneDivision(division);
        editingCategory = null;

        editPanelOpen = true;
        expandedDivisionId = division.Id;
    }

    private void TryToggleEditingDivision()
    {
        if (editingDivision is null || settings is null) return;

        if (!editingDivision.IsEnabled)
        {
            // Trying to enable - check hierarchy
            var (allowed, reason) = SettingsService.CanEnableDivision(settings);
            if (!allowed)
            {
                hierarchyModalMessage = reason ?? "Cannot enable division.";
                showHierarchyModal = true;
                return;
            }
        }

        editingDivision.IsEnabled = !editingDivision.IsEnabled;
    }

    private async Task SaveDivisionEdits()
    {
        if (editingDivision is null || selectedDivision is null || settings is null || isSaving) return;

        isSaving = true;
        ClearMessages();

        try
        {
            SettingsCloner.ApplyDivisionChanges(editingDivision, selectedDivision);

            await SettingsService.SaveSettingsAsync(settings);

            // Update snapshot and status
            originalSnapshot = SettingsCloner.Clone(settings);
            globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);

            ShowSuccessMessage("Division saved successfully.");
            CloseEditPanel();
        }
        catch (ValidationException ex)
        {
            ShowErrorMessage(ex.Message);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to save: {ex.Message}");
        }
        finally
        {
            isSaving = false;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // EDIT PANEL - Category
    // ═══════════════════════════════════════════════════════════════════════════

    private void BeginEditCategory(Division division, Category category)
    {
        // Check if there are unsaved edits in the current panel
        if (HasEditPanelChanges)
        {
            pendingEditAction = () => DoBeginEditCategory(division, category);
            showUnsavedEditModal = true;
            return;
        }

        DoBeginEditCategory(division, category);
    }

    private void DoBeginEditCategory(Division division, Category category)
    {
        selectedDivision = division;
        selectedDivisionId = division.Id;
        selectedCategory = category;
        selectedCategoryId = category.Id;

        // Create editing copies
        editingDivision = null;
        editingCategory = SettingsCloner.CloneCategory(category);

        editPanelOpen = true;
    }

    private void TryToggleEditingCategory()
    {
        if (editingCategory is null || settings is null || selectedDivision is null) return;

        if (!editingCategory.IsEnabled)
        {
            // Trying to enable - check hierarchy
            var (allowed, reason) = SettingsService.CanEnableCategory(settings, selectedDivision.Id);
            if (!allowed)
            {
                hierarchyModalMessage = reason ?? "Cannot enable category.";
                showHierarchyModal = true;
                return;
            }
        }

        editingCategory.IsEnabled = !editingCategory.IsEnabled;
    }

    private async Task SaveCategoryEdits()
    {
        if (editingCategory is null || selectedCategory is null || settings is null || isSaving) return;

        isSaving = true;
        ClearMessages();

        try
        {
            SettingsCloner.ApplyCategoryChanges(editingCategory, selectedCategory);

            await SettingsService.SaveSettingsAsync(settings);

            // Update snapshot and status
            originalSnapshot = SettingsCloner.Clone(settings);
            globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);

            ShowSuccessMessage("Category saved successfully.");
            CloseEditPanel();
        }
        catch (ValidationException ex)
        {
            ShowErrorMessage(ex.Message);
        }
        catch (Exception ex)
        {
            ShowErrorMessage($"Failed to save: {ex.Message}");
        }
        finally
        {
            isSaving = false;
        }
    }

    private void EnableEditingCategoryOverride()
    {
        if (editingCategory is null || settings is null) return;

        editingCategory.RegistrationStart = settings.RegistrationStart;
        editingCategory.RegistrationEnd = settings.RegistrationEnd;
    }

    private void ClearEditingCategoryOverride()
    {
        if (editingCategory is null) return;

        editingCategory.RegistrationStart = null;
        editingCategory.RegistrationEnd = null;
    }

    private void SetEditingCategoryStart(string? value)
    {
        if (editingCategory is null) return;
        editingCategory.RegistrationStart = DateTimeOffset.TryParse(value, out var dt) ? dt : null;
    }

    private void SetEditingCategoryEnd(string? value)
    {
        if (editingCategory is null) return;
        editingCategory.RegistrationEnd = DateTimeOffset.TryParse(value, out var dt) ? dt : null;
    }

    private void CloseEditPanel()
    {
        editPanelOpen = false;
        selectedDivision = null;
        selectedDivisionId = null;
        selectedCategory = null;
        selectedCategoryId = null;
        editingDivision = null;
        editingCategory = null;
    }

    private void TryCloseEditPanel()
    {
        if (HasEditPanelChanges)
        {
            pendingEditAction = CloseEditPanel;
            showUnsavedEditModal = true;
            return;
        }
        CloseEditPanel();
    }

    // Pending edit action handlers
    private async Task SaveAndProceedPendingAction()
    {
        showUnsavedEditModal = false;

        // Save current edits
        if (editingDivision is not null)
        {
            await SaveDivisionEdits();
        }
        else if (editingCategory is not null)
        {
            await SaveCategoryEdits();
        }

        // If save succeeded (no error), proceed with pending action
        if (string.IsNullOrEmpty(errorMessage))
        {
            pendingEditAction?.Invoke();
        }
        pendingEditAction = null;
    }

    private void DiscardAndProceedPendingAction()
    {
        showUnsavedEditModal = false;
        CloseEditPanel();
        pendingEditAction?.Invoke();
        pendingEditAction = null;
    }

    private void CancelPendingAction()
    {
        showUnsavedEditModal = false;
        pendingEditAction = null;
    }

    // Check if a category has override saved (from original snapshot, not editing copy)
    private bool IsCategoryOverrideSaved(string categoryId)
    {
        if (originalSnapshot is null) return false;

        foreach (var div in originalSnapshot.Divisions)
        {
            var cat = div.Categories.FirstOrDefault(c => c.Id == categoryId);
            if (cat is not null)
            {
                return cat.RegistrationStart.HasValue || cat.RegistrationEnd.HasValue;
            }
        }

        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GLOBAL TOGGLE
    // ═══════════════════════════════════════════════════════════════════════════

    private void ToggleGlobalRegistration()
    {
        if (settings is null) return;
        settings.RegistrationEnabled = !settings.RegistrationEnabled;

        // Refresh status after toggle
        globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ADD DIVISION
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartAddDivision()
    {
        isAddingDivision = true;
        newDivisionName = string.Empty;
    }

    private void CancelAddDivision()
    {
        isAddingDivision = false;
        newDivisionName = string.Empty;
    }

    private void ConfirmAddDivision()
    {
        if (settings is null || string.IsNullOrWhiteSpace(newDivisionName)) return;

        var division = new Division
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = newDivisionName.Trim(),
            IsEnabled = false,
            Categories = new List<Category>()
        };

        settings.Divisions.Add(division);
        globalStatus = SettingsService.GetGlobalStatus(settings, DateTimeOffset.Now);

        isAddingDivision = false;
        newDivisionName = string.Empty;
        expandedDivisionId = division.Id;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // ADD CATEGORY
    // ═══════════════════════════════════════════════════════════════════════════

    private void StartAddCategory(string divisionId)
    {
        addingCategoryToDivisionId = divisionId;
        newCategoryName = string.Empty;
    }

    private void CancelAddCategory()
    {
        addingCategoryToDivisionId = null;
        newCategoryName = string.Empty;
    }

    private void ConfirmAddCategory(Division division)
    {
        if (string.IsNullOrWhiteSpace(newCategoryName)) return;

        var category = new Category
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = newCategoryName.Trim(),
            IsEnabled = false,
            PortionOption = PortionOption.NotApplicable
        };

        division.Categories.Add(category);
        globalStatus = SettingsService.GetGlobalStatus(settings!, DateTimeOffset.Now);

        addingCategoryToDivisionId = null;
        newCategoryName = string.Empty;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DELETE
    // ═══════════════════════════════════════════════════════════════════════════

    private void RequestDeleteDivision(Division division)
    {
        deleteModalMessage = $"Are you sure you want to delete \"{division.Name}\"? This will also delete all {division.Categories.Count} categories within it.";
        deleteConfirmAction = () =>
        {
            settings?.Divisions.Remove(division);
            globalStatus = SettingsService.GetGlobalStatus(settings!, DateTimeOffset.Now);
            CloseEditPanel();
        };
        showDeleteModal = true;
    }

    private void RequestDeleteCategory(Division division, Category category)
    {
        deleteModalMessage = $"Are you sure you want to delete \"{category.Name}\" from {division.Name}?";
        deleteConfirmAction = () =>
        {
            division.Categories.Remove(category);
            globalStatus = SettingsService.GetGlobalStatus(settings!, DateTimeOffset.Now);
            CloseEditPanel();
        };
        showDeleteModal = true;
    }

    private void ConfirmDelete()
    {
        deleteConfirmAction?.Invoke();
        CloseDeleteModal();
    }

    private void CloseDeleteModal()
    {
        showDeleteModal = false;
        deleteModalMessage = string.Empty;
        deleteConfirmAction = null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // MODALS
    // ═══════════════════════════════════════════════════════════════════════════

    private void CloseHierarchyModal() => showHierarchyModal = false;

    private void ShowInfo(string title, string content)
    {
        infoModalTitle = title;
        infoModalContent = content;
        showInfoModal = true;
    }

    private void CloseInfoModal() => showInfoModal = false;

    private void ClearMessages()
    {
        errorMessage = null;
        successMessage = null;
        alertTimer?.Dispose();
        alertTimer = null;
    }

    private void ShowSuccessMessage(string message)
    {
        successMessage = message;
        errorMessage = null;
        StartAlertTimer();
    }

    private void ShowErrorMessage(string message)
    {
        errorMessage = message;
        successMessage = null;
        StartAlertTimer();
    }

    private void StartAlertTimer()
    {
        alertTimer?.Dispose();
        alertTimer = new System.Threading.Timer(async _ =>
        {
            await InvokeAsync(() =>
            {
                ClearMessages();
                StateHasChanged();
            });
        }, null, 5000, Timeout.Infinite);
    }

    public void Dispose()
    {
        alertTimer?.Dispose();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // UI HELPERS
    // ═══════════════════════════════════════════════════════════════════════════

    private string GetTabClasses(int tabIndex) =>
        activeTab == tabIndex
            ? "inline-flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium text-cyan-700 border-b-2 border-cyan-700 -mb-px"
            : "inline-flex items-center gap-1.5 px-4 py-2.5 text-sm font-medium text-slate-500 hover:text-slate-700 hover:border-b-2 hover:border-slate-300 -mb-px transition-colors";

    private string GetStatusDotClasses(CategoryRegistrationStatus status)
    {
        var baseClasses = "w-2 h-2 rounded-full";
        return status.StatusLabel switch
        {
            "Open" => $"{baseClasses} bg-cyan-500 shadow-sm shadow-cyan-500/50",
            "Not Started" => $"{baseClasses} bg-amber-500",
            "Ended" => $"{baseClasses} bg-slate-400",
            _ => $"{baseClasses} bg-slate-300"
        };
    }

    private string GetStatusBadgeClasses(CategoryRegistrationStatus status) =>
        status.StatusLabel switch
        {
            "Open" => "inline-flex items-center rounded-full bg-cyan-50 px-2 py-0.5 text-xs font-semibold text-cyan-700 border border-cyan-200",
            "Not Started" => "inline-flex items-center rounded-full bg-amber-50 px-2 py-0.5 text-xs font-semibold text-amber-700 border border-amber-200",
            "Ended" => "inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-600 border border-slate-200",
            _ => "inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-xs font-semibold text-slate-500 border border-slate-200"
        };

    private string GetStatusBadgeClassesCompact(CategoryRegistrationStatus status) =>
        status.StatusLabel switch
        {
            "Open" => "text-[0.65rem] font-semibold text-cyan-700",
            "Not Started" => "text-[0.65rem] font-semibold text-amber-600",
            "Ended" => "text-[0.65rem] font-semibold text-slate-500",
            _ => "text-[0.65rem] font-semibold text-slate-400"
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // INFO CONTENT
    // ═══════════════════════════════════════════════════════════════════════════

    private static string GetRegistrationHierarchyExplanation() => """
        <p class="mb-2">Registration availability follows a <strong>4-level hierarchy</strong>:</p>
        <ol class="list-decimal list-inside space-y-1 text-slate-600">
            <li><strong>Global</strong> — Master switch</li>
            <li><strong>Division</strong> — Must be enabled</li>
            <li><strong>Category</strong> — Must be enabled</li>
            <li><strong>Date Window</strong> — Must be within range</li>
        </ol>
        <p class="mt-2 text-slate-500 text-xs">All levels must pass for registration to be open.</p>
        """;

    private static string GetGlobalToggleExplanation() => """
        <p>The <strong>Global Registration</strong> toggle is the master switch for the entire competition.</p>
        <p class="mt-2 text-slate-500">When disabled, no registrations can be submitted regardless of division or category settings.</p>
        """;

    private static string GetRegistrationWindowExplanation() => """
        <p>The <strong>Default Registration Window</strong> defines when registration opens and closes.</p>
        <p class="mt-2">Categories can override this with their own custom dates if needed.</p>
        """;

    private static string GetAgeCutoffExplanation() => """
        <p>The <strong>Age Cutoff Date</strong> determines how competitor ages are calculated.</p>
        <p class="mt-2">For example, if the cutoff is January 1, 2025, a competitor born on January 2, 2010 would be considered 14 years old.</p>
        """;

    private static string GetScheduleOverrideExplanation() => """
        <p>A <strong>Schedule Override</strong> lets this category have different registration dates than the global default.</p>
        <p class="mt-2">Use this for categories that need to open earlier, close later, or have a completely different window.</p>
        """;

    private static string GetDivisionEnableExplanation() => """
        <p>Enabling a division requires:</p>
        <ul class="list-disc list-inside mt-2 space-y-1">
            <li>Global registration must be enabled</li>
        </ul>
        <p class="mt-2 text-slate-500">When disabled, all categories within this division will be unavailable.</p>
        """;

    private static string GetCategoryEnableExplanation() => """
        <p>Enabling a category requires:</p>
        <ul class="list-disc list-inside mt-2 space-y-1">
            <li>Global registration must be enabled</li>
            <li>Parent division must be enabled</li>
        </ul>
        <p class="mt-2 text-slate-500">Even when enabled, registration is only open during the date window.</p>
        """;
}