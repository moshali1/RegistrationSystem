using Microsoft.AspNetCore.Components;
using RegistrationSystem.Core.Domain.Settings;

namespace RegistrationSystem.Web.Components.Pages.Registration.RegistrationForm.Steps;

public partial class Step1PersonalInfo
{
    [Parameter, EditorRequired] public RegistrationFormState FormState { get; set; } = null!;
    [Parameter] public CompetitionSettings? Settings { get; set; }
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public EventCallback OnNext { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private async Task OnInputChanged()
    {
        await OnDataChanged.InvokeAsync();
    }

    private async Task OnDateOfBirthChanged(ChangeEventArgs e)
    {
        if (DateOnly.TryParse(e.Value?.ToString(), out var date))
        {
            FormState.DateOfBirth = date;
            await OnDataChanged.InvokeAsync();
        }
    }
}