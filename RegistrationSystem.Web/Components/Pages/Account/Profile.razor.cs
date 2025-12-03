using Microsoft.AspNetCore.Components;
using RegistrationSystem.Core.Application.Users;
using RegistrationSystem.Core.Domain.Users;
using RegistrationSystem.Web.Services;

namespace RegistrationSystem.Web.Components.Pages.Account;

public partial class Profile : ComponentBase, IDisposable
{
    [Inject] private CurrentUserService CurrentUserService { get; set; } = default!;
    [Inject] private UserService UserService { get; set; } = default!;

    private User? user;
    private bool isLoading = true;
    private bool isEditing;
    private bool isSaving;
    private bool isAdmin;
    private bool showLogoutWarning;
    private string? successMessage;
    private string? errorMessage;

    // Edit form fields
    private string editFirstName = string.Empty;
    private string editLastName = string.Empty;
    private string editDisplayName = string.Empty;

    // Timer for auto-dismiss alerts
    private System.Threading.Timer? alertTimer;

    protected override async Task OnInitializedAsync()
    {
        await LoadUserAsync();
    }

    private async Task LoadUserAsync()
    {
        isLoading = true;
        try
        {
            user = await CurrentUserService.GetCurrentUserAsync();
            isAdmin = user?.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase) == true;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load profile: {ex.Message}");
        }
        finally
        {
            isLoading = false;
        }
    }

    private void StartEditing()
    {
        if (user is null || !isAdmin) return;

        editFirstName = user.FirstName;
        editLastName = user.LastName;
        editDisplayName = user.DisplayName;
        isEditing = true;
        ClearMessages();
    }

    private void CancelEditing()
    {
        isEditing = false;
        showLogoutWarning = false;
        ClearMessages();
    }

    private async Task SaveProfile()
    {
        if (user is null || isSaving || !isAdmin) return;

        // Basic validation
        if (string.IsNullOrWhiteSpace(editDisplayName))
        {
            ShowError("Display name is required.");
            return;
        }

        // Check if DisplayName is changing - show warning
        if (user.DisplayName != editDisplayName.Trim() && !showLogoutWarning)
        {
            showLogoutWarning = true;
            return;
        }

        await PerformSave();
    }

    private void CancelLogoutWarning()
    {
        showLogoutWarning = false;
    }

    private async Task ConfirmSaveWithLogout()
    {
        showLogoutWarning = false;
        await PerformSave();
    }

    private async Task PerformSave()
    {
        if (user is null) return;

        isSaving = true;
        ClearMessages();

        try
        {
            await UserService.UpdateProfileAsync(
                user.Id,
                editFirstName,
                editLastName,
                editDisplayName);

            // Refresh user data from database
            user = await CurrentUserService.RefreshCurrentUserAsync();

            isEditing = false;
            ShowSuccess("Profile updated successfully. Please sign out and sign back in to see changes reflected everywhere.");
        }
        catch (Exception ex)
        {
            ShowError($"Failed to save profile: {ex.Message}");
        }
        finally
        {
            isSaving = false;
        }
    }

    private void ShowSuccess(string message)
    {
        successMessage = message;
        errorMessage = null;
        StartAlertTimer();
    }

    private void ShowError(string message)
    {
        errorMessage = message;
        successMessage = null;
        StartAlertTimer();
    }

    private void ClearMessages()
    {
        successMessage = null;
        errorMessage = null;
        alertTimer?.Dispose();
        alertTimer = null;
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

    private static string GetInitials(User user)
    {
        if (!string.IsNullOrEmpty(user.FirstName) && !string.IsNullOrEmpty(user.LastName))
        {
            return $"{user.FirstName[0]}{user.LastName[0]}".ToUpper();
        }
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            var parts = user.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return $"{parts[0][0]}{parts[1][0]}".ToUpper();
            }
            return user.DisplayName[0].ToString().ToUpper();
        }
        if (!string.IsNullOrEmpty(user.Email))
        {
            return user.Email[0].ToString().ToUpper();
        }
        return "?";
    }

    private static string GetUserTypeBadgeClasses(string userType) => userType?.ToLowerInvariant() switch
    {
        "teacher" => "inline-flex items-center rounded-full bg-violet-50 px-2.5 py-0.5 text-xs font-semibold text-violet-700 ring-1 ring-inset ring-violet-600/20",
        "parent" => "inline-flex items-center rounded-full bg-cyan-50 px-2.5 py-0.5 text-xs font-semibold text-cyan-700 ring-1 ring-inset ring-cyan-600/20",
        "student" => "inline-flex items-center rounded-full bg-amber-50 px-2.5 py-0.5 text-xs font-semibold text-amber-700 ring-1 ring-inset ring-amber-600/20",
        _ => "inline-flex items-center rounded-full bg-slate-50 px-2.5 py-0.5 text-xs font-semibold text-slate-700 ring-1 ring-inset ring-slate-600/20"
    };

    public void Dispose()
    {
        alertTimer?.Dispose();
    }
}