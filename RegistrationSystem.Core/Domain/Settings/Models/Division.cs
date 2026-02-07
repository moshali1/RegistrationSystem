namespace RegistrationSystem.Core.Domain.Settings;

public class Division
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<Category> Categories { get; set; } = new();

    public Category? FindCategory(string categoryId) =>
        Categories.FirstOrDefault(c => c.Id == categoryId);
}
