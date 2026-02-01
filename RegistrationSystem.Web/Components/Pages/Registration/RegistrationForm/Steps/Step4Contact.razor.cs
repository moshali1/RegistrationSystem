using Microsoft.AspNetCore.Components;
using RegistrationSystem.Core.ReferenceData;

namespace RegistrationSystem.Web.Components.Pages.Registration.Steps;

public partial class Step4Contact
{
    [Parameter, EditorRequired] public RegistrationFormState FormState { get; set; } = null!;
    [Parameter] public Country? SelectedCountry { get; set; }
    [Parameter] public IReadOnlyList<StateProvince> AvailableStates { get; set; } = Array.Empty<StateProvince>();
    [Parameter] public EventCallback<string> OnCountryChanged { get; set; }
    [Parameter] public EventCallback<string> OnStateChanged { get; set; }
    [Parameter] public EventCallback OnDataChanged { get; set; }
    [Parameter] public EventCallback OnNext { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private async Task HandleCountryChanged(ChangeEventArgs e)
    {
        var countryName = e.Value?.ToString() ?? string.Empty;
        await OnCountryChanged.InvokeAsync(countryName);
    }

    private async Task HandleStateChanged(ChangeEventArgs e)
    {
        var stateName = e.Value?.ToString() ?? string.Empty;
        await OnStateChanged.InvokeAsync(stateName);
    }

    private async Task OnInputChanged()
    {
        await OnDataChanged.InvokeAsync();
    }
}