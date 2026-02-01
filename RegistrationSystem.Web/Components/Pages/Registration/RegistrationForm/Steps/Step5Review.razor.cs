using Microsoft.AspNetCore.Components;
using RegistrationSystem.Core.Domain.Settings;
using RegistrationSystem.Core.ReferenceData;

namespace RegistrationSystem.Web.Components.Pages.Registration.Steps;

public partial class Step5Review
{
    [Parameter, EditorRequired] public RegistrationFormState FormState { get; set; } = null!;
    [Parameter] public CompetitionSettings? Settings { get; set; }
    [Parameter] public Division? SelectedDivision { get; set; }
    [Parameter] public Category? SelectedCategory { get; set; }
    [Parameter] public Country? SelectedCountry { get; set; }
    [Parameter] public EventCallback<int> OnGoToStep { get; set; }
    [Parameter] public EventCallback OnSubmit { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }
    [Parameter] public bool IsSubmitting { get; set; }
}